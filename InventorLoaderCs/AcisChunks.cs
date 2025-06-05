using System;
using System.Globalization; // Added for CultureInfo

namespace InventorLoaderCs
{
    public abstract class AcisChunk
    {
        public int Tag { get; protected set; }
        public object Val { get; set; } // Can be string, double, int, bool, Vector2, Vector3, AcisRecord (for EntityRef)

        protected AcisChunk(int tag, object val = null)
        {
            Tag = tag;
            Val = val;
        }

        public override string ToString() => Val?.ToString() + " ";

        // Binary read method to be implemented by subclasses if needed for SAB
        public virtual int Read(byte[] data, int offset)
        {
            throw new NotImplementedException("Binary read not implemented for this chunk type.");
        }
    }

    public class AcisChunkChar : AcisChunk
    {
        public AcisChunkChar(char value) : base(AcisConstants.TAG_CHAR, value) { }
        public AcisChunkChar() : base(AcisConstants.TAG_CHAR) { }
        public override string ToString() => (char)Val + " ";
    }

    public class AcisChunkShort : AcisChunk
    {
        public AcisChunkShort(short value) : base(AcisConstants.TAG_SHORT, value) { }
        public AcisChunkShort() : base(AcisConstants.TAG_SHORT) { }
        public override string ToString() => (short)Val + " ";
    }

    public class AcisChunkLong : AcisChunk // Used for TAG_LONG
    {
        public new long Val { get => (long)base.Val; set => base.Val = value; }
        public AcisChunkLong(long value) : base(AcisConstants.TAG_LONG, value) { }
        public AcisChunkLong() : base(AcisConstants.TAG_LONG, 0L) { }
        public override string ToString() => Val.ToString(CultureInfo.InvariantCulture) + " ";
    }

    public class AcisChunkHuge : AcisChunk // Used for TAG_INT64
    {
        public new long Val { get => (long)base.Val; set => base.Val = value; }
        public AcisChunkHuge(long value) : base(AcisConstants.TAG_INT64, value) { }
        public AcisChunkHuge() : base(AcisConstants.TAG_INT64, 0L) { }
        public override string ToString() => Val.ToString(CultureInfo.InvariantCulture) + " ";
    }

    public class AcisChunkFloat : AcisChunk
    {
        public AcisChunkFloat(float value) : base(AcisConstants.TAG_FLOAT, value) { }
        public AcisChunkFloat() : base(AcisConstants.TAG_FLOAT) { }
        public override string ToString() => ((float)Val).ToString("G9") + " "; // G9 for float precision
    }

    public class AcisChunkDouble : AcisChunk
    {
        public new double Val { get => (double)base.Val; set => base.Val = value; }
        public AcisChunkDouble(double value) : base(AcisConstants.TAG_DOUBLE, value) { }
        public AcisChunkDouble() : base(AcisConstants.TAG_DOUBLE, 0.0) { }
        public override string ToString() => Val.ToString("G17", CultureInfo.InvariantCulture) + " ";
    }

    public class AcisChunkUtf8U8 : AcisChunk
    {
        public AcisChunkUtf8U8(string value) : base(AcisConstants.TAG_UTF8_U8, value) { }
        public AcisChunkUtf8U8() : base(AcisConstants.TAG_UTF8_U8) { }
        public override string ToString() => $"@{((string)Val).Length} {(string)Val} ";
    }

    public class AcisChunkUtf8U16 : AcisChunk // Used for text file string representation
    {
        public AcisChunkUtf8U16(string value) : base(AcisConstants.TAG_UTF8_U16, value) { }
        public AcisChunkUtf8U16() : base(AcisConstants.TAG_UTF8_U16) { }
        public override string ToString() => $"@{((string)Val).Length} {(string)Val} ";
    }

    public class AcisChunkUtf8U32A : AcisChunk // TAG_UTF8_U32_A
    {
        public new string Val { get => (string)base.Val; set => base.Val = value; }
        public AcisChunkUtf8U32A(string value) : base(AcisConstants.TAG_UTF8_U32_A, value) { }
        public AcisChunkUtf8U32A() : base(AcisConstants.TAG_UTF8_U32_A, string.Empty) { }
        public override string ToString() => $"@{Val.Length} {Val} "; // Assuming Val is not null
    }

    public class AcisChunkUtf8U32B : AcisChunk // TAG_UTF8_U32_B
    {
        public new string Val { get => (string)base.Val; set => base.Val = value; }
        public AcisChunkUtf8U32B(string value) : base(AcisConstants.TAG_UTF8_U32_B, value) { }
        public AcisChunkUtf8U32B() : base(AcisConstants.TAG_UTF8_U32_B, string.Empty) { }
        public override string ToString() => $"@{Val.Length} {Val} "; // Assuming Val is not null
    }

    public class AcisChunkEntityRef : AcisChunk
    {
        public new long Val { get => (long)base.Val; set => base.Val = value; } // Stores the index, changed to long
        public AcisRecord Record { get; set; }
        public AcisChunkEntityRef(long entityIndex, AcisRecord record = null)
            : base(AcisConstants.TAG_ENTITY_REF, entityIndex)
        {
            Record = record;
        }
        public AcisChunkEntityRef() : base(AcisConstants.TAG_ENTITY_REF, -1L) { } // Default to -1L
        public override string ToString() => $"${Val} ";
    }

    public class AcisChunkIdent : AcisChunk // Base class name
    {
        public AcisChunkIdent(string value) : base(AcisConstants.TAG_IDENT, value) { }
        public AcisChunkIdent() : base(AcisConstants.TAG_IDENT) { }
        public override string ToString() => (string)Val + " ";
    }

    public class AcisChunkSubident : AcisChunk // Sub-class name
    {
        public AcisChunkSubident(string value) : base(AcisConstants.TAG_SUBIDENT, value) { }
        public AcisChunkSubident() : base(AcisConstants.TAG_SUBIDENT) { }
        public override string ToString() => (string)Val + "-";
    }

    public class AcisChunkSubtypeOpen : AcisChunk
    {
        public AcisChunkSubtypeOpen() : base(AcisConstants.TAG_SUBTYPE_OPEN, "{") { }
    }

    public class AcisChunkSubtypeClose : AcisChunk
    {
        public AcisChunkSubtypeClose() : base(AcisConstants.TAG_SUBTYPE_CLOSE, "}") { }
    }

    public class AcisChunkTerminator : AcisChunk
    {
        public AcisChunkTerminator() : base(AcisConstants.TAG_TERMINATOR, "#") { }
        public override string ToString() => "#";
    }

    public class AcisChunkEnumValue : AcisChunk
    {
        public new long Val { get => (long)base.Val; set => base.Val = value; } // Store enum value as long
        public Dictionary<int, string> PossibleValues { get; set; }

        // Constructor for TAG_ENUM_VALUE, value will be set during parsing
        public AcisChunkEnumValue(long value, Dictionary<int, string> possibleValues = null)
            : base(AcisConstants.TAG_ENUM_VALUE, value)
        {
            PossibleValues = possibleValues;
        }
        public AcisChunkEnumValue() : base(AcisConstants.TAG_ENUM_VALUE, 0L) { }

        // Constructor for TAG_TRUE/TAG_FALSE
        public AcisChunkEnumValue(bool boolValue) :
            base(boolValue ? AcisConstants.TAG_TRUE : AcisConstants.TAG_FALSE, boolValue ? 1L : 0L)
        {
            // For boolean, Val will be 1 for true, 0 for false. Tag distinguishes them.
        }

        public override string ToString()
        {
            if (Tag == AcisConstants.TAG_TRUE) return ".T. ";
            if (Tag == AcisConstants.TAG_FALSE) return ".F. ";
            // For TAG_ENUM_VALUE, try to use PossibleValues if available for int-based keys
            if (PossibleValues != null && PossibleValues.TryGetValue((int)Val, out string stringVal))
            {
                return stringVal + " ";
            }
            return Val.ToString(CultureInfo.InvariantCulture) + " ";
        }
    }

    public class AcisChunkPosition : AcisChunk
    {
        public new System.Numerics.Vector3 Val { get => (System.Numerics.Vector3)base.Val; set => base.Val = value; }
        public AcisChunkPosition(System.Numerics.Vector3 value) : base(AcisConstants.TAG_POSITION, value) { }
        public AcisChunkPosition() : base(AcisConstants.TAG_POSITION, System.Numerics.Vector3.Zero) { }
        public override string ToString()
        {
            return $"{Val.X.ToString("G17", CultureInfo.InvariantCulture)} {Val.Y.ToString("G17", CultureInfo.InvariantCulture)} {Val.Z.ToString("G17", CultureInfo.InvariantCulture)} ";
        }
    }

    public class AcisChunkVector3D : AcisChunk
    {
        public new System.Numerics.Vector3 Val { get => (System.Numerics.Vector3)base.Val; set => base.Val = value; }
        public AcisChunkVector3D(System.Numerics.Vector3 value) : base(AcisConstants.TAG_VECTOR_3D, value) { }
        public AcisChunkVector3D() : base(AcisConstants.TAG_VECTOR_3D, System.Numerics.Vector3.Zero) { }
         public override string ToString()
        {
            return $"{Val.X.ToString("G17", CultureInfo.InvariantCulture)} {Val.Y.ToString("G17", CultureInfo.InvariantCulture)} {Val.Z.ToString("G17", CultureInfo.InvariantCulture)} ";
        }
    }

    public class AcisChunkVector2D : AcisChunk
    {
        public new System.Numerics.Vector2 Val { get => (System.Numerics.Vector2)base.Val; set => base.Val = value; }
        public AcisChunkVector2D(System.Numerics.Vector2 value) : base(AcisConstants.TAG_VECTOR_2D, value) { }
        public AcisChunkVector2D() : base(AcisConstants.TAG_VECTOR_2D, System.Numerics.Vector2.Zero) { }
        public override string ToString()
        {
            return $"{Val.X.ToString("G17", CultureInfo.InvariantCulture)} {Val.Y.ToString("G17", CultureInfo.InvariantCulture)} ";
        }
    }
}
