using TSA.Core.Enums;

namespace TSA.Core.Data;

/// <summary>
/// A card from the deck. Loaded from cards.json.
/// </summary>
public sealed class CardDefinition
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Ops { get; set; }

    /// <summary>
    /// The superpower associated with the event. Neutral = both players.
    /// </summary>
    public Side Side { get; set; }

    public CardEra Era { get; set; }

    public bool Scoring { get; set; }

    /// <summary>
    /// Asterisk on the card: removed from the game when played as an event.
    /// </summary>
    public bool RemoveAfterEvent { get; set; }

    /// <summary>
    /// Underlined title: the event stays in effect after it is played.
    /// </summary>
    public bool Permanent { get; set; }

    /// <summary>
    /// Optional cards 104-110, only used when enabled for the game.
    /// </summary>
    public bool Optional { get; set; }

    public string Text { get; set; } = string.Empty;

    public bool IsChinaCard => Id == CardIds.ChinaCard;

    public override string ToString() => $"{Name} ({Ops})";
}
