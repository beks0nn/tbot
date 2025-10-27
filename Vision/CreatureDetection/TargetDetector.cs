using OpenCvSharp;
using System.Collections.Generic;

namespace Bot.Vision.CreatureDetection;

public sealed class TargetDetector
{
    private readonly IClientProfile _profile;

    public TargetDetector()
    {
        _profile = new TibiaraDXProfile();
    }

    /// <summary>
    /// Determines whether a given creature tile has a red targeting frame.
    /// </summary>
    public bool IsTargeted(Mat gameWindow, Rect bar)
    {
        int tileSize = _profile.TileSize;
        // approximate creature region centered below the health bar
        int x = bar.X + bar.Width / 2 - tileSize / 2;
        int y = bar.Y + bar.Height + 3; // just below HP bar
        var tileRect = new Rect(
            Math.Max(0, x),
            Math.Max(0, y),
            Math.Min(tileSize, gameWindow.Width - x),
            Math.Min(tileSize, gameWindow.Height - y)
        );

        using var roi = new Mat(gameWindow, tileRect);
        using var mask = new Mat();

        // keep only strong red pixels
        Cv2.InRange(roi, new Scalar(0, 0, 150), new Scalar(70, 70, 255), mask);

        int count = Cv2.CountNonZero(mask);
        double ratio = (double)count / (tileRect.Width * tileRect.Height);

        // If more than ~2–3% of pixels are strong red, it’s targeted
        return ratio > 0.02;
    }
}
