namespace Bot.Vision.GameWindow;

using OpenCvSharp;

public sealed class GameWindowAnalyzer
{
    private readonly IClientProfile _profile;
    private Rect? _cachedGameWindow;

    public GameWindowAnalyzer()
    {
        _profile = new TDXProfile();
    }

    public Mat ExtractGameWindow(Mat frame)
    {
        var rect = DetectGameWindow(frame);
        return new Mat(frame, rect);
    }

    private Rect DetectGameWindow(Mat frame)
    {
        if (_cachedGameWindow != null)
            return _cachedGameWindow.Value;

        _cachedGameWindow = _profile.GameWindowRect;
        return _cachedGameWindow.Value;
    }
}
