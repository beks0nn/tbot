using Bot.Control;
using Bot.Navigation;
using Bot.Vision.CreatureDetection;
using System;
using System.Linq;

namespace Bot.Tasks;

public sealed class AttackClosestCreatureTask : BotTask
{
    private readonly IClientProfile _profile;
    private readonly AStar _astar = new();
    private readonly KeyMover _mover = new();
    private readonly MouseMover _mouse = new();

    private Creature? _target;
    private DateTime _nextStep = DateTime.MinValue;
    private DateTime _nextReevaluate = DateTime.MinValue;

    private static readonly TimeSpan StepInterval = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan ReevaluateInterval = TimeSpan.FromMilliseconds(200); // ~5 times per second

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
            Console.WriteLine("[Combat] ❌ No creatures visible.");
            Status = TaskStatus.Completed;
            return;
        }

        // --- Reevaluate target periodically ---
        if (DateTime.UtcNow >= _nextReevaluate)
        {
            var newClosest = FindClosestCreature(ctx);
            if (newClosest != null && !ReferenceEquals(newClosest, _target))
            {
                Console.WriteLine($"[Combat] 🔁 Switching target → ({newClosest.TileSlot!.Value.X},{newClosest.TileSlot!.Value.Y})");
                _target = newClosest;
            }
            _nextReevaluate = DateTime.UtcNow.Add(ReevaluateInterval);
        }

        // --- Handle no valid target ---
        if (_target == null || !_target.TileSlot.HasValue)
        {
            Console.WriteLine("[Combat] ⚠️ Lost target; trying to reacquire...");
            _target = FindClosestCreature(ctx);
            if (_target == null)
            {
                Status = TaskStatus.Completed;
                return;
            }
        }

        // --- If already attacking, hold position ---
        if (ctx.IsAttacking)
        {
            // Do nothing; let combat play out
            return;
        }

        // --- Compute updated relative offset ---
        var tSlot = _target.TileSlot!.Value;
        int dx = Math.Abs(tSlot.X);
        int dy = Math.Abs(tSlot.Y);
        bool inRange = dx <= 1 && dy <= 1;

        if (inRange)
        {
            // Close enough to attack
            var (px, py) = TileToScreenPixel(tSlot, _profile);
            Console.WriteLine($"[Combat] 🖱️ Right-clicking tile ({tSlot.X},{tSlot.Y}) at screen ({px},{py}).");
            _mouse.RightClick(px, py);
            return;
        }

        // --- Move toward target if not in range ---
        if (DateTime.UtcNow < _nextStep)
            return;

        var player = (ctx.PlayerPosition.X, ctx.PlayerPosition.Y, ctx.PlayerPosition.Floor);
        var floor = ctx.CurrentFloor;
        if (floor?.Walkable == null)
            return;

        var targetTile = (player.X + tSlot.X, player.Y + tSlot.Y);
        var path = _astar.FindPath(floor.Walkable, (player.X, player.Y), targetTile);

        if (path.Count > 1)
        {
            _mover.StepTowards((player.X, player.Y), path[1]);
            Console.WriteLine($"[Combat] 🦶 Moving toward creature (next step {path[1]})...");
        }
        else
        {
            Console.WriteLine("[Combat] ⚠️ No path found — waiting for movement update.");
        }

        _nextStep = DateTime.UtcNow.Add(StepInterval);
    }

    public override bool Did(BotContext ctx)
    {
        // Task completes only when all creatures are gone and not attacking
        return ctx.Creatures.Count == 0 && !ctx.IsAttacking;
    }

    // --- Helpers ---

    private void PickClosestCreature(BotContext ctx)
    {
        _target = FindClosestCreature(ctx);
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
