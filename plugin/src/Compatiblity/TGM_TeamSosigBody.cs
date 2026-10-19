using System;
using System.Collections.Generic;
using System.Reflection;
using FistVR;
using HarmonyLib;
using H3MP.Networking;
using UnityEngine;

namespace TeamsGameMode;

/// <summary>
/// Optional (SosigPlayerBody.TeamOutfitBody in the config): when the local player uses Packer's Sosig Player Body,
/// their body becomes one of their team's current Sosigs, so players blend in with their team.
/// Re-picked on every spawn and whenever the team's Sosig set changes as kills go up.
/// Uses Sosig Player Body's own "Apply" path (SetLocalToSosigID + SendSosigToServer), found by name at runtime.
/// </summary>
public static class TGM_TeamSosigBody
{
    const float checkInterval = 5;      //How often to check if the team's Sosig set changed
    const float minApplyInterval = 2;   //Don't re-apply faster than this

    static bool resolved = false;
    static bool available = false;
    static Type sosigBodyType;
    static FieldInfo h3mpCurrentBody;       //H3MP GameManager.currentPlayerBody
    static MethodInfo setLocalToSosigID;    //SosigPlayerBody.SetLocalToSosigID(SosigEnemyID)
    static MethodInfo sendSosigToServer;    //SPB_WristMenu.SendSosigToServer() (clients)
    static MethodInfo sendClientsSosigs;    //SPB_Networking.SendClientsSosigs_Send() (host)

    static float nextCheck = 0;
    static float lastApplied = -100;
    static string appliedSignature = null;
    static int appliedIFF = -100;

    static bool Enabled => TGM_Config.TeamOutfitBody != null && TGM_Config.TeamOutfitBody.Value;

    /// <summary>
    /// Called when the local player spawns with a class: always pick a fresh Sosig from the team
    /// </summary>
    public static void OnSpawn()
    {
        if (!Enabled)
            return;
        Apply(true);
    }

    /// <summary>
    /// Called every frame, re-applies when the team's Sosig set changes
    /// </summary>
    public static void Update()
    {
        if (!Enabled || Time.time < nextCheck)
            return;
        nextCheck = Time.time + checkInterval;

        if (TGM_Manager.gameState != TGM_Manager.GameStateEnum.Pregame
            && TGM_Manager.gameState != TGM_Manager.GameStateEnum.Gameplay)
            return;

        Apply(false);
    }

    static void Apply(bool force)
    {
        if (!Resolve())
            return;

        int iff = GM.CurrentPlayerBody.GetPlayerIFF();
        if (iff < 0 || iff >= TGM_Manager.instance.team.Length)
            return;

        List<int> ids = TGM_Manager.instance.team[iff].GetCurrentSosigIDs();
        if (ids.Count == 0)
            return;

        //Nothing changed since the last body we picked
        ids.Sort();
        string signature = string.Join(",", ids.ConvertAll(id => id.ToString()).ToArray());
        if (!force && iff == appliedIFF && signature == appliedSignature)
            return;
        if (Time.time - lastApplied < minApplyInterval)
            return;

        //Only when the local player is wearing a Sosig Player Body
        Component body = LocalSosigBody();
        if (body == null)
            return;

        int sosigID = ids[UnityEngine.Random.Range(0, ids.Count)];
        if (!IM.Instance.odicSosigObjsByID.ContainsKey((SosigEnemyID)sosigID))
            return;

        try
        {
            setLocalToSosigID.Invoke(body, new object[] { (SosigEnemyID)sosigID });

            //Tell the other players, the same way the Sosig Player Body wrist menu does
            if (Tools.ServerRunning())
            {
                if (Tools.IsHost())
                {
                    if (sendClientsSosigs != null)
                        sendClientsSosigs.Invoke(null, null);
                }
                else if (sendSosigToServer != null)
                    sendSosigToServer.Invoke(null, null);
            }

            appliedSignature = signature;
            appliedIFF = iff;
            lastApplied = Time.time;
            TGMPlugin.Logger.LogMessage("Team outfit body: now Sosig " + (SosigEnemyID)sosigID);
        }
        catch (Exception e)
        {
            TGMPlugin.Logger.LogWarning("Team outfit body: could not change body: " + (e.InnerException ?? e).Message);
        }
    }

    static Component LocalSosigBody()
    {
        Component current = h3mpCurrentBody.GetValue(null) as Component;
        if (current == null)
            return null;
        return current.GetComponent(sosigBodyType);
    }

    /// <summary>
    /// Find Sosig Player Body and H3MP by name, once
    /// </summary>
    static bool Resolve()
    {
        if (resolved)
            return available;
        resolved = true;

        sosigBodyType = AccessTools.TypeByName("SosigPlayerBody.SosigPlayerBody");
        Type wristMenu = AccessTools.TypeByName("SosigPlayerBody.SPB_WristMenu");
        Type networking = AccessTools.TypeByName("SosigPlayerBody.SPB_Networking");
        Type gameManager = AccessTools.TypeByName("H3MP.GameManager");
        if (sosigBodyType == null || gameManager == null)
        {
            TGMPlugin.Logger.LogMessage("Team outfit body: Sosig Player Body or H3MP not installed, disabled");
            return false;
        }

        h3mpCurrentBody = AccessTools.Field(gameManager, "currentPlayerBody");
        setLocalToSosigID = AccessTools.Method(sosigBodyType, "SetLocalToSosigID");
        sendSosigToServer = wristMenu != null ? AccessTools.Method(wristMenu, "SendSosigToServer") : null;
        sendClientsSosigs = networking != null ? AccessTools.Method(networking, "SendClientsSosigs_Send") : null;

        available = h3mpCurrentBody != null && setLocalToSosigID != null;
        if (!available)
            TGMPlugin.Logger.LogWarning("Team outfit body: Sosig Player Body version not supported, disabled");
        return available;
    }
}
