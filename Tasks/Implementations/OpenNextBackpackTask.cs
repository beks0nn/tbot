using Bot.Control;
using OpenCvSharp;
using System;

namespace Bot.Tasks.Implementations
{
    public sealed class OpenNextBackpackTask : BotTask
    {
        private readonly IClientProfile _profile;
        private readonly MouseMover _mouse = new();
        private bool _clicked;
        private DateTime _clickTime;

        private static readonly TimeSpan PostClickDelay = TimeSpan.FromMilliseconds(400);

        public override int Priority { get; set; } = 20;

        public OpenNextBackpackTask(IClientProfile profile)
        {
            _profile = profile;
            Name = "OpenNextBackpack";
        }

        public override void OnBeforeStart(BotContext ctx)
        {
            Console.WriteLine("[Loot] Preparing to open next backpack...");
        }

        public override void Do(BotContext ctx)
        {
            if (_clicked) return;

            // Use bottom-right of backpack window rectangle
            var bp = _profile.BpRect;
            int pixelX = bp.X + bp.Width - 10; // adjust offset if needed
            int pixelY = bp.Y + bp.Height - 10;

            Console.WriteLine($"[Loot] Right-clicking backpack corner at ({pixelX},{pixelY})");
            _mouse.RightClick(pixelX, pixelY);

            _clickTime = DateTime.UtcNow;
            _clicked = true;
        }

        public override bool Did(BotContext ctx)
        {
            // short delay for UI open
            return _clicked && (DateTime.UtcNow - _clickTime) > PostClickDelay;
        }
    }
}
