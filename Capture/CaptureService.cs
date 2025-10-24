using OpenCvSharp;
using ScreenCapture.NET;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Bot.Capture
{
    public sealed class CaptureService : IDisposable
    {
        private DX11ScreenCaptureService _captureService;
        private IScreenCapture _screenCapture;
        private Display _display;
        private ICaptureZone _zone;
        private CancellationTokenSource? _cts;

        // Preallocated reusable buffer (no new Mat per frame)
        private Mat? _frameMat;

        // Expose latest frame Mat (safe after lock)
        public event Action<Mat>? FrameReady;

        public void Start()
        {
            _captureService = new DX11ScreenCaptureService();
            var gpu = _captureService.GetGraphicsCards().First();
            _display = _captureService.GetDisplays(gpu).First();
            _screenCapture = _captureService.GetScreenCapture(_display);

            _zone = _screenCapture.RegisterCaptureZone(0, 0, _display.Width, _display.Height);

            // Preallocate output Mat once
            _frameMat = new Mat(_display.Height, _display.Width, MatType.CV_8UC4);

            _cts = new CancellationTokenSource();
            Task.Run(() => CaptureLoop(_cts.Token));
        }

        private unsafe void CaptureLoop(CancellationToken token)
        {
            var sw = new Stopwatch();

            while (!token.IsCancellationRequested)
            {
                sw.Restart();
                _screenCapture.CaptureScreen();

                using (_zone.Lock())
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

                        // Copy from source (DirectX buffer) to preallocated _frameMat
                        long bytes = srcMat.Total() * srcMat.ElemSize();
                        Buffer.MemoryCopy(srcMat.Data.ToPointer(), _frameMat!.Data.ToPointer(), bytes, bytes);
                    }
                }

                FrameReady?.Invoke(_frameMat!);

                // Maintain ~30 FPS pacing
                int delay = Math.Max(0, 33 - (int)sw.ElapsedMilliseconds);
                if (delay > 0)
                    Thread.Sleep(delay);
            }
        }

        public unsafe Mat CaptureSingleFrame()
        {
            _screenCapture.CaptureScreen();

            using (_zone.Lock())
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

        public void Dispose()
        {
            _cts?.Cancel();
            _frameMat?.Dispose();
            _screenCapture?.Dispose();
            _captureService?.Dispose();
        }
    }
}
