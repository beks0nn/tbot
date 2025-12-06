
namespace Bot.State;

public sealed class BotRuntime
{
    public BotContext Ctx { get; }
    public BotServices Svc { get; }

    public BotRuntime(BotContext ctx, BotServices svc)
    {
        Ctx = ctx;
        Svc = svc;
    }
}
