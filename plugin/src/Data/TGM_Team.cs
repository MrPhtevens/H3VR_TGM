using FistVR;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using Sodalite.Api;
using Random = UnityEngine.Random;
using H3MP.Networking;

namespace TeamsGameMode;

public class TGM_Team
{
    public delegate void CreateSosigDelegate(Sosig s);
    public static event CreateSosigDelegate CreateSosigEvent;

    public string teamName; //Currently Unused, should be a Team Color Name
    public int iff; //Matches the array index, for internal access
    public int playerTeam = 0;
    public int sosigTeam = 0;
    //public TGM_PlayerTeam playerTeam;   //Player Team
    //public TGM_SosigTeam sosigTeam;     //Sosig Team
    public int sosigLimit = 8;      //Total amount of sosigs on this team
    public int scoreGoal = -1;      //Score needed to win
    public List<Sosig> sosigs = new List<Sosig>();
    public List<TGM_Player> sosigsData = new List<TGM_Player>();
    public Color color;

    //Tracking
    public int currentKills = 0;
    public int currentScore = 0;
    public TGM_Area currentSpawnArea;

    /// <summary>
    /// The time which respawning occurs
    /// </summary>
    public float respawnTime = 0;

    private int spawnIndex = 0;
    private int spawnOrderIndex = 0;

    public TGM_PlayerTeam GetPlayerTeam()
    {
        return TGM_ModLoader.playerTeams[playerTeam];
    }

    public TGM_SosigTeam GetSosigTeam()
    {
        return TGM_ModLoader.sosigTeams[sosigTeam];
    }

    /// <summary>
    /// Returns all Team tracking values back to thier default values
    /// </summary>
    public void ResetTeamTracking()
    {
        currentKills = 0;
        currentScore = 0;
    }

    /// <summary>
    /// Triggered on wave timer
    /// </summary>
    public void Respawn()
    {
        int localIFF = GM.CurrentPlayerBody.GetPlayerIFF();

        //Spawn Local Player
        if (localIFF == iff
            && TGM_Manager.instance.localPlayer.awaitingRespawn)
        {
            Vector3[] data = Global.GetRandomPlayerSpawnPoint(currentSpawnArea.spawnPoints[localIFF].playerSpawnPoints);
            GM.CurrentMovementManager.TeleportToPoint(data[0], true, data[1]);
            TGM_Manager.instance.localPlayer.awaitingRespawn = false;
            TGM_ClassMenu.instance.spawnButtonText.text = "Spawn";
        }

        //Only Server spawns sosigs
        if (Tools.ServerRunning())
        {
            if (Tools.IsHost())
                TGM_Network.SpawnPlayers_ToClients();
            else
                return;
        }

        //Spawn Sosigs
        if (sosigs.Count < sosigLimit)
        {

            int sosigRemain = sosigLimit - sosigs.Count;
            SosigAPI.SpawnOptions _spawnOptions = new SosigAPI.SpawnOptions
            {
                SpawnState = Sosig.SosigOrder.Assault,
                SpawnActivated = true,
                EquipmentMode = SosigAPI.SpawnOptions.EquipmentSlots.All,
                SpawnWithFullAmmo = true,
                IFF = iff
            };

            for (int i = 0; i < sosigRemain; i++)
            {
                SosigSetGroup group = GetSosigID(iff);
                bool useVehicle = group.useVehicle;
                Transform spawnArea;

                //Use Vehicle spawns for vehicle sosigs
                if (useVehicle
                    && currentSpawnArea.spawnPoints[iff].sosigVehicleSpawnPoints != null
                    && currentSpawnArea.spawnPoints[iff].sosigVehicleSpawnPoints.Length > 0)
                {
                    spawnArea = currentSpawnArea.spawnPoints[iff].sosigVehicleSpawnPoints[Random.Range(0, currentSpawnArea.spawnPoints[iff].sosigVehicleSpawnPoints.Length)];
                }
                else
                    spawnArea = currentSpawnArea.spawnPoints[iff].sosigSpawnPoints[Random.Range(0, currentSpawnArea.spawnPoints[iff].sosigSpawnPoints.Length)];

                Vector3 spawnPoint = Global.GetGridXZPositionTransform(spawnArea, spawnIndex, 6);   //6x6 area, 4 bonus spawns
                //Debug.Log(spawnArea.position + " : " + spawnPoint);
                Sosig s = CreateTeamSosig(_spawnOptions, spawnPoint, spawnArea.rotation, group.id);

                /*
                //Vehicle sosig? On the Vehicle navMesh
                if (useVehicle && TGM_Scene.instance.vehicleNavMesh != null)
                {
                    TGM_Scene.instance.vehicleNavMesh.defaultArea = 134217728;
                    s.Agent.agentTypeID = TGM_Scene.instance.vehicleNavMesh.agentTypeID;
                    NavMeshQueryFilter agentQuery = new NavMeshQueryFilter();
                    agentQuery.agentTypeID = TGM_Scene.instance.vehicleNavMesh.agentTypeID;
                    agentQuery.areaMask = 134217728;
                    if (NavMesh.SamplePosition(spawnPoint, out NavMeshHit hit, 10, 134217728))
                    {
                        Debug.Log("WE JUST FUCKING WRAPED HERE WOWOW");
                        s.Agent.Warp(hit.position);
                    }

                }
                */

                TGM_Manager.instance.gamemode.OnSosigCreate(s);

                if (CreateSosigEvent != null)
                    CreateSosigEvent.Invoke(s);

                spawnIndex++;
                if (spawnIndex >= sosigLimit)
                    spawnIndex = 0;
            }
        }
    }

    public class SosigSetGroup
    {
        public int id;
        public bool useVehicle;
    }

    public SosigSetGroup GetSosigID(int iff)
    {
        bool useVehicle = false;
        int sosigID = -2;

        List<TGM_SosigTeam.SosigSet> sets = new List<TGM_SosigTeam.SosigSet>();

        TGM_SosigTeam selectedSosigTeam = TGM_ModLoader.sosigTeams[sosigTeam];

        if (selectedSosigTeam == null)
        {
            TGMPlugin.Logger.LogError("MISSING SOSIG TEAM");
            return null;
        }
        if (selectedSosigTeam.sosigSet == null)
        {
            TGMPlugin.Logger.LogError("MISSING SOSIG SET");
            return null;
        }

        for (int i = 0; i < selectedSosigTeam.sosigSet.Length; i++)
        {
            bool maxKills = false;
            if (selectedSosigTeam.sosigSet[i].maxKills == -1
                || currentKills <= selectedSosigTeam.sosigSet[i].maxKills)
                maxKills = true;

            bool minKills = false;
            if (selectedSosigTeam.sosigSet[i].minKills == -1
                || currentKills >= selectedSosigTeam.sosigSet[i].minKills)
                minKills = true;

            //In Range
            if (minKills && maxKills)
                sets.Add(selectedSosigTeam.sosigSet[i]);
        }

        //Error Check - Backup make sure we have at least ONE sosig set
        if (sets.Count <= 0)
        {
            TGMPlugin.Logger.LogWarning("No Valid Sosig Sets found, defaulting to first Sosig Set");
            sets.Add(selectedSosigTeam.sosigSet[0]);
        }

        if (selectedSosigTeam.spawnInOrder)
        {
            //ORDERED
            List<int> idList = new List<int>();
            for (int i = 0; i < sets.Count; i++)
            {
                int[] team = iff == TGM_Gamemode.redIFF ? sets[i].sosigEnemyIDsRed : sets[i].sosigEnemyIDsBlue;

                idList.AddRange(team);
                if (idList.Count >= spawnOrderIndex)
                {
                    useVehicle = sets[i].useVehicleSpawn;
                }
            }

            spawnOrderIndex++;
            if (spawnOrderIndex >= idList.Count)
            {
                spawnOrderIndex = 0;
                useVehicle = sets[0].useVehicleSpawn;
            }

            sosigID = idList[spawnOrderIndex];
        }
        else
        {
            int setID = Random.Range(0, sets.Count);

            //RANDOM
            TGM_SosigTeam.SosigSet selectedSet = sets[setID];
            useVehicle = selectedSet.useVehicleSpawn;

            //Team specific Sosigs
            if (iff == TGM_Gamemode.redIFF)
                sosigID = selectedSet.sosigEnemyIDsRed[Random.Range(0, selectedSet.sosigEnemyIDsRed.Length)];
            else
                sosigID = selectedSet.sosigEnemyIDsBlue[Random.Range(0, selectedSet.sosigEnemyIDsBlue.Length)];
        }

        return new SosigSetGroup { id = sosigID, useVehicle = useVehicle };
    }

    public Sosig CreateTeamSosig(SosigAPI.SpawnOptions spawnOptions, Vector3 position, Quaternion rotation, int sosigID)
    {
        Sosig sosig =
            SosigAPI.Spawn(
                IM.Instance.odicSosigObjsByID[(SosigEnemyID)sosigID],
                spawnOptions,
                position,
                rotation);

        //Force Sosig to RUN
        sosig.SetAssaultSpeed(Sosig.SosigMoveSpeed.Running);
        sosig.FallbackOrder = Sosig.SosigOrder.Assault;
        sosig.CommandAssaultPoint(spawnOptions.SosigTargetPosition);
        sosig.MoveSpeed = Sosig.SosigMoveSpeed.Running;

        //Stop them dropping weapons - they become useless and take up sosig count
        sosig.DoesDropWeaponsOnBallistic = false;

        //Assign to Empty Slot
        int sosigDataIndex = 0;
        for (int i = 0; i < sosigsData.Count; i++)
        {
            if (sosigsData[i].sosig == null)
            {
                sosigDataIndex = i;
                break;
            }
        }

        sosigsData[sosigDataIndex].sosig = sosig;

        DisableSosigWeaponPickup(sosig);

        //Set Agents to quailty level
        NavMeshAgent agent = sosig.GetComponent<NavMeshAgent>();

        agent.obstacleAvoidanceType = TGM_Scene.instance.avoidanceQuailty;
        agent.stoppingDistance = 1;

        sosigs.Add(sosig);

        //IF FRIENDLY!
        if (TGM_Settings.GetSetting(TGMSettingEnum.ShowFriendlies) == 1
            && spawnOptions.IFF == GM.CurrentPlayerBody.GetPlayerIFF())
        {
            GameObject arrow = TGM_Manager.Instantiate(
                TGM_ModLoader.tgmAssets.iffPrefab,
                sosig.Links[0].R.transform.position + (Vector3.up * 0.75f),
                sosig.Links[0].R.transform.rotation,
                sosig.Links[0].R.transform);

            arrow.transform.localRotation = Quaternion.Euler(0, 0, 180);

            sosigsData[sosigDataIndex].allyMarker = arrow;
        }
        return sosig;
    }

    public void DisarmTeam()
    {
        //Remove players weapons
        if(GM.CurrentPlayerBody.GetPlayerIFF() == iff)
            TGM_Manager.instance.localPlayer.DestroyPlayersItems();

        //Thats it, everyone has their weapons taken away
        for (int i = 0; i < sosigs.Count; i++)
        {
            if (sosigs[i] == null)
                continue;

            sosigs[i].DestroyAllHeldObjects();

            //Clear Hands
            for (int h = 0; h < sosigs[i].Hands.Count; h++)
            {
                if (sosigs[i].Hands[h] == null)
                    continue;

                sosigs[i].Hands[h].DropHeldObject();
                sosigs[i].Hands[h].HeldObject = null;
            }

            //Clear Slots
            for (int x = 0; i < sosigs[i].Inventory.Slots.Count; i++)
            {
                if (sosigs[i].Inventory.Slots[x] != null)
                {
                    sosigs[i].Inventory.Slots[x].DetachHeldObject();
                    sosigs[i].Inventory.Slots[x].IsHoldingObject = false;
                }
            }

            //FLEE, FLEE FOR YOUR LIVES
            sosigs[i].CurrentOrder = Sosig.SosigOrder.Flee;
        }
    }

    public void AddAllyMarkers()
    {
        //FRIENDLY Markers
        if (TGM_Settings.GetSetting(TGMSettingEnum.ShowFriendlies) == 1)
        {
            for (int i = 0; i < sosigsData.Count; i++)
            {
                if (sosigsData[i].sosig != null)
                {
                    GameObject arrow = TGM_Manager.Instantiate(
                        TGM_ModLoader.tgmAssets.iffPrefab,
                        sosigsData[i].sosig.Links[0].R.transform.position + (Vector3.up * 0.75f),
                        sosigsData[i].sosig.Links[0].R.transform.rotation,
                        sosigsData[i].sosig.Links[0].R.transform);

                    arrow.transform.localRotation = Quaternion.Euler(0, 0, 180);

                    sosigsData[i].allyMarker = arrow;
                }
            }
        }
    }

    public void RemoveAllAllyMarkers()
    {
        for (int i = 0; i < sosigsData.Count; i++)
        {
            if(sosigsData[i].allyMarker != null)
                TGM_Manager.Destroy(sosigsData[i].allyMarker);
        }
    }

    public void ClearAllTeamSosigs()
    {
        for (int i = 0; i < sosigs.Count; i++)
        {
            if(sosigs[i] != null)
                sosigs[i].ClearSosig();
        }
        sosigs.Clear();

        for (int i = 0; i < sosigsData.Count; i++)
        {
            if (sosigsData[i].sosig != null)
                sosigsData[i].sosig.ClearSosig();
        }

        sosigsData.Clear();
    }

    public static void DisableSosigWeaponPickup(Sosig s)
    {
        if (TGM_Settings.GetSetting(TGMSettingEnum.SosigWeapons) == 1)
            return;

        foreach (var item in s.Inventory.Slots)
        {
            if (item.HeldObject == null)
                continue;

            FVRPhysicalObject obj = item.HeldObject.GetComponent<FVRPhysicalObject>();
            if (obj != null)
                obj.IsPickUpLocked = true;
        }

        foreach (var item in s.Hands)
        {
            if (item.HeldObject == null)
                continue;

            FVRPhysicalObject obj = item.HeldObject.GetComponent<FVRPhysicalObject>();
            if (obj != null)
                obj.IsPickUpLocked = true;
        }
    }

    /*
    public static void DestroySosigWeapons(Sosig s)
    {
        foreach (var item in s.Inventory.Slots)
        {
            if (item.HeldObject == null)
                continue;
            else
                Object.Destroy(item.HeldObject.gameObject);
        }

        foreach (var item in s.Hands)
        {
            if (item.HeldObject == null)
                continue;
            else
                Object.Destroy(item.HeldObject.gameObject);
        }
    }
    */
}