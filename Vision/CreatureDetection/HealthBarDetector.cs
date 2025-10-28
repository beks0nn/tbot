using OpenCvSharp;
using System.Collections.Generic;

namespace Bot.Vision.CreatureDetection;

public sealed class HealthBarDetector
{
    private readonly IClientProfile _profile;

    private const int BorderDarkMax = 30;  // black border max gray value
    private const int FillBrightMin = 70;  // inner fill min gray value
    private const int MinFillPixels = 4;   // minimum bright pixels in inner region

    public HealthBarDetector()
    {
        _profile = new TibiaraDXProfile();
    }

    /// <summary>
    /// Detects 27×4 HP bars (1px black border + bright inner fill) using direct pointer access.
    /// </summary>
    public List<Rect> Detect(Mat gray, bool debug = false)
    {
        var bars = new List<Rect>();
        int barW = _profile.HpBarWidth;   // 27
        int barH = _profile.HpBarHeight;  // 4
        int width = gray.Cols;
        int height = gray.Rows;

        unsafe
        {
            byte* ptr = (byte*)gray.DataPointer;
            int step = (int)gray.Step();

            for (int y = 0; y <= height - barH; y++)
            {
                for (int x = 0; x <= width - barW; x++)
                {
                    byte* start = ptr + y * step + x;
                    bool borderOk = true;

                    // --- Top and bottom borders ---
                    for (int bx = 0; bx < barW; bx++)
                    {
                        if (start[bx] > BorderDarkMax ||
                            start[(barH - 1) * step + bx] > BorderDarkMax)
                        {
                            borderOk = false;
                            break;
                        }
                    }
                    if (!borderOk) continue;

                    // --- Left and right borders ---
                    for (int by = 0; by < barH; by++)
                    {
                        if (start[by * step] > BorderDarkMax ||
                            start[by * step + barW - 1] > BorderDarkMax)
                        {
                            borderOk = false;
                            break;
                        }
                    }
                    if (!borderOk) continue;

                    // --- Check inner fill ---
                    int brightPixels = 0;
                    for (int iy = 1; iy < barH - 1; iy++)
                    {
                        byte* row = start + iy * step + 1;
                        for (int ix = 0; ix < barW - 2; ix++)
                        {
                            if (row[ix] >= FillBrightMin)
                                brightPixels++;
                        }
                    }

                    if (brightPixels >= MinFillPixels)
                        bars.Add(new Rect(x, y, barW, barH));

                    // Skip ahead slightly (avoid overlapping)
                    x += barW - 3;
                }
            }
        }

        if (debug)
        {
            var debugImg = gray.CvtColor(ColorConversionCodes.GRAY2BGR);
            foreach (var r in bars)
                Cv2.Rectangle(debugImg, r, Scalar.Lime, 1);
            Cv2.ImShow("HealthBarDetector (Fast 27x4)", debugImg);
            Cv2.WaitKey(0);
            Cv2.DestroyAllWindows();
        }

        return bars;
    }
}
