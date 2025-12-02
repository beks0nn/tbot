using OpenCvSharp;
using ScreenCapture.NET;
using System.Diagnostics;

namespace Bot.Capture;

public sealed class CaptureService : IDisposable
{
    private DX11ScreenCaptureService? _captureService;
    private IScreenCapture? _screenCapture;
    private Display _display;
    private ICaptureZone? _zone;
    private CancellationTokenSource? _cts;

    private readonly object _lock = new();
    private Mat? _latestFrame; // volatile shared buffer
    private Mat? _scratchBuffer; // reusable scratch buffer for copy
    private bool _running;

    /// <summary>
    /// Starts the asynchronous capture loop.
    /// </summary>
    public void Start()
    {
        _captureService = new DX11ScreenCaptureService();
        var gpu = _captureService.GetGraphicsCards().First();
        _display = _captureService.GetDisplays(gpu).First();
        _screenCapture = _captureService.GetScreenCapture(_display);
        _zone = _screenCapture.RegisterCaptureZone(0, 0, _display.Width, _display.Height);

        _scratchBuffer = new Mat(_display.Height, _display.Width, MatType.CV_8UC4);
        _cts = new CancellationTokenSource();
        _running = true;

        Task.Run(() => CaptureLoop(_cts.Token));
    }

    private unsafe void CaptureLoop(CancellationToken token)
    {
        var sw = new Stopwatch();

        while (!token.IsCancellationRequested && _running)
        {
            sw.Restart();
            _screenCapture!.CaptureScreen();

            using (_zone!.Lock())
            {
                var raw = _zone.RawBuffer;

                fixed (byte* p = raw)
                {
                    using var srcMat = Mat.FromPixelData(
                        _zone.Height,
                        _zone.Width,
                        MatType.CV_8UC4,
                        (IntPtr)p,
                        _zone.Stride);

                    long bytes = srcMat.Total() * srcMat.ElemSize();
                    Buffer.MemoryCopy(srcMat.Data.ToPointer(), _scratchBuffer!.Data.ToPointer(), bytes, bytes);
                }
            }

            // Swap latest frame under lock
            lock (_lock)
            {
                _latestFrame?.Dispose();
                _latestFrame = _scratchBuffer!.Clone(); // deep copy for safety
            }

            // Aim for ~30 FPS capture rate (adjust as needed)
            int delay = Math.Max(0, 33 - (int)sw.ElapsedMilliseconds);
            if (delay > 0)
                Thread.Sleep(delay);
        }
    }

    /// <summary>
    /// Returns a copy of the most recent frame, or null if none is available.
    /// </summary>
    public Mat? GetLatestFrameCopy()
    {
        lock (_lock)
        {
            if (_latestFrame == null)
                return null;

            return _latestFrame.Clone(); // safe detached copy
        }
    }

    /// <summary>
    /// Single synchronous capture, used for initialization.
    /// </summary>
    public unsafe Mat CaptureSingleFrame()
    {
        _screenCapture!.CaptureScreen();

        using (_zone!.Lock())
        {
            var raw = _zone.RawBuffer;

            fixed (byte* p = raw)
            {
                using var srcMat = Mat.FromPixelData(
                    _zone.Height,
                    _zone.Width,
                    MatType.CV_8UC4,
                    (IntPtr)p,
                    _zone.Stride);

                var clone = new Mat(_zone.Height, _zone.Width, MatType.CV_8UC4);
                long bytes = srcMat.Total() * srcMat.ElemSize();
                Buffer.MemoryCopy(srcMat.Data.ToPointer(), clone.Data.ToPointer(), bytes, bytes);
                return clone;
            }
        }
    }

    public void Stop()
    {
        _running = false;
        _cts?.Cancel();
    }

    public void Dispose()
    {
        Stop();

        lock (_lock)
        {
            _latestFrame?.Dispose();
            _scratchBuffer?.Dispose();
        }

        _screenCapture?.Dispose();
        _captureService?.Dispose();
    }
}