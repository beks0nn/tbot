using Bot.Navigation;

namespace Bot.Control.Actions;

public sealed class StepDirectionAction(KeyMover keyboard, Direction direction, IntPtr handle) : InputAction
{
    public override TimeSpan EstimatedDuration => TimeSpan.FromMilliseconds(75);

    public override async Task RunAsync(CancellationToken ct)
    {
        await keyboard.StepDirectionAsync(direction, handle, ct);
    }
}
