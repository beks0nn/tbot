using System.Runtime.InteropServices;

namespace Bot.MemClass;

public class Addys
{
    public IntPtr EntityListStart = (IntPtr)0x001C68B0;//(IntPtr)0x001C68B0;
    public IntPtr OffsetBetweenEntities = (IntPtr)0x9C;
    public IntPtr RedSquareStart = (IntPtr)0x001C681C;
}




[StructLayout(LayoutKind.Explicit, Size = 0x88)]
public unsafe struct Entity
{
    [FieldOffset(0x00)] public uint Id; //0

    [FieldOffset(0x04)] public fixed byte Name[32]; //4

    //[FieldOffset(0x24)] public int X;
    //[FieldOffset(0x28)] public int Y;
    //[FieldOffset(0x2C)] public int Z;
    [FieldOffset(0x24)] public int X; // 36
    [FieldOffset(0x28)] public int Y; //40
    [FieldOffset(0x2C)] public int Z; // 44

    [FieldOffset(0x84)] public int HpPercent; //132

    //public string GetName()
    //{
    //    fixed (byte* p = Name)
    //        return System.Text.Encoding.UTF8.GetString(p, 32).TrimEnd('\0');
    //}
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
    public string Name;
    public int X;
    public int Y;
    public int Z;
    public int HpPercent;
    public bool IsAttacked = false;
}


