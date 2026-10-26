using System.Text.Json.Serialization;
using TSA.Core.Data;
using TSA.Core.Enums;
using TSA.Core.Rules;

namespace TSA.Core.Models;

/// <summary>
/// Runtime state of a country: its map data plus current influence.
/// </summary>
public sealed class Country
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public Region Region { get; set; }

    public SubRegion SubRegions { get; set; }

    public int Stability { get; set; }

    public bool Battleground { get; set; }

    public Side? AdjacentSuperpower { get; set; }

    public List<string> AdjacentCountries { get; set; } = [];

    public int UsInfluence { get; set; }

    public int UssrInfluence { get; set; }

    [JsonIgnore]
    public Side Controller => ControlCalculator.GetController(this);

    [JsonIgnore]
    public bool IsControlledByUS => ControlCalculator.IsControlledByUS(this);

    [JsonIgnore]
    public bool IsControlledByUSSR => ControlCalculator.IsControlledByUSSR(this);

    public static Country FromDefinition(CountryDefinition definition) => new()
    {
        Id = definition.Id,
        Name = definition.Name,
        Region = definition.Region,
        SubRegions = definition.SubRegions.Aggregate(SubRegion.None, (all, s) => all | s),
        Stability = definition.Stability,
        Battleground = definition.Battleground,
        AdjacentSuperpower = definition.AdjacentSuperpower,
        AdjacentCountries = definition.Adjacent.ToList()
    };

    public bool IsIn(SubRegion subRegion) => (SubRegions & subRegion) != 0;

    public bool IsControlledBy(Side side) => ControlCalculator.IsControlledBy(this, side);

    public int Influence(Side side) => side switch
    {
        Side.US => UsInfluence,
        Side.USSR => UssrInfluence,
        _ => 0
    };

    public void SetInfluence(Side side, int amount)
    {
        amount = Math.Max(0, amount);

        switch (side)
        {
            case Side.US:
                UsInfluence = amount;
                break;
            case Side.USSR:
                UssrInfluence = amount;
                break;
            default:
                throw new InvalidOperationException("Neutral has no influence.");
        }
    }

    public void AddInfluence(Side side, int amount) =>
        SetInfluence(side, Influence(side) + amount);

    /// <summary>
    /// Removes influence, never going below zero.
    /// </summary>
    public void RemoveInfluence(Side side, int amount) =>
        SetInfluence(side, Influence(side) - amount);

    /// <summary>
    /// Raises <paramref name="side"/>'s influence just enough to control the country.
    /// </summary>
    public void GainControl(Side side)
    {
        var needed = Influence(side.Opponent()) + Stability;
        SetInfluence(side, Math.Max(Influence(side), needed));
    }

    public override string ToString() => Name;
}
