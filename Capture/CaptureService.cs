
using OpenCvSharp;
using ScreenCapture.NET;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;


namespace Bot.Capture;

public class CaptureService
{
    private DX11ScreenCaptureService _captureService;
    private IScreenCapture _screenCapture;
    private Display _display;
    private ICaptureZone _zone;
    private CancellationTokenSource? _cts;

    public event Action<Bitmap>? FrameReady;

    public void Start()
    {
        _captureService = new DX11ScreenCaptureService();
        var gpu = _captureService.GetGraphicsCards().First();

        _display = _captureService.GetDisplays(gpu).First();

        _screenCapture = _captureService.GetScreenCapture(_display);

        _zone = _screenCapture.RegisterCaptureZone(0, 0, _display.Width, _display.Height);

        _cts = new CancellationTokenSource();
        Task.Run(() => CaptureLoop(_cts.Token));
    }

    private void CaptureLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            _screenCapture.CaptureScreen();

            using (_zone.Lock())
            {
                //var image = _zone.Image;
                var raw = _zone.RawBuffer;
                var bmp = ImageConverters.ToBitmap(raw, _zone.Width, _zone.Height, _zone.Stride);
                //var mat = ImageConverters.ToMat(raw, _zone.Width, _zone.Height, _zone.Stride);

                FrameReady?.Invoke((Bitmap)bmp.Clone());
            }

            Thread.Sleep(30); // ~30 fps
        }
    }

    public Bitmap CaptureSingleFrame()
    {
        _screenCapture.CaptureScreen();
        using (_zone.Lock())
        {
            var raw = _zone.RawBuffer;
            var bmp = ImageConverters.ToBitmap(raw, _zone.Width, _zone.Height, _zone.Stride);
            return bmp;
        }
    }
}