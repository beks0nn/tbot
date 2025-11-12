using Bot.Control;
using Bot.Navigation;
using Bot.Tasks;

public sealed class StepDirectionTask : BotTask
{
    public override int Priority { get; set; } = 1;
    private readonly Waypoint _waypoint;
    private bool _hasStepped = false;
    private DateTime _readyAt = DateTime.MinValue;

    public TimeSpan StepDuration { get; init; } = TimeSpan.FromMilliseconds(500);

    public StepDirectionTask(Waypoint waypoint)
    {
        if (waypoint.Type != WaypointType.Step)
            throw new ArgumentException("StepDirectionTask requires a Step waypoint");

        _waypoint = waypoint;
        Name = $"Step-{waypoint.Dir}";
        DelayAfterComplete = TimeSpan.FromSeconds(1); // post-step cooldown
    }

    public override void OnBeforeStart(BotContext ctx)
    {
        Console.WriteLine($"[Task] Starting step in direction: {_waypoint.Dir}");
    }

    public override void Do(BotContext ctx)
    {
        if (_hasStepped) return;

        new KeyMover().StepDirection(_waypoint.Dir, ctx.GameWindowHandle);
        _readyAt = DateTime.UtcNow.Add(StepDuration);
        _hasStepped = true;

        Console.WriteLine($"[Task] Step-{_waypoint.Dir} executed, waiting {StepDuration.TotalMilliseconds} ms before completion...");
    }

    public override bool Did(BotContext ctx)
    {
        var done = _hasStepped && DateTime.UtcNow >= _readyAt;
        if (done)
            Console.WriteLine($"[Task] Step-{_waypoint.Dir} complete (ready for next waypoint).");
        return done;
    }
}
