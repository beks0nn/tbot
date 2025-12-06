using Bot.State;
namespace Bot;

public sealed class App : IDisposable
{
    public BotController Controller { get; }
    public MainForm Ui { get; }

    public App(BotRuntime runtime)
    {
        Controller = new BotController(runtime);
        Ui = new MainForm(Controller);
    }

    public void Run()
    {
        Application.Run(Ui);
    }

    public void Dispose()
    {
        Controller.Dispose();
    }
}
