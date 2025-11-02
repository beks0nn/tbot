using Bot.Control;
using Bot.Navigation;
using Bot.Vision.CreatureDetection;
using System;
using System.Linq;

namespace Bot.Tasks
{
    public sealed class AttackClosestCreatureTask : BotTask
    {
        private readonly IClientProfile _profile;
        private readonly AStar _astar = new();
        private readonly KeyMover _mover = new();
        private readonly MouseMover _mouse = new();

        private Creature? _target;
        private (int X, int Y)? _targetSlot;

        private DateTime _nextStep = DateTime.MinValue;
        private DateTime _nextReevaluate = DateTime.MinValue;
        private DateTime _lastClick = DateTime.MinValue;
        private DateTime _started = DateTime.UtcNow;
        private DateTime _lastSeenTarget = DateTime.UtcNow;

        private static readonly TimeSpan StepInterval = TimeSpan.FromMilliseconds(40);
        private static readonly TimeSpan ReevaluateInterval = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan ClickCooldown = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan MaxCombatDuration = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan LostTargetTimeout = TimeSpan.FromSeconds(1);
        private static readonly int TargetSwitchThreshold = 2;

        public override int Priority { get; set; } = 100;

        public AttackClosestCreatureTask(IClientProfile profile)
        {
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
                Console.WriteLine("[Combat] ⏰ Timeout — completing task.");
                Status = TaskStatus.Completed;
                return;
            }

            // Refresh target each frame
            if (_target != null && _target.TileSlot.HasValue)
            {
                var match = ctx.Creatures.FirstOrDefault(c =>
                    c.TileSlot.HasValue &&
                    c.TileSlot.Value.X == _target.TileSlot.Value.X &&
                    c.TileSlot.Value.Y == _target.TileSlot.Value.Y);

                if (match != null)
                {
                    _target = match;
                    _targetSlot = match.TileSlot;
                    _lastSeenTarget = DateTime.UtcNow;
                }
                else if (DateTime.UtcNow - _lastSeenTarget > LostTargetTimeout)
                {
                    Console.WriteLine("[Combat] ❌ Lost target for too long — completing task.");
                    Status = TaskStatus.Completed;
                    return;
                }
            }

            if (DateTime.UtcNow >= _nextReevaluate)
            {
                ReevaluateTarget(ctx);
                _nextReevaluate = DateTime.UtcNow.Add(ReevaluateInterval);
            }

            if (_target == null || !_target.TileSlot.HasValue)
            {
                PickClosestCreature(ctx);
                if (_target == null)
                {
                    Status = TaskStatus.Completed;
                    return;
                }
            }

            var tSlot = _target.TileSlot!.Value;
            int dx = Math.Abs(tSlot.X);
            int dy = Math.Abs(tSlot.Y);
            bool inRange = dx <= 1 && dy <= 1;

            // --- Move aggressively toward fleeing targets ---
            if (!inRange)
            {
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
                        Console.WriteLine("[Pathing] ⚠️ Target outside walkable bounds.");
                        Status = TaskStatus.Completed;
                        return;
                    }

                    var path = _astar.FindPath(floor.Walkable, playerMap, targetMap);
                    if (path.Count > 1)
                    {
                        _mover.StepTowards(playerMap, path[1]);
                        Console.WriteLine($"[Combat] 🦶 Moving toward creature (next step {path[1]})...");
                    }
                    else
                    {
                        Console.WriteLine("[Combat] ⚠️ No path found — completing task.");
                        Status = TaskStatus.Completed;
                        return;
                    }

                    _nextStep = DateTime.UtcNow.Add(StepInterval);
                }

                // Keep moving regardless of click cooldown
                return;
            }

            // --- Attack phase ---
            bool clickReady = (DateTime.UtcNow - _lastClick) >= ClickCooldown;

            // only click if not yet targeted and cooldown expired
            if (!_target.IsTargeted && clickReady)
            {
                var (px, py) = TileToScreenPixel(tSlot, _profile);
                Console.WriteLine($"[Combat] 🖱️ Attacking tile ({tSlot.X},{tSlot.Y}) at ({px},{py})");
                _mouse.RightClick(px, py);
                _lastClick = DateTime.UtcNow;
                // movement not delayed by click
            }
        }

        public override bool Did(BotContext ctx)
        {
            bool noEnemies = ctx.Creatures.Count == 0;
            bool noTarget = _target == null || !_target.TileSlot.HasValue;
            bool noAttack = !ctx.IsAttacking;
            return (noEnemies || noTarget) && noAttack;
        }

        private void ReevaluateTarget(BotContext ctx)
        {
            var newClosest = FindClosestCreature(ctx);
            if (newClosest == null)
                return;

            if (_target == null || !_targetSlot.HasValue)
            {
                _target = newClosest;
                _targetSlot = newClosest.TileSlot;
                Console.WriteLine($"[Combat] 🎯 Initial target ({_targetSlot.Value.X},{_targetSlot.Value.Y})");
                return;
            }

            var sameCreatureStillVisible = ctx.Creatures.Any(c =>
                c.TileSlot.HasValue &&
                _targetSlot.HasValue &&
                c.TileSlot.Value.X == _targetSlot.Value.X &&
                c.TileSlot.Value.Y == _targetSlot.Value.Y);

            if (sameCreatureStillVisible)
                return;

            int curDist = Math.Abs(_targetSlot.Value.X) + Math.Abs(_targetSlot.Value.Y);
            int newDist = Math.Abs(newClosest.TileSlot.Value.X) + Math.Abs(newClosest.TileSlot.Value.Y);

            if (newDist + TargetSwitchThreshold < curDist)
            {
                Console.WriteLine($"[Combat] 🔁 Switching to closer target ({newClosest.TileSlot.Value.X},{newClosest.TileSlot.Value.Y})");
                _target = newClosest;
                _targetSlot = newClosest.TileSlot;
            }
        }

        private void PickClosestCreature(BotContext ctx)
        {
            _target = FindClosestCreature(ctx);
            _targetSlot = _target?.TileSlot;
            if (_target != null)
                Console.WriteLine($"[Combat] 🎯 Initial target: tile ({_target.TileSlot!.Value.X},{_target.TileSlot!.Value.Y})");
        }

        private Creature? FindClosestCreature(BotContext ctx)
        {
            return ctx.Creatures
                .Where(c => !c.IsPlayer && c.TileSlot.HasValue)
                .OrderBy(c => Math.Abs(c.TileSlot.Value.X) + Math.Abs(c.TileSlot.Value.Y))
                .FirstOrDefault();
        }

        private static (int X, int Y) TileToScreenPixel((int X, int Y) tileSlot, IClientProfile profile)
        {
            var (visibleX, visibleY) = profile.VisibleTiles;
            int centerTileX = visibleX / 2;
            int centerTileY = visibleY / 2;

            int absTileX = centerTileX + tileSlot.X;
            int absTileY = centerTileY + tileSlot.Y;

            var gameRect = profile.GameWindowRect;
            int px = gameRect.X + absTileX * profile.TileSize + profile.TileSize / 2;
            int py = gameRect.Y + absTileY * profile.TileSize + profile.TileSize / 2;

            return (px, py);
        }
    }
}
