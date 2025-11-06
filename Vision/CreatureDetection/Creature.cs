using Point = OpenCvSharp.Point;
using Rect = OpenCvSharp.Rect;

namespace Bot.Vision.CreatureDetection;

public sealed class Creature
{
    public Point BarCenter { get; init; }
    public Rect BarRect { get; init; }
    public Rect NameRect { get; init; }
    public (int X, int Y)? TileSlot { get; set; }  // mutable for prediction updates
    public string? Name { get; set; }
    public bool IsPlayer { get; set; }
    public bool IsTargeted { get; set; }

    // --- New tracking fields ---
    public (int X, int Y)? PreviousTile { get; set; }
    public (int X, int Y)? Direction { get; set; }
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime DetectedAt { get; set; }
}