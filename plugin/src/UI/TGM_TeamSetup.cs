using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TeamsGameMode;

public class TGM_TeamSetup : MonoBehaviour
{
    public static TGM_TeamSetup instance;

    [Header("Browser")]
    public GameObject browserPanel;
    public GameObject browserButtonPrefab;
    public Transform browserContent;
    public Text browserTitle;
    public Text browserDescription;
    public Image browserThumbnail;
    private List<TGM_Button> browserButtons = new List<TGM_Button>();
    private int browserTeamID = -1;
    private int browserMode = 0;
    private int browserSelectIndex = 0;

    [Header("Team UI")]
    public Text[] scoreCountText;
    public Text[] sosigsCountText;
    public Image[] playerTeamThumbnails;
    public Image[] sosigTeamThumbnails;
    public Text[] playerTeamTitles;
    public Text[] sosigTeamTitles;
    public Text[] playerTeamDescriptions;
    public Text[] sosigTeamDescriptions;
    public GameObject[] teamObjectiveAdjust;

    void Awake()
    {
        instance = this;
    }

    public void Setup()
    {
        //Setup Default Spawn points
        for (int i = 0; i < TGM_Manager.instance.team.Length; i++)
        {
            if (TGM_Scene.instance.teams[i].startSpawnArea == null)
            {
                TGMPlugin.Logger.LogError("Map is Missing team " + i + " startSpawnArea !!!");
                continue;
            }

            TGM_Manager.instance.team[i].currentSpawnArea = TGM_Scene.instance.teams[i].startSpawnArea;
        }

        //Update all areas
        TGM_Scene.UpdateAllAreas();

        //Settings
        TGM_MainMenu.instance.UpdateSettings();
    }


    //RED TEAM
    public void AdjustRedTeamScore(int amount)
    {
        TGM_Manager.instance.team[0].scoreGoal = Mathf.Clamp(TGM_Manager.instance.team[0].scoreGoal + amount, 1, int.MaxValue);
        TGM_MainMenu.instance.UpdateSettings();
    }

    public void AdjustRedSosigCount(int amount)
    {
        TGM_Manager.instance.team[0].sosigLimit = Mathf.Clamp(TGM_Manager.instance.team[0].sosigLimit + amount, 0, 32);
        TGM_MainMenu.instance.UpdateSettings();
    }

    public void OpenBrowserRed(int type)
    {
        OpenBrowser(type, 0);
    }

    //BLUE TEAM
    public void AdjustBlueTeamScore(int amount)
    {
        TGM_Manager.instance.team[1].scoreGoal = Mathf.Clamp(TGM_Manager.instance.team[1].scoreGoal + amount, 1, int.MaxValue);
        TGM_MainMenu.instance.UpdateSettings();
    }

    public void AdjustBlueSosigCount(int amount)
    {
        TGM_Manager.instance.team[1].sosigLimit = Mathf.Clamp(TGM_Manager.instance.team[1].sosigLimit + amount, 0, 32);
        TGM_MainMenu.instance.UpdateSettings();
    }

    public void OpenBrowserBlue(int type)
    {
        OpenBrowser(type, 1);
    }


    public void OpenBrowser(int type, int teamID)
    {
        TGM_Manager.PlayAudio(TGM_Manager.PlayAudioEnum.Press);

        browserMode = type;
        browserTeamID = teamID;

        //Clear old Buttons
        for (int i = browserButtons.Count - 1; i >= 0; i--)
        {
            if (browserButtons[i] != null)
                Destroy(browserButtons[i].gameObject);
        }
        browserButtons.Clear();

        browserPanel.SetActive(true);
        if (type == 0)
        {
            TGM_PlayerTeam team = TGM_ModLoader.playerTeams[TGM_Manager.instance.team[browserTeamID].playerTeam];

            //Select Player Team
            browserTitle.text = team.name;
            browserDescription.text = team.description;
            browserThumbnail.sprite = team.thumbnail;

            for (int i = 0; i < TGM_ModLoader.playerTeams.Count; i++)
            {
                TGM_Button btn = Instantiate(browserButtonPrefab, browserContent).GetComponent<TGM_Button>();
                btn.gameObject.SetActive(true);
                btn.index = i;
                btn.buttons[0].image.sprite = TGM_ModLoader.playerTeams[i].thumbnail;
                btn.texts[0].text = TGM_ModLoader.playerTeams[i].name;
                browserButtons.Add(btn);
            }
        }
        else
        {
            TGM_SosigTeam sosigTeam = TGM_ModLoader.sosigTeams[TGM_Manager.instance.team[browserTeamID].sosigTeam];
            //Select Sosig Team
            browserTitle.text = sosigTeam.name;
            browserDescription.text = sosigTeam.description;
            browserThumbnail.sprite = sosigTeam.thumbnail;

            for (int i = 0; i < TGM_ModLoader.sosigTeams.Count; i++)
            {
                TGM_Button btn = Instantiate(browserButtonPrefab, browserContent).GetComponent<TGM_Button>();
                btn.gameObject.SetActive(true);
                btn.index = i;
                btn.buttons[0].image.sprite = TGM_ModLoader.sosigTeams[i].thumbnail;
                btn.texts[0].text = TGM_ModLoader.sosigTeams[i].name;
                browserButtons.Add(btn);
            }
        }
        
    }

    public void SelectBrowser(int i)
    {
        TGM_Manager.PlayAudio(TGM_Manager.PlayAudioEnum.Press);

        browserSelectIndex = i;
        if (browserMode == 0)
        {
            TGM_PlayerTeam team = TGM_ModLoader.playerTeams[browserSelectIndex];

            browserTitle.text = team.name;
            browserDescription.text = team.description;
            browserThumbnail.sprite = team.thumbnail;
        }
        else
        {
            TGM_SosigTeam team = TGM_ModLoader.sosigTeams[browserSelectIndex];

            browserTitle.text = team.name;
            browserDescription.text = team.description;
            browserThumbnail.sprite = team.thumbnail;
        }
    }

    public void ConfirmBrowser()
    {
        TGM_Manager.PlayAudio(TGM_Manager.PlayAudioEnum.Confirm);

        if (browserMode == 0)
        {
            TGM_Manager.instance.team[browserTeamID].playerTeam = browserSelectIndex;
        }
        else
        {
            TGM_Manager.instance.team[browserTeamID].sosigTeam = browserSelectIndex;
        }

        //Clear old Buttons
        for (int i = browserButtons.Count - 1; i >= 0; i--)
        {
            if(browserButtons[i] != null)
                Destroy(browserButtons[i].gameObject);
        }
        browserButtons.Clear();

        browserPanel.SetActive(false);
        TGM_MainMenu.instance.UpdateSettings();
    }
}
