using OpenCvSharp;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Point = OpenCvSharp.Point;

namespace Bot.Vision.CreatureDetection;

public sealed class CreatureBuilder
{
    private readonly IClientProfile _profile;
    private readonly HealthBarDetector _hpDetector;

    public CreatureBuilder()
    {
        _profile = new TibiaraDXProfile();
        _hpDetector = new HealthBarDetector();
    }

    public List<Creature> Build(Mat grayWindow, bool debug = false)
    {
        var sw = Stopwatch.StartNew();
        var creatures = new List<Creature>();

        var bars = _hpDetector.Detect(grayWindow, debug: false);
        if (bars.Count == 0)
            return creatures;

        int tileW = _profile.TileSize;
        int tileH = _profile.TileSize;
        var visibleTiles = _profile.VisibleTiles;
        int centerTileX = visibleTiles.Item1 / 2;
        int centerTileY = visibleTiles.Item2 / 2;

        // Parallel processing for speed
        var creatureList = new List<Creature>(bars.Count);
        object lockObj = new();

        Parallel.ForEach(bars, bar =>
        {
            var barCenter = new Point(bar.X + bar.Width / 2, bar.Y + bar.Height / 2);
            int tileCenterXpx = barCenter.X - _profile.BarToTileCenterOffsetX;
            int tileCenterYpx = barCenter.Y + _profile.BarToTileCenterOffsetY;
            int tileX = Math.Max(0, tileCenterXpx / tileW);
            int tileY = (tileCenterYpx + _profile.TileOriginYOffset) / tileH;

            int relX = tileX - centerTileX;
            int relY = tileY - centerTileY;
            if (relX == 0 && relY == 0) return;

            bool isTargeted = FastHasRedTargetEdge(grayWindow, bar, _profile);

            var creature = new Creature
            {
                BarCenter = barCenter,
                BarRect = bar,
                NameRect = new Rect(
                    Math.Max(0, bar.X - 3),
                    Math.Max(0, bar.Y - 13),
                    Math.Min(bar.Width + 6, grayWindow.Width - bar.X),
                    11),
                TileSlot = (relX, relY),
                Name = null,
                IsPlayer = (relX == 0 && relY == 0),
                IsTargeted = isTargeted
            };

            lock (lockObj)
                creatureList.Add(creature);
        });

        creatures.AddRange(creatureList);

        if (debug)
        {
            foreach (var c in creatures)
            {
                Scalar barColor = c.IsTargeted ? Scalar.Red : Scalar.Lime;
                Cv2.Rectangle(grayWindow, c.BarRect, barColor, 1);
                Cv2.Circle(grayWindow, c.BarCenter, 2, Scalar.Yellow, -1);

                int tileOriginX = (c.TileSlot.Value.X + centerTileX) * tileW;
                int tileOriginY = (c.TileSlot.Value.Y + centerTileY) * tileH;
                Cv2.Rectangle(grayWindow, new Rect(tileOriginX, tileOriginY, tileW, tileH), new Scalar(255, 0, 255), 1);
            }
            Cv2.ImShow("CreatureBuilder Debug", grayWindow);
            Cv2.WaitKey(0);
            Cv2.DestroyAllWindows();
        }

        sw.Stop();
        Console.WriteLine($"[CreatureBuilder] Detected {creatures.Count} creatures in {sw.ElapsedMilliseconds} ms.");
        return creatures;
    }

    private static bool FastHasRedTargetEdge(Mat gray, Rect bar, IClientProfile profile)
    {
        // Geometry (keep these exactly as in your working version)
        int tileSize = profile.TileSize;
        int barToTileOffsetY = profile.BarToTileCenterOffsetY + 2;
        int borderX = bar.X - profile.BarToTileCenterOffsetX - 5;
        int yOfCreatureBar = bar.Y + barToTileOffsetY - 1;

        int adjustedW = tileSize - 3;
        int adjustedH = tileSize - 3;

        // Correct, non-off-by-one bounds check (last accessed index is -1)
        if (borderX < 0 || yOfCreatureBar < 0) return false;
        if (borderX + adjustedW > gray.Width) return false;
        if (yOfCreatureBar + adjustedH > gray.Height) return false;

        // Grayscale "red" band you've validated
        static bool IsRed(byte v) => v >= 105 && v <= 120;

        unsafe
        {
            byte* basePtr = (byte*)gray.DataPointer;
            int step = (int)gray.Step();
            int redCount = 0;

            // --- Top edge (y = 0) ---
            byte* topRow = basePtr + (yOfCreatureBar * step) + borderX;
            for (int x = 0; x < adjustedW; x++)
            {
                if (IsRed(topRow[x]) && ++redCount > 50) return true;
            }

            // --- Bottom edge (y = adjustedH - 1) ---
            byte* botRow = basePtr + ((yOfCreatureBar + adjustedH - 1) * step) + borderX;
            for (int x = 0; x < adjustedW; x++)
            {
                if (IsRed(botRow[x]) && ++redCount > 50) return true;
            }

            // --- Left edge (x = 0), skip corners already counted
            for (int y = 1; y < adjustedH - 1; y++)
            {
                byte v = *(basePtr + (yOfCreatureBar + y) * step + borderX);
                if (IsRed(v) && ++redCount > 50) return true;
            }

            // --- Right edge (x = adjustedW - 1), skip corners already counted
            for (int y = 1; y < adjustedH - 1; y++)
            {
                byte v = *(basePtr + (yOfCreatureBar + y) * step + (borderX + adjustedW - 1));
                if (IsRed(v) && ++redCount > 50) return true;
            }

            return redCount > 50;
        }
    }

}
