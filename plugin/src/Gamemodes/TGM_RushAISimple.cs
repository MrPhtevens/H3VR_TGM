using System.Collections.Generic;
using FistVR;
using UnityEngine;
using UnityEngine.AI;

namespace TeamsGameMode;

/// <summary>
/// Rush Sosig orders, host only. Each team has one operator who works the Mcom (Red arms / holds it,
/// Blue disarms it), everyone else gets spread out roles around it that rotate over time.
/// </summary>
public class TGM_RushAISimple : ITGM_RushAI
{
    const int redIFF = TGM_Gamemode.redIFF;
    const int blueIFF = TGM_Gamemode.blueIFF;

    //------------------------------------------------------------------------------
    // Tuning
    //------------------------------------------------------------------------------

    const float tickInterval = 1;           //How often orders are checked
    static float ReorderMin(int iff) => iff == redIFF ? TGM_Config.AttackerReorderMin.Value : TGM_Config.DefenderReorderMin.Value;
    static float ReorderMax(int iff) => iff == redIFF ? TGM_Config.AttackerReorderMax.Value : TGM_Config.DefenderReorderMax.Value;
    const float onPointRadius = 2.5f;       //Close enough to the Mcom to arm / disarm it

    //Attackers, Mcom not armed (rest push forward along the approach)
    static float redEscortChance => TGM_Config.AttackerEscortChance.Value;     //Move up with the operator on the approach side
    static float redFlankChance => TGM_Config.AttackerFlankChance.Value;     //Go through the map's attack areas
    //Attackers, Mcom armed (rest hunt towards the defenders' side)
    static float redGuardArmedChance => TGM_Config.AttackerGuardArmedChance.Value;

    //Defenders, nothing happening (rest anchor near the Mcom)
    static float blueForwardChance => TGM_Config.DefenderForwardChance.Value;   //Meet the attack on the approach
    static float blueLaneChance => TGM_Config.DefenderLaneChance.Value;      //The map's defend areas
    static float blueOverwatchChance => TGM_Config.DefenderOverwatchChance.Value;//Ring at mid range around the Mcom
    //Defenders, Mcom being armed / armed
    static float blueReactRange => TGM_Config.DefenderReactRange.Value;        //Defenders this close react
    static float blueResponderChance => TGM_Config.DefenderResponderChance.Value;//Close in and fight, rest hold an overwatch ring

    //------------------------------------------------------------------------------

    const float contestedLinger = 4;        //Arming has to stop for this long before anyone treats it as stopped

    enum RedRole { Escort, Assault, Flank }

    readonly Dictionary<Sosig, RedRole> redRoles = new Dictionary<Sosig, RedRole>();  //Attackers keep their job for life
    float lastContested = -100;
    readonly Dictionary<Sosig, float> nextOrder = new Dictionary<Sosig, float>();
    readonly Dictionary<Sosig, float> defaultPushRange = new Dictionary<Sosig, float>();
    readonly Dictionary<Sosig, float> defaultHearing = new Dictionary<Sosig, float>();
    readonly Sosig[] operators = new Sosig[2];
    const float operatorCheckInterval = 0.25f;  //Operators are kept on task more often than everyone else
    float nextOperatorCheck = 0;
    float nextTick = 0;
    int lastPhase = -1;

    enum Phase { Idle = 0, Arming = 1, Armed = 2 }

    /// <summary>
    /// New objective or new game, everyone gets fresh orders and operators are picked again
    /// </summary>
    public void Reset()
    {
        nextOrder.Clear();
        for (int i = 0; i < operators.Length; i++)
            SetOperator(i, null);
        nextTick = 0;
        lastPhase = -1;
    }

    public void ResetAll()
    {
        Reset();
        redRoles.Clear();
        lastContested = -100;
    }

    public void Update(Rush_CapturePoint objective, TGM_Area defenderArea, bool isReversed)
    {
        //Pull operators back straight away if anything distracted them
        if (Time.time >= nextOperatorCheck)
        {
            nextOperatorCheck = Time.time + operatorCheckInterval;
            for (int i = 0; i < operators.Length; i++)
            {
                if (operators[i] != null && operators[i].BodyState != Sosig.SosigBodyState.Dead)
                    HoldMcom(operators[i], objective.transform.position);
            }
        }

        if (Time.time < nextTick)
            return;
        nextTick = Time.time + tickInterval;

        Vector3 mcom = objective.transform.position;
        Phase phase = SmoothedPhase(objective);
        bool phaseChanged = (int)phase != lastPhase;
        lastPhase = (int)phase;

        Forget();

        for (int iff = 0; iff < TGM_Manager.instance.team.Length; iff++)
        {
            //Remove destroyed or dead Sosigs so they don't block respawns
            TGM_Manager.instance.team[iff].sosigs.RemoveAll(s => s == null || s.BodyState == Sosig.SosigBodyState.Dead);
        }

        //Red always works the Mcom, Blue only once there is something to disarm
        UpdateOperator(redIFF, true, mcom);
        UpdateOperator(blueIFF, phase == Phase.Armed, mcom);

        Vector3 approach = GetApproachDirection(mcom);
        for (int iff = 0; iff < TGM_Manager.instance.team.Length && iff < operators.Length; iff++)
        {
            List<Sosig> sosigs = TGM_Manager.instance.team[iff].sosigs;
            for (int i = 0; i < sosigs.Count; i++)
            {
                Sosig s = sosigs[i];
                if (s == operators[iff])
                {
                    HoldMcom(s, mcom);
                    continue;
                }

                float next;
                if (phaseChanged || !nextOrder.TryGetValue(s, out next) || Time.time >= next)
                    Order(s, iff, mcom, phase, approach, defenderArea, isReversed);
            }
        }
    }

    /// <summary>
    /// Freshly spawned Sosig, give it a role straight away
    /// </summary>
    public void OrderNew(Sosig s, Rush_CapturePoint objective, TGM_Area defenderArea, bool isReversed)
    {
        Vector3 mcom = objective.transform.position;
        Order(s, s.GetIFF(), mcom, SmoothedPhase(objective), GetApproachDirection(mcom), defenderArea, isReversed);
    }

    /// <summary>
    /// Arming flickers on and off as the operator steps on and off the Mcom or dies,
    /// only treat it as stopped once it has stayed stopped for a few seconds
    /// </summary>
    Phase SmoothedPhase(Rush_CapturePoint objective)
    {
        Phase phase = GetPhase(objective);
        if (phase != Phase.Idle)
            lastContested = Time.time;
        else if (Time.time - lastContested < contestedLinger)
            phase = Phase.Arming;
        return phase;
    }

    static Phase GetPhase(Rush_CapturePoint objective)
    {
        switch (objective.state)
        {
            case Rush_CapturePoint.McomState.Arming:
                return Phase.Arming;
            case Rush_CapturePoint.McomState.Armed:
            case Rush_CapturePoint.McomState.Disarming:
                return Phase.Armed;
            default:
                return Phase.Idle;
        }
    }

    //------------------------------------------------------------------------------
    // Operators
    //------------------------------------------------------------------------------

    void UpdateOperator(int iff, bool needed, Vector3 mcom)
    {
        Sosig current = operators[iff];
        bool alive = current != null && current.BodyState != Sosig.SosigBodyState.Dead;

        if (!needed)
        {
            SetOperator(iff, null);
            return;
        }
        if (alive)
            return;

        //Operator died or was never picked, the closest living teammate takes over
        Sosig closest = null;
        float closestDistance = float.MaxValue;
        List<Sosig> sosigs = TGM_Manager.instance.team[iff].sosigs;
        for (int i = 0; i < sosigs.Count; i++)
        {
            float distance = Vector3.Distance(sosigs[i].transform.position, mcom);
            if (distance < closestDistance)
            {
                closest = sosigs[i];
                closestDistance = distance;
            }
        }
        SetOperator(iff, closest);
    }

    void SetOperator(int iff, Sosig s)
    {
        Sosig previous = operators[iff];
        if (previous == s)
            return;

        //Old operator goes back to normal fighting and gets a new role next tick
        if (previous != null)
        {
            float range;
            if (defaultPushRange.TryGetValue(previous, out range))
                previous.m_assaultPointOverridesSkirmishPointWhenFurtherThan = range;
            if (defaultHearing.TryGetValue(previous, out range))
                previous.MaxHearingRange = range;
            nextOrder.Remove(previous);
        }

        operators[iff] = s;
        if (s == null)
            return;

        //Keep pushing to the Mcom even while fighting
        if (!defaultPushRange.ContainsKey(s))
            defaultPushRange[s] = s.m_assaultPointOverridesSkirmishPointWhenFurtherThan;
        s.m_assaultPointOverridesSkirmishPointWhenFurtherThan = 1;

        //Deaf while on the job so noises don't send it off investigating, it still sees and shoots enemies
        if (!defaultHearing.ContainsKey(s))
            defaultHearing[s] = s.MaxHearingRange;
        s.MaxHearingRange = 0;
    }

    /// <summary>
    /// Run to the Mcom, once there hard guard it (stands on it, shoots back, doesn't wander)
    /// </summary>
    static void HoldMcom(Sosig s, Vector3 mcom)
    {
        bool onMcom = Vector3.Distance(s.transform.position, mcom) <= onPointRadius;
        if (TGM_AIDebug.Enabled)
        {
            string team = s.GetIFF() == redIFF ? "Red OPERATOR\n" : "Blue OPERATOR\n";
            string job = s.GetIFF() == redIFF ? "arming / holding Mcom" : "disarming Mcom";
            TGM_AIDebug.Show(s, team + (onMcom ? job : "running to Mcom"), TGM_AIDebug.OperatorColor, mcom);
        }

        if (onMcom)
        {
            bool holding = s.CurrentOrder == Sosig.SosigOrder.GuardPoint
                && s.m_hardGuard
                && Vector3.Distance(s.GetGuardPoint(), mcom) <= onPointRadius;
            if (!holding)
            {
                s.CommandGuardPoint(SampleNavMesh(RandomRing(mcom, 0, 1)), true);
                s.FallbackOrder = Sosig.SosigOrder.GuardPoint;
            }
        }
        else
        {
            bool pushing = s.CurrentOrder == Sosig.SosigOrder.Assault
                && Vector3.Distance(s.m_assaultPoint, mcom) <= onPointRadius;
            if (!pushing)
            {
                s.SetAssaultSpeed(Sosig.SosigMoveSpeed.Running);
                s.CommandAssaultPoint(SampleNavMesh(RandomRing(mcom, 0, 1)));
                s.FallbackOrder = Sosig.SosigOrder.Assault;
            }
        }
    }

    //------------------------------------------------------------------------------
    // Roles
    //------------------------------------------------------------------------------

    void Order(Sosig s, int iff, Vector3 mcom, Phase phase, Vector3 approach, TGM_Area defenderArea, bool isReversed)
    {
        if (s == null || s.BodyState == Sosig.SosigBodyState.Dead || s == operators[iff % operators.Length])
            return;

        //Stagger re-orders so the teams don't move in lockstep
        nextOrder[s] = Time.time + Random.Range(ReorderMin(iff), ReorderMax(iff));
        float roll = Random.value;

        if (iff == redIFF)
        {
            //ATTACKERS
            if (phase == Phase.Armed)
            {
                if (roll < redGuardArmedChance)
                    CommandSosig(s, RandomRing(mcom, 5, 15), false, true, "Red Guard armed Mcom");              //Spread around it, protect the operator
                else
                    CommandSosig(s, AlongApproach(mcom, -approach, 12, 25, 12), true, false, "Red Hunter");  //Hunt defenders coming in
            }
            else
            {
                //Every attacker job ends at the Mcom, and attackers never get sent backwards
                RedRole role = GetRedRole(s);
                Vector3 point = PickForward(s, mcom, approach, role);

                //Escorts dig in around the operator once arming starts, everyone else keeps pushing
                bool dig = role == RedRole.Escort && phase == Phase.Arming;
                CommandSosig(s, point, !dig, dig, "Red " + role);
            }
        }
        else
        {
            //DEFENDERS
            bool contested = phase != Phase.Idle;
            bool inRange = Vector3.Distance(s.transform.position, mcom) <= blueReactRange;

            if (contested && inRange)
            {
                if (roll < blueResponderChance)
                    CommandSosig(s, RandomRing(mcom, 3, 7), false, true, "Blue Responder");       //Close in and fight
                else
                    CommandSosig(s, RandomRing(mcom, 10, 22), false, true, "Blue Cover ring");     //Overwatch, cover the responders
            }
            else if (roll < blueForwardChance)
            {
                CommandSosig(s, AlongApproach(mcom, approach, 25, 50, 15), false, false, "Blue Forward");
            }
            else if (roll < blueForwardChance + blueLaneChance && defenderArea != null)
            {
                List<Vector3> area = defenderArea.GetRandomDefendArea(isReversed);
                CommandSosig(s, area[0], false, false, "Blue Lane");
            }
            else if (roll < blueForwardChance + blueLaneChance + blueOverwatchChance)
            {
                CommandSosig(s, RandomRing(mcom, 10, 25), false, false, "Blue Overwatch");
            }
            else
            {
                CommandSosig(s, RandomRing(mcom, 5, 9), false, false, "Blue Anchor");
            }
        }
    }

    /// <summary>
    /// Attackers get a job when first seen and keep it, so they don't change their mind every re-order
    /// </summary>
    RedRole GetRedRole(Sosig s)
    {
        RedRole role;
        if (redRoles.TryGetValue(s, out role))
            return role;

        float roll = Random.value;
        if (roll < redEscortChance)
            role = RedRole.Escort;
        else if (roll < redEscortChance + redFlankChance)
            role = RedRole.Flank;
        else
            role = RedRole.Assault;
        redRoles[s] = role;
        return role;
    }

    /// <summary>
    /// A spot for this attacker's job that is no further from the Mcom than it already is
    /// </summary>
    static Vector3 PickForward(Sosig s, Vector3 mcom, Vector3 approach, RedRole role)
    {
        float current = Vector3.Distance(s.transform.position, mcom);
        for (int attempt = 0; attempt < 4; attempt++)
        {
            Vector3 point;
            switch (role)
            {
                case RedRole.Escort:
                    point = RandomArc(mcom, approach, 110, 5, 12);      //Operator's side of the Mcom
                    break;
                case RedRole.Flank:
                    //Always the same side for the same Sosig
                    Vector3 side = Vector3.Cross(Vector3.up, approach) * (s.GetInstanceID() % 2 == 0 ? 1 : -1);
                    point = RandomArc(mcom, side, 80, 6, 14);
                    break;
                default:
                    point = RandomRing(mcom, 2, 8);                     //On top of the objective
                    break;
            }

            if (Vector3.Distance(point, mcom) <= current + 2)
                return point;
        }
        return RandomRing(mcom, 2, 8);
    }

    /// <summary>
    /// Assault pushes hard to the point, guard holds the point but still engages enemies.
    /// Hard guard keeps them at the point instead of wandering off to investigate noises.
    /// </summary>
    static void CommandSosig(Sosig s, Vector3 point, bool assault, bool hardGuard, string debug)
    {
        point = SampleNavMesh(point);
        bool fighting = s.CurrentOrder == Sosig.SosigOrder.Skirmish;

        if (TGM_AIDebug.Enabled)
        {
            string action = fighting ? "fighting, then " : "";
            action += assault ? "assault" : (hardGuard ? "hold (hard)" : "hold");
            Color color = s.GetIFF() == redIFF ? TGM_AIDebug.AttackColor : TGM_AIDebug.DefendColor;
            TGM_AIDebug.Show(s, debug + "\n" + action, color, point);
        }

        //Already fighting, keep fighting and head here once it's over
        if (fighting)
        {
            if (assault)
                s.m_assaultPoint = point;
            else
                s.m_guardPoint = point;
            s.FallbackOrder = assault ? Sosig.SosigOrder.Assault : Sosig.SosigOrder.GuardPoint;
            return;
        }

        if (assault)
        {
            s.SetAssaultSpeed(Sosig.SosigMoveSpeed.Running);
            s.CommandAssaultPoint(point);
            s.FallbackOrder = Sosig.SosigOrder.Assault;
        }
        else
        {
            s.CommandGuardPoint(point, hardGuard);
            s.FallbackOrder = Sosig.SosigOrder.GuardPoint;
        }
    }

    //------------------------------------------------------------------------------
    // Positions
    //------------------------------------------------------------------------------

    /// <summary>
    /// Flat direction from the Mcom towards the attackers' spawn
    /// </summary>
    static Vector3 GetApproachDirection(Vector3 mcom)
    {
        TGM_Area redSpawn = TGM_Manager.instance.team[redIFF].currentSpawnArea;
        if (redSpawn != null)
        {
            Vector3 dir = redSpawn.transform.position - mcom;
            dir.y = 0;
            if (dir.sqrMagnitude > 1)
                return dir.normalized;
        }
        Vector2 random = Random.insideUnitCircle.normalized;
        return new Vector3(random.x, 0, random.y);
    }

    /// <summary>
    /// A point out from the Mcom along a direction, with some sideways spread
    /// </summary>
    static Vector3 AlongApproach(Vector3 mcom, Vector3 direction, float minDistance, float maxDistance, float lateral)
    {
        Vector3 side = Vector3.Cross(Vector3.up, direction);
        return mcom
            + direction * Random.Range(minDistance, maxDistance)
            + side * Random.Range(-lateral, lateral);
    }

    /// <summary>
    /// A point in a ring around the Mcom, limited to an arc facing a direction
    /// </summary>
    static Vector3 RandomArc(Vector3 center, Vector3 facing, float arcDegrees, float minRadius, float maxRadius)
    {
        Vector3 dir = Quaternion.Euler(0, Random.Range(-arcDegrees / 2, arcDegrees / 2), 0) * facing;
        return center + dir * Random.Range(minRadius, maxRadius);
    }

    static Vector3 RandomRing(Vector3 center, float minRadius, float maxRadius)
    {
        Vector2 dir = Random.insideUnitCircle.normalized;
        return center + new Vector3(dir.x, 0, dir.y) * Random.Range(minRadius, maxRadius);
    }

    static Vector3 SampleNavMesh(Vector3 point)
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(point, out hit, 8, NavMesh.AllAreas))
            return hit.position;
        return point;
    }

    /// <summary>
    /// Drop Sosigs that no longer exist (destroyed Unity objects compare equal to null)
    /// </summary>
    void Forget()
    {
        RemoveDestroyed(nextOrder);
        RemoveDestroyed(defaultPushRange);
        RemoveDestroyed(defaultHearing);
        RemoveDestroyed(redRoles);
    }

    static void RemoveDestroyed<T>(Dictionary<Sosig, T> dictionary)
    {
        List<Sosig> stale = new List<Sosig>();
        foreach (Sosig s in dictionary.Keys)
        {
            if (s == null)
                stale.Add(s);
        }
        for (int i = 0; i < stale.Count; i++)
            dictionary.Remove(stale[i]);
    }
}
