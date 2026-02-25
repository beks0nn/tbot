namespace Bot.Control;

public abstract class InputAction
{
    public abstract TimeSpan EstimatedDuration { get; }
    public abstract Task RunAsync(CancellationToken ct);
}
