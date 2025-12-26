using Bot.Capture;
using Bot.Control;
using Bot.MemClass;
using Bot.Navigation;
using Bot.State;

namespace Bot;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        Application.SetCompatibleTextRenderingDefault(false);

        var ctx = new BotContext();
        ctx.Profile = ProfileStore.LoadOrCreate("default");

        var services = new BotServices(
            new MouseMover(),
            new KeyMover(),
            new MemoryReader(),
            new MapRepository(),
            new CaptureService(),
            new PathRepository()
        );
        var runtime = new BotRuntime(ctx, services);

        using var app = new App(runtime);
        app.Run();
    }
}