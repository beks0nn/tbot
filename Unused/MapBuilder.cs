using OpenCvSharp;
using System;
using System.IO;
using Point = OpenCvSharp.Point;
using Size = OpenCvSharp.Size;

namespace Bot.Unused;

public class MapBuilder
{
    private readonly Mat _map;
    private readonly int _centerX, _centerY;
    private readonly string _mapPath;
    private Point _lastAddedOffset = new(99999,99999);
    private const int MinimapBorderCrop = 1;

    public MapBuilder(string mapPath, int width = 4096, int height = 4096)
    {
        _mapPath = mapPath;

        if (File.Exists(mapPath))
        {
            _map = Cv2.ImRead(mapPath, ImreadModes.Color);
            Console.WriteLine($"[MapBuilder] Loaded existing map from {mapPath} ({_map.Width}x{_map.Height})");
        }
        else
        {
            _map = new Mat(height, width, MatType.CV_8UC3, Scalar.Black);
            Console.WriteLine($"[MapBuilder] Created new map at {mapPath}");
        }

        _centerX = _map.Width / 2;
        _centerY = _map.Height / 2;
    }

    public void AddMinimap(Mat minimap, int offsetX, int offsetY)
    {

        if(_lastAddedOffset.X == offsetX && _lastAddedOffset.Y == offsetY)
        {
            return;
        }

        _lastAddedOffset = new Point(offsetX, offsetY);

        Console.WriteLine($"[Builder] Drawing stable minimap at offset {_lastAddedOffset}");

        // Convert to BGR
        Mat miniBgr;
        if (minimap.Channels() == 4)
        {
            miniBgr = new Mat();
            Cv2.CvtColor(minimap, miniBgr, ColorConversionCodes.BGRA2BGR);
        }
        else if (minimap.Channels() == 1)
        {
            miniBgr = new Mat();
            Cv2.CvtColor(minimap, miniBgr, ColorConversionCodes.GRAY2BGR);
        }
        else
        {
            miniBgr = minimap;
        }

        // Crop small border (use the same constant everywhere)
        var innerRect = new Rect(MinimapBorderCrop, MinimapBorderCrop,
            miniBgr.Width - 2 * MinimapBorderCrop, miniBgr.Height - 2 * MinimapBorderCrop);
        using var miniCropped = new Mat(miniBgr, innerRect);

        // NOTE: do not shift offsets here
        var roi = new Rect(_centerX + offsetX, _centerY + offsetY,
                           miniCropped.Width, miniCropped.Height);
        roi.X = Math.Clamp(roi.X, 0, _map.Width - miniCropped.Width);
        roi.Y = Math.Clamp(roi.Y, 0, _map.Height - miniCropped.Height);
        miniCropped.CopyTo(new Mat(_map, roi));

        Save(_mapPath);
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Cv2.ImWrite(path, _map);
    }

    public Mat GetCurrentMap() => _map;

    public Point? FindInitialOffset(Mat currentMinimap, string existingMapPath)
    {
        if (!File.Exists(existingMapPath))
            return null;

        using var mapColor = Cv2.ImRead(existingMapPath, ImreadModes.Color);
        if (mapColor.Empty())
        {
            Console.WriteLine($"[MapService] Failed to load map: {existingMapPath}");
            return null;
        }

        // --- Preprocess ---
        // Convert both to grayscale
        var grayMap = new Mat();
        var grayMini = new Mat();
        Cv2.CvtColor(mapColor, grayMap, ColorConversionCodes.BGR2GRAY);
        Cv2.CvtColor(currentMinimap, grayMini, ColorConversionCodes.BGR2GRAY);

        // Crop inner region to remove noisy edges (e.g. player marker / borders)
        if (grayMini.Width > MinimapBorderCrop * 2 && grayMini.Height > MinimapBorderCrop * 2)
        {
            grayMini = new Mat(grayMini, new Rect(
                MinimapBorderCrop, MinimapBorderCrop,
                grayMini.Width - 2 * MinimapBorderCrop,
                grayMini.Height - 2 * MinimapBorderCrop));
        }

        // Apply Gaussian blur to reduce pixel-level differences
        Cv2.GaussianBlur(grayMap, grayMap, new Size(3, 3), 0);
        Cv2.GaussianBlur(grayMini, grayMini, new Size(3, 3), 0);

        // Histogram equalization improves contrast invariance
        Cv2.EqualizeHist(grayMap, grayMap);
        Cv2.EqualizeHist(grayMini, grayMini);

        // --- Template match ---
        using var result = new Mat();
        Cv2.MatchTemplate(grayMap, grayMini, result, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out Point maxLoc);

        Console.WriteLine($"[MapService] Match confidence: {maxVal:F3} at {maxLoc}");

        if (maxVal < 0.6)
        {
            Console.WriteLine($"[MapService] Weak correlation, skipping resume.");
            // Optional: save debug images
            Cv2.ImWrite("Assets/Test/Debug_minimap.png", currentMinimap);
            Cv2.ImWrite("Assets/Test/Debug_map.png", mapColor);
            Cv2.ImWrite("Assets/Test/Debug_result.png", result);
            return null;
        }

        Console.WriteLine($"[MapService] Found minimap on existing map at {maxLoc}, conf={maxVal:F2}");
        return maxLoc;
    }
}
