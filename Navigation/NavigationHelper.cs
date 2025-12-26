using Bot.State;

namespace Bot.Navigation;

public static class NavigationHelper
{
    public static bool[,] BuildDynamicWalkmap(BotContext ctx)
    {
        var walk = (bool[,])ctx.CurrentFloor.Walkable.Clone();

        foreach (var c in ctx.BlockingCreatures)
        {
            if (!c.IsDead)
            {
                int x = c.X;
                int y = c.Y;

                if (y >= 0 && x >= 0 &&
                    y < walk.GetLength(0) &&
                    x < walk.GetLength(1))
                {
                    walk[y, x] = false; // block creature tile
                }
            }
        }
        return walk;
    }

    public static (int X, int Y)? PickBestAdjacentTile(BotContext ctx, bool[,] walk, int targetX, int targetY)
    {
        int h = walk.GetLength(0);
        int w = walk.GetLength(1);

        (int X, int Y)? best = null;
        int bestScore = int.MaxValue;

        foreach (var d in Adj8)
        {
            int nx = targetX + d.dx;
            int ny = targetY + d.dy;

            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
            if (!walk[ny, nx]) continue;
            if (IsOccupiedByCreature(ctx, nx, ny)) continue;

            // Chebyshev distance to player (diagonal-aware)
            int score = Math.Max(Math.Abs(nx - ctx.PlayerPosition.X), Math.Abs(ny - ctx.PlayerPosition.Y));
            if (score < bestScore)
            {
                bestScore = score;
                best = (nx, ny);
            }
        }

        return best;
    }

    public static bool IsAdjacent(int px, int py, int tx, int ty) =>
        Math.Abs(px - tx) <= 1 && Math.Abs(py - ty) <= 1;

    public static readonly (int dx, int dy)[] Adj8 =
    {
        (-1,-1), (0,-1), (1,-1),
        (-1, 0),         (1, 0),
        (-1, 1), (0, 1), (1, 1),
    };

    public static bool IsOccupiedByCreature(BotContext ctx, int x, int y)
    {
        foreach (var c in ctx.Creatures)
            if (c.X == x && c.Y == y)
                return true;
        return false;
    }
}
