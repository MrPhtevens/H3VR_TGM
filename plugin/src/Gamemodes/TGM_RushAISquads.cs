using System.Collections.Generic;
using FistVR;
using UnityEngine;

namespace TeamsGameMode;

/// <summary>
/// Rush Sosig orders, host only.
/// Each team has one operator who works the Mcom (Red arms / holds it, Blue disarms it).
/// Red moves in squads that leap-frog towards their target, Blue holds layered positions.
/// Everyone prefers spots with cover from the enemy's direction (see TGM_Tactics).
/// </summary>
public class TGM_RushAISquads : ITGM_RushAI
{
    const int redIFF = TGM_Gamemode.redIFF;
    const int blueIFF = TGM_Gamemode.blueIFF;

    //------------------------------------------------------------------------------
    // Tuning
    //------------------------------------------------------------------------------

    const float tickInterval = 1;               //How often orders are checked
    const float operatorCheckInterval = 0.25f;  //Operators are kept on task more often than everyone else
    const float onPointRadius = 2.5f;           //Close enough to the Mcom to arm / disarm it
    static float coverSearchRadius => TGM_Config.CoverSearchRadius.Value;          //How far from a chosen spot to look for cover

    //Attacker squads
    static int squadSize => TGM_Config.SquadSize.Value;                    //Max Sosigs per squad
    static float squadFormWindow => TGM_Config.SquadFormWindow.Value;            //Sosigs spawned within this many seconds join the same squad
    static float boundStep => TGM_Config.BoundStep.Value;                 //How far the moving half advances per bound
    static float boundTimeout => TGM_Config.BoundTimeout.Value;               //Swap halves after this long even if the movers haven't arrived
    static float boundArriveDistance => TGM_Config.BoundArriveDistance.Value;        //Movers count as arrived this close to their spot
    static float boundZone => TGM_Config.BoundZone.Value;                 //Squads always leap-frog this close to the Mcom
    static float contactMemory => TGM_Config.ContactMemory.Value;              //Squads keep leap-frogging this long after their last fight
    static float travelStep => TGM_Config.TravelStep.Value;                //Out of contact, squads run towards a waypoint this far ahead
    static float travelSpacing => TGM_Config.TravelSpacing.Value;              //Sideways gap between squad members while travelling
    static float operatorCommitRange => TGM_Config.OperatorCommitRange.Value;       //Red's operator goes for the Mcom once a squad (or it) is this close
    static float squadReplanMin => TGM_Config.SquadReplanMin.Value;            //Squads pick a new target every replanMin-replanMax seconds
    static float squadReplanMax => TGM_Config.SquadReplanMax.Value;

    //Defenders
    static float blueReorderMin => TGM_Config.DefenderReorderMin.Value;            //Defenders get new orders every reorderMin-reorderMax seconds
    static float blueReorderMax => TGM_Config.DefenderReorderMax.Value;
    static float blueForwardChance => TGM_Config.DefenderForwardChance.Value;       //Meet the attack on the approach
    static float blueLaneChance => TGM_Config.DefenderLaneChance.Value;          //The map's defend areas
    static float blueOverwatchChance => TGM_Config.DefenderOverwatchChance.Value;    //Ring at mid range around the Mcom (rest anchor near it)
    static float blueReactRange => TGM_Config.DefenderReactRange.Value;            //Defenders this close react when the Mcom is contested
    static float blueResponderChance => TGM_Config.DefenderResponderChance.Value;    //Close in and fight, rest hold an overwatch ring

    //------------------------------------------------------------------------------

    enum Phase { Idle = 0, Arming = 1, Armed = 2 }
    enum SquadRole { Assault, Flank, Overwatch }
    static readonly SquadRole[] roleCycle = { SquadRole.Assault, SquadRole.Flank, SquadRole.Assault, SquadRole.Overwatch };

    class Squad
    {
        public readonly List<Sosig> members = new List<Sosig>();
        public int id;              //For the debug labels
        public SquadRole role;
        public float formedAt;
        public bool hasTarget;
        public Vector3 target;      //Where the squad wants to end up
        public Vector3 front;       //Where the current moving half is heading
        public int movingHalf;      //Members with index % 2 == movingHalf are moving
        public float boundStarted;
        public bool arrived;
        public bool bounding;       //Leap-frogging (in contact) instead of travelling together
        public float lastContact = -100;
        public float nextReplan;
        public readonly Dictionary<Sosig, Vector3> spots = new Dictionary<Sosig, Vector3>();
    }

    readonly List<Squad> squads = new List<Squad>();
    int nextRoleIndex = 0;

    readonly Dictionary<Sosig, float> nextOrder = new Dictionary<Sosig, float>();
    readonly Dictionary<Sosig, float> defaultPushRange = new Dictionary<Sosig, float>();
    readonly Dictionary<Sosig, float> defaultHearing = new Dictionary<Sosig, float>();
    readonly Sosig[] operators = new Sosig[2];
    bool operatorCommitted = false;     //Red's operator has gone for the Mcom
    float nextOperatorCheck = 0;
    float nextTick = 0;
    int lastPhase = -1;
    bool lastArmed = false;

    /// <summary>
    /// New objective: everyone gets fresh orders, operators and squad targets are picked again
    /// </summary>
    public void Reset()
    {
        nextOrder.Clear();
        for (int i = 0; i < operators.Length; i++)
            SetOperator(i, null);
        for (int i = 0; i < squads.Count; i++)
            squads[i].hasTarget = false;
        nextTick = 0;
        lastPhase = -1;
        lastArmed = false;
    }

    /// <summary>
    /// New game, also disband all squads
    /// </summary>
    public void ResetAll()
    {
        Reset();
        squads.Clear();
        nextRoleIndex = 0;
    }

    public void Update(Rush_CapturePoint objective, TGM_Area defenderArea, bool isReversed)
    {
        Vector3 mcom = objective.transform.position;

        //Pull operators back straight away if anything distracted them
        if (Time.time >= nextOperatorCheck)
        {
            nextOperatorCheck = Time.time + operatorCheckInterval;
            for (int i = 0; i < operators.Length; i++)
            {
                if (operators[i] == null || operators[i].BodyState == Sosig.SosigBodyState.Dead)
                    continue;
                if (i == redIFF)
                    UpdateAttackOperator(operators[i], mcom);
                else
                    HoldMcom(operators[i], mcom);
            }
        }

        if (Time.time < nextTick)
            return;
        nextTick = Time.time + tickInterval;

        Phase phase = GetPhase(objective);
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

        //ATTACKERS: squads (only re-plan when the Mcom becomes armed or stops being armed, arming flickers)
        bool armed = phase == Phase.Armed;
        bool armedChanged = armed != lastArmed;
        lastArmed = armed;
        UpdateSquads(mcom, phase, armedChanged, approach, defenderArea, isReversed);

        //DEFENDERS: individual layered roles
        List<Sosig> defenders = TGM_Manager.instance.team[blueIFF].sosigs;
        for (int i = 0; i < defenders.Count; i++)
        {
            Sosig s = defenders[i];
            if (s == operators[blueIFF])
                continue;   //Kept on the Mcom by the operator check above

            float next;
            if (phaseChanged || !nextOrder.TryGetValue(s, out next) || Time.time >= next)
                OrderDefender(s, mcom, phase, approach, defenderArea, isReversed);
        }
    }

    /// <summary>
    /// Freshly spawned Sosig: attackers join a squad, defenders get a role straight away
    /// </summary>
    public void OrderNew(Sosig s, Rush_CapturePoint objective, TGM_Area defenderArea, bool isReversed)
    {
        if (s.GetIFF() == redIFF)
        {
            //Hold at the spawn while the rest of the wave forms up
            CommandSosig(s, s.transform.position, false, false, "Red\nforming squad");
            JoinSquad(s);
            return;
        }

        Vector3 mcom = objective.transform.position;
        OrderDefender(s, mcom, GetPhase(objective), GetApproachDirection(mcom), defenderArea, isReversed);
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
            float value;
            if (defaultPushRange.TryGetValue(previous, out value))
                previous.m_assaultPointOverridesSkirmishPointWhenFurtherThan = value;
            if (defaultHearing.TryGetValue(previous, out value))
                previous.MaxHearingRange = value;
            nextOrder.Remove(previous);
        }

        operators[iff] = s;
        if (iff == redIFF)
            operatorCommitted = false;
        if (s == null)
            return;

        //The operator leaves its squad, it only works the Mcom now
        for (int i = 0; i < squads.Count; i++)
            squads[i].members.Remove(s);

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
    /// Red's operator moves with the nearest squad and only goes for the Mcom once friends are close,
    /// instead of running in alone
    /// </summary>
    void UpdateAttackOperator(Sosig s, Vector3 mcom)
    {
        Squad escort = null;
        float escortDistance = float.MaxValue;
        bool friendsClose = false;
        for (int i = 0; i < squads.Count; i++)
        {
            if (squads[i].members.Count == 0)
                continue;
            Vector3 centroid = Centroid(squads[i]);
            if (Vector3.Distance(centroid, mcom) <= operatorCommitRange)
                friendsClose = true;
            float distance = Vector3.Distance(centroid, s.transform.position);
            if (distance < escortDistance)
            {
                escort = squads[i];
                escortDistance = distance;
            }
        }

        if (operatorCommitted || escort == null || friendsClose || Vector3.Distance(s.transform.position, mcom) <= operatorCommitRange)
        {
            operatorCommitted = true;   //Once it goes for the Mcom it doesn't turn back
            HoldMcom(s, mcom);
            return;
        }

        //Stay with the escort squad
        Vector3 follow = Centroid(escort);
        if (TGM_AIDebug.Enabled)
            TGM_AIDebug.Show(s, "Red OPERATOR\nwaiting with squad " + escort.id, TGM_AIDebug.OperatorColor, follow);
        bool following = s.CurrentOrder == Sosig.SosigOrder.Assault
            && Vector3.Distance(s.m_assaultPoint, follow) < 8;
        if (!following)
        {
            s.SetAssaultSpeed(Sosig.SosigMoveSpeed.Running);
            s.CommandAssaultPoint(TGM_Tactics.SampleNavMesh(follow));
            s.FallbackOrder = Sosig.SosigOrder.Assault;
        }
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
                s.CommandGuardPoint(TGM_Tactics.SampleNavMesh(TGM_Tactics.RandomRing(mcom, 0, 1)), true);
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
                s.CommandAssaultPoint(TGM_Tactics.SampleNavMesh(TGM_Tactics.RandomRing(mcom, 0, 1)));
                s.FallbackOrder = Sosig.SosigOrder.Assault;
            }
        }
    }

    //------------------------------------------------------------------------------
    // Attacker squads
    //------------------------------------------------------------------------------

    void JoinSquad(Sosig s)
    {
        //Sosigs from the same respawn wave form squads together
        Squad forming = squads.Count > 0 ? squads[squads.Count - 1] : null;
        if (forming == null
            || forming.members.Count >= squadSize
            || Time.time - forming.formedAt > squadFormWindow)
        {
            forming = new Squad
            {
                id = nextRoleIndex + 1,
                role = roleCycle[nextRoleIndex++ % roleCycle.Length],
                formedAt = Time.time,
            };
            squads.Add(forming);
        }
        forming.members.Add(s);
        forming.hasTarget = false;  //Plan with the full squad
    }

    void UpdateSquads(Vector3 mcom, Phase phase, bool phaseChanged, Vector3 approach, TGM_Area defenderArea, bool isReversed)
    {
        //Anyone not in a squad (e.g. an operator that was released) joins one
        List<Sosig> attackers = TGM_Manager.instance.team[redIFF].sosigs;
        for (int i = 0; i < attackers.Count; i++)
        {
            if (attackers[i] != operators[redIFF] && !InSquad(attackers[i]))
                JoinSquad(attackers[i]);
        }

        for (int i = squads.Count - 1; i >= 0; i--)
        {
            Squad squad = squads[i];
            squad.members.RemoveAll(s => s == null || s.BodyState == Sosig.SosigBodyState.Dead || s == operators[redIFF]);
            if (squad.members.Count == 0)
            {
                squads.RemoveAt(i);
                continue;
            }

            //Still forming, wait for the rest of the wave
            if (Time.time - squad.formedAt < squadFormWindow)
                continue;

            if (!squad.hasTarget || phaseChanged || Time.time >= squad.nextReplan)
                PlanSquad(squad, mcom, phase, approach, defenderArea, isReversed);

            MoveSquad(squad, mcom, phase, approach);
        }
    }

    bool InSquad(Sosig s)
    {
        for (int i = 0; i < squads.Count; i++)
        {
            if (squads[i].members.Contains(s))
                return true;
        }
        return false;
    }

    void PlanSquad(Squad squad, Vector3 mcom, Phase phase, Vector3 approach, TGM_Area defenderArea, bool isReversed)
    {
        Vector3 target;
        if (phase == Phase.Armed)
        {
            //Defend the armed Mcom, overwatch stays further back on the approach
            if (squad.role == SquadRole.Overwatch)
                target = TGM_Tactics.AlongDirection(mcom, approach, 15, 25, 10);
            else
                target = TGM_Tactics.RandomRing(mcom, 6, 14);
        }
        else
        {
            switch (squad.role)
            {
                case SquadRole.Flank:
                    if (defenderArea != null)
                        target = defenderArea.GetRandomAttackArea(isReversed)[0];
                    else
                    {
                        Vector3 side = Vector3.Cross(Vector3.up, approach) * (Random.value < 0.5f ? -1 : 1);
                        target = mcom + side * Random.Range(15f, 25f) + approach * 8;
                    }
                    break;
                case SquadRole.Overwatch:
                    target = TGM_Tactics.AlongDirection(mcom, approach, 20, 35, 10);
                    break;
                default:
                    //Escort the operator in from the approach side
                    target = TGM_Tactics.RandomArc(mcom, approach, 120, 6, 12);
                    break;
            }
        }

        squad.target = TGM_Tactics.SampleNavMesh(target);
        squad.front = Centroid(squad);
        squad.hasTarget = true;
        squad.arrived = false;
        squad.nextReplan = Time.time + Random.Range(squadReplanMin, squadReplanMax);
        squad.boundStarted = -boundTimeout;   //Start the first bound on this tick
    }

    /// <summary>
    /// Out of contact the squad runs together, in contact (or close to the Mcom) it leap-frogs
    /// </summary>
    void MoveSquad(Squad squad, Vector3 mcom, Phase phase, Vector3 approach)
    {
        if (squad.arrived)
            return;

        Vector3 centroid = Centroid(squad);

        //Threat is towards the Mcom while attacking, from the defenders' side once armed
        Vector3 threat = phase == Phase.Armed ? -approach : TGM_Tactics.Flatten(mcom - centroid);
        bool contested = phase != Phase.Idle;

        if (InContact(squad))
            squad.lastContact = Time.time;
        bool bound = Time.time - squad.lastContact < contactMemory
            || Vector3.Distance(centroid, mcom) < boundZone;

        if (!bound)
        {
            squad.bounding = false;
            Travel(squad, centroid, threat, contested);
            return;
        }

        //Just made contact, start leap-frogging from where the squad is
        if (!squad.bounding)
        {
            squad.bounding = true;
            squad.front = centroid;
            squad.boundStarted = -boundTimeout;
            squad.spots.Clear();
        }
        Bound(squad, threat, contested);
    }

    /// <summary>
    /// Whole squad runs towards a waypoint ahead, spread out side by side, no stopping
    /// </summary>
    void Travel(Squad squad, Vector3 centroid, Vector3 threat, bool contested)
    {
        if (Vector3.Distance(centroid, squad.target) < boundArriveDistance)
        {
            Settle(squad, threat, contested);
            return;
        }

        Vector3 direction = TGM_Tactics.Flatten(squad.target - centroid);
        Vector3 side = Vector3.Cross(Vector3.up, direction);
        Vector3 waypoint = Vector3.MoveTowards(centroid, squad.target, travelStep);

        for (int i = 0; i < squad.members.Count; i++)
        {
            Sosig s = squad.members[i];
            float lateral = (i - (squad.members.Count - 1) / 2f) * travelSpacing;
            Vector3 point = waypoint + side * lateral - direction * (i % 2) * 2;

            //Already running to roughly the right place, don't interrupt
            bool onTheWay = s.CurrentOrder == Sosig.SosigOrder.Assault
                && Vector3.Distance(s.m_assaultPoint, point) < 10
                && Vector3.Distance(s.transform.position, s.m_assaultPoint) > 8;
            if (!onTheWay)
                CommandSosig(s, point, true, false, SquadLabel(squad, "travelling"));
            else
                ShowDebug(s, SquadLabel(squad, "travelling") + "\nassault", s.m_assaultPoint);
        }
        squad.spots.Clear();
    }

    /// <summary>
    /// Squad reached its target, everyone takes cover around it
    /// </summary>
    void Settle(Squad squad, Vector3 threat, bool contested)
    {
        squad.arrived = true;
        for (int i = 0; i < squad.members.Count; i++)
        {
            Vector3 spot = TGM_Tactics.FindCover(squad.target, threat, coverSearchRadius + 2);
            CommandSosig(squad.members[i], spot, false, contested, SquadLabel(squad, "arrived"));
            squad.spots[squad.members[i]] = spot;
        }
    }

    /// <summary>
    /// Someone in the squad is fighting
    /// </summary>
    static bool InContact(Squad squad)
    {
        for (int i = 0; i < squad.members.Count; i++)
        {
            Sosig.SosigOrder order = squad.members[i].CurrentOrder;
            if (order == Sosig.SosigOrder.Skirmish || order == Sosig.SosigOrder.TakeCover)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Leap-frog: one half moves up while the other holds and covers, then they swap
    /// </summary>
    void Bound(Squad squad, Vector3 threat, bool contested)
    {
        //Wait for the movers to get to their spots (or give up waiting)
        if (Time.time - squad.boundStarted < boundTimeout && !MoversArrived(squad))
            return;

        //Whole squad reached the target, settle into cover around it
        if (Vector3.Distance(squad.front, squad.target) < 1)
        {
            Settle(squad, threat, contested);
            return;
        }

        //Next bound: swap halves and move the new movers up
        squad.movingHalf ^= 1;
        squad.front = Vector3.MoveTowards(squad.front, squad.target, boundStep);
        squad.boundStarted = Time.time;

        for (int i = 0; i < squad.members.Count; i++)
        {
            Sosig s = squad.members[i];
            bool moving = (i % 2 == squad.movingHalf) || squad.members.Count == 1;
            if (moving)
            {
                Vector3 spot = TGM_Tactics.FindCover(squad.front, threat, coverSearchRadius);
                CommandSosig(s, spot, true, false, SquadLabel(squad, "leap-frog: moving"));
                squad.spots[s] = spot;
            }
            else
            {
                //Hold here in the nearest cover and cover the movers
                Vector3 spot = TGM_Tactics.FindCover(s.transform.position, threat, 2);
                CommandSosig(s, spot, false, contested, SquadLabel(squad, "leap-frog: covering"));
                squad.spots.Remove(s);
            }
        }
    }

    bool MoversArrived(Squad squad)
    {
        for (int i = 0; i < squad.members.Count; i++)
        {
            Vector3 spot;
            if (squad.spots.TryGetValue(squad.members[i], out spot)
                && Vector3.Distance(squad.members[i].transform.position, spot) > boundArriveDistance)
                return false;
        }
        return true;
    }

    static Vector3 Centroid(Squad squad)
    {
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < squad.members.Count; i++)
            sum += squad.members[i].transform.position;
        return sum / Mathf.Max(squad.members.Count, 1);
    }

    //------------------------------------------------------------------------------
    // Defenders
    //------------------------------------------------------------------------------

    void OrderDefender(Sosig s, Vector3 mcom, Phase phase, Vector3 approach, TGM_Area defenderArea, bool isReversed)
    {
        if (s == null || s.BodyState == Sosig.SosigBodyState.Dead || s == operators[blueIFF])
            return;

        //Stagger re-orders so the team doesn't move in lockstep
        nextOrder[s] = Time.time + Random.Range(blueReorderMin, blueReorderMax);
        float roll = Random.value;
        bool contested = phase != Phase.Idle;
        bool inRange = Vector3.Distance(s.transform.position, mcom) <= blueReactRange;

        Vector3 point;
        string role;
        bool hardGuard = false;
        if (contested && inRange)
        {
            hardGuard = true;
            if (roll < blueResponderChance)
            {
                point = TGM_Tactics.RandomRing(mcom, 3, 7);     //Close in and fight
                role = "Responder";
            }
            else
            {
                point = TGM_Tactics.RandomRing(mcom, 10, 22);   //Overwatch, cover the responders
                role = "Cover ring";
            }
        }
        else if (roll < blueForwardChance)
        {
            point = TGM_Tactics.AlongDirection(mcom, approach, 25, 50, 15);
            role = "Forward";
        }
        else if (roll < blueForwardChance + blueLaneChance && defenderArea != null)
        {
            point = defenderArea.GetRandomDefendArea(isReversed)[0];
            role = "Lane";
        }
        else if (roll < blueForwardChance + blueLaneChance + blueOverwatchChance)
        {
            point = TGM_Tactics.RandomRing(mcom, 10, 25);
            role = "Overwatch";
        }
        else
        {
            point = TGM_Tactics.RandomRing(mcom, 5, 9);
            role = "Anchor";
        }

        //Attackers come from the approach direction
        CommandSosig(s, TGM_Tactics.FindCover(point, approach, coverSearchRadius), false, hardGuard, "Blue " + role);
    }

    //------------------------------------------------------------------------------
    // Shared
    //------------------------------------------------------------------------------

    static void ShowDebug(Sosig s, string text, Vector3 target)
    {
        if (!TGM_AIDebug.Enabled)
            return;
        Color color = s.GetIFF() == redIFF ? TGM_AIDebug.AttackColor : TGM_AIDebug.DefendColor;
        TGM_AIDebug.Show(s, text, color, target);
    }

    static string SquadLabel(Squad squad, string mode)
    {
        return "Red squad " + squad.id + " " + squad.role + " (" + squad.members.Count + ")\n" + mode;
    }

    /// <summary>
    /// Assault pushes hard to the point, guard holds the point but still engages enemies.
    /// Hard guard keeps them at the point instead of wandering off to investigate noises.
    /// </summary>
    static void CommandSosig(Sosig s, Vector3 point, bool assault, bool hardGuard, string debug = null)
    {
        if (s == null || s.BodyState == Sosig.SosigBodyState.Dead)
            return;

        point = TGM_Tactics.SampleNavMesh(point);
        bool fighting = s.CurrentOrder == Sosig.SosigOrder.Skirmish;

        if (debug != null)
        {
            string action = fighting ? "fighting, then " : "";
            action += assault ? "assault" : (hardGuard ? "hold (hard)" : "hold");
            ShowDebug(s, debug + "\n" + action, point);
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

    /// <summary>
    /// Flat direction from the Mcom towards the attackers' spawn
    /// </summary>
    static Vector3 GetApproachDirection(Vector3 mcom)
    {
        TGM_Area redSpawn = TGM_Manager.instance.team[redIFF].currentSpawnArea;
        if (redSpawn != null)
        {
            Vector3 dir = TGM_Tactics.Flatten(redSpawn.transform.position - mcom);
            if (dir != Vector3.zero)
                return dir;
        }
        Vector2 random = Random.insideUnitCircle.normalized;
        return new Vector3(random.x, 0, random.y);
    }

    /// <summary>
    /// Drop Sosigs that no longer exist (destroyed Unity objects compare equal to null)
    /// </summary>
    void Forget()
    {
        RemoveDestroyed(nextOrder);
        RemoveDestroyed(defaultPushRange);
        RemoveDestroyed(defaultHearing);
        for (int i = 0; i < squads.Count; i++)
        {
            List<Sosig> stale = new List<Sosig>();
            foreach (Sosig s in squads[i].spots.Keys)
            {
                if (s == null)
                    stale.Add(s);
            }
            for (int x = 0; x < stale.Count; x++)
                squads[i].spots.Remove(stale[x]);
        }
    }

    static void RemoveDestroyed(Dictionary<Sosig, float> dictionary)
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
