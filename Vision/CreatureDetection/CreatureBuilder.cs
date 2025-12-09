using OpenCvSharp;
using Point = OpenCvSharp.Point;

namespace Bot.Vision.CreatureDetection;

public sealed class CreatureBuilder
{
    private readonly IClientProfile _profile;
    private readonly HealthBarDetector _hpDetector;

    public CreatureBuilder()
    {
        _profile = new TDXProfile();
        _hpDetector = new HealthBarDetector();
    }

    /// <summary>
    /// Builds both creatures and corpses from detected health bars.
    /// </summary>
    public (List<Creature> Creatures, List<Corpse> Corpses) Build(
        Mat grayWindow,
        (int X, int Y)? previousPlayer = null,
        (int X, int Y)? currentPlayer = null,
        List<Creature>? previousCreatures = null,
        bool debug = false)
    {
        var creatures = new List<Creature>();
        var corpses = new List<Corpse>();

        if (Cv2.Mean(grayWindow)[0] < 35)
            return (creatures, corpses);

        var bars = _hpDetector.Detect(grayWindow, debug: false);
        if (bars.Count == 0)
            return (creatures, corpses);


        int tileW = _profile.TileSize;
        int tileH = _profile.TileSize;
        var visibleTiles = _profile.VisibleTiles;
        int centerTileX = visibleTiles.Width / 2;
        int centerTileY = visibleTiles.Height / 2;

        object lockObj = new();

        Parallel.ForEach(bars, bar =>
        {
            var rect = bar.Rect;
            var barCenter = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            int tileCenterXpx = barCenter.X - _profile.BarToTileCenterOffsetX;
            int tileCenterYpx = barCenter.Y + _profile.BarToTileCenterOffsetY;
            double tileX = (double)tileCenterXpx / tileW;
            double tileY = (double)tileCenterYpx / tileH;
            int relX = (int)Math.Round(tileX - centerTileX);
            int relY = (int)Math.Round(tileY - centerTileY);
            if (relX == 0 && relY == 0) return;

            if (bar.IsDead)
            {
                lock (lockObj)
                {
                    corpses.Add(new Corpse
                    {
                        X = currentPlayer?.X + relX ?? relX,
                        Y = currentPlayer?.Y + relY ?? relY,
                        DetectedAt = bar.DetectedAt
                    });
                }
                // still consider them as creatures... since edge case black hp bars at 1 hp
            }

            bool isTargeted = FastHasRedTargetEdge(grayWindow, rect, _profile);
            var creature = new Creature
            {
                BarCenter = barCenter,
                BarRect = rect,
                NameRect = new Rect(
                    Math.Max(0, rect.X - 10),
                    Math.Max(0, rect.Y - 13),
                    Math.Min(rect.Width + 10 * 2, grayWindow.Width - rect.X),
                    12),
                TileSlot = (relX, relY),
                Name = null,
                IsPlayer = false,
                IsTargeted = isTargeted,
                DetectedAt = bar.DetectedAt,    
            };

            creature.Name = NameDetector.MatchName(grayWindow, creature.NameRect);
            creature.IsPlayer = creature.Name == null ? true : false ;

            lock (lockObj)
                creatures.Add(creature);
        });

        if (debug)
        {
            var debugImg = grayWindow.CvtColor(ColorConversionCodes.GRAY2BGR);
            foreach (var c in creatures)
            {
                var barColor = c.IsTargeted ? Scalar.Red : Scalar.Lime;
                Cv2.Rectangle(debugImg, c.BarRect, barColor, 1);
                Cv2.Circle(debugImg, c.BarCenter, 2, Scalar.Yellow, -1);

                using var nameRoi = new Mat(grayWindow, c.NameRect);
                //Cv2.ImWrite($"names/{c.NameRect.X}_{c.NameRect.Y}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.png", nameRoi);
            }

            foreach (var corpse in corpses)
            {
                var rect = new Rect(
                    (corpse.X - currentPlayer?.X ?? 0 + centerTileX) * tileW,
                    (corpse.Y - currentPlayer?.Y ?? 0 + centerTileY) * tileH,
                    tileW, tileH);
                Cv2.Rectangle(debugImg, rect, Scalar.Blue, 1);
            }

            Cv2.ImShow("CreatureBuilder Debug", debugImg);
            Cv2.WaitKey(0);
            Cv2.DestroyAllWindows();
        }

        return (creatures, corpses);
    }

    private static bool FastHasRedTargetEdge(Mat gray, Rect bar, IClientProfile profile)
    {
        int tileSize = profile.TileSize;
        int borderX = bar.X - profile.TargetScanOffsetX;
        int yOfCreatureBar = bar.Y + profile.TargetScanOffsetY;
        int adjustedW = tileSize - 4;
        int adjustedH = tileSize - 4;

        if (borderX < 0 || yOfCreatureBar < 0) return false;
        if (borderX + adjustedW > gray.Width) return false;
        if (yOfCreatureBar + adjustedH > gray.Height) return false;

        static bool IsRed(byte v) => v >= 111 && v <= 113;

        unsafe
        {
            byte* basePtr = (byte*)gray.DataPointer;
            int step = (int)gray.Step();
            int redCount = 0;

            byte* topRow = basePtr + (yOfCreatureBar * step) + borderX;
            for (int x = 0; x < adjustedW; x++)
                if (IsRed(topRow[x]) && ++redCount > 50) return true;

            byte* botRow = basePtr + ((yOfCreatureBar + adjustedH - 1) * step) + borderX;
            for (int x = 0; x < adjustedW; x++)
                if (IsRed(botRow[x]) && ++redCount > 50) return true;

            for (int y = 1; y < adjustedH - 1; y++)
            {
                if (IsRed(*(basePtr + (yOfCreatureBar + y) * step + borderX)) && ++redCount > 50)
                    return true;
                if (IsRed(*(basePtr + (yOfCreatureBar + y) * step + (borderX + adjustedW - 1))) && ++redCount > 50)
                    return true;
            }

            return redCount > 50;
        }
    }
}
