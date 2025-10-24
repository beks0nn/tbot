using Bot.Control;
using Bot.Navigation;
using Bot.Vision;
using OpenCvSharp;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Bot;

public sealed class BotBrain
{
    private readonly MapRepository _maps = new();
    private readonly MinimapLocalizer _loc = new();
    private readonly MinimapAnalyzer _minimap = new();
    private readonly PathController _path = new();

    private PlayerPosition _playerPosition;

    private bool _recordMode = false;
    private bool _running = false;


    private readonly IntPtr _tibiaHandle;

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd); // detects minimized window

    public BotBrain()
    {
        _maps.LoadAll("Assets/Minimaps");

        var tibia = Process.GetProcessesByName("TibiaraDX").FirstOrDefault();
        if (tibia == null || tibia.MainWindowHandle == IntPtr.Zero)
            throw new InvalidOperationException("⚠️ Could not find TibiaraDX process.");
        _tibiaHandle = tibia.MainWindowHandle;

        Console.WriteLine("[Bot] ✅ Attached to Tibia window handle.");
    }

    // --- Main processing loop ---
    public void ProcessFrame(Mat frame)
    {
        // 🧠 Check window focus before doing any work
        if (ShouldSuspend())
        {
            //Console.WriteLine("[Bot] ⏸ Suspended (Tibia not active or minimized).");
            Thread.Sleep(500);
            return;
        }


        using var mini = _minimap.ExtractMinimap(frame);
        if (mini.Empty()) return;

        var playerPosition = _loc.Locate(mini, _maps);
        if (playerPosition.Confidence < 0.75)
            return;

        _playerPosition = playerPosition;

        if (_recordMode)
            Console.WriteLine($"[REC] ({playerPosition.X},{playerPosition.Y}) z={playerPosition.Floor} Conf={playerPosition.Confidence:F2}");

        if (_running)
            StepBot((playerPosition.X, playerPosition.Y), _maps.Get(playerPosition.Floor));
    }

    private bool ShouldSuspend()
    {
        var active = GetForegroundWindow();
        if (active != _tibiaHandle) return true;
        if (IsIconic(_tibiaHandle)) return true; // minimized
        return false;
    }

    public void ToggleRecord() => _recordMode = !_recordMode;

    public void AddWaypoint()
    {
        if (!_playerPosition.IsValid)
        {
            Console.WriteLine("[Bot] ⚠️ Cannot add waypoint – player position unknown.");
            return;
        }

        // default: move-to waypoint
        _path.AddWaypoint(new MoveWaypoint(_playerPosition.X, _playerPosition.Y, _playerPosition.Floor));
    }

    public void AddRamp(Direction dir)
    {
        _path.AddWaypoint(new StepDirectionWaypoint(dir));
    }


    public void StartBot()
    {
        _path.Start();
        _running = true;
    }

    public void StopBot()
    {
        _path.Stop();
        _running = false;
    }

    // --- Navigation / movement ---
    private void StepBot((int x, int y) player, FloorData floor)
    {
        _path.Step((player.x, player.y, floor.Z), floor);
    }

    public void SavePath(string path) => _path.SaveToJson(path);
    public void LoadPath(string path) => _path.LoadFromJson(path);

    public (List<(string Type, string Info)> Waypoints, int CurrentIndex) GetWaypoints()
    {
        var list = new List<(string, string)>();
        foreach (var wp in _path.GetAll())
        {
            switch (wp)
            {
                case MoveWaypoint m: list.Add(("Move", $"({m.Target.x},{m.Target.y},{m.Target.z})")); break;
                case StepDirectionWaypoint s: list.Add(("Step", s.Dir.ToString())); break;
                default: list.Add((wp.Type, "")); break;
            }
        }
        return (list, _path.CurrentIndex);
    }
}
