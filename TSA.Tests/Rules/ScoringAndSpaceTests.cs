using TSA.Core.Data;
using TSA.Core.Enums;
using TSA.Core.Models;
using TSA.Core.Rules;

namespace TSA.Tests.Rules;

public class ScoringAndSpaceTests
{
    [Fact]
    public void Central_America_Matches_Rule_Example()
    {
        // Rule 10.1.2 example: USSR controls Cuba, Haiti, Dominican Rep; US controls Guatemala, 1 in Panama.
        var state = GameState.NewGame();
        state.Country("cuba").UssrInfluence = 3;
        state.Country("haiti").UssrInfluence = 1;
        state.Country("dominican_republic").UssrInfluence = 1;
        state.Country("guatemala").UsInfluence = 1;
        state.Country("panama").UsInfluence = 1;

        var score = Scoring.Calculate(state, Region.CentralAmerica, finalScoring: false);

        Assert.Equal(ScoringLevel.Domination, score.Ussr.Level);
        Assert.Equal(5, score.Ussr.Total);
        Assert.Equal(1, score.Us.Total);

        Scoring.Apply(state, Region.CentralAmerica, finalScoring: false);
        Assert.Equal(-4, state.VictoryPoints);
    }

    [Fact]
    public void Domination_Needs_A_Non_Battleground()
    {
        var state = GameState.NewGame();
        state.Country("iran").UsInfluence = 2;
        state.Country("iraq").UsInfluence = 3;

        var score = Scoring.Calculate(state, Region.MiddleEast, finalScoring: false);

        Assert.Equal(ScoringLevel.Presence, score.Us.Level);
        Assert.Equal(3 + 2, score.Us.Total);
    }

    [Fact]
    public void Control_Of_Europe_Wins()
    {
        var state = GameState.NewGame();

        foreach (var id in new[] { "france", "west_germany", "italy", "east_germany", "poland" })
        {
            state.Country(id).UsInfluence = 5;
        }

        Assert.Throws<GameOverException>(() => Scoring.Apply(state, Region.Europe, finalScoring: false));
        Assert.Equal(Side.US, state.Winner);
    }

    [Fact]
    public void Shuttle_Diplomacy_Removes_A_Ussr_Battleground_Then_Discards()
    {
        var state = GameState.NewGame();
        state.Country("iraq").UssrInfluence = 3;
        state.AddEffect(CardIds.ShuttleDiplomacy, Side.US);
        state.MoveCard(CardIds.ShuttleDiplomacy, CardLocation.InPlay);

        var score = Scoring.Apply(state, Region.MiddleEast, finalScoring: false);

        Assert.Equal(0, score.Ussr.Total);
        Assert.False(state.HasEffect(CardIds.ShuttleDiplomacy));
        Assert.Equal(CardLocation.Discard, state.LocationOf(CardIds.ShuttleDiplomacy));
    }

    [Fact]
    public void Southeast_Asia_Thailand_Is_Worth_Two()
    {
        var state = GameState.NewGame();
        state.Country("thailand").UssrInfluence = 2;
        state.Country("burma").UsInfluence = 2;

        Assert.Equal(-1, Scoring.SoutheastAsiaNet(state));
    }

    [Fact]
    public void Space_Race_Awards_First_And_Second_Vp()
    {
        var state = GameState.NewGame();

        Assert.True(SpaceRace.Attempt(state, Side.US, 3));
        Assert.Equal(2, state.VictoryPoints);

        Assert.False(SpaceRace.Attempt(state, Side.USSR, 4));
        Assert.True(SpaceRace.Attempt(state, Side.USSR, 1));
        Assert.Equal(1, state.VictoryPoints);
    }

    [Fact]
    public void Space_Race_Limits_And_Ops_Requirements()
    {
        var state = GameState.NewGame();

        Assert.NotNull(SpaceRace.WhyNot(state, Side.US, 1));
        Assert.Null(SpaceRace.WhyNot(state, Side.US, 2));

        SpaceRace.Attempt(state, Side.US, 1);
        Assert.Equal("Space Race limit for this turn reached", SpaceRace.WhyNot(state, Side.US, 4));

        state.US.SpaceRacePosition = 2;
        Assert.Equal(2, SpaceRace.AttemptsAllowed(state, Side.US));
        Assert.Null(SpaceRace.WhyNot(state, Side.US, 2));

        state.USSR.SpaceRacePosition = 2;
        Assert.Equal(1, SpaceRace.AttemptsAllowed(state, Side.US));
    }
}
