using OpenCvSharp;

namespace Bot.Navigation;

public sealed class FloorData
{
    public int Z { get; }
    public Mat Color { get; }
    public Mat Gray { get; }
    public bool[,] Walkable { get; }

    public int PxPerTile = 1;

    public FloorData(int z, Mat color, Mat cost)
    {
        Z = z;
        Color = color;
        Gray = color.CvtColor(ColorConversionCodes.BGR2GRAY);
        Walkable = BuildWalkability(cost);
    }

    private static bool[,] BuildWalkability(Mat cost)
    {
        int h = cost.Height;
        int w = cost.Width;
        bool[,] walk = new bool[h, w];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vec3b p = cost.At<Vec3b>(y, x);
                byte b = p[0];
                byte g = p[1];
                byte r = p[2];

                // Non-walkable yellow: 255,255,0 (with tiny tolerance)
                bool isYellow = r > 250 && g > 250 && b < 10;

                walk[y, x] = !isYellow;
            }

        return walk;
    }

    public void MarkWalkable(int x, int y)
    {
        if (y < 0 || y >= Walkable.GetLength(0) ||
            x < 0 || x >= Walkable.GetLength(1))
            return;

        Walkable[y, x] = true;
    }
}