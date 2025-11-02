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

    public List<Creature> Build(
        Mat grayWindow,
        (int X, int Y)? previousPlayer = null,
        (int X, int Y)? currentPlayer = null,
        List<Creature>? previousCreatures = null,
        bool debug = false)
    {
        var sw = Stopwatch.StartNew();
        var creatures = new List<Creature>();

        var bars = _hpDetector.Detect(grayWindow, debug: false);
        if (bars.Count == 0)
            return creatures;

        int tileW = _profile.TileSize;
        int tileH = _profile.TileSize;
        var visibleTiles = _profile.VisibleTiles;
        int centerTileX = visibleTiles.Width / 2;
        int centerTileY = visibleTiles.Height / 2;

        // Compute player movement (in tile units)
        var playerDelta = (X: 0, Y: 0);
        if (previousPlayer.HasValue && currentPlayer.HasValue)
        {
            playerDelta.X = currentPlayer.Value.X - previousPlayer.Value.X;
            playerDelta.Y = currentPlayer.Value.Y - previousPlayer.Value.Y;
        }

        var creatureList = new List<Creature>(bars.Count);
        object lockObj = new();

        Parallel.ForEach(bars, bar =>
        {
            var barCenter = new Point(bar.X + bar.Width / 2, bar.Y + bar.Height / 2);
            int tileCenterXpx = barCenter.X - _profile.BarToTileCenterOffsetX;
            int tileCenterYpx = barCenter.Y + _profile.BarToTileCenterOffsetY;
            int tileX = tileCenterXpx / tileW;
            int tileY = tileCenterYpx / tileH;

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
                IsPlayer = false,
                IsTargeted = isTargeted,
            };

            lock (lockObj)
                creatureList.Add(creature);
        });

        // --- Match & predict based on previous frame ---
        if (previousCreatures != null && previousCreatures.Count > 0)
        {
            foreach (var c in creatureList)
            {
                // Find nearest previous creature (same area)
                var prev = previousCreatures
                    .OrderBy(p => Math.Abs(p.BarCenter.X - c.BarCenter.X) +
                                  Math.Abs(p.BarCenter.Y - c.BarCenter.Y))
                    .FirstOrDefault(p =>
                        Math.Abs(p.BarCenter.X - c.BarCenter.X) < _profile.TileSize &&
                        Math.Abs(p.BarCenter.Y - c.BarCenter.Y) < _profile.TileSize);

                if (prev == null)
                    continue;

                // Estimate delta in pixels (creature motion)
                int deltaX = c.BarCenter.X - prev.BarCenter.X;
                int deltaY = c.BarCenter.Y - prev.BarCenter.Y;

                // Subtract player motion (convert to pixels)
                deltaX -= playerDelta.X * tileW;
                deltaY -= playerDelta.Y * tileH;

                // Compute direction
                c.Direction = (Math.Sign(deltaX), Math.Sign(deltaY));

                // --- Predictive tile adjustment (Tibia logic) ---
                // If movement just started, snap early to next tile
                if (Math.Abs(deltaX) > tileW / 6) c.TileSlot = (c.TileSlot.Value.X + Math.Sign(deltaX), c.TileSlot.Value.Y);
                if (Math.Abs(deltaY) > tileH / 6) c.TileSlot = (c.TileSlot.Value.X, c.TileSlot.Value.Y + Math.Sign(deltaY));

                c.PreviousTile = prev.TileSlot;
                c.LastSeen = DateTime.UtcNow;
            }
        }

        creatures.AddRange(creatureList);

        if (debug)
        {
            var debugImg = grayWindow.CvtColor(ColorConversionCodes.GRAY2BGR);
            foreach (var c in creatures)
            {
                Scalar barColor = c.IsTargeted ? Scalar.Red : Scalar.Lime;
                Cv2.Rectangle(debugImg, c.BarRect, barColor, 1);
                Cv2.Circle(debugImg, c.BarCenter, 2, Scalar.Yellow, -1);

                // Tile rectangle (magenta)
                int tileOriginX = (c.TileSlot!.Value.X + centerTileX) * tileW;
                int tileOriginY = (c.TileSlot!.Value.Y + centerTileY) * tileH;
                Cv2.Rectangle(debugImg, new Rect(tileOriginX, tileOriginY, tileW, tileH), new Scalar(255, 0, 255), 1);

                // Draw motion arrow if available
                if (c.Direction.HasValue)
                {
                    var end = new Point(c.BarCenter.X + c.Direction.Value.X * 10, c.BarCenter.Y + c.Direction.Value.Y * 10);
                    Cv2.ArrowedLine(debugImg, c.BarCenter, end, new Scalar(0, 255, 255), 1);
                }
            }

            Cv2.ImShow("CreatureBuilder Debug", debugImg);
            Cv2.WaitKey(0);
            Cv2.DestroyAllWindows();
        }

        sw.Stop();
        return creatures;
    }

    private static bool FastHasRedTargetEdge(Mat gray, Rect bar, IClientProfile profile)
    {
        // Geometry (keep these exactly as in your working version)
        int tileSize = profile.TileSize;
        int borderX = bar.X - profile.TargetScanOffsetX;
        int yOfCreatureBar = bar.Y + profile.TargetScanOffsetY;

        int adjustedW = tileSize - 4;
        int adjustedH = tileSize - 4;

        // Correct, non-off-by-one bounds check (last accessed index is -1)
        if (borderX < 0 || yOfCreatureBar < 0) return false;
        if (borderX + adjustedW > gray.Width) return false;
        if (yOfCreatureBar + adjustedH > gray.Height) return false;

        // Grayscale "red" band you've validated
        static bool IsRed(byte v) => v >= 111 && v <= 113;

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
