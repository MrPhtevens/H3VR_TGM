using UnityEngine;
using UnityEngine.AI;
using FistVR;
using System.Collections.Generic;

namespace TeamsGameMode;

public class Global
{
    public static FVRObject GetObjectID(string objectID)
    {
        FVRObject mainObject;
        //Try to find the weapon ID
        if (!IM.OD.TryGetValue(objectID, out mainObject))
        {
            TGMPlugin.Logger.LogMessage($"Cannot find object with id: " + objectID);
            return null;
        }
        return mainObject;
    }

    public static FVRPhysicalObject SpawnFVRObject(FVRObject fvrObject, Vector3 position, Vector3 rotation)
    {
        if (fvrObject == null || fvrObject.GetGameObject() == null)
        {
            TGMPlugin.Logger.LogWarning("Failed to spawn FVRObject");
            return null;
        }

        FVRPhysicalObject spawnedMain = Object.Instantiate(fvrObject.GetGameObject(), position, Quaternion.Euler(rotation)).GetComponent<FVRPhysicalObject>();

        return spawnedMain;
    }

    public static void ReloadWithCartridge(FVRPhysicalObject container, FVRObject cartridge)
    {
        if (container == null || cartridge == null)
            return;

        FireArmRoundClass roundClass = GetFirearmRoundClassFromFVRObject(cartridge);
        FVRFireArmMagazine magazine = container as FVRFireArmMagazine;
        if (magazine != null)
        {
            magazine.ReloadMagWithType(roundClass);
        }
        FVRFireArmClip clip = container as FVRFireArmClip;
        if (clip != null)
        {
            clip.ReloadClipWithType(roundClass);
        }
        Speedloader speedLoader = container as Speedloader;
        if (speedLoader != null)
        {
            speedLoader.ReloadClipWithType(roundClass);
        }
    }

    public static Vector3[] GetValidSpawnPoint(Transform transform)
    {
        Vector3[] spawnData = new Vector3[2];
        Vector3 position = transform.position;
        Vector3 scale = transform.localScale / 2;
        Vector3 randomPosition
            = new Vector3(
                Random.Range(-scale.x, scale.x),
                Random.Range(-scale.y, scale.y),
                Random.Range(-scale.z, scale.z));

        //Assign Position
        if (NavMesh.SamplePosition(position + randomPosition, out NavMeshHit hit, transform.localScale.y, NavMesh.AllAreas))
            position = hit.position;

        spawnData[0] = position;
        spawnData[1] = transform.rotation.eulerAngles;

        return spawnData;
    }

    public static Vector3[] GetRandomPlayerSpawnPoint(Transform[] spawnPoints)
    {
        Transform spawn = spawnPoints[Random.Range(0, spawnPoints.Length)];
        return GetValidSpawnPoint(spawn);
    }

    public static void TeleportToPoint(Vector3[] pointData)
    {
        GM.CurrentMovementManager.TeleportToPoint(pointData[0],
            true,
            pointData[1]);
    }

    /// <summary>
    /// Returns the opposite team's iff to the input iff
    /// </summary>
    /// <param name="iff"></param>
    /// <returns></returns>
    public static int GetEnemyIFF(int iff)
    {
        return iff == 1 ? 0 : 1;
    }

    public static FireArmRoundClass GetFirearmRoundClassFromFVRObject(FVRObject round) //string itemID, FireArmRoundType t)
    {
        for (int i = 0; i < AM.SRoundDisplayDataDic[round.RoundType].Classes.Length; i++)
        {
            if (AM.SRoundDisplayDataDic[round.RoundType].Classes[i].ObjectID.ItemID == round.ItemID)
                return AM.SRoundDisplayDataDic[round.RoundType].Classes[i].Class;
        }

        return AM.SRoundDisplayDataDic[round.RoundType].Classes[0].Class;
    }

    public static Vector3 GetGridXZPositionTransform(Transform t, int index, int squareCount)
    {
        Vector3 scale = t.lossyScale;

        float xGrid = (scale.x / squareCount);
        float zGrid = (scale.z / squareCount);

        float xOffset = -(scale.x / 2) + (xGrid / 2);
        float zOffset = -(scale.z / 2) + (zGrid / 2);

        int row = index / squareCount;
        int col = index % squareCount;

        Vector3 position = t.position; //Transform Position

        position += t.rotation * new Vector3(xOffset + (row * xGrid), 0, zOffset + (col * zGrid));
        //position += (t.rotation * new Vector3(row * x, 0, col * z));    //Rotation offset
        //position -= -new Vector3(scale.x / 2, 0, scale.z / 2);

        return position ;
    }
}
