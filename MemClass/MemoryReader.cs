using Bot.State;
using System.Runtime.InteropServices;

namespace Bot.MemClass;

public sealed class MemoryReader
{
    private HashSet<int> _alreadyAddedCorpses = new();
    private HashSet<int> _everAttackedIds = new();

    [DllImport("kernel32.dll")]
    public static extern bool ReadProcessMemory(int hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

    public unsafe (EntityPure player, IEnumerable<EntityPure> entities, List<Corpse> corpses) ReadEntities(IntPtr process, IntPtr baseAddress)
    {
        var creatures = new List<EntityPure>();
        var corpses = new List<Corpse>();
        var player = new EntityPure();
        int bytesRead = 0;
        byte[] buffer = new byte[sizeof(Entity)];

        for (int i = 0; i < 500; i++)
        {
            ReadProcessMemory(
                (int)process,
                (int)baseAddress + (int)MemoryAddresses.EntityListStart + i * (int)MemoryAddresses.OffsetBetweenEntities, 
                buffer, 
                buffer.Length, 
                ref bytesRead);
            Entity entity = FromSpan(buffer);

            var name = entity.GetName();
            if (string.IsNullOrEmpty(name))
                continue;

            // Player entity
            if (name == "Huntard")
            {
                player = new EntityPure
                {
                    Id = (int)entity.Id,
                    Name = name,
                    X = entity.X,
                    Y = entity.Y,
                    Z = entity.Z,
                    HpPercent = entity.HpPercent
                };
                continue;
            }

            // Corpse
            if (entity.HpPercent == 0 && _everAttackedIds.Contains((int)entity.Id))
            {
                int corpseId = (int)entity.Id;
                if (!_alreadyAddedCorpses.Contains(corpseId))
                {
                    corpses.Add(new Corpse
                    {
                        X = entity.X,
                        Y = entity.Y,
                        Z = entity.Z,
                        DetectedAt = DateTime.UtcNow
                    });

                    _alreadyAddedCorpses.Add(corpseId);
                }

                continue;
            }

            // Creature (alive)
            if (entity.HpPercent > 0 && entity.HpPercent <= 100)
            {
                creatures.Add(new EntityPure
                {
                    Id = (int)entity.Id,
                    Name = name,
                    X = entity.X,
                    Y = entity.Y,
                    Z = entity.Z,
                    HpPercent = entity.HpPercent
                });
            }
        }

        var redBuffer = new byte[4];
        ReadProcessMemory(
            (int)process,
            (int)baseAddress + (int)MemoryAddresses.RedSquareStart,
            redBuffer,
            redBuffer.Length,
            ref bytesRead);
        int redSquareId = BitConverter.ToInt32(redBuffer, 0);

        if(!_everAttackedIds.Contains(redSquareId))
        {
            _everAttackedIds.Add(redSquareId);
        }

        //translate cords and set attack
        int dx = -31744;
        int dy = player.Z switch
        {
            6 => -31232,
            7 => -31232,
            8 => -31488,
            9 => -31488,
            11 => -31232,
            _ => -31488
        };

        player.X += dx;
        player.Y += dy;

        foreach (var c in creatures)
        {
            c.X += dx;
            c.Y += dy;
            c.IsAttacked = (c.Id == redSquareId);
        }

        foreach (var c in corpses)
        {
            c.X += dx;
            c.Y += dy;
        }

        var nearby = new List<EntityPure>();
        //filter nerby
        foreach (var e in creatures)
        {
            if (e.Z != player.Z) continue;
            if (Math.Abs(e.X - player.X) > 4) continue;
            if (Math.Abs(e.Y - player.Y) > 4) continue;
            if (e.Id == player.Id) continue;

            nearby.Add(e);
        }

        return (player, nearby, corpses);
    }


    public static unsafe Entity FromSpan(ReadOnlySpan<byte> span)
    {
        if (span.Length < sizeof(Entity))
            throw new ArgumentException("Buffer too small for Entity struct");

        return MemoryMarshal.Read<Entity>(span);
    }
}
