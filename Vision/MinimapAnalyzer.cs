using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot.Vision;

using OpenCvSharp;

public class MinimapAnalyzer
{
    // Hardcoded for now, later configurable via settings
    private readonly Rect minimapRegion = new Rect(2349, 50, 132, 132);

    public Mat ExtractMinimap(Mat frame)
    {
        var minimap = new Mat(frame, minimapRegion);

        //Cv2.ImShow("Minimap Test", minimap);
        //Cv2.WaitKey(0); // Wait until a key is pressed
        //Cv2.DestroyAllWindows();

        return minimap;
    }
}