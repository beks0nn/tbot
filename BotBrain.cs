using Bot.Models;
using Bot.Navigation;
using Bot.Vision;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Drawing;
using System.IO;

namespace Bot;

public class BotBrain
{
    private readonly Vision.Vision _vision;
    private readonly MinimapAnalyzer _minimap = new();
    private readonly MapService _mapService = new();

    private readonly MapBuilder _builder;
    private readonly MinimapTracker _tracker = new();
    private readonly string FloorZeroMapPath = "Assets/Test/floor0_build.png";

    public BotBrain(Capture.CaptureService capture, Bitmap firstFrameBitmap)
    {
        _builder = new MapBuilder(FloorZeroMapPath);

        _vision = new Vision.Vision();
        //start on base floor -7 later add all on startup
        _mapService.LoadFloorMap(-7, "Assets/Map/floor-7.png");

        var firstMinimap = _minimap.ExtractMinimap(BitmapConverter.ToMat(firstFrameBitmap));

        //Cv2.ImShow("map", firstMinimap);
        //Cv2.WaitKey(0); // Wait until a key is pressed
        //Cv2.DestroyAllWindows();

        var resumeOffset = _builder.FindInitialOffset(firstMinimap, FloorZeroMapPath);


        if (resumeOffset != null)
        {
            // Normalize from top-left coordinate to center-origin
            var centerBased = new OpenCvSharp.Point(
                resumeOffset.Value.X - (_builder.GetCurrentMap().Width / 2),
                resumeOffset.Value.Y - (_builder.GetCurrentMap().Height / 2)
            );

            _tracker.SetOffset(centerBased);
            Console.WriteLine($"[Resume] Normalized offset to {centerBased}");
        }
    }

    public void ProcessFrame(Bitmap bmp)
    {
        using var mat = BitmapConverter.ToMat(bmp);
        var minimap = _minimap.ExtractMinimap(mat);
        //var playerPos = _mapService.EstimatePlayerPosition(minimap, -7);
        //Console.WriteLine($"Player at {playerPos}");

        Cv2.ImShow("Minimap Crop", minimap);
        Cv2.WaitKey(1);
        // Update tracker
        var offset = _tracker.Update(minimap);
        Console.WriteLine($"Minimap offset X: {offset.X} Y: {offset.Y}");

        //Cv2.ImShow("map", minimap);
        //Cv2.WaitKey(0); // Wait until a key is pressed
        //Cv2.DestroyAllWindows();
        // Add to builder
        _builder.AddMinimap(minimap, offset.X, offset.Y);


        // Optional: save periodically
        //if (DateTime.Now.Second % 10 == 0)
        _builder.Save(FloorZeroMapPath);

        //var map = new MapBuilder(512, 512);
        //Cv2.ImShow("map", map.GetCurrentMap());
        //Cv2.WaitKey(0); // Wait until a key is pressed
        //Cv2.DestroyAllWindows();

        //var mini = Cv2.ImRead("Assets/test_mini.png", ImreadModes.Grayscale);
        //Cv2.ImShow("mini", mini);
        //Cv2.WaitKey(0); // Wait until a key is pressed
        //Cv2.DestroyAllWindows();


        //map.AddMinimap(mini, 100, 100);
        //map.AddMinimap(mini, 0, 0);
        //map.Save("Assets/Test/test_map.png");



        FrameObservations obs = _vision.Analyze(mat);

        // TODO: make decisions based on observations
        if (obs.MonsterVisible)
        {
            // Attack monster
        }
        else if (obs.CorpseVisible)
        {
            // Open loot
        }
    }
}