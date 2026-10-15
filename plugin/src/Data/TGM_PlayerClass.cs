using System;
using UnityEngine;
using UnityEngine.UI;
using FistVR;

namespace TeamsGameMode;

[Serializable]
public class TGM_PlayerClass
{
    public string name;
    public Sprite thumbnail;
    public string spriteName = "Image.png";
    public bool canSpawnLock = true;    //Spawnlock per class
    public int playerHealth = 5000;   //How much health does this class have
    public int minKills = -1;   //Minimum kills before usable / -1 no min limit
    public int maxKills = -1;   //Maximum kills before unusable / -1 max limit
    public SubClass[] subClasses;

    [Serializable]
    public class SubClass
    {
        public string name; //For Readiblity mostly
        public ItemSet[] items;
    }

    [Serializable]
    public class ItemSet()
    {
        public string name; //For readiblity
        public int team = -1;   //What Team this is avalible on, -1 means all
        [Header("Objects")]
        public bool requiredSecondaryPieces = true;
        public bool uniformObjects = false; //If True will only spawn one type of objectID
        public int objectCount = 1;
        public string[] objectID;   //Randomly Select per ObjectCount

        [Header("-Ammo-")]
        public int ammoCount = 1;
        public string ammoContainerID = "";  //Magazine / Clip / Speedloader
        public string cartridgeID = "";          // FMJ / AP etc
    }
}
