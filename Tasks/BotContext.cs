using Bot.Navigation;
using Bot.Vision.CreatureDetection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bot.Tasks;

public sealed class BotContext
{
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
    public List<Corpse> CorpsesToLoot = new();

}

public sealed class Corpse
{
    public (int x, int y) Position;
}