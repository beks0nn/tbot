using OpenCvSharp;

namespace Bot.Vision;

public sealed class LootAreaAnalyzer
{
    private readonly IClientProfile _profile;

    public LootAreaAnalyzer()
    {
        _profile = new TibiaraDXProfile();
    }

    public Mat ExtractArea(Mat frame)
    {
        return new Mat(frame, _profile.LootRect);
    }
}
