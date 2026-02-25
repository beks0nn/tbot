using Bot.State;

namespace Bot.Control.Actions;

public sealed class CtrlDragAction : InputAction
{
    private readonly MouseMover _mouse;
    private readonly int _fromX, _fromY, _toX, _toY;

    public override TimeSpan EstimatedDuration => TimeSpan.FromMilliseconds(500);

    public CtrlDragAction(MouseMover mouse, int fromX, int fromY, int toX, int toY)
    {
        _mouse = mouse;
        _fromX = fromX;
        _fromY = fromY;
        _toX = toX;
        _toY = toY;
    }

    public static CtrlDragAction FromTiles(
        MouseMover mouse,
        (int X, int Y) fromTile,
        (int X, int Y) toTile,
        ProfileSettings profile)
    {
        var (fx, fy) = MouseMover.TileToScreenPixel(fromTile, profile);
        var (tx, ty) = MouseMover.TileToScreenPixel(toTile, profile);
        return new CtrlDragAction(mouse, fx, fy, tx, ty);
    }

    public override async Task RunAsync(CancellationToken ct)
    {
        await _mouse.CtrlDragLeftAsync(_fromX, _fromY, _toX, _toY, ct);
    }
}
