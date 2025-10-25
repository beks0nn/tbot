using Bot.Vision;

namespace Bot.Navigation;

public static class WaypointExtensions
{
    /// <summary>
    /// Checks if the player is already standing on this waypoint’s position.
    /// Only applies to Move-type waypoints.
    /// </summary>
    public static bool IsAt(this Waypoint wp, PlayerPosition pos)
    {
        if (wp.Type != WaypointType.Move)
            return false;

        return pos.X == wp.X && pos.Y == wp.Y && pos.Floor == wp.Z;
    }
}
