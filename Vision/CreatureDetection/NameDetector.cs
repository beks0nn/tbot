using OpenCvSharp;

namespace Bot.Vision.CreatureDetection;

public sealed class NameDetector
{
    private static readonly List<(string Name, Mat Template)> _nameTemplates;
    static NameDetector()
    {
        var list = new List<(string, Mat)>();
        if (Directory.Exists("Assets/GwNames"))
        {
            foreach (var path in Directory.GetFiles("Assets/GwNames", "*.png"))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                var img = Cv2.ImRead(path, ImreadModes.Grayscale);
                if (!img.Empty())
                    list.Add((name, img));
            }
        }
        _nameTemplates = list;
    }

    public static string? MatchName(Mat grayWindow, Rect nameRect)
    {
        if (_nameTemplates.Count == 0)
            return null;

        using var roi = new Mat(grayWindow, nameRect);

        foreach (var (name, tmpl) in _nameTemplates)
        {
            if (tmpl.Rows != roi.Rows || tmpl.Cols != roi.Cols)
                continue; // for now require exact size; keep it simple and fast

            if (HasMatrixInsideOther(roi, tmpl))
                return name;
        }

        return null;
    }

    private static unsafe bool HasMatrixInsideOther(Mat matrix, Mat other)
    {
        int rows = other.Rows;
        int cols = other.Cols;
        int mStep = (int)matrix.Step();
        int oStep = (int)other.Step();

        byte* mBase = (byte*)matrix.DataPointer;
        byte* oBase = (byte*)other.DataPointer;

        for (int i = 0; i < rows; i++)
        {
            byte* mRow = mBase + i * mStep;
            byte* oRow = oBase + i * oStep;

            for (int j = 0; j < cols; j++)
            {
                byte o = oRow[j];
                if (o == 0)
                {
                    byte v = mRow[j];
                    if (v != 0 &&
                        v != 110 &&
                        v != 113 &&
                        v != 152 &&
                        v != 170 &&
                        v != 91 &&
                        v != 57 &&
                        v != 29 &&
                        v != 192)
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }
}
