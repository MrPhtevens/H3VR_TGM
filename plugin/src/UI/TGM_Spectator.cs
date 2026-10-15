using UnityEngine;
using UnityEngine.UI;
using H3MP.Networking;
using FistVR;
using System.Collections.Generic;


namespace TeamsGameMode;

public class TGM_Spectator : MonoBehaviour
{
    public static TGM_Spectator instance;

    public int targetPlayerIndex = 0;
    public Camera spectatorCamera;
    public int frameRate = 30;
    private float nextRenderTime = 0;

    public Transform target;
    public Vector3 targetOffset;

    public LayerMask mask;
    private RaycastHit hit;


    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        if (TGM_Scene.instance.spectatorStaticCameras != null
            && TGM_Scene.instance.spectatorStaticCameras.Length > 0
            && TGM_Scene.instance.spectatorStaticCameras[0] != null)
            transform.SetPositionAndRotation(
                TGM_Scene.instance.spectatorStaticCameras[0].position, 
                TGM_Scene.instance.spectatorStaticCameras[0].rotation);
            
        //Give us something to look at
        spectatorCamera.Render();
    }

    void FixedUpdate()
    {
        if (TGM_MainMenu.instance.pages[(int)TGM_MainMenu.Page.Spectator].activeSelf == false
            && GM.CurrentSceneSettings.GetCamObjectPoint() == null)
        {
            target = null;
        }
        else if (target == null)
        {
            //Next Sosig if we don't have something to look at
            GetNextTarget(1);
        }


        if (target == null)
            return;

        if(Input.GetKeyDown(KeyCode.RightArrow))
            GetNextTarget(1);
        if (Input.GetKeyDown(KeyCode.LeftArrow))
            GetNextTarget(-1);

        Vector3 targetHead = target.position + (Vector3.up * 1.5f);
        Vector3 targetBehind = target.position + (Vector3.up * 1.5f) + (-target.forward * 2);

        if (TGM_MainMenu.instance.spectateName.text == "")
            spectatorCamera.transform.position = targetBehind;
        else if (Physics.Linecast(targetHead, targetBehind, out hit, mask))
            spectatorCamera.transform.position = Vector3.Slerp(spectatorCamera.transform.position, hit.point, Time.deltaTime);
        else
            spectatorCamera.transform.position = Vector3.Slerp(spectatorCamera.transform.position, targetBehind, Time.deltaTime);

        spectatorCamera.transform.LookAt(targetHead);

        
        if (nextRenderTime <= Time.time)
        {
            spectatorCamera.Render();
            nextRenderTime = Time.time + (1f / frameRate);
        }
        
    }

    public void GetNextTarget(int adjust)
    {
        target = GetNewTarget(adjust);
    }

    Transform GetNewTarget(int adjust)
    {
        targetPlayerIndex += adjust;

        int sosigCount = 0;
        //int playerCount = 0;

        //Collect all sosigs
        List<TGM_Player> players = new List<TGM_Player>();

        for (int t = 0; t < TGM_Manager.instance.team.Length; t++)
        {
            for (int d = 0; d < TGM_Manager.instance.team[t].sosigsData.Count; d++)
            {
                if(TGM_Manager.instance.team[t].sosigsData[d].sosig != null)
                    players.Add(TGM_Manager.instance.team[t].sosigsData[d]);
            }
        }
        sosigCount = players.Count;

        if (sosigCount == 0)
            return null;

        int total = sosigCount + TGM_Scene.instance.spectatorStaticCameras.Length;// + playerCount;

        if (targetPlayerIndex >= total)
            targetPlayerIndex = 0;
        else if (targetPlayerIndex < 0)
            targetPlayerIndex = total - 1;

        //Static Camera
        if (targetPlayerIndex >= sosigCount)
        {
            int index = targetPlayerIndex - sosigCount;
            if (TGM_Scene.instance.spectatorStaticCameras[index] != null)
            {
                TGM_MainMenu.instance.spectateName.text = "";

                return TGM_Scene.instance.spectatorStaticCameras[index];
            }
            else
                return null;
        }

        //Sosig Player
        if (players[targetPlayerIndex] != null
            && players[targetPlayerIndex].sosig != null)
        {
            spectatorCamera.Render();
            TGM_MainMenu.instance.spectateName.text = players[targetPlayerIndex].playerName;
            TGM_MainMenu.instance.spectateName.color = players[targetPlayerIndex].sosig.GetIFF() == 1 ? TGM_Manager.instance.team[1].color : TGM_Manager.instance.team[0].color;
            return players[targetPlayerIndex].sosig.transform;
        }

        TGM_MainMenu.instance.spectateName.text = "";
        return null;
    }
}
