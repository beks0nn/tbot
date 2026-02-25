using Bot.State;

namespace Bot.Control.Actions;

public sealed class RightClickTileAction(MouseMover mouse, (int X, int Y) tile, ProfileSettings profile) : InputAction
{
    public override TimeSpan EstimatedDuration => TimeSpan.FromMilliseconds(350);

    public override async Task RunAsync(CancellationToken ct)
    {
        await mouse.RightClickTileAsync(tile, profile, ct);
    }
}
