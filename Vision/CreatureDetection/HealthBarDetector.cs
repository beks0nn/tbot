using OpenCvSharp;

public sealed class HealthBarDetector
{
    private readonly IClientProfile _profile;

    private const int BorderDarkMax = 0;
    private const int FillBrightMin = 1;
    private const int MinFillPixels = 1;

    public HealthBarDetector()
    {
        _profile = new TibiaraDXProfile();
    }

    public struct BarDetection
    {
        public Rect Rect;
        public bool IsDead;
        public DateTime DetectedAt;
    }

    /// Detects 27×4 HP bars. Marks IsDead = true if inner fill has zero bright pixels.
    public List<BarDetection> Detect(Mat gray, bool debug = false)
    {
        var bars = new List<BarDetection>();
        int barW = _profile.HpBarWidth;
        int barH = _profile.HpBarHeight;
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

                    bool isDead = brightPixels == 0;
                    bool validLive = brightPixels >= MinFillPixels;

                    if (validLive || isDead)
                    {
                        bars.Add(new BarDetection
                        {
                            Rect = new Rect(x, y, barW, barH),
                            IsDead = isDead,
                            DetectedAt = DateTime.UtcNow
                        });
                    }

                    x += barW - 3;
                }
            }
        }

        if (debug)
        {
            var debugImg = gray.CvtColor(ColorConversionCodes.GRAY2BGR);
            foreach (var b in bars)
            {
                var color = b.IsDead ? Scalar.Red : Scalar.Lime;
                Cv2.Rectangle(debugImg, b.Rect, color, 1);
            }
            Cv2.ImShow("HealthBarDetector (live=green dead=red)", debugImg);
            Cv2.WaitKey(0);
            Cv2.DestroyAllWindows();
        }

        return bars;
    }
}
