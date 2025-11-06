using OpenCvSharp;
using Rect = OpenCvSharp.Rect;

namespace Bot.Vision.Loot;

public sealed class LootBuilder
{
    private readonly Mat backpackTemplate;
    private readonly IClientProfile _clientProfile;
    public LootBuilder()
    {
        backpackTemplate = Cv2.ImRead("Assets/Tools/Backpack.png", ImreadModes.Grayscale);
        _clientProfile = new TibiaraDXProfile();
    }

    public bool IsBackpackFull(Mat backpackImage)
    {
        var bottomRight = new Mat(backpackImage, new Rect(
            _clientProfile.BpRect.Width - 40,
            _clientProfile.BpRect.Height - 40,
            40, 40));

        var result = bottomRight.MatchTemplate(backpackTemplate, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out double maxVal);

        return maxVal > 0.90;
    }
}
