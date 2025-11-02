using OpenCvSharp;

public sealed class TibiaraDXProfile : IClientProfile
{
    public string Name => "TibiaraDX";
    public int TileSize => 78;
    public (int, int) VisibleTiles => (15, 11);
    public Rect GameWindowRect => new(167, 29, 1171, 858);//new(140, 90, 570, 420);


    // --- health bar geometry ---
    public int HpBarWidth => 27;
    public int HpBarHeight => 4;
    public int HpBarThickness => 1;

    // These offsets may need a tiny recalibration later,
    // but this is a good starting scale from the smaller bar.
    public int BarToTileCenterOffsetX => 19; // scaled down from ~23
    public int BarToTileCenterOffsetY => TileSize/2 ;

    public int TargetScanOffsetX => 23;
    public int TargetScanOffsetY => 6;

    //public int TileOriginYOffset => 30;

    public int NameBandAboveBarY => 13;
    public int NameBandHeight => 11;
}
