using Bot.Control;
using Bot.State;

namespace Bot.Tasks.RootTasks;

public sealed class CastLightHealTask : BotTask
{
    public override int Priority => TaskPriority.CastLightHeal;

    private readonly KeyMover _keyboard;
    private bool _casted;
    private DateTime _castTime;

    public TimeSpan PostCastDelay { get; init; } = TimeSpan.FromMilliseconds(250);

    public CastLightHealTask(KeyMover keyboard)
    {
        _keyboard = keyboard;
        Name = "CastLightHeal";
    }

    protected override void OnStart(BotContext ctx)
    {
        Console.WriteLine("[Task] Preparing to cast Light Heal");
    }

    protected override void Execute(BotContext ctx)
    {
        if (_casted)
        {
            if (DateTime.UtcNow - _castTime > PostCastDelay)
                Complete();
            return;
        }

        if (ctx.Health >= 95)
            _keyboard.PressF2(ctx.GameWindowHandle);
        else
            _keyboard.PressF1(ctx.GameWindowHandle);

        _casted = true;
        _castTime = DateTime.UtcNow;
    }
}
