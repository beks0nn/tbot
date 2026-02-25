namespace Bot.Control.Actions;

/// <summary>
/// Left click at screen coordinates (not tile-based).
/// </summary>
public sealed class LeftClickScreenAction(MouseMover mouse, int x, int y) : InputAction
{
    public override TimeSpan EstimatedDuration => TimeSpan.FromMilliseconds(250);

    public override async Task RunAsync(CancellationToken ct)
    {
        await mouse.LeftClickSlowAsync(x, y, ct);
    }
}

/// <summary>
/// Right click at screen coordinates (not tile-based).
/// </summary>
public sealed class RightClickScreenAction(MouseMover mouse, int x, int y) : InputAction
{
    public override TimeSpan EstimatedDuration => TimeSpan.FromMilliseconds(350);

    public override async Task RunAsync(CancellationToken ct)
    {
        await mouse.RightClickSlowAsync(x, y, ct);
    }
}
