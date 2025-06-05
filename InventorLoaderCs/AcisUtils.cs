using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text; // Added for Encoding
using System.IO; // Added for BinaryReader, IOException

namespace InventorLoaderCs
{
    public static class AcisConstants
    {
        public const int TAG_CHAR = 2;
        public const int TAG_SHORT = 3;
        public const int TAG_LONG = 4;
        public const int TAG_FLOAT = 5;
        public const int TAG_DOUBLE = 6;
        public const int TAG_UTF8_U8 = 7;
        public const int TAG_UTF8_U16 = 8;
        public const int TAG_UTF8_U32_A = 9;
        public const int TAG_TRUE = 10;
        public const int TAG_FALSE = 11;
        public const int TAG_ENTITY_REF = 12;
        public const int TAG_IDENT = 13;
        public const int TAG_SUBIDENT = 14;
        public const int TAG_SUBTYPE_OPEN = 15;
        public const int TAG_SUBTYPE_CLOSE = 16;
        public const int TAG_TERMINATOR = 17;
        public const int TAG_UTF8_U32_B = 18;
        public const int TAG_POSITION = 19;
        public const int TAG_VECTOR_3D = 20;
        public const int TAG_ENUM_VALUE = 21;
        public const int TAG_VECTOR_2D = 22;
        public const int TAG_INT64 = 23;
    }


    // Static dictionaries for enum string to value mapping (populated as needed)
    // These mimic the dictionaries like SENSE, ROTATION in Acis.py
    public static class AcisEnums
    {
        public static readonly Dictionary<string, int> SENSE = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            { { "forward", 0 }, { "reversed", 1 }, { "unknown", 2 } };
        public static readonly Dictionary<string, int> ROTATION = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {{"no_rotate", 0}, {"rotate", 1}};
        public static readonly Dictionary<string, int> REFLECTION = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {{"no_reflect", 0}, {"reflect", 1}};
        public static readonly Dictionary<string, int> SHEAR = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {{"no_shear", 0}, {"shear", 1}};
        public static readonly Dictionary<string, int> RANGE_TYPE = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {{"I", 0}, {"F", 1}}; // For Range.Type I=Infinite (0), F=Finite (1)
        public static readonly Dictionary<string, bool> BOOLEAN_TEXT_MAP = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {{".T.", true}, {"T", true}, {".F.", false}, {"F", false}};
    }


    public class BS_Curve
    {
        public List<Vector3> Poles { get; set; } // For 3D, or Vector2 for 2D
        public List<int> UMults { get; set; }
        public List<double> UKnots { get; set; }
        public bool UPeriodic { get; set; }
        public int UDegree { get; set; }
        public List<double> Weights { get; set; }
        public bool Rational { get; set; }

        public BS_Curve(bool rational, bool periodic, int degree)
        {
            Poles = new List<Vector3>();
            UMults = new List<int>();
            UKnots = new List<double>();
            UPeriodic = periodic;
            UDegree = degree;
            Weights = new List<double>();
            Rational = rational;
        }
    }

    public class BS_Surface : BS_Curve
    {
        public new List<List<Vector3>> Poles { get; set; } // List of lists of Vector3
        public new List<List<double>> Weights { get; set; } // List of lists of double
        public List<int> VMults { get; set; }
        public List<double> VKnots { get; set; }
        public bool VPeriodic { get; set; }
        public int VDegree { get; set; }
        public double? Tolerance { get; set; } // Added from readSplineSurface
        public string USingularity { get; set; } // Added from readSplineSurface
        public string VSingularity { get; set; } // Added from readSplineSurface


        public BS_Surface(bool rational, bool uPeriodic, bool vPeriodic, int uDegree, int vDegree)
            : base(rational, uPeriodic, uDegree)
        {
            Poles = new List<List<Vector3>>();
            Weights = new List<List<double>>();
            VMults = new List<int>();
            VKnots = new List<double>();
            VPeriodic = vPeriodic;
            VDegree = vDegree;
        }
    }

    public class Range
    {
        public string Type { get; set; } // 'I' for infinite, 'F' for finite
        public double Limit { get; set; }
        public double Scale { get; set; }

        public Range(string type, double limit, double scale = 1.0)
        {
            Type = type;
            Limit = limit;
            Scale = scale;
        }

        public double GetLimit() => Type == "I" ? Limit : Limit * Scale;

        public override string ToString() => Type == "I" ? "I" : $"F {GetLimit()}";
    }

    public class Interval
    {
        public Range Lower { get; set; }
        public Range Upper { get; set; }

        public Interval(Range lower, Range upper)
        {
            Lower = lower;
            Upper = upper;
        }

        public double GetLowerLimit() => Lower.GetLimit();
        public double GetUpperLimit() => Upper.GetLimit();
        public double GetLimit() => GetUpperLimit() - GetLowerLimit();

        public override string ToString() => $"{Lower} {Upper}";
    }
}
