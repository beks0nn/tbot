using Bot.State;

namespace Bot.Control.Actions;

public sealed class LeftClickTileAction(MouseMover mouse, (int X, int Y) tile, ProfileSettings profile) : InputAction
{
    public override TimeSpan EstimatedDuration => TimeSpan.FromMilliseconds(250);

    public override async Task RunAsync(CancellationToken ct)
    {
        await mouse.LeftClickTileAsync(tile, profile, ct);
    }
}
