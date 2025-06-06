using System;
using System.IO;
using OleCf; // Added for OleCf package

namespace InventorLoaderCs;

public static class Program
{
    public static void Main(string[] args)
    {
        string filePath;
        if (args.Length == 0)
        {
            Console.WriteLine("No file path provided. Using default: Demo-Status/Demo-Status-0.1.ipt");
            filePath = "Demo-Status/Demo-Status-0.1.ipt"; // Default file for testing
        }
        else
        {
            filePath = args[0];
        }

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"File not found: {filePath}");
            return;
        }

        try
        {
            Console.WriteLine($"Attempting to open OLE file: {filePath}");
            using var oleFile = new OleFile(filePath);

            Console.WriteLine("Successfully opened OLE file.");
            Console.WriteLine("Root Storage Entries:");

            if (oleFile.RootStorage == null)
            {
                Console.WriteLine("RootStorage is null.");
                return;
            }

            foreach (var entry in oleFile.RootStorage.Entries)
            {
                string entryType = entry.IsStorage ? "Storage" : (entry.IsStream ? "Stream" : "Unknown");
                Console.WriteLine($"- Name: {entry.Name}, Type: {entryType}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}
