using System;
using System.IO;
using System.Numerics;

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
}
