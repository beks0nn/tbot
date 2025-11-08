using Bot.Navigation;
using Bot.Control;

namespace Bot.Tasks;

public sealed class FollowPathTask : BotTask
{
    public override int Priority { get; set; } = 50;
    private readonly PathRepository _repo;
    private BotTask? _currentSubTask;

    public TimeSpan TransitionDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    public FollowPathTask(PathRepository repo)
    {
        _repo = repo;
        Name = "FollowPath";
    }

    public override void OnBeforeStart(BotContext ctx)
    {
        Console.WriteLine("[Task] Following path...");
        StartNextSubTask(ctx);
    }

    public override void Do(BotContext ctx)
    {
        // If no subtask, try to start one
        if (_currentSubTask == null)
        {
            StartNextSubTask(ctx);
            return;
        }

        // Advance the subtask
        _currentSubTask.Tick(ctx);

        // Subtask finished? Move on after small transition delay
        if (_currentSubTask.IsCompleted)
        {
            Console.WriteLine($"[Task] Subtask '{_currentSubTask.Name}' completed.");
            _currentSubTask = null;
            DelayAfterComplete = TransitionDelay;
        }
    }

    private void StartNextSubTask(BotContext ctx)
    {
        var wp = _repo.Current;
        bool adv;
        Console.WriteLine($"[Task] Next waypoint: {wp}");

        if (wp == null)
        {
            // reached end — restart path
            Console.WriteLine("[Task] Reached end of path — restarting from first waypoint.");
            _repo.Reset();
            wp = _repo.Current;
            if (wp == null) return; // no waypoints at all
        }

        if (wp.IsAt(ctx.PlayerPosition))
        {
            if (!_repo.Advance())
            {
                _repo.Reset();
            }
            return;
        }

        _currentSubTask = wp.Type switch
        {
            WaypointType.Move => new WalkToWaypointTask((wp.X, wp.Y, wp.Z)),
            WaypointType.Step => new StepDirectionTask(wp),
            _ => null
        };

        if (_currentSubTask == null)
        {
            Console.WriteLine($"[Path] Unsupported waypoint type: {wp.Type}");
            return;
        }

        adv = _repo.Advance();

        if (!adv) _repo.Reset();
    }


    public override bool Did(BotContext ctx)
    {
        // keep following path forever
        return false;
    }
}
