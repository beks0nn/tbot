using OpenCvSharp;
using System;
using System.Collections.Generic;
using Point = OpenCvSharp.Point;
using Size = OpenCvSharp.Size;

namespace Bot.Navigation
{
    //public record PlayerPosition (int X, int Y, int Floor, double Confidence);
    public sealed class MinimapLocalizer
    {
        private readonly Queue<Point2d> _recentCenters = new();
        private readonly Kalman2D _kf = new(dt: 1.0 / 15.0);

        private const int SmoothWindow = 1;
        private const double FloorSwitchThreshold = 0.6; // lower than this -> check other floors

        public int SearchRadiusPx = 300;
        public bool Debug = true;

        private static readonly Point PlayerOffsetInMinimap = new(52, 52);
        private int _currentZ = 7; // default start floor
        private bool _initialized = false;

        public PlayerPosition Locate(
            Mat minimap,
            MapRepository maps,
            (int pxX, int pxY)? last = null)
        {
            if (minimap.Empty() || maps == null)
                return new PlayerPosition(0, 0, _currentZ, 0);

            var (bestTileX, bestTileY, bestConf, bestZ) = (0, 0, 0.0, _currentZ);

            // Try current floor first
            var floor = maps.Get(_currentZ);
            if (floor != null)
            {
                (bestTileX, bestTileY, bestConf) = LocateOnFloor(minimap, floor, last);
            }

            // If confidence too low, search other floors
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

                    if (bestConf >= FloorSwitchThreshold)
                        break; // good match found
                }

                _currentZ = bestZ; // update internal floor memory
            }

            return new PlayerPosition(bestTileX, bestTileY, bestZ, bestConf);
        }

        // --- Internal localization on a single floor ---
        private (int tileX, int tileY, double conf) LocateOnFloor(
            Mat minimap,
            FloorData floor,
            (int pxX, int pxY)? last)
        {
            if (floor?.Color == null || floor.Color.Empty())
                return (0, 0, 0);

            using var miniGray = EnsureGray(minimap);
            using var mapGray = EnsureGray(floor.Color);

            // Remove player cross
            Cv2.Rectangle(miniGray,
                new Point(PlayerOffsetInMinimap.X - 3, PlayerOffsetInMinimap.Y - 3),
                new Point(PlayerOffsetInMinimap.X + 3, PlayerOffsetInMinimap.Y + 3),
                Scalar.Black, -1);

            // Define ROI if last known position is available
            Mat searchMat;
            Point roiOffset;
            int sr = SearchRadiusPx;
            if (last.HasValue)
            {
                var (cx, cy) = last.Value;
                var rx = Math.Max(0, cx - sr);
                var ry = Math.Max(0, cy - sr);
                var r = new Rect(rx, ry,
                    Math.Min(sr * 2 + miniGray.Width, mapGray.Width - rx),
                    Math.Min(sr * 2 + miniGray.Height, mapGray.Height - ry));

                searchMat = new Mat(mapGray, r);
                roiOffset = r.Location;
            }
            else
            {
                searchMat = mapGray;
                roiOffset = new Point(0, 0);
            }

            // --- Template matching ---
            using var result = new Mat();
            Cv2.MatchTemplate(searchMat, miniGray, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out Point maxLoc);

            // Compute player center
            Point2d playerCenter = new(
                roiOffset.X + maxLoc.X + PlayerOffsetInMinimap.X,
                roiOffset.Y + maxLoc.Y + PlayerOffsetInMinimap.Y);

            // --- Compute confidence ---
            double conf = Math.Clamp(maxVal, 0.0, 1.0);

            // --- Kalman smoothing (confidence-aware) ---
            if (!_initialized)
            {
                _kf.Update(playerCenter, conf);
                _kf.Update(playerCenter, conf);
                _initialized = true;
                _recentCenters.Clear();
                _recentCenters.Enqueue(playerCenter);
            }
            else
            {
                _kf.Predict();
                var kfUpd = _kf.Update(playerCenter, conf);
                _recentCenters.Enqueue(kfUpd);
                if (_recentCenters.Count > SmoothWindow)
                    _recentCenters.Dequeue();
            }

            var smoothed = Average(_recentCenters);
            int tileX = (int)Math.Round(smoothed.X / floor.PxPerTile);
            int tileY = (int)Math.Round(smoothed.Y / floor.PxPerTile);

            // --- Optional: reset if we get garbage ---
            if (conf < 0.2 && _initialized)
            {
                Console.WriteLine("[Localizer] ⚠️ Low confidence, reinitializing Kalman.");
                _kf.Reset();
                _initialized = false;
            }

            if (Debug)
                ShowDebug(floor, miniGray, playerCenter, tileX, tileY, conf);

            return (tileX, tileY, conf);
        }

        private static Mat EnsureGray(Mat src)
        {
            return src.Channels() == 1 ? src.Clone() :
                src.CvtColor(ColorConversionCodes.BGR2GRAY);
        }

        private static Point2d Average(IEnumerable<Point2d> pts)
        {
            double x = 0, y = 0; int n = 0;
            foreach (var p in pts) { x += p.X; y += p.Y; n++; }
            return n > 0 ? new Point2d(x / n, y / n) : new Point2d();
        }

        private void ShowDebug(FloorData floor, Mat miniGray, Point2d playerCenter, int tileX, int tileY, double conf)
        {
            int padding = 200;
            var rect = new Rect(
                (int)(playerCenter.X - PlayerOffsetInMinimap.X) - padding,
                (int)(playerCenter.Y - PlayerOffsetInMinimap.Y) - padding,
                miniGray.Width + padding * 2,
                miniGray.Height + padding * 2);

            rect.X = Math.Clamp(rect.X, 0, floor.Color.Width - rect.Width);
            rect.Y = Math.Clamp(rect.Y, 0, floor.Color.Height - rect.Height);
            rect.Width = Math.Min(rect.Width, floor.Color.Width - rect.X);
            rect.Height = Math.Min(rect.Height, floor.Color.Height - rect.Y);

            using var preview = new Mat(floor.Color, rect).Clone();

            var localRect = new Rect(
                (int)(playerCenter.X - PlayerOffsetInMinimap.X - rect.X),
                (int)(playerCenter.Y - PlayerOffsetInMinimap.Y - rect.Y),
                miniGray.Width,
                miniGray.Height);

            Cv2.Rectangle(preview, localRect, Scalar.Lime, 2);
            Cv2.PutText(preview, $"z={_currentZ} Tile=({tileX},{tileY}) Conf={conf:F2}",
                new Point(10, 30), HersheyFonts.HersheySimplex, 1, Scalar.White, 2);

            Cv2.ImShow("Minimap Localization (Zoomed)", preview);
            Cv2.WaitKey(1);
        }

        // --- Minimal Kalman filter ---
        private sealed class Kalman2D
        {
            private double x, y, vx, vy;
            private double p11 = 1, p22 = 1, p33 = 1, p44 = 1;
            private readonly double dt;

            // Base parameters
            private const double BaseQ = 0.05;   // process noise (movement smoothness)
            private const double BaseR = 0.05;   // measurement noise (trust in data)

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
                // Adapt noise dynamically
                // high confidence (≈1) → low R (trust measurement)
                // low confidence (≈0) → high R (smooth more)
                double r = BaseR / Math.Max(confidence, 0.05);
                double q = BaseQ * (1.0 / Math.Max(confidence, 0.2));

                // Innovation
                double yx = z.X - x;
                double yy = z.Y - y;

                // Gains
                double kx = p11 / (p11 + r);
                double ky = p22 / (p22 + r);

                // Update state
                x += kx * yx;
                y += ky * yy;

                vx = (1 - kx) * vx + (kx / dt) * yx;
                vy = (1 - ky) * vy + (ky / dt) * yy;

                // Update uncertainty
                p11 = (1 - kx) * p11 + q;
                p22 = (1 - ky) * p22 + q;
                p33 = (1 - kx) * p33 + q;
                p44 = (1 - ky) * p44 + q;

                return new Point2d(x, y);
            }
            public void Reset()
            {
                // Wipe state and covariance — as if freshly initialized
                x = y = vx = vy = 0;
                p11 = p22 = p33 = p44 = 1;
            }
        }

    }
}