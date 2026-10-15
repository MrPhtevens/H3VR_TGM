using FistVR;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TeamsGameMode;

public class TGM_ClassMenu : MonoBehaviour
{
    public static TGM_ClassMenu instance;

    [Header("Menu")]
    public Text spawnButtonText;
    public GameObject buttonPrefab;
    public Transform buttonContent;

    private List<TGM_Button> buttons = new List<TGM_Button>();


    [Header("Spawn Points")]
    public Transform[] mainSpawns;
    public Transform[] ammoSpawns;

    private float spawnRange = 0.1f;

    // --- Spawn Positions
    public static int spawnMainIndex = 0;
    public static float spawnMainOffset = 0;

    public static void ResetSpawnPoints()
    {
        spawnMainIndex = 0;
        spawnMainOffset = 0;
    }
    // -----------------------


    void Awake()
    {
        if (instance != null)
            Destroy(instance.gameObject);

        instance = this;
    }

    void Start()
    {
        Setup(TGM_Manager.instance.team[GM.CurrentPlayerBody.GetPlayerIFF()].GetPlayerTeam().playerClasses);
    }

    void Update()
    {
        int iff = GM.CurrentPlayerBody.GetPlayerIFF();
        if (TGM_Manager.instance.localPlayer.awaitingRespawn)
        {
            switch (TGM_Manager.gameState)
            {
                case TGM_Manager.GameStateEnum.Pregame:
                    spawnButtonText.text = Mathf.Abs((Time.time - TGM_Manager.instance.startTime)).ToString("F2");
                    break;
                case TGM_Manager.GameStateEnum.Gameplay:
                    spawnButtonText.text = Mathf.Abs((Time.time - TGM_Manager.instance.team[iff].respawnTime)).ToString("F2");
                    break;
            }
        }
    }

    public void Setup(TGM_PlayerClass[] classes)
    {
        ClearButtons();

        int iff = GM.CurrentPlayerBody.GetPlayerIFF();

        //Spectator some how
        if (iff != 0 && iff != 1)
            return;

        int currentKills = TGM_Manager.instance.team[iff].currentKills;

        for (int i = 0; i < classes.Length; i++)
        {
            bool maxKills = false;
            if (classes[i].maxKills == -1
                || currentKills <= classes[i].maxKills)
                maxKills = true;

            bool minKills = false;
            if (classes[i].minKills == -1
                || currentKills >= classes[i].minKills)
                minKills = true;

            if (!minKills || !maxKills)
                continue;

            TGM_Button button = Instantiate(buttonPrefab, buttonContent).GetComponent<TGM_Button>();
            button.gameObject.SetActive(true);
            button.texts[0].text = classes[i].name;
            button.buttons[0].image.sprite = classes[i].thumbnail;
            button.index = i;

            buttons.Add(button);
        }
    }

    public void ClearButtons()
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            if(buttons[i] != null)
                Destroy(buttons[i].gameObject);
        }
        buttons.Clear();
    }

    public void JoinRespawn()
    {
        if (TGM_Manager.instance.localPlayer.awaitingRespawn == false)
        {
            TGM_Manager.instance.localPlayer.awaitingRespawn = true;
            SM.PlayGlobalUISound(SM.GlobalUISound.Beep, GM.CurrentPlayerBody.transform.position);
        }
        else
        {
            TGM_Manager.instance.localPlayer.awaitingRespawn = false;
            spawnButtonText.text = "Spawn";
            SM.PlayGlobalUISound(SM.GlobalUISound.Boop, GM.CurrentPlayerBody.transform.position);
        }
    }

    public void LeaveTeam()
    {
        //Destroy ally markers on leaving teamc
        for (int x = 0; x < TGM_Manager.instance.team.Length; x++)
        {
            for (int y = 0; y < TGM_Manager.instance.team[x].sosigsData.Count; y++)
            {
                if (TGM_Manager.instance.team[x].sosigsData != null
                    && TGM_Manager.instance.team[x].sosigsData[y].allyMarker != null)
                {
                    Destroy(TGM_Manager.instance.team[x].sosigsData[y].allyMarker);
                    TGM_Manager.instance.team[x].sosigsData[y].allyMarker = null;
                }
            }
        }

        TGM_Manager.PlayAudio(TGM_Manager.PlayAudioEnum.Confirm);
        TGM_Manager.LeaveTeam();
    }

    public void SpawnClass(int id)
    {
        if (id < 0)
        {
            TGMPlugin.Logger.LogMessage(PluginInfo.NAME + "Spawn Class is -1 or less and will not be spawned");
            return;
        }

        int team = GM.CurrentPlayerBody.GetPlayerIFF();
        
        //Spawn Locking Per class?
        if (TGM_Settings.GetSetting(TGMSettingEnum.SpawnLock) == 2)
            GM.CurrentSceneSettings.IsSpawnLockingEnabled = TGM_Manager.instance.team[team].GetPlayerTeam().playerClasses[id].canSpawnLock;
        else
            GM.CurrentSceneSettings.IsSpawnLockingEnabled = TGM_Settings.GetSetting(TGMSettingEnum.SpawnLock) == 1 ? true : false;

        //Set Player Health
        int healthSetting = TGM_Settings.GetSetting(TGMSettingEnum.PlayerHealth);
        //If 0 use player class health otherwise use global setting
        if (healthSetting == 0)
            healthSetting = TGM_Manager.instance.team[team].GetPlayerTeam().playerClasses[id].playerHealth;
        GM.CurrentPlayerBody.SetHealthThreshold(healthSetting);
        GM.CurrentPlayerBody.Health = healthSetting;

        //Despawn any previously owned / held items
        TGM_Manager.instance.localPlayer.DestroyPlayersItems();

        //Spawn all weapons / items
        TGM_PlayerClass.SubClass subClass 
            = TGM_Manager.instance.team[team].GetPlayerTeam().playerClasses[id].subClasses[
                Random.Range(0, TGM_Manager.instance.team[team].GetPlayerTeam().playerClasses[id].subClasses.Length)];

        //Spawn our sub classes items
        for (int x = 0; x < subClass.items.Length; x++)
        {
            SpawnItemSet(subClass.items[x], team);
        }
    }

    public static Transform GetMainSpawnPoint()
    {
        Transform spawnPoint = instance.mainSpawns[spawnMainIndex];
        spawnMainIndex++;
        if (spawnMainIndex >= instance.mainSpawns.Length)
        {
            spawnMainIndex = 0;
            spawnMainOffset += 0.5f;
        }
        return spawnPoint;
    }

    public static void SpawnItemSet(TGM_PlayerClass.ItemSet itemSet, int iff)
    {
        FVRObject spawnFVRObject = Global.GetObjectID(itemSet.objectID[Random.Range(0, itemSet.objectID.Length)]);

        //Spawn Object IDs
        for (int i = 0; i < itemSet.objectCount; i++)
        {
            if (itemSet.team != -1 && itemSet.team != iff)
                continue;

            FVRPhysicalObject newItem = null;

            //Randomise each Object
            if (!itemSet.uniformObjects && i != 0)
                spawnFVRObject = Global.GetObjectID(itemSet.objectID[Random.Range(0, itemSet.objectID.Length)]);

            Transform mainSpawn;
            if (spawnFVRObject.Category != FVRObject.ObjectCategory.Firearm
                && spawnFVRObject.Category != FVRObject.ObjectCategory.MeleeWeapon)
                mainSpawn = instance.ammoSpawns[3];
            else
                mainSpawn = GetMainSpawnPoint();

            newItem = Global.SpawnFVRObject(
                spawnFVRObject,
                mainSpawn.position + (Vector3.up * spawnMainOffset),
                mainSpawn.rotation.eulerAngles);

            //Add to our local player item tracking
            if (newItem != null)
                TGM_Manager.instance.localPlayer.playersItems.Add(newItem);
            else
                return;

            //Spawn Required Secondary Pieces for Main
            if (itemSet.requiredSecondaryPieces == true 
                && spawnFVRObject.RequiredSecondaryPieces != null 
                && spawnFVRObject.RequiredSecondaryPieces.Count > 0)
            {
                //Loop through and spawn each Piece
                for (int s = 0; s < spawnFVRObject.RequiredSecondaryPieces.Count; s++)
                {
                    if (spawnFVRObject.RequiredSecondaryPieces[s] != null)
                    {
                        FVRPhysicalObject attach = Global.SpawnFVRObject(
                            spawnFVRObject.RequiredSecondaryPieces[s],
                            instance.ammoSpawns[4].position,
                            instance.ammoSpawns[4].rotation.eulerAngles);

                        TGM_Manager.instance.localPlayer.playersItems.Add(attach);
                    }
                }
            }

            //Spawn Ammo
            if (itemSet.ammoCount > 0)
            {
                FVRObject fvrContainer = null;
                FVRObject fvrCartridge = null;
                FVRPhysicalObject[] spawnContainer = new FVRPhysicalObject[itemSet.ammoCount];
                FVRPhysicalObject spawnCartridge = null;

                //Magazine / Speed Loader / Clip
                if (itemSet.ammoContainerID != "")
                {
                    //Make sure ammo cotainer exists
                    if (!IM.OD.TryGetValue(itemSet.ammoContainerID, out fvrContainer))
                        TGMPlugin.Logger.LogWarning("Could not find ammo container: " + itemSet.ammoContainerID);

                    //Spawn the Container
                    if (fvrContainer != null)
                    {
                        for (int a = 0; a < itemSet.ammoCount; a++)
                        {
                            spawnContainer[a] = Global.SpawnFVRObject(
                                fvrContainer,
                                instance.ammoSpawns[spawnMainIndex].position + (Vector3.up * 0.1f) + (Vector3.right * 0.1f * a),
                                instance.ammoSpawns[spawnMainIndex].rotation.eulerAngles);

                            if (spawnContainer[a] != null)
                                TGM_Manager.instance.localPlayer.playersItems.Add(spawnContainer[a]);
                        }
                    }
                }

                // Cartridge / Round / Shell 
                if (itemSet.cartridgeID != "")
                {
                    //Make sure ammo cotainer exists
                    if (!IM.OD.TryGetValue(itemSet.cartridgeID, out fvrCartridge))
                        TGMPlugin.Logger.LogWarning("Could not find cartridge: " + itemSet.cartridgeID);

                    //Fill our Container with our Cartridge type
                    if (fvrCartridge != null)
                    {
                        bool spawnRaw = false;

                        //We have a magazine waiting and ready
                        if (fvrContainer != null && spawnContainer != null && spawnContainer.Length > 0)
                        {
                            for (int a = 0; a < itemSet.ammoCount; a++)
                            {
                                //Is it compatible
                                if (fvrCartridge.RoundType == fvrContainer.RoundType)
                                {
                                    //Fill Container with our Cartridge
                                    Global.ReloadWithCartridge(spawnContainer[a], fvrCartridge);
                                }
                                else
                                    spawnRaw = true;
                            }
                        }
                        else
                            spawnRaw = true;

                        if (spawnRaw)
                        {
                            for (int c = 0; c < itemSet.ammoCount; c++)
                            {
                                //Spawn raw round
                                spawnCartridge = Global.SpawnFVRObject(
                                    fvrCartridge,
                                    instance.ammoSpawns[spawnMainIndex].position + (Vector3.up * 0.05f * c) + (-Vector3.right * 0.025f * c),
                                    instance.ammoSpawns[spawnMainIndex].rotation.eulerAngles);

                                if (spawnCartridge != null)
                                    TGM_Manager.instance.localPlayer.playersItems.Add(spawnCartridge);
                            }
                        }
                    }
                }
            }
        }
    }

    void OnDrawGizmos()
    {
        for (int i = 0; i < mainSpawns.Length; i++)
        {
            if (mainSpawns[i])
                Gizmos.DrawLine((-mainSpawns[i].forward * spawnRange) + mainSpawns[i].position,
                    (mainSpawns[i].forward * spawnRange) + mainSpawns[i].position);
        }

        for (int i = 0; i < ammoSpawns.Length; i++)
        {
            if (ammoSpawns[i] != null)
                Gizmos.DrawLine((-ammoSpawns[i].forward * spawnRange) + ammoSpawns[i].position, 
                    (ammoSpawns[i].forward * spawnRange) + ammoSpawns[i].position);
        }
    }
}
