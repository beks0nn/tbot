using Bot.Control;
using Bot.GameEntity;
using Bot.Navigation;
using Bot.State;

namespace Bot.Tasks.Implementations;

public sealed class AttackClosestCreatureTask : BotTask
{
    public override int Priority { get; set; } = TaskPriority.AttackClosestCreature;
    private readonly IClientProfile _profile;
    private readonly AStar _astar = new();
    private readonly KeyMover _keyboard;
    private readonly MouseMover _mouse;

    private Creature? _target;
    private (int X, int Y)? _targetSlot;

    private DateTime _nextStep = DateTime.MinValue;
    private DateTime _nextReevaluate = DateTime.MinValue;
    private DateTime _lastClick = DateTime.MinValue;
    private DateTime _started = DateTime.UtcNow;
    private DateTime _lastSeenTarget = DateTime.UtcNow;

    // Cached path state
    private (int X, int Y) _lastPlayerMap;
    private (int X, int Y) _lastTargetMap;
    private (int X, int Y)? _nextStepCached;

    private static readonly TimeSpan StepInterval = TimeSpan.FromMilliseconds(40);
    private static readonly TimeSpan ReevaluateInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan ClickCooldown = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan MaxCombatDuration = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LostTargetTimeout = TimeSpan.FromMilliseconds(500);

    private const int MaxFailed = 9;

    public AttackClosestCreatureTask(IClientProfile profile, KeyMover keyboard, MouseMover mouse)
    {
        _keyboard = keyboard;
        _mouse = mouse;
        _profile = profile;
        Name = "AttackClosestCreature";
    }

    public override void OnBeforeStart(BotContext ctx)
    {
        _started = DateTime.UtcNow;
        PickClosestCreature(ctx);
    }

    public override void Do(BotContext ctx)
    {
        if (ctx.Creatures.Count == 0)
        {
            Status = TaskStatus.Completed;
            return;
        }

        if (DateTime.UtcNow - _started > MaxCombatDuration)
        {
            Console.WriteLine("[Combat] Timeout — completing task.");
            Status = TaskStatus.Completed;
            return;
        }

        if (DateTime.UtcNow >= _nextReevaluate)
        {
            ReevaluateTarget(ctx);
            _nextReevaluate = DateTime.UtcNow.Add(ReevaluateInterval);
        }

        // TileSlot removed → only check for null target
        if (_target == null)
        {
            PickClosestCreature(ctx);
            if (_target == null)
            {
                Status = TaskStatus.Completed;
                return;
            }
        }

        // Get tile-relative slot on demand
        var tSlot = _target.GetTileSlot(ctx.PlayerPosition.X, ctx.PlayerPosition.Y);
        _targetSlot = tSlot;

        int dx = Math.Abs(tSlot.X);
        int dy = Math.Abs(tSlot.Y);
        bool inRange = dx <= 1 && dy <= 1;

        if (!inRange)
        {
            if (_nextStep > DateTime.UtcNow)
                _nextStep = DateTime.UtcNow;

            if (DateTime.UtcNow >= _nextStep)
            {
                var floor = ctx.CurrentFloor;
                if (floor?.Walkable == null)
                    return;

                var playerMap = (ctx.PlayerPosition.X, ctx.PlayerPosition.Y);
                var targetMap = (X: playerMap.X + tSlot.X, Y: playerMap.Y + tSlot.Y);

                int height = floor.Walkable.GetLength(0);
                int width = floor.Walkable.GetLength(1);

                if (targetMap.X < 0 || targetMap.Y < 0 || targetMap.X >= width || targetMap.Y >= height)
                {
                    Console.WriteLine("[Combat] Target outside walkable bounds.");
                    Status = TaskStatus.Completed;
                    return;
                }

                // Cache-based path reuse
                if (_nextStepCached.HasValue &&
                    _lastPlayerMap == playerMap &&
                    _lastTargetMap == targetMap)
                {
                    _keyboard.StepTowards(playerMap, _nextStepCached.Value, ctx.GameWindowHandle);
                    _nextStep = DateTime.UtcNow.Add(StepInterval);
                    return;
                }

                var walk = NavigationHelper.BuildDynamicWalkmap(ctx);
                var path = _astar.FindPath(walk, playerMap, targetMap);

                if (path.Count > 1)
                {
                    _nextStepCached = path[1];
                    _lastPlayerMap = playerMap;
                    _lastTargetMap = targetMap;

                    _keyboard.StepTowards(playerMap, path[1], ctx.GameWindowHandle);
                    Console.WriteLine($"[Combat] Moving toward creature (next step {path[1]})");
                }
                else
                {
                    Console.WriteLine("[Combat] No path found — completing task.");
                    Status = TaskStatus.Completed;
                    return;
                }

                _nextStep = DateTime.UtcNow.Add(StepInterval);
            }

            return;
        }

        bool clickReady = (DateTime.UtcNow - _lastClick) >= ClickCooldown;

        if (!_target.IsRedSquare && clickReady)
        {
            Console.WriteLine($"[Combat] Attacking tile ({tSlot.X},{tSlot.Y})");
            _mouse.RightClickTile(tSlot, _profile);
            _lastClick = DateTime.UtcNow;

            // Track failure on the shared context
            if (ctx.FailedAttacks.TryGetValue(_target.Id, out int fails))
                ctx.FailedAttacks[_target.Id] = ++fails;
            else
                ctx.FailedAttacks[_target.Id] = 1;

            if (ctx.FailedAttacks[_target.Id] >= MaxFailed)
            {
                ctx.IgnoredCreatures.Add(_target.Id);
                Console.WriteLine($"[Combat] Marking creature {_target.Id} as invalid (fails={ctx.FailedAttacks[_target.Id]}).");
            }
        }
        else if (_target.IsRedSquare)
        {
            // Reset failures on success
            if (ctx.FailedAttacks.ContainsKey(_target.Id))
                ctx.FailedAttacks.Remove(_target.Id);
        }
    }

    public override bool Did(BotContext ctx)
    {
        bool noEnemies = ctx.Creatures.Count == 0;
        bool noTarget = _target == null; // TileSlot removed
        bool noAttack = !ctx.IsAttacking;
        return (noEnemies || noTarget) && noAttack;
    }

    private void ReevaluateTarget(BotContext ctx)
    {
        // If any creature is visually targeted, keep or switch to it
        var visuallyTargeted = ctx.Creatures.FirstOrDefault(c => c.IsRedSquare);
        if (visuallyTargeted != null)
        {
            _target = visuallyTargeted;
            _targetSlot = visuallyTargeted.GetTileSlot(ctx.PlayerPosition.X, ctx.PlayerPosition.Y);
            _lastSeenTarget = DateTime.UtcNow;
            return;
        }

        if (_target == null)
        {
            PickClosestCreature(ctx);
            return;
        }

        // old: previousPlayerPos + TileSlot = world; now just use world coords
        var targetWorld = (X: _target.X, Y: _target.Y);

        Creature? stillVisible = null;
        foreach (var c in ctx.Creatures)
        {
            // current world position
            var worldX = c.X;
            var worldY = c.Y;

            bool sameNow = (worldX == targetWorld.X && worldY == targetWorld.Y);

            bool movedFromPrev = false;
            //if (c.PreviousTile.HasValue)
            //{
            //    // PreviousTile is still tile-relative to previous player
            //    var prevX = ctx.PreviousPlayerPosition.X + c.PreviousTile.Value.X;
            //    var prevY = ctx.PreviousPlayerPosition.Y + c.PreviousTile.Value.Y;
            //    movedFromPrev = (prevX == targetWorld.X && prevY == targetWorld.Y);
            //}

            if (sameNow || movedFromPrev)
            {
                stillVisible = c;
                break;
            }
        }

        if (stillVisible != null)
        {
            if (_targetSlot.HasValue)
            {
                int curDist = Math.Abs(_targetSlot.Value.X) + Math.Abs(_targetSlot.Value.Y);

                var newSlot = stillVisible.GetTileSlot(ctx.PlayerPosition.X, ctx.PlayerPosition.Y);
                int newDist = Math.Abs(newSlot.X) + Math.Abs(newSlot.Y);

                if (newDist > curDist)
                    _nextStep = DateTime.UtcNow;
            }

            _target = stillVisible;
            _targetSlot = stillVisible.GetTileSlot(ctx.PlayerPosition.X, ctx.PlayerPosition.Y);
            _lastSeenTarget = DateTime.UtcNow;
            return;
        }

        var newClosest = FindClosestCreature(ctx);
        if (newClosest == null)
        {
            Status = TaskStatus.Completed;
            return;
        }

        int curDistTarget = _targetSlot.HasValue
            ? Math.Abs(_targetSlot.Value.X) + Math.Abs(_targetSlot.Value.Y)
            : int.MaxValue;

        var newClosestSlot = newClosest.GetTileSlot(ctx.PlayerPosition.X, ctx.PlayerPosition.Y);
        int newDistTarget = Math.Abs(newClosestSlot.X) + Math.Abs(newClosestSlot.Y);

        if (DateTime.UtcNow - _lastSeenTarget > LostTargetTimeout || newDistTarget + 1 < curDistTarget)
        {
            Console.WriteLine($"[Combat] Switching to new target ({newClosestSlot.X},{newClosestSlot.Y})");
            _target = newClosest;
            _targetSlot = newClosestSlot;
            _lastSeenTarget = DateTime.UtcNow;
        }
    }

    private void PickClosestCreature(BotContext ctx)
    {
        _target = FindClosestCreature(ctx);
        _targetSlot = _target?.GetTileSlot(ctx.PlayerPosition.X, ctx.PlayerPosition.Y);
        if (_target != null && _targetSlot.HasValue)
            Console.WriteLine($"[Combat] Initial target: tile ({_targetSlot.Value.X},{_targetSlot.Value.Y})");
    }

    private Creature? FindClosestCreature(BotContext ctx)
    {
        Creature? best = null;
        int bestDist = int.MaxValue;

        foreach (var c in ctx.Creatures)
        {
            var slot = c.GetTileSlot(ctx.PlayerPosition.X, ctx.PlayerPosition.Y);
            int dist = Math.Abs(slot.X) + Math.Abs(slot.Y);
            if (dist < bestDist)
            {
                best = c;
                bestDist = dist;
            }
        }

        return best;
    }
}
