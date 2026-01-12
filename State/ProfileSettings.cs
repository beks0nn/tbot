
using OpenCvSharp;

namespace Bot.State;

public sealed class ProfileSettings
{
    public string ProfileName { get; set; } = "default";
    public string PlayerName { get; set; } = "";
    public string DiscordWebhookUrl { get; set; } = "";

    public RectDto? GameWindowRect { get; set; }
    public RectDto? BpRect { get; set; }
    public RectDto? LootRect { get; set; }
    public RectDto? ToolsRect { get; set; }
    public RectDto? UhRect { get; set; }

    public bool IsReady =>
        !string.IsNullOrWhiteSpace(PlayerName) &&
        GameWindowRect?.IsValid == true &&
        BpRect?.IsValid == true &&
        LootRect?.IsValid == true &&
        ToolsRect?.IsValid == true &&
        UhRect?.IsValid == true;

    public string[] Missing()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(PlayerName)) missing.Add(nameof(PlayerName));
        if (string.IsNullOrWhiteSpace(DiscordWebhookUrl)) missing.Add(nameof(DiscordWebhookUrl));
        if (GameWindowRect?.IsValid != true) missing.Add(nameof(GameWindowRect));
        if (BpRect?.IsValid != true) missing.Add(nameof(BpRect));
        if (LootRect?.IsValid != true) missing.Add(nameof(LootRect));
        if (ToolsRect?.IsValid != true) missing.Add(nameof(ToolsRect));
        if (UhRect?.IsValid != true) missing.Add(nameof(UhRect));
        return missing.ToArray();
    }

    public int TileSize
    {
        get
        {
            if (GameWindowRect?.IsValid != true)
                return 0;

            var (tilesX, tilesY) = VisibleTiles;

            var tileW = GameWindowRect.W / tilesX;
            var tileH = GameWindowRect.H / tilesY;

            return Math.Min(tileW, tileH);
        }
    }

    public (int, int) VisibleTiles => (15, 11);
}

public sealed class RectDto
{
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; }
    public int H { get; set; }
    public bool IsValid => W > 0 && H > 0;

    public Rect ToCvRect() => new Rect(X, Y, W, H);
    public Rectangle ToRectangle() => new Rectangle(X, Y, W, H);
    public static RectDto FromRectangle(Rectangle r) => new RectDto { X = r.X, Y = r.Y, W = r.Width, H = r.Height };

    public override string ToString() => IsValid ? $"{X},{Y} {W}x{H}" : "(not set)";
}
