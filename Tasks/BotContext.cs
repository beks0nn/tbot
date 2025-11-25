using Bot.Navigation;
using Bot.Vision.CreatureDetection;
using OpenCvSharp;

namespace Bot.Tasks;

public sealed class BotContext
{
    public IntPtr GameWindowHandle { get; set; }
    public int BaseAddy { get; set; }

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
    public List<Creature> BlockingCreatures = new();

    //protect against bad data
    public Dictionary<int, int> FailedAttacks = new();
    public HashSet<int> IgnoredCreatures = new();


    public bool IsCurrentBackpackFull;
    public int RemainingCapacity;
    public int Health;
    public int Mana;

    public Mat[] LootTemplates;
    public Mat[] FoodTemplates;
    public Mat BackpackTemplate;
    public Mat RopeTemplate;
    public Mat ShovelTemplate;
}

public sealed class Corpse
{
    public int X;
    public int Y;
    public int Z;
    public DateTime DetectedAt;
}