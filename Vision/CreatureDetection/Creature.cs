using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Point = OpenCvSharp.Point;

namespace Bot.Vision.CreatureDetection;

public sealed class Creature
{
    public Point BarCenter { get; init; }
    public Rect BarRect { get; init; }
    public Rect NameRect { get; init; }
    public (int X, int Y)? TileSlot { get; init; }
    public string? Name { get; set; }
    public bool IsPlayer { get; set; }
    public bool IsTargeted { get; set; }
}