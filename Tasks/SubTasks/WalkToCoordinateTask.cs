using Bot.Control;
using Bot.Navigation;
using Bot.State;

namespace Bot.Tasks.SubTasks;

public sealed class WalkToCoordinateTask : SubTask
{
    private readonly (int x, int y, int z) _target;
    private readonly AStar _astar = new();
    private readonly KeyMover _keyboard;

    private (int X, int Y)? _expectedTile;
    private int _ticksWaiting;
    private const int MaxWaitTicks = 20;

    private DateTime _nextAllowedMove = DateTime.MinValue;
    private static readonly TimeSpan MoveCooldown = TimeSpan.FromMilliseconds(150);

    private (int X, int Y) _lastPlayerPos;
    private int _stableTicks;
    private const int RequiredStableTicks = 2;

    public WalkToCoordinateTask((int x, int y, int z) target, KeyMover keyboard)
    {
        _target = target;
        _keyboard = keyboard;
        Name = $"WalkTo({_target.x},{_target.y},{_target.z})";
    }

    protected override void OnStart(BotContext ctx)
    {
        _lastPlayerPos = (ctx.PlayerPosition.X, ctx.PlayerPosition.Y);
    }

    protected override void Execute(BotContext ctx)
    {
        var player = (ctx.PlayerPosition.X, ctx.PlayerPosition.Y, ctx.PlayerPosition.Z);

        // Check if at target
        if (player.Item1 == _target.x && player.Item2 == _target.y && player.Item3 == _target.z)
        {
            Complete();
            return;
        }

        // Track position stability
        if (player.Item1 == _lastPlayerPos.X && player.Item2 == _lastPlayerPos.Y)
            _stableTicks++;
        else
        {
            _stableTicks = 0;
            _lastPlayerPos = (player.Item1, player.Item2);
        }

        // Waiting for movement confirmation?
        if (_expectedTile != null)
        {
            if (player.Item1 == _expectedTile.Value.X && player.Item2 == _expectedTile.Value.Y)
            {
                _expectedTile = null;
                _ticksWaiting = 0;
                return;
            }

            _ticksWaiting++;
            if (_ticksWaiting > MaxWaitTicks)
            {
                _expectedTile = null;
                _ticksWaiting = 0;
            }
            return;
        }

        // Cooldown
        if (DateTime.UtcNow < _nextAllowedMove)
            return;

        // Require stable position before sending another movement
        if (_stableTicks < RequiredStableTicks)
            return;

        // Pick next tile
        var walkmap = NavigationHelper.BuildDynamicWalkmap(ctx);
        var path = _astar.FindPath(walkmap, (player.Item1, player.Item2), (_target.x, _target.y));

        if (path.Count > 1)
        {
            var next = path[1];
            _expectedTile = next;
            _keyboard.StepTowards((player.Item1, player.Item2), next, ctx.GameWindowHandle);
            _nextAllowedMove = DateTime.UtcNow + MoveCooldown;
        }
    }
}
