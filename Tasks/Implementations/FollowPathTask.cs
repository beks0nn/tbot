using Bot.Control;
using Bot.Navigation;
using Bot.State;

namespace Bot.Tasks.Implementations;

public sealed class FollowPathTask : BotTask
{
    public override int Priority => TaskPriority.FollowPath;

    private readonly PathRepository _repo;
    private readonly MouseMover _mouse;
    private readonly KeyMover _keyboard;

    private BotTask? _currentSubTask;
    private DateTime? _waitUntil = null;

    public TimeSpan TransitionDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    public FollowPathTask(PathRepository repo, KeyMover keyboard, MouseMover mouse)
    {
        _repo = repo;
        _keyboard = keyboard;
        _mouse = mouse;
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
            else if (_currentSubTask is UseItemOnTileTask uit && uit.TaskFailed)
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
            WaypointType.Move => new WalkToCoordinateTask((wp.X, wp.Y, wp.Z), _keyboard),
            WaypointType.Step => new StepDirectionTask(wp, _keyboard),
            WaypointType.RightClick => new RightClickInTileTask(wp, _mouse),
            WaypointType.UseItem => new UseItemOnTileTask(wp, _mouse, _keyboard),
            _ => null
        };

        if (_currentSubTask == null)
        {
            Console.WriteLine($"[Path] Unsupported waypoint type: {wp.Type}");
            return;
        }

    }

    public override bool Did(BotContext ctx)
    {
        // This task persists until pre-empted by higher priority tasks
        return false;
    }

    public override bool IsCritical
    {
        get
        {
            if (_currentSubTask != null && _currentSubTask.IsCritical)
                return true;

            return false;
        }
    }
}
