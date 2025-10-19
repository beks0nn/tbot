using OpenCvSharp;
using System;
using Point = OpenCvSharp.Point;
using Size = OpenCvSharp.Size;

namespace Bot.Navigation;

/// <summary>
/// Detects how the minimap has shifted between consecutive frames.
/// Used for incremental map building.
/// </summary>
public class MinimapTracker
{
    private Mat? _lastFrame;
    private Point _totalOffset = new(0, 0);
    private Point _lastStableOffset = new(0, 0);
    private readonly double _confidenceThreshold;
    private readonly int _searchMargin;

    // For detecting "standing still" frames
    private int _stableFrameCount = 0;
    private readonly int _requiredStableFrames = 3; // how many identical frames before we trust stability

    public MinimapTracker(double confidenceThreshold = 0.8, int searchMargin = 15)
    {
        _confidenceThreshold = confidenceThreshold;
        _searchMargin = searchMargin;
    }

    /// <summary>
    /// Update the tracker with a new minimap frame and return the accumulated world offset.
    /// </summary>
    public Point Update(Mat currentFrame)
    {
        if (_lastFrame == null)
        {
            _lastFrame = currentFrame.Clone();
            return _totalOffset;
        }

        // Convert to grayscale for matching
        using var grayPrev = new Mat();
        using var grayCurr = new Mat();
        Cv2.CvtColor(_lastFrame, grayPrev, ColorConversionCodes.BGR2GRAY);
        Cv2.CvtColor(currentFrame, grayCurr, ColorConversionCodes.BGR2GRAY);

        // Optional blur to reduce noise
        Cv2.GaussianBlur(grayPrev, grayPrev, new Size(3, 3), 0);
        Cv2.GaussianBlur(grayCurr, grayCurr, new Size(3, 3), 0);

        int w = grayPrev.Width;
        int h = grayPrev.Height;
        int patchSize = Math.Min(w, h) - _searchMargin * 2;
        var centerPatchRect = new Rect(_searchMargin, _searchMargin, patchSize, patchSize);
        using var patch = new Mat(grayPrev, centerPatchRect);

        using var result = new Mat();
        Cv2.MatchTemplate(grayCurr, patch, result, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out Point maxLoc);

        if (maxVal < _confidenceThreshold)
        {
            Console.WriteLine($"[Tracker] Low confidence ({maxVal:F2}), skipping frame");
            _lastFrame = currentFrame.Clone();
            return _totalOffset;
        }

        int dx = maxLoc.X - _searchMargin;
        int dy = maxLoc.Y - _searchMargin;


        // Detect if player is standing still (map identical)
        bool isStable = (dx == 0 && dy == 0);
        if (isStable)
            _stableFrameCount++;
        else
            _stableFrameCount = 0;

        // Only accumulate when movement has stopped for a few frames
        if (_stableFrameCount >= _requiredStableFrames)
        {
            // Update offset once the map stabilizes
            _lastStableOffset = _totalOffset;
        }
        else
        {
            // Update offset continuously (map scrolls opposite to movement)
            _totalOffset.X -= dx;
            _totalOffset.Y -= dy;
        }

        Console.WriteLine($"[Tracker] Δ=({dx},{dy})  Total=({_totalOffset.X},{_totalOffset.Y})  conf={maxVal:F2}");

        // Debug display
        var debug = currentFrame.Clone();
        Cv2.Rectangle(debug, new Rect(maxLoc.X, maxLoc.Y, patch.Width, patch.Height), Scalar.Red, 1);
        Cv2.ImShow("Tracker Debug", debug);
        Cv2.WaitKey(1);

        _lastFrame = currentFrame.Clone();
        return _totalOffset;
    }

    public Point CurrentOffset => _totalOffset;

    public void Reset()
    {
        _lastFrame?.Dispose();
        _lastFrame = null;
        _totalOffset = new Point(0, 0);
        _stableFrameCount = 0;
    }

    public void SetOffset(Point offset)
    {
        _totalOffset = offset;
        _lastStableOffset = offset;
    }
}