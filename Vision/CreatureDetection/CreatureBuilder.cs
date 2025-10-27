using OpenCvSharp;
using System.Collections.Generic;
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
    /// Builds structured creature data from the given game window.
    /// Detects health bars, name regions, and whether a creature is targeted.
    /// </summary>
    public List<Creature> Build(Mat gameWindow, bool debug = false)
    {
        var creatures = new List<Creature>();

        // --- Detect all visible HP bars ---
        var bars = _hpDetector.Detect(gameWindow, debug: false);
        if (bars.Count == 0)
            return creatures;

        int tileWidth = _profile.TileSize;
        int tileHeight = _profile.TileSize;
        var visibleTiles = _profile.VisibleTiles;

        foreach (var bar in bars)
        {
            

            var center = new Point(bar.X + bar.Width / 2, bar.Y + bar.Height / 2);

            // --- Name region (above bar) ---
            var nameRect = new Rect(
                Math.Max(0, bar.X - 3),
                Math.Max(0, bar.Y - 13),
                Math.Min(bar.Width + 6, gameWindow.Width - bar.X),
                11
            );

            // --- Tile position relative to player ---
            int tileX = center.X / tileWidth;
            int tileY = center.Y / tileHeight;
            var relX = tileX - visibleTiles.Item1 / 2;
            var relY = tileY - visibleTiles.Item2 / 2;

            bool isTargeted = HasRedTargetEdge(gameWindow, bar, debug: debug);
            var creature = new Creature
            {
                BarCenter = center,
                BarRect = bar,
                NameRect = nameRect,
                TileSlot = (relX, relY),
                Name = null,
                IsPlayer = false,
                IsTargeted = isTargeted
            };

            creatures.Add(creature);

            // --- Visual Debug ---
            if (debug)
            {
                //Scalar barColor = isTargeted ? Scalar.Red : Scalar.Lime;
                //Cv2.Rectangle(gameWindow, bar, barColor, 1);
                //Cv2.Rectangle(gameWindow, nameRect, Scalar.Blue, 1);
                //Cv2.Circle(gameWindow, center, 2, Scalar.Yellow, -1);
            }
        }

        if (debug)
        {
            //Cv2.ImShow("CreatureBuilder Debug", gameWindow);
            //Cv2.WaitKey(0);
            //Cv2.DestroyAllWindows();
        }

        return creatures;
    }

    private bool HasRedTargetEdge(Mat frame, Rect bar, bool debug = false)
    {
        using var gray = new Mat();
        Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);

        int tileSize = _profile.TileSize;
        int barToTileOffsetY = _profile.BarToTileCenterOffsetY + 2;
        int borderX = bar.X - _profile.BarToTileCenterOffsetX - 5;
        int yOfCreatureBar = bar.Y + barToTileOffsetY - 1;        

        int adjustedWidth = tileSize - 3;
        int adjustedHeight = tileSize -3 ;

        var creatureRect = new Rect(borderX, yOfCreatureBar, adjustedWidth, adjustedHeight);
        using var roi = new Mat(gray, creatureRect);

        static bool IsRedPixel(byte v) => v >= 105 && v <= 115;

        int pixelCount = 0;

        // top and bottom edges
        for (int x = 0; x < adjustedWidth; x++)
        {
            byte top = roi.At<byte>(0, x);
            byte bottom = roi.At<byte>(adjustedHeight - 1, x);
            if (IsRedPixel(top)) { pixelCount++; }
            //if (IsRedPixel(bottom)) { pixelCount++; }
        }

        // left and right edges
        for (int y = 0; y < adjustedHeight; y++)
        {
            byte left = roi.At<byte>(y, 0);
            byte right = roi.At<byte>(y, adjustedWidth - 1);
            if (IsRedPixel(left)) { pixelCount++;}
            //if (IsRedPixel(right)) { pixelCount++; }
        }

        bool isTargeted = pixelCount > 50;

        if (debug)
        {
            Cv2.Rectangle(frame, creatureRect, isTargeted ? Scalar.Green : Scalar.Blue, 1);
            Cv2.ImShow("Target Detection ROI", frame);
            Cv2.WaitKey(0);
            Cv2.DestroyAllWindows();
        }

        return isTargeted;
    }

}
