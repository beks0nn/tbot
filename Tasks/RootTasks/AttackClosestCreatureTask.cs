using Bot.Control;
using Bot.GameEntity;
using Bot.State;
using Bot.Tasks.SubTasks;

namespace Bot.Tasks.RootTasks;

public sealed class AttackClosestCreatureTask : BotTask
{
    public override int Priority => TaskPriority.AttackClosestCreature;

    private readonly KeyMover _keyboard;
    private readonly MouseMover _mouse;

    private int? _targetId;
    private WalkToCreatureTask? _walkSub;

    private DateTime _nextReevaluate = DateTime.MinValue;
    private DateTime _lastClick = DateTime.MinValue;
    private DateTime _started;

    private static readonly TimeSpan ReevaluateInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan ClickCooldown = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan MaxCombatDuration = TimeSpan.FromSeconds(10);

    private const int MaxFailedAttempts = 9;

    public AttackClosestCreatureTask(KeyMover keyboard, MouseMover mouse)
    {
        _keyboard = keyboard;
        _mouse = mouse;
        Name = "AttackClosestCreature";
    }

    protected override void OnStart(BotContext ctx)
    {
        _started = DateTime.UtcNow;
        PickTarget(ctx);
    }

    protected override void Execute(BotContext ctx)
    {
        if (ctx.Creatures.Count == 0)
        {
            Complete();
            return;
        }

        if (DateTime.UtcNow - _started > MaxCombatDuration)
        {
            Complete();
            return;
        }

        if (DateTime.UtcNow >= _nextReevaluate)
        {
            ReevaluateTarget(ctx);
            _nextReevaluate = DateTime.UtcNow + ReevaluateInterval;
        }

        var target = ResolveTarget(ctx);
        if (target == null)
        {
            PickTarget(ctx);
            target = ResolveTarget(ctx);
            if (target == null)
            {
                Complete();
                return;
            }
        }

        bool inRange = Math.Abs(target.X - ctx.PlayerPosition.X) <= 1 &&
                       Math.Abs(target.Y - ctx.PlayerPosition.Y) <= 1 &&
                       !(target.X == ctx.PlayerPosition.X && target.Y == ctx.PlayerPosition.Y);

        if (!inRange)
        {
            if (_walkSub == null || _walkSub.TargetId != _targetId!.Value)
                _walkSub = new WalkToCreatureTask(_targetId!.Value, _keyboard, TimeSpan.FromMilliseconds(40));

            _walkSub.Tick(ctx);

            if (_walkSub.IsCompleted)
                _walkSub = null;

            return;
        }

        _walkSub = null;

        bool clickReady = (DateTime.UtcNow - _lastClick) >= ClickCooldown;
        if (clickReady && !target.IsRedSquare)
        {
            var rel = target.GetTileSlot(ctx.PlayerPosition.X, ctx.PlayerPosition.Y);
            _mouse.RightClickTile(rel, ctx.Profile);
            _lastClick = DateTime.UtcNow;

            if (ctx.FailedAttacks.TryGetValue(target.Id, out int fails))
                ctx.FailedAttacks[target.Id] = ++fails;
            else
                ctx.FailedAttacks[target.Id] = 1;

            if (ctx.FailedAttacks[target.Id] >= MaxFailedAttempts)
                ctx.IgnoredCreatures.Add(target.Id);
        }
        else if (target.IsRedSquare)
        {
            ctx.FailedAttacks.Remove(target.Id);
        }
    }

    private Creature? ResolveTarget(BotContext ctx)
    {
        if (_targetId == null)
            return null;
        return ctx.Creatures.FirstOrDefault(c => c.Id == _targetId.Value);
    }

    private void ReevaluateTarget(BotContext ctx)
    {
        var red = ctx.Creatures.FirstOrDefault(c => c.IsRedSquare);
        if (red != null)
        {
            if (_targetId != red.Id)
            {
                _targetId = red.Id;
                _walkSub = null;
            }
            return;
        }

        if (ResolveTarget(ctx) != null)
            return;

        PickTarget(ctx);
    }

    private void PickTarget(BotContext ctx)
    {
        Creature? best = null;
        int bestDist = int.MaxValue;

        foreach (var c in ctx.Creatures)
        {
            if (ctx.IgnoredCreatures.Contains(c.Id))
                continue;

            int dist = Math.Abs(c.X - ctx.PlayerPosition.X) + Math.Abs(c.Y - ctx.PlayerPosition.Y);
            if (dist < bestDist)
            {
                best = c;
                bestDist = dist;
            }
        }

        _targetId = best?.Id;
        _walkSub = null;
    }
}
