using Bot.Navigation;
using Bot.Vision.CreatureDetection;
using OpenCvSharp;

namespace Bot.Tasks;

public sealed class BotContext
{
    public Mat CurrentFrame;
    public Mat CurrentFrameGray;

    // Core game state
    public PlayerPosition PlayerPosition;
    public PlayerPosition PreviousPlayerPosition;
    public FloorData CurrentFloor;

    // Runtime flags
    public bool IsRunning;
    public bool RecordMode;
    public bool IsPaused;

    public bool IsAttacking => Creatures.Any(c => c.IsTargeted);
    public bool ShouldRefill;

    // dunno
    public List<Creature> Creatures = new();
    public List<Corpse> Corpses = new();


    public bool IsCurrentBackpackFull;
    public int RemainingCapacity;
    public int Health;
    public int Mana;

    public Mat[] LootTemplates;
    public Mat[] FoodTemplates;
}

public sealed class Corpse
{
    public int X;
    public int Y;
    public DateTime DetectedAt;
}