using Bot.Control;
using Bot.Navigation;
using System.Windows.Controls;

namespace Bot.Tasks;

public sealed class WalkToWaypointTask : BotTask
{
    public override int Priority { get; set; } = 1;

    private readonly (int x, int y, int z) _target;
    private readonly AStar _astar = new();
    private readonly KeyMover _mover = new();

    private (int X, int Y)? _expectedTile = null;
    private int _ticksWaiting = 0;
    private const int MaxTicks = 20; // ~20 frames of time allowed

    private DateTime _nextAllowedMove = DateTime.MinValue;
    private static readonly TimeSpan MoveCooldown = TimeSpan.FromMilliseconds(150);

    private (int X, int Y) _lastPlayerPos;
    private int _stableTicks = 0;
    private const int RequiredStableTicks = 2;

    public WalkToWaypointTask((int x, int y, int z) target)
    {
        _target = target;
        Name = $"WalkToWaypoint({_target.x},{_target.y},{_target.z})";
    }

    public override void OnBeforeStart(BotContext ctx)
    {
        Console.WriteLine($"[Task] WalkToWaypoint OnBeforeStart ({_target.x}, {_target.y}, {_target.z})");
        _lastPlayerPos = (ctx.PlayerPosition.X, ctx.PlayerPosition.Y);
    }

    public override void Do(BotContext ctx)
    {
        var player = (ctx.PlayerPosition.X, ctx.PlayerPosition.Y, ctx.PlayerPosition.Floor);

        // done?
        if (player == (_target.x, _target.y, _target.z)) return;

        // Track position stability
        if (player.X == _lastPlayerPos.X && player.Y == _lastPlayerPos.Y)
            _stableTicks++;
        else
        {
            _stableTicks = 0;
            _lastPlayerPos = (player.X, player.Y);
        }


        // waiting for movement confirmation?
        if (_expectedTile != null)
        {
            if (player.X == _expectedTile.Value.X &&
                player.Y == _expectedTile.Value.Y)
            {
                // movement completed successfully
                _expectedTile = null;
                _ticksWaiting = 0;
                return;
            }

            // timer advances on each Tick (no sleeps)
            _ticksWaiting++;

            if (_ticksWaiting > MaxTicks)
            {
                // assume movement failed, recalc path
                _expectedTile = null;
                _ticksWaiting = 0;
            }

            return;
        }

        // Cooldown: don't issue another step too soon
        if (DateTime.UtcNow < _nextAllowedMove)
            return;

        // Require stable position before sending another movement
        if (_stableTicks < RequiredStableTicks)
            return;

        // pick next tile
        var walkmap = NavigationHelper.BuildDynamicWalkmap(ctx);
        var path = _astar.FindPath(walkmap, (player.X, player.Y), (_target.x, _target.y));

        if (path.Count > 1)
        {
            var next = path[1];
            _expectedTile = next;

            _mover.StepTowards((player.X, player.Y), next, ctx.GameWindowHandle);

            // SAFETY: enforce movement pacing
            _nextAllowedMove = DateTime.UtcNow + MoveCooldown;
        }
    }

    public override bool Did(BotContext ctx)
    {
        var p = ctx.PlayerPosition;
        return (p.X == _target.x && p.Y == _target.y && p.Floor == _target.z);
    }
}


