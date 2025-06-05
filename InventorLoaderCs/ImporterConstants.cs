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
        public const double MAX_2PI = 2.0 * System.Math.PI;
        public const double MAX_PI = System.Math.PI;
        public const double MAX_PI2 = System.Math.PI / 2.0;
        public const double MAX_INF = double.PositiveInfinity;
        public const double MAX_LEN = 2e+100;
        public const double EPS = 1.0e-6;

        // Direction Vectors
        public static readonly Vector3 CENTER = new Vector3(0, 0, 0);
        public static readonly Vector3 DIR_X = new Vector3(1, 0, 0);
        public static readonly Vector3 DIR_Y = new Vector3(0, 1, 0);
        public static readonly Vector3 DIR_Z = new Vector3(0, 0, 1);

        // Encoding
        public static readonly Encoding ENCODING_FS = Encoding.UTF8;

        // Other Constants
        public const int REF_CROSS = 1;
        public const int REF_CHILD = 2;
        public const int REF_PARENT = 3;

        public const int VAL_GUESS = 0;
        public const int VAL_UINT8 = 1;
        public const int VAL_UINT16 = 2;
        public const int VAL_UINT32 = 3;
        public const int VAL_UINT64 = 4;
        public const int VAL_REF = 5;
        public const int VAL_STR8 = 6;
        public const int VAL_STR16 = 7;
        public const int VAL_DATETIME = 8;
        public const int VAL_ENUM = 9;

        public static readonly Dictionary<int, string> VAL_FORMAT = new Dictionary<int, string>
        {
            { VAL_GUESS, "%s" },
            { VAL_UINT8, "%02X" },
            { VAL_UINT16, "%03X" },
            { VAL_UINT32, "%04X" },
            { VAL_UINT64, "%05X" },
            { VAL_STR8, "'%s'" },
            { VAL_STR16, "\"%s\"" },
            { VAL_DATETIME, "#%s#" },
            { VAL_ENUM, "%s" }
        };
    }
}
