using Bot.Control;
using Bot.Navigation;
using Bot.Vision;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace Bot;

public sealed class BotBrain
{
    private readonly MapRepository _maps = new();
    private readonly MinimapLocalizer _loc = new();
    private readonly MinimapAnalyzer _minimap = new();
    private readonly AStar _astar = new();
    private readonly KeyMover _mover = new();

    private int _z = 7;
    private (int x, int y) _playerTile;
    private (int pxX, int pxY)? _lastMatchPx;
    private readonly List<(int x, int y, int z)> _waypoints = new();
    private bool _recordMode = false;
    private bool _running = false;
    private int _wpIndex = 0;

    public BotBrain()
    {
        _maps.LoadAll("Assets/Minimaps"); // folder with your stitched PNGs
    }

    public void ToggleRecord() => _recordMode = !_recordMode;
    public void AddWaypoint()
    {
        if (_playerTile is { } pt)
        {
            _waypoints.Add((pt.x, pt.y, _z));
            Console.WriteLine($"Added waypoint {_waypoints.Count} at {pt}");
        }
    }

    public void StartBot()
    {
        if (_waypoints.Count == 0) { Console.WriteLine("No waypoints."); return; }
        _wpIndex = 0;
        _running = true;
        Console.WriteLine("🤖 Bot started.");
    }

    public void StopBot() { _running = false; Console.WriteLine("⛔ Bot stopped."); }

    public void ProcessFrame(Bitmap frame)
    {
        using var mat = BitmapConverter.ToMat(frame);
        var mini = _minimap.ExtractMinimap(mat);


        if (mini.Empty()) return;

        var floor = _maps.Get(_z);
        if (floor == null) return;

        var (tileX, tileY, conf) = _loc.Locate(mini, floor, _lastMatchPx);
        if (conf < 0.3) return;
        _playerTile = (tileX, tileY);

        if (_recordMode) Console.WriteLine($"[REC] ({tileX},{tileY}) z={_z}");

        if (_running) StepBot(_playerTile, floor);
    }

    private void StepBot((int x, int y) player, FloorData floor)
    {
        if (_wpIndex >= _waypoints.Count) { _running = false; return; }

        var target = _waypoints[_wpIndex];
        if (target.z != _z) { Console.WriteLine("stairs TBD"); _running = false; return; }

        var path = _astar.FindPath(floor.Walkable, player, (target.x, target.y));
        if (path.Count < 2) return;

        _mover.StepTowards(player, path[1]);
        if (player == (target.x, target.y))
        {
            _wpIndex++;
            Console.WriteLine($"✅ reached waypoint #{_wpIndex}");
        }
    }
}