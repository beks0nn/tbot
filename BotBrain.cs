using Bot.MemClass;
using Bot.Navigation;
using Bot.State;
using Bot.Tasks;
using Bot.Tasks.Implementations;
using Bot.Util;
using Bot.Vision;
using Bot.Vision.CreatureDetection;
using Bot.Vision.Mana;
using OpenCvSharp;

namespace Bot;

public sealed class BotBrain(BotRuntime rt)
{
    private readonly BotRuntime _rt = rt;
    private BotContext Ctx => _rt.Ctx;
    private BotServices Svc => _rt.Svc;
    private readonly IClientProfile _clientProfile = new TDXProfile();
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

        //Mana extraction
        Ctx.Mana = _manaAnalyzer.ExtractManaPercent(gray);

        //Read memory for player and creatures
        var (memPlayer, memCreatures, corpses) = Svc.Memory.ReadEntities(Ctx.ProcessHandle, Ctx.ProcessMemoryBaseAddress);
        Ctx.Health = memPlayer.HpPercent;

        Ctx.PreviousPlayerPosition = Ctx.PlayerPosition;
        var pos = new PlayerPosition(x: memPlayer.X, memPlayer.Y, memPlayer.Z, 100);
        Ctx.PlayerPosition = pos;
        Ctx.CurrentFloor = Svc.MapRepo.Get(pos.Floor);

        var allCreatures = new List<Creature>();
        var killCreatures = new List<Creature>();

        var whitelist = new HashSet<string>
        {
            "Rat",
            "Cave Rat",
            "Snake",
            "Wolf",
            "Troll",
            "Orc",
            "Spider",
            "Poison Spider",
            "Orc Spearman",
            "Bug",
            "Rotworm",
            "Orc Warrior",
        };

        foreach (var mc in memCreatures)
        {
            // Skip creatures we decided to ignore
            if (Ctx.IgnoredCreatures.Contains(mc.Id))
                continue;

            var creature = new Creature
            {
                Id = mc.Id,
                Name = mc.Name,
                X = mc.X,
                Y = mc.Y,
                Floor = mc.Z,
                IsDead = mc.HpPercent == 0,
                TileSlot = (
                    mc.X - Ctx.PlayerPosition.X,
                    mc.Y - Ctx.PlayerPosition.Y
                ),
                IsPlayer = false,
                IsTargeted = mc.IsAttacked,
                DetectedAt = DateTime.UtcNow,
            };

            allCreatures.Add(creature);

            if (whitelist.Contains(mc.Name.Trim()))
                killCreatures.Add(creature);
        }

        //ctx.BlockingCreatures = allCreatures;
        Ctx.Creatures = killCreatures;

        foreach (var c in corpses)
        {
            if(!Ctx.Corpses.Any(corpse => c.X == corpse.X && c.Y == corpse.Y))
            {
                Ctx.Corpses.Add(new Corpse
                {
                    X = c.X,
                    Y = c.Y,
                    DetectedAt = DateTime.UtcNow,
                });
            }
        }

        if (allCreatures.Any(c => c.IsPlayer))
        {
            if ((DateTime.UtcNow - _lastPlayerAlert).TotalSeconds > 30)
            {
                _ = Task.Run(() => DiscordNotifier.SendAsync("Player on screen."));
                _lastPlayerAlert = DateTime.UtcNow;
            }
        }

        //if (ctx.RecordMode)
        //    Console.WriteLine($"[REC] ({pos.X},{pos.Y}) z={pos.Floor} Conf={pos.Confidence:F2}");

        if (_isRunning)
        {
            EvaluateAndSetRootTask();
            _orchestrator.Tick(Ctx);
        }
    }

    private void EvaluateAndSetRootTask()
    {
        BotTask? next = null;

        //hp low? heal
        // 1. Combat takes top priority
        if (Ctx.Creatures.Count > 0)
        {
            next = new AttackClosestCreatureTask(_clientProfile, Svc.Keyboard, Svc.Mouse);
        }
        // 2. cast light healing spell if mana full
        else if (Ctx.Mana >= 90)
        {
            next = new CastLightHealTask(Svc.Keyboard);
        }
        // 3. Looting corpses after combat
        else if (Ctx.Corpses.Count > 0)
        {
            next = new LootCorpseTask(_clientProfile, Svc.Keyboard, Svc.Mouse);
        }
        // 4. Path following when idle
        else if (Svc.PathRepo.Waypoints.Count > 0)
        {
            next = new FollowPathTask(Svc.PathRepo, _clientProfile, Svc.Keyboard, Svc.Mouse);
        }

        _orchestrator.MaybeReplaceRoot(next);
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
        _orchestrator.Reset();

        Console.WriteLine("[Bot] Stopped.");
    }
}
