using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace InventorLoaderCs
{
    public static class ImporterConstants
    {
        // Mathematical Constants
        public const double MIN_0 = 0.0;
        public const double MIN_PI = -System.Math.PI;
        public const double MIN_PI2 = -System.Math.PI / 2.0;
        public const double MIN_INF = double.NegativeInfinity;
        public const double MAX_2PI = System.Math.PI * 2.0;
        public const double MAX_PI = System.Math.PI;
        public const double MAX_PI2 = System.Math.PI / 2.0;
        public const double MAX_INF = double.PositiveInfinity;
        public const double MAX_LEN = 1.0e12;
        public const double EPS = 1.0e-9;

        // Direction Vectors
        public static readonly Vector3 CENTER = new Vector3(0.0f, 0.0f, 0.0f);
        public static readonly Vector3 DIR_X = new Vector3(1.0f, 0.0f, 0.0f);
        public static readonly Vector3 DIR_Y = new Vector3(0.0f, 1.0f, 0.0f);
        public static readonly Vector3 DIR_Z = new Vector3(0.0f, 0.0f, 1.0f);

        // Encoding
        public static readonly Encoding ENCODING_FS = Encoding.UTF8;

        // Other Constants (Mapped to int or potentially an enum later if beneficial)
        public const int REF_CROSS = 0;
        public const int REF_CHILD = 1;
        public const int REF_PARENT = 2;
        public const int REF_NEXT = 3;
        public const int REF_PREV = 4;
        public const int REF_START = 5;
        public const int REF_END = 6;
        public const int REF_FIRST = 7;
        public const int REF_LAST = 8;
        public const int REF_OTHER = 9;
        public const int REF_PARTNER = 10;

        public const int VAL_GUESS = 0;
        public const int VAL_UINT8 = 1;
        public const int VAL_INT16 = 2;
        public const int VAL_INT32 = 3;
        public const int VAL_FLOAT = 4;
        public const int VAL_DOUBLE = 5;
        public const int VAL_STR = 6;
        public const int VAL_LIST = 7;
        public const int VAL_REF = 8;
        public const int VAL_BOOL = 9;
        public const int VAL_ENUM = 10;
        public const int VAL_BINARY = 11;
        public const int VAL_UNKNOWN = 12;

        // VAL_FORMAT Dictionary
        public static readonly Dictionary<int, string> VAL_FORMAT = new Dictionary<int, string>
        {
            { VAL_GUESS, "?" },
            { VAL_UINT8, "B" },
            { VAL_INT16, "h" },
            { VAL_INT32, "i" },
            { VAL_FLOAT, "f" },
            { VAL_DOUBLE, "d" },
            { VAL_STR, "s" },
            { VAL_LIST, "L" },
            { VAL_REF, "R" },
            { VAL_BOOL, "b" },
            { VAL_ENUM, "E" },
            { VAL_BINARY, "X" }
        };

        public const int MAX_STR_LEN = 255;
        public const int MAX_SMALL_STR_LEN = 31;
        public const int OBJ_HEADER_LEN = 10;

        public const int OBJ_UNKNOWN = 0;
        public const int OBJ_HEADER = 1;
        public const int OBJ_VALUE = 2;
        public const int OBJ_PROP = 3;
        public const int OBJ_GEO = 4;
        public const int OBJ_REL = 5;
        public const int OBJ_GROUP = 6;
        public const int OBJ_XPROP = 7;

        public const int GEO_UNKNOWN = 0;
        public const int GEO_POINT = 1;
        public const int GEO_LINE = 2;
        public const int GEO_PLANE = 3;
        public const int GEO_CIRCLE = 4;
        public const int GEO_CONIC = 5;
        public const int GEO_CURVE = 6;
        public const int GEO_SURFACE = 7;
        public const int GEO_SOLID = 8;
        public const int GEO_GROUP = 9;

        public const int REL_UNKNOWN = 0;
        public const int REL_REF = 1;
        public const int REL_DIM = 2;
        public const int REL_TAN = 3;
        public const int REL_PARA = 4;
        public const int REL_PERP = 5;

        public const int GRP_UNKNOWN = 0;
        public const int GRP_ASS = 1;
        public const int GRP_BODY = 2;
        public const int GRP_SHELL = 3;
        public const int GRP_FACE = 4;
        public const int GRP_LOOP = 5;
        public const int GRP_EDGE = 6;
        public const int GRP_VERTEX = 7;
    }
}
