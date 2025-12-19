using Bot.Control;
using Bot.Navigation;
using Bot.State;

namespace Bot.Tasks.Implementations;

public sealed class RightClickInTileTask : BotTask
{
    public override int Priority => TaskPriority.SubTask;

    private readonly Waypoint _wp;
    private readonly IClientProfile _profile;
    private readonly MouseMover _mouse;

    public bool TaskFailed { get; private set; } = false;

    private bool _clicked = false;
    private (int X, int Y, int Z) _startPos;

    private const int MaxWaitTicks = 20;
    private int _ticksWaiting = 0;
    public override bool IsCritical => _clicked;


    public RightClickInTileTask(Waypoint wp, IClientProfile profile, MouseMover mouse)
    {
        _wp = wp;
        _profile = profile;
        _mouse = mouse;
        Name = $"RightClickTile-{wp.Dir}";
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

    public override void OnBeforeStart(BotContext ctx)
    {
        _startPos = (ctx.PlayerPosition.X, ctx.PlayerPosition.Y, ctx.PlayerPosition.Z);
        TaskFailed = false;

        Console.WriteLine($"[Task] RightClickTile-{_wp.Dir} from Z={_startPos.Z}");
    }

    public override void Do(BotContext ctx)
    {
        if (_clicked)
            return;

        // Require exact alignment before attempting ladder/hole use
        if (ctx.PlayerPosition.X != _wp.X || ctx.PlayerPosition.Y != _wp.Y)
        {
            Console.WriteLine($"[Task] RightClickTile aborted: incorrect position ({_wp.X},{_wp.Y})");
            TaskFailed = true;
            return;
        }

        var slot = ComputeTileSlot(_wp, ctx);
        _mouse.RightClickTile(slot, _profile);

        _clicked = true;
        Console.WriteLine($"[Task] RightClickTile-{_wp.Dir} clicked, waiting for Z change...");
    }

    public override bool Did(BotContext ctx)
    {
        if (TaskFailed)
        {
            return true;
        }

        if (!_clicked)
            return false;

        _ticksWaiting++;

        var currentZ = ctx.PlayerPosition.Z;

        // SUCCESS → Z changed (ladder down or drain hole)
        if (currentZ != _startPos.Z)
        {
            Console.WriteLine($"[Task] RightClickTile successful: Z changed {_startPos.Z} → {currentZ}");
            return true;
        }

        // FAIL → timeout
        if (_ticksWaiting > MaxWaitTicks)
        {
            Console.WriteLine("[Task] RightClickTile failed: Z did not change.");
            TaskFailed = true;
            return true;
        }

        return false;
    }
}
