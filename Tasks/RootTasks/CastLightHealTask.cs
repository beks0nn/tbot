using Bot.Control;
using Bot.Control.Actions;
using Bot.State;

namespace Bot.Tasks.RootTasks;

public sealed class CastLightHealTask : BotTask
{
    public override int Priority => TaskPriority.CastLightHeal;

    private readonly InputQueue _queue;
    private readonly KeyMover _keyboard;
    private ActionHandle? _pending;
    private bool _casted;
    private DateTime _castTime;

    private static readonly Random _rng = new();
    public TimeSpan PostCastDelay { get; init; } = TimeSpan.FromMilliseconds(200 + _rng.Next(0, 100));

    public CastLightHealTask(InputQueue queue, KeyMover keyboard)
    {
        _queue = queue;
        _keyboard = keyboard;
        Name = "CastLightHeal";
    }

    protected override void OnStart(BotContext ctx)
    {
        Console.WriteLine("[Task] Preparing to cast Light Heal");
    }

    protected override void Execute(BotContext ctx)
    {
        if (_pending != null)
        {
            if (!_pending.IsCompleted) return;
            _pending = null;
            _casted = true;
            _castTime = DateTime.UtcNow;
            return;
        }

        if (_casted)
        {
            if (DateTime.UtcNow - _castTime > PostCastDelay)
                Complete();
            return;
        }

        ushort key = ctx.Health >= 95 ? KeyMover.VK_F2 : KeyMover.VK_F1;
        _pending = _queue.Enqueue(new PressKeyAction(_keyboard, key, ctx.GameWindowHandle), this);
    }
}
