using System.Collections.Generic;

namespace Bot.Navigation;

public sealed class AStar
{
    private readonly int[] dx = { 0, 0, -1, 1 };
    private readonly int[] dy = { -1, 1, 0, 0 };

    public List<(int x, int y)> FindPath(bool[,] walkable, (int x, int y) start, (int x, int y) goal)
    {
        int h = walkable.GetLength(0);
        int w = walkable.GetLength(1);

        var open = new PriorityQueue<(int x, int y), int>();
        var came = new Dictionary<(int, int), (int, int)>();
        var g = new Dictionary<(int, int), int>();

        bool In(int x, int y) => x >= 0 && y >= 0 && x < w && y < h;

        int Heur((int x, int y) a) => Math.Abs(a.x - goal.x) + Math.Abs(a.y - goal.y);

        open.Enqueue(start, Heur(start));
        g[start] = 0;

        while (open.Count > 0)
        {
            var current = open.Dequeue();
            if (current == goal)
                return Reconstruct(came, start, goal);

            for (int k = 0; k < 4; k++)
            {
                int nx = current.x + dx[k];
                int ny = current.y + dy[k];
                var nb = (nx, ny);

                if (!In(nx, ny) || !walkable[ny, nx]) continue;

                int tentative = g[current] + 1;
                if (!g.TryGetValue(nb, out var prev) || tentative < prev)
                {
                    came[nb] = current;
                    g[nb] = tentative;
                    open.Enqueue(nb, tentative + Heur(nb));
                }
            }
        }
        return new(); // no path
    }

    private List<(int x, int y)> Reconstruct(Dictionary<(int, int), (int, int)> came, (int x, int y) start, (int x, int y) goal)
    {
        var path = new List<(int, int)>();
        var cur = goal;
        path.Add(cur);
        while (cur != start && came.TryGetValue(cur, out var p))
        {
            cur = p;
            path.Add(cur);
        }
        path.Reverse();
        return path;
    }
}
