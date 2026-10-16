using UnityEngine;
using UnityEngine.AI;

namespace TeamsGameMode;

/// <summary>
/// Position helpers for Sosig tactics, not tied to any gamemode
/// </summary>
public static class TGM_Tactics
{
    const float bodyHeight = 0.9f;      //Chest height when crouched behind something
    const float headHeight = 1.6f;      //Standing head height, to shoot over cover
    const float coverCheckDistance = 5; //Cover has to be this close in the threat direction

    static int coverMask = 0;

    /// <summary>
    /// Map geometry layers (Default + Environment if the game has it)
    /// </summary>
    static int CoverMask
    {
        get
        {
            if (coverMask == 0)
            {
                coverMask = 1 << LayerMask.NameToLayer("Default");
                int environment = LayerMask.NameToLayer("Environment");
                if (environment >= 0)
                    coverMask |= 1 << environment;
            }
            return coverMask;
        }
    }

    /// <summary>
    /// Pick a spot near 'around' that has cover from the threat direction, ideally low cover to shoot over
    /// </summary>
    public static Vector3 FindCover(Vector3 around, Vector3 threatDirection, float radius, int samples = 8)
    {
        Vector3 threat = Flatten(threatDirection);
        Vector3 best = SampleNavMesh(around);
        if (threat == Vector3.zero)
            return best;

        float bestScore = CoverScore(best, threat);
        for (int i = 0; i < samples && bestScore < 3; i++)
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(around + new Vector3(offset.x, 0, offset.y), out hit, 3, NavMesh.AllAreas))
                continue;

            //Small penalty for drifting away from where we were asked to be
            float score = CoverScore(hit.position, threat) - Vector3.Distance(hit.position, around) * 0.05f;
            if (score > bestScore)
            {
                best = hit.position;
                bestScore = score;
            }
        }
        return best;
    }

    /// <summary>
    /// 2 for something solid in front at chest height, +1 if the head is clear to shoot over it
    /// </summary>
    static float CoverScore(Vector3 position, Vector3 threat)
    {
        float score = 0;
        if (Physics.Raycast(position + Vector3.up * bodyHeight, threat, coverCheckDistance, CoverMask, QueryTriggerInteraction.Ignore))
        {
            score += 2;
            if (!Physics.Raycast(position + Vector3.up * headHeight, threat, coverCheckDistance, CoverMask, QueryTriggerInteraction.Ignore))
                score += 1;
        }
        return score;
    }

    public static Vector3 SampleNavMesh(Vector3 point, float radius = 8)
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(point, out hit, radius, NavMesh.AllAreas))
            return hit.position;
        return point;
    }

    public static Vector3 Flatten(Vector3 direction)
    {
        direction.y = 0;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
    }

    /// <summary>
    /// A point out from 'center' along a direction, with some sideways spread
    /// </summary>
    public static Vector3 AlongDirection(Vector3 center, Vector3 direction, float minDistance, float maxDistance, float lateral)
    {
        Vector3 side = Vector3.Cross(Vector3.up, direction);
        return center
            + direction * Random.Range(minDistance, maxDistance)
            + side * Random.Range(-lateral, lateral);
    }

    /// <summary>
    /// A point in a ring around 'center', limited to an arc facing a direction
    /// </summary>
    public static Vector3 RandomArc(Vector3 center, Vector3 facing, float arcDegrees, float minRadius, float maxRadius)
    {
        Vector3 dir = Quaternion.Euler(0, Random.Range(-arcDegrees / 2, arcDegrees / 2), 0) * facing;
        return center + dir * Random.Range(minRadius, maxRadius);
    }

    public static Vector3 RandomRing(Vector3 center, float minRadius, float maxRadius)
    {
        Vector2 dir = Random.insideUnitCircle.normalized;
        return center + new Vector3(dir.x, 0, dir.y) * Random.Range(minRadius, maxRadius);
    }
}
