using Bot.Control;
using Bot.Control.Actions;
using Bot.Navigation;
using Bot.State;
using Bot.Vision;
using OpenCvSharp;

namespace Bot.Tasks.SubTasks;

public sealed class UseItemOnTileTask : SubTask
{
    private readonly Waypoint _wp;
    private readonly InputQueue _queue;
    private readonly MouseMover _mouse;
    private readonly KeyMover _keyboard;
    private readonly object _owner;

    private ActionHandle? _pending;
    private bool _itemSelected;
    private bool _usedItem;
    private (int X, int Y, int Z) _startPos;

    private int _ticksWaiting;
    private const int MaxWaitTicks = 20;

    private int _dragAttempts;
    private const int MaxDragAttempts = 3;
    private DateTime _nextDragAllowed = DateTime.MinValue;
    private static readonly Random _rng = new();
    private readonly TimeSpan DragCooldown = TimeSpan.FromMilliseconds(250 + _rng.Next(0, 100));

    private bool _didDragCleanup;

    /// <summary>
    /// True once item is selected until completion - prevents preemption
    /// (crosshair is active after item selection, and Z change must be detected).
    /// </summary>
    public bool IsCritical => (_pending != null || _itemSelected) && !IsCompleted;

    public UseItemOnTileTask(Waypoint wp, InputQueue queue, MouseMover mouse, KeyMover keyboard, object owner)
    {
        if (wp.Type != WaypointType.UseItem)
            throw new ArgumentException("UseItemOnTileTask requires a UseItem waypoint");

        if (wp.Item == null)
            throw new ArgumentException("UseItemOnTileTask requires a waypoint with an Item specified");

        _wp = wp;
        _queue = queue;
        _mouse = mouse;
        _keyboard = keyboard;
        _owner = owner;
        Name = $"Use-{wp.Item}-{wp.Dir}";
    }

    protected override void OnStart(BotContext ctx)
    {
        _startPos = (ctx.PlayerPosition.X, ctx.PlayerPosition.Y, ctx.PlayerPosition.Z);
    }

    protected override void Execute(BotContext ctx)
    {
        // Wait for pending action, then wait for fresh frame
        if (_pending != null)
        {
            if (!_pending.IsCompleted) return;
            _pending = null;
            return;
        }

        // Rope cleanup phase: drag items off the rope spot
        if (_wp.Item == Item.Rope && _didDragCleanup && _dragAttempts < MaxDragAttempts)
        {
            if (DateTime.UtcNow >= _nextDragAllowed)
            {
                var slot = ComputeTileSlot(_wp, ctx);
                Console.WriteLine($"[{Name}] Rope drag cleanup #{_dragAttempts + 1}");
                _pending = _queue.Enqueue(
                    CtrlDragAction.FromTiles(_mouse, slot, (0, 0), ctx.Profile), _owner);
                _dragAttempts++;
                _nextDragAllowed = DateTime.UtcNow + DragCooldown;
            }
            return;
        }

        // After cleanup attempts, retry rope usage
        if (_wp.Item == Item.Rope && _didDragCleanup && _dragAttempts >= MaxDragAttempts)
        {
            Console.WriteLine($"[{Name}] Rope cleanup complete, retrying");
            _didDragCleanup = false;
            _itemSelected = false;
            _usedItem = false;
            _ticksWaiting = 0;
            return;
        }

        // Phase 1: Select item from inventory
        if (!_itemSelected)
        {
            if (ctx.PlayerPosition.X != _wp.X || ctx.PlayerPosition.Y != _wp.Y)
            {
                Fail($"Incorrect position, expected ({_wp.X},{_wp.Y})");
                _queue.Enqueue(new PressKeyAction(_keyboard, KeyMover.VK_ESCAPE, ctx.GameWindowHandle), _owner);
                return;
            }

            var itemPos = ItemFinder.FindItemInArea(
                ctx.CurrentFrameGray,
                GetMyTemplate(_wp, ctx),
                ctx.Profile.ToolsRect.ToCvRect());

            if (itemPos == null)
            {
                Fail($"{_wp.Item} not found in inventory");
                return;
            }

            _pending = _queue.Enqueue(
                new RightClickScreenAction(_mouse, itemPos.Value.X, itemPos.Value.Y), _owner);
            _itemSelected = true;
            Console.WriteLine($"[{Name}] Item selected, waiting to use...");
            return;
        }

        // Phase 2: Use item on tile
        if (!_usedItem)
        {
            var slot = ComputeTileSlot(_wp, ctx);

            if (slot.X < -3 || slot.X > 3 || slot.Y < -3 || slot.Y > 3)
            {
                Fail("Tile offscreen");
                _queue.Enqueue(new PressKeyAction(_keyboard, KeyMover.VK_ESCAPE, ctx.GameWindowHandle), _owner);
                return;
            }

            _pending = _queue.Enqueue(new LeftClickTileAction(_mouse, slot, ctx.Profile), _owner);
            _usedItem = true;
            Console.WriteLine($"[{Name}] Used on tile {slot}, waiting for Z change...");
            return;
        }

        // Phase 3: Wait for Z change
        _ticksWaiting++;

        int currentZ = ctx.PlayerPosition.Z;

        // Success: Z decreased by 1
        if (currentZ == _startPos.Z - 1)
        {
            Complete();
            Console.WriteLine($"[{Name}] Success: Z changed {_startPos.Z} -> {currentZ}");
            return;
        }

        // Timeout handling
        if (_ticksWaiting > MaxWaitTicks)
        {
            // Rope special case: try cleanup before failing
            if (_wp.Item == Item.Rope && !_didDragCleanup)
            {
                Console.WriteLine($"[{Name}] Failed, attempting tile cleanup...");
                _didDragCleanup = true;
                _dragAttempts = 0;
                _itemSelected = false;
                _usedItem = false;
                _ticksWaiting = 0;
                _nextDragAllowed = DateTime.UtcNow;
                return;
            }

            Fail("Z did not change (timeout)");
            _queue.Enqueue(new PressKeyAction(_keyboard, KeyMover.VK_ESCAPE, ctx.GameWindowHandle), _owner);
        }
    }

    private static (int X, int Y) ComputeTileSlot(Waypoint wp, BotContext ctx)
    {
        int tx = wp.X;
        int ty = wp.Y;

        switch (wp.Dir)
        {
            case Direction.North: ty -= 1; break;
            case Direction.South: ty += 1; break;
            case Direction.East: tx += 1; break;
            case Direction.West: tx -= 1; break;
        }

        return (tx - ctx.PlayerPosition.X, ty - ctx.PlayerPosition.Y);
    }

    private static Mat GetMyTemplate(Waypoint wp, BotContext ctx)
    {
        return wp.Item switch
        {
            Item.Rope => ctx.RopeTemplate,
            Item.Shovel => ctx.ShovelTemplate,
            _ => throw new ArgumentException($"UseItemOnTileTask does not support item {wp.Item}"),
        };
    }
}
