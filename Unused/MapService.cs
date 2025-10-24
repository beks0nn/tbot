using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
namespace Bot.Unused;

public class MapService
{
    private readonly Dictionary<int, Mat> _floorMaps = new();

    /// <summary>
    /// Loads a floor map from disk.
    /// Key = floor number (e.g. 0, -1, -2 etc.)
    /// </summary>
    public void LoadFloorMap(int floor, string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Floor map not found: {path}");

        var map = Cv2.ImRead(path, ImreadModes.Grayscale);

        _floorMaps[floor] = map;
    }

    /// <summary>
    /// Estimate player world position by matching minimap against floor map.
    /// Returns (x, y) position in global floor map coordinates.
    /// </summary>
    public (int x, int y) EstimatePlayerPosition(Mat minimap, int floor)
    {
        if (!_floorMaps.ContainsKey(floor))
            throw new InvalidOperationException($"No map loaded for floor {floor}");

        var globalMap = _floorMaps[floor];

        var grayMini = new Mat();

        if (minimap.Channels() > 1)
            Cv2.CvtColor(minimap, grayMini, ColorConversionCodes.BGR2GRAY);
        else
            grayMini = minimap.Clone();

        Cv2.ImShow("Minimap Test", grayMini);
        Cv2.WaitKey(0); // Wait until a key is pressed
        Cv2.DestroyAllWindows();

        Cv2.ImShow("Global Test", globalMap);
        Cv2.WaitKey(0); // Wait until a key is pressed
        Cv2.DestroyAllWindows();

        using var result = new Mat();
        Cv2.MatchTemplate(globalMap, grayMini, result, TemplateMatchModes.CCoeffNormed);

        Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out var maxLoc);

        if (maxVal < 0.8)
            throw new Exception($"Failed to localize player (confidence={maxVal:F2})");

        // Player is approximately at minimap center
        int px = maxLoc.X + minimap.Width / 2;
        int py = maxLoc.Y + minimap.Height / 2;

        return (px, py);
    }
}