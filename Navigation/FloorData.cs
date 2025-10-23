using OpenCvSharp;

namespace Bot.Navigation;

public sealed class FloorData
{
    public int Z { get; }
    public Mat Color { get; }
    public bool[,] Walkable { get; }
    public int TileWidth => Walkable.GetLength(1);
    public int TileHeight => Walkable.GetLength(0);
    public int PxPerTile = 1; // one pixel = one tile

    public FloorData(int z, Mat color, Mat cost)
    {
        Z = z;
        Color = color;
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
                byte v = cost.At<byte>(y, x);
                // Common convention: 255 = wall/unknown, lower = walkable
                walk[y, x] = v < 254;
            }

        return walk;
    }
}