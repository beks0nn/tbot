using OpenCvSharp;
using Point = OpenCvSharp.Point;

namespace Bot.Vision;

public static class ItemFinder
{
    public static (int X, int Y)? FindItemInArea(Mat frame, Mat targetTemplate, Rect searchRect)
    {
        using var searchArea = new Mat(frame, searchRect);
        using var result = searchArea.MatchTemplate(targetTemplate, TemplateMatchModes.CCoeffNormed);

        //Cv2.ImShow("searchArea", searchArea);
        //Cv2.WaitKey(0); // Wait until a key is pressed
        //Cv2.DestroyAllWindows();

        Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out Point maxLoc);

        if (maxVal > 0.90)
        {
            var localCenter = new Point(
                maxLoc.X + targetTemplate.Width / 2, 
                maxLoc.Y + targetTemplate.Height / 2);

            int X = searchRect.X + localCenter.X;
            int Y = searchRect.Y + localCenter.Y;
            return (X, Y);
        }
        else return null;
    }

    public static bool IsGoldStackFull(Mat frame, Mat fullStackGp, Rect backPackRect)
    {
        var rect = new Rect(
            backPackRect.X,
            backPackRect.Y,
            40, 40);

        return FindItemInArea(frame, fullStackGp, rect) != null;
    }

    public static bool IsBackpackFull(Mat frame, Mat backpackTemplate, Rect backPackRect)
    {
        var rect = new Rect(
            backPackRect.X + backPackRect.Width - 40,
            backPackRect.Y + backPackRect.Height - 40,
            40, 40);

        return FindItemInArea(frame, backpackTemplate, rect) != null;
    }

    public static bool IsBackpackEmpty(Mat frame, Mat backpackTemplate, Rect backPackRect)
    {
        var rect = new Rect(
            backPackRect.X,
            backPackRect.Y,
            40, 40);

        return FindItemInArea(frame, backpackTemplate, rect) != null;
    }
}