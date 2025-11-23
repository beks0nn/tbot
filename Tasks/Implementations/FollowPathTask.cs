using Bot.Navigation;

namespace Bot.Tasks;

public sealed class FollowPathTask : BotTask
{
    public override int Priority { get; set; } = 50;

    private readonly PathRepository _repo;
    private readonly IClientProfile _profile;

    private BotTask? _currentSubTask;
    private DateTime? _waitUntil = null;

    public TimeSpan TransitionDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    public FollowPathTask(PathRepository repo, IClientProfile profile)
    {
        _repo = repo;
        _profile = profile;
        Name = "FollowPath";
    }

    public override void OnBeforeStart(BotContext ctx)
    {
        Console.WriteLine("[Task] FollowPath OnBeforeStart");
        StartNextSubTask(ctx);
    }

    public override void Do(BotContext ctx)
    {
        // If we are between subtasks, wait until the delay expires
        if (_waitUntil != null)
        {
            if (DateTime.UtcNow < _waitUntil)
                return;

            _waitUntil = null;
        }

        // If no subtask is running → start next one
        if (_currentSubTask == null)
        {
            StartNextSubTask(ctx);
            return;
        }

        // Tick the running subtask
        _currentSubTask.Tick(ctx);

        // Subtask finished → clear it and apply cooldown
        if (_currentSubTask.IsCompleted)
        {
            Console.WriteLine($"[Task] Subtask '{_currentSubTask.Name}' completed.");
            // Check for failure in special task types
            if (_currentSubTask is StepDirectionTask stepTask && stepTask.StepFailed)
            {
                Console.WriteLine("[FollowPath] Step task failed. Rewinding path index.");
                _repo.GoBackOne(); // Move back to previous waypoint
            }
            else if (_currentSubTask is RightClickInTileTask t && t.TaskFailed)
            {
                _repo.GoBackOne();
            }
            else
            {
                // Success → advance waypoint
                if (!_repo.Advance())
                    _repo.Reset();
            }

            // Clear subtask and apply spacing
            _currentSubTask = null;
            _waitUntil = DateTime.UtcNow + TransitionDelay;
        }
    }

    private void StartNextSubTask(BotContext ctx)
    {
        var wp = _repo.Current;
        if (wp == null)
        {
            Console.WriteLine("[Task] End of path — restarting.");
            _repo.Reset();
            wp = _repo.Current;
            if (wp == null) return;
        }

        // If already standing at the waypoint → skip it
        if (wp.IsAt(ctx.PlayerPosition))
        {
            _repo.Advance();
            wp = _repo.Current;
            if (wp == null) return;
        }

        Console.WriteLine($"[Task] Next waypoint: {wp}");

        _currentSubTask = wp.Type switch
        {
            WaypointType.Move => new WalkToWaypointTask((wp.X, wp.Y, wp.Z)),
            WaypointType.Step => new StepDirectionTask(wp),
            WaypointType.RightClick => new RightClickInTileTask(wp, _profile),
            _ => null
        };

        if (_currentSubTask == null)
        {
            Console.WriteLine($"[Path] Unsupported waypoint type: {wp.Type}");
            return;
        }

        //// Advance path pointer for next iteration
        //if (!_repo.Advance())
        //    _repo.Reset();
    }

    public override bool Did(BotContext ctx)
    {
        // This task persists until pre-empted by higher priority tasks
        return false;
    }
}
