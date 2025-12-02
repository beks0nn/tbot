using OpenCvSharp;
using Vortice.Mathematics;

namespace Bot.Navigation;

public sealed class MapRepository
{
    private readonly Dictionary<int, FloorData> _floors = new();

    public IReadOnlyDictionary<int, FloorData> Floors => _floors;

    public void LoadAll(string folder)
    {
        foreach (var colorPath in Directory.GetFiles(folder, "map_color_floor_*.png"))
        {
            var zStr = Path.GetFileNameWithoutExtension(colorPath)
                .Split('_').Last();
            if (!int.TryParse(zStr, out var z)) continue;

            var costPath = Path.Combine(folder, $"map_cost_floor_{z}.png");
            if (!File.Exists(costPath))
            {
                Console.WriteLine($"Missing cost map for floor {z}");
                continue;
            }

            var color = Cv2.ImRead(colorPath, ImreadModes.Color);
            var cost = Cv2.ImRead(costPath, ImreadModes.Color);

            _floors[z] = new FloorData(z, color, cost);

            Console.WriteLine($"Loaded floor {z} ({color.Width}×{color.Height})");
        }
    }

    public FloorData? Get(int z) => _floors.TryGetValue(z, out var f) ? f : null;

    public void SaveWalkmap(FloorData data, string folder)
    {
        int h = data.Walkable.GetLength(0);
        int w = data.Walkable.GetLength(1);

        var costPath = Path.Combine(folder, $"map_cost_floor_{data.Z}.png");
        var cost = Cv2.ImRead(costPath, ImreadModes.Color);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool walk = data.Walkable[y, x];
                // Yellow = blocked, Black = walkable
                var color = walk ? new Vec3b(130, 130, 130) : new Vec3b(0, 255, 255);
                cost.Set(y, x, color);
            }

        Cv2.ImWrite(costPath, cost);
    }
}