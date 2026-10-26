using TSA.Core.Data;
using TSA.Core.Enums;
using TSA.Core.Models;

namespace TSA.Core.Engine;

/// <summary>
/// Everything a card event needs while it resolves.
/// </summary>
public sealed class EventContext
{
    public required GameRunner Runner { get; init; }

    public required int CardId { get; init; }

    /// <summary>The player carrying out the event text (the card's side, or the player of a both-player card).</summary>
    public required Side Player { get; init; }

    /// <summary>The player whose action caused the event. Responsible for DEFCON 1.</summary>
    public required Side Phasing { get; init; }

    public bool IsHeadline { get; init; }

    /// <summary>Set by events that send their card somewhere unusual (e.g. Missile Envy to the opponent's hand).</summary>
    public CardLocation? Destination { get; set; }

    public GameState State => Runner.State;

    public Decisions Ask => Runner.Ask;

    public CardDefinition Card => GameData.Instance.Card(CardId);

    public Side Opponent => Player.Opponent();

    public Country Country(string id) => State.Country(id);

    public IEnumerable<Country> Countries(Func<Country, bool> predicate) => State.Countries.Where(predicate);

    public void Log(string text) => State.AddLog(Player, text);

    /// <summary>
    /// Asks the player to add or remove a number of markers among <paramref name="eligible"/>, then applies it.
    /// </summary>
    public async Task<int> ChangeInfluence(
        string prompt,
        Side influenceSide,
        bool remove,
        IEnumerable<Country> eligible,
        int amount,
        int? maxPerCountry = null,
        bool upTo = false,
        Side? actor = null)
    {
        var request = InfluenceRequests.Fixed(
            actor ?? Player, prompt, influenceSide, remove, eligible, amount, maxPerCountry, upTo, Card.Text);

        var allocation = await Ask.Influence(request);
        Runner.ApplyInfluence(influenceSide, remove, allocation, actor ?? Player);
        return allocation.Values.Sum();
    }

    public Task<int> AddInfluence(string prompt, Side side, IEnumerable<Country> eligible, int amount, int? maxPerCountry = null) =>
        ChangeInfluence(prompt, side, remove: false, eligible, amount, maxPerCountry);

    public Task<int> RemoveInfluence(string prompt, Side side, IEnumerable<Country> eligible, int amount, int? maxPerCountry = null, bool upTo = false) =>
        ChangeInfluence(prompt, side, remove: true, eligible, amount, maxPerCountry, upTo);

    public Task<string?> ChooseCountry(string prompt, IEnumerable<Country> eligible, string? noneLabel = null, Side? actor = null) =>
        Ask.Country(new CountryRequest
        {
            Actor = actor ?? Player,
            Prompt = prompt,
            Detail = Card.Text,
            Eligible = eligible.Select(c => c.Id).ToList(),
            NoneLabel = noneLabel
        });

    public Task<IReadOnlyList<string>> ChooseCountries(string prompt, IEnumerable<Country> eligible, int min, int max)
    {
        var list = eligible.Select(c => c.Id).ToList();

        return Ask.Countries(new CountriesRequest
        {
            Actor = Player,
            Prompt = prompt,
            Detail = Card.Text,
            Eligible = list,
            Min = Math.Min(min, list.Count),
            Max = Math.Min(max, list.Count)
        });
    }

    public Task<int> Roll(string prompt, string? target = null, Side? roller = null) =>
        Ask.Die(roller ?? Player, prompt, target);

    /// <summary>
    /// Asks which card a player had to discard. Returns null if they had none.
    /// </summary>
    public Task<int?> ChooseCard(Side actor, string prompt, IEnumerable<int> eligible, string? noneLabel) =>
        Ask.Card(new CardRequest
        {
            Actor = actor,
            Prompt = prompt,
            Detail = Card.Text,
            Eligible = eligible.ToList(),
            NoneLabel = noneLabel
        });
}

public static class InfluenceRequests
{
    /// <summary>
    /// "Add/remove N influence among these countries, at most M per country."
    /// The required total shrinks if there is not enough room (e.g. too little influence to remove).
    /// </summary>
    public static InfluenceRequest Fixed(
        Side actor,
        string prompt,
        Side influenceSide,
        bool remove,
        IEnumerable<Country> eligible,
        int amount,
        int? maxPerCountry = null,
        bool upTo = false,
        string? detail = null)
    {
        var list = eligible.Where(c => !remove || c.Influence(influenceSide) > 0).ToList();
        var perCountry = maxPerCountry ?? int.MaxValue;

        var capacity = remove
            ? list.Sum(c => Math.Min(perCountry, c.Influence(influenceSide)))
            : maxPerCountry is null ? amount : perCountry * list.Count;

        var required = Math.Min(amount, capacity);
        var verb = remove ? "Remove" : "Add";

        return new InfluenceRequest
        {
            Actor = actor,
            Prompt = prompt,
            Detail = detail,
            InfluenceSide = influenceSide,
            Remove = remove,
            Eligible = list.Select(c => c.Id).ToList(),
            MaxPerCountry = maxPerCountry,
            Validate = allocation =>
            {
                var total = allocation.Values.Sum();

                if (total > required)
                {
                    return $"Only {required} allowed";
                }

                if (!upTo && total < required)
                {
                    return $"{verb} {required - total} more";
                }

                return null;
            },
            Status = allocation => $"{allocation.Values.Sum()} / {required}{(upTo ? " (up to)" : "")}"
        };
    }
}
