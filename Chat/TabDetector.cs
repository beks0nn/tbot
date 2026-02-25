using OpenCvSharp;
using Bot.State;

namespace Bot.Chat;

public sealed class TabDetector
{
    /// <summary>
    /// Template-match both focused and unfocused DefaultTab images.
    /// Returns the screen-space center of the Default tab and whether it is currently focused.
    /// </summary>
    public (int ScreenX, int ScreenY, bool IsFocused)? FindDefaultTab(
        Mat frameGray, RectDto tabsRect, Mat focusedTemplate, Mat unfocusedTemplate)
    {
        if (!tabsRect.IsValid || frameGray.Empty())
            return null;

        var roi = tabsRect.ToCvRect();
        if (roi.X + roi.Width > frameGray.Width || roi.Y + roi.Height > frameGray.Height)
            return null;

        using var tabRegion = new Mat(frameGray, roi);

        var focused = MatchTemplate(tabRegion, focusedTemplate);
        var unfocused = MatchTemplate(tabRegion, unfocusedTemplate);

        const double Threshold = 0.80;

        bool useFocused;
        double bestScore;
        OpenCvSharp.Point bestLoc;

        if (focused.Score >= unfocused.Score)
        {
            useFocused = true;
            bestScore = focused.Score;
            bestLoc = focused.Location;
        }
        else
        {
            useFocused = false;
            bestScore = unfocused.Score;
            bestLoc = unfocused.Location;
        }

        if (bestScore < Threshold)
            return null;

        var tmpl = useFocused ? focusedTemplate : unfocusedTemplate;

        int screenX = tabsRect.X + bestLoc.X + tmpl.Width / 2;
        int screenY = tabsRect.Y + bestLoc.Y + tmpl.Height / 2;

        return (screenX, screenY, useFocused);
    }

    /// <summary>
    /// Count private tabs by scanning for left-edge templates at each 96px slot boundary.
    /// Returns (count, focusedIndex) where focusedIndex is the 0-based index of the
    /// focused private tab, or -1 if no private tab is focused.
    /// </summary>
    public (int Count, int FocusedIndex) CountPrivateTabs(
        Mat frameGray, RectDto tabsRect, int defaultCenterX, int tabWidth,
        Mat leftEdgeFocused, Mat leftEdgeUnfocused)
    {
        if (!tabsRect.IsValid || frameGray.Empty())
            return (0, -1);

        var roi = tabsRect.ToCvRect();
        if (roi.X + roi.Width > frameGray.Width || roi.Y + roi.Height > frameGray.Height)
            return (0, -1);

        using var tabRegion = new Mat(frameGray, roi);

        int count = 0;
        int focusedIndex = -1;
        int maxSlots = (tabsRect.W - tabWidth) / tabWidth;

        for (int i = 1; i <= maxSlots; i++)
        {
            int expectedLocalX = (defaultCenterX - tabsRect.X) - tabWidth / 2 + i * tabWidth;

            var edge = CheckEdgeAt(tabRegion, expectedLocalX, leftEdgeFocused, leftEdgeUnfocused);
            if (!edge.HasValue)
                break;

            if (edge.Value)
                focusedIndex = count;

            count++;
        }

        return (count, focusedIndex);
    }

    /// <summary>
    /// Check if a left-edge template matches at the expected X position.
    /// Returns null if no edge found, true if focused, false if unfocused.
    /// </summary>
    private static bool? CheckEdgeAt(Mat tabRegion, int expectedX, Mat edgeFocused, Mat edgeUnfocused)
    {
        const int SearchMargin = 4;
        const double Threshold = 0.75;

        int cropX = Math.Max(0, expectedX - SearchMargin);
        int cropW = edgeFocused.Width + SearchMargin * 2;
        int cropH = tabRegion.Height;

        if (cropX + cropW > tabRegion.Width || cropH < edgeFocused.Height)
            return null;

        var cropRect = new Rect(cropX, 0, cropW, cropH);
        using var crop = new Mat(tabRegion, cropRect);

        var f = MatchTemplate(crop, edgeFocused);
        var u = MatchTemplate(crop, edgeUnfocused);

        double best = Math.Max(f.Score, u.Score);
        if (best < Threshold)
            return null;

        return f.Score >= u.Score;
    }

    /// <summary>
    /// Check for red (unread indicator) pixels in a specific tab slot.
    /// </summary>
    public bool HasRedPixelsInSlot(Mat frameBgr, RectDto tabsRect, int slotCenterX, int tabWidth, int tabHeight)
    {
        int slotLeft = slotCenterX - tabWidth / 2;
        int slotTop = tabsRect.Y;
        int slotRight = slotLeft + tabWidth;
        int slotBottom = slotTop + tabHeight;

        slotLeft = Math.Max(0, slotLeft);
        slotTop = Math.Max(0, slotTop);
        slotRight = Math.Min(frameBgr.Width, slotRight);
        slotBottom = Math.Min(frameBgr.Height, slotBottom);

        if (slotRight <= slotLeft || slotBottom <= slotTop)
            return false;

        var slotRect = new Rect(slotLeft, slotTop, slotRight - slotLeft, slotBottom - slotTop);
        using var slotRegion = new Mat(frameBgr, slotRect);

        for (int y = 0; y < slotRegion.Height; y += 2)
        {
            for (int x = 0; x < slotRegion.Width; x += 2)
            {
                var pixel = slotRegion.At<Vec3b>(y, x);
                if (pixel.Item2 > 200 && pixel.Item1 < 120 && pixel.Item0 < 120)
                    return true;
            }
        }

        return false;
    }

    private static (double Score, OpenCvSharp.Point Location) MatchTemplate(Mat region, Mat template)
    {
        if (template.Width > region.Width || template.Height > region.Height)
            return (0, default);

        using var result = new Mat();
        Cv2.MatchTemplate(region, template, result, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);
        return (maxVal, maxLoc);
    }
}
