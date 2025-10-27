using OpenCvSharp;
using System.Collections.Generic;

namespace Bot.Vision.CreatureDetection;

public sealed class HealthBarDetector
{
    private readonly IClientProfile _profile;

    private const int BlackThreshold = 30;   // Must be <= this to count as border black
    private const int MinFillPixels = 4;     // At least this many colored pixels inside
    private const int MinGreenOrRed = 70;    // A pixel is "colored" if R or G >= this

    public HealthBarDetector()
    {
        _profile = new TibiaraDXProfile();
    }

    public List<Rect> Detect(Mat gameWindow, bool debug = false)
    {
        var bars = new List<Rect>();
        int BarHeight = _profile.HpBarHeight;
        int BarWidth = _profile.HpBarWidth;
        int BorderThickness = _profile.HpBarThickness;

        using var gray = new Mat();
        Cv2.CvtColor(gameWindow, gray, ColorConversionCodes.BGR2GRAY);

        int width = gray.Cols;
        int height = gray.Rows;

        for (int y = 0; y <= height - BarHeight; y++)
        {
            for (int x = 0; x <= width - BarWidth; x++)
            {
                bool borderOk = true;

                // Check top & bottom border pixels
                for (int bx = 0; bx < BarWidth && borderOk; bx++)
                {
                    if (gray.At<byte>(y, x + bx) > BlackThreshold ||
                        gray.At<byte>(y + BarHeight - 1, x + bx) > BlackThreshold)
                        borderOk = false;
                }

                // Check left & right border pixels
                for (int by = 0; by < BarHeight && borderOk; by++)
                {
                    if (gray.At<byte>(y + by, x) > BlackThreshold ||
                        gray.At<byte>(y + by, x + BarWidth - 1) > BlackThreshold)
                        borderOk = false;
                }

                if (!borderOk)
                    continue;

                // Now check the inner 25x2 px region for colored fill
                var innerRoi = new Rect(
                    x + BorderThickness,
                    y + BorderThickness,
                    BarWidth - BorderThickness * 2,
                    BarHeight - BorderThickness * 2
                );

                using var inner = new Mat(gameWindow, innerRoi);
                int coloredPixels = 0;

                for (int iy = 0; iy < inner.Rows; iy++)
                {
                    for (int ix = 0; ix < inner.Cols; ix++)
                    {
                        var color = inner.At<Vec3b>(iy, ix);
                        // Check if inner pixel has strong red/green channel
                        if (color.Item1 >= MinGreenOrRed || color.Item2 >= MinGreenOrRed)
                            coloredPixels++;
                    }
                }

                if (coloredPixels < MinFillPixels)
                    continue; // too few colored pixels → likely shadow or fully empty

                bars.Add(new Rect(x, y, BarWidth, BarHeight));

                x += BarWidth - 2; // Skip overlapping
            }
        }

        if (debug)
        {
            foreach (var r in bars)
                Cv2.Rectangle(gameWindow, r, Scalar.Lime, 1);

            Cv2.ImShow("HealthBarDetector (Strict)", gameWindow);
            Cv2.WaitKey(0);
            Cv2.DestroyAllWindows();
        }

        return bars;
    }
}
