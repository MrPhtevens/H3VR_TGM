using UnityEngine;
using H3MP.Networking;

namespace TeamsGameMode;

public  class TGM_Network
{
    // Connections
    private static CustomConnection connectionGameSettings;
    private static CustomConnection connectionRequestSettings;
    private static CustomConnection connectionGameplay;
    private static CustomConnection connectionAdjustScores;
    private static CustomConnection connectionSpawnPlayers;

    public static void Setup()
    {
        TGMPlugin.Logger.LogMessage("Network Setup");
        connectionGameSettings = Tools.CreateCustomConnection("TGM_GameSettings");
        //connectionGameSettings.ServerHandlerEvent += GameSettings_Receiver;
        connectionGameSettings.ClientHandlerEvent += GameSettings_Receiver;

        connectionRequestSettings = Tools.CreateCustomConnection("TGM_RequestSettings");
        connectionRequestSettings.ServerHandlerEvent += RequestSettings_Receiver;
        //connectionGameModeSettings.ClientHandlerEvent += GameModeSettings_Receiver;

        connectionGameplay = Tools.CreateCustomConnection("TGM_Gameplay");
        connectionGameplay.ServerHandlerEvent += Gameplay_Receiver;
        connectionGameplay.ClientHandlerEvent += Gameplay_Receiver;

        connectionAdjustScores = Tools.CreateCustomConnection("TGM_AdjustScores");
        connectionAdjustScores.ServerHandlerEvent += AdjustScores_Receiver;
        connectionAdjustScores.ClientHandlerEvent += AdjustScores_Receiver;


        connectionSpawnPlayers = Tools.CreateCustomConnection("TGM_SpawnPlayers");
        connectionSpawnPlayers.ServerHandlerEvent += SpawnPlayers_Receiver;
    }

    public static void OnDestroyed()
    {
        //Clear Connections
        if(connectionGameSettings != null)
            connectionGameSettings.ClientHandlerEvent -= GameSettings_Receiver;
        if (connectionRequestSettings != null)
            connectionRequestSettings.ServerHandlerEvent -= RequestSettings_Receiver;
        if (connectionGameplay != null)
            connectionGameplay.ServerHandlerEvent -= Gameplay_Receiver;
        if (connectionGameplay != null)
            connectionGameplay.ClientHandlerEvent -= Gameplay_Receiver;
        if (connectionAdjustScores != null)
            connectionAdjustScores.ServerHandlerEvent -= AdjustScores_Receiver;
        if (connectionAdjustScores != null)
            connectionAdjustScores.ClientHandlerEvent -= AdjustScores_Receiver;
    }

    //------------------------------------------------------------------------------
    // Game Settings
    //------------------------------------------------------------------------------

    static void GameSettings_Receiver(int clientID, PacketData packet)
    {
        //Host doesn't get settings
        if (!Tools.ServerRunning() || Tools.IsHost())
            return;

        TGMPlugin.Logger.LogMessage("Game Settings Received");

        //Gamemode
        int gmi = packet.ReadInt();
        if(gmi != -1)
            TGM_MainMenu.instance.SelectGamemode(gmi);

        //Game State
        TGM_Manager.instance.SetGameState((TGM_Manager.GameStateEnum)packet.ReadInt());

        //Game Settings
        for (int i = 0; i < TGM_Settings.gameSettings.Count; i++)
        {
            if (!TGM_Settings.gameSettings[i].localOnly)
                TGM_Settings.SetSetting((TGMSettingEnum)i, packet.ReadInt());
        }

        //Gamemode Settings
        for (int i = 0; i < TGM_Settings.gamemodeSettings.Count; i++)
        {
            TGM_Settings.SetModeSetting(i, packet.ReadInt());
        }

        //Teams
        for (int teamIFF = 0; teamIFF < TGM_Manager.instance.team.Length; teamIFF++)
        {
            TGM_Manager.instance.team[teamIFF].playerTeam = packet.ReadInt();
            TGM_Manager.instance.team[teamIFF].sosigTeam = packet.ReadInt();
            TGM_Manager.instance.team[teamIFF].scoreGoal = packet.ReadInt();
            TGM_Manager.instance.team[teamIFF].currentScore = packet.ReadInt();
            TGM_Manager.instance.team[teamIFF].currentKills = packet.ReadInt();
            TGM_Manager.instance.team[teamIFF].currentSpawnArea.index = packet.ReadInt();
        }

        //Areas
        for (int i = 0; i < TGM_Scene.instance.areas.Length; i++)
        {
            TGM_Scene.instance.areas[i].iff = packet.ReadInt();
        }

        TGM_MainMenu.instance.UpdateSettings();
    }

    public static void GameSettings_ToClients()
    {
        if (!Tools.ServerRunning() || connectionGameSettings == null)
            return;

        PacketData packet = new PacketData(connectionGameSettings.ToClientID);

        //Gamemode
        if(TGM_Manager.instance.gamemode != null)
            packet.Write(TGM_Manager.instance.gamemode.index);
        else
            packet.Write(-1);   //-1 gets ignored


        //Game State
        packet.Write((int)TGM_Manager.gameState);

        //Game Settings
        for (int i = 0; i < TGM_Settings.gameSettings.Count; i++)
        {
            if (!TGM_Settings.gameSettings[i].localOnly)
                packet.Write(TGM_Settings.GetSetting((TGMSettingEnum)i));
        }

        //Gamemode Settings
        for (int i = 0; i < TGM_Settings.gamemodeSettings.Count; i++)
        {
            packet.Write(TGM_Settings.GetModeSetting(i));
        }

        //Teams
        for (int teamIFF = 0; teamIFF < TGM_Manager.instance.team.Length; teamIFF++)
        {
            packet.Write(TGM_Manager.instance.team[teamIFF].playerTeam);
            packet.Write(TGM_Manager.instance.team[teamIFF].sosigTeam);
            packet.Write(TGM_Manager.instance.team[teamIFF].scoreGoal);
            packet.Write(TGM_Manager.instance.team[teamIFF].currentScore);
            packet.Write(TGM_Manager.instance.team[teamIFF].currentKills);
            packet.Write(TGM_Manager.instance.team[teamIFF].currentSpawnArea.index);
        }

        //Areas
        for (int i = 0; i < TGM_Scene.instance.areas.Length; i++)
        {
            packet.Write(TGM_Scene.instance.areas[i].iff);
        }

        TGMPlugin.Logger.LogMessage("Game Settings Sent to Clients");

        connectionGameSettings.ServerToClients(packet);
    }

    //------------------------------------------------------------------------------
    // Request Settings
    //------------------------------------------------------------------------------

    static void RequestSettings_Receiver(int clientID, PacketData packet)
    {
        //Host doesn't get settings
        if (!Tools.ServerRunning() || Tools.IsHost())
            return;

        TGMPlugin.Logger.LogMessage("Request Settings Received");

        //Send Settings
        GameSettings_ToClients();
        
        //Send Gameplay Update

    }

    public static void RequestSettings_ToServer()
    {
        if (!Tools.ServerRunning() || connectionRequestSettings == null)
            return;

        TGMPlugin.Logger.LogMessage("Request Settings Sent");

        PacketData packet = new PacketData(connectionRequestSettings.ToServerID);
        connectionRequestSettings.ClientToServer(packet);
    }

    //------------------------------------------------------------------------------
    // Gameplay
    //------------------------------------------------------------------------------

    static void Gameplay_Receiver(int clientID, PacketData packet)
    {
        //Host doesn't get settings
        if (!Tools.ServerRunning() || !Tools.IsClient())
            return;

        TGMPlugin.Logger.LogMessage("Gameplay Received");

        //Teams
        for (int teamIFF = 0; teamIFF < TGM_Manager.instance.team.Length; teamIFF++)
        {
            TGM_Manager.instance.team[teamIFF].respawnTime = Time.time + packet.ReadFloat();
            TGM_Manager.instance.team[teamIFF].scoreGoal = packet.ReadInt();
            TGM_Manager.instance.team[teamIFF].currentScore = packet.ReadInt();
            TGM_Manager.instance.team[teamIFF].currentKills = packet.ReadInt();
            TGM_Manager.instance.team[teamIFF].currentSpawnArea.index = packet.ReadInt();
        }

        //Areas
        for (int i = 0; i < TGM_Scene.instance.areas.Length; i++)
        {
            TGM_Scene.instance.areas[i].iff = packet.ReadInt();
        }
    }

    public static void Gameplay_ToServer()
    {
        if (!Tools.ServerRunning() || connectionGameplay == null)
            return;


        TGMPlugin.Logger.LogMessage("Gameplay To Server");

        PacketData packet = new PacketData(connectionGameplay.ToServerID);

        connectionGameplay.ClientToServer(packet);
    }

    public static void Gameplay_ToClients()
    {
        if (!Tools.ServerRunning())
            return;

        TGMPlugin.Logger.LogMessage("Gameplay To Clients");


        PacketData packet = new PacketData(connectionGameplay.ToClientID);

        //Teams
        for (int i = 0; i < TGM_Manager.instance.team.Length; i++)
        {
            //Time left before spawn
            packet.Write(Time.time - TGM_Manager.instance.team[i].respawnTime);
            packet.Write(TGM_Manager.instance.team[i].scoreGoal);
            packet.Write(TGM_Manager.instance.team[i].currentScore);
            packet.Write(TGM_Manager.instance.team[i].currentKills);
            packet.Write(TGM_Manager.instance.team[i].currentSpawnArea.index);
        }

        //Areas
        for (int i = 0; i < TGM_Scene.instance.areas.Length; i++)
        {
            packet.Write(TGM_Scene.instance.areas[i].iff);
        }
        connectionGameplay.ServerToClients(packet);
    }

    //------------------------------------------------------------------------------
    // Adjust Scores
    //------------------------------------------------------------------------------

    static void AdjustScores_Receiver(int clientID, PacketData packet)
    {
        if (!Tools.ServerRunning())
            return;

        TGMPlugin.Logger.LogMessage("Adjust Scores Received");

        int teamIFF = packet.ReadInt();
        int score = packet.ReadInt();

        //Adjust Score but don't network it
        TGM_Manager.instance.gamemode.AdjustTeamScore(teamIFF, score, false);
    }

    public static void AdjustScores_ToServer(int teamIFF, int amount)
    {
        if (!Tools.ServerRunning() || connectionAdjustScores == null)
            return;

        TGMPlugin.Logger.LogMessage("Adjust Scores To Server");

        PacketData packet = new PacketData(connectionAdjustScores.ToServerID);
        packet.Write(teamIFF);
        packet.Write(amount);

        connectionAdjustScores.ClientToServer(packet);
    }
    public static void AdjustScores_ToClients(int teamIFF, int amount)
    {
        if (!Tools.ServerRunning() || connectionAdjustScores == null)
            return;


        TGMPlugin.Logger.LogMessage("Adjust Scores To Clients");

        PacketData packet = new PacketData(connectionAdjustScores.ToClientID);
        packet.Write(teamIFF);
        packet.Write(amount);

        connectionAdjustScores.ServerToClients(packet);
    }

    //------------------------------------------------------------------------------
    // Spawn Players
    //------------------------------------------------------------------------------

    static void SpawnPlayers_Receiver(int clientID, PacketData packet)
    {
        //Host doesn't get settings
        if (!Tools.ServerRunning() || Tools.IsHost())
            return;

        //Respawn players!
        for (int i = 0; i < TGM_Manager.instance.team.Length; i++)
        {
            TGM_Manager.instance.team[i].Respawn();
        }

    }

    private static float SpawnplayersTimeout = 1;
    public static void SpawnPlayers_ToClients()
    {
        if (!Tools.ServerRunning() || connectionSpawnPlayers == null || Time.time < SpawnplayersTimeout)
            return;

        //Time out our respawns so we don't multi trigger
        SpawnplayersTimeout = Time.time + 1;
        PacketData packet = new PacketData(connectionSpawnPlayers.ToClientID);
        connectionSpawnPlayers.ServerToClients(packet);
    }
}
