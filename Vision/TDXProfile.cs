using OpenCvSharp;

namespace Bot.Vision;

public sealed class TDXProfile : IClientProfile
{
    public string Name => "OtName";
    public int TileSize => 78;
    public (int, int) VisibleTiles => (15, 11);
    public Rect GameWindowRect => new(167, 29, 1171, 858);//new(140, 90, 570, 420);
    public Rect BpRect => new(1514, 429, 146, 183);
    public Rect LootRect => new(1510,623,160,300);


    // --- health bar geometry ---
    public int HpBarWidth => 27;
    public int HpBarHeight => 4;
    public int HpBarThickness => 1;

    // These offsets may need a tiny recalibration later,
    // but this is a good starting scale from the smaller bar.
    public int BarToTileCenterOffsetX => TileSize / 2;
    public int BarToTileCenterOffsetY => TileSize / 2;

    public int TargetScanOffsetX => 23;
    public int TargetScanOffsetY => 6;

    //public int TileOriginYOffset => 30;

    public int NameBandAboveBarY => 13;
    public int NameBandHeight => 11;
}
