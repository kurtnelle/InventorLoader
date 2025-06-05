using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;

namespace InventorLoaderCs
{
    // Placeholder enums that would ideally be in a shared location like AcisUtils.cs or ImporterConstants.cs
    public enum SenseEnum { FORWARD, REVERSED, UNKNOWN } // From Acis.py SENSE
    public enum RotationFlagEnum { NO_ROTATE, ROTATE }
    public enum ReflectionFlagEnum { NO_REFLECT, REFLECT }
    public enum ShearFlagEnum { NO_SHEAR, SHEAR }
    public enum RangeTypeEnum { I, F } // For Interval parsing, from Acis.py RANGE ('I'/'F')


    public static class AcisParsingUtils
    {
        private static T GetChunkVal<T>(AcisRecord record, ref int chunkIndex, string fieldNameForError)
        {
            if (record.Chunks.Count <= chunkIndex)
            {
                Logger.Error($"AcisParsingUtils: Expected chunk for '{fieldNameForError}' at index {chunkIndex}, but record only has {record.Chunks.Count} chunks. Record: {record.Name} #{record.Index}");
                throw new IndexOutOfRangeException($"Chunk index {chunkIndex} out of bounds for record {record.Name} #{record.Index} while reading '{fieldNameForError}'.");
            }
            var chunk = record.Chunks[chunkIndex++];
            if (chunk.Val is T val)
            {
                return val;
            }
            try { return (T)Convert.ChangeType(chunk.Val, typeof(T), CultureInfo.InvariantCulture); }
            catch (Exception ex) {
                Logger.Error($"AcisParsingUtils: Type mismatch for chunk '{fieldNameForError}'. Expected {typeof(T).Name}, got {chunk.Val?.GetType().Name}. Value: '{chunk.Val}'. Record: {record.Name} #{record.Index}. Ex: {ex.Message}");
                throw;
            }
        }

        public static double GetFloat(AcisRecord record, ref int chunkIndex, string fieldName = "Float") =>
            GetChunkVal<double>(record, ref chunkIndex, fieldName);

        public static int GetInteger(AcisRecord record, ref int chunkIndex, string fieldName = "Integer") =>
            GetChunkVal<int>(record, ref chunkIndex, fieldName);

        public static long GetLong(AcisRecord record, ref int chunkIndex, string fieldName = "Long") =>
            GetChunkVal<long>(record, ref chunkIndex, fieldName);

        public static bool GetBoolean(AcisRecord record, ref int chunkIndex, string fieldName = "Boolean")
        {
            if (record.Chunks.Count <= chunkIndex) throw new IndexOutOfRangeException($"Chunk index {chunkIndex} for {fieldName} out of bounds.");
            var chunk = record.Chunks[chunkIndex];
            if (chunk is AcisChunkEnumValue enumChunk)
            {
                chunkIndex++;
                return enumChunk.Tag == AcisConstants.TAG_TRUE;
            }
            string textVal = GetChunkVal<string>(record, ref chunkIndex, fieldName); // Consumes chunk via GetChunkVal
            if (AcisEnums.BOOLEAN_TEXT_MAP.TryGetValue(textVal, out bool mappedVal)) return mappedVal;

            Logger.Warning($"AcisParsingUtils: Could not parse boolean for '{fieldName}' from value '{textVal}'. Defaulting to false. Record: {record.Name} #{record.Index}");
            return false;
        }

        public static string GetText(AcisRecord record, ref int chunkIndex, string fieldName = "Text") =>
            GetChunkVal<string>(record, ref chunkIndex, fieldName);

        public static Vector3 GetPoint(AcisRecord record, ref int chunkIndex, string fieldName = "Point")
        {
            if (record.Chunks[chunkIndex].Val is System.Numerics.Vector3 vecVal) {
                chunkIndex++;
                return vecVal;
            }
            double x = GetChunkVal<double>(record, ref chunkIndex, fieldName + ".X");
            double y = GetChunkVal<double>(record, ref chunkIndex, fieldName + ".Y");
            double z = GetChunkVal<double>(record, ref chunkIndex, fieldName + ".Z");
            return new Vector3((float)x, (float)y, (float)z);
        }

        public static Vector3 GetVector(AcisRecord record, ref int chunkIndex, string fieldName = "Vector") =>
            GetPoint(record, ref chunkIndex, fieldName);

        public static Vector3 GetLocation(AcisRecord record, ref int chunkIndex, AcisHeader header, string fieldName = "Location")
        {
            var point = GetPoint(record, ref chunkIndex, fieldName);
            return point * (float)header.Scale;
        }

        public static double GetLength(AcisRecord record, ref int chunkIndex, AcisHeader header, string fieldName = "Length")
        {
            double val = GetChunkVal<double>(record, ref chunkIndex, fieldName);
            return val * header.Scale;
        }

        public static AcisEntity GetRefNode(AcisRecord record, ref int chunkIndex, string fieldName = "RefNode", string expectedEntityType = null)
        {
            if (record.Chunks.Count <= chunkIndex) {
                 Logger.Warning($"AcisParsingUtils: Expected AcisChunkEntityRef for '{fieldName}', but no more chunks. Record: {record.Name} #{record.Index}");
                 return null;
            }
            if (record.Chunks[chunkIndex] is AcisChunkEntityRef refChunk)
            {
                chunkIndex++;
                AcisEntity entity = refChunk.Record?.Entity;
                if (entity == null && refChunk.Val is int refIdx && refIdx == -1) return null;

                if (expectedEntityType != null && entity != null && entity.Record.Name != expectedEntityType && !entity.GetType().Name.Equals(expectedEntityType, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Warning($"AcisParsingUtils: Type mismatch for entity reference '{fieldName}'. Expected '{expectedEntityType}', got '{entity?.Record.Name ?? "null"}'. Record: {record.Name} #{record.Index}");
                }
                return entity;
            }
            Logger.Error($"AcisParsingUtils: Expected AcisChunkEntityRef for '{fieldName}' but found {record.Chunks[chunkIndex].GetType().Name}. Record: {record.Name} #{record.Index}");
            chunkIndex++;
            return null;
        }

        public static TEnum GetEnumByTag<TEnum>(AcisRecord record, ref int chunkIndex, string fieldName = "Enum") where TEnum : struct, Enum
        {
            if (record.Chunks.Count <= chunkIndex) throw new IndexOutOfRangeException($"Chunk index {chunkIndex} for {fieldName} out of bounds.");
            var chunk = record.Chunks[chunkIndex];
            object rawVal = chunk.Val;

            if (chunk is AcisChunkEnumValue enumChunk && enumChunk.Val is int intEnumVal)
            {
                rawVal = intEnumVal;
            }
             else if (chunk.Val is string strVal)
            {
                if (AcisUtils.TryGetEnumValue(strVal, out var mappedAcisChunkEnum) && mappedAcisChunkEnum.Val is int mappedIntVal)
                {
                     rawVal = mappedIntVal;
                }
                else if (Enum.TryParse<TEnum>(strVal, true, out TEnum parsedStrEnum)) {
                    chunkIndex++;
                    return parsedStrEnum;
                }
                else
                {
                    Logger.Warning($"AcisParsingUtils: Could not parse string '{strVal}' to enum {typeof(TEnum).Name} for field '{fieldName}'. Record: {record.Name} #{record.Index}. Defaulting to 0.");
                    rawVal = 0;
                }
            }

            chunkIndex++;
            try { return (TEnum)Enum.ToObject(typeof(TEnum), Convert.ToInt32(rawVal)); }
            catch {
                Logger.Warning($"AcisParsingUtils: Could not convert value '{rawVal}' to enum {typeof(TEnum).Name} for field '{fieldName}'. Record: {record.Name} #{record.Index}. Defaulting to first enum value.");
                return default(TEnum);
            }
        }

        public static Range GetRange(AcisRecord record, ref int chunkIndex, AcisHeader header, double defaultValue, string fieldName = "Range")
        {
            string typeStr = GetText(record, ref chunkIndex, fieldName + ".Type"); // Changed from GetChunkVal
            RangeTypeEnum type = typeStr.Equals("I", StringComparison.OrdinalIgnoreCase) ? RangeTypeEnum.I : RangeTypeEnum.F;
            double limit = defaultValue;
            if (type == RangeTypeEnum.F)
            {
                limit = GetFloat(record, ref chunkIndex, fieldName + ".Limit"); // Changed from GetChunkVal
            }
            return new Range(type.ToString(), limit, header.Scale);
        }

        public static Interval GetInterval(AcisRecord record, ref int chunkIndex, AcisHeader header, double defaultMin, double defaultMax, string fieldName = "Interval")
        {
            Range lower = GetRange(record, ref chunkIndex, header, defaultMin, fieldName + ".Lower");
            Range upper = GetRange(record, ref chunkIndex, header, defaultMax, fieldName + ".Upper");
            return new Interval(lower, upper);
        }

        public static object GetUnknownFTPlaceholder(AcisRecord record, ref int chunkIndex, string fieldName = "UnknownFT")
        {
            Logger.Info($"AcisParsingUtils: Reading placeholder for UnknownFT structure: {fieldName}");
            if (record.Chunks.Count <= chunkIndex)
            {
                Logger.Warning($"AcisParsingUtils: Not enough chunks for UnknownFT '{fieldName}' at index {chunkIndex}.");
                return null;
            }
            string val1 = GetText(record, ref chunkIndex, fieldName + ".Val1");
            if (val1 == null) return null;
            var parsedData = new Dictionary<string, object> { { "Val1", val1 } };
            if (val1.Equals("T", StringComparison.OrdinalIgnoreCase))
            {
                if (record.Chunks.Count > chunkIndex + 5)
                {
                    float[] floats = new float[6];
                    for (int i = 0; i < 6; i++) floats[i] = (float)GetFloat(record, ref chunkIndex, $"{fieldName}.Float{i}");
                    parsedData["Floats"] = floats;
                } else Logger.Warning($"AcisParsingUtils: Not enough chunks for 6 floats in UnknownFT '{fieldName}' after 'T'.");
                if (record.Chunks.Count > chunkIndex) parsedData["Val2"] = GetText(record, ref chunkIndex, fieldName + ".Val2");
                else Logger.Warning($"AcisParsingUtils: Not enough chunks for Val2 in UnknownFT '{fieldName}' after 'T' and floats.");
            }
            return parsedData;
        }

        public static SidesEnum GetSides(AcisRecord record, ref int chunkIndex, out FaceSideEnum? containmentSide, string fieldName = "SidesInfo")
        {
            containmentSide = null;
            SidesEnum sides = GetEnumByTag<SidesEnum>(record, ref chunkIndex, fieldName + ".Sides");
            if (sides == SidesEnum.DOUBLE)
            {
                containmentSide = GetEnumByTag<FaceSideEnum>(record, ref chunkIndex, fieldName + ".ContainmentSide");
            }
            return sides;
        }

        public static bool GetDimensionCurve(AcisRecord record, ref int chunkIndex, out bool isNurbs, out int degree, string fieldName = "DimensionCurve")
        {
            isNurbs = false;
            degree = 0;
            string typeStr = GetText(record, ref chunkIndex, fieldName + ".Type");
            if (typeStr == null) return false;

            if (typeStr.Equals("nullbs", StringComparison.OrdinalIgnoreCase)) return true;

            if (typeStr.Equals("nurbs", StringComparison.OrdinalIgnoreCase)) isNurbs = true;
            else if (!typeStr.Equals("nubs", StringComparison.OrdinalIgnoreCase))
            {
                Logger.Warning($"AcisParsingUtils: Unknown dimension curve type '{typeStr}' for {fieldName}. Record: {record.Name} #{record.Index}");
            }
            degree = GetInteger(record, ref chunkIndex, fieldName + ".Degree");
            return true;
        }

        public static bool GetClosureCurve(AcisRecord record, ref int chunkIndex, out string closureType, out int knotCount, string fieldName = "ClosureCurve")
        {
            closureType = "open";
            knotCount = 0;
            var chunk = record.Chunks[chunkIndex];
            if (chunk.Val is string strVal)
            {
                closureType = strVal.ToLowerInvariant();
                chunkIndex++;
            }
            else if (chunk.Val is int intVal || chunk.Val is long longVal)
            {
                int val = (chunk.Val is int) ? (int)chunk.Val : (int)(long)chunk.Val;
                chunkIndex++;
                switch(val) { case 0: closureType = "open"; break; case 1: closureType = "closed"; break; case 2: closureType = "periodic"; break; default: Logger.Warning($"AcisParsingUtils: Unknown closure type value '{val}' for {fieldName}. Defaulting to open."); break; }
            }
            else { Logger.Error($"AcisParsingUtils: Unexpected chunk type for closure: {chunk.GetType().Name}"); chunkIndex++; }
            knotCount = GetInteger(record, ref chunkIndex, fieldName + ".KnotCount");
            return true;
        }

        public static bool GetDimensionSurface(AcisRecord record, ref int chunkIndex, out bool isNurbs, out int uDegree, out int vDegree, string fieldName = "DimensionSurface")
        {
            isNurbs = false; uDegree = 0; vDegree = 0;
            string typeStr = GetText(record, ref chunkIndex, fieldName + ".Type");
            if (typeStr == null) return false;
            if (typeStr.Equals("nullbs", StringComparison.OrdinalIgnoreCase)) return true;
            if (typeStr.Equals("nurbs", StringComparison.OrdinalIgnoreCase)) isNurbs = true;
            else if (!typeStr.Equals("nubs", StringComparison.OrdinalIgnoreCase) && !typeStr.Equals("summary", StringComparison.OrdinalIgnoreCase))
            {
                Logger.Warning($"AcisParsingUtils: Unknown dimension surface type '{typeStr}' for {fieldName}. Record: {record.Name} #{record.Index}");
            }
            uDegree = GetInteger(record, ref chunkIndex, fieldName + ".UDegree");
            vDegree = GetInteger(record, ref chunkIndex, fieldName + ".VDegree");
            return true;
        }

        public static bool GetClosureSurface(AcisRecord record, ref int chunkIndex,
                                         out string uClosure, out string vClosure,
                                         out string uSingularity, out string vSingularity,
                                         out int uKnotCount, out int vKnotCount,
                                         string fieldName = "ClosureSurface")
        {
            uClosure = "open"; vClosure = "open";
            uSingularity = "none"; vSingularity = "none";
            uKnotCount = 0; vKnotCount = 0;

            // This is a simplification. Python code uses getEnumByValue with specific dictionaries.
            // Here, assuming text chunks for these string-like enum values.
            uClosure = GetText(record, ref chunkIndex, fieldName + ".UClosure").ToLowerInvariant();
            vClosure = GetText(record, ref chunkIndex, fieldName + ".VClosure").ToLowerInvariant();
            uSingularity = GetText(record, ref chunkIndex, fieldName + ".USingularity").ToLowerInvariant();
            vSingularity = GetText(record, ref chunkIndex, fieldName + ".VSingularity").ToLowerInvariant();

            uKnotCount = GetInteger(record, ref chunkIndex, fieldName + ".UKnotCount");
            vKnotCount = GetInteger(record, ref chunkIndex, fieldName + ".VKnotCount");
            return true;
        }
    }
}
