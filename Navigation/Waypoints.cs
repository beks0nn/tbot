using Bot.Control;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot.Navigation;

public enum Direction { North, South, East, West }

public abstract class Waypoint
{
    public abstract string Type { get; }

    public virtual bool IsComplete((int x, int y, int z) player) => false;

    public abstract void Execute(KeyMover mover, (int x, int y, int z) player);
}

/// <summary>Walk to absolute map coordinate.</summary>
public sealed class MoveWaypoint : Waypoint
{
    public override string Type => "Move";
    public (int x, int y, int z) Target;

    public MoveWaypoint(int x, int y, int z)
    {
        Target = (x, y, z);
    }

    public override bool IsComplete((int x, int y, int z) player) =>
        player == Target;

    public override void Execute(KeyMover mover, (int x, int y, int z) player)
    {
        // handled by A*
    }
}

/// <summary>Step one tile in a direction (ramps, ladders, etc.).</summary>
public sealed class StepDirectionWaypoint : Waypoint
{
    public override string Type => "StepDir";
    public Direction Dir { get; }

    public StepDirectionWaypoint(Direction dir) => Dir = dir;

    public override void Execute(KeyMover mover, (int x, int y, int z) player)
    {
        mover.StepDirection(Dir);
    }
}

/// <summary>Use an item in a direction (rope, shovel, etc.).</summary>
public sealed class UseItemWaypoint : Waypoint
{
    public override string Type => "UseItem";
    public Direction Dir { get; }
    public string Item { get; }

    public UseItemWaypoint(Direction dir, string item)
    {
        Dir = dir;
        Item = item;
    }

    public override void Execute(KeyMover mover, (int x, int y, int z) player)
    {
        Console.WriteLine($"[Path] Using {Item} to the {Dir}");
        // later: integrate mouse/interaction layer
    }
}

/// <summary>Right-click an adjacent tile (e.g. open door).</summary>
public sealed class RightClickWaypoint : Waypoint
{
    public override string Type => "RightClick";
    public Direction Dir { get; }

    public RightClickWaypoint(Direction dir) => Dir = dir;

    public override void Execute(KeyMover mover, (int x, int y, int z) player)
    {
        Console.WriteLine($"[Path] Right-clicking {Dir} of player.");
        // future: call mouse controller
    }
}