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

    public Mat CurrentFrame { get; set; } = null!;
    public Mat CurrentFrameGray { get; set; } = null!;

    // Core game state
    public PlayerPosition PlayerPosition { get; set; }
    public PlayerPosition PreviousPlayerPosition { get; set; }
    public FloorData CurrentFloor { get; set; } = null!;
    public int RemainingCapacity { get; set; }
    public int Health { get; set; }
    public int Mana { get; set; }
    public bool IsAttacking => Creatures.Any(c => c.IsRedSquare);


    public List<Creature> Creatures { get; set; } = [];
    public Stack<Corpse> Corpses { get; set; } = [];
    public IEnumerable<Creature> BlockingCreatures { get; set; } = [];
    public Dictionary<int, int> FailedAttacks { get; set; } = [];
    public HashSet<int> IgnoredCreatures { get; set; } = [];


    // Template Caches (loaded during InitializeAsync, validated before main loop)
    public Mat[] LootTemplates { get; set; } = null!;
    public Mat[] FoodTemplates { get; set; } = null!;
    public Mat[] FloorLootTemplates { get; set; } = null!;
    public Mat OneHundredGold { get; set; } = null!;
    public Mat BackpackTemplate { get; set; } = null!;
    public Mat BagTemplate { get; set; } = null!;
    public Mat RopeTemplate { get; set; } = null!;
    public Mat ShovelTemplate { get; set; } = null!;
    public Mat ManaTemplate { get; set; } = null!;
    public Mat UhTemplate { get; set; } = null!;

    // Runtime flags
    public bool RecordMode { get; set; }
}

