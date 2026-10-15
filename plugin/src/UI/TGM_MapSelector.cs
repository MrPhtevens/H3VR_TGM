using Atlas;
using FistVR;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TeamsGameMode;

public class TGM_MapSelector : MonoBehaviour
{
	public static TGM_MapSelector instance;

	public Transform buttonContent;
	private List<TGM_Button> mapButtons = new List<TGM_Button>();
	public GameObject mapPrefab;

	private List<CustomSceneInfo> collectedScenes = new List<CustomSceneInfo>();
	private int pageIndex = 0;

	public int mapPageCount = 12;   //Public so modding???

	[Header("Selected Map")]
	public Text mapTitle;
	public Text mapDescription;
	public RawImage mapThumbnail;
	private CustomSceneInfo selectedScene;

	[Header("Take and Hold")]
	public Texture2D tnhTexture;
	public GameObject tnhButton;

	private void Awake()
	{
		instance = this;
	}

	
	private void Start()
	{
		foreach (CustomSceneInfo customSceneInfo in AtlasPlugin.CustomSceneInfos)
		{
			if (customSceneInfo.GameMode == "teamsgamemode"
                || customSceneInfo.DisplayMode == "teamsgamemode"
                || customSceneInfo.DisplayName.Contains("TGM"))
			{
				collectedScenes.Add(customSceneInfo);
			}
		}

		//Default to nothing
		mapTitle.text = "";
		if(collectedScenes.Count != 0)
			mapDescription.text = "";

		//Generate initial list
		GenerateMapButtonsFrom(pageIndex);
	}

	public void ScrollMaps(int amount)
	{
		pageIndex += amount * mapPageCount;

		if (pageIndex < 0)
			pageIndex = 0;
		else if(pageIndex >= collectedScenes.Count)
			pageIndex = collectedScenes.Count - 1;
		GenerateMapButtonsFrom(pageIndex);

		SM.PlayGlobalUISound(SM.GlobalUISound.Boop, GM.CurrentPlayerBody.transform.position);
	}

	void GenerateMapButtonsFrom(int i)
	{
		//Clear Old Buttons
		for (int j = 0; j < mapButtons.Count; j++)
		{
			Destroy(mapButtons[j].gameObject);
		}
		mapButtons.Clear();

		//Generate Pages
		for (int c = 0; i < collectedScenes.Count; i++, c++)
		{
			//Only support for mapPageCount maps per page
			if (c >= mapPageCount)
				break;

			TGM_Button btn = Instantiate(mapPrefab, buttonContent).GetComponent<TGM_Button>();
			btn.index = i;
			btn.go.GetComponent<RawImage>().texture = collectedScenes[i].ThumbnailTexture;
			btn.texts[0].text = collectedScenes[i].DisplayName;
			btn.gameObject.SetActive(true);
			mapButtons.Add(btn);
		}
	}

	public void SelectMap(int index)
	{
		selectedScene = collectedScenes[index];
		mapTitle.text = selectedScene.DisplayName;
		mapDescription.text = selectedScene.Description;
		mapThumbnail.texture = selectedScene.ThumbnailTexture;
		SM.PlayGlobalUISound(SM.GlobalUISound.Boop, GM.CurrentPlayerBody.transform.position);
	}

	public void SelectTnH()
	{
		selectedScene = null;
		mapTitle.text = "Take And Hold";
		mapDescription.text = "Game \n" +
			"- Get Loot!\n" +
			"- Take Capture Points!\n" +
			"- Defend Them!\n" +
			"- Go As Long As You Can!";
		mapThumbnail.texture = tnhTexture;
		SM.PlayGlobalUISound(SM.GlobalUISound.Boop, GM.CurrentPlayerBody.transform.position);
	}

	public void LaunchMap()
	{
		if (selectedScene == null)
		{
			if (mapTitle.text == "Take And Hold")
			{
				SteamVR_LoadLevel.Begin("TakeAndHold_Lobby_2", false, 0.5f, 0f, 0f, 0f, 1f);
			}
			else
			{
				SM.PlayGlobalUISound(SM.GlobalUISound.Boop, GM.CurrentPlayerBody.transform.position);
				return;
			}
		}
		else
			AtlasPlugin.LoadCustomScene(selectedScene);

		SM.PlayGlobalUISound(SM.GlobalUISound.Beep, GM.CurrentPlayerBody.transform.position);
	}
}