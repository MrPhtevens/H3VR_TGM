using System;
using System.Collections.Generic;
using FistVR;
using UnityEngine;

namespace TeamsGameMode;

public class TGM_Sosigs
{
    /*
    public static void UpdateAllSosigsAttackArea()
    {
        for (int i = 0; i < sosigs.Count; i++)
        {
            if (sosigs[i] != null)
                UpdateSosigAttackArea(sosigs[i]);
        }
    }
    */

    public static void OrderSosigToLocations(Sosig sosig, List<Vector3> locations, Sosig.SosigMoveSpeed moveSpeed = Sosig.SosigMoveSpeed.Running)
    {
        /*
        //Debug Sosig move locations
        for (int i = 0; i < locations.Count; i++)
        {
            Debug.Log("SOSIG MOVE LOCATION " + i + " : " + locations[i]);
        }
        */

        List<Vector3> pathDirs = new List<Vector3> { locations[2], locations[2] };
        locations.RemoveAt(2);
        List<Vector3> pathPoints = locations;

        sosig.CommandPathTo(
            pathPoints,
            pathDirs,
            1,
            Vector2.one * 4,
            20f,
            moveSpeed,
            Sosig.PathLoopType.LoopEndless,
            null,
            0.2f,
            1f,
            true,
            50f);

        sosig.UpdateAssaultPoint(pathPoints[0]);
        sosig.SetCurrentOrder(Sosig.SosigOrder.Assault);
    }
}
