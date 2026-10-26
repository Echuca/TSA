using System.Text.Json;

namespace TSA.Core.Engine;

/// <summary>
/// A checkpoint snapshot plus every answer given since it. The engine is deterministic
/// (dice are answers too), so state at any point = snapshot + replayed answers.
/// </summary>
public sealed class HistoryEntry
{
    public string State { get; set; } = string.Empty;

    /// <summary>Answers in order. JsonElement when loaded from storage; converted at replay.</summary>
    public List<object?> Answers { get; set; } = [];
}

/// <summary>
/// Undo history: one entry per checkpoint (start of each setup step, headline, action round
/// and end of turn). Undo goes back exactly one answer by restoring the latest snapshot and
/// replaying all but the last answer.
/// </summary>
public sealed class GameHistory
{
    private const int MaxEntries = 80;

    public List<HistoryEntry> Entries { get; private set; } = [];

    public HistoryEntry? Current => Entries.Count > 0 ? Entries[^1] : null;

    public bool CanUndo => Entries.Count > 1 || Current?.Answers.Count > 0;

    /// <summary>Raised after every checkpoint or recorded answer (time to save).</summary>
    public event Action? Changed;

    /// <summary>
    /// Called by the engine at each checkpoint. Re-reaching the same checkpoint after a
    /// restore does nothing, so its recorded answers are kept.
    /// </summary>
    public void Checkpoint(string state)
    {
        if (Current?.State == state)
        {
            return;
        }

        Entries.Add(new HistoryEntry { State = state });

        if (Entries.Count > MaxEntries)
        {
            Entries.RemoveAt(0);
        }

        Changed?.Invoke();
    }

    public void Record(object? answer)
    {
        Current?.Answers.Add(answer);
        Changed?.Invoke();
    }

    /// <summary>
    /// Undo one answer. Returns the snapshot to restore and the answers to replay on top of it.
    /// With no answers in the current step, steps back into the previous one.
    /// </summary>
    public (string State, List<object?> Replay) StepBack()
    {
        if (Current is null)
        {
            throw new InvalidOperationException("Nothing to undo.");
        }

        if (Current.Answers.Count == 0 && Entries.Count > 1)
        {
            Entries.RemoveAt(Entries.Count - 1);
        }

        var entry = Current;

        if (entry.Answers.Count > 0)
        {
            entry.Answers.RemoveAt(entry.Answers.Count - 1);
        }

        return TakeReplay(entry);
    }

    /// <summary>The latest snapshot and all its answers (to resume exactly where play stopped).</summary>
    public (string State, List<object?> Replay) Resume() => TakeReplay(Current ?? throw new InvalidOperationException("No saved game."));

    /// <summary>
    /// A manual correction replaces the current step: its partial answers are dropped and the
    /// corrected state becomes a new checkpoint (Undo removes the correction).
    /// </summary>
    public void AddCorrection(string state)
    {
        Current?.Answers.Clear();
        Entries.Add(new HistoryEntry { State = state });
        Changed?.Invoke();
    }

    public void Clear() => Entries = [];

    // Replayed answers are recorded again as the engine consumes them.
    private static (string, List<object?>) TakeReplay(HistoryEntry entry)
    {
        var replay = entry.Answers.ToList();
        entry.Answers.Clear();
        return (entry.State, replay);
    }

    public string Serialize(int maxEntries) =>
        JsonSerializer.Serialize(Entries.TakeLast(maxEntries).ToList());

    public static GameHistory Deserialize(string json)
    {
        var entries = JsonSerializer.Deserialize<List<HistoryEntry>>(json) ?? [];

        // Answers come back as JsonElement; normalise nulls so replay can tell them apart.
        foreach (var entry in entries)
        {
            entry.Answers = entry.Answers
                .Select(a => a is JsonElement { ValueKind: JsonValueKind.Null } ? null : a)
                .ToList();
        }

        return new GameHistory { Entries = entries };
    }
}

/// <summary>
/// Wraps the real decision provider: replays queued answers first, then asks the player,
/// recording every answer in the history.
/// </summary>
public sealed class RecordingDecisionProvider(IDecisionProvider inner, GameHistory history) : IDecisionProvider
{
    private readonly Queue<object?> _replay = new();

    public bool IsReplaying => _replay.Count > 0;

    public void Replay(IEnumerable<object?> answers)
    {
        _replay.Clear();

        foreach (var answer in answers)
        {
            _replay.Enqueue(answer);
        }
    }

    public async Task<object?> DecideAsync(DecisionRequest request, CancellationToken cancellationToken)
    {
        var answer = _replay.Count > 0
            ? ToAnswerType(_replay.Dequeue(), request)
            : await inner.DecideAsync(request, cancellationToken);

        history.Record(answer);
        return answer;
    }

    /// <summary>Converts an answer loaded from storage (JsonElement) to the type the request expects.</summary>
    public static object? ToAnswerType(object? answer, DecisionRequest request)
    {
        if (answer is not JsonElement json)
        {
            return answer;
        }

        if (json.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return request switch
        {
            OptionRequest or DieRequest => json.GetInt32(),
            CardRequest => (int?)json.GetInt32(),
            CountryRequest => json.GetString(),
            CountriesRequest => json.Deserialize<List<string>>(),
            InfluenceRequest => json.Deserialize<Dictionary<string, int>>(),
            _ => throw new InvalidOperationException($"Cannot replay an answer for {request.GetType().Name}.")
        };
    }
}
