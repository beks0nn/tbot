using Bot.Control;
using Bot.Control.Actions;
using Bot.Navigation;
using Bot.State;

namespace Bot.Tasks.SubTasks;

public sealed class StepDirectionTask : SubTask
{
    private readonly Waypoint _waypoint;
    private readonly InputQueue _queue;
    private readonly KeyMover _keyboard;
    private readonly object _owner;

    private ActionHandle? _pending;
    private bool _requestedStep;
    private (int X, int Y, int Z) _startPos;
    private int _ticksWaiting;
    private const int MaxWaitTicks = 20;

    /// <summary>
    /// True once step is enqueued until completion - prevents preemption
    /// so the task can detect the Z change and advance the path.
    /// </summary>
    public bool IsCritical => (_pending != null || _requestedStep) && !IsCompleted;

    public StepDirectionTask(Waypoint waypoint, InputQueue queue, KeyMover keyboard, object owner)
    {
        if (waypoint.Type != WaypointType.Step)
            throw new ArgumentException("StepDirectionTask requires a Step waypoint");

        _waypoint = waypoint;
        _queue = queue;
        _keyboard = keyboard;
        _owner = owner;
        Name = $"Step-{waypoint.Dir}";
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
            _requestedStep = true;
            Console.WriteLine($"[{Name}] Executed, waiting for Z change...");
            return;
        }

        // Already requested? Wait for Z change
        if (_requestedStep)
        {
            if (ctx.PlayerPosition.Z != _startPos.Z)
            {
                Complete();
                return;
            }

            _ticksWaiting++;
            if (_ticksWaiting > MaxWaitTicks)
                Fail($"Z did not change after step (timeout)");
            return;
        }

        // Must be exactly at waypoint position before stepping
        if (ctx.PlayerPosition.X != _waypoint.X || ctx.PlayerPosition.Y != _waypoint.Y)
        {
            Fail($"Not at required position ({_waypoint.X},{_waypoint.Y})");
            return;
        }

        _pending = _queue.Enqueue(
            new StepDirectionAction(_keyboard, _waypoint.Dir, ctx.GameWindowHandle), _owner);
    }

    protected override void OnFinish(BotContext ctx)
    {
        if (!Failed && _requestedStep)
            Console.WriteLine($"[{Name}] Success: Z changed from {_startPos.Z} to {ctx.PlayerPosition.Z}");
    }
}
