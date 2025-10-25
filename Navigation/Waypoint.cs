using Bot.Control;
using System.Text.Json.Serialization;

namespace Bot.Navigation;

public sealed class Waypoint
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WaypointType Type { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Direction Dir { get; set; } = Direction.None;

    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }

    public string? Item { get; set; }

    public Waypoint() { } // required for JSON

    public Waypoint(WaypointType type, int x, int y, int z, Direction dir = Direction.None, string? item = null)
    {
        Type = type;
        X = x;
        Y = y;
        Z = z;
        Dir = dir;
        Item = item;
    }

    public override string ToString() =>
        Type switch
        {
            WaypointType.Move => $"Move ({X},{Y},{Z})",
            WaypointType.Step => $"Step {Dir}",
            WaypointType.UseItem => $"Use {Item ?? "?"} {Dir}",
            WaypointType.RightClick => $"RightClick {Dir}",
            _ => Type.ToString()
        };
}

public enum WaypointType
{
    Move,
    Step,
    UseItem,
    RightClick
}

public enum Direction
{
    None,
    North,
    South,
    East,
    West
}
