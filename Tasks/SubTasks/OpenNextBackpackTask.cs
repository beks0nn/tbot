using Bot.Control;
using Bot.State;

namespace Bot.Tasks.SubTasks;

public sealed class OpenNextBackpackTask : SubTask
{
    private readonly ProfileSettings _profile;
    private readonly MouseMover _mouse;

    private bool _clicked;
    private DateTime _clickTime;
    private static readonly TimeSpan PostClickDelay = TimeSpan.FromMilliseconds(400);

    public OpenNextBackpackTask(ProfileSettings profile, MouseMover mouse)
    {
        _profile = profile;
        _mouse = mouse;
        Name = "OpenNextBackpack";
    }

    protected override void OnStart(BotContext ctx)
    {
        Console.WriteLine("[Loot] Preparing to open next backpack...");
    }

    protected override void Execute(BotContext ctx)
    {
        if (_clicked)
        {
            if (DateTime.UtcNow - _clickTime > PostClickDelay)
                Complete();
            return;
        }

        var bp = _profile.BpRect;
        int pixelX = bp.X + bp.W - 10;
        int pixelY = bp.Y + bp.H - 10;

        Console.WriteLine($"[Loot] Right-clicking backpack corner at ({pixelX},{pixelY})");
        _mouse.RightClickSlow(pixelX, pixelY);

        _clickTime = DateTime.UtcNow;
        _clicked = true;
    }
}
