using Bot.Tasks;
using OpenCvSharp;
using Point = OpenCvSharp.Point;

namespace Bot.Vision.Tools;

public sealed class ItemBuilder
{
    private readonly IClientProfile _clientProfile;
    private Mat _template;

    public ItemBuilder(IClientProfile profile, Mat template)
    {
        _template = template;
        _clientProfile = profile;
    }

    public (int X, int Y)? FindItem(Mat frame)
    {
        try
        {
            using var lootArea = new Mat(frame, _clientProfile.LootRect);
            var result = lootArea.MatchTemplate(_template, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out Point maxLoc);

            if (maxVal > 0.98)
            {
                var localCenter = new Point(maxLoc.X + _template.Width / 2, maxLoc.Y + _template.Height / 2);
                int X = _clientProfile.LootRect.X + localCenter.X;
                int Y = _clientProfile.LootRect.Y + localCenter.Y;
                return (X, Y);
            }
            else return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ItemBuilder] Error in FindItem: {ex}");
            throw;
        }
    }
}