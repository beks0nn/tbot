using Bot.MemClass;
using Bot.Navigation;
using Bot.Tasks;
using Bot.Tasks.Implementations;
using Bot.Util;
using Bot.Vision;
using Bot.Vision.CreatureDetection;
using Bot.Vision.Mana;
using OpenCvSharp;
using SharpGen.Runtime;
using System.CodeDom;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vortice.Mathematics;
using static System.Net.Mime.MediaTypeNames;


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
    private readonly MemHero _memHero = new();

    //[DllImport("kernel32.dll")]
    //public static extern bool ReadProcessMemory(int hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

    private DateTime _lastPlayerAlert = DateTime.MinValue;

    public async Task InitializeAsync()
    {
        await Task.Run(() => _maps.LoadAll("Assets/Minimaps"));
        Console.WriteLine("[BotBrain] Minimap data loaded.");
    }

    //public static unsafe Entity FromSpan(ReadOnlySpan<byte> span)
    //{
    //    if (span.Length < sizeof(Entity))
    //        throw new ArgumentException("Buffer too small for Entity struct");

    //    return MemoryMarshal.Read<Entity>(span);
    //}

    public void ProcessFrame(Mat frame, BotContext ctx, PathRepository pathRepo, IntPtr process)
    {
        //var entities = new List<EntityPure>();
        //var player = new EntityPure();

        //var sw = System.Diagnostics.Stopwatch.StartNew();
        //for (int i = 0; i < 600; i++)
        //{
        //    unsafe
        //    {
        //    //Entity ent = null;
        //    /*
        //        * int count = 600;
        //        int entitySize = sizeof(EntityRaw);
        //        int totalBytes = count * entitySize;
        //        byte[] bigBuffer = new byte[totalBytes];
        //        int bytesRead = 0;

        //        // single read
        //        ReadProcessMemory(process, baseAddr, bigBuffer, bigBuffer.Length, ref bytesRead);

        //        // walk through the buffer
        //        for (int i = 0; i < count; i++)
        //        {
        //        int offset = i * entitySize;
        //        EntityRaw e = FromSpan(bigBuffer.AsSpan(offset, entitySize));
        //        ...
        //        }
        //        */

        //        int bytesRead = 0;
        //        byte[] buffer = new byte[sizeof(Entity)];
        //        //+ (uint)(i * a.OffsetBetweenEntities)
        //        ReadProcessMemory(
        //            hProcess: (int)process,
        //            lpBaseAddress: ctx.BaseAddy + (int)_addys.EntityListStart + i * (int)_addys.OffsetBetweenEntities,//(int)0x005C68B0,
        //            lpBuffer: buffer,
        //            dwSize: buffer.Length,
        //            lpNumberOfBytesRead: ref bytesRead);
        //        Entity entity = FromSpan(buffer);

        //        var entName = entity.GetName();
        //        if (string.IsNullOrEmpty(entName))
        //            continue;
        //        if (entName == "Mainpromon")
        //        {
        //            player = new EntityPure()
        //            {
        //                Id = (int)entity.Id,
        //                Name = entName,
        //                X = entity.X,
        //                Y = entity.Y,
        //                Z = entity.Z,
        //                HpPercent = entity.HpPercent
        //            };
        //        }
        //        else if(entity.HpPercent > 0 && entity.HpPercent <= 100)
        //        {
        //            var newEnt = new EntityPure()
        //            {
        //                Id = (int)entity.Id,
        //                Name = entName,
        //                X = entity.X,
        //                Y = entity.Y,
        //                Z = entity.Z,
        //                HpPercent = entity.HpPercent
        //            };
        //            entities.Add(newEnt);
        //        }
        //    }
        //}

        //entities = entities.Where(
        //    e => e.Z == player.Z  && 
        //    (Math.Abs(e.X - player.X) <= 4) && 
        //    (Math.Abs(e.Y - player.Y) <= 4) &&
        //    e.Id != player.Id
        //    ).ToList();

        //sw.Stop();
        //Console.WriteLine($"[BotBrain] Entity scan found {entities.Count} entities in {sw.ElapsedMilliseconds} ms.");

        ctx.CurrentFrame = frame;
        using var gray = new Mat();
        Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
        ctx.CurrentFrameGray = gray;

        var (memPlayer, memCreatures, corpses) = _memHero.ReadEntities(process, ctx.BaseAddy);

        //mana
        ctx.Mana = _manaAnalyzer.ExtractManaPercent(gray);

        ////PlayerPosition
        //using var mini = _minimap.ExtractMinimap(gray);
        //if (mini.Empty()) return;

        //var pos = _loc.Locate(mini, _maps);
        //if (pos.Confidence < 0.75) return;

        // offsets to convert from mem coords to map coords
        // might need to be adjusted per Z index

        ctx.PreviousPlayerPosition = ctx.PlayerPosition;
        var pos = new PlayerPosition(x: memPlayer.X, memPlayer.Y, memPlayer.Z, 100);
        ctx.PlayerPosition = pos;
        ctx.CurrentFloor = _maps.Get(pos.Floor);

        //ctx.PreviousPlayerPosition = ctx.PlayerPosition;
        //ctx.PlayerPosition = pos;
        //ctx.CurrentFloor = _maps.Get(pos.Floor);


        ////creature Vision
        //var gw = _gameWindow.ExtractGameWindow(gray);
        //var (creatures, newCorpses) = _creatureBuilder.Build(
        //    gw,
        //    previousPlayer: (ctx.PreviousPlayerPosition.X, ctx.PreviousPlayerPosition.Y),
        //    currentPlayer: (ctx.PlayerPosition.X, ctx.PlayerPosition.Y),
        //    previousCreatures: ctx.Creatures,
        //    debug: false);
        //ctx.Creatures = creatures;

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

        //foreach (var corpse in newCorpses)
        //{
        //    bool alreadyKnown = ctx.Corpses.Any(c => c.X == corpse.X && c.Y == corpse.Y);
        //    if (!alreadyKnown)
        //        ctx.Corpses.Add(corpse);
        //}

        ////if (ctx.Creatures.Any(c => c.IsPlayer))
        ////{
        ////    if ((DateTime.UtcNow - _lastPlayerAlert).TotalSeconds > 30)
        ////    {
        ////        _ = Task.Run(() => DiscordNotifier.SendAsync("Player on screen."));
        ////        _lastPlayerAlert = DateTime.UtcNow;
        ////    }
        ////}

        //if (ctx.RecordMode)
        //    Console.WriteLine($"[REC] ({pos.X},{pos.Y}) z={pos.Floor} Conf={pos.Confidence:F2}");

        //// Only tick the brain while running
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
            //var close = ctx.Creatures
            //    .Where(c => c.TileSlot is { } slot && Math.Abs(slot.X) <= 3 && Math.Abs(slot.Y) <= 3 && c.IsPlayer == false)
            //    .ToList();

            //if (close.Count > 0)
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
