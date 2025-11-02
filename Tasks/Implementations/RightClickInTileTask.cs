using Bot.Control;
using Bot.Vision.CreatureDetection;
using OpenCvSharp;
using System;
using WindowsInput.Events;

namespace Bot.Tasks;

public sealed class RightClickInTileTask : BotTask
{
    public override int Priority { get; set; } = 1;
    private readonly (int X, int Y) _tileSlot;
    private readonly IClientProfile _profile;
    private readonly MouseMover _mouse; // assumes you have a mouse control abstraction
    private bool _clicked = false;
    private DateTime _clickTime;

    public TimeSpan PostClickDelay { get; init; } = TimeSpan.FromMilliseconds(250);

    public RightClickInTileTask((int X, int Y) tileSlot, IClientProfile profile)
    {
        _tileSlot = tileSlot;
        _profile = profile;
        _mouse = new MouseMover();
        Name = $"RightClickTile({tileSlot.X},{tileSlot.Y})";
    }

    public override void OnBeforeStart(BotContext ctx)
    {
        Console.WriteLine($"[Task] Preparing to right-click on tile {_tileSlot.X},{_tileSlot.Y}");
    }

    public override void Do(BotContext ctx)
    {
        if (_clicked) return;

        // Compute pixel coordinates of tile center in game window
        var gameRect = _profile.GameWindowRect;
        var (visibleX, visibleY) = _profile.VisibleTiles;

        int centerTileX = visibleX / 2;
        int centerTileY = visibleY / 2;

        int absTileX = centerTileX + _tileSlot.X;
        int absTileY = centerTileY + _tileSlot.Y;

        int pixelX = gameRect.X + (absTileX * _profile.TileSize) + _profile.TileSize / 2;
        int pixelY = gameRect.Y + (absTileY * _profile.TileSize) + _profile.TileSize / 2;

        Console.WriteLine($"[Task] Right-clicking at screen ({pixelX},{pixelY}) for tile {_tileSlot.X},{_tileSlot.Y}");

        _mouse.RightClick(pixelX, pixelY);
        _clickTime = DateTime.UtcNow;
        _clicked = true;
    }

    public override bool Did(BotContext ctx)
    {
        // Wait small delay or confirm attacking state
        bool timePassed = _clicked && (DateTime.UtcNow - _clickTime) > PostClickDelay;
        bool attacking = ctx.IsAttacking;

        if (attacking)
            Console.WriteLine("[Task] Confirmed attack initiation.");

        return timePassed || attacking;
    }
}
