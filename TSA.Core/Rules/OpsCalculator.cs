using TSA.Core.Data;
using TSA.Core.Enums;
using TSA.Core.Models;

namespace TSA.Core.Rules;

/// <summary>
/// Where a card's Operations points are being spent, for region-dependent bonuses.
/// </summary>
public readonly record struct OpsScope(bool AllInAsia, bool AllInSoutheastAsia)
{
    public static readonly OpsScope None = new(false, false);

    public static OpsScope For(IEnumerable<Country> targets)
    {
        var list = targets.ToList();

        if (list.Count == 0)
        {
            return None;
        }

        return new OpsScope(
            list.All(c => c.Region == Region.Asia),
            list.All(c => c.IsIn(SubRegion.SoutheastAsia)));
    }

    public static OpsScope For(Country target) => For([target]);
}

public static class OpsCalculator
{
    /// <summary>
    /// Operations value of a card played by <paramref name="side"/>, after
    /// Containment / Brezhnev Doctrine / Red Scare and The China Card / Vietnam Revolts
    /// region bonuses (rule 7.4).
    /// </summary>
    public static int Value(GameState state, Side side, int cardId, OpsScope scope)
    {
        var card = GameData.Instance.Card(cardId);

        if (card.Scoring)
        {
            return 0;
        }

        var value = card.Ops;

        if ((side == Side.US && state.HasEffect(CardIds.Containment)) ||
            (side == Side.USSR && state.HasEffect(CardIds.BrezhnevDoctrine)))
        {
            value = Math.Max(value, Math.Min(4, value + 1));
        }

        if (card.IsChinaCard && scope.AllInAsia)
        {
            value++;
        }

        if (side == Side.USSR && scope.AllInSoutheastAsia && state.HasEffect(CardIds.VietnamRevolts))
        {
            value++;
        }

        if (state.HasEffect(CardIds.RedScarePurge, side))
        {
            value = Math.Max(1, value - 1);
        }

        return value;
    }

    /// <summary>Value with no region bonuses (discard requirements, Space Race).</summary>
    public static int Value(GameState state, Side side, int cardId) =>
        Value(state, side, cardId, OpsScope.None);

    /// <summary>Highest value the card could reach (for display).</summary>
    public static int MaxValue(GameState state, Side side, int cardId) =>
        Value(state, side, cardId, new OpsScope(true, true));
}
