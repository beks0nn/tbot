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
        private (int X, int Y)? _targetSlot;  // Persistent slot memory
        private DateTime _nextStep = DateTime.MinValue;
        private DateTime _nextReevaluate = DateTime.MinValue;

        private static readonly TimeSpan StepInterval = TimeSpan.FromMilliseconds(120);
        private static readonly TimeSpan ReevaluateInterval = TimeSpan.FromMilliseconds(400);
        private static readonly TimeSpan ClickCooldown = TimeSpan.FromMilliseconds(500);
        private static readonly int TargetSwitchThreshold = 2; // tiles closer required to switch

        private DateTime _lastClick = DateTime.MinValue;

        public AttackClosestCreatureTask(IClientProfile profile)
        {
            _profile = profile;
            Name = "AttackClosestCreature";
        }

        public override void OnBeforeStart(BotContext ctx)
        {
            PickClosestCreature(ctx);
        }

        public override void Do(BotContext ctx)
        {
            if (ctx.Creatures.Count == 0)
            {
                Status = TaskStatus.Completed;
                return;
            }

            // --- Reevaluate target every interval ---
            if (DateTime.UtcNow >= _nextReevaluate)
            {
                ReevaluateTarget(ctx);
                _nextReevaluate = DateTime.UtcNow.Add(ReevaluateInterval);
            }

            // --- Ensure target is valid ---
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

            // --- Step 1: Move toward creature if not adjacent ---
            if (!inRange)
            {
                if (DateTime.UtcNow < _nextStep)
                    return;

                var floor = ctx.CurrentFloor;
                if (floor?.Walkable == null)
                    return;

                var playerMap = (ctx.PlayerPosition.X, ctx.PlayerPosition.Y);
                var targetMap = (playerMap.X + tSlot.X, playerMap.Y + tSlot.Y);

                //
                int height = floor.Walkable.GetLength(0);
                int width = floor.Walkable.GetLength(1);

                // A* works in absolute map coordinates, same as Walkable indices.
                var startLocal = (playerMap.X, playerMap.Y);
                var goalLocal = (x: targetMap.Item1, y: targetMap.Item2);

                // Bounds check
                if (goalLocal.x < 0 || goalLocal.y < 0 || goalLocal.x >= width || goalLocal.y >= height)
                {
                    Console.WriteLine($"[Pathing] ⚠️ Target outside visible region ({goalLocal.x},{goalLocal.y})");
                    return;
                }
                //

                if (goalLocal.x < 0 || goalLocal.y < 0 ||
                    goalLocal.x >= width || goalLocal.y >= height)
                {
                    Console.WriteLine($"[Pathing] ⚠️ Target outside visible region ({goalLocal.x},{goalLocal.y})");
                    return;
                }

                Console.WriteLine($"[PathDebug] Player=({ctx.PlayerPosition.X},{ctx.PlayerPosition.Y}) " +
                  $"Target=({targetMap.Item1},{targetMap.Item2}) " +
                  $"LocalStart=({startLocal.X},{startLocal.Y}) " +
                  $"LocalGoal=({goalLocal.x},{goalLocal.y}) " +
                  $"FloorSize={width}x{height} " +
                  $"WalkableGoal={(goalLocal.x >= 0 && goalLocal.x < width && goalLocal.y >= 0 && goalLocal.y < height ? floor.Walkable[goalLocal.y, goalLocal.x] : false)}");
                var path = _astar.FindPath(floor.Walkable, startLocal, goalLocal);

                if (path.Count > 1)
                {
                    _mover.StepTowards(startLocal, path[1]);
                    Console.WriteLine($"[Combat] 🦶 Moving toward creature (next step {path[1]})...");
                }
                else
                {
                    Console.WriteLine("[Combat] ⚠️ No path found — waiting for movement update.");
                }

                _nextStep = DateTime.UtcNow.Add(StepInterval);
                return;
            }

            // --- Step 2: If already targeting this creature, do nothing ---
            if (_target.IsTargeted)
                return;

            // --- Step 3: Otherwise click once to target it ---
            if ((DateTime.UtcNow - _lastClick) < ClickCooldown)
                return;

            var (px, py) = TileToScreenPixel(tSlot, _profile);
            Console.WriteLine($"[Combat] 🖱️ Attacking tile ({tSlot.X},{tSlot.Y}) at ({px},{py})");
            _mouse.RightClick(px, py);
            _lastClick = DateTime.UtcNow;
        }

        public override bool Did(BotContext ctx)
        {
            return ctx.Creatures.Count == 0 && !ctx.IsAttacking;
        }

        // --- Helpers ---

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

            // Keep lock if current target is still visible
            var sameCreatureStillVisible = ctx.Creatures.Any(c =>
                c.TileSlot == _targetSlot);

            if (sameCreatureStillVisible)
                return;

            // Otherwise, only switch if new one is significantly closer
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
