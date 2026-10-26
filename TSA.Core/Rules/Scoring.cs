using TSA.Core.Data;
using TSA.Core.Enums;
using TSA.Core.Models;

namespace TSA.Core.Rules;

public enum ScoringLevel
{
    None,
    Presence,
    Domination,
    Control
}

public sealed record SideScore(Side Side, int Countries, int Battlegrounds, ScoringLevel Level, int LevelVp, int BattlegroundVp, int AdjacencyVp)
{
    public int Total => LevelVp + BattlegroundVp + AdjacencyVp;
}

public sealed record RegionScore(Region Region, SideScore Us, SideScore Ussr)
{
    /// <summary>Net VP, positive for the US.</summary>
    public int Net => Us.Total - Ussr.Total;

    public string Describe() =>
        $"{Region.Label()} scoring: US {Us.Level} ({Us.Total} VP), USSR {Ussr.Level} ({Ussr.Total} VP).";
}

public static class Scoring
{
    private sealed record Table(int Presence, int Domination, int Control);

    private static readonly IReadOnlyDictionary<Region, Table> Tables = new Dictionary<Region, Table>
    {
        [Region.Europe] = new(3, 7, 0),
        [Region.Asia] = new(3, 7, 9),
        [Region.MiddleEast] = new(3, 5, 7),
        [Region.Africa] = new(1, 4, 6),
        [Region.CentralAmerica] = new(1, 3, 5),
        [Region.SouthAmerica] = new(2, 5, 6)
    };

    public static readonly IReadOnlyDictionary<int, Region> ScoringCards = new Dictionary<int, Region>
    {
        [CardIds.AsiaScoring] = Region.Asia,
        [CardIds.EuropeScoring] = Region.Europe,
        [CardIds.MiddleEastScoring] = Region.MiddleEast,
        [CardIds.CentralAmericaScoring] = Region.CentralAmerica,
        [CardIds.AfricaScoring] = Region.Africa,
        [CardIds.SouthAmericaScoring] = Region.SouthAmerica
    };

    /// <summary>
    /// Calculates a region's score without changing anything.
    /// </summary>
    public static RegionScore Calculate(GameState state, Region region, bool finalScoring)
    {
        var countries = state.Countries.Where(c => c.Region == region).ToList();

        // Formosan Resolution: Taiwan is a battleground for scoring while the US controls it.
        bool IsBattleground(Country c) =>
            c.Battleground ||
            (c.Id == "taiwan" && state.HasEffect(CardIds.FormosanResolution) && c.IsControlledByUS);

        var totalBattlegrounds = countries.Count(IsBattleground);

        var shuttle = !finalScoring
            && region is Region.MiddleEast or Region.Asia
            && state.HasEffect(CardIds.ShuttleDiplomacy);

        SideScore ScoreFor(Side side)
        {
            var controlled = countries.Where(c => c.IsControlledBy(side)).ToList();
            var battlegrounds = controlled.Count(IsBattleground);
            var count = controlled.Count;

            if (shuttle && side == Side.USSR && battlegrounds > 0)
            {
                battlegrounds--;
                count--;
            }

            var adjacency = controlled.Count(c => c.AdjacentSuperpower == side.Opponent());
            return new SideScore(side, count, battlegrounds, ScoringLevel.None, 0, battlegrounds, adjacency);
        }

        var us = ScoreFor(Side.US);
        var ussr = ScoreFor(Side.USSR);
        var table = Tables[region];

        SideScore WithLevel(SideScore me, SideScore them)
        {
            var level = ScoringLevel.None;

            if (me.Countries > 0)
            {
                level = ScoringLevel.Presence;
            }

            var nonBattlegrounds = me.Countries - me.Battlegrounds;

            if (me.Countries > them.Countries && me.Battlegrounds > them.Battlegrounds &&
                me.Battlegrounds > 0 && nonBattlegrounds > 0)
            {
                level = ScoringLevel.Domination;
            }

            if (me.Countries > them.Countries && me.Battlegrounds == totalBattlegrounds && totalBattlegrounds > 0)
            {
                level = ScoringLevel.Control;
            }

            var vp = level switch
            {
                ScoringLevel.Presence => table.Presence,
                ScoringLevel.Domination => table.Domination,
                ScoringLevel.Control => table.Control,
                _ => 0
            };

            return me with { Level = level, LevelVp = vp };
        }

        return new RegionScore(region, WithLevel(us, ussr), WithLevel(ussr, us));
    }

    /// <summary>
    /// Scores a region and applies the VP. Control of Europe wins the game.
    /// </summary>
    public static RegionScore Apply(GameState state, Region region, bool finalScoring)
    {
        var score = Calculate(state, region, finalScoring);
        state.AddLog(null, score.Describe());

        if (region == Region.Europe)
        {
            if (score.Us.Level == ScoringLevel.Control)
            {
                Victory.EndGame(state, Side.US, "US controls Europe.");
            }

            if (score.Ussr.Level == ScoringLevel.Control)
            {
                Victory.EndGame(state, Side.USSR, "USSR controls Europe.");
            }
        }

        if (score.Net != 0)
        {
            state.AddVp(score.Net > 0 ? Side.US : Side.USSR, Math.Abs(score.Net));
        }

        if (!finalScoring && region is Region.MiddleEast or Region.Asia && state.HasEffect(CardIds.ShuttleDiplomacy))
        {
            state.RemoveEffect(CardIds.ShuttleDiplomacy);
            state.MoveCard(CardIds.ShuttleDiplomacy, CardLocation.Discard);
            state.AddLog(Side.US, "Shuttle Diplomacy used and discarded.");
        }

        return score;
    }

    private static readonly IReadOnlyDictionary<string, int> SoutheastAsiaValues = new Dictionary<string, int>
    {
        ["burma"] = 1,
        ["laos_cambodia"] = 1,
        ["vietnam"] = 1,
        ["malaysia"] = 1,
        ["indonesia"] = 1,
        ["philippines"] = 1,
        ["thailand"] = 2
    };

    /// <summary>Net VP for Southeast Asia scoring, positive for the US.</summary>
    public static int SoutheastAsiaNet(GameState state) =>
        SoutheastAsiaValues.Sum(kv =>
        {
            var country = state.Country(kv.Key);
            return country.IsControlledByUS ? kv.Value : country.IsControlledByUSSR ? -kv.Value : 0;
        });

    public static void ApplySoutheastAsia(GameState state)
    {
        var net = SoutheastAsiaNet(state);
        state.AddLog(null, $"Southeast Asia scoring: net {GameState.VpLabel(net)}.");

        if (net != 0)
        {
            state.AddVp(net > 0 ? Side.US : Side.USSR, Math.Abs(net));
        }
    }

    /// <summary>
    /// End of Turn 10: score every region (Europe control still wins), +1 VP for
    /// The China Card holder, then the higher VP wins. 20 VP is not automatic here.
    /// </summary>
    public static void ApplyFinal(GameState state)
    {
        state.AddLog(null, "Final scoring.");

        foreach (var region in Enum.GetValues<Region>())
        {
            Apply(state, region, finalScoring: true);
        }

        state.AddVp(state.ChinaCardOwner, 1);
        Victory.EndOnPoints(state, "Final scoring complete.");
    }

    /// <summary>
    /// Regions a side Dominates or Controls (for Summit).
    /// </summary>
    public static int RegionsDominatedOrControlled(GameState state, Side side) =>
        Enum.GetValues<Region>().Count(region =>
        {
            var score = Calculate(state, region, finalScoring: true);
            var mine = side == Side.US ? score.Us : score.Ussr;
            return mine.Level is ScoringLevel.Domination or ScoringLevel.Control;
        });
}
