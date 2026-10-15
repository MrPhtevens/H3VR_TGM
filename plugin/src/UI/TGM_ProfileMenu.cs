using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using H3MP.Networking;

namespace TeamsGameMode;

public class TGM_ProfileMenu : MonoBehaviour
{

    public static TGM_ProfileMenu instance;

    public InputField profileInput;
    public GameObject loadProfileButton;
    public GameObject profilePrefab;
    [SerializeField] List<TGM_Button> profileButtons = new List<TGM_Button>();

    void Awake()
    {
        instance = this;
    }

    public void Setup()
    {

        if (Tools.IsClient())
        {
            loadProfileButton.SetActive(false);
        }

        TGM_ModLoader.LoadProfiles();
    }

    public void Load()
    {
        if (profileInput.text == "")
        {
            TGM_Manager.PlayAudio(TGM_Manager.PlayAudioEnum.Fail);
            return;
        }

        //Loop Through each profile and find matching name
        for (int i = 0; i < TGM_ModLoader.profiles.Count; i++)
        {
            if (TGM_ModLoader.profiles[i].name == profileInput.text)
            {
                TGMPlugin.Logger.LogError($"Loading Profile - " + TGM_ModLoader.profiles[i].name);

                TGM_Profile.profile = TGM_ModLoader.profiles[i];

                //---------------------------------------------------------------------------------------------------------------------
                // LOAD SETTINGS
                //---------------------------------------------------------------------------------------------------------------------

                //Set Gamemode
                TGM_MainMenu.instance.SelectGamemode(TGM_ModLoader.GetGamemodeIndex(TGM_Profile.profile.gamemode));

                //Setup Teams
                for (int x = 0; x < TGM_Manager.instance.team.Length; x++)
                {
                    TGM_Manager.instance.team[x].sosigTeam = TGM_ModLoader.GetSosigTeamIndex(TGM_Profile.profile.sosigTeams[x]);
                    TGM_Manager.instance.team[x].playerTeam = TGM_ModLoader.GetPlayerTeamIndex(TGM_Profile.profile.playerTeams[x]);
                    TGM_Manager.instance.team[x].sosigLimit = TGM_Profile.profile.sosigLimit[x];
                    TGM_Manager.instance.team[x].scoreGoal = TGM_Profile.profile.scoreGoal[x];
                }

                //Load Game Settings
                for (int x = 0; x < TGM_Profile.profile.gameSettings.Count; x++)
                {
                    TGM_Settings.gameSettings[x].value = TGM_Profile.profile.gameSettings[x];
                }

                //Load Gamemode Settings
                for (int x = 0; x < TGM_Profile.profile.gamemodeSettings.Count; x++)
                {
                    TGM_Settings.gamemodeSettings[x].value = TGM_Profile.profile.gamemodeSettings[x];
                }

                //---------------------------------------------------------------------------------------------------------------------

                TGM_MainMenu.instance.UpdateSettings();

                //UpdateGameOptions();
                TGM_Manager.PlayAudio(TGM_Manager.PlayAudioEnum.Confirm);
                return;
            }
        }

        TGM_Manager.PlayAudio(TGM_Manager.PlayAudioEnum.Fail);
    }

    public void Save()
    {
        if (profileInput.text == "")
        {
            TGM_Manager.PlayAudio(TGM_Manager.PlayAudioEnum.Fail);
            return;
        }

        if (TGM_ModLoader.SaveProfile(profileInput.text))
        {
            TGM_Manager.PlayAudio(TGM_Manager.PlayAudioEnum.Confirm);
            TGM_ModLoader.LoadProfiles();
            PopulateProfiles();
        }
        else
            TGM_Manager.PlayAudio(TGM_Manager.PlayAudioEnum.Fail);

    }

    public void SetProfile(string profileName)
    {
        if (profileName != "")
        {
            profileInput.text = profileName;
            TGM_Manager.PlayAudio(TGM_Manager.PlayAudioEnum.Press);
        }
        else
            TGM_Manager.PlayAudio(TGM_Manager.PlayAudioEnum.Fail);
    }

    public void PopulateProfiles()
    {
        for (int i = 0; i < profileButtons.Count; i++)
        {
            Destroy(profileButtons[i].gameObject);
        }
        profileButtons.Clear();

        for (int i = 0; i < TGM_ModLoader.profiles.Count; i++)
        {
            if (TGM_ModLoader.profiles[i] == null)
                continue;

            TGM_Button btn = Instantiate(profilePrefab, profilePrefab.transform.parent).GetComponent<TGM_Button>();
            btn.gameObject.SetActive(true);
            btn.texts[0].text = TGM_ModLoader.profiles[i].name;
            profileButtons.Add(btn);
        }
    }
}
