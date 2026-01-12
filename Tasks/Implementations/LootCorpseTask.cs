using OpenCvSharp;
using Bot.Control;
using Bot.State;
using Bot.Navigation;
using Bot.Vision;
using Bot.GameEntity;

namespace Bot.Tasks.Implementations;

public sealed class LootCorpseTask : BotTask
{
    public override int Priority => TaskPriority.LootCorpse;

    private readonly KeyMover _keyboard;
    private readonly MouseMover _mouse;

    private Corpse? _targetCorpse;
    private WalkToCoordinateTask? _walkSub;
    private (int x, int y, int z)? _walkGoal;

    private OpenNextBackpackTask? _openBagSub;

    private DateTime _nextStep = DateTime.MinValue;
    private DateTime _startedAt = DateTime.UtcNow;
    private bool _opened;
    private bool _looted;
    private bool _ate;
    private bool _waitedNextToCorpse;

    private static readonly TimeSpan MaxLootTime = TimeSpan.FromSeconds(10);

    private static readonly Random _rng = new();
    private static int ShortDelay = 100;
    private static int MediumDelay = 300;
    private static int LongDelay = 500;

    public LootCorpseTask(KeyMover keyboard, MouseMover mouse)
    {
        _keyboard = keyboard;
        _mouse = mouse;
        Name = "LootClosestCorpse";
    }

    public override void OnBeforeStart(BotContext ctx)
    {
        _startedAt = DateTime.UtcNow;

        _targetCorpse = ctx.Corpses.Peek();

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

        if (DateTime.UtcNow - _startedAt > MaxLootTime)
        {
            Console.WriteLine($"[Loot] Timeout — skipping corpse {_targetCorpse.X},{_targetCorpse.Y}");
            ctx.Corpses.Pop();
            Status = TaskStatus.Completed;
            return;
        }

        if (DateTime.UtcNow < _nextStep)
            return;

        // --- Movement / Open phase ---
        if (!_opened)
        {
            var player = (x: ctx.PlayerPosition.X, y: ctx.PlayerPosition.Y, z: ctx.PlayerPosition.Z);
            var corpse = (x: _targetCorpse.X, y: _targetCorpse.Y, z: player.z);

            bool adjacent = NavigationHelper.IsAdjacent(player.x, player.y, corpse.x, corpse.y);

            if (!adjacent)
            {
                var walk = NavigationHelper.BuildDynamicWalkmap(ctx);

                var bestAdjecantTile = NavigationHelper.PickBestAdjacentTile(ctx, walk, corpse.x, corpse.y);
                if (bestAdjecantTile == null)
                {
                    Console.WriteLine("[Loot] No walkable adjacent tiles near corpse.");
                    ctx.Corpses.Pop();
                    Status = TaskStatus.Completed;
                    return;
                }

                var goal = (x: bestAdjecantTile.Value.X, y: bestAdjecantTile.Value.Y, z: corpse.z);

                if (_walkSub == null || _walkGoal != goal)
                {
                    _walkGoal = goal;
                    _walkSub = new WalkToCoordinateTask(goal, _keyboard);
                }

                _walkSub.Tick(ctx);

                if (_walkSub.IsCompleted)
                {
                    _walkSub = null;
                }

                return;
            }

            // we are adjacent now - stop movement subtask
            _walkSub = null;
            _walkGoal = null;

            // dwell guard
            if (!_waitedNextToCorpse)
            {
                _waitedNextToCorpse = true;
                Console.WriteLine("[Loot] Arrived next to corpse, waiting briefly to settle.");
                _nextStep = RandomDelayFrom(LongDelay);
                return;
            }

            if (DateTime.UtcNow - _targetCorpse.DetectedAt < TimeSpan.FromMilliseconds(1000))
            {
                Console.WriteLine("Looting too soon adding some ms..");
                _nextStep = RandomDelayFrom(ShortDelay);
                return;
            }

            var relTile = (_targetCorpse.X - ctx.PlayerPosition.X, _targetCorpse.Y - ctx.PlayerPosition.Y);
            _mouse.RightClickTile(relTile, ctx.Profile);
            Console.WriteLine("[Loot] Opened corpse window.");
            _opened = true;
            _nextStep = RandomDelayFrom(LongDelay);
            return;
        }

        using var lootArea = new Mat(ctx.CurrentFrameGray, ctx.Profile.LootRect.ToCvRect());

        // --- Eating phase ---
        if (!_ate)
        {
            foreach (var food in ctx.FoodTemplates)
            {
                var itemLocation = ItemFinder.FindItemInArea(
                    lootArea,
                    food,
                    new Rect(0, 0, lootArea.Width, lootArea.Height)
                );

                if (itemLocation != null)
                {
                    int eatX = ctx.Profile.LootRect.X + itemLocation.Value.X;
                    int eatY = ctx.Profile.LootRect.Y + itemLocation.Value.Y;

                    _mouse.RightClickSlow(eatX, eatY);
                    _nextStep = RandomDelayFrom(MediumDelay);
                    break;
                }
            }

            _ate = true;
            _nextStep = RandomDelayFrom(MediumDelay);
            return;
        }

        // --- Looting phase ---
        bool foundItem = false;
        Console.WriteLine("[Loot] Checking loot area for items...");
        bool backpackEmpty = ItemFinder.IsBackpackEmpty(ctx.CurrentFrameGray, ctx.BackpackTemplate, ctx.Profile.BpRect.ToCvRect());

        foreach (var loot in ctx.LootTemplates)
        {
            var itemLocation = ItemFinder.FindItemInArea(
                lootArea,
                loot,
                new Rect(0, 0, lootArea.Width, lootArea.Height)
            );

            if (itemLocation != null)
            {
                foundItem = true;

                if (EnsureBackpackHasSpace(ctx))
                    return;

                int fromX = ctx.Profile.LootRect.X + itemLocation.Value.X;
                int fromY = ctx.Profile.LootRect.Y + itemLocation.Value.Y;

                int dropX, dropY;

                if (backpackEmpty)
                {
                    dropX = ctx.Profile.BpRect.X + ctx.Profile.BpRect.W - 20;
                    dropY = ctx.Profile.BpRect.Y + ctx.Profile.BpRect.H - 20;
                }
                else
                {
                    dropX = ctx.Profile.BpRect.X + 20;
                    dropY = ctx.Profile.BpRect.Y + 20;
                }

                _mouse.CtrlDragLeft(fromX, fromY, dropX, dropY);

                _nextStep = RandomDelayFrom(MediumDelay);
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
            ctx.Corpses.Pop();
            var relTile = (_targetCorpse.X - ctx.PlayerPosition.X, _targetCorpse.Y - ctx.PlayerPosition.Y);
            if(ctx.Corpses.Any(c => c.X == _targetCorpse.X && c.Y == _targetCorpse.Y))
            {
                _mouse.DragLeftTile(relTile, (0, 0), ctx.Profile);
            }

            Console.WriteLine($"[Loot] Done looting corpse at {_targetCorpse.X},{_targetCorpse.Y}");
            Status = TaskStatus.Completed;
        }
    }


    private bool EnsureBackpackHasSpace(BotContext ctx)
    {
        //just tick subtask if existing
        if (_openBagSub != null)
        {
            _openBagSub.Tick(ctx);

            if (_openBagSub.IsCompleted)
            {
                _openBagSub = null;
                Console.WriteLine("[Loot] Opened next backpack.");
                _nextStep = RandomDelayFrom(MediumDelay);
            }

            return true;
        }

        var bpRect = ctx.Profile.BpRect.ToCvRect();

        if (!ItemFinder.IsBackpackFull(ctx.CurrentFrameGray, ctx.BackpackTemplate, bpRect))
            return false;

        if (!ItemFinder.IsGoldStackFull(ctx.CurrentFrameGray, ctx.OneHundredGold, bpRect))
            return false;

        // Start subtask and consume tick
        _openBagSub = new OpenNextBackpackTask(ctx.Profile, _mouse);
        _openBagSub.Tick(ctx);
        return true;
    }

    private static DateTime RandomDelayFrom(int delayBase)
    {
        return DateTime.UtcNow.AddMilliseconds(delayBase + _rng.Next(-25, 101));
    }

    public override bool Did(BotContext ctx) => _looted;
}
