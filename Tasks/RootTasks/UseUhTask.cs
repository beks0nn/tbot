using Bot.Control;
using Bot.Control.Actions;
using Bot.State;
using Bot.Vision;
using OpenCvSharp;

namespace Bot.Tasks.RootTasks;

public sealed class UseUhTask : BotTask
{
    public override int Priority => TaskPriority.UseUh;
    public override bool IsCritical => true;

    private readonly InputQueue _queue;
    private readonly MouseMover _mouse;
    private ActionHandle? _pending;

    private enum Phase { FindUh, UseUh, WaitClick, OpenBackpack, WaitBackpack, RecheckUh, Done }
    private Phase _phase = Phase.FindUh;

    private (int X, int Y)? _uhPosition;
    private int _recheckAttempts;

    private static DateTime _disabledUntil = DateTime.MinValue;
    private static readonly Random _rng = new();

    private static readonly TimeSpan DisableDuration = TimeSpan.FromMinutes(5);
    private readonly TimeSpan UseCooldown = TimeSpan.FromMilliseconds(360 + _rng.Next(0, 104));

    public static bool IsDisabled => DateTime.UtcNow < _disabledUntil;
    public static void ResetCooldown() => _disabledUntil = DateTime.MinValue;

    public UseUhTask(InputQueue queue, MouseMover mouse)
    {
        _queue = queue;
        _mouse = mouse;
        Name = "UseUh";
    }

    protected override void OnStart(BotContext ctx)
    {
        Console.WriteLine("[Task] UseUh - Health critical, looking for UH rune");
    }

    protected override void Execute(BotContext ctx)
    {
        // Wait for pending action to complete, then wait for fresh frame
        if (_pending != null)
        {
            if (!_pending.IsCompleted) return;
            _pending = null;
            return;
        }

        var uhRect = ctx.Profile.UhRect;
        if (uhRect == null || !uhRect.IsValid)
        {
            Console.WriteLine("[UseUh] UhRect not configured");
            Complete();
            return;
        }

        var searchRect = GetTopLeftSlotRect(uhRect);

        switch (_phase)
        {
            case Phase.FindUh:
                _uhPosition = ItemFinder.FindItemInArea(ctx.CurrentFrameGray, ctx.UhTemplate, searchRect);
                if (_uhPosition != null)
                {
                    Console.WriteLine($"[UseUh] Found UH at ({_uhPosition.Value.X}, {_uhPosition.Value.Y})");
                    _phase = Phase.UseUh;
                }
                else
                {
                    var bpPos = ItemFinder.FindItemInArea(ctx.CurrentFrameGray, ctx.BackpackTemplate, searchRect);
                    if (bpPos != null)
                    {
                        Console.WriteLine("[UseUh] No UH found, but found backpack - opening it");
                        _uhPosition = bpPos;
                        _phase = Phase.OpenBackpack;
                    }
                    else
                    {
                        Console.WriteLine("[UseUh] No UH and no backpack found - disabling for 5 minutes");
                        _disabledUntil = DateTime.UtcNow + DisableDuration;
                        Complete();
                    }
                }
                break;

            case Phase.UseUh:
                _pending = _queue.EnqueueFront(
                    new RightClickScreenAction(_mouse, _uhPosition!.Value.X, _uhPosition.Value.Y), this);
                _phase = Phase.WaitClick;
                break;

            case Phase.WaitClick:
                // Left-click on player tile (0,0) to use rune on self
                _pending = _queue.EnqueueFront(
                    new LeftClickTileAction(_mouse, (0, 0), ctx.Profile), this);
                Console.WriteLine("[UseUh] Rune used on player");
                _disabledUntil = DateTime.UtcNow + UseCooldown;
                _phase = Phase.Done;
                break;

            case Phase.OpenBackpack:
                _pending = _queue.EnqueueFront(
                    new RightClickScreenAction(_mouse, _uhPosition!.Value.X, _uhPosition.Value.Y), this);
                _phase = Phase.WaitBackpack;
                break;

            case Phase.WaitBackpack:
                _phase = Phase.RecheckUh;
                break;

            case Phase.RecheckUh:
                _recheckAttempts++;
                _uhPosition = ItemFinder.FindItemInArea(ctx.CurrentFrameGray, ctx.UhTemplate, searchRect);
                if (_uhPosition != null)
                {
                    Console.WriteLine($"[UseUh] Found UH after opening backpack at ({_uhPosition.Value.X}, {_uhPosition.Value.Y})");
                    _phase = Phase.UseUh;
                }
                else if (_recheckAttempts < 3)
                {
                    var bpPos = ItemFinder.FindItemInArea(ctx.CurrentFrameGray, ctx.BackpackTemplate, searchRect);
                    if (bpPos != null)
                    {
                        Console.WriteLine($"[UseUh] Found nested backpack, opening (attempt {_recheckAttempts})");
                        _uhPosition = bpPos;
                        _phase = Phase.OpenBackpack;
                    }
                    else
                    {
                        Console.WriteLine("[UseUh] No UH found after opening backpack - disabling for 5 minutes");
                        _disabledUntil = DateTime.UtcNow + DisableDuration;
                        _phase = Phase.Done;
                    }
                }
                else
                {
                    Console.WriteLine("[UseUh] Max recheck attempts reached - disabling for 5 minutes");
                    _disabledUntil = DateTime.UtcNow + DisableDuration;
                    _phase = Phase.Done;
                }
                break;

            case Phase.Done:
                Complete();
                break;
        }
    }

    private static Rect GetTopLeftSlotRect(RectDto uhRect)
    {
        return new Rect(uhRect.X, uhRect.Y, 40, 40);
    }

    protected override void OnComplete(BotContext ctx)
    {
        Console.WriteLine("[Task] UseUh complete");
    }
}
