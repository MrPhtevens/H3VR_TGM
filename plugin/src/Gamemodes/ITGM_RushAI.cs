using FistVR;

namespace TeamsGameMode;

/// <summary>
/// Rush Sosig AI, host only. Picked at the start of each game from the Tactics setting.
/// </summary>
public interface ITGM_RushAI
{
    /// <summary>
    /// New objective: fresh orders and operators
    /// </summary>
    void Reset();

    /// <summary>
    /// New game: forget everything
    /// </summary>
    void ResetAll();

    void Update(Rush_CapturePoint objective, TGM_Area defenderArea, bool isReversed);

    /// <summary>
    /// Freshly spawned Sosig
    /// </summary>
    void OrderNew(Sosig s, Rush_CapturePoint objective, TGM_Area defenderArea, bool isReversed);
}

public enum TGM_RushTactics
{
    Simple,
    Squads,
}
