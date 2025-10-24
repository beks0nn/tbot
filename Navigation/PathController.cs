using Bot.Control;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Bot.Navigation;

public record WaypointData(string Type, int X, int Y, int Z, string? Dir = null);
public sealed class PathController
{
    private readonly AStar _astar = new();
    private readonly KeyMover _mover = new();

    private readonly List<Waypoint> _waypoints = new();
    private int _currentIndex = 0;
    private bool _active = false;
    private DateTime _nextAllowedStep = DateTime.MinValue;

    public bool IsActive => _active && _currentIndex < _waypoints.Count;
    public int Count => _waypoints.Count;
    public int CurrentIndex => _currentIndex;

    public IReadOnlyList<Waypoint> GetAll() => _waypoints.AsReadOnly();

    // --- Waypoint management ---

    public void AddWaypoint(Waypoint wp)
    {
        _waypoints.Add(wp);
        Console.WriteLine($"[Path] Added {wp.Type} waypoint #{_waypoints.Count}");
    }

    public void Clear()
    {
        _waypoints.Clear();
        _currentIndex = 0;
        _active = false;
        Console.WriteLine("[Path] Cleared all waypoints.");
    }

    // --- Control methods ---

    public void Start()
    {
        if (_waypoints.Count == 0)
        {
            Console.WriteLine("[Path] ⚠️ No waypoints to start.");
            return;
        }

        _currentIndex = 0;
        _active = true;
        Console.WriteLine("[Path] ▶ Navigation started.");
    }

    public void Stop()
    {
        _active = false;
        Console.WriteLine("[Path] ⏹ Navigation stopped.");
    }

    // --- Core navigation step ---

    public void Step((int x, int y, int z) player, FloorData floor)
    {
        if (!_active) return;
        if (DateTime.UtcNow < _nextAllowedStep) return;


        if (_currentIndex >= _waypoints.Count)
        {
            Console.WriteLine("[Path] ✅ Finished all waypoints.");
            _active = false;
            return;
        }

        var wp = _waypoints[_currentIndex];

        // Handle MoveWaypoint (requires pathfinding)
        if (wp is MoveWaypoint move)
        {
            if (move.IsComplete(player))
            {
                _currentIndex++;
                Console.WriteLine($"[Path] Reached waypoint #{_currentIndex}");
                return;
            }

            var path = _astar.FindPath(floor.Walkable, (player.x, player.y), (move.Target.x, move.Target.y));
            if (path.Count < 2)
                return;

            _mover.StepTowards((player.x, player.y), path[1]);
            return;
        }

        // Handle one-off action waypoints (e.g., step direction, use item, right-click)
        wp.Execute(_mover, player);
        // 🕒 Non-blocking cooldown for floor transitions (e.g. ramps)
        if (wp is StepDirectionWaypoint)
        {
            Console.WriteLine("[Path] 🕒 Floor transition – cooldown 5s for stabilization...");
            _nextAllowedStep = DateTime.UtcNow.AddSeconds(1);
        }
        _currentIndex++;
    }


    // --- Persistence (JSON) ---

    public void SaveToJson(string path)
    {
        var data = new List<WaypointData>();
        foreach (var wp in _waypoints)
        {
            switch (wp)
            {
                case MoveWaypoint m:
                    data.Add(new("Move", m.Target.x, m.Target.y, m.Target.z));
                    break;
                case StepDirectionWaypoint s:
                    data.Add(new("Step", 0, 0, 0, s.Dir.ToString()));
                    break;
            }
        }

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
        Console.WriteLine($"[Path] 💾 Saved {_waypoints.Count} waypoints to {path}");
    }

    public void LoadFromJson(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"[Path] ⚠️ File not found: {path}");
            return;
        }

        var json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize<List<WaypointData>>(json);
        if (data == null) return;

        _waypoints.Clear();
        foreach (var d in data)
        {
            switch (d.Type)
            {
                case "Move":
                    _waypoints.Add(new MoveWaypoint(d.X, d.Y, d.Z));
                    break;
                case "Step":
                    if (Enum.TryParse<Direction>(d.Dir, out var dir))
                        _waypoints.Add(new StepDirectionWaypoint(dir));
                    break;
            }
        }

        _currentIndex = 0;
        Console.WriteLine($"[Path] 📂 Loaded {_waypoints.Count} waypoints from {path}");
    }
}
