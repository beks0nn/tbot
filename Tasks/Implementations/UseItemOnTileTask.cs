using Bot.Control;
using Bot.Navigation;
using Bot.Vision.Tools;

namespace Bot.Tasks.Implementations;

public sealed class UseItemOnTileTask : BotTask
{
    public override int Priority { get; set; } = 1;

    private readonly Waypoint _wp;
    private readonly IClientProfile _profile;

    private readonly MouseMover _mouse = new();
    private ItemBuilder? _itemBuilder;

    private bool _itemSelected = false;
    private bool _usedItem = false;

    private (int X, int Y, int Z) _startPos;
    private int _ticksWaiting = 0;
    private const int MaxWaitTicks = 20;

    public override bool IsCritical => _usedItem;

    public bool TaskFailed { get; private set; } = false;

    public UseItemOnTileTask(Waypoint wp, IClientProfile profile)
    {
        if (wp.Type != WaypointType.UseItem)
            throw new ArgumentException("UseItemOnTileTask requires a UseItem waypoint");

        if(wp.Item == null)
            throw new ArgumentException("UseItemOnTileTask requires a waypoint with an Item specified");

        _wp = wp;
        _profile = profile;

        Name = $"Use-{wp.Item}-{wp.Dir}";
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

    public override void OnBeforeStart(BotContext ctx)
    {
        _startPos = (ctx.PlayerPosition.X, ctx.PlayerPosition.Y, ctx.PlayerPosition.Floor);
        TaskFailed = false;

        switch (_wp.Item)
        {
            case Item.Rope:
                _itemBuilder = new ItemBuilder(_profile, ctx.RopeTemplate);
                break;
            case Item.Shovel:
                _itemBuilder = new ItemBuilder(_profile, ctx.ShovelTemplate);
                break;
            default:
                throw new ArgumentException($"UseItemOnTileTask does not support item {_wp.Item}");
        }
        

        Console.WriteLine($"[Task] Use-{_wp.Item}-{_wp.Dir} from Z={_startPos.Z}");
    }

    public override void Do(BotContext ctx)
    {
        if (TaskFailed)
            return;

        if (!_itemSelected)
        {
            // Ensure alignment
            if (ctx.PlayerPosition.X != _wp.X || ctx.PlayerPosition.Y != _wp.Y)
            {
                Console.WriteLine($"[Task] Use {_wp.Item} aborted: incorrect position ({_wp.X},{_wp.Y})");
                TaskFailed = true;
                return;
            }

            // Try to find item
            var itemScreenPos = _itemBuilder!.FindItem(ctx.CurrentFrameGray);
            if (itemScreenPos == null)
            {
                Console.WriteLine($"[Task] Use {_wp.Item} failed: item not found in inventory.");
                TaskFailed = true;
                return;
            }

            // Right-click item to activate "Use with"
            _mouse.RightClick(itemScreenPos.Value.X, itemScreenPos.Value.Y);
            _itemSelected = true;

            Console.WriteLine($"[Task] {_wp.Item} selected at {itemScreenPos.Value}. Waiting to use it...");
            return;
        }

        if (!_usedItem)
        {
            var slot = ComputeTileSlot(_wp, ctx);

            if (slot.X < -3 || slot.X > 3 || slot.Y < -3 || slot.Y > 3)
            {
                Console.WriteLine($"[Task] Use {_wp.Item} aborted: tile offscreen.");
                TaskFailed = true;
                return;
            }

            _mouse.LeftClickTile(slot, _profile);
            _usedItem = true;

            Console.WriteLine($"[Task] {_wp.Item} used on tileSlot {slot}. Waiting for Z decrease...");
            return;
        }
    }

    public override bool Did(BotContext ctx)
    {
        if (!_usedItem)
            return false;

        _ticksWaiting++;

        int currentZ = ctx.PlayerPosition.Floor;

        // SUCCESS = Z decreases by exactly 1
        if (currentZ == _startPos.Z - 1)
        {
            Console.WriteLine(
                $"[Task] Use {_wp.Item} success: Z changed {_startPos.Z} → {currentZ}"
            );
            return true;
        }

        // FAILURE = Z didn't change in time
        if (_ticksWaiting > MaxWaitTicks)
        {
            Console.WriteLine($"[Task] Use {_wp.Item} FAILED: Z did not decrease.");
            TaskFailed = true;
            return true;
        }

        return false;
    }
}
