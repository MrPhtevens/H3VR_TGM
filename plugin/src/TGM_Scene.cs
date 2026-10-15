using FistVR;
using H3MP.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace TeamsGameMode;

public class TGM_Scene : MonoBehaviour
{
    public static TGM_Scene instance;
    [Header("Scene Setup")]
    public TeamSpawnRoom[] teams = new TeamSpawnRoom[2];
    public TGM_Area[] areas = new TGM_Area[2];
    public ObstacleAvoidanceType avoidanceQuailty = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
    public Transform playerResetPoint;
    [HideInInspector]
    public Vector3 defaultResetPosition;
    [HideInInspector]
    public Quaternion defaultResetRotation;

    [Header("Menus")]
    public Transform mainMenu;
    public Transform teamSetupMenu;
    public Transform profilesMenu;
    public Transform itemSpawner;

    [Header("Spectator")]
    public Transform[] spectatorStaticCameras;

    [Header("Gamemode Prefabs")]
    public GameObject rushCapturePrefab;

    [Header("Audio Overwrite")]
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
    [Tooltip("An objective has been captured by a friendly")]
    public AudioClip audioObjectiveFriendlyCaptured;
    [Tooltip("An objective has been captured by the enemy")]
    public AudioClip audioObjectiveEnemyCaptured;
    [Tooltip("A capture point or objective is being captured")]
    public AudioClip audioCapturing;
    [Tooltip("Background Audio that plays, should loop")]
    public AudioClip audioBackground;

    public delegate void SceneLoadedDelegate();
    public static event SceneLoadedDelegate SceneLoadedEvent;

    [System.Serializable]
    public class TeamSpawnRoom
    {
        [Tooltip("For array display only, does nothing")]
        public string name;

        [Tooltip("The area which a player can respawn, can be scaled")]
        public Transform respawnArea;   //Based on Gizmo
        [Tooltip("The Starting area for this Team")]
        public TGM_Area startSpawnArea;
        [Tooltip("The time between wave respawns for this team")]
        public float teamSpawnTime = 5f;

        [Tooltip("What Sosig Team is auto selected on this map")]
        public string defaultPlayerTeam = "Meatcenaries";
        [Tooltip("What Player Team is auto selected on this map")]
        public string defaultSosigTeam = "Meat Fortress Meatcenaries";
    }

    /// <summary>
    /// Returns the input IFF Team Spawn Room
    /// </summary>
    /// <param name="iff"></param>
    /// <returns></returns>
    public static TeamSpawnRoom Team(int iff)
    {
        if (instance == null || instance.teams == null || instance.teams.Length >= iff)
            return null;

        return instance.teams[iff];
    }

    public static Transform GetTeamSpawnRoomTransform(int team)
    {
        return instance.teams[team].respawnArea;
    }

    void Awake()
    {
        instance = this;
        defaultResetPosition = playerResetPoint.position;
        defaultResetRotation = playerResetPoint.rotation;
    }

    void Start()
    {
        StartCoroutine(Setup());
    }

    public IEnumerator Setup()
    {
        yield return StartCoroutine(TGM_ModLoader.LoadAssets());

        //Create our Gamemode assets
        TGM_Manager manager = Instantiate(TGM_ModLoader.tgmAssets.manager);
        yield return new WaitForEndOfFrame();

        Instantiate(TGM_ModLoader.tgmAssets.mainMenu, mainMenu.position, mainMenu.rotation * Quaternion.Euler(0, 180, 0));
        Instantiate(TGM_ModLoader.tgmAssets.teamSetup, teamSetupMenu.position, teamSetupMenu.rotation * Quaternion.Euler(0, 180, 0));
        Instantiate(TGM_ModLoader.tgmAssets.profileMenu, profilesMenu.position, profilesMenu.rotation * Quaternion.Euler(0, 180, 0));
        yield return new WaitForEndOfFrame();

        Instantiate(TGM_ModLoader.tgmAssets.spectator);
        Instantiate(TGM_ModLoader.tgmAssets.compass);
        Instantiate(TGM_ModLoader.tgmAssets.endScreen);
        yield return new WaitForEndOfFrame();

        //Everything has loaded in, invoke the event to get any extras in before we set everything up
        if (SceneLoadedEvent != null)
            SceneLoadedEvent.Invoke();
        TGM_Manager.instance.Setup();

        //Play the supplied audio
        if (audioBackground != null)
        {
            TGM_Manager.PlayBackground(audioBackground);
        }
    }

    public static void UpdateAllAreas()
    {
        //Update all areas
        for (int i = 0; i < instance.areas.Length; i++)
        {
            instance.areas[i].UpdateArea();
        }

    }

    void OnDrawGizmos()
    {
        Color newColor = Color.green;
        newColor.a = 0.2f;
        Gizmos.color = newColor;

        if (teams != null)
        {
            for (int i = 0; i < teams.Length; i++)
            {
                if (teams[i] == null)
                    continue;

                if (teams[i].respawnArea != null)
                    Gizmos.DrawCube(teams[i].respawnArea.position, teams[i].respawnArea.lossyScale);

            }
        }

        newColor = Color.yellow;
        newColor.a = 0.2f;
        Gizmos.color = newColor;
        for (int i = 0; i < areas.Length; i++)
        {
            if (areas[i] != null && i + 1 < areas.Length && areas[i + 1] != null)
            {
                if(areas[i].capturePoint != null && areas[i + 1].capturePoint != null)
                    Gizmos.DrawLine(areas[i].capturePoint.position, areas[i + 1].capturePoint.position);
            }
        }

        //MATRIX

        if (mainMenu != null)
        {
            newColor = Color.grey;
            newColor.a = 0.2f;
            Gizmos.color = newColor;

            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawLine(mainMenu.position, mainMenu.position + mainMenu.forward);
            Gizmos.matrix = Matrix4x4.TRS(mainMenu.position, mainMenu.rotation, new Vector3(1.92f, 1.08f, 0.01f));
            Gizmos.DrawCube(Vector3.zero, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }

        if (teamSetupMenu != null)
        {
            newColor = Color.grey;
            newColor.a = 0.2f;
            Gizmos.color = newColor;

            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawLine(teamSetupMenu.position, teamSetupMenu.position + teamSetupMenu.forward);
            Gizmos.matrix = Matrix4x4.TRS(teamSetupMenu.position, teamSetupMenu.rotation, new Vector3(1.92f, 1.08f, 0.01f));
            Gizmos.DrawCube(Vector3.zero, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }

        if (profilesMenu != null)
        {
            newColor = Color.grey;
            newColor.a = 0.2f;
            Gizmos.color = newColor;

            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawLine(profilesMenu.position, profilesMenu.position + profilesMenu.forward);
            Gizmos.matrix = Matrix4x4.TRS(profilesMenu.position, profilesMenu.rotation, new Vector3(0.96f, 1.08f, 0.01f));
            Gizmos.DrawCube(Vector3.zero, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }

        //Matrix Manipulated Below        
        if (itemSpawner != null)
        {
            Gizmos.color = new Color(0.4f, 0.4f, 0.9f, 0.5f);
            Gizmos.matrix = itemSpawner.localToWorldMatrix;
            //Item Spawner
            Vector3 vector = new Vector3(0f, 0.7f, 0.25f);
            Vector3 size = new Vector3(2.3f, 1.2f, 0.5f);
            Vector3 vector2 = Vector3.forward;
            Gizmos.DrawCube(vector, size);
            Gizmos.DrawLine(vector, vector + vector2 * 0.5f);
        }
    }

    void OnValidate()
    {

        if (teams != null)
        {
            if (teams.Length < 2)
            {
                List<TeamSpawnRoom> newTeams = teams.ToList();
                int missing = 2 - newTeams.Count;
                for (int i = 0; i < missing; i++)
                {
                    newTeams.Add(new TeamSpawnRoom());
                }
                teams = newTeams.ToArray();
            }

            teams[0].name = "Red";
            teams[1].name = "Blue";

        }

        if (areas != null)
        {
            if (areas.Length < 2)
            {
                List<TGM_Area> newAreas = areas.ToList();
                int missing = 2 - newAreas.Count;
                for (int i = 0; i < missing; i++)
                {
                    newAreas.Add(new TGM_Area());
                }
                areas = newAreas.ToArray();
            }

            for (int i = 0; i < areas.Length; i++)
            {
                if (areas[i] != null)
                    areas[i].index = i;
            }
        }
    }
}
