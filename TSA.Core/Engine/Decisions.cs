using TSA.Core.Enums;
using TSA.Core.Models;

namespace TSA.Core.Engine;

/// <summary>
/// A question the engine needs a player to answer (choose a card, enter a die roll, ...).
/// The UI renders one request at a time; tests answer them from a script.
/// </summary>
public abstract class DecisionRequest
{
    /// <summary>The player who makes this decision, if any.</summary>
    public Side? Actor { get; init; }

    public string Prompt { get; init; } = string.Empty;

    /// <summary>Extra guidance such as the card text being resolved.</summary>
    public string? Detail { get; init; }
}

/// <summary>Answer: the index of the chosen option (int).</summary>
public sealed class OptionRequest : DecisionRequest
{
    public IReadOnlyList<DecisionOption> Options { get; init; } = [];
}

public sealed record DecisionOption(string Label, string? DisabledReason = null)
{
    public bool Enabled => DisabledReason is null;
}

/// <summary>Answer: a country id, or null when <see cref="NoneLabel"/> is chosen.</summary>
public sealed class CountryRequest : DecisionRequest
{
    public IReadOnlyList<string> Eligible { get; init; } = [];

    /// <summary>Why other countries cannot be chosen (shown to the player).</summary>
    public IReadOnlyDictionary<string, string> Reasons { get; init; } = new Dictionary<string, string>();

    public string? NoneLabel { get; init; }
}

/// <summary>Answer: a list of country ids (IReadOnlyList&lt;string&gt;).</summary>
public sealed class CountriesRequest : DecisionRequest
{
    public IReadOnlyList<string> Eligible { get; init; } = [];

    public int Min { get; init; }

    public int Max { get; init; }
}

/// <summary>Answer: a card id, or null when <see cref="NoneLabel"/> is chosen.</summary>
public sealed class CardRequest : DecisionRequest
{
    public IReadOnlyList<int> Eligible { get; init; } = [];

    public string? NoneLabel { get; init; }
}

/// <summary>Answer: a die result 1-6 (int).</summary>
public sealed class DieRequest : DecisionRequest
{
    /// <summary>What a successful roll looks like, e.g. "Success on 4-6".</summary>
    public string? Target { get; init; }
}

/// <summary>
/// Add or remove influence across several countries.
/// Answer: Dictionary&lt;string, int&gt; of country id to markers added/removed.
/// </summary>
public sealed class InfluenceRequest : DecisionRequest
{
    /// <summary>Whose influence markers are being changed.</summary>
    public Side InfluenceSide { get; init; }

    public bool Remove { get; init; }

    public IReadOnlyList<string> Eligible { get; init; } = [];

    public IReadOnlyDictionary<string, string> Reasons { get; init; } = new Dictionary<string, string>();

    public int? MaxPerCountry { get; init; }

    /// <summary>Returns an error for an invalid allocation, or null if it can be confirmed.</summary>
    public Func<IReadOnlyDictionary<string, int>, string?> Validate { get; init; } = _ => null;

    /// <summary>Progress text, e.g. "Ops used 2 / 3".</summary>
    public Func<IReadOnlyDictionary<string, int>, string> Status { get; init; } = _ => string.Empty;

    public int MaxFor(Country country)
    {
        var max = MaxPerCountry ?? 99;
        return Remove ? Math.Min(max, country.Influence(InfluenceSide)) : max;
    }
}

public interface IDecisionProvider
{
    Task<object?> DecideAsync(DecisionRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Typed helpers over <see cref="IDecisionProvider"/> used by the engine and events.
/// </summary>
public sealed class Decisions(IDecisionProvider provider, CancellationToken cancellationToken)
{
    private async Task<T> Ask<T>(DecisionRequest request)
    {
        var answer = await provider.DecideAsync(request, cancellationToken);
        return (T)answer!;
    }

    public async Task<int> Option(OptionRequest request)
    {
        var index = await Ask<int>(request);

        if (index < 0 || index >= request.Options.Count || !request.Options[index].Enabled)
        {
            throw new InvalidOperationException($"Option {index} is not available for: {request.Prompt}");
        }

        return index;
    }

    public Task<int> Option(Side? actor, string prompt, params string[] options) =>
        Option(new OptionRequest
        {
            Actor = actor,
            Prompt = prompt,
            Options = options.Select(o => new DecisionOption(o)).ToList()
        });

    public Task<int> Option(Side? actor, string prompt, IReadOnlyList<DecisionOption> options, string? detail = null) =>
        Option(new OptionRequest { Actor = actor, Prompt = prompt, Options = options, Detail = detail });

    public async Task<bool> YesNo(Side? actor, string prompt, string yes = "Yes", string no = "No") =>
        await Option(actor, prompt, yes, no) == 0;

    public Task<string?> Country(CountryRequest request) => Ask<string?>(request);

    public async Task<IReadOnlyList<string>> Countries(CountriesRequest request)
    {
        if (request.Max == 0)
        {
            return [];
        }

        return await Ask<IReadOnlyList<string>>(request);
    }

    public Task<int?> Card(CardRequest request) => Ask<int?>(request);

    public async Task<int> Die(Side? actor, string prompt, string? target = null)
    {
        var roll = await Ask<int>(new DieRequest { Actor = actor, Prompt = prompt, Target = target });

        if (roll is < 1 or > 6)
        {
            throw new InvalidOperationException($"Invalid die roll {roll}.");
        }

        return roll;
    }

    public async Task<Dictionary<string, int>> Influence(InfluenceRequest request)
    {
        if (request.Eligible.Count == 0)
        {
            return [];
        }

        return await Ask<Dictionary<string, int>>(request);
    }
}
