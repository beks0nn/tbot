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

    private Mat[]? _buffers;   // triple buffer
    private int _frontIndex;  // -1 = no frame yet, else 0–2

    private bool _running;
    private Task? _captureTask;

    public void Start()
    {
        _captureService = new DX11ScreenCaptureService();
        var gpu = _captureService.GetGraphicsCards().First();
        _display = _captureService.GetDisplays(gpu).First();
        _screenCapture = _captureService.GetScreenCapture(_display);
        _zone = _screenCapture.RegisterCaptureZone(0, 0, _display.Width, _display.Height);

        _buffers = [
            new Mat(_display.Height, _display.Width, MatType.CV_8UC4),
            new Mat(_display.Height, _display.Width, MatType.CV_8UC4),
            new Mat(_display.Height, _display.Width, MatType.CV_8UC4)
        ];

        _frontIndex = -1; // sentinel: no frame yet

        _cts = new CancellationTokenSource();
        _running = true;

        _captureTask = Task.Run(() => CaptureLoop(_cts.Token));
    }

    private unsafe void CaptureLoop(CancellationToken token)
    {
        if (_buffers == null)
            return;

        int backIndex = 0;
        int spareIndex = 1;
        int frontIndexLocal = 2;

        var sw = new Stopwatch();

        while (!token.IsCancellationRequested && _running)
        {
            sw.Restart();
            _screenCapture!.CaptureScreen();

            // write into back buffer
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
                    Buffer.MemoryCopy(
                        srcMat.Data.ToPointer(),
                        _buffers[backIndex].Data.ToPointer(),
                        bytes,
                        bytes);
                }
            }

            // --- publish frame ---
            Volatile.Write(ref _frontIndex, backIndex);

            // rotate roles (front <- back, back <- spare, spare <- old front)
            int oldFront = frontIndexLocal;
            frontIndexLocal = backIndex;
            backIndex = spareIndex;
            spareIndex = oldFront;

            // cap ~30 FPS
            int delay = Math.Max(0, 33 - (int)sw.ElapsedMilliseconds);
            if (delay > 0)
                Thread.Sleep(delay);
        }
    }

    /// <summary>
    /// Returns a clone of the most recent frame, or null if none is available.
    /// </summary>
    public Mat? GetLatestFrameCopy()
    {
        if (_buffers == null)
            return null;

        int idx = Volatile.Read(ref _frontIndex);
        if (idx < 0)
            return null;

        return _buffers[idx].Clone(); // one clone per read
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
                Buffer.MemoryCopy(
                    srcMat.Data.ToPointer(),
                    clone.Data.ToPointer(),
                    bytes,
                    bytes);

                return clone;
            }
        }
    }

    public void Stop()
    {
        _running = false;
        _cts?.Cancel();
        try
        {
            _captureTask?.Wait();
        }
        catch (AggregateException ae) when (ae.InnerExceptions.All(e => e is TaskCanceledException))
        {
            // ignore cancellation
        }
    }

    public void Dispose()
    {
        Stop();

        if (_buffers != null)
        {
            foreach (var b in _buffers)
                b.Dispose();
        }

        _screenCapture?.Dispose();
        _captureService?.Dispose();
        _cts?.Dispose();
    }
}
