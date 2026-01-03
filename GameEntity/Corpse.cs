
namespace Bot.GameEntity;

public sealed class Corpse
{
    public int Id;
    public int X;
    public int Y;
    public int Z;
    public DateTime DetectedAt;

    public override int GetHashCode() => Id;
    public override bool Equals(object? obj)
    {
        if (obj is Corpse other)
        {
            return Id == other.Id;
        }
        return false;
    }
}
