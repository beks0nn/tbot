using Bot.Control;
using Bot.Navigation;
using Bot.Tasks;
using Bot.Vision;
using Bot.Vision.CreatureDetection;
using OpenCvSharp;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Bot;

public sealed class BotBrain
{
    private readonly MapRepository _maps = new();
    private readonly MinimapLocalizer _loc = new();
    private readonly MinimapAnalyzer _minimap = new();
    private readonly GameWindowAnalyzer _gameWindow = new();
    private readonly CreatureBuilder _creatureBuilder = new();
    private readonly TaskOrchestrator _orchestrator = new();
    private readonly PathRepository _pathRepo = new();
    private readonly BotContext _ctx = new();

    private BotTask? _activeRootTask;
    private readonly IntPtr _tibiaHandle;

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);

    public BotBrain()
    {
        _maps.LoadAll("Assets/Minimaps");

        var tibia = Process.GetProcessesByName("TibiaraDX").FirstOrDefault();
        if (tibia == null || tibia.MainWindowHandle == IntPtr.Zero)
            throw new InvalidOperationException("⚠️ Could not find TibiaraDX process.");

        _tibiaHandle = tibia.MainWindowHandle;

        Console.WriteLine("[Bot] Attached to Tibia window handle.");
    }

    public void ProcessFrame(Mat frame)
    {
        if (ShouldSuspend()) return;

        using var mini = _minimap.ExtractMinimap(frame);
        if (mini.Empty()) return;

        using var gray = new Mat();
        Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
        var gw = _gameWindow.ExtractGameWindow(gray);

        var creatures = _creatureBuilder.Build(gw, debug: false);
        _ctx.Creatures = creatures;
       

        var pos = _loc.Locate(mini, _maps);
        if (pos.Confidence < 0.75) return;

        // Update context
        _ctx.PlayerPosition = pos;
        _ctx.CurrentFloor = _maps.Get(pos.Floor);

        if (_ctx.RecordMode)
            Console.WriteLine($"[REC] ({pos.X},{pos.Y}) z={pos.Floor} Conf={pos.Confidence:F2}");

        // Only tick the brain while running
        if (_ctx.IsRunning)
        {
            EvaluateAndSetRootTask();
            _orchestrator.Tick(_ctx);
        }
    }

    private void EvaluateAndSetRootTask()
    {
        // 🩹 Healing > 🗡️ Combat > 💰 Loot > 🧭 Walk
        BotTask? next = null;

        // Example placeholders (to re-enable later):
        // if (_ctx.NeedsHealing) next = new HealTask();
        // else if (_ctx.Monsters.Count > 0) next = new AttackClosestCreatureTask();
        // else if (_ctx.CorpsesToLoot.Count > 0) next = new LootCorpseTask(_ctx.CorpsesToLoot.First());

        // Otherwise, follow navigation path if available
        if (_ctx.Creatures.Count > 0) next = next = new AttackClosestCreatureTask(new TibiaraDXProfile());
        else if (_pathRepo.Waypoints.Count > 0)
            next = new FollowPathTask(_pathRepo);

        // Switch root task only if the type changes
        if (next?.GetType() != _activeRootTask?.GetType())
        {
            _activeRootTask = next;
            _orchestrator.SetRoot(_activeRootTask);
        }
    }

    public void StartBot()
    {
        if (_ctx.IsRunning) return;

        _ctx.IsRunning = true;
        Console.WriteLine("[Bot] ▶ Started.");
    }

    public void StopBot()
    {
        if (!_ctx.IsRunning) return;

        _ctx.IsRunning = false;
        _orchestrator.Reset();

        Console.WriteLine("[Bot] ⏹ Stopped.");
    }

    private bool ShouldSuspend()
    {
        var active = GetForegroundWindow();
        if (active != _tibiaHandle) return true;
        if (IsIconic(_tibiaHandle)) return true;
        return false;
    }

    public void ToggleRecord()
    {
        _ctx.RecordMode = !_ctx.RecordMode;
        Console.WriteLine($"[Bot] Record mode: {(_ctx.RecordMode ? "ON" : "OFF")}");
    }

    // ---- Waypoint management ----

    public void AddWaypoint()
    {
        if (!_ctx.PlayerPosition.IsValid)
        {
            Console.WriteLine("[Bot] ⚠️ Cannot add waypoint – player position unknown.");
            return;
        }

        _pathRepo.Add(new Waypoint(
            WaypointType.Move,
            _ctx.PlayerPosition.X,
            _ctx.PlayerPosition.Y,
            _ctx.PlayerPosition.Floor));
    }

    public void AddRamp(Direction dir)
    {
        if (!_ctx.PlayerPosition.IsValid)
        {
            Console.WriteLine("[Bot] ⚠️ Cannot add ramp – player position unknown.");
            return;
        }

        _pathRepo.Add(new Waypoint(
            WaypointType.Step,
            _ctx.PlayerPosition.X,
            _ctx.PlayerPosition.Y,
            _ctx.PlayerPosition.Floor,
            dir));
    }

    public void SavePath(string path) => _pathRepo.SaveToJson(path);
    public void LoadPath(string path) => _pathRepo.LoadFromJson(path);

    public (List<(string Type, string Info)> Waypoints, int CurrentIndex) GetWaypoints()
    {
        var list = _pathRepo.Waypoints
            .Select(wp => (
                wp.Type.ToString(),
                wp.Type == WaypointType.Move
                    ? $"({wp.X},{wp.Y},{wp.Z})"
                    : $"{wp.Dir}"
            ))
            .ToList();

        return (list, _pathRepo.CurrentIndex);
    }
}
