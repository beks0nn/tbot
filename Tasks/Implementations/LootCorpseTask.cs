using Bot.Control;
using Bot.Navigation;
using Bot.Tasks.Implementations;
using Bot.Vision;
using Bot.Vision.Loot;
using OpenCvSharp;
using System;
using System.IO;
using System.Linq;
using Point = OpenCvSharp.Point;

namespace Bot.Tasks;

public sealed class LootCorpseTask : BotTask
{
    private readonly IClientProfile _profile;
    private readonly AStar _astar = new();
    private readonly KeyMover _mover = new();
    private readonly MouseMover _mouse = new();
    private readonly BotContext _ctx;
    private readonly LootBuilder _lootBuilder = new();

    private Corpse? _targetCorpse;
    private readonly Mat[] _lootTemplates;
    private DateTime _nextStep = DateTime.MinValue;
    private DateTime _startedAt = DateTime.UtcNow;
    private bool _opened;
    private bool _looted;
    private bool _openedNextBag;

    private static readonly TimeSpan StepInterval = TimeSpan.FromMilliseconds(40);
    private static readonly TimeSpan LootDelay = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan MaxLootTime = TimeSpan.FromSeconds(3);

    public override int Priority { get; set; } = 50;

    public LootCorpseTask(IClientProfile profile, BotContext ctx)
    {
        _profile = profile;
        _ctx = ctx;
        Name = "LootClosestCorpse";

        var lootFolder = "Assets/Loot";
        _lootTemplates = Directory.GetFiles(lootFolder, "*.png")
            .Select(path => Cv2.ImRead(path, ImreadModes.Grayscale))
            .ToArray();
    }

    public override void OnBeforeStart(BotContext ctx)
    {
        _startedAt = DateTime.UtcNow;
        _targetCorpse = _ctx.Corpses
            .OrderBy(c => Math.Abs(c.X - _ctx.PlayerPosition.X) + Math.Abs(c.Y - _ctx.PlayerPosition.Y))
            .FirstOrDefault();

        if (_targetCorpse == null)
        {
            Console.WriteLine("[Loot] No corpses available.");
            Status = TaskStatus.Completed;
            return;
        }

        Console.WriteLine($"[Loot] Moving to corpse at {_targetCorpse.X},{_targetCorpse.Y}");
    }

    public override void Do(BotContext ctx)
    {
        if (_targetCorpse == null)
        {
            Status = TaskStatus.Completed;
            return;
        }

        // Timeout guard
        if (DateTime.UtcNow - _startedAt > MaxLootTime)
        {
            Console.WriteLine($"[Loot] Timeout — skipping corpse {_targetCorpse.X},{_targetCorpse.Y}");
            ctx.Corpses.RemoveAll(c => c.X == _targetCorpse.X && c.Y == _targetCorpse.Y);
            Status = TaskStatus.Completed;
            return;
        }

        if (DateTime.UtcNow < _nextStep)
            return;

        var floor = ctx.CurrentFloor;
        if (floor?.Walkable == null)
        {
            Status = TaskStatus.Completed;
            return;
        }

        var player = (ctx.PlayerPosition.X, ctx.PlayerPosition.Y);

        // --- Movement phase ---
        if (!_opened)
        {
            var dist = Math.Abs(player.X - _targetCorpse.X) + Math.Abs(player.Y - _targetCorpse.Y);
            if (dist > 1)
            {
                var path = _astar.FindPath(floor.Walkable, player, (_targetCorpse.X, _targetCorpse.Y));
                if (path.Count <= 1)
                {
                    Console.WriteLine("[Loot] Cannot reach corpse.");
                    ctx.Corpses.RemoveAll(c => c.X == _targetCorpse.X && c.Y == _targetCorpse.Y);
                    Status = TaskStatus.Completed;
                    return;
                }

                _mover.StepTowards(player, path[1]);
                _nextStep = DateTime.UtcNow.Add(StepInterval);
                return;
            }

            // wait if corpse was just detected recently
            if (DateTime.UtcNow - _targetCorpse.DetectedAt < TimeSpan.FromMilliseconds(600))
            {
                _nextStep = DateTime.UtcNow.AddMilliseconds(200);
                return;
            }

            var relTile = (_targetCorpse.X - ctx.PlayerPosition.X, _targetCorpse.Y - ctx.PlayerPosition.Y);
            var (px, py) = TileToScreenPixel(relTile, _profile);
            _mouse.RightClick(px, py);
            Console.WriteLine("[Loot] Opened corpse window.");
            _opened = true;
            _nextStep = DateTime.UtcNow.AddMilliseconds(500);
            return;
        }

        // --- Subtask: open next backpack if full ---
        using (var bp = new Mat(_ctx.CurrentFrameGray, _profile.BpRect))
        {
            var isFull = _lootBuilder.IsBackpackFull(bp);
            if (!_openedNextBag && isFull)
            {
                var openBag = new OpenNextBackpackTask(_profile);
                openBag.OnBeforeStart(ctx);
                openBag.Do(ctx);
                if (openBag.Did(ctx))
                {
                    _openedNextBag = true;
                    Console.WriteLine("[Loot] Backpack full — opened next one.");
                }
                _nextStep = DateTime.UtcNow.AddMilliseconds(400);
                return;
            }
        }

        // --- Looting phase ---
        bool foundItem = false;
        Console.WriteLine("[Loot] Checking loot area for items...");

        using var lootArea = new Mat(_ctx.CurrentFrameGray, _profile.LootRect);
        int templateIndex = 0;
        //Cv2.ImShow("Minimap Test", lootArea);
        //Cv2.WaitKey(0); // Wait until a key is pressed
        //Cv2.DestroyAllWindows();

        foreach (var tmpl in _lootTemplates)
        {
            templateIndex++;
            var result = lootArea.MatchTemplate(tmpl, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out Point maxLoc);


            Console.WriteLine($"[Loot] Template {templateIndex} match {maxVal:F2} at {maxLoc.X},{maxLoc.Y}");

            if (maxVal > 0.99)
            {
                foundItem = true;

                var localCenter = new Point(maxLoc.X + tmpl.Width / 2, maxLoc.Y + tmpl.Height / 2);
                int fromX = _profile.LootRect.X + localCenter.X;
                int fromY = _profile.LootRect.Y + localCenter.Y;
                int dropX = _profile.BpRect.X + _profile.BpRect.Width - 20;
                int dropY = _profile.BpRect.Y + _profile.BpRect.Height - 20;

                Console.WriteLine($"[Loot] Dragging from ({fromX},{fromY}) to ({dropX},{dropY})");
                _mouse.CtrlDragLeft(fromX, fromY, dropX, dropY);
                Console.WriteLine($"[Loot] Collected item ({maxVal:F2})");

                _nextStep = DateTime.UtcNow.Add(LootDelay);
                return;
            }
        }

        if (!foundItem)
        {
            Console.WriteLine("[Loot] No matching loot templates found in corpse window.");
            _looted = true;
        }

        // --- Cleanup phase ---
        if (_looted)
        {
            ctx.Corpses.RemoveAll(c => c.X == _targetCorpse.X && c.Y == _targetCorpse.Y);
            Console.WriteLine($"[Loot] Done looting corpse at {_targetCorpse.X},{_targetCorpse.Y}");
            Status = TaskStatus.Completed;
        }

        return;
    }




    public override bool Did(BotContext ctx) => _looted;

    private static (int X, int Y) TileToScreenPixel((int X, int Y) tileSlot, IClientProfile profile)
    {
        var (visibleX, visibleY) = profile.VisibleTiles;
        int centerTileX = visibleX / 2;
        int centerTileY = visibleY / 2;
        int absTileX = centerTileX + tileSlot.X;
        int absTileY = centerTileY + tileSlot.Y;
        var gameRect = profile.GameWindowRect;
        int px = gameRect.X + absTileX * profile.TileSize + profile.TileSize / 2;
        int py = gameRect.Y + absTileY * profile.TileSize + profile.TileSize / 2;
        return (px, py);
    }
}
