using TSA.Core.Data;
using TSA.Core.Enums;
using TSA.Core.Models;

namespace TSA.Core.Rules;

public static class InfluenceRules
{
    /// <summary>
    /// Countries holding <paramref name="side"/>'s influence. Taken at the start of the
    /// action round: new markers must go in or next to these (rule 6.1.1).
    /// </summary>
    public static IReadOnlySet<string> PlacementOrigins(GameState state, Side side) =>
        state.Countries.Where(c => c.Influence(side) > 0).Select(c => c.Id).ToHashSet();

    public static bool CanPlaceWithOps(GameState state, Side side, Country country, IReadOnlySet<string> origins)
    {
        if (side == Side.USSR &&
            state.GetEffect(CardIds.Chernobyl) is { } chernobyl &&
            chernobyl.Data == country.Region.ToString())
        {
            return false;
        }

        return origins.Contains(country.Id)
            || country.AdjacentSuperpower == side
            || country.AdjacentCountries.Any(origins.Contains);
    }

    /// <summary>
    /// Ops needed to add <paramref name="markers"/> influence one at a time: 2 per marker
    /// while the opponent controls the country, 1 once control is broken (rule 6.1.2).
    /// </summary>
    public static int PlacementCost(Country country, Side side, int markers)
    {
        var mine = country.Influence(side);
        var theirs = country.Influence(side.Opponent());
        var cost = 0;

        for (var i = 0; i < markers; i++)
        {
            var opponentControls = theirs >= country.Stability && theirs - mine >= country.Stability;
            cost += opponentControls ? 2 : 1;
            mine++;
        }

        return cost;
    }

    public static int PlacementCost(GameState state, Side side, IReadOnlyDictionary<string, int> allocation) =>
        allocation.Where(kv => kv.Value > 0).Sum(kv => PlacementCost(state.Country(kv.Key), side, kv.Value));

    public static OpsScope ScopeOf(GameState state, IReadOnlyDictionary<string, int> allocation) =>
        OpsScope.For(allocation.Where(kv => kv.Value > 0).Select(kv => state.Country(kv.Key)));
}

public static class CoupRules
{
    /// <summary>
    /// Returns why <paramref name="side"/> may not coup <paramref name="country"/>, or null.
    /// A "free" coup from an event ignores DEFCON region restrictions (rule 6.3.5).
    /// </summary>
    public static string? WhyNot(GameState state, Side side, Country country, bool free)
    {
        if (country.Influence(side.Opponent()) == 0)
        {
            return "No opponent influence";
        }

        if (!free && Defcon.RegionRestricted(state, country.Region))
        {
            return $"DEFCON {state.Defcon} bans coups in {country.Region.Label()}";
        }

        if (side == Side.USSR)
        {
            if (Protections.NatoProtects(state, country))
            {
                return "Protected by NATO";
            }

            if (country.Id == "japan" && state.HasEffect(CardIds.UsJapanPact))
            {
                return "US/Japan Mutual Defense Pact";
            }

            if (country.Region == Region.Europe && state.HasEffect(CardIds.Reformer))
            {
                return "The Reformer: no USSR coups in Europe";
            }
        }

        return null;
    }

    public static int Modifier(GameState state, Side side, Country country)
    {
        var modifier = 0;

        if (state.HasEffect(CardIds.SaltNegotiations))
        {
            modifier--;
        }

        if (state.GetEffect(CardIds.DeathSquads) is { } squads &&
            country.Region is Region.CentralAmerica or Region.SouthAmerica)
        {
            modifier += squads.Side == side ? 1 : -1;
        }

        return modifier;
    }

    /// <summary>
    /// Resolves a coup with an entered die roll. Military Ops and DEFCON are updated;
    /// may end the game (Cuban Missile Crisis, DEFCON 1).
    /// </summary>
    public static CoupResult Resolve(GameState state, Side side, Country country, int ops, int roll, bool free)
    {
        if (state.HasEffect(CardIds.CubanMissileCrisis, side))
        {
            Victory.EndGame(state, side.Opponent(),
                $"Cuban Missile Crisis: {side.Label()} attempted a coup and caused nuclear war.");
        }

        var modifier = Modifier(state, side, country);
        var net = roll + ops + modifier - country.Stability * 2;
        var opponent = side.Opponent();
        var result = new CoupResult(country.Id, roll, ops, modifier, country.Stability * 2, net);

        if (net > 0)
        {
            var removed = Math.Min(net, country.Influence(opponent));
            country.RemoveInfluence(opponent, removed);
            country.AddInfluence(side, net - removed);
            result = result with { Removed = removed, Added = net - removed };
        }

        state.AddLog(side, $"{side.Label()} coup in {country.Name}: roll {roll} + {ops} ops{(modifier != 0 ? $" {modifier:+0;-0}" : "")} vs {country.Stability * 2} → " +
                           (net > 0 ? $"success, removed {result.Removed}, added {result.Added}." : "failed."));

        if (!free)
        {
            MilitaryOps.Add(state, side, ops);
        }

        if (side == Side.US && state.HasEffect(CardIds.SamanthaAndYuri))
        {
            state.AddVp(Side.USSR, 1);
        }

        if (country.Battleground && !(side == Side.US && state.HasEffect(CardIds.NuclearSubs)))
        {
            Defcon.Degrade(state, 1, side);
        }

        return result;
    }
}

public sealed record CoupResult(string CountryId, int Roll, int Ops, int Modifier, int Target, int Net)
{
    public bool Success => Net > 0;

    public int Removed { get; init; }

    public int Added { get; init; }
}

public static class RealignmentRules
{
    public static string? WhyNot(GameState state, Side side, Country country, bool ignoreDefcon)
    {
        if (country.Influence(side.Opponent()) == 0)
        {
            return "No opponent influence";
        }

        if (!ignoreDefcon && Defcon.RegionRestricted(state, country.Region))
        {
            return $"DEFCON {state.Defcon} bans realignments in {country.Region.Label()}";
        }

        if (side == Side.USSR)
        {
            if (Protections.NatoProtects(state, country))
            {
                return "Protected by NATO";
            }

            if (country.Id == "japan" && state.HasEffect(CardIds.UsJapanPact))
            {
                return "US/Japan Mutual Defense Pact";
            }
        }

        return null;
    }

    /// <summary>
    /// Die modifier for <paramref name="side"/> in a realignment of <paramref name="country"/> (rule 6.2.2).
    /// </summary>
    public static int Modifier(GameState state, Side side, Country country)
    {
        var modifier = country.AdjacentCountries.Count(id => state.Country(id).IsControlledBy(side));

        if (country.Influence(side) > country.Influence(side.Opponent()))
        {
            modifier++;
        }

        if (country.AdjacentSuperpower == side)
        {
            modifier++;
        }

        if (side == Side.US && state.HasEffect(CardIds.IranContra))
        {
            modifier--;
        }

        return modifier;
    }

    public static RealignResult Resolve(GameState state, Side side, Country country, int usRoll, int ussrRoll)
    {
        var usTotal = usRoll + Modifier(state, Side.US, country);
        var ussrTotal = ussrRoll + Modifier(state, Side.USSR, country);
        var difference = Math.Abs(usTotal - ussrTotal);

        Side? winner = usTotal > ussrTotal ? Side.US : ussrTotal > usTotal ? Side.USSR : null;
        var removed = 0;

        if (winner is { } w)
        {
            var loser = w.Opponent();
            removed = Math.Min(difference, country.Influence(loser));
            country.RemoveInfluence(loser, removed);
        }

        state.AddLog(side, $"{side.Label()} realigns {country.Name}: US {usTotal} vs USSR {ussrTotal} → " +
                           (winner is null ? "tie, no effect." : $"{winner.Value.Label()} removes {removed} {winner.Value.Opponent().Label()} influence."));

        return new RealignResult(country.Id, usTotal, ussrTotal, winner, removed);
    }
}

public sealed record RealignResult(string CountryId, int UsTotal, int UssrTotal, Side? Winner, int Removed);

public static class Protections
{
    /// <summary>
    /// NATO stops USSR coups, realignments and Brush War in US-controlled Europe,
    /// except France under De Gaulle and West Germany under Willy Brandt.
    /// </summary>
    public static bool NatoProtects(GameState state, Country country) =>
        state.HasEffect(CardIds.Nato)
        && country.Region == Region.Europe
        && country.IsControlledByUS
        && !(country.Id == "france" && state.HasEffect(CardIds.DeGaulle))
        && !(country.Id == "west_germany" && state.HasEffect(CardIds.WillyBrandt));
}

public static class MilitaryOps
{
    public static void Add(GameState state, Side side, int amount)
    {
        var player = state.Player(side);
        player.MilitaryOps = Math.Min(5, player.MilitaryOps + amount);
    }

    public static void Set(GameState state, Side side, int amount)
    {
        state.Player(side).MilitaryOps = Math.Clamp(amount, 0, 5);
    }
}
