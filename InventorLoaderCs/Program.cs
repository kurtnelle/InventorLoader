using System;
using System.IO;

namespace InventorLoaderCs;

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: InventorLoaderCs <path to file>");
            return;
        }

        var path = args[0];
        if (!File.Exists(path))
        {
            Console.WriteLine($"File not found: {path}");
            return;
        }

        using var fs = File.OpenRead(path);
        using var reader = new BinaryReader(fs);
        Console.WriteLine($"First 4 bytes: 0x{reader.ReadUInt32():X8}");
    }
}
