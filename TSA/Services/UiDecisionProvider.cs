using TSA.Core.Engine;

namespace TSA.Services;

/// <summary>
/// Bridges the engine to the UI: the engine awaits a decision, the UI shows
/// <see cref="Pending"/> and calls <see cref="Answer"/> when the player taps.
/// </summary>
public sealed class UiDecisionProvider : IDecisionProvider
{
    private TaskCompletionSource<object?>? _completion;

    public DecisionRequest? Pending { get; private set; }

    public event Action? Changed;

    public Task<object?> DecideAsync(DecisionRequest request, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        cancellationToken.Register(() => completion.TrySetCanceled());

        _completion = completion;
        Pending = request;
        Changed?.Invoke();

        return completion.Task;
    }

    public void Answer(object? value)
    {
        var completion = _completion;
        _completion = null;
        Pending = null;
        completion?.TrySetResult(value);
    }

    public void Cancel()
    {
        var completion = _completion;
        _completion = null;
        Pending = null;
        completion?.TrySetCanceled();
    }
}
