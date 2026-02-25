using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bot.Navigation;

public sealed class PathRepository
{
    private readonly List<Waypoint> _waypoints = new();
    public IReadOnlyList<Waypoint> Waypoints => _waypoints.AsReadOnly();
    public int CurrentIndex { get; private set; } = 0;
    private readonly string FolderName = "Paths";

    public Waypoint? Current =>
        (CurrentIndex >= 0 && CurrentIndex < _waypoints.Count)
            ? _waypoints[CurrentIndex]
            : null;

    public void Add(Waypoint wp)
    {
        _waypoints.Add(wp);
        Console.WriteLine($"[PathRepo] Added {wp}");
    }

    public bool Advance()
    {
        if (CurrentIndex + 1 < _waypoints.Count)
        {
            CurrentIndex++;
            return true;
        }
        return false;
    }

    public bool GoBackOne()
    {
        if (CurrentIndex > 0)
        {
            CurrentIndex--;
            return true;
        }
        return false;
    }

    public void Reset() => CurrentIndex = 0;

    /// <summary>
    /// Computes the set of tiles that cause floor changes (holes, stairs).
    /// These are the destination tiles of Step/RightClick/UseItem waypoints.
    /// Used to block these tiles during normal A* pathfinding.
    /// </summary>
    public HashSet<(int X, int Y, int Z)> ComputeFloorChangeTiles()
    {
        var tiles = new HashSet<(int, int, int)>();
        foreach (var wp in _waypoints)
        {
            if (wp.Type is not (WaypointType.Step or WaypointType.RightClick or WaypointType.UseItem))
                continue;

            var (dx, dy) = wp.Dir switch
            {
                Direction.North => (0, -1),
                Direction.South => (0, 1),
                Direction.East => (1, 0),
                Direction.West => (-1, 0),
                _ => (0, 0)
            };

            if (dx != 0 || dy != 0)
                tiles.Add((wp.X + dx, wp.Y + dy, wp.Z));
        }
        return tiles;
    }

    // --- Save / Load directly as JSON ---

    public void SaveToJson(string path)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, FolderName);
        Directory.CreateDirectory(folder);
        var fullPath = Path.Combine(folder, path);

        var json = JsonSerializer.Serialize(
            _waypoints,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            });

        File.WriteAllText(fullPath, json);
        Console.WriteLine($"[PathRepo] Saved {_waypoints.Count} waypoints to {path}");
    }

    public void LoadFromJson(string path)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, FolderName);
        var fullPath = Path.Combine(folder, path);

        if (!File.Exists(fullPath))
        {
            Console.WriteLine($"[PathRepo] File not found: {path}");
            return;
        }

        var json = File.ReadAllText(fullPath);
        var data = JsonSerializer.Deserialize<List<Waypoint>>(json);
        if (data == null) return;

        _waypoints.Clear();
        _waypoints.AddRange(data);
        Reset();

        Console.WriteLine($"[PathRepo] Loaded {_waypoints.Count} waypoints from {path}");
    }
}
