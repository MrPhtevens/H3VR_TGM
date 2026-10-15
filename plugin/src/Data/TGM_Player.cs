using System.Collections.Generic;
using UnityEngine;
using FistVR;

namespace TeamsGameMode;

[System.Serializable]
public class TGM_Player
{
    [Header("Sosig")]
    public bool isSosig = false;    //is this a sosig
    public string playerName;
    public Sosig sosig;

    [Header("Human")]
    public int playerIndex = 0; //H3MP data
    public int classIndex = 0; // Class ID
    public bool awaitingRespawn = false;

    [Header("Stats")]
    public int iff = 0; //Default team
    public int kills = 0;
    public int deaths = 0;
    public int score = 0;   //Player score

    [Header("Data")]
    public List<FVRPhysicalObject> playersItems = new List<FVRPhysicalObject>();
    public GameObject allyMarker;

    public void ResetPlayer()
    {
        kills = 0;
        deaths = 0;
        score = 0;
        awaitingRespawn = false;
    }

    public void DestroyPlayersItems()
    {
        for (int i = 0; i < playersItems.Count; i++)
        {
            if (playersItems[i] != null)
                Object.Destroy(playersItems[i].gameObject);
        }
        playersItems.Clear();

        TGM_ClassMenu.ResetSpawnPoints();
    }
}
