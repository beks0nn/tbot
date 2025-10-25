using Bot.Navigation;
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
    public FloorData CurrentFloor;

    // Runtime flags
    public bool IsRunning;
    public bool RecordMode;
    public bool IsPaused;
    public bool IsAttacking;
    public bool ShouldRefill;

    // dunno
    public List<Monster> Monsters = new();
    public List<Corpse> CorpsesToLoot = new();

}

public sealed class Monster
{
    public string Name;
    public (int x, int y) Position;
    public int HealthPercent;
}
public sealed class Corpse
{
    public (int x, int y) Position;
}