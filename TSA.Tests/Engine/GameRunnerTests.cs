using TSA.Core.Data;
using TSA.Core.Engine;
using TSA.Core.Enums;
using TSA.Core.Models;
using TSA.Core.Rules;

namespace TSA.Tests.Engine;

public class GameRunnerTests
{
    /// <summary>Runs the engine until the script runs out; returns the request it stopped on.</summary>
    private static async Task<DecisionRequest> RunUntilStuck(GameState state, ScriptedDecisions script)
    {
        var runner = new GameRunner(state, script);
        var stuck = await Assert.ThrowsAsync<ScriptExhaustedException>(runner.RunAsync);
        return stuck.Request;
    }

    /// <summary>A game past setup, at the start of turn 1's action rounds.</summary>
    private static GameState GameInActionRounds()
    {
        var state = GameRunner.CreateGame();
        state.Phase = GamePhase.ActionRounds;
        return state;
    }

    [Fact]
    public async Task Setup_Then_Headline_Order()
    {
        var state = GameRunner.CreateGame();

        var script = new ScriptedDecisions()
            .Influence(("poland", 4), ("east_germany", 1), ("yugoslavia", 1))
            .Influence(("west_germany", 2), ("italy", 4), ("france", 1))
            .Card(CardIds.OlympicGames)        // USSR headline
            .Card(CardIds.MiddleEastScoring)   // US headline (scoring resolves second)
            .Option("Participate")
            .Die(6).Die(1);                    // USSR sponsor 6+2 vs US 1

        var stoppedAt = await RunUntilStuck(state, script);

        Assert.Equal(GamePhase.ActionRounds, state.Phase);
        Assert.Equal(4, state.Country("poland").UssrInfluence);
        Assert.Equal(4, state.Country("italy").UsInfluence);
        Assert.Equal(-2, state.VictoryPoints); // Olympics +2 USSR, Middle East scoring 0
        Assert.Equal(CardLocation.Discard, state.LocationOf(CardIds.MiddleEastScoring));

        var cardRequest = Assert.IsType<CardRequest>(stoppedAt);
        Assert.Equal(Side.USSR, cardRequest.Actor);
        Assert.Contains(CardIds.ChinaCard, cardRequest.Eligible);
    }

    [Fact]
    public async Task Setup_Rejects_Influence_Outside_Eastern_Europe()
    {
        var state = GameRunner.CreateGame();
        var script = new ScriptedDecisions().Influence(("poland", 4), ("italy", 2));

        await Assert.ThrowsAsync<InvalidOperationException>(new GameRunner(state, script).RunAsync);
    }

    [Fact]
    public async Task Defectors_Headline_Cancels_Ussr_Headline()
    {
        var state = GameRunner.CreateGame();
        state.Phase = GamePhase.Headline;

        var script = new ScriptedDecisions()
            .Card(CardIds.Fidel)
            .Card(CardIds.Defectors);

        await RunUntilStuck(state, script);

        Assert.Equal(0, state.Country("cuba").UssrInfluence);
        Assert.Equal(CardLocation.Discard, state.LocationOf(CardIds.Fidel));
    }

    [Fact]
    public async Task Opponent_Event_Fires_When_Played_For_Ops()
    {
        var state = GameInActionRounds();

        var script = new ScriptedDecisions()
            .Card(CardIds.DuckAndCover)
            .Option("Operations")
            .Option("Event first")
            .Option("Place Influence")
            .Influence(("east_germany", 2), ("poland", 1));

        var stoppedAt = await RunUntilStuck(state, script);

        Assert.Equal(4, state.Defcon);
        Assert.Equal(1, state.VictoryPoints); // US gets 5 - DEFCON 4
        Assert.Equal(5, state.Country("east_germany").UssrInfluence);
        Assert.Equal(1, state.Country("poland").UssrInfluence);
        Assert.Equal(CardLocation.Discard, state.LocationOf(CardIds.DuckAndCover));
        Assert.Equal(Side.US, stoppedAt.Actor);
    }

    [Fact]
    public async Task Influence_Must_Use_All_Ops()
    {
        var state = GameInActionRounds();

        var script = new ScriptedDecisions()
            .Card(CardIds.Comecon)
            .Option("Operations")
            .Option("Place Influence")
            .Influence(("east_germany", 2));

        await Assert.ThrowsAsync<InvalidOperationException>(new GameRunner(state, script).RunAsync);
    }

    [Fact]
    public async Task China_Card_Gets_Bonus_In_Asia_And_Passes_Face_Down()
    {
        var state = GameInActionRounds();

        var script = new ScriptedDecisions()
            .Card(CardIds.ChinaCard)
            .Option("Operations")
            .Option("Place Influence")
            .Influence(("north_korea", 2), ("south_korea", 3)); // 5 Ops: 4 + 1 for all in Asia

        await RunUntilStuck(state, script);

        Assert.Equal(Side.US, state.ChinaCardOwner);
        Assert.False(state.ChinaCardFaceUp);
    }

    [Fact]
    public async Task Asterisk_Event_Is_Removed_And_Permanent_Effect_Recorded()
    {
        var state = GameInActionRounds();

        var script = new ScriptedDecisions()
            .Card(CardIds.UsJapanPact)
            .Option("Event");

        state.PhasingPlayer = Side.US;
        state.USSR.ActionRoundsTaken = 1;

        await RunUntilStuck(state, script);

        Assert.True(state.Country("japan").IsControlledByUS);
        Assert.True(state.HasEffect(CardIds.UsJapanPact));
        Assert.Equal(CardLocation.Removed, state.LocationOf(CardIds.UsJapanPact));
    }

    [Fact]
    public async Task Nato_Event_Not_Offered_Without_Prerequisite()
    {
        var state = GameInActionRounds();
        state.USSR.ActionRoundsTaken = 1;

        var script = new ScriptedDecisions()
            .Card(CardIds.Nato)
            .Then(r =>
            {
                var options = ((OptionRequest)r).Options;
                Assert.False(options[0].Enabled);
                Assert.Equal("Needs Marshall Plan or Warsaw Pact first", options[0].DisabledReason);
                return 1; // Operations
            });

        var stoppedAt = await RunUntilStuck(state, script);
        Assert.Contains("use 4 Ops", stoppedAt.Prompt);
    }

    [Fact]
    public async Task Korean_War_Victory()
    {
        var state = GameInActionRounds();

        var script = new ScriptedDecisions()
            .Card(CardIds.KoreanWar)
            .Option("Event")
            .Die(5);

        await RunUntilStuck(state, script);

        var southKorea = state.Country("south_korea");
        Assert.Equal(0, southKorea.UsInfluence);
        Assert.Equal(1, southKorea.UssrInfluence);
        Assert.Equal(-2, state.VictoryPoints);
        Assert.Equal(2, state.USSR.MilitaryOps);
        Assert.Equal(CardLocation.Removed, state.LocationOf(CardIds.KoreanWar));
    }

    [Fact]
    public async Task Coup_Into_Defcon_1_Ends_Game()
    {
        var state = GameInActionRounds();
        state.Defcon = 2;

        var script = new ScriptedDecisions()
            .Card(CardIds.Comecon)
            .Option("Operations")
            .Option("Coup")
            .Country("panama")
            .Die(6);

        await new GameRunner(state, script).RunAsync();

        Assert.Equal(GamePhase.GameOver, state.Phase);
        Assert.Equal(Side.US, state.Winner);
    }

    [Fact]
    public async Task End_Of_Turn_Applies_Military_Ops_Penalty()
    {
        var state = GameInActionRounds();
        state.US.ActionRoundsTaken = 6;
        state.USSR.ActionRoundsTaken = 6;
        state.Defcon = 3;
        state.USSR.MilitaryOps = 3;
        state.US.MilitaryOps = 1;

        var stoppedAt = await RunUntilStuck(state, new ScriptedDecisions().Option("No scoring cards held"));

        Assert.Equal(-2, state.VictoryPoints);
        Assert.Equal(2, state.Turn);
        Assert.Equal(4, state.Defcon); // improved at start of turn 2
        Assert.IsType<CardRequest>(stoppedAt); // headline
    }

    [Fact]
    public async Task Quagmire_Escape()
    {
        var state = GameInActionRounds();
        state.USSR.ActionRoundsTaken = 1;
        state.AddEffect(CardIds.Quagmire, Side.US);

        var script = new ScriptedDecisions()
            .Option("Discard")
            .Card(CardIds.Comecon)
            .Die(3);

        await RunUntilStuck(state, script);

        Assert.False(state.HasEffect(CardIds.Quagmire));
        Assert.Equal(CardLocation.Discard, state.LocationOf(CardIds.Comecon));
    }

    [Fact]
    public async Task Mid_War_Cards_Join_At_Turn_4()
    {
        var state = GameRunner.CreateGame();
        state.Phase = GamePhase.TurnStart;
        state.Turn = 4;

        await RunUntilStuck(state, new ScriptedDecisions());

        Assert.Equal(CardLocation.Available, state.LocationOf(CardIds.BrushWar));
        Assert.Equal(CardLocation.NotInGame, state.LocationOf(CardIds.Glasnost));
        Assert.Equal(7, new GameRunner(state, new ScriptedDecisions()).ActionRoundsFor(Side.US));
    }

    [Fact]
    public async Task Final_Scoring_After_Turn_10()
    {
        var state = GameInActionRounds();
        state.Turn = 10;
        state.US.ActionRoundsTaken = 7;
        state.USSR.ActionRoundsTaken = 7;
        state.US.MilitaryOps = 5;
        state.USSR.MilitaryOps = 5;

        await new GameRunner(state, new ScriptedDecisions().Option("No scoring cards held")).RunAsync();

        Assert.Equal(GamePhase.GameOver, state.Phase);
        Assert.NotNull(state.GameOverReason);
    }

    [Fact]
    public async Task Held_Scoring_Card_Is_Scored_At_End_Of_Turn()
    {
        var state = GameInActionRounds();
        state.US.ActionRoundsTaken = 6;
        state.USSR.ActionRoundsTaken = 6;
        state.US.MilitaryOps = 5;
        state.USSR.MilitaryOps = 5;
        state.Country("iran").UsInfluence = 2; // US controls a Middle East battleground

        var script = new ScriptedDecisions()
            .Then(r =>
            {
                Assert.Contains("Middle East Scoring", r.Detail);
                return 2; // US is holding a scoring card: score it now
            })
            .Card(CardIds.MiddleEastScoring)
            .Option("No scoring cards held");

        await RunUntilStuck(state, script);

        Assert.Equal(CardLocation.Discard, state.LocationOf(CardIds.MiddleEastScoring));
        Assert.Equal(3 + 1, state.VictoryPoints); // presence + 1 battleground
        Assert.Equal(2, state.Turn);
    }

    [Fact]
    public async Task Held_Scoring_Card_Loses_Under_Tournament_Rule()
    {
        var state = GameInActionRounds();
        state.US.ActionRoundsTaken = 6;
        state.USSR.ActionRoundsTaken = 6;
        state.US.MilitaryOps = 5;
        state.USSR.MilitaryOps = 5;

        await new GameRunner(state, new ScriptedDecisions().Option("USSR is holding a scoring card: USSR loses")).RunAsync();

        Assert.Equal(GamePhase.GameOver, state.Phase);
        Assert.Equal(Side.US, state.Winner);
    }
}
