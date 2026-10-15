using System;
using FistVR;
using UnityEngine;
using H3MP.Networking;
using System.Collections.Generic;

namespace TeamsGameMode;

[Serializable]
public class TGM_Rush : TGM_Gamemode
{
    List<Rush_CapturePoint> capturePoints = new List<Rush_CapturePoint>();
    int captureRatio = 2;  //1 in X will go to attack positions (Rest goes to Objective)
    int redSpawnRatio = 0;
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
            //Sync Rush data
            if (connectionRush == null)
            {
                connectionRush = Tools.CreateCustomConnection("TGM_Rush");
                connectionRush.ServerHandlerEvent += Rush_Receiver;
                connectionRush.ClientHandlerEvent += Rush_Receiver;
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
                winIFF = blueIFF;
                TGM_Manager.instance.SetGameState(TGM_Manager.GameStateEnum.Postgame);
            }
        }

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

        //Increase player score
        if (teamIFF == GM.CurrentPlayerBody.GetPlayerIFF())
            TGM_Manager.instance.localPlayer.score += amount;

        //Network
        if (Tools.ServerRunning() && network)
        {
            if (Tools.IsHost())
            {
                TGM_Network.AdjustScores_ToClients(teamIFF, amount);
            }
            else
                TGM_Network.AdjustScores_ToServer(teamIFF, amount);
        }

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

        int iff = s.GetIFF();

        if (iff == blueIFF)
        {
            //Defenders
            TGM_Sosigs.OrderSosigToLocations(s, TGM_Manager.instance.team[iff].currentSpawnArea.GetRandomDefendArea(isReversed));
        }
        else
        {
            //ATTACKERS
            int enemyIFF = Global.GetEnemyIFF(s.GetIFF());
            if (redSpawnRatio++ >= captureRatio)
            {
                //Move to attack positions
                TGM_Sosigs.OrderSosigToLocations(s, TGM_Manager.instance.team[enemyIFF].currentSpawnArea.GetRandomAttackArea(isReversed));
                redSpawnRatio = 0;
            }
            else
            {
                //Get on the objective!
                TGM_Sosigs.OrderSosigToLocations(s, TGM_Manager.instance.team[enemyIFF].currentSpawnArea.GetObjectiveArea());
            }
        }
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

        for (int iff = 0; iff < TGM_Manager.instance.team.Length; iff++)
        {
            for (int x = 0; x < TGM_Manager.instance.team[iff].sosigs.Count; x++)
            {
                if (TGM_Manager.instance.team[iff].sosigs == null)
                    continue;
                Sosig s = TGM_Manager.instance.team[iff].sosigs[x];

                if (iff == blueIFF)
                {
                    //Blue Defenders
                    TGM_Sosigs.OrderSosigToLocations(s, TGM_Manager.instance.team[iff].currentSpawnArea.GetRandomDefendArea(isReversed));
                }
                else
                {
                    //ATTACKERS
                    int enemyIFF = Global.GetEnemyIFF(s.GetIFF());
                    if (redSpawnRatio++ >= captureRatio)
                    {
                        //Move to attack positions
                        TGM_Sosigs.OrderSosigToLocations(s, TGM_Manager.instance.team[enemyIFF].currentSpawnArea.GetRandomAttackArea(isReversed));
                        redSpawnRatio = 0;
                    }
                    else
                    {
                        //Get on the objective!
                        TGM_Sosigs.OrderSosigToLocations(s, TGM_Manager.instance.team[enemyIFF].currentSpawnArea.GetObjectiveArea());
                    }
                }
            }
        }
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
    // Gameplay
    //------------------------------------------------------------------------------

    void Rush_Receiver(int clientID, PacketData packet)
    {
        //Host doesn't get settings
        if (!Tools.ServerRunning())
            return;

        if (Tools.IsHost())
        {
            //Apply Host Update
        }
        else
        {
            //Apply Client Update
        }
    }

    public void Rush_ToServer()
    {
        if (!Tools.ServerRunning())
            return;

        PacketData packet = new PacketData(connectionRush.ToServerID);

        connectionRush.ClientToServer(packet);
    }
    public void Rush_ToClients()
    {
        if (!Tools.ServerRunning())
            return;

        PacketData packet = new PacketData(connectionRush.ToServerID);

        connectionRush.ClientToServer(packet);
    }
}