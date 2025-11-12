namespace Bot.Navigation;

public readonly struct PlayerPosition
{
    public int X { get; }
    public int Y { get; }
    public int Floor { get; }
    public double Confidence { get; }

    public PlayerPosition(int x, int y, int floor, double confidence)
    {
        X = x;
        Y = y;
        Floor = floor;
        Confidence = confidence;
    }

    public bool IsValid => Confidence > 90;

    public override string ToString() =>
        $"(PlayerPosition X={X}, Y={Y}, Floor={Floor}, Conf={Confidence:F2})";
}
