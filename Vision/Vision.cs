using System.Drawing;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Bot.Models;

namespace Bot.Vision;

public class Vision
{
    public FrameObservations Analyze(Mat mat)
    {
        // TODO: implement template matching, color checks, etc
        // Example: detect if red pixel cluster exists (monster)
        bool monsterSeen = false;

        return new FrameObservations
        {
            MonsterVisible = monsterSeen,
            PlayerPosition = (0, 0), // stub
            CorpseVisible = false
        };
    }
}