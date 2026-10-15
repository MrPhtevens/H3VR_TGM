using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TeamsGameMode;

[Serializable]
public class TGM_PlayerTeam
{
    [Tooltip("Name of this team")]
    public string name;
    [Tooltip("Short explanation of the team"), Multiline(6)]
    public string description;
    [Tooltip("Preview image of the team when selected")]
    public Sprite thumbnail;
    public TGM_PlayerClass[] playerClasses = new TGM_PlayerClass[1];
}
