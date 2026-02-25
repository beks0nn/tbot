using System.Drawing.Imaging;
using OpenCvSharp;

namespace Bot.Control.Actions;

public sealed class FindAndClickTemplateAction(
    MouseMover mouse, Mat template, int searchX, int searchY, int searchW, int searchH) : InputAction
{
    private const double MatchThreshold = 0.65;

    public override TimeSpan EstimatedDuration => TimeSpan.FromMilliseconds(500);

    public override async Task RunAsync(CancellationToken ct)
    {
        using var screenshot = CaptureScreenRegion(searchX, searchY, searchW, searchH);
        if (screenshot.Empty())
        {
            Console.WriteLine("[FindClick] Failed to capture screen region");
            return;
        }

        using var gray = new Mat();
        Cv2.CvtColor(screenshot, gray, ColorConversionCodes.BGR2GRAY);

        using var result = new Mat();
        Cv2.MatchTemplate(gray, template, result, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out var maxLoc);

        if (maxVal < MatchThreshold)
        {
            Console.WriteLine($"[FindClick] Template not found (best match: {maxVal:F2})");
            return;
        }

        int targetX = searchX + maxLoc.X + template.Width / 2;
        int targetY = searchY + maxLoc.Y + template.Height / 2;

        Console.WriteLine($"[FindClick] Found at ({targetX}, {targetY}) confidence: {maxVal:F2}");
        await mouse.LeftClickSlowAsync(targetX, targetY, ct);
    }

    private static Mat CaptureScreenRegion(int x, int y, int width, int height)
    {
        try
        {
            using var bmp = new System.Drawing.Bitmap(width, height, PixelFormat.Format24bppRgb);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height));

            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Bmp);
            return Cv2.ImDecode(ms.ToArray(), ImreadModes.Color);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FindClick] Screen capture failed: {ex.Message}");
            return new Mat();
        }
    }
}
