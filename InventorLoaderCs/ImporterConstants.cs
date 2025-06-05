using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace InventorLoaderCs
{
    public static class ImporterConstants
    {
        // Mathematical Constants
        public const double MIN_0 = 0.0;
        public const double MIN_PI = -Math.PI;
        public const double MIN_PI2 = -Math.PI / 2.0;
        public const double MIN_INF = double.NegativeInfinity;
        public const double MAX_2PI = 2.0 * Math.PI;
        public const double MAX_PI = Math.PI;
        public const double MAX_PI2 = Math.PI / 2.0;
        public const double MAX_INF = double.PositiveInfinity;
        public const double MAX_LEN = 1.0e12; // Arbitrary large length
        public const double EPS = 1.0e-9;   // Epsilon for comparisons

        // Direction Vectors
        public static readonly Vector3 CENTER = Vector3.Zero;
        public static readonly Vector3 DIR_X = Vector3.UnitX;
        public static readonly Vector3 DIR_Y = Vector3.UnitY;
        public static readonly Vector3 DIR_Z = Vector3.UnitZ;

        // Encoding
        public static readonly Encoding ENCODING_FS = Encoding.UTF8;

        // Reference Types
        public const int REF_CROSS = 0;
        public const int REF_CHILD = 1;
        public const int REF_PARENT = 2;

        // Value Types Enum
        public enum ValueType : int
        {
            VAL_GUESS = 0,
            VAL_UINT8 = 1,
            VAL_SINT16 = 2,
            VAL_UINT16 = 3,
            VAL_SINT32 = 4,
            VAL_UINT32 = 5,
            VAL_SINT64 = 6,
            VAL_UINT64 = 7,
            VAL_FLOAT32 = 8,
            VAL_FLOAT64 = 9,
            VAL_TEXT_ID = 10, // Special case for text IDs
            VAL_BLOB_ID = 11, // Special case for BLOB IDs
            VAL_UNKNOWN = 12,
            VAL_NT_ENTRY = 13,
            VAL_COLOR_RGBA = 14,
            VAL_VEC_3D = 15,
            VAL_VEC_2D = 16,
            VAL_MAT_3D = 17,
            VAL_MAT_2D = 18,
            VAL_GUID = 19,
            VAL_TIME = 20,
            VAL_LEN_32_TEXT_8 = 21,
            VAL_LEN_32_TEXT_16 = 22,
            VAL_BOOL_8 = 23,
            VAL_ENUM_16 = 24,
            VAL_ENUM_32 = 25,
            VAL_MAP_GUID_TO_OBJECT = 26,
            VAL_MAP_STRING_TO_OBJECT = 27,
            VAL_CHILD_REF_LIST = 28,
            VAL_ARR_SINT32 = 29,
            VAL_ARR_FLOAT64 = 30,
            VAL_ARR_GUID = 31,
            VAL_ARR_ENUM_16 = 32,
            VAL_ARR_CHILD_REF = 33,
            VAL_ARR_LEN_32_TEXT_16 = 34,
            VAL_UNKNOWN_LEN_32 = 35
        }

        // Value Type Format Mapping
        public static readonly Dictionary<int, string> VAL_FORMAT = new Dictionary<int, string>
        {
            { (int)ValueType.VAL_GUESS, "?" },
            { (int)ValueType.VAL_UINT8, "B" },    // Unsigned char (1 byte)
            { (int)ValueType.VAL_SINT16, "h" },   // Short (2 bytes)
            { (int)ValueType.VAL_UINT16, "H" },   // Unsigned short (2 bytes)
            { (int)ValueType.VAL_SINT32, "i" },   // Int (4 bytes)
            { (int)ValueType.VAL_UINT32, "I" },   // Unsigned int (4 bytes)
            { (int)ValueType.VAL_SINT64, "q" },   // Long long (8 bytes)
            { (int)ValueType.VAL_UINT64, "Q" },   // Unsigned long long (8 bytes)
            { (int)ValueType.VAL_FLOAT32, "f" }, // Float (4 bytes)
            { (int)ValueType.VAL_FLOAT64, "d" }, // Double (8 bytes)
            // VAL_TEXT_ID, VAL_BLOB_ID, VAL_UNKNOWN are special and don't have simple struct format strings
            { (int)ValueType.VAL_NT_ENTRY, "NtEntry" }, // Custom structure
            { (int)ValueType.VAL_COLOR_RGBA, "ColorRgba" }, // Custom structure
            { (int)ValueType.VAL_VEC_3D, "Vector3d" }, // Custom structure
            { (int)ValueType.VAL_VEC_2D, "Vector2d" }, // Custom structure
            { (int)ValueType.VAL_MAT_3D, "Matrix3d" }, // Custom structure
            { (int)ValueType.VAL_MAT_2D, "Matrix2d" }, // Custom structure
            { (int)ValueType.VAL_GUID, "Guid" }, // Custom structure (16 bytes)
            { (int)ValueType.VAL_TIME, "DateTime" }, // Custom structure (FILETIME, 8 bytes)
            { (int)ValueType.VAL_LEN_32_TEXT_8, "Len32Text8" }, // Custom
            { (int)ValueType.VAL_LEN_32_TEXT_16, "Len32Text16" }, // Custom
            { (int)ValueType.VAL_BOOL_8, "b" }, // Actually bool, but read as byte
            { (int)ValueType.VAL_ENUM_16, "H" }, // Read as unsigned short
            { (int)ValueType.VAL_ENUM_32, "I" }, // Read as unsigned int
            { (int)ValueType.VAL_MAP_GUID_TO_OBJECT, "MapGuidToObject" }, // Custom
            { (int)ValueType.VAL_MAP_STRING_TO_OBJECT, "MapStringToObject" }, // Custom
            { (int)ValueType.VAL_CHILD_REF_LIST, "ChildRefList" }, // Custom
            { (int)ValueType.VAL_ARR_SINT32, "ArraySInt32" }, // Custom
            { (int)ValueType.VAL_ARR_FLOAT64, "ArrayFloat64" }, // Custom
            { (int)ValueType.VAL_ARR_GUID, "ArrayGuid" }, // Custom
            { (int)ValueType.VAL_ARR_ENUM_16, "ArrayEnum16" }, // Custom
            { (int)ValueType.VAL_ARR_CHILD_REF, "ArrayChildRef" }, // Custom
            { (int)ValueType.VAL_ARR_LEN_32_TEXT_16, "ArrayLen32Text16" }, // Custom
            { (int)ValueType.VAL_UNKNOWN_LEN_32, "UnknownLen32" } // Custom
        };
    }
}
