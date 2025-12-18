
namespace Bot.GameEntity;

public sealed class Creature
{
    public int Id;
    public required string Name;
    public int X;
    public int Y;
    public int Z;
    public int HpPercent;
    public bool IsDead => HpPercent == 0;
    public bool IsRedSquare;
    public bool IsWhitelisted;

    public (int X, int Y) GetTileSlot(int playerX, int playerY) => (X - playerX, Y - playerY);
}
