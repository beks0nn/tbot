using OpenCvSharp;
namespace Bot.Vision;

public sealed class BackpackAnalyzer
{
    private readonly IClientProfile _profile;

    public BackpackAnalyzer()
    {
        _profile = new TibiaraDXProfile();
    }

    public Mat ExtractArea(Mat frame)
    {
        return new Mat(frame, _profile.BpRect);
    }
}