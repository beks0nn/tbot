
namespace Bot.Models;

public class FrameObservations
{
    public (int X, int Y) PlayerPosition { get; set; }
    public bool MonsterVisible { get; set; }
    public bool CorpseVisible { get; set; }
}
