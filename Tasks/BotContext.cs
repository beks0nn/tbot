using Bot.Navigation;
using Bot.Vision.CreatureDetection;
using OpenCvSharp;

namespace Bot.Tasks;

public sealed class BotContext
{
    public IntPtr GameWindowHandle { get; set; }
    public IntPtr ProcessMemoryBaseAddress { get; set; }
    public IntPtr ProcessHandle { get; set; }

    public Mat CurrentFrame { get; set; }
    public Mat CurrentFrameGray { get; set; }

    // Core game state
    public PlayerPosition PlayerPosition { get; set; }
    public PlayerPosition PreviousPlayerPosition { get; set; }
    public FloorData CurrentFloor { get; set; }
    public int RemainingCapacity { get; set; }
    public int Health { get; set; }
    public int Mana { get; set; }
    public bool IsAttacking => Creatures.Any(c => c.IsTargeted);


    public List<Creature> Creatures { get; set; } = [];
    public List<Corpse> Corpses { get; set; } = [];
    public List<Creature> BlockingCreatures { get; set; } = [];
    public Dictionary<int, int> FailedAttacks { get; set; } = [];
    public HashSet<int> IgnoredCreatures { get; set; } = [];


    // Template Caches
    public Mat[] LootTemplates { get; set; }
    public Mat[] FoodTemplates { get; set; }
    public Mat BackpackTemplate { get; set; }
    public Mat RopeTemplate { get; set; }
    public Mat ShovelTemplate { get; set; }

    // Runtime flags
    public bool RecordMode { get; set; }
}

public sealed class Corpse
{
    public int X;
    public int Y;
    public int Z;
    public DateTime DetectedAt;
}