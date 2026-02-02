using OpenCvSharp;
using Bot.State;
using Bot.Tasks;
using Bot.Util;
using Bot.Vision.Mana;
using Bot.Navigation;
using Bot.Tasks.RootTasks;
using Bot.GameEntity;

namespace Bot;

public sealed class BotBrain(BotRuntime rt)
{
    private readonly BotRuntime _rt = rt;
    private BotContext Ctx => _rt.Ctx;
    private BotServices Svc => _rt.Svc;

    private readonly TaskOrchestrator _orchestrator = new();
    private readonly ManaAnalyzer _manaAnalyzer = new();

    private bool _isRunning = false;
    private DateTime _lastPlayerAlert = DateTime.MinValue;

    public void ProcessFrame(Mat frame)
    {
        Ctx.CurrentFrame?.Dispose();
        Ctx.CurrentFrameGray?.Dispose();

        Ctx.CurrentFrame = frame;
        var gray = new Mat();
        Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
        Ctx.CurrentFrameGray = gray;

        var (player, creatures, corpses) = Svc.Memory.ReadEntities(
            Ctx.ProcessHandle,
            Ctx.ProcessMemoryBaseAddress,
            Ctx.Profile.PlayerName);

        Ctx.Health = player.HpPercent;
        Ctx.Mana = _manaAnalyzer.ExtractManaPercent(gray);

        Ctx.PreviousPlayerPosition = Ctx.PlayerPosition;
        Ctx.PlayerPosition = new PlayerPosition(x: player.X, player.Y, player.Z, 100);
        Ctx.CurrentFloor = Svc.MapRepo.Get(Ctx.PlayerPosition.Z);

        var vettedCreatures = new List<Creature>();
        var unknownCreatures = new List<Creature>();
        foreach (var c in creatures)
        {
            if (c.Z != player.Z) continue;
            if (Math.Abs(c.X - player.X) > 4) continue;
            if (Math.Abs(c.Y - player.Y) > 4) continue;
            if (Ctx.IgnoredCreatures.Contains(c.Id)) continue;


            if (c.IsWhitelisted)
            {
                vettedCreatures.Add(c);
            } 
            else
            {
                unknownCreatures.Add(c);
            }
                
        }

        //Ctx.BlockingCreatures = creatures;
        Ctx.Creatures = vettedCreatures;

        foreach (var c in corpses)
        {
            if (Ctx.Corpses.Contains(c) == false) 
            {
                Ctx.Corpses.Push(c);
            }
        }

        if (unknownCreatures.Count > 0)
        {
            if ((DateTime.UtcNow - _lastPlayerAlert).TotalSeconds > 30)
            {
                _lastPlayerAlert = DateTime.UtcNow;
                _ = DiscordNotifier.PlayerOnScreenAsync(unknownCreatures, Ctx.Profile.DiscordWebhookUrl);
            }
        }

        //if (ctx.RecordMode)
        //    Console.WriteLine($"[REC] ({pos.X},{pos.Y}) z={pos.Floor} Conf={pos.Confidence:F2}");
        if (_isRunning == false) return;

        EvaluateAndSetRootTask();
        _orchestrator.Tick(Ctx);

    }

    private void EvaluateAndSetRootTask()
    {
        BotTask? next = null;

        // 0. Emergency healing with UH when health is critical (highest priority)
        if (Ctx.Health < 50 && !UseUhTask.IsDisabled && Ctx.UhTemplate != null)
        {
            next = new UseUhTask(Svc.Mouse);
        }
        // 1. Combat
        else if (Ctx.Creatures.Count > 0)
        {
            next = new AttackClosestCreatureTask(Svc.Keyboard, Svc.Mouse);
        }
        // 2. cast light healing spell if mana full
        else if (Ctx.Mana >= 94)
        {
            next = new CastLightHealTask(Svc.Keyboard);
        }
        // 3. Looting corpses after combat
        else if (Ctx.Corpses.Count > 0)
        {
            next = new LootCorpseTask(Svc.Keyboard, Svc.Mouse);
        }
        // 4. Path following when idle
        else if (Svc.PathRepo.Waypoints.Count > 0)
        {
            next = new FollowPathTask(Svc.PathRepo, Svc.Keyboard, Svc.Mouse);
        }

        _orchestrator.MaybeReplaceRoot(next, Ctx);
    }

    public void StartBot()
    {
        if (_isRunning) return;

        _isRunning = true;
        Console.WriteLine("[Bot] Started.");
    }

    public void StopBot()
    {
        if (_isRunning) return;

        _isRunning = false;
        Console.WriteLine("[Bot] Stopped.");
    }
}
