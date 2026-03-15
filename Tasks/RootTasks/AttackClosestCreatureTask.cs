using Bot.Control;
using Bot.Control.Actions;
using Bot.GameEntity;
using Bot.State;
using Bot.Tasks.SubTasks;

namespace Bot.Tasks.RootTasks;

public sealed class AttackClosestCreatureTask : BotTask
{
    public override int Priority => TaskPriority.AttackClosestCreature;

    private readonly InputQueue _queue;
    private readonly KeyMover _keyboard;
    private readonly MouseMover _mouse;

    private int? _targetId;
    private WalkToCreatureTask? _walkSub;
    private ActionHandle? _pending;

    private DateTime _nextReevaluate = DateTime.MinValue;
    private DateTime _lastClick = DateTime.MinValue;
    private DateTime _started;
    private bool _timedOut;
    private static readonly Random _rng = new();
    private static readonly TimeSpan ReevaluateInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan ClickCooldown = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan MaxCombatDuration = TimeSpan.FromSeconds(_rng.Next(55,180));

    private const int MaxFailedAttempts = 9;

    public AttackClosestCreatureTask(InputQueue queue, KeyMover keyboard, MouseMover mouse)
    {
        _queue = queue;
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
        // Wait for pending action, then apply cooldown from completion time
        if (_pending != null)
        {
            if (!_pending.IsCompleted) return;
            _pending = null;
            _lastClick = DateTime.UtcNow; // cooldown starts after action completes
            return; // wait for fresh frame
        }

        if (ctx.Creatures.Count == 0)
        {
            Complete();
            return;
        }

        if (_timedOut)
        {
            Complete();
            return;
        }

        if (DateTime.UtcNow - _started > MaxCombatDuration)
        {
            Console.WriteLine("[AttackClosest] Combat timeout, sending Escape to clear target");
            _pending = _queue.Enqueue(new PressKeyAction(_keyboard, KeyMover.VK_ESCAPE, ctx.GameWindowHandle), this);
            _timedOut = true;
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
                _walkSub = new WalkToCreatureTask(_targetId!.Value, _queue, _keyboard, this, TimeSpan.FromMilliseconds(40));

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
            _pending = _queue.Enqueue(new RightClickTileAction(_mouse, rel, ctx.Profile), this);

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
