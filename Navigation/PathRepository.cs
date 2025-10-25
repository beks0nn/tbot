using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bot.Navigation;

public sealed class PathRepository
{
    private readonly List<Waypoint> _waypoints = new();
    public IReadOnlyList<Waypoint> Waypoints => _waypoints.AsReadOnly();
    public int CurrentIndex { get; private set; } = 0;

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

    public void Reset() => CurrentIndex = 0;

    // --- 💾 Save / Load directly as JSON ---

    public void SaveToJson(string path)
    {
        var json = JsonSerializer.Serialize(
            _waypoints,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            });

        File.WriteAllText(path, json);
        Console.WriteLine($"[PathRepo] 💾 Saved {_waypoints.Count} waypoints to {path}");
    }

    public void LoadFromJson(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"[PathRepo] ⚠️ File not found: {path}");
            return;
        }

        var json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize<List<Waypoint>>(json);
        if (data == null) return;

        _waypoints.Clear();
        _waypoints.AddRange(data);
        Reset();

        Console.WriteLine($"[PathRepo] 📂 Loaded {_waypoints.Count} waypoints from {path}");
    }
}
