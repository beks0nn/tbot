using OpenCvSharp;
using Bot.GameEntity;
using Bot.Navigation;

namespace Bot.State;

public sealed class BotContext
{
    public ProfileSettings Profile { get; set; } = new ProfileSettings();
    public nint GameWindowHandle { get; set; }
    public nint ProcessMemoryBaseAddress { get; set; }
    public nint ProcessHandle { get; set; }

    public Mat CurrentFrame { get; set; }
    public Mat CurrentFrameGray { get; set; }

    // Core game state
    public PlayerPosition PlayerPosition { get; set; }
    public PlayerPosition PreviousPlayerPosition { get; set; }
    public FloorData CurrentFloor { get; set; }
    public int RemainingCapacity { get; set; }
    public int Health { get; set; }
    public int Mana { get; set; }
    public bool IsAttacking => Creatures.Any(c => c.IsRedSquare);


    public List<Creature> Creatures { get; set; } = [];
    public Stack<Corpse> Corpses { get; set; } = [];
    public IEnumerable<Creature> BlockingCreatures { get; set; } = [];
    public Dictionary<int, int> FailedAttacks { get; set; } = [];
    public HashSet<int> IgnoredCreatures { get; set; } = [];


    // Template Caches
    public Mat[] LootTemplates { get; set; }
    public Mat[] FoodTemplates { get; set; }
    public Mat BackpackTemplate { get; set; }
    public Mat RopeTemplate { get; set; }
    public Mat ShovelTemplate { get; set; }
    public Mat ManaTemplate { get; set; }

    // Runtime flags
    public bool RecordMode { get; set; }
}

