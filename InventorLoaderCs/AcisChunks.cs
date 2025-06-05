using System;

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

    public class AcisChunkLong : AcisChunk
    {
        public AcisChunkLong(long value) : base(AcisConstants.TAG_LONG, value) { } // Python long can be C# long (Int64)
        public AcisChunkLong() : base(AcisConstants.TAG_LONG) { }
        public override string ToString() => (long)Val + " ";
    }

    public class AcisChunkFloat : AcisChunk
    {
        public AcisChunkFloat(float value) : base(AcisConstants.TAG_FLOAT, value) { }
        public AcisChunkFloat() : base(AcisConstants.TAG_FLOAT) { }
        public override string ToString() => ((float)Val).ToString("G9") + " "; // G9 for float precision
    }

    public class AcisChunkDouble : AcisChunk
    {
        public AcisChunkDouble(double value) : base(AcisConstants.TAG_DOUBLE, value) { }
        public AcisChunkDouble() : base(AcisConstants.TAG_DOUBLE) { }
        public override string ToString() => ((double)Val).ToString("G17") + " "; // G17 for double precision
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


    public class AcisChunkEntityRef : AcisChunk
    {
        public AcisRecord Record { get; set; } // Reference to the actual AcisRecord
        public AcisChunkEntityRef(int entityIndex, AcisRecord record = null)
            : base(AcisConstants.TAG_ENTITY_REF, entityIndex)
        {
            Record = record;
        }
        public AcisChunkEntityRef() : base(AcisConstants.TAG_ENTITY_REF, -1) { }
        public override string ToString() => $"${(int)Val} ";
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
        public Dictionary<int, string> PossibleValues { get; set; } // To store the enum definition like SENSE, BOOLEAN
        public AcisChunkEnumValue(int tag, object value, Dictionary<int, string> possibleValues = null)
            : base(tag, value) // tag could be TAG_TRUE, TAG_FALSE, or TAG_ENUM_VALUE
        {
            PossibleValues = possibleValues;
        }
        public AcisChunkEnumValue() : base(AcisConstants.TAG_ENUM_VALUE) { }

        public override string ToString()
        {
            if (PossibleValues != null && Val is int intVal && PossibleValues.TryGetValue(intVal, out string stringVal))
            {
                return stringVal + " ";
            }
            return Val?.ToString() + " ";
        }
    }

    public class AcisChunkPosition : AcisChunk // Vector3D, scaled
    {
        public AcisChunkPosition(System.Numerics.Vector3 value) : base(AcisConstants.TAG_POSITION, value) { }
        public AcisChunkPosition() : base(AcisConstants.TAG_POSITION) { }
        public override string ToString()
        {
            var v = (System.Numerics.Vector3)Val;
            return $"{v.X} {v.Y} {v.Z} ";
        }
    }

    public class AcisChunkVector3D : AcisChunk // Vector3D, normalized
    {
        public AcisChunkVector3D(System.Numerics.Vector3 value) : base(AcisConstants.TAG_VECTOR_3D, value) { }
        public AcisChunkVector3D() : base(AcisConstants.TAG_VECTOR_3D) { }
         public override string ToString()
        {
            var v = (System.Numerics.Vector3)Val;
            return $"{v.X} {v.Y} {v.Z} ";
        }
    }

    public class AcisChunkVector2D : AcisChunk
    {
        public AcisChunkVector2D(System.Numerics.Vector2 value) : base(AcisConstants.TAG_VECTOR_2D, value) { }
        public AcisChunkVector2D() : base(AcisConstants.TAG_VECTOR_2D) { }
        public override string ToString()
        {
            var v = (System.Numerics.Vector2)Val;
            return $"{v.X} {v.Y} ";
        }
    }
}
