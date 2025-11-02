using Bot.Control;
using Bot.Navigation;

namespace Bot.Tasks;

public sealed class WalkToWaypointTask : BotTask
{
    public override int Priority { get; set; } = 1;
    private readonly (int x, int y, int z) _target;
    private readonly AStar _astar = new();
    private readonly KeyMover _mover = new();

    private DateTime _nextAllowedStep = DateTime.MinValue;
    private bool _startedMoving = false;
    private bool _reached = false;
    private bool _loggedCompletion = false;

    public TimeSpan StepInterval { get; init; } = TimeSpan.FromMilliseconds(120);

    public WalkToWaypointTask((int x, int y, int z) target)
    {
        _target = target;
        Name = $"WalkToWaypoint ({target.x},{target.y},{target.z})";
    }

    public override void OnBeforeStart(BotContext ctx)
    {
        Console.WriteLine($"[Task] Starting walk to ({_target.x},{_target.y},{_target.z})");
    }

    public override void Do(BotContext ctx)
    {
        if (_reached)
            return;

        if (DateTime.UtcNow < _nextAllowedStep)
            return;

        var player = (ctx.PlayerPosition.X, ctx.PlayerPosition.Y, ctx.PlayerPosition.Floor);

        if (player == _target)
        {
            _reached = true;
            return;
        }

        var floor = ctx.CurrentFloor;
        if (floor?.Walkable == null)
            return;

        var path = _astar.FindPath(floor.Walkable, (player.X, player.Y), (_target.x, _target.y));
        if (path.Count > 1)
        {
            _mover.StepTowards((player.X, player.Y), path[1]);
            _startedMoving = true;
        }

        _nextAllowedStep = DateTime.UtcNow.Add(StepInterval);
    }

    public override bool Did(BotContext ctx)
    {
        var player = (ctx.PlayerPosition.X, ctx.PlayerPosition.Y, ctx.PlayerPosition.Floor);
        bool done = _startedMoving && _reached && player == _target;

        if (done && !_loggedCompletion)
        {
            _loggedCompletion = true;
            Console.WriteLine($"[Task] WalkToWaypoint completed at ({_target.x},{_target.y},{_target.z}).");
        }

        return done;
    }
}
