using Bot.GameEntity;
using System.Runtime.InteropServices;

namespace Bot.MemClass;

public sealed class MemoryReader
{
    [DllImport("kernel32.dll")]
    private static extern bool ReadProcessMemory(nint hProcess, nint lpBaseAddress, byte[] lpBuffer, int dwSize, out nint lpNumberOfBytesRead);

    private const int EntityCount = 500;
    private const int EntityStride = (int)MemoryAddresses.OffsetBetweenEntities; // 0x9C = 156 bytes
    private const int EntitySize = 0x88; // 136 bytes

    private readonly byte[] _batchBuffer = new byte[EntityCount * EntityStride];
    private readonly byte[] _redBuffer = new byte[4];

    private readonly HashSet<int> _alreadyAddedCorpses = [];
    private readonly HashSet<int> _everAttackedIds = [];

    public (Player player, List<Creature> creatures, List<Corpse> corpses) ReadEntities(nint process, nint baseAddress, string playerName)
    {
        var creatures = new List<Creature>();
        var corpses = new List<Corpse>();
        Player? player = null;

        // Read red square target ID
        ReadProcessMemory(process, baseAddress + MemoryAddresses.RedSquareStart, _redBuffer, _redBuffer.Length, out _);
        int redSquareId = BitConverter.ToInt32(_redBuffer, 0);
        if (redSquareId > 0)
            _everAttackedIds.Add(redSquareId);

        // Batch read all entities in one call
        ReadProcessMemory(process, baseAddress + MemoryAddresses.EntityListStart, _batchBuffer, _batchBuffer.Length, out _);

        for (int i = 0; i < EntityCount; i++)
        {
            var entitySpan = _batchBuffer.AsSpan(i * EntityStride, EntitySize);
            var rawEntity = MemoryMarshal.Read<RawEntity>(entitySpan);

            var name = rawEntity.GetName();
            if (string.IsNullOrEmpty(name))
                continue;

            if (name == playerName)
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
                    corpses.Add(ToCorpse(rawEntity));
            }
        }

        if (player == null)
            throw new InvalidOperationException("Player entity not found in memory.");

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
            IsWhitelisted = CreatureWhitelist.Contains(name)
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
            Id = (int)raw.Id,
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
            6 => -31232, //+1
            7 => -31232, //Ground level
            8 => -31488, //-1
            9 => -31488, //-2
            10 => -31488,//-3
            11 => -31232,//-4
            12 => -31488,//-5
            _ => -31488
        };

        int normX = x + X_CORD_FIXED_OFFSET;
        int normY = y + Z_BASED_Y_OFFSET;

        return (normX, normY);
    }
}
