using Bot.Tasks;

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
}
