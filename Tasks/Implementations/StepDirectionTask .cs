using Bot.Control;
using Bot.Navigation;
using Bot.Tasks;

public sealed class StepDirectionTask : BotTask
{
    public override int Priority { get; set; } = 1;
    private readonly Waypoint _waypoint;

    private bool _requestedStep = false;
    private (int X, int Y, int Z) _startPos;
    public bool StepFailed { get; private set; } = false;

    private const int MaxWaitTicks = 20;
    private int _ticksWaiting = 0;

    public TimeSpan StepCooldown { get; init; } = TimeSpan.FromMilliseconds(500);

    public override bool IsCritical => _requestedStep;

    public StepDirectionTask(Waypoint waypoint)
    {
        if (waypoint.Type != WaypointType.Step)
            throw new ArgumentException("StepDirectionTask requires a Step waypoint");

        _waypoint = waypoint;
        Name = $"Step-{waypoint.Dir}";
    }

    public override void OnBeforeStart(BotContext ctx)
    {
        _startPos = (ctx.PlayerPosition.X, ctx.PlayerPosition.Y, ctx.PlayerPosition.Floor);

        Console.WriteLine($"[Task] Starting Step-{_waypoint.Dir} from ({_startPos.X},{_startPos.Y},{_startPos.Z})");
    }

    public override void Do(BotContext ctx)
    {
        // Already requested a step? Wait for Z change.
        if (_requestedStep)
            return;

        // Make sure player is EXACTLY at waypoint X,Y before stepping
        if (ctx.PlayerPosition.X != _waypoint.X || ctx.PlayerPosition.Y != _waypoint.Y)
        {
            // Not in exact position → DO NOTHING (fail safe)
            Console.WriteLine($"[Task] Step-{_waypoint.Dir} aborted: not at required ({_waypoint.X},{_waypoint.Y}).");
            StepFailed = true;
            return;
        }

        // Send the stepping key
        new KeyMover().StepDirection(_waypoint.Dir, ctx.GameWindowHandle);
        _requestedStep = true;

        Console.WriteLine($"[Task] Step-{_waypoint.Dir} executed, now waiting for Z change...");
    }

    public override bool Did(BotContext ctx)
    {
        if (!_requestedStep)
            return false;

        _ticksWaiting++;

        var currentZ = ctx.PlayerPosition.Floor;

        // Z changed → success
        if (currentZ != _startPos.Z)
        {
            Console.WriteLine($"[Task] Step-{_waypoint.Dir} successful: Z changed from {_startPos.Z} → {currentZ}");
            return true;
        }

        // Timeout → failure
        if (_ticksWaiting > MaxWaitTicks)
        {
            Console.WriteLine($"[Task] Step-{_waypoint.Dir} failed: Z did not change after step.");
            StepFailed = true;
            return true; // complete, but caller will notice no Z change
        }

        return false;
    }
}