using OpenCvSharp;
using System.Collections.Generic;
using Point = OpenCvSharp.Point;

namespace Bot.Navigation;

public sealed class MinimapLocalizer
{
    private readonly Queue<Point2d> _recentCenters = new();
    private readonly Kalman2D _kf = new(dt: 1.0 / 15.0);
    private readonly Mat _resultBuffer = new();
    private readonly Mat _miniBuffer = new();

    private const int SmoothWindow = 1;
    private const double FloorSwitchThreshold = 0.6;

    public int SearchRadiusPx = 300;
    public bool Debug = false;

    private static readonly Point PlayerOffsetInMinimap = new(52, 52);
    private int _currentZ = 7;
    private bool _initialized = false;

    private (int pxX, int pxY)? _lastGoodPx;
    private int _frameCount = 0;
    private double _avgConf = 1.0;

    public PlayerPosition Locate(Mat minimap, MapRepository maps, (int pxX, int pxY)? last = null)
    {
        if (minimap.Empty() || maps == null)
            return new PlayerPosition(0, 0, _currentZ, 0);

        _frameCount++;
        bool forceFullSearch = _frameCount % 80 == 0;

        var (bestTileX, bestTileY, bestConf, bestZ) = (0, 0, 0.0, _currentZ);

        var floor = maps.Get(_currentZ);
        if (floor != null)
        {
            var searchHint = forceFullSearch ? null : (last ?? _lastGoodPx);
            (bestTileX, bestTileY, bestConf) = LocateOnFloor(minimap, floor, searchHint);
        }

        if (bestConf < FloorSwitchThreshold)
        {
            for (int offset = 1; offset <= 3; offset++)
            {
                foreach (int candidateZ in new[] { _currentZ - offset, _currentZ + offset })
                {
                    var alt = maps.Get(candidateZ);
                    if (alt == null) continue;

                    var (tx, ty, conf) = LocateOnFloor(minimap, alt, last);
                    if (conf > bestConf)
                    {
                        bestConf = conf;
                        bestTileX = tx;
                        bestTileY = ty;
                        bestZ = candidateZ;
                    }
                }
                if (bestConf >= FloorSwitchThreshold) break;
            }
            _currentZ = bestZ;
        }

        if (bestConf >= 0.7)
            _lastGoodPx = ((int)(bestTileX * maps.Get(bestZ).PxPerTile),
                           (int)(bestTileY * maps.Get(bestZ).PxPerTile));

        // rolling confidence average to detect slow drift
        _avgConf = 0.9 * _avgConf + 0.1 * bestConf;
        if (_avgConf < 0.6)
        {
            _kf.Reset();
            _initialized = false;
        }

        return new PlayerPosition(bestTileX, bestTileY, bestZ, bestConf);
    }

    private (int tileX, int tileY, double conf) LocateOnFloor(Mat minimap, FloorData floor, (int pxX, int pxY)? last)
    {
        if (floor?.Gray == null || floor.Gray.Empty())
            return (0, 0, 0);

        minimap.CopyTo(_miniBuffer);
        Cv2.Rectangle(_miniBuffer,
            new Point(PlayerOffsetInMinimap.X - 3, PlayerOffsetInMinimap.Y - 3),
            new Point(PlayerOffsetInMinimap.X + 3, PlayerOffsetInMinimap.Y + 3),
            Scalar.Black, -1);

        Point roiOffset = new(0, 0);
        Mat searchMat = floor.Gray;

        int sr = SearchRadiusPx;
        if (last.HasValue && _initialized)
        {
            sr = (int)(sr * 0.6);
            var (cx, cy) = last.Value;
            int rx = Math.Max(0, cx - sr);
            int ry = Math.Max(0, cy - sr);
            int w = Math.Min(sr * 2 + _miniBuffer.Width, floor.Gray.Width - rx);
            int h = Math.Min(sr * 2 + _miniBuffer.Height, floor.Gray.Height - ry);
            var rect = new Rect(rx, ry, w, h);
            searchMat = floor.Gray.SubMat(rect);
            roiOffset = rect.Location;
        }

        Cv2.MatchTemplate(searchMat, _miniBuffer, _resultBuffer, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(_resultBuffer, out _, out double maxVal, out _, out Point maxLoc);

        var playerCenter = new Point2d(
            roiOffset.X + maxLoc.X + PlayerOffsetInMinimap.X,
            roiOffset.Y + maxLoc.Y + PlayerOffsetInMinimap.Y);

        double conf = Math.Clamp(maxVal, 0.0, 1.0);

        UpdateKalman(playerCenter, conf);

        // adaptive ROI expansion
        if (conf < 0.6) SearchRadiusPx = Math.Min(SearchRadiusPx * 2, 600);
        else SearchRadiusPx = 300;

        var smoothed = Average(_recentCenters);
        int tileX = (int)Math.Round(smoothed.X / floor.PxPerTile);
        int tileY = (int)Math.Round(smoothed.Y / floor.PxPerTile);

        if (Debug)
            ShowDebug(floor, _miniBuffer, playerCenter, tileX, tileY, conf);

        return (tileX, tileY, conf);
    }

    private void UpdateKalman(Point2d playerCenter, double conf)
    {
        var predicted = _kf.Predict();
        double residual = Math.Sqrt(Math.Pow(playerCenter.X - predicted.X, 2) +
                                    Math.Pow(playerCenter.Y - predicted.Y, 2));

        if (residual > 400)
        {
            _kf.Reset();
            _initialized = false;
        }

        var kfUpd = _kf.Update(playerCenter, conf);
        if (!_initialized)
        {
            _kf.Update(playerCenter, conf);
            _initialized = true;
            _recentCenters.Clear();
        }

        _recentCenters.Enqueue(kfUpd);
        if (_recentCenters.Count > SmoothWindow)
            _recentCenters.Dequeue();

        if (conf < 0.2)
        {
            _kf.Reset();
            _initialized = false;
        }
    }

    private static Point2d Average(IEnumerable<Point2d> pts)
    {
        double x = 0, y = 0; int n = 0;
        foreach (var p in pts) { x += p.X; y += p.Y; n++; }
        return n > 0 ? new Point2d(x / n, y / n) : new Point2d();
    }

    private void ShowDebug(FloorData floor, Mat mini, Point2d playerCenter, int tileX, int tileY, double conf)
    {
        int padding = 200;
        var rect = new Rect(
            (int)(playerCenter.X - PlayerOffsetInMinimap.X) - padding,
            (int)(playerCenter.Y - PlayerOffsetInMinimap.Y) - padding,
            mini.Width + padding * 2,
            mini.Height + padding * 2);

        rect.X = Math.Clamp(rect.X, 0, floor.Color.Width - rect.Width);
        rect.Y = Math.Clamp(rect.Y, 0, floor.Color.Height - rect.Height);
        rect.Width = Math.Min(rect.Width, floor.Color.Width - rect.X);
        rect.Height = Math.Min(rect.Height, floor.Color.Height - rect.Y);

        using var preview = new Mat(floor.Color, rect).Clone();
        var localRect = new Rect(
            (int)(playerCenter.X - PlayerOffsetInMinimap.X - rect.X),
            (int)(playerCenter.Y - PlayerOffsetInMinimap.Y - rect.Y),
            mini.Width, mini.Height);
        Cv2.Rectangle(preview, localRect, Scalar.Lime, 2);
        Cv2.PutText(preview, $"z={_currentZ} Tile=({tileX},{tileY}) Conf={conf:F2}",
            new Point(10, 30), HersheyFonts.HersheySimplex, 1, Scalar.White, 2);
        Cv2.ImShow("Minimap Localization (Zoomed)", preview);
        Cv2.WaitKey(1);
    }

    private sealed class Kalman2D
    {
        private double x, y, vx, vy;
        private double p11 = 1, p22 = 1, p33 = 1, p44 = 1;
        private readonly double dt;
        private const double BaseQ = 0.05;
        private const double BaseR = 0.05;

        public Kalman2D(double dt) => this.dt = dt;

        public Point2d Predict()
        {
            x += vx * dt;
            y += vy * dt;
            p11 += BaseQ;
            p22 += BaseQ;
            p33 += BaseQ;
            p44 += BaseQ;
            return new Point2d(x, y);
        }

        public Point2d Update(Point2d z, double confidence = 1.0)
        {
            double r = BaseR / Math.Max(confidence, 0.05);
            double q = BaseQ / Math.Max(confidence, 0.2);
            double yx = z.X - x;
            double yy = z.Y - y;
            double kx = p11 / (p11 + r);
            double ky = p22 / (p22 + r);
            x += kx * yx;
            y += ky * yy;
            vx = (1 - kx) * vx + (kx / dt) * yx;
            vy = (1 - ky) * vy + (ky / dt) * yy;
            p11 = (1 - kx) * p11 + q;
            p22 = (1 - ky) * p22 + q;
            p33 = (1 - kx) * p33 + q;
            p44 = (1 - ky) * p44 + q;
            return new Point2d(x, y);
        }

        public void Reset()
        {
            x = y = vx = vy = 0;
            p11 = p22 = p33 = p44 = 1;
        }
    }
}
