using Microsoft.JSInterop;
using TSA.Core.Engine;
using TSA.Core.Models;

namespace TSA.Services;

/// <summary>
/// The running game: owns the state, runs the engine, records every answer for undo and
/// saves to browser storage after each one.
/// </summary>
public sealed class GameSession(IJSRuntime js)
{
    private const string CurrentKey = "tsa.current";
    private const string HistoryKey = "tsa.history.v2";
    private const int SavedEntries = 15;

    private readonly UiDecisionProvider _ui = new();
    private GameHistory _history = new();
    private CancellationTokenSource? _cancellation;
    private Task? _running;
    private bool _initialized;
    private bool _saving;
    private bool _saveAgain;

    public GameState? State { get; private set; }

    public DecisionRequest? Pending => _ui.Pending;

    public string? Error { get; private set; }

    public bool HasSavedGame { get; private set; }

    public bool CanUndo => _history.CanUndo;

    /// <summary>Raised whenever the UI should re-render.</summary>
    public event Action? Changed;

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _ui.Changed += () => Changed?.Invoke();
        HasSavedGame = !string.IsNullOrEmpty(await GetItem(CurrentKey));
    }

    public async Task NewGameAsync(bool optionalCards, int usBonusInfluence)
    {
        await StopAsync();
        UseHistory(new GameHistory());
        Start(GameRunner.CreateGame(optionalCards, usBonusInfluence), []);
    }

    public async Task ResumeAsync()
    {
        await StopAsync();

        try
        {
            var saved = await GetItem(HistoryKey);

            if (!string.IsNullOrEmpty(saved))
            {
                UseHistory(GameHistory.Deserialize(saved));
            }
            else
            {
                // Saves from before answer-level undo: just the last checkpoint.
                var current = await GetItem(CurrentKey) ?? throw new InvalidOperationException("No saved game.");
                UseHistory(new GameHistory());
                _history.Checkpoint(current);
            }

            var (state, replay) = _history.Resume();
            Start(GameState.Deserialize(state), replay);
        }
        catch (Exception ex)
        {
            Error = $"Could not load the saved game: {ex.Message}";
            Changed?.Invoke();
        }
    }

    public void Answer(object? value)
    {
        _ui.Answer(value);
        Changed?.Invoke();
    }

    /// <summary>Undo the last answer (stepping back into the previous step if needed).</summary>
    public async Task UndoAsync()
    {
        if (!_history.CanUndo)
        {
            return;
        }

        await StopAsync();
        var (state, replay) = _history.StepBack();
        Start(GameState.Deserialize(state), replay);
    }

    /// <summary>
    /// A copy of the state at the start of the current step, for the Corrections panel to edit.
    /// </summary>
    public GameState? CorrectionDraft() =>
        _history.Current is { } entry ? GameState.Deserialize(entry.State) : State?.Clone();

    /// <summary>
    /// Replaces the state with a manually corrected one. Undo removes the correction.
    /// </summary>
    public async Task ApplyCorrectionAsync(GameState corrected, string description)
    {
        await StopAsync();
        corrected.AddLog(null, $"Correction: {description}");
        var json = corrected.Serialize();
        _history.AddCorrection(json);
        Start(GameState.Deserialize(json), []);
    }

    private void UseHistory(GameHistory history)
    {
        _history = history;
        _history.Changed += () => _ = SaveAsync();
    }

    private void Start(GameState state, List<object?> replay)
    {
        State = state;
        Error = null;
        _cancellation = new CancellationTokenSource();

        var provider = new RecordingDecisionProvider(_ui, _history);
        provider.Replay(replay);

        var runner = new GameRunner(State, provider, CheckpointAsync, _cancellation.Token);
        _running = RunAsync(runner);
        Changed?.Invoke();
    }

    private async Task RunAsync(GameRunner runner)
    {
        try
        {
            await runner.RunAsync();
        }
        catch (OperationCanceledException)
        {
            // Undo, correction or new game.
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }

        Changed?.Invoke();
    }

    private async Task StopAsync()
    {
        _cancellation?.Cancel();
        _ui.Cancel();

        if (_running is not null)
        {
            await _running;
        }

        _running = null;
        _cancellation = null;
    }

    private Task CheckpointAsync()
    {
        _history.Checkpoint(State!.Serialize());
        return Task.CompletedTask;
    }

    /// <summary>Saves the history; coalesces bursts (e.g. replay) into one write at a time.</summary>
    private async Task SaveAsync()
    {
        if (_saving)
        {
            _saveAgain = true;
            return;
        }

        _saving = true;

        try
        {
            do
            {
                _saveAgain = false;

                if (_history.Current is { } current)
                {
                    await SetItem(CurrentKey, current.State);
                    await SetItem(HistoryKey, _history.Serialize(SavedEntries));
                    HasSavedGame = true;
                }
            }
            while (_saveAgain);
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task<string?> GetItem(string key)
    {
        try
        {
            return await js.InvokeAsync<string?>("localStorage.getItem", key);
        }
        catch (JSException)
        {
            return null;
        }
    }

    private async Task SetItem(string key, string value)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", key, value);
        }
        catch (JSException)
        {
            // Storage full or unavailable (private browsing): the game still runs, it just isn't saved.
        }
    }
}
