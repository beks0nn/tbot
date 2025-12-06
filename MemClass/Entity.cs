using System.Runtime.InteropServices;

namespace Bot.MemClass;

[StructLayout(LayoutKind.Explicit, Size = 0x88)]
public unsafe struct Entity
{
    [FieldOffset(0x00)] public uint Id; //0
    [FieldOffset(0x04)] public fixed byte Name[32];//4
    [FieldOffset(0x24)] public int X; //36
    [FieldOffset(0x28)] public int Y; //40
    [FieldOffset(0x2C)] public int Z; //44
    [FieldOffset(0x84)] public int HpPercent; //132

    public string GetName()
    {
        fixed (byte* p = Name)
        {
            // Decode as ASCII, not UTF-8
            string raw = System.Text.Encoding.ASCII.GetString(p, 32);

            // Trim at first null terminator
            int idx = raw.IndexOf('\0');
            return idx >= 0 ? raw[..idx] : raw;
        }
    }
}

public class EntityPure
{
    public int Id;
    public string Name = "";
    public int X;
    public int Y;
    public int Z;
    public int HpPercent;
    public bool IsAttacked = false;
}
