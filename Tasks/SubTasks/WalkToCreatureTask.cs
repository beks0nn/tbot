using Bot.Control;
using Bot.GameEntity;
using Bot.Navigation;
using Bot.State;

namespace Bot.Tasks.SubTasks;

public sealed class WalkToCreatureTask : SubTask
{
    public int TargetId => _targetId;

    private readonly int _targetId;
    private readonly AStar _astar = new();
    private readonly KeyMover _keyboard;
    private readonly TimeSpan _moveCooldown;

    private (int X, int Y)? _expectedTile;
    private int _ticksWaiting;
    private const int MaxWaitTicks = 20;

    private DateTime _nextAllowedMove = DateTime.MinValue;

    private (int X, int Y) _lastPlayerPos;
    private int _stableTicks;
    private const int RequiredStableTicks = 2;

    public WalkToCreatureTask(int targetId, KeyMover keyboard, TimeSpan? moveCooldown = null)
    {
        _targetId = targetId;
        _keyboard = keyboard;
        _moveCooldown = moveCooldown ?? TimeSpan.FromMilliseconds(40);
        Name = $"WalkToCreature({_targetId})";
    }

    protected override void OnStart(BotContext ctx)
    {
        _lastPlayerPos = (ctx.PlayerPosition.X, ctx.PlayerPosition.Y);
    }

    protected override void Execute(BotContext ctx)
    {
        var target = ResolveTarget(ctx);
        if (target == null)
        {
            Fail("Target creature not found");
            return;
        }

        var player = (X: ctx.PlayerPosition.X, Y: ctx.PlayerPosition.Y, Z: ctx.PlayerPosition.Z);

        if (player.Z != target.Z)
        {
            Fail("Target on different floor");
            return;
        }

        // Done when adjacent
        if (NavigationHelper.IsAdjacent(player.X, player.Y, target.X, target.Y))
        {
            Complete();
            return;
        }

        // Track position stability
        if (player.X == _lastPlayerPos.X && player.Y == _lastPlayerPos.Y)
            _stableTicks++;
        else
        {
            _stableTicks = 0;
            _lastPlayerPos = (player.X, player.Y);
        }

        // Waiting for movement confirmation
        if (_expectedTile != null)
        {
            if (NavigationHelper.IsAdjacent(player.X, player.Y, target.X, target.Y))
            {
                _expectedTile = null;
                _ticksWaiting = 0;
                return;
            }

            if (player.X == _expectedTile.Value.X && player.Y == _expectedTile.Value.Y)
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

        // Cooldown / stability gates
        if (DateTime.UtcNow < _nextAllowedMove)
            return;
        if (_stableTicks < RequiredStableTicks)
            return;

        // Re-check right before planning
        if (NavigationHelper.IsAdjacent(player.X, player.Y, target.X, target.Y))
            return;

        var walk = NavigationHelper.BuildDynamicWalkmap(ctx);
        var goal = NavigationHelper.PickBestAdjacentTile(ctx, walk, target.X, target.Y);

        if (goal == null)
        {
            Fail("No walkable path to creature");
            return;
        }

        var playerPos = (player.X, player.Y);
        var path = _astar.FindPath(walk, playerPos, goal.Value);

        if (path.Count > 1)
        {
            var next = path[1];

            if (NavigationHelper.IsOccupiedByCreature(ctx, next.x, next.y))
            {
                _nextAllowedMove = DateTime.UtcNow + _moveCooldown;
                return;
            }

            _expectedTile = next;
            _keyboard.StepTowards(playerPos, next, ctx.GameWindowHandle);
            _nextAllowedMove = DateTime.UtcNow + _moveCooldown;
        }
        else
        {
            Fail("No path found");
        }
    }

    private Creature? ResolveTarget(BotContext ctx) =>
        ctx.Creatures.FirstOrDefault(c => c.Id == _targetId);
}
