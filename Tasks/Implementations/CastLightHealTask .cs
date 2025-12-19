using Bot.Control;
using Bot.State;

namespace Bot.Tasks.Implementations;

public sealed class CastLightHealTask : BotTask
{
    public override int Priority => TaskPriority.CastLightHeal;
    private bool _casted = false;
    private DateTime _castTime;
    private readonly KeyMover _keyboard;

    public TimeSpan PostCastDelay { get; init; } = TimeSpan.FromMilliseconds(250);

    public CastLightHealTask(KeyMover keyboard)
    {
        _keyboard = keyboard;
        Name = "CastLightHeal";
    }

    public override void OnBeforeStart(BotContext ctx)
    {
        Console.WriteLine("[Task] Preparing to cast Light Heal (F1)");
    }

    public override void Do(BotContext ctx)
    {
        if (_casted) return;

        if(ctx.Health >= 95)
        {
            _keyboard.PressF2(ctx.GameWindowHandle);
        }
        else
        {
            _keyboard.PressF1(ctx.GameWindowHandle);
        }
            
        _casted = true;
        _castTime = DateTime.UtcNow;

        Console.WriteLine("[Task] Pressed F1 for Light Heal");
    }

    public override bool Did(BotContext ctx)
    {
        // Small delay after cast before finishing
        return _casted && (DateTime.UtcNow - _castTime) > PostCastDelay;
    }
}
