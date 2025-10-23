using OpenCvSharp;
using System;
using System.Collections.Generic;
using Point = OpenCvSharp.Point;
using Size = OpenCvSharp.Size;

namespace Bot.Navigation
{
    public sealed class MinimapLocalizer
    {
        private readonly Queue<Point2d> _recentCenters = new();
        private readonly Kalman2D _kf = new(dt: 1.0 / 15.0);

        private const int SmoothWindow = 1;

        public int SearchRadiusPx = 300;       // reduced default
        public bool Debug = true;

        // True player offset within minimap (confirmed)
        private static readonly Point PlayerOffsetInMinimap = new(52, 52);

        public (int tileX, int tileY, double conf) Locate(
            Mat minimap,
            FloorData floor,
            (int pxX, int pxY)? last = null)
        {
            if (minimap.Empty() || floor?.Color == null || floor.Color.Empty())
                return (0, 0, 0);

            using var miniGray = EnsureGray(minimap);
            using var mapGray = EnsureGray(floor.Color);

            // remove player cross
            Cv2.Rectangle(miniGray,
                new Point(PlayerOffsetInMinimap.X - 3, PlayerOffsetInMinimap.Y - 3),
                new Point(PlayerOffsetInMinimap.X + 3, PlayerOffsetInMinimap.Y + 3),
                Scalar.Black, -1);

            // define ROI if last known position is available
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

            // template matching (normalized correlation)
            using var result = new Mat();
            Cv2.MatchTemplate(searchMat, miniGray, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out Point maxLoc);

            // compute player center (actual offset)
            Point2d playerCenter = new(
                roiOffset.X + maxLoc.X + PlayerOffsetInMinimap.X,
                roiOffset.Y + maxLoc.Y + PlayerOffsetInMinimap.Y);

            // Kalman smoothing
            var kfPred = _kf.Predict();
            var kfUpd = _kf.Update(playerCenter);

            _recentCenters.Enqueue(kfUpd);
            if (_recentCenters.Count > SmoothWindow)
                _recentCenters.Dequeue();
            var smoothed = Average(_recentCenters);

            int tileX = (int)Math.Round(smoothed.X / floor.PxPerTile);
            int tileY = (int)Math.Round(smoothed.Y / floor.PxPerTile);
            double conf = Math.Clamp(maxVal, 0.0, 1.0);

            if (Debug)
            {
                int padding = 200; // visible margin around the detected minimap

                var rect = new Rect(
                    (int)(playerCenter.X - PlayerOffsetInMinimap.X) - padding,
                    (int)(playerCenter.Y - PlayerOffsetInMinimap.Y) - padding,
                    miniGray.Width + padding * 2,
                    miniGray.Height + padding * 2);

                // Clamp within the floor boundaries
                rect.X = Math.Clamp(rect.X, 0, floor.Color.Width - rect.Width);
                rect.Y = Math.Clamp(rect.Y, 0, floor.Color.Height - rect.Height);
                rect.Width = Math.Min(rect.Width, floor.Color.Width - rect.X);
                rect.Height = Math.Min(rect.Height, floor.Color.Height - rect.Y);

                // Crop the area from the map
                using var preview = new Mat(floor.Color, rect).Clone();

                // Draw a simple green border where the minimap is
                var localRect = new Rect(
                    (int)(playerCenter.X - PlayerOffsetInMinimap.X - rect.X),
                    (int)(playerCenter.Y - PlayerOffsetInMinimap.Y - rect.Y),
                    miniGray.Width,
                    miniGray.Height);

                Cv2.Rectangle(preview, localRect, Scalar.Lime, 2);

                // Display only text info (coordinates + confidence)
                Cv2.PutText(preview, $"Tile=({tileX},{tileY})  Conf={conf:F2}",
                    new Point(10, 30), HersheyFonts.HersheySimplex, 1, Scalar.White, 2);

                Cv2.ImShow("Minimap Localization (Zoomed)", preview);
                Cv2.WaitKey(1);
            }

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

        private sealed class Kalman2D
        {
            private double x, y, vx, vy;
            private double p11 = 1000, p22 = 1000, p33 = 1000, p44 = 1000;
            private readonly double q = 0.25;
            private readonly double r = 1.0;
            private readonly double dt;

            public Kalman2D(double dt) => this.dt = dt;

            public Point2d Predict()
            {
                x += vx * dt; y += vy * dt;
                p11 += q; p22 += q; p33 += q; p44 += q;
                return new Point2d(x, y);
            }

            public Point2d Update(Point2d z)
            {
                double yx = z.X - x, yy = z.Y - y;
                double kx = p11 / (p11 + r), ky = p22 / (p22 + r);
                x += kx * yx; y += ky * yy;
                vx = (1 - kx) * vx + (kx / dt) * yx;
                vy = (1 - ky) * vy + (ky / dt) * yy;
                p11 *= (1 - kx); p22 *= (1 - ky);
                p33 *= (1 - kx); p44 *= (1 - ky);
                return new Point2d(x, y);
            }
        }
    }
}