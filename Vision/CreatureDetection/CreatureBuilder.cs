using Bot.Tasks;
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

        var bars = _hpDetector.Detect(grayWindow, debug: false);
        if (bars.Count == 0)
            return (creatures, corpses);

        int tileW = _profile.TileSize;
        int tileH = _profile.TileSize;
        var visibleTiles = _profile.VisibleTiles;
        int centerTileX = visibleTiles.Width / 2;
        int centerTileY = visibleTiles.Height / 2;

        // Compute player movement in tiles
        var playerDelta = (X: 0, Y: 0);
        if (previousPlayer.HasValue && currentPlayer.HasValue)
        {
            playerDelta.X = currentPlayer.Value.X - previousPlayer.Value.X;
            playerDelta.Y = currentPlayer.Value.Y - previousPlayer.Value.Y;
        }

        object lockObj = new();

        Parallel.ForEach(bars, bar =>
        {
            var rect = bar.Rect;
            var barCenter = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            int tileCenterXpx = barCenter.X - _profile.BarToTileCenterOffsetX;
            int tileCenterYpx = barCenter.Y + _profile.BarToTileCenterOffsetY;
            int tileX = tileCenterXpx / tileW;
            int tileY = tileCenterYpx / tileH;
            int relX = tileX - centerTileX;
            int relY = tileY - centerTileY;
            if (relX == 0 && relY == 0) return;

            if (bar.IsDead)
            {
                lock (lockObj)
                {
                    corpses.Add(new Corpse
                    {
                        X = currentPlayer?.X + relX ?? relX,
                        Y = currentPlayer?.Y + relY ?? relY,
                        Floor = currentPlayer?.Y ?? 0, // adjust to use actual floor if available
                        DetectedAt = bar.DetectedAt
                    });
                }
                return;
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

        // --- Match & predict creature motion ---
        if (previousCreatures is { Count: > 0 })
        {
            foreach (var c in creatures)
            {
                var prev = previousCreatures
                    .OrderBy(p => Math.Abs(p.BarCenter.X - c.BarCenter.X) +
                                  Math.Abs(p.BarCenter.Y - c.BarCenter.Y))
                    .FirstOrDefault(p =>
                        Math.Abs(p.BarCenter.X - c.BarCenter.X) < _profile.TileSize &&
                        Math.Abs(p.BarCenter.Y - c.BarCenter.Y) < _profile.TileSize);

                if (prev == null)
                    continue;

                int deltaX = c.BarCenter.X - prev.BarCenter.X;
                int deltaY = c.BarCenter.Y - prev.BarCenter.Y;

                c.Direction = (Math.Sign(deltaX), Math.Sign(deltaY));

                // Predict tile transition when bar center drifts past ~1/3 tile
                int threshold = _profile.TileSize / 3;

                var slot = c.TileSlot.Value;
                if (Math.Abs(deltaX) > threshold)
                    slot.X += Math.Sign(deltaX);
                if (Math.Abs(deltaY) > threshold)
                    slot.Y += Math.Sign(deltaY);

                c.TileSlot = slot;
                c.PreviousTile = prev.TileSlot;
                c.LastSeen = DateTime.UtcNow;
            }
        }

        if (debug)
        {
            var debugImg = grayWindow.CvtColor(ColorConversionCodes.GRAY2BGR);
            foreach (var c in creatures)
            {
                var barColor = c.IsTargeted ? Scalar.Red : Scalar.Lime;
                Cv2.Rectangle(debugImg, c.BarRect, barColor, 1);
                Cv2.Circle(debugImg, c.BarCenter, 2, Scalar.Yellow, -1);

                var nameRoi = new Mat(grayWindow, c.NameRect);
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
