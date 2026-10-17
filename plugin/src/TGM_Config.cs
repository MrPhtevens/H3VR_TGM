using BepInEx.Configuration;

namespace TeamsGameMode;

/// <summary>
/// Tunable values, saved in BepInEx/config/Packer.Teams.cfg.
/// Values are read when used, so changes from an in-game config panel apply straight away.
/// Only the host's values matter for the Mcom timing and the AI (the host runs both).
/// </summary>
public static class TGM_Config
{
    //Debug
    public static ConfigEntry<bool> ShowAIDebug;

    //Rush - Mcom
    public static ConfigEntry<float> ArmTime;
    public static ConfigEntry<float> DisarmTime;
    public static ConfigEntry<float> FuseTime;

    //Rush AI - General
    public static ConfigEntry<TGM_RushTactics> Tactics;
    public static ConfigEntry<float> CoverSearchRadius;

    //Rush AI - Simple attackers
    public static ConfigEntry<float> AttackerEscortChance;
    public static ConfigEntry<float> AttackerFlankChance;
    public static ConfigEntry<float> AttackerGuardArmedChance;
    public static ConfigEntry<float> AttackerReorderMin;
    public static ConfigEntry<float> AttackerReorderMax;
    public static ConfigEntry<float> RallyDistance;
    public static ConfigEntry<int> PushGroupSize;
    public static ConfigEntry<float> RallyTimeout;
    public static ConfigEntry<float> FlankWideDistance;
    public static ConfigEntry<float> FlankHoldTime;

    //Rush AI - Squad attackers
    public static ConfigEntry<int> SquadSize;
    public static ConfigEntry<float> SquadFormWindow;
    public static ConfigEntry<float> TravelStep;
    public static ConfigEntry<float> TravelSpacing;
    public static ConfigEntry<float> BoundZone;
    public static ConfigEntry<float> ContactMemory;
    public static ConfigEntry<float> BoundStep;
    public static ConfigEntry<float> BoundTimeout;
    public static ConfigEntry<float> BoundArriveDistance;
    public static ConfigEntry<float> SquadReplanMin;
    public static ConfigEntry<float> SquadReplanMax;
    public static ConfigEntry<float> OperatorCommitRange;

    //Rush AI - Defenders
    public static ConfigEntry<float> DefenderReorderMin;
    public static ConfigEntry<float> DefenderReorderMax;
    public static ConfigEntry<float> DefenderForwardChance;
    public static ConfigEntry<float> DefenderLaneChance;
    public static ConfigEntry<float> DefenderOverwatchChance;
    public static ConfigEntry<float> DefenderReactRange;
    public static ConfigEntry<float> DefenderResponderChance;


    public static void Bind(ConfigFile config)
    {
        const string debug = "Debug";
        ShowAIDebug = config.Bind(debug, "ShowAIDebug", false,
            "Show a label over every Sosig (team, squad, role, what it is doing) and a line to where it is heading. Only visible on the host.");

        const string mcom = "Rush - Mcom";
        ArmTime = config.Bind(mcom, "ArmTime", 5f,
            new ConfigDescription("Seconds attackers must stand on the Mcom to arm it.", new AcceptableValueRange<float>(1, 30)));
        DisarmTime = config.Bind(mcom, "DisarmTime", 5f,
            new ConfigDescription("Seconds defenders must stand on an armed Mcom to disarm it.", new AcceptableValueRange<float>(1, 30)));
        FuseTime = config.Bind(mcom, "FuseTime", 25f,
            new ConfigDescription("Seconds from armed to explosion.", new AcceptableValueRange<float>(5, 90)));

        const string general = "Rush AI - General";
        Tactics = config.Bind(general, "Tactics", TGM_RushTactics.Simple,
            "Simple: attackers get individual spread out roles (escort, flank, push) around one operator. " +
            "Squads: attackers move in squads that travel together and leap-frog under fire. Applies from the next game.");
        CoverSearchRadius = config.Bind(general, "CoverSearchRadius", 4f,
            new ConfigDescription("Squads only: how far in metres from a chosen spot Sosigs look for cover.", new AcceptableValueRange<float>(0, 15)));

        const string simple = "Rush AI - Simple attackers";
        AttackerEscortChance = config.Bind(simple, "EscortChance", 0.5f,
            new ConfigDescription("Share of attackers escorting the operator, 5-12m from the Mcom on the attackers' side. Attackers keep their job for life.", new AcceptableValueRange<float>(0, 1)));
        AttackerFlankChance = config.Bind(simple, "FlankChance", 0.25f,
            new ConfigDescription("Share of attackers coming at the Mcom from one side, 6-14m out. The rest assault the Mcom directly (2-8m).", new AcceptableValueRange<float>(0, 1)));
        AttackerGuardArmedChance = config.Bind(simple, "GuardArmedChance", 0.75f,
            new ConfigDescription("Once the Mcom is armed, share of attackers guarding 5-15m around it. The rest hunt towards the defenders' side.", new AcceptableValueRange<float>(0, 1)));
        AttackerReorderMin = config.Bind(simple, "ReorderMin", 15f,
            new ConfigDescription("Attackers get new roles every ReorderMin-ReorderMax seconds.", new AcceptableValueRange<float>(5, 300)));
        AttackerReorderMax = config.Bind(simple, "ReorderMax", 30f,
            new ConfigDescription("See ReorderMin.", new AcceptableValueRange<float>(5, 300)));
        RallyDistance = config.Bind(simple, "RallyDistance", 45f,
            new ConfigDescription("Attackers gather this many metres from the Mcom (towards their spawn) before pushing together. Capped at 60% of the spawn-Mcom distance.", new AcceptableValueRange<float>(10, 150)));
        PushGroupSize = config.Bind(simple, "PushGroupSize", 4,
            new ConfigDescription("Attackers push once this many have gathered (fewer if not that many are alive).", new AcceptableValueRange<int>(1, 16)));
        RallyTimeout = config.Bind(simple, "RallyTimeout", 20f,
            new ConfigDescription("Gathered attackers push anyway after waiting this many seconds.", new AcceptableValueRange<float>(0, 120)));
        FlankWideDistance = config.Bind(simple, "FlankWideDistance", 30f,
            new ConfigDescription("Flankers first go this many metres off to one side of the Mcom to draw defenders away.", new AcceptableValueRange<float>(10, 100)));
        FlankHoldTime = config.Bind(simple, "FlankHoldTime", 25f,
            new ConfigDescription("Seconds flankers stay out wide before closing in on the Mcom from that side.", new AcceptableValueRange<float>(0, 120)));

        const string attack = "Rush AI - Squad attackers";
        SquadSize = config.Bind(attack, "SquadSize", 4,
            new ConfigDescription("Max Sosigs per attacker squad.", new AcceptableValueRange<int>(1, 8)));
        SquadFormWindow = config.Bind(attack, "SquadFormWindow", 2f,
            new ConfigDescription("Attackers spawned within this many seconds of each other join the same squad.", new AcceptableValueRange<float>(0.5f, 10)));
        TravelStep = config.Bind(attack, "TravelStep", 30f,
            new ConfigDescription("Out of contact, squads run together towards a waypoint this many metres ahead.", new AcceptableValueRange<float>(5, 100)));
        TravelSpacing = config.Bind(attack, "TravelSpacing", 3f,
            new ConfigDescription("Sideways gap in metres between squad members while travelling.", new AcceptableValueRange<float>(0.5f, 10)));
        BoundZone = config.Bind(attack, "BoundZone", 45f,
            new ConfigDescription("Squads always leap-frog within this many metres of the Mcom.", new AcceptableValueRange<float>(0, 150)));
        ContactMemory = config.Bind(attack, "ContactMemory", 8f,
            new ConfigDescription("Squads keep leap-frogging this many seconds after someone in the squad was last fighting.", new AcceptableValueRange<float>(0, 60)));
        BoundStep = config.Bind(attack, "BoundStep", 20f,
            new ConfigDescription("How far in metres the moving half advances per leap-frog bound.", new AcceptableValueRange<float>(5, 60)));
        BoundTimeout = config.Bind(attack, "BoundTimeout", 6f,
            new ConfigDescription("Halves swap after this many seconds even if the movers haven't arrived.", new AcceptableValueRange<float>(1, 30)));
        BoundArriveDistance = config.Bind(attack, "BoundArriveDistance", 5f,
            new ConfigDescription("Movers count as arrived within this many metres of their spot.", new AcceptableValueRange<float>(1, 20)));
        SquadReplanMin = config.Bind(attack, "SquadReplanMin", 25f,
            new ConfigDescription("Squads pick a new target every SquadReplanMin-SquadReplanMax seconds.", new AcceptableValueRange<float>(5, 300)));
        SquadReplanMax = config.Bind(attack, "SquadReplanMax", 40f,
            new ConfigDescription("See SquadReplanMin.", new AcceptableValueRange<float>(5, 300)));
        OperatorCommitRange = config.Bind(attack, "OperatorCommitRange", 30f,
            new ConfigDescription("The attacking operator stays with a squad until a squad (or itself) is this many metres from the Mcom.", new AcceptableValueRange<float>(0, 150)));

        const string defend = "Rush AI - Defenders";
        DefenderReorderMin = config.Bind(defend, "ReorderMin", 15f,
            new ConfigDescription("Defenders get new positions every ReorderMin-ReorderMax seconds.", new AcceptableValueRange<float>(5, 300)));
        DefenderReorderMax = config.Bind(defend, "ReorderMax", 30f,
            new ConfigDescription("See ReorderMin.", new AcceptableValueRange<float>(5, 300)));
        DefenderForwardChance = config.Bind(defend, "ForwardChance", 0.3f,
            new ConfigDescription("Share of defenders meeting the attack forward on the approach.", new AcceptableValueRange<float>(0, 1)));
        DefenderLaneChance = config.Bind(defend, "LaneChance", 0.3f,
            new ConfigDescription("Share of defenders using the map's defend areas.", new AcceptableValueRange<float>(0, 1)));
        DefenderOverwatchChance = config.Bind(defend, "OverwatchChance", 0.25f,
            new ConfigDescription("Share of defenders in a 10-25m ring around the Mcom. Whatever is left of the four shares anchors 5-9m from it.", new AcceptableValueRange<float>(0, 1)));
        DefenderReactRange = config.Bind(defend, "ReactRange", 60f,
            new ConfigDescription("Defenders within this many metres react when the Mcom is being armed or is armed.", new AcceptableValueRange<float>(0, 300)));
        DefenderResponderChance = config.Bind(defend, "ResponderChance", 0.35f,
            new ConfigDescription("Share of reacting defenders that close in to 3-7m, the rest hold a 10-22m ring.", new AcceptableValueRange<float>(0, 1)));
    }
}
