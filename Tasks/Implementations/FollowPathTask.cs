using Bot.Navigation;
using Bot.Control;

namespace Bot.Tasks;

public sealed class FollowPathTask : BotTask
{
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
        _repo.Reset();
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
        if (wp == null)
        {
            Console.WriteLine("[Task] No more waypoints — path complete.");
            Status = TaskStatus.Completed;
            return;
        }

        // Skip if we're already at this waypoint
        if (wp.IsAt(ctx.PlayerPosition))
        {
            Console.WriteLine($"[Path] Skipping waypoint — already at ({wp.X},{wp.Y},{wp.Z}).");
            if (!_repo.Advance())
            {
                Status = TaskStatus.Completed;
                return;
            }

            StartNextSubTask(ctx);
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
            Console.WriteLine($"[Path] ⚠️ Unsupported waypoint type: {wp.Type}");
            Status = TaskStatus.Completed;
            return;
        }

        _repo.Advance();
    }

    public override bool Did(BotContext ctx)
    {
        // Done when there are no remaining waypoints or subtasks
        return _repo.Current == null && _currentSubTask == null;
    }
}
