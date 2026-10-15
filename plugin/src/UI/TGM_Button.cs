using UnityEngine;
using UnityEngine.UI;
using FistVR;
using H3MP.Networking;

namespace TeamsGameMode;

public class TGM_Button : MonoBehaviour
{
    public Text[] texts;
    public Button[] buttons;
    public int index = -1;
    public int value = 0;
    public GameObject go;

    public void SelectClass()
    {
        TGM_Manager.PlayAudio(TGM_Manager.PlayAudioEnum.Rearm);
        TGM_ClassMenu.instance.SpawnClass(index);
    }

    public void AdjustSettingValue(int amount)
    {
        TGM_Settings.Setting setting = TGM_Settings.gameSettings[index];

        amount *= setting.intIncrement;

        //Loop Around
        if (setting.intMax == 0 && setting.intMin == 0)
        {
            value += amount;
        }
        else
        {
            if (value + amount > setting.intMax)
                value = setting.intMin;
            else if (value + amount < setting.intMin)
                value = setting.intMax;
            else
                value += amount;
        }

        TGM_Manager.PlayAudio(TGM_Manager.PlayAudioEnum.Press);

        //Assign Game Setting
        TGM_Settings.SetSetting((TGMSettingEnum)index, value);
        //TGM_Settings.gameSettings[index].value = value;

        TGM_MainMenu.instance.UpdateSettings();
    }

    public void AdjustGamemodeSettingValue(int amount)
    {
        TGM_Settings.Setting setting = TGM_Settings.gamemodeSettings[index];

        amount *= setting.intIncrement;

        //Loop Around
        if (setting.intMax == 0 && setting.intMin == 0)
        {
            value += amount;
        }
        else
        {
            if (value + amount > setting.intMax)
                value = setting.intMin;
            else if (value + amount < setting.intMin)
                value = setting.intMax;
            else
                value += amount;
        }

        TGM_Manager.PlayAudio(TGM_Manager.PlayAudioEnum.Press);

        //Assign Game Setting
        TGM_Settings.SetModeSetting(index, value);
        //TGM_Settings.gameSettings[index].value = value;

        TGM_MainMenu.instance.UpdateSettings();
    }

    public void ChooseGamemode()
    {
        TGM_MainMenu.instance.SelectGamemode(index);

        TGM_Manager.PlayAudio(TGM_Manager.PlayAudioEnum.Confirm);
    }

    public void SelectProfile()
    {
        TGM_ProfileMenu.instance.SetProfile(texts[0].text);
    }

    public void SelectBrowserItem()
    {
        TGM_TeamSetup.instance.SelectBrowser(index);
    }

    public void JoinTeam(int iff)
    {
        //Use Tools to set player IFF
        Tools.SetLocalPlayerIFF(iff);

        TGM_Manager.PlayAudio(TGM_Manager.PlayAudioEnum.Confirm);

        if (iff == -3)
        {
            //Spectator
            TGM_MainMenu.instance.OpenPage(TGM_MainMenu.Page.Spectator);
            return;
        }

        Transform spawn = TGM_Scene.GetTeamSpawnRoomTransform(iff);

        //Teleport to team spawn room
        Instantiate(TGM_ModLoader.tgmAssets.classMenu, spawn.position + (Vector3.up * 1.25f) + (spawn.forward * (spawn.localScale.z / 2)), spawn.rotation);
        Global.TeleportToPoint(Global.GetValidSpawnPoint(spawn));

        //Set Spawn point to new Spawn Room
        GM.CurrentSceneSettings.DeathResetPoint = TGM_Scene.instance.playerResetPoint;
        TGM_Scene.instance.playerResetPoint.SetPositionAndRotation(spawn.position, spawn.rotation);
        TGM_Manager.instance.gamemode.OnJoinTeam(iff);

        //Update areas depending on Team
        TGM_Scene.UpdateAllAreas();

        //Ally Markers
        TGM_Manager.instance.team[iff].AddAllyMarkers();

        //Set Main page back to team selection incase in spectator
        TGM_MainMenu.instance.OpenPage(TGM_MainMenu.Page.JoinTeam);
    }

    public void AdjustSpectatorTarget(int amount)
    {
        TGM_Spectator.instance.GetNextTarget(amount);
    }

    void OnValidate()
    {
        /*

        BoxCollider box = gameObject.GetComponent<BoxCollider>();

        if (box == null)
            return;

        RectTransform rect = gameObject.GetComponent<RectTransform>();

        if (!rect)
            return;

        box.size = new Vector3(rect.sizeDelta.x, rect.sizeDelta.y, 1);
        */
    }
    public void SelectMap()
    {
        TGM_MapSelector.instance.SelectMap(index);
    }
}
