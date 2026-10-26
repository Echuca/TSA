using TSA.Core.Engine;
using TSA.Core.Models;

namespace TSA.Components;

/// <summary>
/// What the player has tapped on the map for the current decision, before confirming.
/// Shared by the decision panel and the board.
/// </summary>
public sealed class BoardInteraction
{
    public DecisionRequest? Request { get; private set; }

    /// <summary>Markers added/removed per country (InfluenceRequest).</summary>
    public Dictionary<string, int> Allocation { get; } = [];

    /// <summary>Countries picked (CountryRequest uses at most one).</summary>
    public List<string> Selected { get; } = [];

    /// <summary>Clears the working state when a new decision arrives.</summary>
    public void Sync(DecisionRequest? request)
    {
        if (ReferenceEquals(request, Request))
        {
            return;
        }

        Request = request;
        Allocation.Clear();
        Selected.Clear();
    }

    public IReadOnlyList<string> Eligible => Request switch
    {
        CountryRequest r => r.Eligible,
        CountriesRequest r => r.Eligible,
        InfluenceRequest r => r.Eligible,
        _ => []
    };

    public bool IsActive => Request is CountryRequest or CountriesRequest or InfluenceRequest;

    public bool IsEligible(string countryId) => Eligible.Contains(countryId);

    public string? ReasonFor(string countryId) => Request switch
    {
        CountryRequest r => r.Reasons.GetValueOrDefault(countryId),
        InfluenceRequest r => r.Reasons.GetValueOrDefault(countryId),
        _ => null
    };

    public int Delta(string countryId) => Allocation.GetValueOrDefault(countryId);

    public bool CanIncrement(Country country) =>
        Request is InfluenceRequest r && IsEligible(country.Id) && Delta(country.Id) < r.MaxFor(country);

    public void Increment(Country country)
    {
        if (CanIncrement(country))
        {
            Allocation[country.Id] = Delta(country.Id) + 1;
        }
    }

    public void Decrement(Country country)
    {
        var value = Delta(country.Id) - 1;

        if (value <= 0)
        {
            Allocation.Remove(country.Id);
        }
        else
        {
            Allocation[country.Id] = value;
        }
    }

    /// <summary>Tap on a country tile when choosing countries.</summary>
    public void Tap(string countryId)
    {
        if (!IsEligible(countryId))
        {
            return;
        }

        switch (Request)
        {
            case CountryRequest:
                Selected.Clear();
                Selected.Add(countryId);
                break;

            case CountriesRequest r:
                if (!Selected.Remove(countryId) && Selected.Count < r.Max)
                {
                    Selected.Add(countryId);
                }

                break;
        }
    }

    public void Clear()
    {
        Allocation.Clear();
        Selected.Clear();
    }
}
