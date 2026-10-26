using TSA.Core.Enums;

namespace TSA.Core.Models;

public sealed class PlayerState
{
    public Side Side { get; set; }

    /// <summary>
    /// Military Ops track position (0-5).
    /// </summary>
    public int MilitaryOps { get; set; }

    /// <summary>
    /// Space Race box reached (0 = start, 8 = Space Station).
    /// </summary>
    public int SpaceRacePosition { get; set; }

    /// <summary>
    /// Cards played on the Space Race this turn.
    /// </summary>
    public int SpaceRaceAttemptsThisTurn { get; set; }

    /// <summary>
    /// Action rounds completed this turn.
    /// </summary>
    public int ActionRoundsTaken { get; set; }

    /// <summary>
    /// Card chosen for the current headline (cleared each turn).
    /// </summary>
    public int? HeadlineCardId { get; set; }
}
