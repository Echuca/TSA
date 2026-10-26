namespace TSA.Core.Enums;

public enum CardEra
{
    EarlyWar,
    MidWar,
    LateWar
}

/// <summary>
/// Where a card is, as far as the assistant knows. Hands are hidden, so a card that is
/// in the draw pile or in either player's hand is simply <see cref="Available"/>.
/// </summary>
public enum CardLocation
{
    /// <summary>Its era's deck has not been shuffled in yet, or it is an unused optional card.</summary>
    NotInGame,

    /// <summary>In the draw pile or in a player's hand.</summary>
    Available,

    Discard,

    /// <summary>Removed from the game (asterisk event played).</summary>
    Removed,

    /// <summary>Displayed in front of a player (e.g. Shuttle Diplomacy) until used.</summary>
    InPlay
}

public enum GamePhase
{
    Setup,
    TurnStart,
    Headline,
    ActionRounds,
    TurnEnd,
    GameOver
}
