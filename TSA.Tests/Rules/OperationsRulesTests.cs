using TSA.Core.Data;
using TSA.Core.Enums;
using TSA.Core.Models;
using TSA.Core.Rules;

namespace TSA.Tests.Rules;

public class OperationsRulesTests
{
    [Fact]
    public void Placement_Costs_Two_Until_Control_Is_Broken()
    {
        // Rule 6.1.2 example: US has 2 in Turkey (stability 2).
        var state = GameState.NewGame();
        var turkey = state.Country("turkey");
        turkey.UsInfluence = 2;

        Assert.Equal(2, InfluenceRules.PlacementCost(turkey, Side.USSR, 1));
        Assert.Equal(4, InfluenceRules.PlacementCost(turkey, Side.USSR, 3));

        turkey.UsInfluence = 1;
        Assert.Equal(3, InfluenceRules.PlacementCost(turkey, Side.USSR, 3));
    }

    [Fact]
    public void Placement_Must_Be_Adjacent_To_Existing_Influence()
    {
        // Rule 6.1.3 example: US markers in Panama.
        var state = GameState.NewGame();
        state.Country("panama").UsInfluence = 1;
        var origins = InfluenceRules.PlacementOrigins(state, Side.US);

        Assert.True(InfluenceRules.CanPlaceWithOps(state, Side.US, state.Country("costa_rica"), origins));
        Assert.True(InfluenceRules.CanPlaceWithOps(state, Side.US, state.Country("colombia"), origins));
        Assert.False(InfluenceRules.CanPlaceWithOps(state, Side.US, state.Country("nicaragua"), origins));
        Assert.True(InfluenceRules.CanPlaceWithOps(state, Side.US, state.Country("mexico"), origins), "Adjacent to the US");
    }

    [Fact]
    public void China_Card_Bonus_Only_In_Asia()
    {
        var state = GameState.NewGame();

        Assert.Equal(4, OpsCalculator.Value(state, Side.USSR, CardIds.ChinaCard, OpsScope.None));
        Assert.Equal(5, OpsCalculator.Value(state, Side.USSR, CardIds.ChinaCard, new OpsScope(true, false)));
    }

    [Fact]
    public void Ops_Modifiers_Stack_As_In_Rule_7_4_Example()
    {
        // China Card in SE Asia with Vietnam Revolts, under US Red Scare: 4 +1 +1 -1 = 5.
        var state = GameState.NewGame();
        state.AddEffect(CardIds.VietnamRevolts, Side.USSR, thisTurnOnly: true);
        state.AddEffect(CardIds.RedScarePurge, Side.USSR, thisTurnOnly: true);

        Assert.Equal(5, OpsCalculator.Value(state, Side.USSR, CardIds.ChinaCard, new OpsScope(true, true)));
    }

    [Fact]
    public void Containment_Caps_At_Four_And_Red_Scare_Floors_At_One()
    {
        var state = GameState.NewGame();
        state.AddEffect(CardIds.Containment, Side.US, thisTurnOnly: true);

        Assert.Equal(2, OpsCalculator.Value(state, Side.US, CardIds.CiaCreated));
        Assert.Equal(4, OpsCalculator.Value(state, Side.US, CardIds.MarshallPlan));

        state.AddEffect(CardIds.RedScarePurge, Side.USSR, thisTurnOnly: true);
        Assert.Equal(1, OpsCalculator.Value(state, Side.USSR, CardIds.Nasser));
    }

    [Fact]
    public void Coup_Matches_Rule_Example()
    {
        // Rule 6.3 example: US 3 Ops coup in Mexico (USSR 2), roll 4.
        var state = GameState.NewGame();
        var mexico = state.Country("mexico");
        mexico.UssrInfluence = 2;

        var result = CoupRules.Resolve(state, Side.US, mexico, ops: 3, roll: 4, free: false);

        Assert.Equal(3, result.Net);
        Assert.Equal(0, mexico.UssrInfluence);
        Assert.Equal(1, mexico.UsInfluence);
        Assert.Equal(3, state.US.MilitaryOps);
        Assert.Equal(4, state.Defcon); // Mexico is a battleground
    }

    [Fact]
    public void Free_Coup_Ignores_Defcon_But_Not_Influence_Requirement()
    {
        var state = GameState.NewGame();
        state.Defcon = 2;
        var iran = state.Country("iran");

        Assert.NotNull(CoupRules.WhyNot(state, Side.USSR, iran, free: true));

        iran.UsInfluence = 1;
        Assert.NotNull(CoupRules.WhyNot(state, Side.USSR, iran, free: false));
        Assert.Null(CoupRules.WhyNot(state, Side.USSR, iran, free: true));
    }

    [Fact]
    public void Nato_Protects_Us_Controlled_Europe_Except_Under_De_Gaulle()
    {
        var state = GameState.NewGame();
        state.AddEffect(CardIds.Nato, Side.US);
        state.Country("france").UsInfluence = 3;
        state.Country("italy").UsInfluence = 2;

        Assert.Equal("Protected by NATO", CoupRules.WhyNot(state, Side.USSR, state.Country("italy"), free: false));

        state.AddEffect(CardIds.DeGaulle, Side.USSR);
        Assert.Null(CoupRules.WhyNot(state, Side.USSR, state.Country("france"), free: false));
    }

    [Fact]
    public void Coup_Causing_Defcon_1_Loses_The_Game()
    {
        var state = GameState.NewGame();
        state.Defcon = 2;
        var iran = state.Country("iran");
        iran.UsInfluence = 1;

        Assert.Throws<GameOverException>(() => CoupRules.Resolve(state, Side.USSR, iran, 3, 6, free: false));
        Assert.Equal(Side.US, state.Winner);
    }

    [Fact]
    public void Realignment_Matches_Rule_Example()
    {
        // Rule 6.2 example: US realigns North Korea (USSR 3). US rolls 5, USSR 2 (+2).
        var state = GameState.NewGame();
        var northKorea = state.Country("north_korea");
        northKorea.UssrInfluence = 3;

        Assert.Equal(0, RealignmentRules.Modifier(state, Side.US, northKorea));
        Assert.Equal(2, RealignmentRules.Modifier(state, Side.USSR, northKorea));

        var result = RealignmentRules.Resolve(state, Side.US, northKorea, usRoll: 5, ussrRoll: 2);

        Assert.Equal(Side.US, result.Winner);
        Assert.Equal(2, northKorea.UssrInfluence);
    }

    [Fact]
    public void Defcon_Restricts_Regions()
    {
        var state = GameState.NewGame();

        state.Defcon = 4;
        Assert.True(Defcon.RegionRestricted(state, Region.Europe));
        Assert.False(Defcon.RegionRestricted(state, Region.Asia));

        state.Defcon = 2;
        Assert.True(Defcon.RegionRestricted(state, Region.MiddleEast));
        Assert.False(Defcon.RegionRestricted(state, Region.Africa));
    }
}
