using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using Point = OpenCvSharp.Point;
using Size = OpenCvSharp.Size;

namespace Bot.Unused;

public class MinimapTracker
{
    private Mat? _lastFrame;
    private Point _totalOffset = new(0, 0);
    private int _stableFrameCount = 0;
    private readonly int _requiredStableFrames = 6;

    public record TrackerResult(Point Offset, bool IsStable);

    public TrackerResult Update(Mat currentFrame)
    {
        if (_lastFrame == null)
        {
            _lastFrame = currentFrame.Clone();
            return new TrackerResult(_totalOffset, false);
        }

        // --- 1️⃣ Convert and preprocess both frames ---
        using var grayPrev = new Mat();
        using var grayCurr = new Mat();
        Cv2.CvtColor(_lastFrame, grayPrev, ColorConversionCodes.BGR2GRAY);
        Cv2.CvtColor(currentFrame, grayCurr, ColorConversionCodes.BGR2GRAY);

        Cv2.GaussianBlur(grayPrev, grayPrev, new Size(3, 3), 0);
        Cv2.GaussianBlur(grayCurr, grayCurr, new Size(3, 3), 0);
        Cv2.EqualizeHist(grayPrev, grayPrev);
        Cv2.EqualizeHist(grayCurr, grayCurr);

        // --- 2️⃣ Detect and describe features ---
        var orb = ORB.Create(500); // limit to 500 keypoints per frame
        KeyPoint[] kpPrev, kpCurr;
        using var descPrev = new Mat();
        using var descCurr = new Mat();

        orb.DetectAndCompute(grayPrev, null, out kpPrev, descPrev);
        orb.DetectAndCompute(grayCurr, null, out kpCurr, descCurr);

        if (descPrev.Empty() || descCurr.Empty())
        {
            Console.WriteLine("[Tracker] No ORB features detected — skipping frame.");
            _lastFrame = currentFrame.Clone();
            return new TrackerResult(_totalOffset, false);
        }

        // --- 3️⃣ Match descriptors ---
        var bf = new BFMatcher(NormTypes.Hamming, crossCheck: true);
        var matches = bf.Match(descPrev, descCurr);
        if (matches.Count() < 8)
        {
            Console.WriteLine("[Tracker] Too few matches — skipping frame.");
            _lastFrame = currentFrame.Clone();
            return new TrackerResult(_totalOffset, false);
        }

        // --- 4️⃣ Compute median displacement among best matches ---
        // sort by descriptor distance
        var bestMatches = matches.OrderBy(m => m.Distance).Take(matches.Count() / 2).ToList();

        var deltas = new List<Point2f>();
        foreach (var m in bestMatches)
        {
            var p1 = kpPrev[m.QueryIdx].Pt;
            var p2 = kpCurr[m.TrainIdx].Pt;
            deltas.Add(new Point2f(p2.X - p1.X, p2.Y - p1.Y));
        }

        if (deltas.Count == 0)
        {
            _lastFrame = currentFrame.Clone();
            return new TrackerResult(_totalOffset, false);
        }

        float medianDx = Median(deltas.Select(d => d.X));
        float medianDy = Median(deltas.Select(d => d.Y));

        int dx = (int)Math.Round(medianDx);
        int dy = (int)Math.Round(medianDy);

        // --- 5️⃣ Accumulate and stability detection ---
        _totalOffset.X -= dx;
        _totalOffset.Y -= dy;

        bool isStableNow = Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1;
        if (isStableNow)
            _stableFrameCount++;
        else
            _stableFrameCount = 0;

        bool isStable = _stableFrameCount >= _requiredStableFrames;

        if (isStable)
        {
            int snappedX = (int)Math.Round(_totalOffset.X / 2.0) * 2;
            int snappedY = (int)Math.Round(_totalOffset.Y / 2.0) * 2;

            if (snappedX != _totalOffset.X || snappedY != _totalOffset.Y)
            {
                Console.WriteLine($"[Tracker] Snap correction from ({_totalOffset.X},{_totalOffset.Y}) → ({snappedX},{snappedY})");
                _totalOffset.X = snappedX;
                _totalOffset.Y = snappedY;
            }
        }

        _lastFrame = currentFrame.Clone();
        return new TrackerResult(_totalOffset, isStable);
    }

    private static float Median(IEnumerable<float> values)
    {
        var arr = values.OrderBy(v => v).ToArray();
        int n = arr.Length;
        if (n == 0) return 0;
        return n % 2 == 1 ? arr[n / 2] : (arr[n / 2 - 1] + arr[n / 2]) / 2f;
    }

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
    }
}