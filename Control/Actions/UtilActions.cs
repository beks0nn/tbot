namespace Bot.Control.Actions;

/// <summary>
/// Simple delay action for spacing out queued actions.
/// </summary>
public sealed class DelayAction(TimeSpan duration) : InputAction
{
    public override TimeSpan EstimatedDuration => duration;

    public override async Task RunAsync(CancellationToken ct)
    {
        await Task.Delay(duration, ct);
    }
}

/// <summary>
/// Runs a callback after preceding queued actions complete.
/// </summary>
public sealed class CallbackAction(Action callback) : InputAction
{
    public override TimeSpan EstimatedDuration => TimeSpan.Zero;

    public override Task RunAsync(CancellationToken ct)
    {
        callback();
        return Task.CompletedTask;
    }
}
