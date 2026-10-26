using TSA.Core.Enums;
using TSA.Core.Models;

namespace TSA.Core.Rules;

/// <summary>
/// One box of the Space Race track, as printed on the board.
/// </summary>
public sealed record SpaceRaceBox(int Number, string Name, int OpsRequired, int MaxRoll, int FirstVp, int SecondVp, string? Ability);

public static class SpaceRace
{
    public static readonly IReadOnlyList<SpaceRaceBox> Track =
    [
        new(1, "Earth Satellite", 2, 3, 2, 1, null),
        new(2, "Animal in Space", 2, 4, 0, 0, "May play 2 Space Race cards per turn"),
        new(3, "Man in Space", 2, 3, 2, 0, null),
        new(4, "Man in Earth Orbit", 2, 4, 0, 0, "Opponent chooses and shows headline first"),
        new(5, "Lunar Orbit", 3, 3, 3, 1, null),
        new(6, "Eagle/Bear Has Landed", 3, 4, 0, 0, "May discard held card at end of turn"),
        new(7, "Space Shuttle", 3, 3, 4, 2, null),
        new(8, "Space Station", 4, 2, 2, 0, "May take 8 action rounds")
    ];

    public const int FinalBox = 8;

    public static SpaceRaceBox? NextBox(GameState state, Side side)
    {
        var position = state.Player(side).SpaceRacePosition;
        return position >= FinalBox ? null : Track[position];
    }

    /// <summary>
    /// A box's special ability belongs to the first player to reach it, until the
    /// second player also reaches it (rule 6.4.4).
    /// </summary>
    public static bool HasAbility(GameState state, Side side, int box) =>
        state.Player(side).SpaceRacePosition >= box &&
        state.Player(side.Opponent()).SpaceRacePosition < box;

    public static int AttemptsAllowed(GameState state, Side side) =>
        HasAbility(state, side, 2) ? 2 : 1;

    public static string? WhyNot(GameState state, Side side, int opsValue)
    {
        var next = NextBox(state, side);

        if (next is null)
        {
            return "Already at the end of the Space Race";
        }

        if (state.Player(side).SpaceRaceAttemptsThisTurn >= AttemptsAllowed(state, side))
        {
            return "Space Race limit for this turn reached";
        }

        if (opsValue < next.OpsRequired)
        {
            return $"Needs a {next.OpsRequired} Ops card";
        }

        return null;
    }

    /// <summary>
    /// Resolves a Space Race attempt with an entered die roll.
    /// </summary>
    public static bool Attempt(GameState state, Side side, int roll)
    {
        var player = state.Player(side);
        var next = NextBox(state, side)!;
        player.SpaceRaceAttemptsThisTurn++;

        if (roll > next.MaxRoll)
        {
            state.AddLog(side, $"{side.Label()} Space Race attempt for {next.Name}: rolled {roll}, failed.");
            return false;
        }

        state.AddLog(side, $"{side.Label()} Space Race attempt: rolled {roll}, success.");
        Advance(state, side, 1);
        return true;
    }

    /// <summary>
    /// Moves the marker forward, awarding VP for each box (or only the last box).
    /// </summary>
    public static void Advance(GameState state, Side side, int boxes, bool vpForLastBoxOnly = false)
    {
        var player = state.Player(side);
        var opponent = state.Player(side.Opponent());

        for (var i = 0; i < boxes && player.SpaceRacePosition < FinalBox; i++)
        {
            var box = Track[player.SpaceRacePosition];
            player.SpaceRacePosition++;

            state.AddLog(side, $"{side.Label()} reaches {box.Name}.{(box.Ability is null ? "" : $" ({box.Ability})")}");

            var isLast = i == boxes - 1 || player.SpaceRacePosition == FinalBox;

            if (!vpForLastBoxOnly || isLast)
            {
                var vp = opponent.SpaceRacePosition >= box.Number ? box.SecondVp : box.FirstVp;
                state.AddVp(side, vp);
            }
        }
    }
}
