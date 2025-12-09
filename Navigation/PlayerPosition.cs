namespace Bot.Navigation;

public readonly struct PlayerPosition(int x, int y, int z, double confidence)
{
    public int X { get; } = x;
    public int Y { get; } = y;
    public int Z { get; } = z;
    public double Confidence { get; } = confidence;

    public bool IsValid => Confidence > 90;

    public override string ToString() =>
        $"(PlayerPosition X={X}, Y={Y}, Z={Z}, Conf={Confidence:F2})";
}
