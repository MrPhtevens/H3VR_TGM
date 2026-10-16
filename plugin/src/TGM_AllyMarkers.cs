using System.Collections.Generic;
using FistVR;
using UnityEngine;
using H3MP.Networking;

namespace TeamsGameMode;

/// <summary>
/// Clients never spawn Sosigs (the host does and H3MP copies them over),
/// so friendly markers are added here to the Sosigs the client can see.
/// </summary>
public static class TGM_AllyMarkers
{
    static readonly Dictionary<Sosig, GameObject> markers = new Dictionary<Sosig, GameObject>();
    static float nextScan = 0;
    const float scanInterval = 1;

    public static void Update()
    {
        if (!Tools.ServerRunning() || Tools.IsHost())
            return;

        if (Time.time < nextScan)
            return;
        nextScan = Time.time + scanInterval;

        int localIFF = GM.CurrentPlayerBody.GetPlayerIFF();
        bool active = TGM_Settings.GetSetting(TGMSettingEnum.ShowFriendlies) == 1
            && (localIFF == TGM_Gamemode.redIFF || localIFF == TGM_Gamemode.blueIFF)
            && (TGM_Manager.gameState == TGM_Manager.GameStateEnum.Pregame
                || TGM_Manager.gameState == TGM_Manager.GameStateEnum.Gameplay);

        if (!active)
        {
            Clear();
            return;
        }

        //Drop markers on Sosigs that died, were removed or changed team
        List<Sosig> remove = new List<Sosig>();
        foreach (var pair in markers)
        {
            if (pair.Key == null
                || pair.Key.BodyState == Sosig.SosigBodyState.Dead
                || pair.Key.GetIFF() != localIFF
                || pair.Value == null)
                remove.Add(pair.Key);
        }
        for (int i = 0; i < remove.Count; i++)
        {
            if (markers[remove[i]] != null)
                Object.Destroy(markers[remove[i]]);
            markers.Remove(remove[i]);
        }

        //Mark new friendlies
        Sosig[] sosigs = Object.FindObjectsOfType<Sosig>();
        for (int i = 0; i < sosigs.Length; i++)
        {
            Sosig s = sosigs[i];
            if (s == null || markers.ContainsKey(s)
                || s.BodyState == Sosig.SosigBodyState.Dead
                || s.GetIFF() != localIFF
                || s.Links == null || s.Links.Count == 0 || s.Links[0] == null)
                continue;

            Transform head = s.Links[0].R.transform;
            GameObject arrow = Object.Instantiate(
                TGM_ModLoader.tgmAssets.iffPrefab,
                head.position + (Vector3.up * 0.75f),
                head.rotation,
                head);
            arrow.transform.localRotation = Quaternion.Euler(0, 0, 180);
            markers[s] = arrow;
        }
    }

    public static void Clear()
    {
        foreach (var marker in markers.Values)
        {
            if (marker != null)
                Object.Destroy(marker);
        }
        markers.Clear();
    }
}
