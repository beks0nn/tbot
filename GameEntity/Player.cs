
namespace Bot.GameEntity;

public sealed class Player : IPositional
{
    public int Id;
    public string Name;
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int HpPercent;
    public int ManaPercent;
}
