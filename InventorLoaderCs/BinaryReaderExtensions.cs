using System;
using System.IO;
using System.Numerics;
using System.Text;
using System.Collections.Generic;

namespace InventorLoaderCs;

public static class BinaryReaderExtensions
{
    public static byte ReadUInt8(this BinaryReader reader) => reader.ReadByte();

    public static ushort ReadUInt16(this BinaryReader reader) => reader.ReadUInt16();

    public static uint ReadUInt32(this BinaryReader reader) => reader.ReadUInt32();

    public static ulong ReadUInt64(this BinaryReader reader) => reader.ReadUInt64();

    public static short ReadInt16(this BinaryReader reader) => reader.ReadInt16();

    public static int ReadInt32(this BinaryReader reader) => reader.ReadInt32();

    public static long ReadInt64(this BinaryReader reader) => reader.ReadInt64();

    public static float ReadFloat32(this BinaryReader reader) => reader.ReadSingle();

    public static double ReadFloat64(this BinaryReader reader) => reader.ReadDouble();

    public static Guid ReadGuid(this BinaryReader reader)
    {
        var bytes = reader.ReadBytes(16);
        return new Guid(bytes);
    }

    public static ushort[] ReadUInt16Array(this BinaryReader reader, int count)
    {
        var arr = new ushort[count];
        for (int i = 0; i < count; i++)
            arr[i] = reader.ReadUInt16();
        return arr;
    }

    public static uint[] ReadUInt32Array(this BinaryReader reader, int count)
    {
        var arr = new uint[count];
        for (int i = 0; i < count; i++)
            arr[i] = reader.ReadUInt32();
        return arr;
    }

    public static string ReadCString(this BinaryReader reader)
    {
        var bytes = new List<byte>();
        byte b;
        while ((b = reader.ReadByte()) != 0)
            bytes.Add(b);
        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    public static string ReadLengthPrefixedString8(this BinaryReader reader)
    {
        int len = (int)reader.ReadUInt32();
        var bytes = reader.ReadBytes(len);
        var text = Encoding.UTF8.GetString(bytes);
        if (text.EndsWith("\0"))
            text = text[..^1];
        return text;
    }

    public static string ReadLengthPrefixedString16(this BinaryReader reader)
    {
        int len = (int)reader.ReadUInt32();
        var bytes = reader.ReadBytes(len * 2);
        var text = Encoding.Unicode.GetString(bytes);
        if (text.EndsWith("\0"))
            text = text[..^1];
        return text;
    }
}
