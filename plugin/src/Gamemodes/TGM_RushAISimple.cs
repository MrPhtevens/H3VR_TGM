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

    //Attacker waves
    static float rallyDistance => TGM_Config.RallyDistance.Value;
    static int pushGroupSize => TGM_Config.PushGroupSize.Value;
    static float rallyTimeout => TGM_Config.RallyTimeout.Value;
    static float flankWideDistance => TGM_Config.FlankWideDistance.Value;
    static float flankHoldTime => TGM_Config.FlankHoldTime.Value;
    const float rallyRadius = 15;           //Counts as gathered this close to the rally point
    const float neverPullBackRange = 30;    //Attackers already this close to the Mcom join the attack, never sent back to rally

    enum RedRole { Escort, Assault, Flank }

    readonly Dictionary<Sosig, RedRole> redRoles = new Dictionary<Sosig, RedRole>();  //Attackers keep their job for life
    readonly HashSet<Sosig> committed = new HashSet<Sosig>();                          //Attackers that have left the rally and are attacking
    readonly Dictionary<Sosig, float> flankWideSince = new Dictionary<Sosig, float>(); //When a flanker went wide
    readonly Dictionary<Sosig, float> gatherSince = new Dictionary<Sosig, float>();     //When an attacker was first sent to the rally
    Vector3 rallyPoint;
    bool hasRally = false;      //Rally point worked out for the current Mcom
    Vector3 rallyFor;
    Vector3 cachedRally;
    float firstGathered = -1;
    int gatheredCount = 0;
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

        //New objective, attackers regroup at the new rally point
        committed.Clear();
        flankWideSince.Clear();
        gatherSince.Clear();
        hasRally = false;
        firstGathered = -1;
        gatheredCount = 0;
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

        Vector3 approach = GetApproachDirection(mcom);
        UpdateWaves(mcom, approach, phase);

        //Red's operator comes from the attackers that are pushing, Blue only needs one once there is something to disarm
        UpdateOperator(redIFF, committed.Count > 0, mcom);
        UpdateOperator(blueIFF, phase == Phase.Armed, mcom);
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
        Vector3 approach = GetApproachDirection(mcom);
        rallyPoint = GetRallyPoint(mcom, approach);
        Order(s, s.GetIFF(), mcom, SmoothedPhase(objective), approach, defenderArea, isReversed);
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
        //(for Red only from the attackers that are pushing, so it moves in with them)
        Sosig closest = null;
        float closestDistance = float.MaxValue;
        List<Sosig> sosigs = TGM_Manager.instance.team[iff].sosigs;
        for (int i = 0; i < sosigs.Count; i++)
        {
            if (iff == redIFF && !committed.Contains(sosigs[i]))
                continue;
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
            else if (!committed.Contains(s))
            {
                if (!gatherSince.ContainsKey(s))
                    gatherSince[s] = Time.time;

                //Wait for the rest of the wave at the rally point
                if (Vector3.Distance(s.transform.position, rallyPoint) > 8)
                {
                    CommandSosig(s, RandomRing(rallyPoint, 0, 6), true, false, "Red Gathering");
                    nextOrder[s] = Time.time + 6;
                }
                else
                {
                    CommandSosig(s, s.transform.position, false, false,
                        "Red Waiting at rally (" + gatheredCount + "/" + PushThreshold() + ")");
                    nextOrder[s] = Time.time + 3;
                }
            }
            else
            {
                RedRole role = GetRedRole(s);
                if (role == RedRole.Flank)
                {
                    OrderFlanker(s, mcom, approach);
                    return;
                }

                //Every other attacker job ends at the Mcom, and attackers never get sent backwards
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

    //------------------------------------------------------------------------------
    // Attacker waves
    //------------------------------------------------------------------------------

    /// <summary>
    /// Attackers gather at a rally point and push together once enough have arrived (or after a wait),
    /// instead of running at the Mcom one by one as they respawn
    /// </summary>
    void UpdateWaves(Vector3 mcom, Vector3 approach, Phase phase)
    {
        rallyPoint = GetRallyPoint(mcom, approach);

        List<Sosig> attackers = TGM_Manager.instance.team[redIFF].sosigs;
        int gathered = 0;
        for (int i = 0; i < attackers.Count; i++)
        {
            Sosig s = attackers[i];
            if (committed.Contains(s))
                continue;

            //Armed: everyone defends it. Already close: never pulled back to the rally.
            //Been trying to gather for too long (rally unreachable / nobody else coming): go anyway
            float since;
            bool waitedTooLong = gatherSince.TryGetValue(s, out since) && Time.time - since >= rallyTimeout * 2;
            if (phase == Phase.Armed || waitedTooLong || Vector3.Distance(s.transform.position, mcom) <= neverPullBackRange)
            {
                Commit(s);
                continue;
            }
            if (Vector3.Distance(s.transform.position, rallyPoint) <= rallyRadius)
                gathered++;
        }
        gatheredCount = gathered;

        if (gathered == 0)
        {
            firstGathered = -1;
            return;
        }
        if (firstGathered < 0)
            firstGathered = Time.time;

        //Enough of the wave is here, or they've waited long enough: everyone at the rally goes
        if (gathered >= PushThreshold() || Time.time - firstGathered >= rallyTimeout)
        {
            for (int i = 0; i < attackers.Count; i++)
            {
                if (!committed.Contains(attackers[i])
                    && Vector3.Distance(attackers[i].transform.position, rallyPoint) <= rallyRadius)
                    Commit(attackers[i]);
            }
            firstGathered = -1;
            gatheredCount = 0;
        }
    }

    /// <summary>
    /// How many need to gather before pushing, fewer if not that many attackers are waiting
    /// </summary>
    int PushThreshold()
    {
        int waiting = 0;
        List<Sosig> attackers = TGM_Manager.instance.team[redIFF].sosigs;
        for (int i = 0; i < attackers.Count; i++)
        {
            if (!committed.Contains(attackers[i]))
                waiting++;
        }
        return Mathf.Clamp(Mathf.CeilToInt(waiting * 0.6f), 1, pushGroupSize);
    }

    void Commit(Sosig s)
    {
        committed.Add(s);
        gatherSince.Remove(s);
        nextOrder.Remove(s);    //New orders this tick
    }

    /// <summary>
    /// RallyDistance before the Mcom along the actual walking route from the attackers' spawn
    /// (never more than 60% of the way back). Worked out once per objective.
    /// </summary>
    Vector3 GetRallyPoint(Vector3 mcom, Vector3 approach)
    {
        if (hasRally && Vector3.Distance(rallyFor, mcom) < 0.5f)
            return cachedRally;

        Vector3 spawn = SampleNavMesh(RedSpawnPosition(mcom));
        Vector3 target = SampleNavMesh(mcom);
        cachedRally = SampleNavMesh(mcom + approach * Mathf.Min(rallyDistance, Vector3.Distance(spawn, mcom) * 0.6f));

        NavMeshPath path = new NavMeshPath();
        if (NavMesh.CalculatePath(spawn, target, NavMesh.AllAreas, path)
            && path.status != NavMeshPathStatus.PathInvalid
            && path.corners.Length >= 2)
        {
            Vector3[] corners = path.corners;
            float length = 0;
            for (int i = 1; i < corners.Length; i++)
                length += Vector3.Distance(corners[i - 1], corners[i]);

            //Walk back from the Mcom end of the route
            float remaining = Mathf.Min(rallyDistance, length * 0.6f);
            cachedRally = corners[0];
            for (int i = corners.Length - 1; i >= 1; i--)
            {
                float segment = Vector3.Distance(corners[i], corners[i - 1]);
                if (remaining <= segment)
                {
                    cachedRally = Vector3.Lerp(corners[i], corners[i - 1], segment > 0 ? remaining / segment : 0);
                    break;
                }
                remaining -= segment;
            }
        }
        else
            TGMPlugin.Logger.LogWarning("Rush AI: no walking route from the attackers' spawn to the Mcom, using a straight line for the rally point");

        hasRally = true;
        rallyFor = mcom;
        return cachedRally;
    }

    /// <summary>
    /// Where the attackers actually spawn: the average of the current spawn area's Sosig (or player) spawn points
    /// </summary>
    static Vector3 RedSpawnPosition(Vector3 fallback)
    {
        TGM_Area area = TGM_Manager.instance.team[redIFF].currentSpawnArea;
        if (area == null)
            return fallback;

        if (area.spawnPoints != null && area.spawnPoints.Length > redIFF && area.spawnPoints[redIFF] != null)
        {
            Transform[] points = area.spawnPoints[redIFF].sosigSpawnPoints;
            if (points == null || points.Length == 0)
                points = area.spawnPoints[redIFF].playerSpawnPoints;

            if (points != null)
            {
                Vector3 sum = Vector3.zero;
                int count = 0;
                for (int i = 0; i < points.Length; i++)
                {
                    if (points[i] == null)
                        continue;
                    sum += points[i].position;
                    count++;
                }
                if (count > 0)
                    return sum / count;
            }
        }
        return area.transform.position;
    }

    /// <summary>
    /// Flankers go wide to one side first to draw defenders away, then close in on the Mcom from that side
    /// </summary>
    void OrderFlanker(Sosig s, Vector3 mcom, Vector3 approach)
    {
        Vector3 side = Vector3.Cross(Vector3.up, approach) * (s.GetInstanceID() % 2 == 0 ? 1 : -1);

        float since;
        if (!flankWideSince.TryGetValue(s, out since))
        {
            //Already too close to bother going wide
            since = Vector3.Distance(s.transform.position, mcom) > flankWideDistance * 0.7f ? Time.time : -1000;
            flankWideSince[s] = since;
        }

        if (Time.time - since < flankHoldTime)
        {
            Vector3 wide = mcom
                + side * flankWideDistance * Random.Range(0.8f, 1.2f)
                + approach * Random.Range(-5f, 10f);
            bool arrived = Vector3.Distance(s.transform.position, wide) < 10;
            CommandSosig(s, wide, !arrived, false, arrived ? "Red Flank\ndrawing fire" : "Red Flank\ngoing wide");

            //Close in on time even if the normal re-order would come later
            float next;
            if (nextOrder.TryGetValue(s, out next))
                nextOrder[s] = Mathf.Min(next, since + flankHoldTime);
            return;
        }

        CommandSosig(s, RandomArc(mcom, side, 80, 6, 14), true, false, "Red Flank\nclosing in");
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
        //From the Mcom towards where the attackers actually spawn
        Vector3 dir = RedSpawnPosition(mcom) - mcom;
        dir.y = 0;
        if (dir.sqrMagnitude > 1)
            return dir.normalized;

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
        RemoveDestroyed(flankWideSince);
        RemoveDestroyed(gatherSince);
        committed.RemoveWhere(s => s == null);
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
