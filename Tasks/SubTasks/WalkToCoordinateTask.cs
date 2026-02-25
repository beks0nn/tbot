using Bot.Control;
using Bot.Control.Actions;
using Bot.Navigation;
using Bot.State;

namespace Bot.Tasks.SubTasks;

public sealed class WalkToCoordinateTask : SubTask
{
    private readonly (int x, int y, int z) _target;
    private readonly AStar _astar = new();
    private readonly InputQueue _queue;
    private readonly KeyMover _keyboard;
    private readonly object _owner;

    private ActionHandle? _pending;
    private (int X, int Y)? _expectedTile;
    private int _ticksWaiting;
    private const int MaxWaitTicks = 20;

    private DateTime _nextAllowedMove = DateTime.MinValue;
    private static readonly TimeSpan MoveCooldown = TimeSpan.FromMilliseconds(150);

    private (int X, int Y) _lastPlayerPos;
    private DateTime _stableSince = DateTime.UtcNow;
    private static readonly TimeSpan MinStableTime = TimeSpan.FromMilliseconds(200);

    public WalkToCoordinateTask((int x, int y, int z) target, InputQueue queue, KeyMover keyboard, object owner)
    {
        _target = target;
        _queue = queue;
        _keyboard = keyboard;
        _owner = owner;
        Name = $"WalkTo({_target.x},{_target.y},{_target.z})";
    }

    protected override void OnStart(BotContext ctx)
    {
        _lastPlayerPos = (ctx.PlayerPosition.X, ctx.PlayerPosition.Y);
        _stableSince = DateTime.UtcNow;
    }

    protected override void Execute(BotContext ctx)
    {
        // Wait for pending keyboard action, then apply cooldown from completion
        if (_pending != null)
        {
            if (!_pending.IsCompleted) return;
            _pending = null;
            _nextAllowedMove = DateTime.UtcNow + MoveCooldown;
            return;
        }

        var player = (ctx.PlayerPosition.X, ctx.PlayerPosition.Y, ctx.PlayerPosition.Z);

        // Check if at target
        if (player.Item1 == _target.x && player.Item2 == _target.y && player.Item3 == _target.z)
        {
            Complete();
            return;
        }

        // Detect unexpected floor change (e.g. fell through hole)
        if (player.Item3 != _target.z)
        {
            Fail($"Unexpected floor change (Z={player.Item3}, expected {_target.z})");
            return;
        }

        // Track position stability (time-based to be tick-rate independent)
        if (player.Item1 != _lastPlayerPos.X || player.Item2 != _lastPlayerPos.Y)
        {
            _lastPlayerPos = (player.Item1, player.Item2);
            _stableSince = DateTime.UtcNow;
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

        // Require stable position for MinStableTime before sending another movement
        if (DateTime.UtcNow - _stableSince < MinStableTime)
            return;

        // Pick next tile
        var walkmap = NavigationHelper.BuildDynamicWalkmap(ctx);
        var path = _astar.FindPath(walkmap, (player.Item1, player.Item2), (_target.x, _target.y));

        if (path.Count > 1)
        {
            var next = path[1];
            //Console.WriteLine($"[{Name}] Step ({player.Item1},{player.Item2}) -> ({next.x},{next.y})");
            _expectedTile = next;
            _pending = _queue.Enqueue(
                new StepTowardsAction(_keyboard, (player.Item1, player.Item2), next, ctx.GameWindowHandle), _owner);
        }
    }
}
