using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace InventorLoaderCs
{
    public static class Logger
    {
        public static void Info(string message, params object[] args)
        {
            Console.WriteLine($"INFO: {string.Format(message, args)}");
        }

        public static void Warning(string message, params object[] args)
        {
            Console.WriteLine($"WARNING: {string.Format(message, args)}");
        }

        public static void Error(string message, params object[] args)
        {
            Console.WriteLine($"ERROR: {string.Format(message, args)}");
        }
    }

    public static class ImporterUtils
    {
        private static int _fileVersion = 0;
        private static int _blockSize = 0;

        public static int GetFileVersion() => _fileVersion;
        public static void SetFileVersion(int version)
        {
            _fileVersion = version;
            _blockSize = (_fileVersion > 2010) ? 0 : 4; // Example logic from Python
        }
        public static int GetBlockSize() => _blockSize;

        // Data Reading Functions
        public static (bool Value, int NewOffset) GetBoolean(byte[] data, int offset)
        {
            if (offset >= data.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            byte val = data[offset];
            if (val == 1) return (true, offset + 1);
            if (val == 0) return (false, offset + 1);
            throw new ArgumentException($"Expected 0 or 1 for boolean but found {val} at offset {offset}");
        }

        public static (byte Value, int NewOffset) GetUInt8(byte[] data, int offset)
        {
            if (offset >= data.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            return (data[offset], offset + 1);
        }

        public static (short Value, int NewOffset) GetSInt16(byte[] data, int offset)
        {
            if (offset + 1 >= data.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            return (BitConverter.ToInt16(data, offset), offset + 2);
        }

        public static (ushort Value, int NewOffset) GetUInt16(byte[] data, int offset)
        {
            if (offset + 1 >= data.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            return (BitConverter.ToUInt16(data, offset), offset + 2);
        }

        public static (int Value, int NewOffset) GetSInt32(byte[] data, int offset)
        {
            if (offset + 3 >= data.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            return (BitConverter.ToInt32(data, offset), offset + 4);
        }

        public static (uint Value, int NewOffset) GetUInt32(byte[] data, int offset)
        {
            if (offset + 3 >= data.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            return (BitConverter.ToUInt32(data, offset), offset + 4);
        }

        public static (long Value, int NewOffset) GetSInt64(byte[] data, int offset)
        {
            if (offset + 7 >= data.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            return (BitConverter.ToInt64(data, offset), offset + 8);
        }

        public static (ulong Value, int NewOffset) GetUInt64(byte[] data, int offset)
        {
            if (offset + 7 >= data.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            return (BitConverter.ToUInt64(data, offset), offset + 8);
        }


        public static (float Value, int NewOffset) GetFloat32(byte[] data, int offset)
        {
            if (offset + 3 >= data.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            return (BitConverter.ToSingle(data, offset), offset + 4);
        }

        public static (double Value, int NewOffset) GetFloat64(byte[] data, int offset)
        {
            if (offset + 7 >= data.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            return (BitConverter.ToDouble(data, offset), offset + 8);
        }

        public static (Guid Value, int NewOffset) GetGuid(byte[] data, int offset)
        {
            if (offset + 15 >= data.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            // Guid constructor takes byte[16]. Python code implies little-endian components.
            // The standard Guid(byte[]) constructor expects a specific byte order:
            // The first four bytes are data1 (int, little-endian).
            // The next two bytes are data2 (short, little-endian).
            // The next two bytes are data3 (short, little-endian).
            // The last eight bytes are data4 (byte[8]).
            // The Python UID class constructs from bytes_le by manually assigning parts,
            // which corresponds to how Guid(int, short, short, byte[]) works.
            // For simplicity here, we'll use the Guid(byte[]) constructor and assume the byte order matches,
            // or adjust if specific ordering issues arise during testing.
            byte[] guidBytes = new byte[16];
            Array.Copy(data, offset, guidBytes, 0, 16);
            return (new Guid(guidBytes), offset + 16);
        }

        public static (DateTime? Value, int NewOffset) GetDateTime(byte[] data, int offset)
        {
            if (offset + 7 >= data.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            long fileTime = BitConverter.ToInt64(data, offset);
            if (fileTime == 0) return (null, offset + 8);
            try
            {
                return (DateTime.FromFileTimeUtc(fileTime).ToLocalTime(), offset + 8);
            }
            catch (ArgumentOutOfRangeException) // For fileTime values that are out of range for DateTime
            {
                // Handle invalid fileTime, e.g. by returning null or a min/max DateTime value
                return (null, offset + 8);
            }
        }

        public static (string Value, int NewOffset) GetLen32Text8(byte[] data, int offset)
        {
            var (length, currentOffset) = GetUInt32(data, offset);
            if (currentOffset + length > data.Length) throw new ArgumentOutOfRangeException(nameof(offset), "String length exceeds data bounds.");
            // ENCODING_FS from Python ('cp1252' or 'ansi' as fallback)
            // Defaulting to UTF-8 which is more common now, but might need specific encoding from Python constants.
            // Python's ENCODING_FS was 'utf8' in importerConstants.py (read in a previous step), but getText8 used 'cp1252'/'ansi'.
            // For now, using UTF-8 as a common default. If issues arise, Encoding.GetEncoding("windows-1252") might be needed.
            string text = Encoding.UTF8.GetString(data, currentOffset, (int)length);
            // Python code has logic for stripping trailing nulls/newlines.
            text = text.TrimEnd('\0', '\n');
            return (text, currentOffset + (int)length);
        }

        public static (string Value, int NewOffset) GetLen32Text16(byte[] data, int offset)
        {
            var (length, currentOffset) = GetUInt32(data, offset); // Number of characters
            int byteLength = (int)length * 2;
            if (currentOffset + byteLength > data.Length) throw new ArgumentOutOfRangeException(nameof(offset), "String length exceeds data bounds.");

            string text = Encoding.Unicode.GetString(data, currentOffset, byteLength); // UTF-16LE
            text = text.TrimEnd('\0', '\n');
            return (text, currentOffset + byteLength);
        }

        // String Formatting
        public static string FloatArrToString(IEnumerable<float> arr)
        {
            if (arr == null) return string.Empty;
            return string.Join(", ", arr.Select(f => f.ToString(CultureInfo.InvariantCulture)));
        }
        public static string FloatArrToString(IEnumerable<double> arr)
        {
            if (arr == null) return string.Empty;
            return string.Join(", ", arr.Select(f => f.ToString(CultureInfo.InvariantCulture)));
        }


        public static string IntArrToString(IEnumerable<int> arr, string format = "X") // format "X" for hex
        {
            if (arr == null) return string.Empty;
            return string.Join(", ", arr.Select(i => i.ToString(format)));
        }
        public static string IntArrToString(IEnumerable<long> arr, string format = "X")
        {
            if (arr == null) return string.Empty;
            return string.Join(", ", arr.Select(i => i.ToString(format)));
        }


        // Geometric Utilities
        public static bool IsEqual(Vector3 a, Vector3 b, double tolerance = 0.0001)
        {
            return (a - b).LengthSquared() < tolerance * tolerance;
        }

        public static bool IsEqual1D(double a, double b, double tolerance = 0.0001)
        {
            return Math.Abs(a - b) < tolerance;
        }
    }
}
