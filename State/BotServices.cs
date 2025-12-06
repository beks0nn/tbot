using Bot.Capture;
using Bot.Control;
using Bot.MemClass;
using Bot.Navigation;

namespace Bot.State;

public sealed class BotServices : IDisposable
{
    public MouseMover Mouse { get; }
    public KeyMover Keyboard { get; }
    public MemoryReader Memory { get; }
    public MapRepository MapRepo { get; }
    public CaptureService Capture { get; }
    public PathRepository PathRepo { get; }

    private bool _disposed;

    public BotServices(
        MouseMover mouse,
        KeyMover keyboard, 
        MemoryReader memory, 
        MapRepository mapRepo, 
        CaptureService capture,
        PathRepository pathRepo)
    {
        Mouse = mouse;
        Keyboard = keyboard;
        Memory = memory;
        MapRepo = mapRepo;
        Capture = capture;
        PathRepo = pathRepo;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Capture?.Dispose();
    }
}
