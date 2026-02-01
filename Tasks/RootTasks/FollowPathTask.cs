using Bot.Control;
using Bot.Navigation;
using Bot.State;
using Bot.Tasks.SubTasks;

namespace Bot.Tasks.RootTasks;

public sealed class FollowPathTask : BotTask
{
    public override int Priority => TaskPriority.FollowPath;

    private readonly PathRepository _repo;
    private readonly MouseMover _mouse;
    private readonly KeyMover _keyboard;

    private SubTask? _currentSubTask;
    private DateTime? _waitUntil;

    public TimeSpan TransitionDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    public FollowPathTask(PathRepository repo, KeyMover keyboard, MouseMover mouse)
    {
        _repo = repo;
        _keyboard = keyboard;
        _mouse = mouse;
        Name = "FollowPath";
    }

    protected override void OnStart(BotContext ctx)
    {
        StartNextSubTask(ctx);
    }

    protected override void Execute(BotContext ctx)
    {
        // Wait between subtasks
        if (_waitUntil != null)
        {
            if (DateTime.UtcNow < _waitUntil)
                return;
            _waitUntil = null;
        }

        // Start next subtask if none running
        if (_currentSubTask == null)
        {
            StartNextSubTask(ctx);
            return;
        }

        // Tick current subtask
        _currentSubTask.Tick(ctx);

        // Handle subtask completion
        if (_currentSubTask.IsCompleted)
        {
            Console.WriteLine($"[FollowPath] Subtask '{_currentSubTask.Name}' completed.");

            if (_currentSubTask.Failed)
            {
                Console.WriteLine("[FollowPath] Subtask failed, rewinding path.");
                _repo.GoBackOne();
            }
            else
            {
                if (!_repo.Advance())
                    _repo.Reset();
            }

            _currentSubTask = null;
            _waitUntil = DateTime.UtcNow + TransitionDelay;
        }
        // Note: This task never completes on its own - it runs until preempted
    }

    public override bool IsCritical
    {
        get
        {
            if (_currentSubTask == null)
                return false;

            // Check if subtask is critical (step/click waiting for Z change)
            return _currentSubTask switch
            {
                StepDirectionTask step => step.IsCritical,
                RightClickInTileTask click => click.IsCritical,
                UseItemOnTileTask use => use.IsCritical,
                _ => false
            };
        }
    }

    private void StartNextSubTask(BotContext ctx)
    {
        var wp = _repo.Current;
        if (wp == null)
        {
            Console.WriteLine("[FollowPath] End of path, restarting.");
            _repo.Reset();
            wp = _repo.Current;
            if (wp == null)
                return;
        }

        // Skip if already at waypoint
        if (wp.IsAt(ctx.PlayerPosition))
        {
            _repo.Advance();
            wp = _repo.Current;
            if (wp == null)
                return;
        }

        Console.WriteLine($"[FollowPath] Next: {wp}");

        _currentSubTask = wp.Type switch
        {
            WaypointType.Move => new WalkToCoordinateTask((wp.X, wp.Y, wp.Z), _keyboard),
            WaypointType.Step => new StepDirectionTask(wp, _keyboard),
            WaypointType.RightClick => new RightClickInTileTask(wp, _mouse),
            WaypointType.UseItem => new UseItemOnTileTask(wp, _mouse, _keyboard),
            _ => null
        };

        if (_currentSubTask == null)
            Console.WriteLine($"[FollowPath] Unsupported waypoint type: {wp.Type}");
    }
}
