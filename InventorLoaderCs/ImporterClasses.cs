using System;
using System.Collections.Generic;
using System.Numerics;

namespace InventorLoaderCs
{
    // Placeholder for UID, assuming it's a unique identifier, possibly a GUID or string
    public struct Uid
    {
        public string Value { get; set; }
        public Uid(string value) { Value = value; }
        public override string ToString() => Value;
    }

    // Placeholder for Color class from importerUtils
    public struct Color
    {
        public int R { get; set; }
        public int G { get; set; }
        public int B { get; set; }
        public int A { get; set; } // Alpha

        public Color(int r, int g, int b, int a = 255)
        {
            R = r; G = g; B = b; A = a;
        }
        public override string ToString() => $"R:{R}, G:{G}, B:{B}, A:{A}";
    }

    // Moved from SegmentReaders.cs for SecNode dependency
    public struct ColorPlaceholder
    {
        public float R, G, B, A;
        public ColorPlaceholder(float r, float g, float b, float a) { R = r; G = g; B = b; A = a; }
        public override string ToString() => $"R:{R} G:{G} B:{B} A:{A}";
    }

    // Placeholder Enums for Parameter reading
    public enum Tolerances
    {
        NOMINAL = 0, // Assign values based on Python or common usage
        LOWER = 1,
        UPPER = 2,
        MEDIAN = 3
    }

    public enum Functions
    {
        // Values should match those in Python's importerConstants or Acis.py Functions Enum
        NONE = 0, // Assuming 0 for empty string if that's the first in Python
        COS = 1,
        SIN = 2,
        TAN = 3,
        ACOS = 4,
        ASIN = 5,
        ATAN = 6,
        COSH = 7,
        SINH = 8,
        TANH = 9,
        SQRT = 10,
        EXP = 11,
        POW = 12,
        LOG = 13, // Natural Log (ln)
        LOG10 = 14, // Base 10 Log
        FLOOR = 15,
        CEIL = 16,
        ROUND = 17,
        ABS = 18,
        SIGN = 19,
        MAX = 20,
        MIN = 21,
        RANDOM = 22,
        ACOSH = 23,
        ASINH = 24,
        ATANH = 25,
        ISOLATE = 26
        // Add all other functions from Python's Functions Enum
    }

    public enum PartFeatureOperationEnumPlaceholder
    {
        // These values should align with the actual enum values used in Inventor files.
        // Using common STEP Booleans as placeholders.
        OP_NEW_BODY = 0, // Or i nessuna operazione
        OP_JOIN = 1,     // Union / Add
        OP_CUT = 2,      // Difference / Remove
        OP_INTERSECT = 3,// Common
        OP_UNKNOWN = 99
    }

    // --- Law Parameter Holder Classes ---
    public class LawTransformParameter
    {
        public Matrix4x4 TransformMatrix { get; set; }
        public bool HasRotation { get; set; }
        public bool HasReflection { get; set; }
        public bool HasShear { get; set; }
        // In Python, the transform within a law is just the 13 doubles + 3 flags.
        // It's not a full transform-entity reference.
        public LawTransformParameter(double[] m, bool rot, bool refl, bool shr)
        {
            float scale = (float)m[0];
            TransformMatrix = new Matrix4x4(
                (float)m[1]*scale, (float)m[2]*scale, (float)m[3]*scale, 0,
                (float)m[4]*scale, (float)m[5]*scale, (float)m[6]*scale, 0,
                (float)m[7]*scale, (float)m[8]*scale, (float)m[9]*scale, 0,
                (float)m[10],      (float)m[11],      (float)m[12],     1
            );
            HasRotation = rot;
            HasReflection = refl;
            HasShear = shr;
        }
    }

    public class LawEdgeParameter
    {
        public Curve ReferencedCurve { get; set; }
        public double Param1 { get; set; }
        public double Param2 { get; set; }
    }

    public class LawSplineLawParameter
    {
        public int Type { get; set; } // Or some enum
        public List<double> Knots { get; set; }
        public List<double> Values { get; set; } // Corresponds to "y-values" or similar in spline laws
        public Vector3 Point { get; set; } // Or Point entity reference
    }


    public class VersionInfo
    {
        public int Major { get; set; }
        public int Minor { get; set; }
        public int Revision { get; set; }
        public Tuple<int, int, int, int, int> Data { get; set; }

        public VersionInfo(int major = 0, int minor = 0, int revision = 0)
        {
            Major = major;
            Minor = minor;
            Revision = revision;
            Data = new Tuple<int, int, int, int, int>(0, 0, 0, 0, 0);
        }

        public bool IsGreaterThan(int maj, int min, int rev) => Major > maj || (Major == maj && Minor > min) || (Major == maj && Minor == min && Revision > rev);
        public bool IsGreaterOrEqualTo(int maj, int min, int rev) => Major > maj || (Major == maj && Minor > min) || (Major == maj && Minor == min && Revision >= rev);

        public string GetDisplayName()
        {
            // Method implementation deferred
            throw new NotImplementedException();
        }

        public int GetBits()
        {
            // Method implementation deferred
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            // Method implementation deferred
            throw new NotImplementedException();
        }
    }

    public class RSeDatabase
    {
        public RSeSegInformation SegInfo { get; set; }
        public Uid? Uid { get; set; } // Internal-Name of the object
        public int Schema { get; set; }
        public VersionInfo Vers1 { get; set; }
        public object Dat1 { get; set; } // Type not specified, using object
        public List<object> Arr2 { get; set; } // Type not specified, using List<object>
        public VersionInfo Vers2 { get; set; }
        public object Dat2 { get; set; } // Type not specified, using object
        public string Txt { get; set; }

        public RSeDatabase()
        {
            SegInfo = new RSeSegInformation();
            Arr2 = new List<object>();
            Txt = string.Empty;
            Schema = -1;
        }
    }

    public class RSeSegInformation
    {
        public string Text { get; set; }
        public List<object> Vers { get; set; } // Type not specified, using List<object>
        public object Date { get; set; } // Type not specified, using object (DateTime?)
        public Uid? Uid { get; set; }
        public List<object> Arr2 { get; set; }
        public List<object> Arr3 { get; set; }
        public ushort U16 { get; set; }
        public string Text2 { get; set; }
        public List<object> Arr4 { get; set; }
        public Dictionary<object, object> Segments { get; set; } // Type not specified
        public List<ushort> Val { get; set; } // UInt16[2]
        public List<Uid> UidList1 { get; set; }
        public List<Uid> UidList2 { get; set; }

        public RSeSegInformation()
        {
            Text = string.Empty;
            Vers = new List<object>();
            Arr2 = new List<object>();
            Arr3 = new List<object>();
            Text2 = string.Empty;
            Arr4 = new List<object>();
            Segments = new Dictionary<object, object>();
            Val = new List<ushort>();
            UidList1 = new List<Uid>();
            UidList2 = new List<Uid>();
        }
    }

    // Forward declaration for RSeDbRevisionInfo if needed by RSeSegmentObject
    // public class RSeDbRevisionInfo { /* ... */ }

    public class RSeSegmentObject
    {
        // In Python: self.revisionRef = None # reference to RSeDbRevisionInfo
        // For now, let's use object or a specific placeholder class if RSeDbRevisionInfo is defined elsewhere.
        public object RevisionRef { get; set; }
        public List<object> Values { get; set; } // Type not specified
        public object SegRef { get; set; } // Type not specified
        public int Value1 { get; set; }
        public int Value2 { get; set; }

        public RSeSegmentObject()
        {
            Values = new List<object>();
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }
    }

    public class RSeSegmentValue2
    {
        public int Index { get; set; }
        public int IndexSegList1 { get; set; }
        public int IndexSegList2 { get; set; }
        public List<object> Values { get; set; } // Type not specified
        public int Number { get; set; }

        public RSeSegmentValue2()
        {
            Index = -1;
            IndexSegList1 = -1;
            IndexSegList2 = -1;
            Values = new List<object>();
            Number = -1;
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }
    }

    // Forward declaration for RSeDbRevisionInfo
    // public class RSeDbRevisionInfo { /* Definition */ }
    // Forward declaration for VersionInfo (already defined above)

    public class RSeSegment
    {
        public string Name { get; set; }
        public Uid? ID { get; set; }
        public object RevisionRef { get; set; } // Reference to RSeDbRevisionInfo
        public int Value1 { get; set; }
        public int Count1 { get; set; }
        public int Count2 { get; set; }
        public string Type { get; set; }
        public object MetaData { get; set; } // Type not specified
        public List<object> Arr1 { get; set; } // ???, ???, ???, numSec1, ???
        public List<object> Arr2 { get; set; }
        public VersionInfo Version { get; set; }
        public int Value2 { get; set; }
        public List<RSeSegmentObject> Objects { get; set; }
        public List<SecNode> Nodes { get; set; } // Changed from DataNode to SecNode for SegmentReader compatibility

        public RSeSegment(string name = "", VersionInfo version = null)
        {
            Name = name;
            Type = string.Empty;
            Version = version ?? new VersionInfo();
            Arr1 = new List<object>();
            Arr2 = new List<object>();
            Objects = new List<RSeSegmentObject>();
            Nodes = new List<SecNode>(); // Initialize SecNode list
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }
    }

    // Forward declaration for RSeDatabase (already defined)
    // Forward declaration for RSeRevisions (needs to be defined)
    public class RSeRevisions
    {
        public Dictionary<object, object> Mapping { get; set; } // Types not specified
        public List<object> Infos { get; set; } // List of RSeDbRevisionInfo

        public RSeRevisions()
        {
            Mapping = new Dictionary<object, object>();
            Infos = new List<object>();
        }
    }

    public class Inventor
    {
        public RSeDatabase RSeDb { get; set; }
        public UFRxDocument UFRxDoc { get; set; } // Changed from object to UFRxDocument
        public RSeRevisions RSeRevisions { get; set; }
        // Updated IProperties type to match the requirement
        public Dictionary<string, Dictionary<object, Tuple<string, object>>> iProperties { get; set; }
        public Dictionary<string, RSeSegment> Segments { get; private set; }

        public Inventor()
        {
            RSeDb = new RSeDatabase();
            UFRxDoc = new UFRxDocument(); // Initialize UFRxDoc
            RSeRevisions = new RSeRevisions();
            // Initialize with the new type
            iProperties = new Dictionary<string, Dictionary<object, Tuple<string, object>>>();
            Segments = new Dictionary<string, RSeSegment>();
        }

        // Methods like getApp, getBRep, etc., are deferred to use the Segments dictionary
    }

    // New class for UFRxDoc segment data
    public class UFRxDocument
    {
        public UFRxHeader1 Header1 { get; set; }
        // Add other parts of UFRxDoc as needed
        public UFRxDocument()
        {
            Header1 = new UFRxHeader1();
        }
    }

    // New class for UFRxHeader1 data
    public class UFRxHeader1
    {
        public int Schema { get; set; }
        public int Magic1 { get; set; } // Usually 0x09072000
        public int Magic2 { get; set; } // Usually 0x00000100
        public string VersionString { get; set; } // e.g., "15.0.211200.0"
        public VersionInfo ParsedVersion { get; set; }
        public string FileName { get; set; }
        public string SourceFileName { get; set; }
        public DateTime CreationDate { get; set; }
        public Guid DocGuid { get; set; }
        public Guid VersionGuid { get; set; }
        // Add other fields from UFRxHeader1 as needed
    }

    // --- ACIS Specific Data Structures (Potentially moved from AcisUtils or new) ---

    public class Law
    {
        public string LawTypeString { get; set; } // The type read from file e.g. "TRANS", "EDGE", "null_law", or an equation
        public string EquationString { get; set; } // If LawTypeString is an equation itself (not a keyword for a structured type)
        public List<object> Parameters { get; set; } // Holds LawTransformParameter, LawEdgeParameter, LawSplineLawParameter, or other Law objects for variables

        // Optional: Direct properties for very simple, standalone laws if not handled by separate parameter classes
        // public double? ConstantValue { get; set; } // Example: For a "constant_law" type if it doesn't go into Parameters
        // public AcisEntity ReferencedEntity { get; set; } // Example: For a simple reference-based law

        public Law(string typeString)
        {
            LawTypeString = typeString;
            Parameters = new List<object>();
            // EquationString is set by the parser if LawTypeString is not a known keyword
        }
    }

    public class Helix
    {
        public Interval RadAngles { get; set; } // start_angle, end_angle
        public Vector3 PosCenter { get; set; }   // center_point (location)
        public Vector3 DirMajor { get; set; }    // axis_start_point (location) - but used as direction vector
        public Vector3 DirMinor { get; set; }    // major_axis_point (location) - but used as direction vector
        public Vector3 DirPitch { get; set; }    // pitch_vec (vector) - but often given as location, take direction
        public double FacApex { get; set; }      // apex_factor (double)
        public Vector3 VecAxis { get; set; }     // axis_vec (vector) - normalized pitch direction

        // New properties for projection curves/surfaces as per subtask
        public Surface ProjectionSurface1 { get; set; }
        public BSCurveData ProjectionPCurve1 { get; set; }
        public Surface ProjectionSurface2 { get; set; }
        public BSCurveData ProjectionPCurve2 { get; set; }

        public Helix()
        {
            RadAngles = new Interval(0,0);
        }
    }

    public class BSCurveData // Also used for BSurface poles/weights if needed
    {
        public int Degree { get; set; }
        public bool IsPeriodic { get; set; }
        public bool IsRational { get; set; }
        public List<double> Knots { get; set; }
        public List<int> Multiplicities { get; set; }
        public List<Vector2> Poles2D { get; set; } // For 2D B-Splines (pcurves)
        public List<Vector3> Poles3D { get; set; } // For 3D B-Splines
        public List<double> Weights { get; set; }  // For rational B-Splines

        public BSCurveData()
        {
            Knots = new List<double>();
            Multiplicities = new List<int>();
            Poles2D = new List<Vector2>();
            Poles3D = new List<Vector3>();
            Weights = new List<double>();
        }
    }

    public class BSSurfaceData
    {
        public int UDegree { get; set; }
        public int VDegree { get; set; }
        public bool UPeriodic { get; set; }
        public bool VPeriodic { get; set; }
        public bool URational { get; set; } // Indicates if poles have weights in U direction
        public bool VRational { get; set; } // Indicates if poles have weights in V direction (usually same as URational)
        public List<double> UKnots { get; set; }
        public List<int> UMultiplicities { get; set; }
        public List<double> VKnots { get; set; }
        public List<int> VMultiplicities { get; set; }
        public List<List<Vector3>> Poles { get; set; } // Grid of 3D points
        public List<List<double>> Weights { get; set; } // Grid of weights (if rational)

        public BSSurfaceData()
        {
            UKnots = new List<double>();
            UMultiplicities = new List<int>();
            VKnots = new List<double>();
            VMultiplicities = new List<int>();
            Poles = new List<List<Vector3>>();
            Weights = new List<List<double>>();
        }
    }


    // New struct/class for segment directory entries
    public class SegmentEntryInfo
    {
        public string Name { get; set; }
        public string TypeString { get; set; } // Segment type as a string
        public string UidString { get; set; }  // Segment UID as a string
        public int OleStorageIndex { get; set; } // Or some other identifier for OLE stream

        public SegmentEntryInfo(string name, string typeString, string uidString, int oleIdx = 0)
        {
            Name = name;
            TypeString = typeString;
            UidString = uidString;
            OleStorageIndex = oleIdx;
        }
        public override string ToString() => $"Name: {Name}, Type: {TypeString}, UID: {UidString}";
    }


    public class RSeSegInformation
    {
        public string Text { get; set; }
        public List<object> Vers { get; set; }
        public object Date { get; set; }
        public Uid? Uid { get; set; }
        public List<object> Arr2 { get; set; }
        public List<object> Arr3 { get; set; }
        public ushort U16 { get; set; }
        public string Text2 { get; set; }
        public List<object> Arr4 { get; set; }
        public Dictionary<object, object> Segments { get; set; }
        public List<ushort> Val { get; set; }
        public List<Uid> UidList1 { get; set; }
        public List<Uid> UidList2 { get; set; }
        public List<SegmentEntryInfo> SegmentDirectory { get; set; } // Added for RSeDbReader

        public RSeSegInformation()
        {
            Text = string.Empty;
            Vers = new List<object>();
            Arr2 = new List<object>();
            Arr3 = new List<object>();
            Text2 = string.Empty;
            Arr4 = new List<object>();
            Segments = new Dictionary<object, object>();
            Val = new List<ushort>();
            UidList1 = new List<Uid>();
            UidList2 = new List<Uid>();
            SegmentDirectory = new List<SegmentEntryInfo>(); // Initialize
        }
    }


    public abstract class AbstractData
    {
        public Uid? Uid { get; set; }
        public string Name { get; set; }
        public int Index { get; set; }
        public List<object> References { get; set; } // Type not specified
        public Dictionary<string, Tuple<object, int>> Properties { get; set; } // Value, ClassType (from VAL_GUESS etc.)
        public int Size { get; set; }
        public bool Visible { get; set; }
        public bool Construction { get; set; }
        public RSeSegment Segment { get; set; } // Assuming RSeSegment is defined
        public object Geometry { get; set; } // Type not specified (Part.Shape in FreeCAD)
        public int? SketchIndex { get; set; }
        public Vector3? SketchPos { get; set; } // Assuming VEC maps to Vector3
        public bool Valid { get; set; }
        public bool Handled { get; set; }
        public DataNode Node { get; set; } // Assuming DataNode will be defined
        public bool SkipCheck { get; set; }

        // Python's self.data for unknown fields, mapped to a flexible container
        public List<byte> RawData { get; set; }


        protected AbstractData()
        {
            Index = -1;
            References = new List<object>();
            Properties = new Dictionary<string, Tuple<object, int>>();
            Valid = true;
            SkipCheck = true;
            RawData = new List<byte>();
        }

        public virtual string TypeName => GetType().Name; // Basic implementation

        public string Content
        {
            get { throw new NotImplementedException(); }
        }

        public void Set(string name, object value, int cls = ImporterConstants.VAL_GUESS)
        {
            // Method implementation deferred
            throw new NotImplementedException();
        }

        public object Get(string name)
        {
            // Method implementation deferred
            throw new NotImplementedException();
        }

        public string GetName()
        {
            // Method implementation deferred
            throw new NotImplementedException();
        }
    }

    public class DataNode
    {
        public AbstractData Data { get; set; }
        public bool IsRef { get; set; }
        public List<DataNode> Children { get; set; }
        public DataNode Parent { get; set; } // Added for parent reference

        public DataNode(AbstractData data)
        {
            Data = data;
            Children = new List<DataNode>();
        }

        public string TypeName => Data?.TypeName;
        public int Index => Data?.Index ?? -1;
        public bool Handled
        {
            get => Data?.Handled ?? false;
            set { if (Data != null) Data.Handled = value; }
        }
        public bool Valid
        {
            get => Data?.Valid ?? false;
            set { if (Data != null) Data.Valid = value; }
        }
        public object Geometry => Data?.Geometry;
        public RSeSegment Segment => Data?.Segment;
        public int Size => Children.Count;
        public bool IsLeaf => Size == 0;
        public string Name => Data?.GetName();
        public int? SketchIndex => Data?.SketchIndex;

        public void SetGeometry(object geometry, int index = 1)
        {
            if (Data != null)
            {
                Data.Geometry = geometry;
                Data.SketchIndex = index;
            }
        }

        public DataNode Append(DataNode node)
        {
            Children.Add(node);
            node.Parent = this;
            return node;
        }

        // Other methods like getFirstChild, get, set, getSegment, getRefText, etc. deferred
    }

    public class Header0
    {
        public int M { get; set; } // Assuming m from Python is int
        public int X { get; set; } // Assuming x from Python is int

        public Header0(int m, int x)
        {
            M = m;
            X = x;
        }

        public override string ToString() => $"m={M:X} x={X:X3}";
    }

    public class NtEntry
    {
        public int NameTable { get; set; }
        public int Key { get; set; }
        public object Entry { get; set; } // Type not specified

        public NtEntry(int nameTable, int key)
        {
            NameTable = nameTable & 0x7FFFFFFF;
            Key = key;
        }

        public override string ToString()
        {
            if (NameTable != 0)
            {
                return $"{NameTable:X4}[{Key:X4}]";
            }
            return string.Empty;
        }
    }

    public abstract class AbstractValue
    {
        public double X { get; set; }
        public double Factor { get; set; }
        public double Offset { get; set; }
        public string Unit { get; set; }

        protected AbstractValue(double x, double factor, double offset, string unit)
        {
            X = x;
            Factor = factor;
            Offset = offset;
            Unit = unit;
        }

        public override string ToString() => $"{X / Factor - Offset}{Unit}";
        public virtual string ToStandard() => ToString();
        public double GetNominalValue() => X / Factor + Offset;

        // Operator overloads would require more specific type handling or generic constraints
        // For now, methods representing operations can be defined.
        public AbstractValue Add(AbstractValue other)
        {
            // Basic implementation, assumes compatible units
            return (AbstractValue)Activator.CreateInstance(GetType(), X + other.X, Factor, Offset, Unit);
        }
        // Other operations like Subtract, Multiply can be similarly defined.
    }

    public class Length : AbstractValue
    {
        public Length(double x, double factor = 0.1, string unit = "mm")
            : base(x, factor, 0.0, unit) { }

        public double GetMM() => X / 0.1;
        public override string ToStandard() => $"{GetMM()} mm";
    }

    public class Angle : AbstractValue
    {
        public Angle(double a, double factor, string unit)
            : base(a, factor, 0.0, unit) { }

        public double GetRAD() => X;
        public double GetGRAD() => X * (180.0 / System.Math.PI); // Degrees
        public override string ToStandard() => $"{GetGRAD()}°";
    }

    // SecNode class moved from SegmentReaders.cs
    public class SecNode
    {
        public string Uid { get; set; }
        public byte[] FullDataBuffer { get; private set; }
        public int Offset { get; private set; }
        public int Size { get; private set; }
        public int CurrentReadOffset { get; set; }

        public Dictionary<string, object> ParsedContent { get; set; }

        public SecNode(string uid, byte[] fullDataBuffer, int offset, int size)
        {
            Uid = uid;
            FullDataBuffer = fullDataBuffer;
            Offset = offset;
            Size = size;
            CurrentReadOffset = offset;
            ParsedContent = new Dictionary<string, object>();
        }

        private bool CheckBounds(int requiredBytes, StreamWriter logFile, string fieldName, bool allowPartial = false)
        {
            if (CurrentReadOffset + requiredBytes > Offset + Size)
            {
                if (allowPartial && CurrentReadOffset <= Offset + Size) return true;
                string errorMsg = $"SecNode Read Error (UID: {Uid}, Field: {fieldName}): Not enough data. Required: {requiredBytes}, Available: {Offset + Size - CurrentReadOffset}";
                logFile?.WriteLine(errorMsg); // Log to passed stream writer
                Logger.Error(errorMsg);
                return false;
            }
            return true;
        }

        public void LogAction(StreamWriter logFile, string action) => logFile?.WriteLine($"  {action} (UID: {Uid}, Offset: {CurrentReadOffset})");


        public void ReadHeader0(StreamWriter logFile = null)
        {
            LogAction(logFile, "Conceptual Read_Header0() called.");
        }

        public uint? ReadUInt32(string propertyName, StreamWriter logFile)
        {
            if (!CheckBounds(sizeof(uint), logFile, propertyName)) return null;
            var (val, newOffset) = ImporterUtils.GetUInt32(FullDataBuffer, CurrentReadOffset);
            CurrentReadOffset = newOffset;
            ParsedContent[propertyName] = val;
            LogAction(logFile, $"Read {propertyName}: {val}");
            return val;
        }

        public int? ReadSInt32(string propertyName, StreamWriter logFile)
        {
            if (!CheckBounds(sizeof(int), logFile, propertyName)) return null;
            var (val, newOffset) = ImporterUtils.GetSInt32(FullDataBuffer, CurrentReadOffset);
            CurrentReadOffset = newOffset;
            ParsedContent[propertyName] = val;
            LogAction(logFile, $"Read {propertyName}: {val}");
            return val;
        }

        public ushort[] ReadUInt16Array(string propertyName, int count, StreamWriter logFile)
        {
            if (!CheckBounds(sizeof(ushort) * count, logFile, propertyName)) return null;
            ushort[] arr = new ushort[count];
            for(int i=0; i<count; i++)
            {
                var(val, newOffset) = ImporterUtils.GetUInt16(FullDataBuffer, CurrentReadOffset);
                arr[i] = val;
                CurrentReadOffset = newOffset;
            }
            ParsedContent[propertyName] = arr;
            LogAction(logFile, $"Read {propertyName}: [{string.Join(", ", arr)}]");
            return arr;
        }

        public byte[] ReadBytes(string propertyName, int count, StreamWriter logFile)
        {
            if (!CheckBounds(count, logFile, propertyName)) return null;
            byte[] bytes = new byte[count];
            Array.Copy(FullDataBuffer, CurrentReadOffset, bytes, 0, count);
            CurrentReadOffset += count;
            ParsedContent[propertyName] = bytes;
            LogAction(logFile, $"Read {propertyName}: {count} bytes");
            return bytes;
        }

        public string ReadLen32Text16(string propertyName, StreamWriter logFile)
        {
            if (!CheckBounds(sizeof(uint), logFile, propertyName + "_length", allowPartial: true)) return null;
            var (charCount, offsetAfterLength) = ImporterUtils.GetUInt32(FullDataBuffer, CurrentReadOffset);

            int byteLength = (int)charCount * 2;
            if (offsetAfterLength + byteLength > Offset + Size)
            {
                LogAction(logFile, $"Error reading {propertyName}: Not enough data for string content. Calculated byteLength {byteLength} at {offsetAfterLength}.");
                CurrentReadOffset = offsetAfterLength;
                return null;
            }

            var (val, newOffset) = ImporterUtils.GetLen32Text16(FullDataBuffer, CurrentReadOffset);
            CurrentReadOffset = newOffset;
            ParsedContent[propertyName] = val;
            LogAction(logFile, $"Read {propertyName}: {val}");
            return val;
        }

        public Guid? ReadGuid(string propertyName, StreamWriter logFile)
        {
            if (!CheckBounds(16, logFile, propertyName)) return null;
            var (val, newOffset) = ImporterUtils.GetGuid(FullDataBuffer, CurrentReadOffset);
            CurrentReadOffset = newOffset;
            ParsedContent[propertyName] = val;
            LogAction(logFile, $"Read {propertyName}: {val}");
            return val;
        }

        public ColorPlaceholder? ReadColorRgba(string propertyName, StreamWriter logFile) // Using ColorPlaceholder for now
        {
            if (!CheckBounds(sizeof(float) * 4, logFile, propertyName)) return null;
            var (r, rOff) = ImporterUtils.GetFloat32(FullDataBuffer, CurrentReadOffset);
            var (g, gOff) = ImporterUtils.GetFloat32(FullDataBuffer, rOff);
            var (b, bOff) = ImporterUtils.GetFloat32(FullDataBuffer, gOff);
            var (a, aOff) = ImporterUtils.GetFloat32(FullDataBuffer, bOff);
            CurrentReadOffset = aOff;
            var color = new ColorPlaceholder(r, g, b, a);
            ParsedContent[propertyName] = color;
            LogAction(logFile, $"Read {propertyName}: {color}");
            return color;
        }

        public byte? ReadUInt8(string propertyName, StreamWriter logFile)
        {
            if (!CheckBounds(sizeof(byte), logFile, propertyName)) return null;
            var (val, newOffset) = ImporterUtils.GetUInt8(FullDataBuffer, CurrentReadOffset);
            CurrentReadOffset = newOffset;
            ParsedContent[propertyName] = val;
            LogAction(logFile, $"Read {propertyName}: {val}");
            return val;
        }

        public ushort? ReadUInt16(string propertyName, StreamWriter logFile)
        {
            if (!CheckBounds(sizeof(ushort), logFile, propertyName)) return null;
            var (val, newOffset) = ImporterUtils.GetUInt16(FullDataBuffer, CurrentReadOffset);
            CurrentReadOffset = newOffset;
            ParsedContent[propertyName] = val;
            LogAction(logFile, $"Read {propertyName}: {val}");
            return val;
        }

        public float[] ReadFloat32Array(string propertyName, int count, StreamWriter logFile)
        {
            if (!CheckBounds(sizeof(float) * count, logFile, propertyName)) return null;
            float[] arr = new float[count];
            for(int i=0; i<count; i++)
            {
                var(val, newOffset) = ImporterUtils.GetFloat32(FullDataBuffer, CurrentReadOffset);
                arr[i] = val;
                CurrentReadOffset = newOffset;
            }
            ParsedContent[propertyName] = arr;
            LogAction(logFile, $"Read {propertyName}: float[{count}]");
            return arr;
        }

        public double[] ReadFloat64Array(string propertyName, int count, StreamWriter logFile)
        {
            if (!CheckBounds(sizeof(double) * count, logFile, propertyName)) return null;
            double[] arr = new double[count];
            for(int i=0; i<count; i++)
            {
                var(val, newOffset) = ImporterUtils.GetFloat64(FullDataBuffer, CurrentReadOffset);
                arr[i] = val;
                CurrentReadOffset = newOffset;
            }
            ParsedContent[propertyName] = arr;
            LogAction(logFile, $"Read {propertyName}: double[{count}]");
            return arr;
        }

        public Vector2? ReadVector2D(string propertyName, StreamWriter logFile, bool useFloat32 = true)
        {
            if (useFloat32)
            {
                if (!CheckBounds(sizeof(float) * 2, logFile, propertyName)) return null;
                var (x, xOff) = ImporterUtils.GetFloat32(FullDataBuffer, CurrentReadOffset);
                var (y, yOff) = ImporterUtils.GetFloat32(FullDataBuffer, xOff);
                CurrentReadOffset = yOff;
                var vec = new Vector2(x,y);
                ParsedContent[propertyName] = vec;
                LogAction(logFile, $"Read {propertyName} (Vec2f): {vec}");
                return vec;
            }
            else // Float64
            {
                if (!CheckBounds(sizeof(double) * 2, logFile, propertyName)) return null;
                var (x, xOff) = ImporterUtils.GetFloat64(FullDataBuffer, CurrentReadOffset);
                var (y, yOff) = ImporterUtils.GetFloat64(FullDataBuffer, xOff);
                CurrentReadOffset = yOff;
                var vec = new Vector2((float)x, (float)y); // Storing as float Vector2 for consistency
                ParsedContent[propertyName] = vec;
                LogAction(logFile, $"Read {propertyName} (Vec2d): {vec}");
                return vec;
            }
        }

        public Vector3? ReadVector3DFloat32(string propertyName, StreamWriter logFile)
        {
            if (!CheckBounds(sizeof(float) * 3, logFile, propertyName)) return null;
            var (x, xOff) = ImporterUtils.GetFloat32(FullDataBuffer, CurrentReadOffset);
            var (y, yOff) = ImporterUtils.GetFloat32(FullDataBuffer, xOff);
            var (z, zOff) = ImporterUtils.GetFloat32(FullDataBuffer, yOff);
            CurrentReadOffset = zOff;
            var vec = new Vector3(x,y,z);
            ParsedContent[propertyName] = vec;
            LogAction(logFile, $"Read {propertyName} (Vec3f): {vec}");
            return vec;
        }

        public Vector3? ReadVector3DFloat64(string propertyName, StreamWriter logFile)
        {
            if (!CheckBounds(sizeof(double) * 3, logFile, propertyName)) return null;
            var (x, xOff) = ImporterUtils.GetFloat64(FullDataBuffer, CurrentReadOffset);
            var (y, yOff) = ImporterUtils.GetFloat64(FullDataBuffer, xOff);
            var (z, zOff) = ImporterUtils.GetFloat64(FullDataBuffer, yOff);
            CurrentReadOffset = zOff;
            var vec = new Vector3((float)x, (float)y, (float)z);  // Storing as float Vector3 for consistency
            ParsedContent[propertyName] = vec;
            LogAction(logFile, $"Read {propertyName} (Vec3d): {vec}");
            return vec;
        }

        public double? ReadAngle(string propertyName, StreamWriter logFile) // Angles are often doubles (radians)
        {
            return ReadFloat64(propertyName, logFile);
        }

        public object ReadParentRef(string propertyName, StreamWriter logFile)
        {
            // This is a simplified version of ReadCrossRef for now
            return ReadCrossRef(propertyName, logFile, "Parent");
        }

        public object ReadChildRef(string propertyName, StreamWriter logFile, int key = -1, string expectedType = null)
        {
            string actualPropName = key == -1 ? propertyName : $"{propertyName}[{key}]";
            return ReadCrossRef(actualPropName, logFile, expectedType ?? "Child");
        }

        public object ReadMaterial(string propertyName, StreamWriter logFile)
        {
            LogAction(logFile, $"Conceptual ReadMaterial() for {propertyName} called.");
            int placeholderMatSize = 12;
            if (!CheckBounds(placeholderMatSize, logFile, propertyName)) return null;
            CurrentReadOffset += placeholderMatSize;
            string matPlaceholder = "Material_Placeholder";
            ParsedContent[propertyName] = matPlaceholder;
            return matPlaceholder;
        }

        public T? ReadEnum16<T>(string propertyName, StreamWriter logFile) where T : struct, Enum
        {
            if (!CheckBounds(sizeof(ushort), logFile, propertyName)) return null;
            var (val, newOffset) = ImporterUtils.GetUInt16(FullDataBuffer, CurrentReadOffset);
            CurrentReadOffset = newOffset;

            // Attempt to convert to the specific enum type T
            // This assumes the enum's underlying type is compatible with int after ushort conversion.
            if (Enum.IsDefined(typeof(T), Convert.ChangeType(val, Enum.GetUnderlyingType(typeof(T)))))
            {
                T enumVal = (T)Enum.ToObject(typeof(T), val);
                ParsedContent[propertyName] = enumVal;
                LogAction(logFile, $"Read Enum {propertyName} ({typeof(T).Name}): {enumVal} (raw: {val})");
                return enumVal;
            }
            else
            {
                LogAction(logFile, $"Warning: Raw value {val} is not defined for Enum {typeof(T).Name} for property {propertyName}. Storing raw value.");
                ParsedContent[propertyName] = val; // Store raw value if not defined
                return null;
            }
        }

        public short? ReadSInt16(string propertyName, StreamWriter logFile)
        {
            if (!CheckBounds(sizeof(short), logFile, propertyName)) return null;
            var (val, newOffset) = ImporterUtils.GetSInt16(FullDataBuffer, CurrentReadOffset);
            CurrentReadOffset = newOffset;
            ParsedContent[propertyName] = val;
            LogAction(logFile, $"Read {propertyName}: {val}");
            return val;
        }

        public List<object> ReadChildRefList(string propertyName, StreamWriter logFile)
        {
            LogAction(logFile, $"Reading ChildRefList for {propertyName}");
            var list = new List<object>();
            uint? count = ReadUInt32($"{propertyName}_count", logFile);
            if (!count.HasValue)
            {
                LogAction(logFile, $"Error: Could not read count for list {propertyName}.");
                return list; // Return empty list
            }

            for (int i = 0; i < count.Value; i++)
            {
                object childRef = ReadChildRef($"{propertyName}_{i}", logFile);
                if (childRef != null)
                {
                    list.Add(childRef);
                }
                else
                {
                    LogAction(logFile, $"Warning: Failed to read child ref {i} for list {propertyName}.");
                }
            }
            ParsedContent[propertyName] = list;
            return list;
        }


        public double? ReadFloat64(string propertyName, StreamWriter logFile)
        {
            if (!CheckBounds(sizeof(double), logFile, propertyName)) return null;
            var (val, newOffset) = ImporterUtils.GetFloat64(FullDataBuffer, CurrentReadOffset);
            CurrentReadOffset = newOffset;
            ParsedContent[propertyName] = val;
            LogAction(logFile, $"Read {propertyName}: {val}");
            return val;
        }

        public float? ReadFloat32(string propertyName, StreamWriter logFile)
        {
            if (!CheckBounds(sizeof(float), logFile, propertyName)) return null;
            var (val, newOffset) = ImporterUtils.GetFloat32(FullDataBuffer, CurrentReadOffset);
            CurrentReadOffset = newOffset;
            ParsedContent[propertyName] = val;
            LogAction(logFile, $"Read {propertyName}: {val}");
            return val;
        }

        public bool? ReadBoolean(string propertyName, StreamWriter logFile)
        {
            if (!CheckBounds(sizeof(byte), logFile, propertyName)) return null;
            var (val, newOffset) = ImporterUtils.GetBoolean(FullDataBuffer, CurrentReadOffset);
            CurrentReadOffset = newOffset;
            ParsedContent[propertyName] = val;
            LogAction(logFile, $"Read {propertyName}: {val}");
            return val;
        }

        public object ReadCrossRef(string propertyName, StreamWriter logFile, string expectedType = null)
        {
            LogAction(logFile, $"Conceptual ReadCrossRef() for {propertyName} (Expected: {expectedType ?? "any"}) called.");
            int placeholderRefSize = 8;
            if (!CheckBounds(placeholderRefSize, logFile, propertyName)) return null;
            CurrentReadOffset += placeholderRefSize;
            string refPlaceholder = $"CrossRef_To_{expectedType ?? "Object"}_ID_Placeholder";
            ParsedContent[propertyName] = refPlaceholder;
            return refPlaceholder;
        }

        public void SkipBytes(int count, StreamWriter logFile, string reason = "padding")
        {
            LogAction(logFile, $"Skipping {count} bytes for {reason}.");
            if (!CheckBounds(count, logFile, $"SkipBytes for {reason}")) return;
            CurrentReadOffset += count;
        }

        public bool IsEof(StreamWriter logFile = null)
        {
            bool eof = CurrentReadOffset >= (Offset + Size);
            if (eof) LogAction(logFile, "EOF reached for this node.");
            return eof;
        }

        public uint[] ReadUInt32Array(string propertyName, int count, StreamWriter logFile)
        {
            if (!CheckBounds(sizeof(uint) * count, logFile, propertyName)) return null;
            uint[] arr = new uint[count];
            for(int i=0; i<count; i++)
            {
                var(val, newOffset) = ImporterUtils.GetUInt32(FullDataBuffer, CurrentReadOffset);
                arr[i] = val;
                CurrentReadOffset = newOffset;
            }
            ParsedContent[propertyName] = arr;
            LogAction(logFile, $"Read {propertyName}: uint[{count}]");
            return arr;
        }

        public Dictionary<string, TValue> ReadMapStringToObject<TValue>(string propertyName, Func<SecNode, StreamWriter, TValue> valueReaderFunc, StreamWriter logFile)
        {
            LogAction(logFile, $"Reading MapStringToObject for {propertyName}");
            var map = new Dictionary<string, TValue>();
            uint? count = ReadUInt32($"{propertyName}_count", logFile);
            if (!count.HasValue)
            {
                LogAction(logFile, $"Error: Could not read count for map {propertyName}.");
                ParsedContent[propertyName] = map; // Store empty map
                return map;
            }

            for (int i = 0; i < count.Value; i++)
            {
                string key = ReadLen32Text16($"{propertyName}_Key{i}", logFile);
                if (key == null)
                {
                    LogAction(logFile, $"Error: Could not read key for map entry {i} in {propertyName}.");
                    break;
                }
                TValue value = valueReaderFunc(this, logFile);
                if (value != null)
                {
                    map[key] = value;
                }
                else
                {
                    LogAction(logFile, $"Warning: Failed to read value for map entry {key} in {propertyName}.");
                }
            }
            ParsedContent[propertyName] = map;
            return map;
        }

        public Dictionary<Guid, TValue> ReadMapGuidToObject<TValue>(string propertyName, Func<SecNode, StreamWriter, TValue> valueReaderFunc, StreamWriter logFile)
        {
            LogAction(logFile, $"Reading MapGuidToObject for {propertyName}");
            var map = new Dictionary<Guid, TValue>();
            uint? count = ReadUInt32($"{propertyName}_count", logFile);
            if (!count.HasValue)
            {
                LogAction(logFile, $"Error: Could not read count for map {propertyName}.");
                ParsedContent[propertyName] = map; // Store empty map
                return map;
            }

            for (int i = 0; i < count.Value; i++)
            {
                Guid? key = ReadGuid($"{propertyName}_Key{i}", logFile);
                if (!key.HasValue)
                {
                    LogAction(logFile, $"Error: Could not read GUID key for map entry {i} in {propertyName}.");
                    break;
                }
                TValue value = valueReaderFunc(this, logFile);
                if (value != null) // valueReaderFunc should return null on failure
                {
                    map[key.Value] = value;
                }
                else
                {
                     LogAction(logFile, $"Warning: Failed to read value for map entry {key.Value} in {propertyName}.");
                }
            }
            ParsedContent[propertyName] = map;
            return map;
        }

        // New helper methods for typed lists and transformations
        public List<Vector3> ReadListOfVector3DFloat64(string propertyName, StreamWriter logFile)
        {
            LogAction(logFile, $"Reading ListOfVector3DFloat64 for {propertyName}");
            var list = new List<Vector3>();
            uint? count = ReadUInt32($"{propertyName}_count", logFile);
            if (!count.HasValue)
            {
                LogAction(logFile, $"Error: Could not read count for list {propertyName}.");
                ParsedContent[propertyName] = list;
                return list;
            }

            // Python version reads: uInt32 (count), uInt32 (block header 1), uInt32 (block header 2)
            // Assuming block headers are not part of the generic list structure handled here
            // or are specific to the context where this list is read.
            // If they are standard, they should be read before looping.
            // For now, directly reading Vector3 data based on count.
            // node.ReadUInt32($"{propertyName}_listBlockHeader1", logFile); // Example if headers were generic
            // node.ReadUInt32($"{propertyName}_listBlockHeader2", logFile);

            for (int i = 0; i < count.Value; i++)
            {
                Vector3? vec = ReadVector3DFloat64($"{propertyName}_Item{i}", logFile);
                if (vec.HasValue)
                {
                    list.Add(vec.Value);
                }
                else
                {
                    LogAction(logFile, $"Warning: Failed to read Vector3D item {i} for list {propertyName}.");
                    // Add a default or break, depending on desired strictness
                    list.Add(Vector3.Zero);
                }
            }
            ParsedContent[propertyName] = list;
            return list;
        }

        public List<uint> ReadListOfUInt32(string propertyName, StreamWriter logFile)
        {
            LogAction(logFile, $"Reading ListOfUInt32 for {propertyName}");
            var list = new List<uint>();
            uint? count = ReadUInt32($"{propertyName}_count", logFile);
            if (!count.HasValue)
            {
                LogAction(logFile, $"Error: Could not read count for list {propertyName}.");
                ParsedContent[propertyName] = list;
                return list;
            }

            // Similar to ReadListOfVector3DFloat64, skipping block headers for now.
            // node.ReadUInt32($"{propertyName}_listBlockHeader1", logFile);
            // node.ReadUInt32($"{propertyName}_listBlockHeader2", logFile);

            for (int i = 0; i < count.Value; i++)
            {
                uint? val = ReadUInt32($"{propertyName}_Item{i}", logFile);
                if (val.HasValue)
                {
                    list.Add(val.Value);
                }
                else
                {
                    LogAction(logFile, $"Warning: Failed to read UInt32 item {i} for list {propertyName}.");
                    list.Add(0); // Add default or break
                }
            }
            ParsedContent[propertyName] = list;
            return list;
        }

        public Matrix4x4 ReadTransformation3D(string propertyName, StreamWriter logFile)
        {
            LogAction(logFile, $"Reading Transformation3D for {propertyName}");
            double[] m = new double[12];
            bool allDoublesRead = true;
            for (int i = 0; i < 12; i++)
            {
                double? val = ReadFloat64($"{propertyName}.MatrixVal{i}", logFile);
                if (val.HasValue)
                {
                    m[i] = val.Value;
                }
                else
                {
                    allDoublesRead = false;
                    m[i] = (i % 4 == i / 4) ? 1.0 : 0.0; // Default to identity components on error
                }
            }

            if (!allDoublesRead)
            {
                Logger.Warning($"SecNode.ReadTransformation3D: Failed to read all 12 matrix components for {propertyName}. Returning Identity.");
                ParsedContent[propertyName] = Matrix4x4.Identity;
                return Matrix4x4.Identity;
            }

            // Assuming matrix is stored row-major: Rxx Rxy Rxz Tx / Ryx Ryy Ryz Ty / Rzx Rzy Rzz Tz
            var matrix = new Matrix4x4(
                (float)m[0], (float)m[1], (float)m[2], 0,  // Row 1 (M11, M12, M13, M14=0)
                (float)m[3], (float)m[4], (float)m[5], 0,  // Row 2 (M21, M22, M23, M24=0)
                (float)m[6], (float)m[7], (float)m[8], 0,  // Row 3 (M31, M32, M33, M34=0)
                (float)m[9], (float)m[10],(float)m[11],1); // Row 4 (Tx,  Ty,  Tz,  Tw=1)

            ParsedContent[propertyName] = matrix;
            return matrix;
        }

    }
}
