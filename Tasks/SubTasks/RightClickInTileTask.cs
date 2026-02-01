using Bot.Control;
using Bot.Navigation;
using Bot.State;

namespace Bot.Tasks.SubTasks;

public sealed class RightClickInTileTask : SubTask
{
    private readonly Waypoint _wp;
    private readonly MouseMover _mouse;

    private bool _clicked;
    private (int X, int Y, int Z) _startPos;
    private int _ticksWaiting;
    private const int MaxWaitTicks = 20;

    /// <summary>
    /// True while waiting for Z change - prevents preemption.
    /// </summary>
    public bool IsCritical => _clicked && !IsCompleted;

    public RightClickInTileTask(Waypoint wp, MouseMover mouse)
    {
        _wp = wp;
        _mouse = mouse;
        Name = $"RightClickTile-{wp.Dir}";
    }

    protected override void OnStart(BotContext ctx)
    {
        _startPos = (ctx.PlayerPosition.X, ctx.PlayerPosition.Y, ctx.PlayerPosition.Z);
    }

    protected override void Execute(BotContext ctx)
    {
        if (_clicked)
        {
            if (ctx.PlayerPosition.Z != _startPos.Z)
            {
                Complete();
                return;
            }

            _ticksWaiting++;
            if (_ticksWaiting > MaxWaitTicks)
                Fail("Z did not change (timeout)");
            return;
        }

        // Require exact alignment
        if (ctx.PlayerPosition.X != _wp.X || ctx.PlayerPosition.Y != _wp.Y)
        {
            Fail($"Incorrect position, expected ({_wp.X},{_wp.Y})");
            return;
        }

        var slot = ComputeTileSlot(_wp, ctx);
        _mouse.RightClickTile(slot, ctx.Profile);
        _clicked = true;
        Console.WriteLine($"[{Name}] Clicked, waiting for Z change...");
    }

    protected override void OnFinish(BotContext ctx)
    {
        if (!Failed && _clicked)
            Console.WriteLine($"[{Name}] Success: Z changed from {_startPos.Z} to {ctx.PlayerPosition.Z}");
    }

    private static (int X, int Y) ComputeTileSlot(Waypoint wp, BotContext ctx)
    {
        int tx = wp.X;
        int ty = wp.Y;

        switch (wp.Dir)
        {
            case Direction.North: ty -= 1; break;
            case Direction.South: ty += 1; break;
            case Direction.East: tx += 1; break;
            case Direction.West: tx -= 1; break;
        }

        return (tx - ctx.PlayerPosition.X, ty - ctx.PlayerPosition.Y);
    }
}
