using Bot.Control;
using System;
using WindowsInput.Events;


namespace Bot.Tasks.Implementations;

public sealed class CastLightHealTask : BotTask
{
    public override int Priority { get; set; } = 90; // high but below combat
    private bool _casted = false;
    private DateTime _castTime;
    private readonly KeyMover _keys = new();

    public TimeSpan PostCastDelay { get; init; } = TimeSpan.FromMilliseconds(250);

    public CastLightHealTask()
    {
        Name = "CastLightHeal";
    }

    public override void OnBeforeStart(BotContext ctx)
    {
        Console.WriteLine("[Task] Preparing to cast Light Heal (F1)");
    }

    public override void Do(BotContext ctx)
    {
        if (_casted) return;

        _keys.PressF1(ctx.GameWindowHandle);
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
