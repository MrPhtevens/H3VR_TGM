using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace TeamsGameMode;

/// <summary>
/// Fixes for H3MP player bodies, mainly Packer's Sosig Player Body 1.0.0 (built for H3MP 1.10) on H3MP 1.14:
/// - When H3MP sets up a remote player's body, PlayerManager.SetIFF crashed on Sosig bodies. That aborted
///   TrackedPlayerBody.Init before it connected the body's hitboxes, so that player could not be damaged by anyone else.
///   Empty body arrays are filled in before Init, and a SetIFF crash is logged instead of aborting Init.
/// - Sosig Player Body's DelaySetHostBody assumed every player had a Sosig body and had already chosen a Sosig.
/// Types are looked up by name at runtime so this does nothing if H3MP / Sosig Player Body aren't installed.
/// </summary>
public static class TGM_H3MPBodyFix
{
    static Type playerBodyType;
    static Type sosigBodyType;
    static int loggedSetIFF = 0;
    static readonly HashSet<int> fixedBodies = new HashSet<int>();

    public static void Apply(Harmony harmony)
    {
        Type trackedBodyType = AccessTools.TypeByName("H3MP.Tracking.TrackedPlayerBody");
        Type playerManagerType = AccessTools.TypeByName("H3MP.Scripts.PlayerManager");
        playerBodyType = AccessTools.TypeByName("H3MP.Scripts.PlayerBody");
        if (trackedBodyType == null || playerManagerType == null || playerBodyType == null)
        {
            TGMPlugin.Logger.LogMessage("H3MP body fix: H3MP player body types not found, skipped");
            return;
        }

        try
        {
            harmony.Patch(AccessTools.Method(trackedBodyType, "Init"),
                prefix: new HarmonyMethod(typeof(TGM_H3MPBodyFix), nameof(TrackedBodyInitPrefix)));
            harmony.Patch(AccessTools.Method(playerManagerType, "SetIFF"),
                finalizer: new HarmonyMethod(typeof(TGM_H3MPBodyFix), nameof(SetIFFFinalizer)));
            TGMPlugin.Logger.LogMessage("H3MP body fix: applied");
        }
        catch (Exception e)
        {
            TGMPlugin.Logger.LogError("H3MP body fix: failed to patch H3MP: " + e);
        }

        sosigBodyType = AccessTools.TypeByName("SosigPlayerBody.SosigPlayerBody");
        if (sosigBodyType == null)
            return;

        try
        {
            harmony.Patch(AccessTools.Method(sosigBodyType, "DelaySetHostBody"),
                prefix: new HarmonyMethod(typeof(TGM_H3MPBodyFix), nameof(DelaySetHostBodyPrefix)));
            TGMPlugin.Logger.LogMessage("H3MP body fix: Sosig Player Body sync fix applied");
        }
        catch (Exception e)
        {
            TGMPlugin.Logger.LogError("H3MP body fix: failed to patch Sosig Player Body: " + e);
        }
    }

    //------------------------------------------------------------------------------
    // Body setup
    //------------------------------------------------------------------------------

    /// <summary>
    /// Before H3MP sets up a body: fill in body arrays the prefab left empty
    /// </summary>
    static void TrackedBodyInitPrefix(object __instance)
    {
        try
        {
            Component body = Traverse.Create(__instance).Field("physicalPlayerBody").GetValue() as Component;
            if (body != null)
                FixBodyArrays(body);
        }
        catch (Exception e)
        {
            TGMPlugin.Logger.LogWarning("H3MP body fix: could not check body: " + e.Message);
        }
    }

    /// <summary>
    /// Null arrays become empty, and empty entity/hitbox lists are filled from the body's children
    /// </summary>
    static void FixBodyArrays(Component body)
    {
        List<string> changed = new List<string>();
        foreach (FieldInfo field in playerBodyType.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!field.FieldType.IsArray)
                continue;

            Type element = field.FieldType.GetElementType();
            Array value = field.GetValue(body) as Array;
            bool findInChildren = (field.Name == "entities" || field.Name == "hitboxes")
                && typeof(Component).IsAssignableFrom(element);

            if (findInChildren && (value == null || value.Length == 0))
            {
                Component[] found = body.GetComponentsInChildren(element, true);
                if (found.Length > 0)
                {
                    Array filled = Array.CreateInstance(element, found.Length);
                    Array.Copy(found, filled, found.Length);
                    field.SetValue(body, filled);
                    changed.Add(field.Name + " (found " + found.Length + ")");
                    continue;
                }
            }

            if (value == null)
            {
                field.SetValue(body, Array.CreateInstance(element, 0));
                changed.Add(field.Name + " (was null)");
            }
        }

        //Log once per body so the cause shows up in the log
        if (changed.Count > 0 && fixedBodies.Add(body.GetInstanceID()))
            TGMPlugin.Logger.LogWarning("H3MP body fix: " + body.GetType().Name + " '" + body.name + "' had empty fields: " + string.Join(", ", changed.ToArray()));
    }

    /// <summary>
    /// If SetIFF still crashes, log it and let H3MP carry on (Init still has to connect the hitboxes)
    /// </summary>
    static Exception SetIFFFinalizer(Exception __exception)
    {
        if (__exception != null && loggedSetIFF++ < 5)
            TGMPlugin.Logger.LogWarning("H3MP body fix: PlayerManager.SetIFF failed, continuing body setup anyway: " + __exception);
        return null;
    }

    //------------------------------------------------------------------------------
    // Sosig Player Body sync
    //------------------------------------------------------------------------------

    /// <summary>
    /// Replace Sosig Player Body's DelaySetHostBody with a version that skips players
    /// without a Sosig body or without a chosen Sosig, instead of throwing
    /// </summary>
    static bool DelaySetHostBodyPrefix(ref IEnumerator __result)
    {
        __result = SafeDelaySetHostBody();
        return false;
    }

    static IEnumerator SafeDelaySetHostBody()
    {
        yield return new WaitForSeconds(1);

        try
        {
            Type gameManager = AccessTools.TypeByName("H3MP.GameManager");
            Type networking = AccessTools.TypeByName("SosigPlayerBody.SPB_Networking");
            IDictionary players = AccessTools.Field(gameManager, "players").GetValue(null) as IDictionary;
            IDictionary playerSosigs = AccessTools.Field(networking, "playerSosigs").GetValue(null) as IDictionary;
            Array colors = AccessTools.Field(gameManager, "colors").GetValue(null) as Array;
            MethodInfo setClothing = AccessTools.Method(networking, "SetPlayerClothing");
            MethodInfo setColor = AccessTools.Method(playerBodyType, "SetColor");
            if (players == null || playerSosigs == null)
                yield break;

            foreach (DictionaryEntry player in players)
            {
                object trackedBody = Traverse.Create(player.Value).Field("playerBody").GetValue();
                if (trackedBody == null)
                    continue;
                Component body = Traverse.Create(trackedBody).Field("physicalPlayerBody").GetValue() as Component;

                //Default H3MP body, or that player hasn't sent their Sosig yet
                if (body == null || !sosigBodyType.IsInstanceOfType(body) || !playerSosigs.Contains(player.Key))
                    continue;

                setClothing.Invoke(null, new object[] { playerSosigs[player.Key] });

                int colorIndex = Traverse.Create(player.Value).Field("colorIndex").GetValue<int>();
                if (colors != null && setColor != null && colorIndex >= 0 && colorIndex < colors.Length)
                    setColor.Invoke(body, new object[] { colors.GetValue(colorIndex) });
            }
        }
        catch (Exception e)
        {
            TGMPlugin.Logger.LogWarning("H3MP body fix: Sosig body sync failed: " + e.Message);
        }
    }
}
