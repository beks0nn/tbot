
namespace Bot.GameEntity;

public sealed class Corpse : IPositional
{
    public int Id;
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
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
