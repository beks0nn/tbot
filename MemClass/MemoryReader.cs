using System.Runtime.InteropServices;
using Bot.GameEntity;

namespace Bot.MemClass;

public sealed class MemoryReader
{
    [DllImport("kernel32.dll")]
    private static extern bool ReadProcessMemory(int hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

    private readonly byte[] _entityBuffer = new byte[0x88];
    private readonly byte[] _redBuffer = new byte[4];

    private readonly HashSet<int> _alreadyAddedCorpses = [];
    private readonly HashSet<int> _everAttackedIds = [];

    public unsafe (Player player, IEnumerable<Creature> creatures, IEnumerable<Corpse> corpses) ReadEntities(IntPtr process, IntPtr baseAddress)
    {
        var creatures = new List<Creature>();
        var corpses = new List<Corpse>();
        Player? player = null;
        int bytesRead = 0;

        ReadProcessMemory(
            (int)process,
            (int)baseAddress + (int)MemoryAddresses.RedSquareStart,
            _redBuffer,
            _redBuffer.Length,
            ref bytesRead);
        int redSquareId = BitConverter.ToInt32(_redBuffer, 0);
        if (redSquareId > 0)
            _everAttackedIds.Add(redSquareId);

        for (int i = 0; i < 500; i++)
        {
            ReadProcessMemory(
                (int)process,
                (int)baseAddress + (int)MemoryAddresses.EntityListStart + i * (int)MemoryAddresses.OffsetBetweenEntities,
                _entityBuffer,
                _entityBuffer.Length, 
                ref bytesRead);
            var rawEntity = MemoryMarshal.Read<RawEntity>(_entityBuffer);

            var name = rawEntity.GetName();
            if (string.IsNullOrEmpty(name))
                continue;

            if (name == "Huntard")
            {
                player = ToPlayer(rawEntity, name);
                continue;
            }

            if (rawEntity.HpPercent > 0 && rawEntity.HpPercent <= 100)
            {
                creatures.Add(ToCreature(rawEntity, name, redSquareId));
                continue;
            }

            if (rawEntity.HpPercent == 0 && _everAttackedIds.Contains((int)rawEntity.Id))
            {
                if (_alreadyAddedCorpses.Add((int)rawEntity.Id))
                {
                    corpses.Add(ToCorpse(rawEntity));
                }
                continue;
            }
        }

        if (player == null)
            throw new InvalidOperationException("Player entity not found in memory.");

        //var nearby = new List<Creature>();
        //foreach (var e in creatures)
        //{
        //    if (e.Z != player.Z) continue;
        //    if (Math.Abs(e.X - player.X) > 4) continue;
        //    if (Math.Abs(e.Y - player.Y) > 4) continue;
        //    nearby.Add(e);
        //}

        return (player, creatures, corpses);
    }

    private static Creature ToCreature(RawEntity raw, string name, int redSquareId)
    {
        var (normalizedX, normalizedY) = NormalizeCoordinates(raw.X, raw.Y, raw.Z);
        return new Creature
        {
            Id = (int)raw.Id,
            Name = name,
            X = normalizedX,
            Y = normalizedY,
            Z = raw.Z,
            HpPercent = raw.HpPercent,
            IsRedSquare = raw.Id == redSquareId,
        };
    }

    private static Player ToPlayer(RawEntity raw, string name)
    {
        var (normalizedX, normalizedY) = NormalizeCoordinates(raw.X, raw.Y, raw.Z);
        return new Player
        {
            Id = (int)raw.Id,
            Name = name,
            X = normalizedX,
            Y = normalizedY,
            Z = raw.Z,
            HpPercent = raw.HpPercent
        };
    }

    private static Corpse ToCorpse(RawEntity raw)
    {
        var (normalizedX, normalizedY) = NormalizeCoordinates(raw.X, raw.Y, raw.Z);
        return new Corpse
        {
            X = normalizedX,
            Y = normalizedY,
            Z = raw.Z,
            DetectedAt = DateTime.UtcNow
        };
    }

    private static (int X, int Y) NormalizeCoordinates(int x, int y, int z)
    {
        int X_CORD_FIXED_OFFSET = -31744;
        //TODO: test more z levels
        var Z_BASED_Y_OFFSET = z switch
        {
            6 => -31232,
            7 => -31232,
            8 => -31488,
            9 => -31488,
            11 => -31232,
            _ => -31488
        };

        int normX = x + X_CORD_FIXED_OFFSET;
        int normY = y + Z_BASED_Y_OFFSET;

        return (normX, normY);
    }
}
