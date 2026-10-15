using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using FistVR;

namespace TeamsGameMode;

[Serializable]
public class TGM_Settings
{
    [Serializable]
    public class Setting
    {
        public string description;
        //public SettingEnum setting; //For Internal use only, doesn't do anything
        public string[] settings;
        [Tooltip("Does this use the settings string array as the output")]
        public SettingType type = SettingType.Strings;
        [Tooltip("Current value of this setting")]
        public int value = 0;
        [Tooltip("The Minimum possible value, Set to 0 with intMax for infinite")]
        public int intMin = 0;
        [Tooltip("The Maximum possible value, Set to 0 with intMin for infinite")]
        public int intMax = 64;
        [Tooltip("How much the value is incremented by")]
        public int intIncrement = 1;
        [Tooltip("If true, does not sync over H3MP")]
        public bool localOnly = false;
        [HideInInspector]
        public bool active = true;  //Disabled via code if not valid
        [HideInInspector]
        public TGM_Button button;

        public enum SettingType
        {
            Strings = 0,
            Int = 1,
            FirstString = 2,
        }
    }

    public static int GetSetting(TGMSettingEnum setting)
    {
        return gameSettings[(int)setting].value;
    }

    public static void SetSetting(TGMSettingEnum setting, int value)
    {
        gameSettings[(int)setting].value = value;
    }

    public static int GetModeSetting(int setting)
    {
        return gamemodeSettings[setting].value;
    }

    public static void SetModeSetting(int setting, int value)
    {
        gamemodeSettings[setting].value = value;
    }

    public static List<Setting> gamemodeSettings = new List<Setting>();
    public static List<Setting> gameSettings = new List<Setting>() 
    {
        new Setting
        {
            description = "Spawn Lock:",
            settings = ["Disabled (Global)", "Enabled (Global)", "Set Per Class"],
            type = Setting.SettingType.Strings,
            value = 2,
            intMin = 0,
            intMax = 2,
            intIncrement = 1,
            localOnly = false,
        },
        new Setting
        {
            description = "Wave Spawn Time:",
            settings = ["Map Default"],
            type = Setting.SettingType.FirstString,
            value = 0,
            intMin = 0,
            intMax = 600,
            intIncrement = 5,
            localOnly = false,
        },
        new Setting
        {
            description = "Time Limit:",
            settings = ["Infinite"],
            type = Setting.SettingType.FirstString,
            value = 0,
            intMin = 0,
            intMax = 3600,
            intIncrement = 30,
            localOnly = false,
        },
        /*
        new Setting
        {
            description = "Respawning:",
            settings = ["Disabled", "Enabled"],
            type = Setting.SettingType.Strings,
            value = 1,
            intMin = 0,
            intMax = 1,
            intIncrement = 1,
            localOnly = false,
        },
        */
        new Setting
        {
            description = "Show Friendlies:",
            settings = ["Disabled", "Enabled"],
            type = Setting.SettingType.Strings,
            value = 1,
            intMin = 0,
            intMax = 1,
            intIncrement = 1,
            localOnly = false,
        },
        new Setting
        {
            description = "Player Items Drop:",
            settings = ["Disabled", "Enabled"],
            type = Setting.SettingType.Strings,
            value = 0,
            intMin = 0,
            intMax = 1,
            intIncrement = 1,
            localOnly = false,
        },
        new Setting
        {
            description = "Sosig Weapons Drop:",
            settings = ["Disabled", "Enabled"],
            type = Setting.SettingType.Strings,
            value = 0,
            intMin = 0,
            intMax = 1,
            intIncrement = 1,
            localOnly = false,
        },
        new Setting
        {
            description = "Player Health:",
            settings = ["Set Per Class"],
            type = Setting.SettingType.FirstString,
            value = 0,
            intMin = 0,
            intMax = 100000,
            intIncrement = 250,
            localOnly = false,
        },
        new Setting
        {
            description = "Item Spawner:",
            settings = ["Disabled", "Enabled"],
            type = Setting.SettingType.Strings,
            value = 0,
            intMin = 0,
            intMax = 1,
            intIncrement = 1,
            localOnly = false,
        },
        new Setting
        {
            description = "Color Blind:",
            settings = ["Disabled", "Enabled"],
            type = Setting.SettingType.Strings,
            value = 0,
            intMin = 0,
            intMax = 1,
            intIncrement = 1,
            localOnly = true,
        },
    };
}

public enum TGMSettingEnum
{
    ///<summary>"Disabled (Global)" = 0, "Enabled (Global)" = 1, "Set Per Class" = 2</summary>
    SpawnLock,
    ///<summary>"Map Default" = 0, # => 1</summary>
    SpawnWaveTime,
    ///<summary>"Infinite" = 0, # => 1</summary>
    TimeLimit,
    ///<summary>"Disabled" = 0, "Enabled" = 1</summary>
    //CanRespawn = 3,
    ///<summary>"Disabled" = 0, "Enabled" = 1</summary>
    ShowFriendlies,
    ///<summary>"Disabled" = 0, "Enabled" = 1, "Weapons Only" = 2, "Ammo Only" = 3</summary>
    PlayerItemsOnDeath,
    ///<summary>"Disabled" = 0, "Enabled" = 1</summary>
    SosigWeapons,
    ///<summary>"Set Per Class" = 0, # => 1</summary>
    PlayerHealth,
    ///<summary>"Disabled" = 0, "Enabled" = 1</summary>
    ItemSpawner,
    ///<summary>"Disabled" = 0, "Enabled" = 1</summary>
    ColorBlind,
}
