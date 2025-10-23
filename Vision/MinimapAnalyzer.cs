using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot.Vision;

using OpenCvSharp;

public class MinimapAnalyzer
{
    private readonly Mat _template;
    private Rect? _cachedMinimap;
    private readonly string templatePath = "Assets/Templates/Compass.png";

    public MinimapAnalyzer()
    {
        _template = Cv2.ImRead(templatePath, ImreadModes.Color);
        if (_template.Empty())
            throw new FileNotFoundException("Compass template not found", templatePath);
    }

    public Mat ExtractMinimap(Mat frame)
    {
        var rect = DetectMinimap(frame);

        if (rect == null)
            throw new InvalidOperationException("Could not locate minimap on screen.");

        var minimap = new Mat(frame, rect.Value);

        //Cv2.ImShow("Minimap Test", minimap);
        //Cv2.WaitKey(0); // Wait until a key is pressed
        //Cv2.DestroyAllWindows();

        return minimap;
    }

    private Rect? DetectMinimap(Mat frame)
    {
        if (_cachedMinimap != null)
            return _cachedMinimap;


        // Convert to BGR
        Mat miniBgr;
        if (frame.Channels() == 4)
        {
            miniBgr = new Mat();
            Cv2.CvtColor(frame, miniBgr, ColorConversionCodes.BGRA2BGR);
        }
        else if (frame.Channels() == 1)
        {
            miniBgr = new Mat();
            Cv2.CvtColor(frame, miniBgr, ColorConversionCodes.GRAY2BGR);
        }
        else
        {
            miniBgr = frame;
        }
        frame = miniBgr;

        using var result = new Mat();
        Cv2.MatchTemplate(frame, _template, result, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out Point maxLoc);

        if (maxVal < 0.9)
        {
            Console.WriteLine($"[MinimapLocator] Compass not found (conf={maxVal:F2})");
            return null;
        }

        Console.WriteLine($"[MinimapLocator] Compass found at {maxLoc} (conf={maxVal:F2})");

        // Adjust for minimap position relative to compass
        var minimapRect = new Rect(maxLoc.X - 115, maxLoc.Y, 105, 105);
        _cachedMinimap = minimapRect;

        return minimapRect;
    }

    private void Reset() => _cachedMinimap = null;
}