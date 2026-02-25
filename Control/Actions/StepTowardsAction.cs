namespace Bot.Control.Actions;

public sealed class StepTowardsAction(
    KeyMover keyboard, (int x, int y) from, (int x, int y) to, IntPtr handle) : InputAction
{
    public override TimeSpan EstimatedDuration => TimeSpan.FromMilliseconds(75);

    public override async Task RunAsync(CancellationToken ct)
    {
        await keyboard.StepTowardsAsync(from, to, handle, ct);
    }
}
