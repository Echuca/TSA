using TSA.Core.Engine;

namespace TSA.Tests;

/// <summary>
/// Answers engine decisions from a queue. Each entry may be a value or a
/// function of the request. Throws <see cref="ScriptExhaustedException"/> when the
/// queue is empty so a test can run the engine "until it needs more input".
/// </summary>
public sealed class ScriptedDecisions : IDecisionProvider
{
    private readonly Queue<object?> _answers = new();

    public List<DecisionRequest> Asked { get; } = [];

    public ScriptedDecisions Then(object? answer)
    {
        _answers.Enqueue(answer);
        return this;
    }

    public ScriptedDecisions Then(Func<DecisionRequest, object?> answer)
    {
        _answers.Enqueue(answer);
        return this;
    }

    /// <summary>Answers an option request by matching the start of an option label.</summary>
    public ScriptedDecisions Option(string labelStart) => Then(r =>
    {
        var options = ((OptionRequest)r).Options;
        var index = options.ToList().FindIndex(o => o.Label.StartsWith(labelStart, StringComparison.OrdinalIgnoreCase));

        if (index < 0)
        {
            throw new InvalidOperationException($"No option starting '{labelStart}' in: {string.Join(" | ", options.Select(o => o.Label))}");
        }

        if (!options[index].Enabled)
        {
            throw new InvalidOperationException($"Option '{options[index].Label}' is disabled: {options[index].DisabledReason}");
        }

        return index;
    });

    public ScriptedDecisions Influence(params (string Country, int Amount)[] allocation) => Then(r =>
    {
        var request = (InfluenceRequest)r;
        var answer = allocation.ToDictionary(a => a.Country, a => a.Amount);

        foreach (var id in answer.Keys.Where(id => !request.Eligible.Contains(id)))
        {
            throw new InvalidOperationException($"{id} is not eligible for: {request.Prompt}");
        }

        if (request.Validate(answer) is { } error)
        {
            throw new InvalidOperationException($"Invalid allocation for '{request.Prompt}': {error}");
        }

        return answer;
    });

    public ScriptedDecisions Country(string id) => Then(r =>
    {
        var request = (CountryRequest)r;

        if (!request.Eligible.Contains(id))
        {
            throw new InvalidOperationException($"{id} is not eligible for: {request.Prompt}");
        }

        return id;
    });

    public ScriptedDecisions Card(int? id) => Then(r =>
    {
        var request = (CardRequest)r;

        if (id is { } value && !request.Eligible.Contains(value))
        {
            throw new InvalidOperationException($"Card {value} is not eligible for: {request.Prompt}");
        }

        return id;
    });

    public ScriptedDecisions Die(int roll) => Then(r =>
    {
        Assert.IsType<DieRequest>(r);
        return roll;
    });

    public Task<object?> DecideAsync(DecisionRequest request, CancellationToken cancellationToken)
    {
        Asked.Add(request);

        if (_answers.Count == 0)
        {
            throw new ScriptExhaustedException(request);
        }

        var answer = _answers.Dequeue();
        return Task.FromResult(answer is Func<DecisionRequest, object?> f ? f(request) : answer);
    }
}

public sealed class ScriptExhaustedException(DecisionRequest request) : Exception($"No scripted answer for: {request.Prompt}")
{
    public DecisionRequest Request { get; } = request;
}
