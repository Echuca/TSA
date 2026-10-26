using TSA.Core.Enums;

namespace TSA.Core.Models;

/// <summary>
/// An ongoing card effect (NATO, Red Scare, Quagmire, ...).
/// </summary>
public sealed class ActiveEffect
{
    /// <summary>
    /// Card that created the effect. See <see cref="Data.CardIds"/>.
    /// </summary>
    public int CardId { get; set; }

    /// <summary>
    /// Meaning depends on the card; documented on <see cref="Rules.Effects"/>.
    /// </summary>
    public Side Side { get; set; }

    /// <summary>
    /// Removed during end of turn.
    /// </summary>
    public bool ThisTurnOnly { get; set; }

    /// <summary>
    /// Extra data, e.g. the Region chosen for Chernobyl.
    /// </summary>
    public string? Data { get; set; }
}

public sealed class LogEntry
{
    public int Turn { get; set; }

    public string Round { get; set; } = string.Empty;

    public Side? Side { get; set; }

    public string Text { get; set; } = string.Empty;
}
