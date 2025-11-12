using Bot.Navigation;
using Bot.Tasks;
using Bot.Tasks.Implementations;
using Bot.Vision;
using Bot.Vision.CreatureDetection;
using Bot.Vision.Mana;
using OpenCvSharp;

namespace Bot;

public sealed class BotBrain
{
    private readonly IClientProfile _clientProfile = new TDXProfile();
    private readonly MapRepository _maps = new();
    private readonly MinimapLocalizer _loc = new();
    private readonly MinimapAnalyzer _minimap = new();
    private readonly GameWindowAnalyzer _gameWindow = new();
    private readonly CreatureBuilder _creatureBuilder = new();
    private readonly TaskOrchestrator _orchestrator = new();
    private readonly ManaAnalyzer _manaAnalyzer = new();

    public async Task InitializeAsync()
    {
        await Task.Run(() => _maps.LoadAll("Assets/Minimaps"));
        Console.WriteLine("[BotBrain] Minimap data loaded.");
    }

    public void ProcessFrame(Mat frame, BotContext ctx, PathRepository pathRepo)
    {
        ctx.CurrentFrame = frame;
        using var gray = new Mat();
        Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
        ctx.CurrentFrameGray = gray;

        //mana
        ctx.Mana = _manaAnalyzer.ExtractManaPercent(gray);

        //PlayerPosition
        using var mini = _minimap.ExtractMinimap(gray);
        if (mini.Empty()) return;

        var pos = _loc.Locate(mini, _maps);
        if (pos.Confidence < 0.75) return;


        ctx.PreviousPlayerPosition = ctx.PlayerPosition;
        ctx.PlayerPosition = pos;
        ctx.CurrentFloor = _maps.Get(pos.Floor);


        //creature Vision
        var gw = _gameWindow.ExtractGameWindow(gray);
        var (creatures, newCorpses) = _creatureBuilder.Build(
            gw,
            previousPlayer: (ctx.PreviousPlayerPosition.X, ctx.PreviousPlayerPosition.Y),
            currentPlayer: (ctx.PlayerPosition.X, ctx.PlayerPosition.Y),
            previousCreatures: ctx.Creatures,
            debug: false);
        ctx.Creatures = creatures;

        foreach (var corpse in newCorpses)
        {
            bool alreadyKnown = ctx.Corpses.Any(c => c.X == corpse.X && c.Y == corpse.Y);
            if (!alreadyKnown)
                ctx.Corpses.Add(corpse);
        }

        if (ctx.RecordMode)
            Console.WriteLine($"[REC] ({pos.X},{pos.Y}) z={pos.Floor} Conf={pos.Confidence:F2}");

        // Only tick the brain while running
        if (ctx.IsRunning)
        {
            EvaluateAndSetRootTask(ctx, pathRepo);
            _orchestrator.Tick(ctx);
        }
    }

    private void EvaluateAndSetRootTask(BotContext ctx, PathRepository pathRepo)
    {
        BotTask? next = null;

        //hp low? heal
        // 1. Combat takes top priority
        if (ctx.Creatures.Count > 0)
        {
            var close = ctx.Creatures
                .Where(c => c.TileSlot is { } slot && Math.Abs(slot.X) <= 3 && Math.Abs(slot.Y) <= 3 && c.IsPlayer == false)
                .ToList();

            if (close.Count > 0)
                next = new AttackClosestCreatureTask(_clientProfile);
        }
        // 2. cast light healing spell if mana full
        else if (ctx.Mana >= 90)
        {
            next = new CastLightHealTask();
        }
        // 3. Looting corpses after combat
        else if (ctx.Corpses.Count > 0)
        {
            next = new LootCorpseTask(_clientProfile, ctx);
        }
        // 4. Path following when idle
        else if (pathRepo.Waypoints.Count > 0)
        {
            next = new FollowPathTask(pathRepo);
        }

        // just pass the suggestion; orchestrator decides whether to swap
        _orchestrator.MaybeReplaceRoot(next);
    }

    public void StartBot(BotContext ctx)
    {
        if (ctx.IsRunning) return;

        ctx.IsRunning = true;
        Console.WriteLine("[Bot] Started.");
    }

    public void StopBot(BotContext ctx)
    {
        if (!ctx.IsRunning) return;

        ctx.IsRunning = false;
        _orchestrator.Reset();

        Console.WriteLine("[Bot] Stopped.");
    }
}
