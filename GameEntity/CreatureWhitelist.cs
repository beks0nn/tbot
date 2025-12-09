namespace Bot.GameEntity;

public static class CreatureWhitelist
{
    public static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        "Rotworm",
        "Rat",
        "Cave Rat",
        "Snake",
        "Bug",
        "Wolf",
        "Troll",
        "Goblin",
        "Spider",
        "Poison Spider",
        "Orc",
        "Orc Spearman",
        "Orc Warrior",
        "Orc Shaman",
        "Minotaur",
        "Minotaur Archer",
        "Minotaur Guard",
        "Minotaur Mage",
        "Dwarf",
        "Dwarf Soldier",
        "Dwarf Guard",
    };

    public static bool Contains(string name) => Names.Contains(name.Trim());
}
