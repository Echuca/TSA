using TSA.Core.Enums;
using TSA.Core.Models;

namespace TSA.Core.Rules;

/// <summary>
/// Thrown to unwind the engine immediately when the game ends mid-action
/// (nuclear war, 20 VP, Europe control, Wargames).
/// </summary>
public sealed class GameOverException(string reason) : Exception(reason);

public static class Victory
{
    public const int AutomaticVictoryVp = 20;

    public static void EndGame(GameState state, Side? winner, string reason)
    {
        state.Phase = GamePhase.GameOver;
        state.Winner = winner;
        state.GameOverReason = reason;
        state.AddLog(winner, $"GAME OVER: {reason}");

        throw new GameOverException(reason);
    }

    /// <summary>
    /// Ends the game if either side has reached 20 VP. Call after a card or scoring
    /// has fully resolved (all VP awards are applied before checking).
    /// </summary>
    public static void CheckAutomaticVictory(GameState state)
    {
        if (state.VictoryPoints >= AutomaticVictoryVp)
        {
            EndGame(state, Side.US, "US reached 20 VP.");
        }

        if (state.VictoryPoints <= -AutomaticVictoryVp)
        {
            EndGame(state, Side.USSR, "USSR reached 20 VP.");
        }
    }

    /// <summary>
    /// Ends the game on the current VP total (end of turn 10, Wargames).
    /// </summary>
    public static void EndOnPoints(GameState state, string reason)
    {
        var winner = state.VictoryPoints switch
        {
            > 0 => Side.US,
            < 0 => Side.USSR,
            _ => (Side?)null
        };

        var result = winner is null ? "The game is a draw" : $"{winner.Value.Label()} wins";
        EndGame(state, winner, $"{reason} {result} ({GameState.VpLabel(state.VictoryPoints)}).");
    }
}

public static class Defcon
{
    /// <summary>
    /// Moves DEFCON towards nuclear war. If it reaches 1, <paramref name="phasing"/>
    /// is responsible and loses.
    /// </summary>
    public static void Degrade(GameState state, int levels, Side phasing)
    {
        Set(state, state.Defcon - levels, phasing);
    }

    public static void Improve(GameState state, int levels)
    {
        var before = state.Defcon;
        state.Defcon = Math.Min(5, state.Defcon + levels);

        if (state.Defcon != before)
        {
            state.AddLog(null, $"DEFCON improves to {state.Defcon}.");
        }
    }

    public static void Set(GameState state, int level, Side phasing)
    {
        level = Math.Clamp(level, 1, 5);

        if (level == state.Defcon)
        {
            return;
        }

        state.Defcon = level;
        state.AddLog(null, $"DEFCON is now {level}.");

        if (level == 2)
        {
            state.DefconMovedTo2ThisRound = true;
        }

        if (level == 1)
        {
            Victory.EndGame(state, phasing.Opponent(), $"Nuclear war! {phasing.Label()} moved DEFCON to 1 and loses.");
        }
    }

    public static bool RegionRestricted(GameState state, Region region) =>
        state.Defcon <= region.DefconRestrictionLevel();
}
