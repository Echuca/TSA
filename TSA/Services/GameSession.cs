using System.Text.Json;
using Microsoft.JSInterop;
using TSA.Core.Engine;
using TSA.Core.Models;

namespace TSA.Services;

/// <summary>
/// The running game: owns the state, runs the engine, keeps undo snapshots and
/// saves to browser storage at every checkpoint.
/// </summary>
public sealed class GameSession(IJSRuntime js)
{
    private const string CurrentKey = "tsa.current";
    private const string HistoryKey = "tsa.history";
    private const int MaxSnapshots = 80;
    private const int SavedSnapshots = 25;

    private readonly UiDecisionProvider _provider = new();
    private readonly List<string> _snapshots = [];
    private CancellationTokenSource? _cancellation;
    private Task? _running;
    private int _answersSinceCheckpoint;
    private bool _initialized;

    public GameState? State { get; private set; }

    public DecisionRequest? Pending => _provider.Pending;

    public string? Error { get; private set; }

    public bool HasSavedGame { get; private set; }

    public bool CanUndo => _snapshots.Count > 1 || _answersSinceCheckpoint > 0 || Error is not null;

    /// <summary>Raised whenever the UI should re-render.</summary>
    public event Action? Changed;

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        _provider.Changed += () => Changed?.Invoke();
        HasSavedGame = !string.IsNullOrEmpty(await GetItem(CurrentKey));
    }

    public async Task NewGameAsync(bool optionalCards, int usBonusInfluence)
    {
        await StopAsync();
        _snapshots.Clear();
        State = GameRunner.CreateGame(optionalCards, usBonusInfluence);
        Start();
    }

    public async Task ResumeAsync()
    {
        var json = await GetItem(CurrentKey);

        if (string.IsNullOrEmpty(json))
        {
            return;
        }

        await StopAsync();
        _snapshots.Clear();

        try
        {
            var history = await GetItem(HistoryKey);

            if (!string.IsNullOrEmpty(history))
            {
                _snapshots.AddRange(JsonSerializer.Deserialize<List<string>>(history) ?? []);
            }

            State = GameState.Deserialize(json);
        }
        catch (Exception ex)
        {
            Error = $"Could not load the saved game: {ex.Message}";
            Changed?.Invoke();
            return;
        }

        Start();
    }

    public void Answer(object? value)
    {
        _answersSinceCheckpoint++;
        _provider.Answer(value);
        Changed?.Invoke();
    }

    /// <summary>
    /// Undo: restart the current step if anything was entered in it, otherwise go back one step.
    /// </summary>
    public async Task UndoAsync()
    {
        if (_snapshots.Count == 0)
        {
            return;
        }

        if (_answersSinceCheckpoint == 0 && Error is null && _snapshots.Count > 1)
        {
            _snapshots.RemoveAt(_snapshots.Count - 1);
        }

        await RestartFromAsync(_snapshots[^1]);
    }

    /// <summary>
    /// A copy of the state at the start of the current step, for the Corrections panel to edit.
    /// </summary>
    public GameState? CorrectionDraft() =>
        _snapshots.Count > 0 ? GameState.Deserialize(_snapshots[^1]) : State?.Clone();

    /// <summary>
    /// Replaces the state with a manually corrected one. Undo removes the correction.
    /// </summary>
    public async Task ApplyCorrectionAsync(GameState corrected, string description)
    {
        corrected.AddLog(null, $"Correction: {description}");
        var json = corrected.Serialize();
        _snapshots.Add(json);
        await RestartFromAsync(json);
    }

    private async Task RestartFromAsync(string json)
    {
        await StopAsync();
        State = GameState.Deserialize(json);
        Start();
    }

    private void Start()
    {
        Error = null;
        _answersSinceCheckpoint = 0;
        _cancellation = new CancellationTokenSource();

        var runner = new GameRunner(State!, _provider, CheckpointAsync, _cancellation.Token);
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
            // Undo or new game.
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
        _provider.Cancel();

        if (_running is not null)
        {
            await _running;
        }

        _running = null;
        _cancellation = null;
    }

    private async Task CheckpointAsync()
    {
        var json = State!.Serialize();

        if (_snapshots.Count == 0 || _snapshots[^1] != json)
        {
            _snapshots.Add(json);

            if (_snapshots.Count > MaxSnapshots)
            {
                _snapshots.RemoveAt(0);
            }
        }

        _answersSinceCheckpoint = 0;

        await SetItem(CurrentKey, json);
        await SetItem(HistoryKey, JsonSerializer.Serialize(_snapshots.TakeLast(SavedSnapshots)));
        HasSavedGame = true;
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
