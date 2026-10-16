using System;
using FistVR;
using UnityEngine;
using H3MP.Networking;
using System.Collections.Generic;
using UnityEngine.AI;

namespace TeamsGameMode;

[Serializable]
public class TGM_Rush : TGM_Gamemode
{
    List<Rush_CapturePoint> capturePoints = new List<Rush_CapturePoint>();
    bool isReversed = false;

    public TGM_Rush(string modeName = "", string modeDescription = "", Sprite modeThumbnail = null)
    {
        name = modeName;
        description = modeDescription;
        thumbnail = modeThumbnail;
    }

    public override void LoadDefaultProfile()
    {
        base.LoadDefaultProfile();
        //Do Gamemode Settings here
        TGM_Settings.SetSetting(TGMSettingEnum.TimeLimit, 720);

    }

    private bool IsSetup = false;

    public override void Setup()
    {
        base.Setup();
        //Defaults
        for (int i = 0; i < TGM_Manager.instance.team.Length; i++)
        {
            if(TGM_Manager.instance.team[i].scoreGoal == -1)
                TGM_Manager.instance.team[i].scoreGoal = TGM_Scene.instance.areas.Length - 1;
        }
        TGM_MainMenu.instance.UpdateSettings();

        if (!IsSetup)
        {
            IsSetup = true;
            TGM_Settings.gamemodeSettings = new List<TGM_Settings.Setting>() 
            {
                new TGM_Settings.Setting
                {
                    description = "Reverse Mode:",
                    settings = ["Disabled", "Enabled"],
                    type = TGM_Settings.Setting.SettingType.Strings,
                    value = 0,
                    intMin = 0,
                    intMax = 1,
                    intIncrement = 1,
                    localOnly = false,
                }
            };

            TGM_MainMenu.instance.SetupGamemodeSettings();
        }

        //Hide Objective UI
        for (int i = 0; i < TGM_TeamSetup.instance.teamObjectiveAdjust.Length; i++)
        {
            TGM_TeamSetup.instance.teamObjectiveAdjust[i].SetActive(false);
        }

        //H3MP
        if (Tools.ServerRunning())
        {
            //Sync Rush data (static receiver, the gamemode instance changes on every scene load)
            if (connectionRush == null)
            {
                connectionRush = Tools.CreateCustomConnection("TGM_Rush");
                connectionRush.ServerHandlerEvent += Rush_ServerReceiver;
                connectionRush.ClientHandlerEvent += Rush_ClientReceiver;
            }
        }
    }

    public override void Pregame()
    {
        base.Pregame();

        //Blue instantly spawns:
        TGM_Scene.instance.teams[blueIFF].teamSpawnTime = 1;

        //Area Setup
        SetupAreas();

        //Clear old Radios
        for (int i = 0; i < capturePoints.Count; i++)
        {
            if (capturePoints[i] != null)
            {
                if (capturePoints[i].spawnedPrefab != null)
                    TGM_Manager.Destroy(capturePoints[i].spawnedPrefab);
                TGM_Manager.Destroy(capturePoints[i].gameObject);
            }
        }
        capturePoints.Clear();

        //All Blu Areas get a Radio
        for (int i = 0; i < TGM_Scene.instance.areas.Length; i++)
        {
            if (TGM_Scene.instance.areas[i].iff == blueIFF)
            {
                GameObject prefab = TGM_Scene.instance.rushCapturePrefab;

                Rush_CapturePoint capturePoint = TGM_Manager.Instantiate(
                    prefab,
                    TGM_Scene.instance.areas[i].objective.position,
                    TGM_Scene.instance.areas[i].objective.rotation).GetComponent<Rush_CapturePoint>();

                capturePoint.pointIndex = capturePoints.Count;  //Same order on host and clients
                capturePoints.Add(capturePoint);

                //Enable first capture Point
                if (TGM_Scene.instance.areas[i] == TGM_Manager.instance.team[blueIFF].currentSpawnArea)
                    capturePoint.canCapture = true;
            }
        }

        TGM_Scene.UpdateAllAreas();
    }

    public override void GameplayStart()
    {
        //Set spawn time back to the same as Red
        TGM_Scene.instance.teams[blueIFF].teamSpawnTime = TGM_Scene.instance.teams[redIFF].teamSpawnTime;
    }

    public override void Postgame()
    {
        base.Postgame();

        int localIFF = GM.CurrentPlayerBody.GetPlayerIFF();

        if (winIFF == -1)
            TGM_Manager.PlayAudio(TGM_Manager.PlayAudioEnum.TeamDraw);
        else if (localIFF == winIFF || localIFF < 0)
            TGM_Manager.PlayAudio(TGM_Manager.PlayAudioEnum.TeamWon);
        else
            TGM_Manager.PlayAudio(TGM_Manager.PlayAudioEnum.TeamLost);
    }

    public override void GameOver()
    {
        base.GameOver();
    }

    public override bool IsGamemodeValid()
    {
        TGMPlugin.Logger.LogMessage(PluginInfo.NAME + "Set Gamemode " + name + " to " + true);

        //Do we have a prefab for Rush Radios
        if (TGM_Scene.instance.rushCapturePrefab == null)
            return false;

        //If any area does not have an objective we can
        for (int i = 0; i < TGM_Scene.instance.areas.Length; i++)
        {
            if (TGM_Scene.instance.areas[i].objective == null)
                return false;
        }

        //Checks passed
        return true;
    }

    public override void Update()
    {
        base.Update();

        float remainTime = Time.time - TGM_Manager.instance.startTime;
        TimeSpan time = TimeSpan.FromSeconds(remainTime);

        TGM_Compass.instance.gameTimeText.text = time.Minutes + ":" + (time.Seconds < 10 ? "0" + time.Seconds : time.Seconds);

        if (Tools.IsClient())
            return;

        //Only Blue gets Respawn in Pregame
        if (TGM_Manager.gameState == TGM_Manager.GameStateEnum.Pregame)
        {
            if (Time.time >= TGM_Manager.instance.team[blueIFF].respawnTime)
            {
                TGMPlugin.Logger.LogDebug("Gamemode: RespawnTime at: " + Time.time + " for team " + blueIFF);
                TGM_Manager.instance.team[blueIFF].Respawn();
                TGM_Manager.instance.team[blueIFF].respawnTime = Time.time + TGM_Scene.instance.teams[blueIFF].teamSpawnTime;
                TGM_Network.Gameplay_ToClients();
            }
        }

        //Only update during gameplay
        if (TGM_Manager.gameState != TGM_Manager.GameStateEnum.Gameplay)
            return;

        //If hit our Timelimit
        if (TGM_Settings.GetSetting(TGMSettingEnum.TimeLimit) > 0)
        {
            if (Time.time - TGM_Manager.instance.startTime >= TGM_Settings.GetSetting(TGMSettingEnum.TimeLimit))
            {
                AdjustTeamScore(redIFF, -TGM_Manager.instance.team[redIFF].currentScore);
                AdjustTeamScore(blueIFF, TGM_Manager.instance.team[blueIFF].scoreGoal);

                //Blue reaching its goal already ends the game, only end it here if that didn't happen
                if (TGM_Manager.gameState == TGM_Manager.GameStateEnum.Gameplay)
                {
                    winIFF = blueIFF;
                    TGM_Manager.instance.SetGameState(TGM_Manager.GameStateEnum.Postgame);
                }
                return;
            }
        }

        UpdateSosigAI();
        RespawnTime();
    }

    public override void OnPlayerKilled(bool killedSelf, int iff)
    {
        if (iff != redIFF && iff != blueIFF)
            return;

        base.OnPlayerKilled(killedSelf, iff);

        if (TGM_Manager.gameState != TGM_Manager.GameStateEnum.Gameplay)
            return;


        //Player Stats
        TGM_Manager.instance.localPlayer.deaths++;
        TGM_Manager.instance.localPlayer.score += killedSelf ? -1 : 1;  //Minus score if killed self
    }

    public override void OnJoinTeam(int iff)
    {
        base.OnJoinTeam(iff);
        SetMarkers();
    }

    void SetMarkers()
    {
        //Clear anything that remains
        TGM_Compass.ClearAllMarkers();

        //MARKERS
        int playerIFF = GM.CurrentPlayerBody.GetPlayerIFF();
        int enemyIFF = Global.GetEnemyIFF(playerIFF);

        int spriteIndex = playerIFF == redIFF ? (int)TGM_Compass.MarkerEnum.Attack : (int)TGM_Compass.MarkerEnum.Defend;

        //Set at Objective
        TGM_Compass.instance.CreateMarker(
            TGM_Compass.instance.markerSprites[spriteIndex],
            Color.blue,
            TGM_Manager.instance.team[blueIFF].currentSpawnArea.objective
            );
    }

    public override void AdjustTeamScore(int teamIFF, int amount, bool network = true)
    {
        if (TGM_Manager.gameState != TGM_Manager.GameStateEnum.Gameplay || teamIFF == -1)
            return;
        if (ForwardScoreToHost(teamIFF, amount, network))
            return;

        //Increase player score
        if (teamIFF == GM.CurrentPlayerBody.GetPlayerIFF())
            TGM_Manager.instance.localPlayer.score += amount;

        //Network
        if (Tools.ServerRunning() && network && Tools.IsHost())
            TGM_Network.AdjustScores_ToClients(teamIFF, amount);

        TGM_Manager.instance.team[teamIFF].currentScore += amount;

        //Score hit the goal, they win!
        if (TGM_Manager.instance.team[teamIFF].currentScore >= TGM_Manager.instance.team[teamIFF].scoreGoal)
        {
            winIFF = teamIFF;
            TGM_Manager.instance.SetGameState(TGM_Manager.GameStateEnum.Postgame);
            return;
        }

        //IF RED SCORED
        if (teamIFF == redIFF)
        {
            //Debug.Log("RED " + "A");
            //Audio Capture
            int localIFF = GM.CurrentPlayerBody.GetPlayerIFF();

            //Debug.Log("RED " + localIFF);
            //If Red or Spectator
            if (amount > 0)
            {
                if (localIFF <= redIFF)
                    TGM_Manager.PlayAudio(TGM_Manager.PlayAudioEnum.ObjectiveFriendlyCaptured);
                else
                    TGM_Manager.PlayAudio(TGM_Manager.PlayAudioEnum.ObjectiveEnemyCaptured);
            }

            //Score
            int redScore = TGM_Manager.instance.team[redIFF].currentScore;
            int blueArea = redScore + 1;
            //Debug.Log("RED score " + redScore);
            //Debug.Log("RED blue: " + blueArea);

            //Set All capture points (For Networking reasons)
            for (int i = 0; i < capturePoints.Count; i++)
            {
                //Debug.Log("RED cap " + i);
                capturePoints[i].canCapture = false;
            }

            if (blueArea <= capturePoints.Count)
            {
                //Debug.Log("CAPTURED!!! " + redScore);
                //Update Spawns
                TGM_Manager.instance.team[redIFF].currentSpawnArea = TGM_Scene.instance.areas[redScore];
                TGM_Manager.instance.team[redIFF].currentSpawnArea.iff = redIFF;
                TGM_Manager.instance.team[blueIFF].currentSpawnArea = TGM_Scene.instance.areas[blueArea];
                TGM_Manager.instance.team[blueIFF].currentSpawnArea.iff = blueIFF;

                //Captures at 1 behind blue Area
                capturePoints[redScore].canCapture = true;

                TGM_Scene.UpdateAllAreas();

                if(blueArea < capturePoints.Count)
                    UpdateSosigOrders();
            }
        }
    }

    public override void OnSosigCreate(Sosig s)
    {
        base.OnSosigCreate(s);

        Rush_CapturePoint objective = GetActiveObjective();
        if (objective != null)
            OrderSosig(s, s.GetIFF(), objective);
    }

    public override void OnSosigKilled(Sosig s)
    {
        base.OnSosigKilled(s);

        //Remove from sosig team count
        for (int i = 0; i < TGM_Manager.instance.team.Length; i++)
        {
            if (TGM_Manager.instance.team[i].sosigs.Contains(s))
                TGM_Manager.instance.team[i].sosigs.Remove(s);
        }
    }

    void UpdateSosigOrders()
    {
        //Set Player's Markers
        SetMarkers();

        //Objective moved, give everyone new orders on the next AI tick
        nextSosigOrder.Clear();
        nextAIUpdate = 0;
    }

    //------------------------------------------------------------------------------
    // Sosig AI (host only)
    //------------------------------------------------------------------------------

    const float attackerPushChance = 0.75f;   //Attackers heading for the Mcom, rest go to attack areas
    const float defenderRingChance = 0.3f;    //Defenders holding near the Mcom when nothing is happening
    const float defenderReactRange = 60;      //Defenders this close respond to the Mcom being armed
    readonly Dictionary<Sosig, float> nextSosigOrder = new Dictionary<Sosig, float>();
    float nextAIUpdate = 0;
    bool lastContested = false;

    Rush_CapturePoint GetActiveObjective()
    {
        for (int i = 0; i < capturePoints.Count; i++)
        {
            if (capturePoints[i] != null && capturePoints[i].canCapture)
                return capturePoints[i];
        }
        return null;
    }

    void UpdateSosigAI()
    {
        if (Time.time < nextAIUpdate)
            return;
        nextAIUpdate = Time.time + 1;

        Rush_CapturePoint objective = GetActiveObjective();
        if (objective == null)
            return;

        //Arming started or stopped, everyone reacts straight away
        bool contested = objective.state != Rush_CapturePoint.McomState.Idle;
        bool reactNow = contested != lastContested;
        lastContested = contested;

        //Forget Sosigs that no longer exist
        List<Sosig> stale = new List<Sosig>();
        foreach (Sosig s in nextSosigOrder.Keys)
        {
            if (s == null)
                stale.Add(s);
        }
        for (int i = 0; i < stale.Count; i++)
            nextSosigOrder.Remove(stale[i]);

        for (int iff = 0; iff < TGM_Manager.instance.team.Length; iff++)
        {
            List<Sosig> sosigs = TGM_Manager.instance.team[iff].sosigs;

            //Remove destroyed or dead Sosigs so they don't block respawns
            sosigs.RemoveAll(s => s == null || s.BodyState == Sosig.SosigBodyState.Dead);

            for (int i = 0; i < sosigs.Count; i++)
            {
                float next;
                if (!reactNow && nextSosigOrder.TryGetValue(sosigs[i], out next) && Time.time < next)
                    continue;

                OrderSosig(sosigs[i], iff, objective);
            }
        }
    }

    void OrderSosig(Sosig s, int iff, Rush_CapturePoint objective)
    {
        //Stagger re-orders so the teams don't move in lockstep
        nextSosigOrder[s] = Time.time + UnityEngine.Random.Range(10f, 20f);

        Vector3 mcom = objective.transform.position;
        bool armed = objective.state == Rush_CapturePoint.McomState.Armed
            || objective.state == Rush_CapturePoint.McomState.Disarming;
        bool contested = objective.state != Rush_CapturePoint.McomState.Idle;
        float roll = UnityEngine.Random.value;

        if (iff == redIFF)
        {
            //ATTACKERS
            if (armed)
            {
                //Protect the armed Mcom, a few stay on it
                if (roll < 0.35f)
                    CommandSosig(s, RandomRing(mcom, 0, 2), true);
                else
                    CommandSosig(s, RandomRing(mcom, 3, 10), false);
            }
            else if (roll < attackerPushChance)
            {
                //Rush the Mcom and arm it
                CommandSosig(s, RandomRing(mcom, 0, 2), true);
            }
            else
            {
                //Flank through the attack areas
                List<Vector3> area = TGM_Manager.instance.team[blueIFF].currentSpawnArea.GetRandomAttackArea(isReversed);
                CommandSosig(s, area[0], true);
            }
        }
        else
        {
            //DEFENDERS
            bool inRange = Vector3.Distance(s.transform.position, mcom) <= defenderReactRange;
            if (contested && inRange)
            {
                //Get on the Mcom to stop / disarm it
                if (roll < 0.7f)
                    CommandSosig(s, RandomRing(mcom, 0, 2), true);
                else
                    CommandSosig(s, RandomRing(mcom, 4, 10), false);
            }
            else if (roll < defenderRingChance)
            {
                //Hold near the Mcom without sitting on it
                CommandSosig(s, RandomRing(mcom, 6, 15), false);
            }
            else
            {
                List<Vector3> area = TGM_Manager.instance.team[blueIFF].currentSpawnArea.GetRandomDefendArea(isReversed);
                CommandSosig(s, area[0], false);
            }
        }
    }

    /// <summary>
    /// Assault pushes hard to the point, guard holds the point but still engages enemies
    /// </summary>
    static void CommandSosig(Sosig s, Vector3 point, bool assault)
    {
        if (s == null || s.BodyState == Sosig.SosigBodyState.Dead)
            return;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(point, out hit, 4, NavMesh.AllAreas))
            point = hit.position;

        //Already fighting, keep fighting and head here once it's over
        if (s.CurrentOrder == Sosig.SosigOrder.Skirmish)
        {
            if (assault)
                s.m_assaultPoint = point;
            else
                s.m_guardPoint = point;
            s.FallbackOrder = assault ? Sosig.SosigOrder.Assault : Sosig.SosigOrder.GuardPoint;
            return;
        }

        if (assault)
        {
            s.SetAssaultSpeed(Sosig.SosigMoveSpeed.Running);
            s.CommandAssaultPoint(point);
            s.FallbackOrder = Sosig.SosigOrder.Assault;
        }
        else
        {
            s.CommandGuardPoint(point, false);
            s.FallbackOrder = Sosig.SosigOrder.GuardPoint;
        }
    }

    static Vector3 RandomRing(Vector3 center, float minRadius, float maxRadius)
    {
        Vector2 dir = UnityEngine.Random.insideUnitCircle.normalized;
        float radius = UnityEngine.Random.Range(minRadius, maxRadius);
        return center + new Vector3(dir.x, 0, dir.y) * radius;
    }

    void SetupAreas()
    {
        //Reverse Mode
        if (!isReversed && TGM_Settings.GetModeSetting(0) == 1)
        {
            isReversed = true;
            ReverseAreas();

        }
        else if (isReversed && TGM_Settings.GetModeSetting(0) == 0)
        {
            isReversed = false;
            ReverseAreas();
        }

        //Assign all areas to Team Blue
        for (int i = 0; i < TGM_Scene.instance.areas.Length; i++)
        {
            TGM_Scene.instance.areas[i].iff = blueIFF;
        }

        //Set Red Spawn
        TGM_Manager.instance.team[redIFF].currentSpawnArea = TGM_Scene.instance.teams[redIFF].startSpawnArea;
        TGM_Manager.instance.team[redIFF].currentSpawnArea.iff = redIFF;

        //Set BLU Spawn
        if (TGM_Scene.instance.areas[1] != TGM_Manager.instance.team[redIFF].currentSpawnArea)    //If Next spot is not owned by Red
            TGM_Manager.instance.team[blueIFF].currentSpawnArea = TGM_Scene.instance.areas[1]; //2nd Area belongs to Blue
        TGM_Manager.instance.team[blueIFF].currentSpawnArea.iff = blueIFF;
    }

    void ReverseAreas()
    {
        TGM_Area[] newArray = new TGM_Area[TGM_Scene.instance.areas.Length];
        for (int i = TGM_Scene.instance.areas.Length - 1, j = 0; i >= 0; i--, j++)
        {
            newArray[j] = TGM_Scene.instance.areas[i];
        }
        TGM_Scene.instance.areas = newArray;
        //RED
        TGM_Scene.instance.teams[redIFF].startSpawnArea = TGM_Scene.instance.areas[0];
        TGM_Manager.instance.team[redIFF].currentSpawnArea = TGM_Scene.instance.areas[0];
        TGM_Scene.instance.areas[0].iff = redIFF;

        //BLUE
        int areaMax = TGM_Scene.instance.areas.Length - 1;
        TGM_Scene.instance.teams[blueIFF].startSpawnArea = TGM_Scene.instance.areas[1];
        TGM_Manager.instance.team[blueIFF].currentSpawnArea = TGM_Scene.instance.areas[areaMax];
        TGM_Scene.instance.areas[areaMax].iff = blueIFF;
    }


    // H3MP

    public static CustomConnection connectionRush;

    //------------------------------------------------------------------------------
    // Mcom
    //------------------------------------------------------------------------------

    Rush_CapturePoint GetCapturePoint(int index)
    {
        if (index < 0 || index >= capturePoints.Count)
            return null;
        return capturePoints[index];
    }

    //Host: a client's hands entered or left an Mcom
    static void Rush_ServerReceiver(int clientID, PacketData packet)
    {
        if (!Tools.ServerRunning() || !Tools.IsHost())
            return;

        int index = packet.ReadInt();
        int iff = packet.ReadInt();

        TGM_Rush rush = TGM_Manager.instance.gamemode as TGM_Rush;
        Rush_CapturePoint point = rush != null ? rush.GetCapturePoint(index) : null;
        if (point != null)
            point.ReceivePresence(clientID, iff);
    }

    //Client: Mcom state from the host
    static void Rush_ClientReceiver(int clientID, PacketData packet)
    {
        if (!Tools.ServerRunning() || Tools.IsHost())
            return;

        int index = packet.ReadInt();
        Rush_CapturePoint.McomState state = (Rush_CapturePoint.McomState)packet.ReadInt();
        float progress = packet.ReadFloat();
        float fuse = packet.ReadFloat();

        TGM_Rush rush = TGM_Manager.instance.gamemode as TGM_Rush;
        Rush_CapturePoint point = rush != null ? rush.GetCapturePoint(index) : null;
        if (point != null)
            point.ReceiveState(state, progress, fuse);
    }

    public void McomPresence_ToServer(int index, int iff)
    {
        if (!Tools.ServerRunning() || connectionRush == null)
            return;

        PacketData packet = new PacketData(connectionRush.ToServerID);
        packet.Write(index);
        packet.Write(iff);
        connectionRush.ClientToServer(packet);
    }

    public void McomState_ToClients(int index, int state, float progress, float fuse)
    {
        if (!Tools.ServerRunning() || !Tools.IsHost() || connectionRush == null)
            return;

        PacketData packet = new PacketData(connectionRush.ToClientID);
        packet.Write(index);
        packet.Write(state);
        packet.Write(progress);
        packet.Write(fuse);
        connectionRush.ServerToClients(packet);
    }
}