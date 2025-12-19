using Bot.Control;
using Bot.GameEntity;
using Bot.Navigation;
using Bot.State;

namespace Bot.Tasks.Implementations;

public sealed class WalkToCreatureTask : BotTask
{
    public override int Priority => TaskPriority.SubTask;
    public int TargetId => _targetId;

    private readonly int _targetId;
    private readonly AStar _astar = new();
    private readonly KeyMover _keyboard;

    private (int X, int Y)? _expectedTile;
    private int _ticksWaiting;
    private const int MaxTicks = 20;

    private DateTime _nextAllowedMove = DateTime.MinValue;
    private readonly TimeSpan _moveCooldown;

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

    public override void OnBeforeStart(BotContext ctx)
    {
        _lastPlayerPos = (ctx.PlayerPosition.X, ctx.PlayerPosition.Y);
        _stableTicks = 0;
        _expectedTile = null;
        _ticksWaiting = 0;
    }

    public override void Do(BotContext ctx)
    {
        var target = ResolveTarget(ctx);
        if (target == null)
        {
            Status = TaskStatus.Completed;
            return;
        }

        var player = (X: ctx.PlayerPosition.X, Y: ctx.PlayerPosition.Y, Z: ctx.PlayerPosition.Z);

        if (player.Z != target.Z)
        {
            Status = TaskStatus.Completed;
            return;
        }

        // Done when adjacent (includes diagonals)
        if (IsAdjacent(player.X, player.Y, target.X, target.Y))
            return;

        // Track position stability (same as your WalkToCoordinateTask)
        if (player.X == _lastPlayerPos.X && player.Y == _lastPlayerPos.Y) _stableTicks++;
        else
        {
            _stableTicks = 0;
            _lastPlayerPos = (player.X, player.Y);
        }

        // Waiting for movement confirmation: do NOT issue more steps until we land where expected
        if (_expectedTile != null)
        {
            // If we became adjacent, stop immediately (prevents extra step after target moved)
            if (IsAdjacent(player.X, player.Y, target.X, target.Y))
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

            if (++_ticksWaiting > MaxTicks)
            {
                // assume movement failed; allow replanning next tick
                _expectedTile = null;
                _ticksWaiting = 0;
            }

            return;
        }

        // Cooldown / stability gates
        if (DateTime.UtcNow < _nextAllowedMove) return;
        if (_stableTicks < RequiredStableTicks) return;

        // Re-check right before planning/stepping
        if (IsAdjacent(player.X, player.Y, target.X, target.Y))
            return;

        var walk = NavigationHelper.BuildDynamicWalkmap(ctx);

        // pick a goal adjacent tile (8-neighbors), not the creature tile
        var goal = PickBestAdjacentTile(ctx, walk, target);
        if (goal == null)
        {
            Status = TaskStatus.Completed;
            return;
        }

        var playerMap = (player.X, player.Y);
        var path = _astar.FindPath(walk, playerMap, goal.Value);

        if (path.Count > 1)
        {
            var next = path[1];

            // Hard guard: never step onto a creature tile even if things changed mid-tick
            if (IsOccupiedByCreature(ctx, next.x, next.y))
            {
                _nextAllowedMove = DateTime.UtcNow + _moveCooldown;
                return;
            }

            _expectedTile = next;
            _keyboard.StepTowards(playerMap, next, ctx.GameWindowHandle);
            _nextAllowedMove = DateTime.UtcNow + _moveCooldown;
        }
        else
        {
            Status = TaskStatus.Completed;
        }
    }

    public override bool Did(BotContext ctx)
    {
        var target = ResolveTarget(ctx);
        if (target == null) return true;

        var p = ctx.PlayerPosition;
        if (p.Z != target.Z) return true;

        return IsAdjacent(p.X, p.Y, target.X, target.Y);
    }

    private Creature? ResolveTarget(BotContext ctx) =>
        ctx.Creatures.FirstOrDefault(c => c.Id == _targetId);

    private static bool IsAdjacent(int px, int py, int tx, int ty) =>
        Math.Abs(px - tx) <= 1 && Math.Abs(py - ty) <= 1 && !(px == tx && py == ty);

    private static bool IsOccupiedByCreature(BotContext ctx, int x, int y)
    {
        foreach (var c in ctx.Creatures)
            if (c.X == x && c.Y == y)
                return true;
        return false;
    }

    private static readonly (int dx, int dy)[] Adj8 =
    {
        (-1,-1), (0,-1), (1,-1),
        (-1, 0),         (1, 0),
        (-1, 1), (0, 1), (1, 1),
    };

    private static (int X, int Y)? PickBestAdjacentTile(BotContext ctx, bool[,] walk, Creature target)
    {
        int h = walk.GetLength(0);
        int w = walk.GetLength(1);

        var player = (X: ctx.PlayerPosition.X, Y: ctx.PlayerPosition.Y);

        (int X, int Y)? best = null;
        int bestScore = int.MaxValue;

        foreach (var d in Adj8)
        {
            int nx = target.X + d.dx;
            int ny = target.Y + d.dy;

            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
            if (!walk[ny, nx]) continue;
            if (IsOccupiedByCreature(ctx, nx, ny)) continue;

            // Chebyshev distance to player (diagonal-aware)
            int score = Math.Max(Math.Abs(nx - player.X), Math.Abs(ny - player.Y));
            if (score < bestScore)
            {
                bestScore = score;
                best = (nx, ny);
            }
        }

        return best;
    }
}
