using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using FistVR;
using H3MP.Networking;

namespace TeamsGameMode;

public class TGM_Manager : MonoBehaviour
{
    public static TGM_Manager instance;
    //public static TGM_Profile profile;
    public static GameStateEnum gameState = GameStateEnum.GamemodeSelect;
    public List<TGM_Gamemode> gamemodes = new List<TGM_Gamemode>();

    public Sprite[] gamemodeThumbnails;

    //public Transform playerSpawnPoint;
    //public TGM_Teams teams;

    public int teamCount = 2;
    public TGM_Team[] team = new TGM_Team[2];
    [HideInInspector] public TGM_Gamemode gamemode;

    [Header("DATA")]
    public TGM_Player localPlayer = new TGM_Player();
    public float startTime = 0;

    [Header("Audio")]
    public AudioSource globalAudio;
    public AudioSource backgroundAudio;
    [Tooltip("Start Game, or spawn items etc")]
    public AudioClip audioConfirm;
    [Tooltip("Regular Button Press")]
    public AudioClip audioPress;
    [Tooltip("Something broke or didn't accept input")]
    public AudioClip audioFail;
    [Tooltip("Rearm Player")]
    public AudioClip audioRearm;
    [Tooltip("Player's Team Objective point change")]
    public AudioClip audioPoint;
    [Tooltip("Get Kill")]
    public AudioClip audioElimination;
    [Tooltip("Player's Team Won")]
    public AudioClip audioTeamWon;
    [Tooltip("Player's Team Lost")]
    public AudioClip audioTeamLost;
    [Tooltip("Both Teams Draw")]
    public AudioClip audioTeamDraw;
    [Tooltip("An objective has been captured")]
    public AudioClip audioObjectiveEnemyCaptured;
    [Tooltip("An objective has been captured")]
    public AudioClip audioObjectiveFriendlyCaptured;
    [Tooltip("A capture point or objective is being captured")]
    public AudioClip audioCapturing;

    public delegate void GameStateDelegate();
    public static event GameStateDelegate GameStateEvent;

    public delegate void StandardDelegate();
    public static event StandardDelegate GamemodesLoadedEvent;

    private float safteyCheck = 0;

    public void Setup()
    {
        //Load our Mods in
        TGM_ModLoader.LoadPlayerTeams();
        TGM_ModLoader.LoadSosigTeams();

        //Setup our teams
        team = new TGM_Team[teamCount];
        for (int i = 0; i < team.Length; i++)
        {
            team[i] = new TGM_Team();

            //Default to first in the list
            team[i].playerTeam = 0;
            team[i].iff = i;

            //Assign Default Sosig Team
            for (int t = 0; t < TGM_ModLoader.sosigTeams.Count; t++)
            {
                if (TGM_ModLoader.sosigTeams[t].name.Contains(TGM_Scene.instance.teams[i].defaultSosigTeam))
                {
                    team[i].sosigTeam = t;
                    break;
                }
            }

            //Assign Default Player Team
            for (int t = 0; t < TGM_ModLoader.playerTeams.Count; t++)
            {
                if (TGM_ModLoader.playerTeams[t].name.Contains(TGM_Scene.instance.teams[i].defaultPlayerTeam))
                {
                    team[i].playerTeam = t;
                    break;
                }
            }
        }

        //Unique Setup
        team[0].teamName = "Red";
        team[0].color =  new Color(1f, 0.3764f, 0.3764f);

        team[1].teamName = "Blue";
        team[1].color = new Color(0.3764f, 0.3764f, 1f);

        //Setup Areas
        for (int i = 0; i < TGM_Scene.instance.areas.Length; i++)
        {
            if (TGM_Scene.instance.areas[i] != null)
            {
                TGM_Scene.instance.areas[i].index = i;
            }
        }

        //Setup our other systems
        TGM_TeamSetup.instance.Setup();
        TGM_MainMenu.instance.Setup();
        TGM_ProfileMenu.instance.Setup();

        //Setup H3MP
        if (Tools.ServerRunning())
            TGM_Network.Setup();

        SetGameState(GameStateEnum.GamemodeSelect);

        TGMPlugin.Logger.LogMessage($"TGM Setup Complete");
    }

    void OnDestroy()
    {
        if(Tools.H3MPEnabled)
            TGM_Network.OnDestroyed();

        //Clear old gamemode settings on scene change
        gamemode = null;
        TGM_Settings.gamemodeSettings.Clear();
    }

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        //Maybe move this to SETUP, needs to be robust with custom gamemodes
        if (gamemodes == null)
            gamemodes = new List<TGM_Gamemode>();

        TGM_TeamDeathmatch teamDeathmatch = new TGM_TeamDeathmatch("Team Deathmatch", "2 Teams fight to the death", gamemodeThumbnails[0]);
        AddGamemode(teamDeathmatch);

        
        TGM_Rush rush = new TGM_Rush("Rush", "Attack and Defend", gamemodeThumbnails[1]);
        AddGamemode(rush);
        
        GamemodesLoadedEvent?.Invoke();
    }

    void Update()
    {
        if(gamemode != null)
            gamemode.Update();

        if (gameState == GameStateEnum.Gameplay)
        {
            //Catch any Sosigs that have fallen out of the map
            if (Time.time >= safteyCheck)
            {
                safteyCheck = Time.time + 10.1f;

                for (int i = 0; i < team.Length; i++)
                {
                    for (int x = 0; x < team[i].sosigsData.Count; x++)
                    {
                        if (team[i].sosigsData[x].sosig != null
                            && team[i].sosigsData[x].sosig.transform.position.y < GM.CurrentSceneSettings.CatchHeight)
                        {
                            team[i].sosigsData[x].sosig.ClearSosig();
                        }
                    }
                }
            }
        }
    }

    public void AddGamemode(TGM_Gamemode gamemode)
    {
        for (int i = 0; i < gamemodes.Count; i++)
        {
            if (gamemodes[i].name == gamemode.name)
                return;
        }

        TGMPlugin.Logger.LogMessage($"Adding Gamemode: " + gamemode.name);
        gamemodes.Add(gamemode);
        gamemode.index = gamemodes.Count - 1;
    }

    public void SetGameState(GameStateEnum state)
    {
        gameState = state;

        TGMPlugin.Logger.LogMessage($"Set Game State: " + state.ToString());

        if (GameStateEvent != null)
            GameStateEvent.Invoke();

        //Hide Team Selection unless in Setup
        TGM_TeamSetup.instance.gameObject.SetActive(false);
        TGM_ProfileMenu.instance.loadProfileButton.SetActive(false);

        switch (state)
        {
            case GameStateEnum.GamemodeSelect:
                TGM_MainMenu.instance.OpenPage(TGM_MainMenu.Page.Gamemode);
                break;
            case GameStateEnum.Setup:       //Gamemode being configured
                TGM_MainMenu.instance.OpenPage(TGM_MainMenu.Page.GameSettings);
                gamemode.Setup();
                startTime = 0;               //Reset Playtime to zero;
                TGM_TeamSetup.instance.gameObject.SetActive(true);
                TGM_ProfileMenu.instance.loadProfileButton.SetActive(true);
                TGM_MainMenu.instance.UpdateSettings();
                SetColorBlind();
                break;
            case GameStateEnum.Pregame:     //30 sec Count down to game start
                TGM_MainMenu.instance.OpenPage(TGM_MainMenu.Page.JoinTeam);
                startTime = Time.time + TGM_Gamemode.gameStartDelay;
                gamemode.Pregame();
                break;
            case GameStateEnum.Gameplay:    //Combat
                TGM_MainMenu.instance.OpenPage(TGM_MainMenu.Page.JoinTeam);
                gamemode.GameplayStart();
                break;
            case GameStateEnum.Postgame:    //30 Sec post gameplay
                TGM_MainMenu.instance.OpenPage(TGM_MainMenu.Page.JoinTeam);
                gamemode.Postgame();
                break;
            case GameStateEnum.Gameover:    //Final Scores, Wait for master or wait for all players to ready
                TGM_MainMenu.instance.OpenPage(TGM_MainMenu.Page.JoinTeam);
                gamemode.GameOver();
                break;
        }

        // Put Networking state change here
        if (Tools.ServerRunning() && Tools.IsHost())
        {
            //Send Settings
            TGM_Network.GameSettings_ToClients();
        }
    }

    public IEnumerator SetGameStateDelayed(GameStateEnum state, float delay)
    {
        yield return new WaitForSeconds(delay);

        //New State
        instance.SetGameState(state);
    }

    //------------------------------------------------------------------------------
    // Sosigs
    //------------------------------------------------------------------------------

    public void OnSosigKilled(Sosig s)
    {
        TGM_Team sosigTeam = team[s.GetIFF()];

        //Assign to Empty Slot

        //Sosig Death logs are disabled until a system to display ingame is added
        /*
        for (int i = 0; i < sosigTeam.sosigsData.Count; i++)
        {
            if (sosigTeam.sosigsData[i].sosig == s)
            {
                if(s.GetIFF() != s.GetDiedFromIFF())
                    TeamGameModePlugin.Logger.LogMessage(sosigTeam.sosigsData[i].playerName + " was killed by an enemy");
                else
                    TeamGameModePlugin.Logger.LogMessage(sosigTeam.sosigsData[i].playerName + " was killed by a teammate");
                break;
            }
        }
        */
    }

    public void SetColorBlind()
    {
        if (TGM_Settings.GetSetting(TGMSettingEnum.ColorBlind) == 1)
        {
            team[0].teamName = "YELLOW";
            team[0].color = new Color(1f, 1f, 0.3764f);

            team[1].teamName = "PURPLE";
            team[1].color = new Color(0.5f, 0.3764f, 1f);
        }
        else
        {
            //Unique Setup
            team[0].teamName = "RED";
            team[0].color = new Color(1f, 0.3764f, 0.3764f);

            team[1].teamName = "BLUE";
            team[1].color = new Color(0.3764f, 0.3764f, 1f);
        }
        //RED
        TGM_Compass.instance.cornerBackgrounds[0].color = team[0].color;
        TGM_MainMenu.instance.teamButtons[0].color = team[0].color;
        TGM_MainMenu.instance.teamButtonText[0].text = team[0].teamName;

        //BLUE
        TGM_Compass.instance.cornerBackgrounds[1].color = team[1].color;
        TGM_MainMenu.instance.teamButtons[1].color = team[1].color;
        TGM_MainMenu.instance.teamButtonText[1].text = team[1].teamName;
    }

    //------------------------------------------------------------------------------
    // Player
    //------------------------------------------------------------------------------

    public static void LeaveTeam()
    {
        //Stop any respawning
        instance.localPlayer.awaitingRespawn = false;

        TGM_Scene.instance.playerResetPoint.SetPositionAndRotation(
            TGM_Scene.instance.defaultResetPosition,
            TGM_Scene.instance.defaultResetRotation);

        GM.CurrentPlayerBody.SetPlayerIFF(-3);
        Global.TeleportToPoint(Global.GetValidSpawnPoint(TGM_Scene.instance.playerResetPoint));

        //Remove Class Menu
        if(TGM_ClassMenu.instance != null)
            Destroy(TGM_ClassMenu.instance.gameObject);

        //Update all areas
        TGM_Scene.UpdateAllAreas();
    }

    public void PlayerDeathEvent(bool killedSelf, int iff)
    {
        if (TGM_Settings.GetSetting(TGMSettingEnum.PlayerItemsOnDeath) > 0)
        {
            //Hand Items
            FVRPhysicalObject[] hands = GM.CurrentPlayerBody.transform.GetComponentsInChildren<FVRPhysicalObject>();

            if (hands != null)
            {
                for (int i = 0; i < hands.Length; i++)
                {
                    if (hands[i] != null)
                    {
                        if (hands[i].ObjectWrapper)
                            continue;

                        hands[i].ForceBreakInteraction();
                        hands[i].ClearQuickbeltState();
                    }
                }
            }

            if (GM.CurrentPlayerBody != null && GM.CurrentPlayerBody.QBSlots_Internal != null)
            {
                //All Quick belt slots into backpacks
                for (int i = 0; i < GM.CurrentPlayerBody.QBSlots_Internal.Count; i++)
                {
                    if (GM.CurrentPlayerBody.QBSlots_Internal[i] == null)
                        continue;

                    //Debug.Log("QB: " + i);
                    FVRPhysicalObject phy = GM.CurrentPlayerBody.QBSlots_Internal[i].CurObject;

                    if (phy == null || phy.ObjectWrapper != null)
                        continue;

                    phy.ClearQuickbeltState();

                    if (phy.Slots != null && phy.Slots.Length > 0)
                    {
                        for (int x = 0; x < phy.Slots.Length; x++)
                        {
                            if (phy.Slots[x] != null && phy.Slots[x].CurObject != null)
                            {
                                phy.Slots[x].CurObject.ClearQuickbeltState();
                            }
                        }
                    }
                }
            }
        }

        /*
        //If respawning not enabled
        if (TGM_Settings.GetSetting(TGMSettingEnum.CanRespawn) == 0)
        {

        }
        */
    }

    //------------------------------------------------------------------------------
    // AUDIO
    //------------------------------------------------------------------------------
    #region Audio
    public static void PlayAudio(PlayAudioEnum type, bool pitchVariation = false)
    {
        AudioClip clip = null;

        switch (type)
        {
            case PlayAudioEnum.Confirm:
                if (TGM_Scene.instance.audioConfirm)
                    clip = TGM_Scene.instance.audioConfirm;
                else
                    clip = instance.audioConfirm;
                break;
            default:
            case PlayAudioEnum.Press:
                if (TGM_Scene.instance.audioPress)
                    clip = TGM_Scene.instance.audioPress;
                else
                    clip = instance.audioPress;
                break;
            case PlayAudioEnum.Fail:
                if (TGM_Scene.instance.audioFail)
                    clip = TGM_Scene.instance.audioFail;
                else
                    clip = instance.audioFail;
                break;
            case PlayAudioEnum.Rearm:
                if (TGM_Scene.instance.audioRearm)
                    clip = TGM_Scene.instance.audioRearm;
                else
                    clip = instance.audioRearm;
                break;
            case PlayAudioEnum.Point:
                if (TGM_Scene.instance.audioPoint)
                    clip = TGM_Scene.instance.audioPoint;
                else
                    clip = instance.audioPoint;
                break;
            case PlayAudioEnum.Elimination:
                if (TGM_Scene.instance.audioElimination)
                    clip = TGM_Scene.instance.audioElimination;
                else
                    clip = instance.audioElimination;
                break;
            case PlayAudioEnum.TeamWon:
                if (TGM_Scene.instance.audioTeamWon)
                    clip = TGM_Scene.instance.audioTeamWon;
                else
                    clip = instance.audioTeamWon;
                break;
            case PlayAudioEnum.TeamLost:
                if (TGM_Scene.instance.audioTeamLost)
                    clip = TGM_Scene.instance.audioTeamLost;
                else
                    clip = instance.audioTeamLost;
                break;
            case PlayAudioEnum.ObjectiveFriendlyCaptured:
                if (TGM_Scene.instance.audioObjectiveFriendlyCaptured)
                    clip = TGM_Scene.instance.audioObjectiveFriendlyCaptured;
                else
                    clip = instance.audioObjectiveFriendlyCaptured;
                break;
            case PlayAudioEnum.ObjectiveEnemyCaptured:
                if (TGM_Scene.instance.audioObjectiveEnemyCaptured)
                    clip = TGM_Scene.instance.audioObjectiveEnemyCaptured;
                else
                    clip = instance.audioObjectiveEnemyCaptured;
                break;
            case PlayAudioEnum.TeamDraw:
                if (TGM_Scene.instance.audioTeamDraw)
                    clip = TGM_Scene.instance.audioTeamDraw;
                else
                    clip = instance.audioTeamDraw;
                break;
        }

        if (pitchVariation)
            instance.globalAudio.pitch = Random.Range(0.975f, 1.025f);
        else
            instance.globalAudio.pitch = 1;

        if (clip != null)
            instance.globalAudio.PlayOneShot(clip);
    }

    public static void PlayBackground(AudioClip clip)
    {
        instance.backgroundAudio.clip = clip;
        instance.backgroundAudio.loop = true;
        instance.backgroundAudio.Play();
    }

    public static void PlayCustomAudio(AudioClip clip)
    {
        instance.globalAudio.PlayOneShot(clip);
    }

    public enum PlayAudioEnum
    {
        Confirm = 0,
        Press = 1,
        Fail = 2,
        Rearm = 3,
        Point = 4,
        Elimination = 5,
        TeamWon = 6,
        TeamLost = 7,
        ObjectiveFriendlyCaptured = 8,
        ObjectiveEnemyCaptured = 9,
        TeamDraw = 10,
    }
    #endregion

    public enum GameStateEnum
    {
        GamemodeSelect,
        Setup,
        Pregame,    //Countdown to game start
        Gameplay,
        Postgame,   //30 secs post game before game over
        Gameover,   //Put everyone back into start room
    }
}
