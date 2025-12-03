using Bot.MemClass;
using Bot.Navigation;
using Bot.Tasks;
using Bot.Tasks.Implementations;
using Bot.Util;
using Bot.Vision;
using Bot.Vision.CreatureDetection;
using Bot.Vision.Mana;
using OpenCvSharp;

namespace Bot;

public sealed class BotBrain
{
    private readonly IClientProfile _clientProfile = new TDXProfile();
    private readonly MapRepository _maps = new();
    private readonly TaskOrchestrator _orchestrator = new();
    private readonly ManaAnalyzer _manaAnalyzer = new();
    private readonly MemHero _memHero = new();
    private bool _isRunning = false;

    private DateTime _lastPlayerAlert = DateTime.MinValue;

    public async Task InitializeAsync()
    {
        await Task.Run(() => _maps.LoadAll("Assets/Minimaps"));
        Console.WriteLine("[BotBrain] Minimap data loaded.");
    }

    public void ProcessFrame(Mat frame, BotContext ctx, PathRepository pathRepo)
    {
        ctx.CurrentFrame?.Dispose();
        ctx.CurrentFrame = frame;

        using var gray = new Mat();
        Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);

        ctx.CurrentFrameGray?.Dispose();
        ctx.CurrentFrameGray = gray;

        //Mana extraction
        ctx.Mana = _manaAnalyzer.ExtractManaPercent(gray);

        //Read memory for player and creatures
        var (memPlayer, memCreatures, corpses) = _memHero.ReadEntities(ctx.ProcessHandle, ctx.ProcessMemoryBaseAddress);
        ctx.Health = memPlayer.HpPercent;

        ctx.PreviousPlayerPosition = ctx.PlayerPosition;
        var pos = new PlayerPosition(x: memPlayer.X, memPlayer.Y, memPlayer.Z, 100);
        ctx.PlayerPosition = pos;
        ctx.CurrentFloor = _maps.Get(pos.Floor);

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
            if (ctx.IgnoredCreatures.Contains(mc.Id))
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
                    mc.X - ctx.PlayerPosition.X,
                    mc.Y - ctx.PlayerPosition.Y
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
        ctx.Creatures = killCreatures;

        foreach (var c in corpses)
        {
            if(!ctx.Corpses.Any(corpse => c.X == corpse.X && c.Y == corpse.Y))
            {
                ctx.Corpses.Add(new Corpse
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
            next = new FollowPathTask(pathRepo, _clientProfile);
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
