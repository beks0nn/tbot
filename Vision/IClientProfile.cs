using OpenCvSharp;

public interface IClientProfile
{
    string Name { get; }
    int TileSize { get; }
    (int Width, int Height) VisibleTiles { get; }
    Rect GameWindowRect { get; }


    // Health bar geometry in *game window pixels*
    int HpBarWidth { get; }      // default 27
    int HpBarHeight { get; }     // default 4
    int HpBarThickness { get; }  // default 1

    // Mapping from bar (top-left) → approximate tile center offsets (px)
    // These are *per-client*; start with the defaults then calibrate.
    int BarToTileCenterOffsetX { get; } // default ~23 for TileSize=78 (scaled from 19 @ 64)
    int BarToTileCenterOffsetY { get; } // default 5


    int TargetScanOffsetX { get; }
    int TargetScanOffsetY { get; }

    //Offset from hp bar down to within the origin tile
    //int TileOriginYOffset { get; }

    // Name band above bar (for template matching)
    int NameBandAboveBarY { get; } // default 13
    int NameBandHeight { get; }    // default 11
}
