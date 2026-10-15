using UnityEngine;
using UnityEngine.UI;
using FistVR;
using System.Collections.Generic;

namespace TeamsGameMode;

public class TGM_Compass : MonoBehaviour
{
    public static TGM_Compass instance;

    public float distance = .25f;

    [Header("Health")]
    public Image healthFill;
    public Color healthFull = Color.green;
    public Color healthEmpty = Color.red;

    [Header("Stats")]
    public Text killText;
    public Text deathText;
    public Text scoreText;
    public Text gameTimeText;

    [Header("Teams")]
    public Text redScoreText;
    public Text blueScoreText;
    public Transform scorePanel;
    public Image[] cornerBackgrounds;

    [Header("Markers")]
    public List<Marker> markers = new List<Marker>();
    public GameObject markerPrefab;

    public Sprite[] markerSprites;

    [Header("Direction")]
    public Transform directionNeedle;
    public Text directionText;

    [System.Serializable]
    public class Marker
    {
        public Transform parent;    //Center of compass
        public Transform marker;    //Edge of compass
        public Image thumbnail;
        public Transform target;

        public void LookAtTarget()
        {
            //If no target, destory it
            if (target == null)
            {
                instance.markers.Remove(this);
                Destroy(parent.gameObject);
                return;
            }

            parent.LookAt(target.position);

            parent.rotation = Quaternion.LookRotation(target.position - parent.position, Vector3.up);
            parent.rotation = Quaternion.Euler(0, parent.rotation.eulerAngles.y, 0);
            //marker.rotation = Quaternion.LookRotation(marker.position, Vector3.up);
        }
    }

    public void SetMarkerTarget(Marker marker, Transform newTarget, MarkerEnum type)
    {
        marker.target = newTarget;
        //marker.thumbnail.color = markerColors[(int)type];
        marker.thumbnail.sprite = markerSprites[(int)type];

    }

    public void CreateMarker(Sprite sprite, Color color, Transform lookAt)
    {
        GameObject newMarker = Instantiate(markerPrefab, markerPrefab.transform.parent);
        TGM_Button markerBtn = newMarker.GetComponent<TGM_Button>();

        Marker marker = new Marker
        {
            parent = newMarker.transform,
            marker = markerBtn.go.transform,
            thumbnail = markerBtn.go.GetComponent<Image>(),
            target = lookAt
        };
        newMarker.SetActive(true);

        marker.thumbnail.sprite = sprite;
        marker.thumbnail.color = color;
        markers.Add(marker);
    }

    public static void ClearAllMarkers()
    {
        for (int i = instance.markers.Count - 1; i >= 0; i--)
        {
            Destroy(instance.markers[i].parent.gameObject);
        }
        instance.markers.Clear();
    }

    public enum MarkerEnum
    {
        Attack,
        Defend,
        Objective,
        Person,
    }

    void Awake()
    {
        instance = this;
    }

    void Update()
    {
        //Yikes
        if (TGM_Manager.instance == null
            || TGM_Manager.gameState == TGM_Manager.GameStateEnum.GamemodeSelect
            || TGM_Manager.gameState == TGM_Manager.GameStateEnum.Setup)
            return;

        //Compass Position
        if (!TGM_MainMenu.handSide)
            transform.position = GM.CurrentPlayerBody.LeftHand.position - GM.CurrentPlayerBody.LeftHand.forward * distance;
        else
            transform.position = GM.CurrentPlayerBody.RightHand.position - GM.CurrentPlayerBody.RightHand.forward * distance;
        transform.rotation = Quaternion.identity;

        //Markers
        for (int i = 0; i < markers.Count; i++)
        {
            if(markers[i] != null)
                markers[i].LookAtTarget();
        }

        //Stats
        killText.text = TGM_Manager.instance.localPlayer.kills.ToString();
        deathText.text = TGM_Manager.instance.localPlayer.deaths.ToString();

        int playerIFF = TGM_Manager.instance.localPlayer.iff;
        if(playerIFF == 0 || playerIFF == 1)
            scoreText.text = TGM_Manager.instance.team[playerIFF].currentScore.ToString();

        //Health
        healthFill.fillAmount = GM.GetPlayerHealth();
        healthFill.color = Color.Lerp(healthEmpty, healthFull, GM.GetPlayerHealth());

        //Direction
        directionNeedle.LookAt(GM.CurrentPlayerBody.Head.position);
        directionText.text = Mathf.FloorToInt(directionNeedle.eulerAngles.y).ToString();
        directionNeedle.rotation = Quaternion.Euler(90, directionNeedle.eulerAngles.y + 180, 0);

        //Team Score - If the panel is enabled
        if (scorePanel.gameObject.activeSelf)
        {
            scorePanel.LookAt(GM.CurrentPlayerBody.Head.position);
            scorePanel.rotation = Quaternion.Euler(90, scorePanel.eulerAngles.y + 180, 0);
            redScoreText.text = TGM_Manager.instance.team[0].currentScore.ToString();
            blueScoreText.text = TGM_Manager.instance.team[1].currentScore.ToString();
        }

    }
}
