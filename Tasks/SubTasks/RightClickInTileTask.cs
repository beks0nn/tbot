using Bot.Control;
using Bot.Control.Actions;
using Bot.Navigation;
using Bot.State;

namespace Bot.Tasks.SubTasks;

public sealed class RightClickInTileTask : SubTask
{
    private readonly Waypoint _wp;
    private readonly InputQueue _queue;
    private readonly MouseMover _mouse;
    private readonly object _owner;

    private ActionHandle? _pending;
    private bool _clicked;
    private (int X, int Y, int Z) _startPos;
    private int _ticksWaiting;
    private const int MaxWaitTicks = 20;

    /// <summary>
    /// True once click is enqueued until completion - prevents preemption
    /// so the task can detect the Z change and advance the path.
    /// </summary>
    public bool IsCritical => (_pending != null || _clicked) && !IsCompleted;

    public RightClickInTileTask(Waypoint wp, InputQueue queue, MouseMover mouse, object owner)
    {
        _wp = wp;
        _queue = queue;
        _mouse = mouse;
        _owner = owner;
        Name = $"RightClickTile-{wp.Dir}";
    }

    protected override void OnStart(BotContext ctx)
    {
        _startPos = (ctx.PlayerPosition.X, ctx.PlayerPosition.Y, ctx.PlayerPosition.Z);
    }

    protected override void Execute(BotContext ctx)
    {
        if (_pending != null)
        {
            if (!_pending.IsCompleted) return;
            _pending = null;
            _clicked = true;
            Console.WriteLine($"[{Name}] Clicked, waiting for Z change...");
            return;
        }

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
        _pending = _queue.Enqueue(new RightClickTileAction(_mouse, slot, ctx.Profile), _owner);
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
