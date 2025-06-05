using System;
using System.Collections.Generic;
using System.Numerics;

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

    public static class AcisGlobalUtils
    {
        private static AcisReader _reader;

        public static AcisReader GetReader()
        {
            return _reader;
        }

        public static void SetReader(AcisReader reader)
        {
            _reader = reader;
            // ClearEntities(); // Assuming ClearEntities will be part of this or another utility class
        }

        private static Dictionary<string, Type> _recordToEntityTypeMap = new Dictionary<string, Type>();
        private static bool _mapInitialized = false;

        private static void InitializeEntityTypeMap()
        {
            if (_mapInitialized) return;

            // Populate with mappings from Python's RECORD_2_ENTITY
            // Topological entities
            _recordToEntityTypeMap["body-entity"] = typeof(Body);
            _recordToEntityTypeMap["lump-entity"] = typeof(Lump);
            _recordToEntityTypeMap["shell-entity"] = typeof(Shell);
            _recordToEntityTypeMap["subshell-entity"] = typeof(SubShell);
            _recordToEntityTypeMap["face-entity"] = typeof(Face);
            _recordToEntityTypeMap["loop-entity"] = typeof(Loop);
            _recordToEntityTypeMap["coedge-entity"] = typeof(CoEdge);
            _recordToEntityTypeMap["tedge-coedge-entity"] = typeof(CoEdge); // CoEdgeTolerance in Python, maps to CoEdge for now
            _recordToEntityTypeMap["edge-entity"] = typeof(Edge);
            _recordToEntityTypeMap["tedge-edge-entity"] = typeof(Edge); // EdgeTolerance in Python, maps to Edge
            _recordToEntityTypeMap["vertex-entity"] = typeof(Vertex);
            _recordToEntityTypeMap["tvertex-vertex-entity"] = typeof(Vertex); // VertexTolerance, maps to Vertex
            _recordToEntityTypeMap["wire-entity"] = typeof(Wire);

            // Geometrical entities - Curves
            _recordToEntityTypeMap["straight-curve"] = typeof(CurveStraight);
            _recordToEntityTypeMap["ellipse-curve"] = typeof(CurveEllipse);
            _recordToEntityTypeMap["intcurve-curve"] = typeof(CurveInt);
            _recordToEntityTypeMap["intcurve-intcurve-curve"] = typeof(CurveIntInt); // Specific type of intcurve
            _recordToEntityTypeMap["pcurve-curve"] = typeof(CurveP); // Parameter curve
            _recordToEntityTypeMap["compcurv-curve"] = typeof(CurveComp); // Compound curve
            _recordToEntityTypeMap["degenerate_curve-curve"] = typeof(CurveDegenerate); // Degenerate curve
            _recordToEntityTypeMap["null_curve-curve"] = typeof(Curve); // Represents null curve, map to base Curve or specific NullCurve if created

            // Geometrical entities - Surfaces
            _recordToEntityTypeMap["plane-surface"] = typeof(SurfacePlane);
            _recordToEntityTypeMap["cone-surface"] = typeof(SurfaceCone);
            _recordToEntityTypeMap["sphere-surface"] = typeof(SurfaceSphere);
            _recordToEntityTypeMap["torus-surface"] = typeof(SurfaceTorus);
            _recordToEntityTypeMap["spline-surface"] = typeof(SurfaceSpline);
            _recordToEntityTypeMap["meshsurf-surface"] = typeof(SurfaceMesh);
            _recordToEntityTypeMap["null_surface-surface"] = typeof(Surface); // Represents null surface

            // Other core entities
            _recordToEntityTypeMap["point-entity"] = typeof(Point);
            _recordToEntityTypeMap["transform-entity"] = typeof(Transform);
            // Generic "entity" might be a fallback or specific data container not yet fully modeled
            _recordToEntityTypeMap["entity"] = typeof(AcisEntity); // Or a more specific "GenericEntity" if needed

            // Attributes (many variations, map to Attributes or specific stubs if created)
            // Using a few examples from Python's list, many will map to the base Attrib(utes) class or specific stubs
            _recordToEntityTypeMap["attrib-entity"] = typeof(Attributes); // Base for many attributes
            _recordToEntityTypeMap["attrib_custom-attrib-entity"] = typeof(AttribCustom); // Assuming AttribCustom : Attributes
            _recordToEntityTypeMap["name_attrib-gen-attrib-entity"] = typeof(AttribGenName); // AttribGenName : AttribGen : Attributes
            _recordToEntityTypeMap["rgb_color-st-attrib-entity"] = typeof(AttribStRgbColor); // AttribStRgbColor : AttribSt : Attributes
            _recordToEntityTypeMap["adesk-attrib-entity"] = typeof(AttribADesk);
            _recordToEntityTypeMap["color-adesk-attrib-entity"] = typeof(AttribADeskColor);
            _recordToEntityTypeMap["material-adesk-attrib-entity"] = typeof(AttribADeskMaterial);
            _recordToEntityTypeMap["truecolor-adesk-attrib-entity"] = typeof(AttribADeskTrueColor);
            // Add all other attribute mappings from Python, using typeof(Attributes) as a fallback if specific class not made
            // For example:
            _recordToEntityTypeMap["id-ansoft-attrib-entity"] = typeof(Attributes); // Placeholder for AttribAnsoftId
            _recordToEntityTypeMap["history-Designer-attrib-entity"] = typeof(Attributes); // Placeholder for AttribDesignerHistory
            _recordToEntityTypeMap["NMx_Brep_tag-NamingMatching-attrib-entity"] = typeof(Attributes); // Placeholder for complex NamingMatching attributes

            // Special/meta records (these are usually not instantiated as entities by CreateEntity but handled by reader)
            // "Begin-of-ACIS-History-Data" -> typeof(BeginOfAcisHistoryData) (if such a class exists for type checking)
            // "End-of-ACIS-data" -> typeof(EndOfAcisData)
            // "delta_state-entity" -> typeof(DeltaState) (if modeled)

            // For any unlisted types from Python, they would default to UnknownAcisEntity via CreateEntity's fallback.
            _recordToEntityTypeMap["unknown-entity"] = typeof(UnknownAcisEntity); // Explicit fallback

            _mapInitialized = true;
        }

        public static AcisEntity CreateEntity(AcisRecord record)
        {
            InitializeEntityTypeMap();
            if (record == null) return null;

            if (_recordToEntityTypeMap.TryGetValue(record.Name, out Type entityType))
            {
                try
                {
                    AcisEntity entity = (AcisEntity)Activator.CreateInstance(entityType);
                    entity.Record = record; // Link record to entity
                    record.Entity = entity; // Link entity to record
                    entity.Set(record);     // Call the entity's setup method
                    return entity;
                }
                catch (Exception ex)
                {
                    Logger.Error($"AcisGlobalUtils: Error creating entity of type {entityType.Name} for record {record.Name}: {ex.Message}");
                    return null;
                }
            }
            else
            {
                // Try to find a base class if specific type is not found (e.g. "plane-surface" -> "surface-entity")
                string[] typeParts = record.Name.Split('-');
                if (typeParts.Length > 1)
                {
                    string baseTypeName = string.Join("-", typeParts.Skip(1));
                    if (_recordToEntityTypeMap.TryGetValue(baseTypeName, out Type baseEntityType))
                    {
                         try
                         {
                            Logger.Warning($"AcisGlobalUtils: Specific type for '{record.Name}' not found. Using base type '{baseTypeName}'.");
                            AcisEntity entity = (AcisEntity)Activator.CreateInstance(baseEntityType);
                            entity.Record = record;
                            record.Entity = entity;
                            entity.Set(record);
                            return entity;
                         }
                         catch (Exception ex)
                         {
                            Logger.Error($"AcisGlobalUtils: Error creating base entity of type {baseEntityType.Name} for record {record.Name}: {ex.Message}");
                         }
                    }
                }
                Logger.Warning($"AcisGlobalUtils: Entity type for record name '{record.Name}' not found in map. Creating base AcisEntity.");
                // Fallback to a generic AcisEntity or a specific "UnknownEntity" if defined
                AcisEntity genericEntity = new UnknownAcisEntity(); // Assuming UnknownAcisEntity : AcisEntity exists
                genericEntity.Record = record;
                record.Entity = genericEntity;
                genericEntity.Set(record); // Call set even for unknown, base might do something
                return genericEntity;
            }
        }

        // Placeholder for other global utility functions from Acis.py

        // For AcisParsingUtils.GetEnumByTag & _TranslateChunkToken
        public static readonly Dictionary<int, string> BooleanValues = new Dictionary<int, string>
        {
            { AcisConstants.TAG_TRUE, "TRUE" }, { AcisConstants.TAG_FALSE, "FALSE" }
        };

        // More sophisticated enum lookup might be needed, similar to Python's __build_bool_enum__ and specific enum dicts
        public static bool TryGetEnumValue(string token, out AcisChunkEnumValue chunkEnumValue)
        {
            // This is a simplified lookup. A more robust solution would check against various enum dictionaries.
            if (AcisEnums.BOOLEAN_TEXT_MAP.TryGetValue(token, out bool boolVal))
            {
                chunkEnumValue = new AcisChunkEnumValue(boolVal ? AcisConstants.TAG_TRUE : AcisConstants.TAG_FALSE, boolVal ? AcisConstants.TAG_TRUE : AcisConstants.TAG_FALSE, BooleanValues);
                return true;
            }
            // Example for SENSE enum (integer based in C# for now)
            if (AcisEnums.SENSE.TryGetValue(token, out int senseVal))
            {
                 // The AcisChunkEnumValue would ideally store the C# enum type for later casting,
                 // or the parser would handle this mapping directly.
                 // For now, storing the int value that the C# enum would correspond to.
                chunkEnumValue = new AcisChunkEnumValue(AcisConstants.TAG_ENUM_VALUE, senseVal, null); // No specific dict for reverse lookup here
                return true;
            }
            // Add lookups for other known string enums (ROTATION, REFLECTION, SHEAR, etc.)
            if (AcisEnums.ROTATION.TryGetValue(token, out int rotVal))
            {
                chunkEnumValue = new AcisChunkEnumValue(AcisConstants.TAG_ENUM_VALUE, rotVal, null);
                return true;
            }
            if (AcisEnums.REFLECTION.TryGetValue(token, out int reflVal))
            {
                chunkEnumValue = new AcisChunkEnumValue(AcisConstants.TAG_ENUM_VALUE, reflVal, null);
                return true;
            }
            if (AcisEnums.SHEAR.TryGetValue(token, out int shearVal))
            {
                chunkEnumValue = new AcisChunkEnumValue(AcisConstants.TAG_ENUM_VALUE, shearVal, null);
                return true;
            }
            if (AcisEnums.RANGE_TYPE.TryGetValue(token, out int rangeVal)) // For Range.Type (I/F)
            {
                chunkEnumValue = new AcisChunkEnumValue(AcisConstants.TAG_ENUM_VALUE, rangeVal, null);
                return true;
            }

            chunkEnumValue = null;
            return false;
        }
         public static double IntToVersion(int versionInt)
        {
            // From Python: float("%d.%d" %(num / 100, num % 100))
            // This implies integer division for major, then modulo for minor.
            // E.g., 700 -> major=7, minor=0  -> 7.0
            // E.g., 1550 -> major=15, minor=50 -> 15.5 (or 15.50)
            // E.g., 21201 -> major=212, minor=1 -> 212.01
            int major = versionInt / 100;
            int minor = versionInt % 100;
            return major + (minor / 100.0);
        }

        public static int VersionToInt(double versionDouble)
        {
            // From Python: int(major*100 + minor)
            // E.g. 7.0 -> major=7, minor=0 -> 700
            // E.g. 15.5 -> major=15, minor=50 -> 1550
            // E.g. 212.01 -> major=212, minor=1 -> 21201
            int major = (int)versionDouble;
            int minor = (int)Math.Round((versionDouble - major) * 100);
            return major * 100 + minor;
        }
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

    // Placeholder for UnknownAcisEntity if not defined in AcisEntities.cs
    public class UnknownAcisEntity : AcisEntity
    {
        public override int Set(AcisRecord record)
        {
            Logger.Info($"UnknownAcisEntity.Set called for record: {record.Name}");
            return base.Set(record); // Call base set, which handles attrib and history
        }
    }


    public class Law
    {
        public string Equation { get; set; }

        public Law(string eq)
        {
            Equation = eq.Replace("^", " ** "); // Basic transformation
        }

        public object Evaluate(object x) // Parameter X could be double or Vector, return type depends on equation
        {
            // Method implementation deferred
            throw new NotImplementedException();
        }
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

    public class Helix
    {
        public Interval RadAngles { get; set; }
        public Vector3 PosCenter { get; set; }
        public Vector3 DirMajor { get; set; }
        public Vector3 DirMinor { get; set; }
        public Vector3 DirPitch { get; set; }
        public double FacApex { get; set; }
        public Vector3 VecAxis { get; set; }

        public Helix()
        {
            RadAngles = new Interval(new Range("I", 1.0), new Range("I", 1.0)); // Default values
            PosCenter = Vector3.Zero;
            DirMajor = Vector3.UnitX;
            DirMinor = Vector3.UnitY;
            DirPitch = Vector3.UnitZ;
            VecAxis = Vector3.UnitZ;
        }

        public object Build() // Returns a shape, e.g. Part.BSplineCurve().toShape()
        {
            // Method implementation deferred
            throw new NotImplementedException();
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
