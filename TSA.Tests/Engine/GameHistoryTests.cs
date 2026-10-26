using TSA.Core.Data;
using TSA.Core.Engine;
using TSA.Core.Enums;
using TSA.Core.Models;

namespace TSA.Tests.Engine;

/// <summary>
/// Undo works by restoring the latest checkpoint and replaying all but the last answer.
/// These tests drive the real engine the way GameSession does.
/// </summary>
public class GameHistoryTests
{
    private sealed class Harness
    {
        public GameHistory History { get; } = new();

        public GameState State { get; private set; } = default!;

        /// <summary>Runs from a state, replaying answers, then answering from the script until it runs out.</summary>
        public async Task<DecisionRequest> Run(GameState state, IEnumerable<object?> replay, ScriptedDecisions script)
        {
            State = state;
            var provider = new RecordingDecisionProvider(script, History);
            provider.Replay(replay);

            var runner = new GameRunner(state, provider, () =>
            {
                History.Checkpoint(state.Serialize());
                return Task.CompletedTask;
            });

            var stuck = await Assert.ThrowsAsync<ScriptExhaustedException>(runner.RunAsync);
            return stuck.Request;
        }

        public Task<DecisionRequest> Undo()
        {
            var (state, replay) = History.StepBack();
            return Run(GameState.Deserialize(state), replay, new ScriptedDecisions());
        }

        public Task<DecisionRequest> Resume()
        {
            var saved = GameHistory.Deserialize(History.Serialize(15));
            var (state, replay) = saved.Resume();
            History.Entries.Clear();
            History.Entries.AddRange(saved.Entries);
            return Run(GameState.Deserialize(state), replay, new ScriptedDecisions());
        }
    }

    private static GameState ActionRoundGame()
    {
        var state = GameRunner.CreateGame();
        state.Phase = GamePhase.ActionRounds;
        return state;
    }

    [Fact]
    public async Task Undo_Goes_Back_One_Answer_Within_A_Step()
    {
        var harness = new Harness();

        var script = new ScriptedDecisions()
            .Card(CardIds.Comecon)
            .Option("Operations")
            .Option("Place Influence");

        var stoppedAt = await harness.Run(ActionRoundGame(), [], script);
        Assert.IsType<InfluenceRequest>(stoppedAt);

        // Back to "use 3 Ops for?"
        stoppedAt = await harness.Undo();
        Assert.Contains("use 3 Ops for", stoppedAt.Prompt);

        // Back to "how is it used?"
        stoppedAt = await harness.Undo();
        Assert.Contains("how is it used", stoppedAt.Prompt);

        // Back to choosing the card.
        stoppedAt = await harness.Undo();
        Assert.IsType<CardRequest>(stoppedAt);
        Assert.False(harness.History.CanUndo);
    }

    [Fact]
    public async Task Undo_Steps_Back_Into_The_Previous_Action_Round()
    {
        var harness = new Harness();

        var script = new ScriptedDecisions()
            .Card(CardIds.KoreanWar)
            .Option("Event")
            .Die(5); // USSR wins in South Korea, then the US action round starts

        var stoppedAt = await harness.Run(ActionRoundGame(), [], script);
        Assert.Equal(Side.US, stoppedAt.Actor);
        Assert.Equal(-2, harness.State.VictoryPoints);

        // Undo the die roll: back to entering it, with the war not yet resolved.
        stoppedAt = await harness.Undo();
        Assert.IsType<DieRequest>(stoppedAt);
        Assert.Equal(0, harness.State.VictoryPoints);
        Assert.Equal(1, harness.State.Country("south_korea").UsInfluence);
    }

    [Fact]
    public async Task Resume_Returns_To_The_Same_Question()
    {
        var harness = new Harness();

        var script = new ScriptedDecisions()
            .Card(CardIds.Comecon)
            .Option("Operations")
            .Option("Place Influence")
            .Influence(("east_germany", 2), ("poland", 1)); // then the US action round starts

        await harness.Run(ActionRoundGame(), [], script);

        var stoppedAt = await harness.Resume();

        Assert.Equal(Side.US, stoppedAt.Actor);
        Assert.Equal(5, harness.State.Country("east_germany").UssrInfluence);

        // Saved answers come back as JSON; undo after resume still replays them correctly.
        stoppedAt = await harness.Undo();
        Assert.IsType<InfluenceRequest>(stoppedAt);
        Assert.Equal(3, harness.State.Country("east_germany").UssrInfluence);
    }

    [Fact]
    public void Correction_Can_Be_Undone()
    {
        var history = new GameHistory();
        history.Checkpoint("before");
        history.Record(1);
        history.AddCorrection("corrected");

        var (state, replay) = history.StepBack();

        Assert.Equal("before", state);
        Assert.Empty(replay);
    }
}
