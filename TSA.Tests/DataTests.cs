using TSA.Core.Data;
using TSA.Core.Enums;
using TSA.Core.Events;
using TSA.Core.Models;

namespace TSA.Tests;

public class DataTests
{
    private static GameData Data => GameData.Instance;

    [Fact]
    public void Map_Has_84_Countries_With_TwoWay_Connections()
    {
        Assert.Equal(84, Data.Countries.Count);
        Assert.Empty(AdjacencyValidator.Validate(Data.Countries));
    }

    [Theory]
    [InlineData(Region.Europe, 21, 5)]
    [InlineData(Region.Asia, 15, 6)]
    [InlineData(Region.MiddleEast, 10, 6)]
    [InlineData(Region.Africa, 18, 5)]
    [InlineData(Region.CentralAmerica, 10, 3)]
    [InlineData(Region.SouthAmerica, 10, 4)]
    public void Region_Country_And_Battleground_Counts(Region region, int countries, int battlegrounds)
    {
        var inRegion = Data.Countries.Where(c => c.Region == region).ToList();

        Assert.Equal(countries, inRegion.Count);
        Assert.Equal(battlegrounds, inRegion.Count(c => c.Battleground));
    }

    [Fact]
    public void Subregions_Match_The_Board()
    {
        var state = GameState.NewGame();

        Assert.Equal(14, state.Countries.Count(c => c.IsIn(SubRegion.WesternEurope)));
        Assert.Equal(9, state.Countries.Count(c => c.IsIn(SubRegion.EasternEurope)));
        Assert.Equal(7, state.Countries.Count(c => c.IsIn(SubRegion.SoutheastAsia)));
    }

    [Fact]
    public void Superpower_Adjacency()
    {
        string Ids(Side side) => string.Join(",", Data.Countries.Where(c => c.AdjacentSuperpower == side).Select(c => c.Id).Order());

        Assert.Equal("canada,cuba,japan,mexico", Ids(Side.US));
        Assert.Equal("afghanistan,finland,north_korea,poland,romania", Ids(Side.USSR));
    }

    [Fact]
    public void Deck_Has_110_Cards()
    {
        Assert.Equal(110, Data.Cards.Count);
        Assert.Equal(Data.Cards.Count, Data.Cards.Select(c => c.Id).Distinct().Count());
        Assert.Equal(35, Data.Cards.Count(c => c.Era == CardEra.EarlyWar && !c.Optional && !c.IsChinaCard));
        Assert.Equal(46, Data.Cards.Count(c => c.Era == CardEra.MidWar && !c.Optional));
        Assert.Equal(21, Data.Cards.Count(c => c.Era == CardEra.LateWar && !c.Optional));
    }

    [Fact]
    public void Every_Event_Has_A_Handler()
    {
        var missing = Data.Cards
            .Where(c => !c.Scoring && !c.IsChinaCard && c.Id != CardIds.UnIntervention)
            .Where(c => !CardEvents.IsImplemented(c.Id))
            .Select(c => c.Name)
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void New_Game_Excludes_Optional_Cards_Unless_Requested()
    {
        Assert.Equal(CardLocation.NotInGame, GameState.NewGame().LocationOf(CardIds.CambridgeFive));
        Assert.Equal(CardLocation.Available, GameState.NewGame(optionalCards: true).LocationOf(CardIds.CambridgeFive));
        Assert.Equal(CardLocation.NotInGame, GameState.NewGame().LocationOf(CardIds.BrushWar));
    }

    [Fact]
    public void State_Round_Trips_Through_Json()
    {
        var state = GameState.NewGame();
        state.Country("iran").AddInfluence(Side.US, 2);
        state.AddEffect(CardIds.Nato, Side.US);

        var copy = GameState.Deserialize(state.Serialize());

        Assert.Equal(2, copy.Country("iran").UsInfluence);
        Assert.True(copy.HasEffect(CardIds.Nato));
        Assert.Equal(state.Serialize(), copy.Serialize());
    }
}
