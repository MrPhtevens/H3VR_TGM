using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TeamsGameMode;

[Serializable]
public class TGM_SosigTeam
{
    [Tooltip("Name of this team of Sosigs")]
    public string name = "";
    [Tooltip("Short explanation of the team of sosigs"), Multiline(6)]
    public string description = "A short description of this sosig faction";
    [Tooltip("Preview image of the team when selected")]
    public Sprite thumbnail;
    [Tooltip("Do Sosigs spawn in a linear order")]
    public bool spawnInOrder = false;
    public SosigSet[] sosigSet;

    [Serializable]
    public class SosigSet
    {
        public int minKills = -1;
        public int maxKills = -1;
        public bool useVehicleSpawn = false;

        //[Tooltip("SosigEnemyID list, add duplicate IDs to weight them more likely to be picked")]
        //public int[] sosigEnemyIDs;
        [Tooltip("SosigEnemyID list for the RED Team, add duplicate IDs to weight them more likely to be picked")]
        public int[] sosigEnemyIDsRed;
        [Tooltip("SosigEnemyID list for the BLUE Team, add duplicate IDs to weight them more likely to be picked")]
        public int[] sosigEnemyIDsBlue;

        /*
        public int GetRandomSosigEnemyID()
        {
            return sosigEnemyIDs[Random.Range(0, sosigEnemyIDs.Length)];
        }
        */
    }

}
