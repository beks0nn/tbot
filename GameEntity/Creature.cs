
namespace Bot.GameEntity;

public sealed class Creature : IPositional
{
    public int Id;
    public required string Name;
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int HpPercent;
    public bool IsDead => HpPercent == 0;
    public bool IsRedSquare;
    public bool IsWhitelisted;

    public (int X, int Y) GetTileSlot(int playerX, int playerY) => (X - playerX, Y - playerY);
}
