using System;
using System.Collections;
using System.Collections.Generic;
using FistVR;
using UnityEngine;
using H3MP.Networking;


namespace TeamsGameMode;

[Serializable]
public class TGM_Gamemode
{
    public const int redIFF = 0;
    public const int blueIFF = 1;

    public string name;
    public string description;
    public Sprite thumbnail;

    public const float gameStartDelay = 20;
    public const float gameOverDelay = 15;

    public int winIFF = -1;
    [HideInInspector]
    public int index = -1;

    public virtual void LoadDefaultProfile()
    {
        TGM_Settings.SetSetting(TGMSettingEnum.SpawnLock, 2);
        TGM_Settings.SetSetting(TGMSettingEnum.SpawnWaveTime, 0);
        TGM_Settings.SetSetting(TGMSettingEnum.TimeLimit, 0);
        //TGM_Settings.SetSetting(TGMSettingEnum.CanRespawn, 1);
        TGM_Settings.SetSetting(TGMSettingEnum.ShowFriendlies, 1);
        TGM_Settings.SetSetting(TGMSettingEnum.PlayerItemsOnDeath, 0);
        TGM_Settings.SetSetting(TGMSettingEnum.SosigWeapons, 0);
        TGM_Settings.SetSetting(TGMSettingEnum.PlayerHealth, 0);
        TGM_Settings.SetSetting(TGMSettingEnum.ItemSpawner, 0);

        //TODO Set per team values - Bots and and Objective

    }

    /// <summary>
    /// Called when gamemode is selected
    /// </summary>
    public virtual void Setup()
    {
        TGMPlugin.Logger.LogDebug($"Gamemode: Setup");
    }

    /// <summary>
    /// Called at the start of the pregame countdown
    /// </summary>
    public virtual void Pregame()
    {
        TGMPlugin.Logger.LogDebug($"Gamemode: Pregame");

        GM.CurrentSceneSettings.SosigKillEvent += TGM_Manager.instance.OnSosigKilled;
        GM.CurrentSceneSettings.SosigKillEvent += TGM_Manager.instance.gamemode.OnSosigKilled;
        GM.CurrentSceneSettings.PlayerDeathFromIFFEvent += TGM_Manager.instance.gamemode.OnPlayerKilled;
        GM.CurrentSceneSettings.PlayerDeathFromIFFEvent += TGM_Manager.instance.PlayerDeathEvent;

        //15 Seconds before game starts
        TGM_Manager.instance.StartCoroutine(TGM_Manager.instance.SetGameStateDelayed(TGM_Manager.GameStateEnum.Gameplay, gameStartDelay));

        
        //Spawn Times
        if(TGM_Settings.GetSetting(TGMSettingEnum.SpawnWaveTime) >= 1)
        {
            for (int i = 0; i < TGM_Manager.instance.team.Length; i++)
            {
                TGM_Scene.instance.teams[i].teamSpawnTime = TGM_Settings.GetSetting(TGMSettingEnum.SpawnWaveTime);
            }
        }

        //Item Spawner
        TGM_Scene.instance.itemSpawner.gameObject.SetActive(TGM_Settings.GetSetting(TGMSettingEnum.ItemSpawner) == 1 ? true : false);
    }

    /// <summary>
    /// Called at the start of the gameplay round
    /// </summary>
    public virtual void GameplayStart()
    {
        TGMPlugin.Logger.LogDebug($"Gamemode: Gameplay Start");
    }

    /// <summary>
    /// Called at the start of the post game
    /// </summary>
    public virtual void Postgame()
    {
        TGMPlugin.Logger.LogDebug($"Gamemode: Post Game");

        //Force Respawn everyone if not already spawned
        for (int i = 0; i < TGM_Manager.instance.team.Length; i++)
        {
            TGM_Manager.instance.team[i].Respawn();
        }

        //DRAW
        if (winIFF == -1)
        {
            for (int t = 0; t < TGM_Manager.instance.team.Length; t++)
            {
                //Thats it, everyone has their weapons taken away
                TGM_Manager.instance.team[t].DisarmTeam();
            }
        }
        else
        {
            //One Team won
            int lossIff = Global.GetEnemyIFF(winIFF);

            //Defeated Team has their weapons taken away
            TGM_Manager.instance.team[lossIff].DisarmTeam();
        }

        //Open All Areas
        for (int i = 0; i < TGM_Scene.instance.areas.Length; i++)
        {
            TGM_Scene.instance.areas[i].OpenArea();
        }

        //End Game Screen
        TGM_EndScreen.instance.SetEndScreen(true);

        //15 Seconds before game over
        TGM_Manager.instance.StartCoroutine(TGM_Manager.instance.SetGameStateDelayed(TGM_Manager.GameStateEnum.Gameover, gameOverDelay));
    }

    public virtual void GameOver()
    {
        TGMPlugin.Logger.LogDebug($"Gamemode: Game Over, Team Won: " + winIFF);

        TGM_Manager.PlayAudio(TGM_Manager.PlayAudioEnum.Confirm);
        TGM_Manager.LeaveTeam();

        //Clean up Sosigs
        for (int i = 0; i < TGM_Manager.instance.team.Length; i++)
        {
            TGM_Manager.instance.team[i].ClearAllTeamSosigs();
        }

        //Clear player items if no Item Spawner
        if(TGM_Settings.GetSetting(TGMSettingEnum.ItemSpawner) == 0)
            GM.CurrentPlayerBody.WipeQuickbeltContents();

        //Unsubscribe to Kill Events
        GM.CurrentSceneSettings.SosigKillEvent -= TGM_Manager.instance.OnSosigKilled;
        GM.CurrentSceneSettings.SosigKillEvent -= TGM_Manager.instance.gamemode.OnSosigKilled;
        GM.CurrentSceneSettings.PlayerDeathFromIFFEvent -= TGM_Manager.instance.gamemode.OnPlayerKilled;
        GM.CurrentSceneSettings.PlayerDeathFromIFFEvent -= TGM_Manager.instance.PlayerDeathEvent;

        //Reset the gamemode to the same one!
        TGM_MainMenu.instance.SelectGamemode(index);

        //Clear all Items
        VaultSystem.ClearExistingSaveableObjects(true);
        
        //Clear Sosig Engineer Turrets
        AutoMeater[] meats = GameObject.FindObjectsOfType<AutoMeater>();
        for (int i = 0; i < meats.Length; i++)
        {
            if (meats[i] != null)
                meats[i].KillMe();
        }

        //Clear Dispensers
        MF2_Dispenser[] dispensers = GameObject.FindObjectsOfType<MF2_Dispenser>();
        for (int i = 0; i < dispensers.Length; i++)
        {
            if (dispensers[i] != null)
                dispensers[i].DestroyMe();
        }        
    }

    /// <summary>
    /// Called each Update frame while in Gameplay Game State
    /// </summary>
    public virtual void Update()
    {
        //Only update during gameplay
        if (TGM_Manager.gameState != TGM_Manager.GameStateEnum.Gameplay)
            return;

        TimeSpan time;

        //TIME LIMIT
        if (TGM_Settings.GetSetting(TGMSettingEnum.TimeLimit) > 0)
        {
            //How long a round is
            float goal = TGM_Settings.GetSetting(TGMSettingEnum.TimeLimit);
            float playTime = Time.time - TGM_Manager.instance.startTime;

            float remainTime = (TGM_Manager.instance.startTime + goal) - playTime;
            time = TimeSpan.FromSeconds(remainTime);
        }
        else   //INFINITE
        {
            float remainTime = Time.time - TGM_Manager.instance.startTime;
            time = TimeSpan.FromSeconds(remainTime);
        }
        TGM_Compass.instance.gameTimeText.text = time.Minutes + ":" + (time.Seconds < 10 ? "0" + time.Seconds : time.Seconds);
    }

    /// <summary>
    /// Before the gamemode is added to the selectible list, check if all its critria for it to work is met
    /// </summary>
    /// <returns></returns>
    public virtual bool IsGamemodeValid()
    {
        TGMPlugin.Logger.LogDebug("Gamemode: IsGamemode " + name + " valid: " + false);
        //Check if all required data is avalible for this gamemode to work
        return false;
    }

    public virtual void OnJoinTeam(int iff)
    {
        
    }

    /// <summary>
    /// Method to adjust a teams score
    /// </summary>
    /// <param name="teamIFF"></param>
    /// <param name="amount"></param>
    public virtual void AdjustTeamScore(int teamIFF, int amount, bool network = true)
    {
        TGMPlugin.Logger.LogDebug($"Gamemode: Adjust Team:" + teamIFF + " Score: " + amount);

        if (TGM_Manager.gameState != TGM_Manager.GameStateEnum.Gameplay)
            return;
        TGM_Manager.instance.team[teamIFF].currentScore += amount;

        //Network
        if (Tools.ServerRunning() && network)
        {
            if (Tools.IsHost())
                TGM_Network.AdjustScores_ToClients(teamIFF, amount);
            else
                TGM_Network.AdjustScores_ToServer(teamIFF, amount);
        }
    }

    public virtual void OnSosigCreate(Sosig s)
    {
        TGMPlugin.Logger.LogDebug("Gamemode: OnSosigCreate");
        //Command Sosig here
    }

    /// <summary>
    /// Hook into when a Sosig is killed
    /// </summary>
    /// <param name="s"></param>
    public virtual void OnSosigKilled(Sosig s)
    {
        TGMPlugin.Logger.LogDebug("Gamemode: OnSosigKilled");

        if (TGM_Settings.GetSetting(TGMSettingEnum.SosigWeapons) == 0)
            s.DestroyAllHeldObjects();
        s.ClearSosig();

        if (TGM_Manager.gameState != TGM_Manager.GameStateEnum.Gameplay)
            return;

        //Stats
        int iff = s.GetIFF();

        //Make sure killed by a combatant
        if (s.GetDiedFromIFF() >= 0 && iff != s.GetDiedFromIFF())
        {
            if (s.GetDiedFromIFF() < TGM_Manager.instance.team.Length)
                TGM_Manager.instance.team[s.GetDiedFromIFF()].currentKills++;

            //The sosig isn't on the player's team
            if (iff != GM.CurrentPlayerBody.GetPlayerIFF())
                TGM_Manager.instance.localPlayer.kills++;
        }
    }

    /// <summary>
    /// Hook into when a player is Killed
    /// </summary>
    /// <param name="playerIndex"></param>
    /// <param name="killerIFF"></param>
    public virtual void OnPlayerKilled(bool killedSelf, int iff)
    {
        TGMPlugin.Logger.LogDebug("Gamemode: OnPlayerKilled");

        if (iff != redIFF && iff != blueIFF)
            return;

        //Update Class Menu
        TGM_ClassMenu.instance.Setup(TGM_Manager.instance.team[GM.CurrentPlayerBody.GetPlayerIFF()].GetPlayerTeam().playerClasses);
    }

    /// <summary>
    /// A base respawning timer for each team
    /// </summary>
    public virtual void RespawnTime()
    {
        for (int i = 0; i < TGM_Manager.instance.team.Length; i++)
        {
            if (Time.time >= TGM_Manager.instance.team[i].respawnTime)
            {
                TGMPlugin.Logger.LogDebug("Gamemode: RespawnTime at: " + Time.time + " for team " + i);
                TGM_Manager.instance.team[i].Respawn();
                TGM_Manager.instance.team[i].respawnTime = Time.time + TGM_Scene.instance.teams[i].teamSpawnTime;
            }
        }
    }
}
