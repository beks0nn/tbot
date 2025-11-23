using Bot.Control;
using Bot.Navigation;
using Bot.Tasks.Implementations;
using Bot.Vision.Loot;
using OpenCvSharp;
using Point = OpenCvSharp.Point;

namespace Bot.Tasks
{
    public sealed class LootCorpseTask : BotTask
    {
        private readonly IClientProfile _profile;
        private readonly AStar _astar = new();
        private readonly KeyMover _mover = new();
        private readonly MouseMover _mouse = new();
        private readonly BotContext _ctx;
        private readonly LootBuilder _lootBuilder = new();

        private Corpse? _targetCorpse;

        private DateTime _nextStep = DateTime.MinValue;
        private DateTime _startedAt = DateTime.UtcNow;
        private bool _opened;
        private bool _looted;
        private bool _ate;
        private bool _openedNextBag;
        private bool _waitedNextToCorpse;

        private static readonly TimeSpan StepInterval = TimeSpan.FromMilliseconds(40);
        private static readonly TimeSpan LootDelay = TimeSpan.FromMilliseconds(300);
        private static readonly TimeSpan MaxLootTime = TimeSpan.FromSeconds(10);

        public override int Priority { get; set; } = 50;

        public LootCorpseTask(IClientProfile profile, BotContext ctx)
        {
            _profile = profile;
            _ctx = ctx;
            Name = "LootClosestCorpse";
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

            var floor = _ctx.CurrentFloor;
            if (floor?.Walkable == null)
            {
                Status = TaskStatus.Completed;
                return;
            }

            // quick reachability test
            var player = (_ctx.PlayerPosition.X, _ctx.PlayerPosition.Y);
            var walk = NavigationHelper.BuildDynamicWalkmap(ctx);
            var adjacent = GetAdjacentWalkableTiles(walk, _targetCorpse.X, _targetCorpse.Y);

            bool reachable = false;
            foreach (var adj in adjacent)
            {

                var path = _astar.FindPath(walk, player, adj);
                //var path = _astar.FindPath(floor.Walkable, player, adj);
                if (path.Count > 0)
                {
                    reachable = true;
                    break;
                }
            }

            if (!reachable)
            {
                Console.WriteLine($"[Loot] Corpse at {_targetCorpse.X},{_targetCorpse.Y} unreachable — removing.");
                ctx.Corpses.RemoveAll(c => c.X == _targetCorpse.X && c.Y == _targetCorpse.Y);
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


            var player = (ctx.PlayerPosition.X, ctx.PlayerPosition.Y);

            // --- Movement phase ---
            if (!_opened)
            {
                int dist = Math.Abs(player.X - _targetCorpse.X) + Math.Abs(player.Y - _targetCorpse.Y);

                // If not adjacent, move to nearest walkable tile around corpse
                if (dist > 1)
                {
                    var walk = NavigationHelper.BuildDynamicWalkmap(ctx);
                    var adjacent = GetAdjacentWalkableTiles(walk, _targetCorpse.X, _targetCorpse.Y);
                    if (adjacent.Count == 0)
                    {
                        Console.WriteLine("[Loot] No walkable adjacent tiles near corpse.");
                        ctx.Corpses.RemoveAll(c => c.X == _targetCorpse.X && c.Y == _targetCorpse.Y);
                        Status = TaskStatus.Completed;
                        return;
                    }

                    // pick nearest adjacent tile
                    var best = adjacent.OrderBy(p => Math.Abs(p.X - player.X) + Math.Abs(p.Y - player.Y)).First();

                    
                    var path = _astar.FindPath(walk, player, (best.X, best.Y));
                    //var path = _astar.FindPath(floor.Walkable, player, (best.X, best.Y));
                    if (path.Count <= 1)
                    {
                        Console.WriteLine("[Loot] Cannot reach adjacent tile near corpse.");
                        ctx.Corpses.RemoveAll(c => c.X == _targetCorpse.X && c.Y == _targetCorpse.Y);
                        Status = TaskStatus.Completed;
                        return;
                    }

                    _mover.StepTowards(player, path[1], ctx.GameWindowHandle);
                    _nextStep = DateTime.UtcNow.Add(StepInterval);
                    return;
                }

                // wait abit before looting new corpse
                if (DateTime.UtcNow - _targetCorpse.DetectedAt < TimeSpan.FromMilliseconds(1300))
                {
                    Console.WriteLine("Looting to soon adding some ms..");
                    _nextStep = DateTime.UtcNow.AddMilliseconds(100);
                    return;
                }

                // *** dwell guard here ***
                if (!_waitedNextToCorpse)
                {
                    _waitedNextToCorpse = true;
                    Console.WriteLine("[Loot] Arrived next to corpse, waiting briefly to settle.");
                    _nextStep = DateTime.UtcNow.AddMilliseconds(500);  // tune 300-600 ms
                    return;
                }


                var relTile = (_targetCorpse.X - ctx.PlayerPosition.X, _targetCorpse.Y - ctx.PlayerPosition.Y);
                _mouse.RightClickTile(relTile, _profile);
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

            using var lootArea = new Mat(_ctx.CurrentFrameGray, _profile.LootRect);

            // --- Eating phase ---
            if (_ate == false)
            {
                foreach (var food in _ctx.FoodTemplates)
                {
                    var result = lootArea.MatchTemplate(food, TemplateMatchModes.CCoeffNormed);
                    Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out Point maxLoc);

                    if (maxVal > 0.98)
                    {
                        var localCenter = new Point(maxLoc.X + food.Width / 2, maxLoc.Y + food.Height / 2);
                        int eatX = _profile.LootRect.X + localCenter.X;
                        int eatY = _profile.LootRect.Y + localCenter.Y;

                        Console.WriteLine($"[Loot] Found food ({maxVal:F2}) — right-clicking to eat at ({eatX},{eatY})");
                        _mouse.RightClickSlow(eatX, eatY);
                        _nextStep = DateTime.UtcNow.AddMilliseconds(300);
                        break;
                    }
                }
                _ate = true;
                _nextStep = DateTime.UtcNow.AddMilliseconds(300);
                return; // exit to wait before continuing
            }

            // --- Looting phase ---
            bool foundItem = false;
            Console.WriteLine("[Loot] Checking loot area for items...");

            
            int templateIndex = 0;
            foreach (var tmpl in _ctx.LootTemplates)
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
                    //int dropX = _profile.BpRect.X + _profile.BpRect.Width - 20;
                    //int dropY = _profile.BpRect.Y + _profile.BpRect.Height - 20;
                    int dropX, dropY;
                    using (var bp = new Mat(_ctx.CurrentFrameGray, _profile.BpRect))
                    {
                        bool empty = _lootBuilder.IsBackpackEmpty(bp);
                        

                        if (!empty)
                        {
                            // Drop item in top-left slot instead
                            dropX = _profile.BpRect.X + 20;
                            dropY = _profile.BpRect.Y + 20;
                            //Console.WriteLine("[Loot] Backpack not empty — dropping into top-left slot.");
                        }
                        else
                        {
                            // Normal bottom-right drop
                            dropX = _profile.BpRect.X + _profile.BpRect.Width - 20;
                            dropY = _profile.BpRect.Y + _profile.BpRect.Height - 20;
                        }
                    }

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
        }

        public override bool Did(BotContext ctx) => _looted;

        private static List<(int X, int Y)> GetAdjacentWalkableTiles(bool[,] map, int x, int y)
        {
            var result = new List<(int, int)>();
            int h = map.GetLength(0);
            int w = map.GetLength(1);
            var dirs = new (int dx, int dy)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };

            foreach (var d in dirs)
            {
                int nx = x + d.dx;
                int ny = y + d.dy;
                if (nx >= 0 && ny >= 0 && nx < w && ny < h && map[ny, nx])
                    result.Add((nx, ny));
            }

            return result;
        }
    }
}
