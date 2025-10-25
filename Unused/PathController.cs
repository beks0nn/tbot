using Bot.Control;
using Bot.Navigation;

public sealed class PathController
{
    private readonly AStar _astar = new();
    private readonly KeyMover _mover = new();

    public void Step((int x, int y, int z) player, (int x, int y, int z) target, FloorData floor)
    {
        var path = _astar.FindPath(floor.Walkable, (player.x, player.y), (target.x, target.y));
        if (path.Count < 2)
            return;

        _mover.StepTowards((player.x, player.y), path[1]);
    }
}