using Bot.Control;
using Bot.Navigation;
using Bot.State;

namespace Bot.Tasks.SubTasks;

public sealed class StepDirectionTask : SubTask
{
    private readonly Waypoint _waypoint;
    private readonly KeyMover _keyboard;

    private bool _requestedStep;
    private (int X, int Y, int Z) _startPos;
    private int _ticksWaiting;
    private const int MaxWaitTicks = 20;

    /// <summary>
    /// True while waiting for step confirmation - prevents preemption.
    /// </summary>
    public bool IsCritical => _requestedStep && !IsCompleted;

    public StepDirectionTask(Waypoint waypoint, KeyMover keyboard)
    {
        if (waypoint.Type != WaypointType.Step)
            throw new ArgumentException("StepDirectionTask requires a Step waypoint");

        _waypoint = waypoint;
        _keyboard = keyboard;
        Name = $"Step-{waypoint.Dir}";
    }

    protected override void OnStart(BotContext ctx)
    {
        _startPos = (ctx.PlayerPosition.X, ctx.PlayerPosition.Y, ctx.PlayerPosition.Z);
    }

    protected override void Execute(BotContext ctx)
    {
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

        _keyboard.StepDirection(_waypoint.Dir, ctx.GameWindowHandle);
        _requestedStep = true;
        Console.WriteLine($"[{Name}] Executed, waiting for Z change...");
    }

    protected override void OnFinish(BotContext ctx)
    {
        if (!Failed && _requestedStep)
            Console.WriteLine($"[{Name}] Success: Z changed from {_startPos.Z} to {ctx.PlayerPosition.Z}");
    }
}
