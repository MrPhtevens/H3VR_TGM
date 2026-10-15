using System;
using FistVR;
using UnityEngine;
using H3MP.Networking;
using System.Collections.Generic;


namespace TeamsGameMode;

[Serializable]
public class TGM_Payload: TGM_Gamemode
{

    public TGM_Payload(string modeName = "", string modeDescription = "", Sprite modeThumbnail = null)
    {
        name = modeName;
        description = modeDescription;
        thumbnail = modeThumbnail;
    }
}