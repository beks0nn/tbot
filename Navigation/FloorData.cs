using OpenCvSharp;

namespace Bot.Navigation;

public sealed class FloorData
{
    public int Z { get; }
    public Mat Color { get; }
    public Mat Gray { get; }
    public bool[,] Walkable { get; }
    public int TileWidth => Walkable.GetLength(1);
    public int TileHeight => Walkable.GetLength(0);
    public int PxPerTile = 1; // one pixel = one tile

    public FloorData(int z, Mat color, Mat cost)
    {
        Z = z;
        Color = color;
        Gray = color.CvtColor(ColorConversionCodes.BGR2GRAY);
        Walkable = BuildWalkability(cost);
    }

    private static bool[,] BuildWalkability(Mat cost)
    {
        //Cv2.ImShow("BuildWalkability", cost);
        //Cv2.WaitKey(0); // Wait until a key is pressed
        //Cv2.DestroyAllWindows();
        int h = cost.Height;
        int w = cost.Width;
        bool[,] walk = new bool[h, w];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                byte v = cost.At<byte>(y, x);
                walk[y, x] = v < 240;
            }

        return walk;
    }
}