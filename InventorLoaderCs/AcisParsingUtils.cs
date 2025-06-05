using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Linq; // Required for Sum()

namespace InventorLoaderCs
{
    // Enums like SenseEnum, RotationFlagEnum, etc., are in AcisEntities.cs
    // RangeTypeEnum is here as it's closely tied to GetRange parsing logic.
    public enum RangeTypeEnum { I, F }


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
            string textVal = GetText(record, ref chunkIndex, fieldName); // Use GetText to consume chunk
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

                if (expectedEntityType != null && entity != null && entity.Record.Name != expectedEntityType && !entity.GetType().Name.Equals(expectedEntityType, StringComparison.OrdinalIgnoreCase) && entity.SubtypeName != expectedEntityType)
                {
                    Logger.Warning($"AcisParsingUtils: Type mismatch for entity reference '{fieldName}'. Expected '{expectedEntityType}', got '{entity?.Record.Name ?? "null"}' (Subtype: {entity?.SubtypeName ?? "N/A"}). Record: {record.Name} #{record.Index}");
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
                    chunkIndex++; // Consumed the string chunk
                    return parsedStrEnum;
                }
                else
                {
                    Logger.Warning($"AcisParsingUtils: Could not parse string '{strVal}' to enum {typeof(TEnum).Name} for field '{fieldName}'. Record: {record.Name} #{record.Index}. Defaulting to 0.");
                    rawVal = 0;
                }
            }

            chunkIndex++; // Consumed the chunk that contained rawVal
            try { return (TEnum)Enum.ToObject(typeof(TEnum), Convert.ToInt32(rawVal)); }
            catch {
                Logger.Warning($"AcisParsingUtils: Could not convert value '{rawVal}' to enum {typeof(TEnum).Name} for field '{fieldName}'. Record: {record.Name} #{record.Index}. Defaulting to first enum value.");
                return default(TEnum);
            }
        }

        public static Range GetRange(AcisRecord record, ref int chunkIndex, AcisHeader header, double defaultValue, string fieldName = "Range")
        {
            string typeStr = GetText(record, ref chunkIndex, fieldName + ".Type");
            RangeTypeEnum type = typeStr.Equals("I", StringComparison.OrdinalIgnoreCase) ? RangeTypeEnum.I : RangeTypeEnum.F;
            double limit = defaultValue;
            if (type == RangeTypeEnum.F)
            {
                limit = GetFloat(record, ref chunkIndex, fieldName + ".Limit");
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
            if (record.Chunks.Count <= chunkIndex) { Logger.Error($"AcisParsingUtils: Not enough chunks for {fieldName}."); return false; }
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

            uClosure = GetText(record, ref chunkIndex, fieldName + ".UClosure").ToLowerInvariant();
            vClosure = GetText(record, ref chunkIndex, fieldName + ".VClosure").ToLowerInvariant();
            uSingularity = GetText(record, ref chunkIndex, fieldName + ".USingularity").ToLowerInvariant();
            vSingularity = GetText(record, ref chunkIndex, fieldName + ".VSingularity").ToLowerInvariant();

            uKnotCount = GetInteger(record, ref chunkIndex, fieldName + ".UKnotCount");
            vKnotCount = GetInteger(record, ref chunkIndex, fieldName + ".VKnotCount");
            return true;
        }

        public static bool ReadKnotsAndMults(AcisRecord record, ref int chunkIndex, int numKnotsFromHeader,
                                           out List<double> knots, out List<int> mults, string fieldName)
        {
            knots = new List<double>(numKnotsFromHeader);
            mults = new List<int>(numKnotsFromHeader);
            Logger.Info($"AcisParsingUtils: Reading {numKnotsFromHeader} knots and multiplicities for {fieldName}. Record: {record.Name} #{record.Index}");

            for (int i = 0; i < numKnotsFromHeader; i++)
            {
                if (record.Chunks.Count <= chunkIndex + 1)
                {
                    Logger.Error($"AcisParsingUtils: Not enough chunks for knot/multiplicity pair {i + 1} for {fieldName}. Record: {record.Name} #{record.Index}");
                    return false;
                }
                knots.Add(GetFloat(record, ref chunkIndex, $"{fieldName}.Knot{i}"));
                mults.Add(GetInteger(record, ref chunkIndex, $"{fieldName}.Mult{i}"));
            }
            return true;
        }

        public static void AdjustKnotsAndMults(List<double> knots, List<int> mults, int degree)
        {
            if (mults == null || mults.Count == 0)
            {
                Logger.Warning("AcisParsingUtils.AdjustKnotsAndMults: Multiplicity list is null or empty.");
                return;
            }
            if (mults.Count > 0) mults[0] = degree + 1; // Clamped start
            if (mults.Count > 1) mults[mults.Count - 1] = degree + 1; // Clamped end
        }

        public static bool ReadPoints2DList(AcisRecord record, ref int chunkIndex, AcisHeader header,
                                          BSCurveData splineData, int expectedPoleCount, string fieldName)
        {
            Logger.Info($"AcisParsingUtils: Reading {expectedPoleCount} 2D poles for {fieldName}. Rational: {splineData.IsRational}. Record: {record.Name} #{record.Index}");
            splineData.Poles2D = new List<Vector2>(expectedPoleCount);
            if (splineData.IsRational) splineData.Weights = new List<double>(expectedPoleCount);

            for (int i = 0; i < expectedPoleCount; i++)
            {
                if (record.Chunks.Count <= chunkIndex + 1)
                {
                    Logger.Error($"AcisParsingUtils: Not enough chunks for 2D pole {i + 1} for {fieldName}. Record: {record.Name} #{record.Index}");
                    return false;
                }
                float x = (float)GetFloat(record, ref chunkIndex, $"{fieldName}.Pole{i}.X");
                float y = (float)GetFloat(record, ref chunkIndex, $"{fieldName}.Pole{i}.Y");
                splineData.Poles2D.Add(new Vector2(x, y));

                if (splineData.IsRational)
                {
                    if (record.Chunks.Count <= chunkIndex) { Logger.Error($"AcisParsingUtils: Not enough chunks for weight of 2D pole {i + 1} for {fieldName}."); return false; }
                    splineData.Weights.Add(GetFloat(record, ref chunkIndex, $"{fieldName}.Weight{i}"));
                }
            }
            return true;
        }

        public static bool ReadPoints3DList(AcisRecord record, ref int chunkIndex, AcisHeader header,
                                          BSCurveData splineData, int expectedPoleCount, string fieldName)
        {
            Logger.Info($"AcisParsingUtils: Reading {expectedPoleCount} 3D poles for {fieldName}. Rational: {splineData.IsRational}. Record: {record.Name} #{record.Index}");
            splineData.Poles3D = new List<Vector3>(expectedPoleCount);
            if (splineData.IsRational) splineData.Weights = new List<double>(expectedPoleCount);

            for (int i = 0; i < expectedPoleCount; i++)
            {
                 if (record.Chunks.Count <= chunkIndex + 2)
                {
                    Logger.Error($"AcisParsingUtils: Not enough chunks for 3D pole {i + 1} for {fieldName}. Record: {record.Name} #{record.Index}");
                    return false;
                }
                splineData.Poles3D.Add(GetLocation(record, ref chunkIndex, header, $"{fieldName}.Pole{i}"));

                if (splineData.IsRational)
                {
                    if (record.Chunks.Count <= chunkIndex) { Logger.Error($"AcisParsingUtils: Not enough chunks for weight of 3D pole {i + 1} for {fieldName}."); return false; }
                    splineData.Weights.Add(GetFloat(record, ref chunkIndex, $"{fieldName}.Weight{i}"));
                }
            }
            return true;
        }

        public static bool ReadPoints3DSurface(AcisRecord record, ref int chunkIndex, AcisHeader header,
                                             BSSurfaceData splineData, int expectedUPoleCount, int expectedVPoleCount,
                                             string fieldName)
        {
            Logger.Info($"AcisParsingUtils: Reading {expectedUPoleCount}x{expectedVPoleCount} 3D surface poles for {fieldName}. Rational: {splineData.IsRational}. Record: {record.Name} #{record.Index}");
            splineData.Poles3DGrid = new List<List<Vector3>>(expectedUPoleCount);
            if (splineData.IsRational) splineData.WeightsGrid = new List<List<double>>(expectedUPoleCount);

            for (int u = 0; u < expectedUPoleCount; u++)
            {
                var poleRow = new List<Vector3>(expectedVPoleCount);
                var weightRow = splineData.IsRational ? new List<double>(expectedVPoleCount) : null;
                for (int v = 0; v < expectedVPoleCount; v++)
                {
                    if (record.Chunks.Count <= chunkIndex + 2)
                    {
                        Logger.Error($"AcisParsingUtils: Not enough chunks for 3D surface pole U{u}V{v} for {fieldName}. Record: {record.Name} #{record.Index}");
                        return false;
                    }
                    poleRow.Add(GetLocation(record, ref chunkIndex, header, $"{fieldName}.PoleU{u}V{v}"));
                    if (splineData.IsRational)
                    {
                        if (record.Chunks.Count <= chunkIndex) { Logger.Error($"AcisParsingUtils: Not enough chunks for weight of 3D surface pole U{u}V{v} for {fieldName}."); return false; }
                        weightRow.Add(GetFloat(record, ref chunkIndex, $"{fieldName}.WeightU{u}V{v}"));
                    }
                }
                splineData.Poles3DGrid.Add(poleRow);
                if (splineData.IsRational) splineData.WeightsGrid.Add(weightRow);
            }
            return true;
        }

        public static List<double> GetFloatArray(AcisRecord record, ref int chunkIndex, string fieldName)
        {
            Logger.Info($"AcisParsingUtils: Reading FloatArray for {fieldName}. Record: {record.Name} #{record.Index}");
            int count = GetInteger(record, ref chunkIndex, fieldName + ".Count");
            var list = new List<double>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(GetFloat(record, ref chunkIndex, $"{fieldName}.Value{i}"));
            }
            return list;
        }

        public static Law ReadLaw(AcisRecord record, ref int chunkIndex, AcisHeader header, string fieldName)
        {
            Logger.Info($"AcisParsingUtils: Reading Law for {fieldName}. Record: {record.Name} #{record.Index}");
            string lawTypeStringFromFile = GetText(record, ref chunkIndex, fieldName + ".LawTypeString");

            Law law = new Law(lawTypeStringFromFile); // Constructor sets LawTypeString

            switch (law.LawTypeString.ToLowerInvariant())
            {
                case "null_law":
                    // No further parameters. LawTypeString is already "null_law".
                    break;
                case "trans":
                    double[] m = new double[13];
                    for (int i = 0; i < 13; i++) m[i] = GetFloat(record, ref chunkIndex, $"{fieldName}.TRANS.MatrixVal{i}");
                    bool rot = GetEnumByTag<RotationFlagEnum>(record, ref chunkIndex, $"{fieldName}.TRANS.HasRotation") == RotationFlagEnum.ROTATE;
                    bool refl = GetEnumByTag<ReflectionFlagEnum>(record, ref chunkIndex, $"{fieldName}.TRANS.HasReflection") == ReflectionFlagEnum.REFLECT;
                    bool shr = GetEnumByTag<ShearFlagEnum>(record, ref chunkIndex, $"{fieldName}.TRANS.HasShear") == ShearFlagEnum.SHEAR;
                    law.Parameters.Add(new LawTransformParameter(m, rot, refl, shr));
                    break;
                case "edge":
                    var edgeParam = new LawEdgeParameter
                    {
                        ReferencedCurve = GetRefNode(record, ref chunkIndex, fieldName + ".EDGE.CurveRef", "curve-entity") as Curve,
                        Param1 = GetFloat(record, ref chunkIndex, fieldName + ".EDGE.FloatParam1"),
                        Param2 = GetFloat(record, ref chunkIndex, fieldName + ".EDGE.FloatParam2")
                    };
                    law.Parameters.Add(edgeParam);
                    break;
                case "spline_law":
                    var splineParam = new LawSplineLawParameter
                    {
                        Type = GetInteger(record, ref chunkIndex, fieldName + ".SPLINE_LAW.IntParam"),
                        Knots = GetFloatArray(record, ref chunkIndex, fieldName + ".SPLINE_LAW.KnotsArray"),
                        Values = GetFloatArray(record, ref chunkIndex, fieldName + ".SPLINE_LAW.ValuesArray"),
                        Point = GetPoint(record, ref chunkIndex, fieldName + ".SPLINE_LAW.PointParam")
                    };
                    law.Parameters.Add(splineParam);
                    break;
                // Cases for constant_law, linear_law etc. could be added here if they have specific structures
                // For example:
                // case "constant_law":
                //    law.ConstantValue = GetFloat(record, ref chunkIndex, fieldName + ".ConstantValue");
                //    break;
                default:
                    // The lawTypeString is the equation itself.
                    law.EquationString = law.LawTypeString;
                    Logger.Info($"AcisParsingUtils: Law type '{law.LawTypeString}' treated as generic equation string for {fieldName}.");
                    break;
            }
            return law;
        }

        public static Law ReadFormulaStructure(AcisRecord record, ref int chunkIndex, AcisHeader header, string fieldName)
        {
            Logger.Info($"AcisParsingUtils: Reading FormulaStructure for {fieldName}. Record: {record.Name} #{record.Index}");

            // The first part of a formula-structure is the primary law/equation string itself.
            string primaryLawString = GetText(record, ref chunkIndex, fieldName + ".PrimaryLawString");
            Law mainLaw = new Law(primaryLawString); // Sets LawTypeString
            if (! (primaryLawString.ToLowerInvariant() == "null_law" ||
                   primaryLawString.ToLowerInvariant() == "trans" ||
                   primaryLawString.ToLowerInvariant() == "edge" ||
                   primaryLawString.ToLowerInvariant() == "spline_law") ) // Add other known types
            {
                 mainLaw.EquationString = primaryLawString; // If it's not a known keyword, it's an equation
            }
            // If primaryLawString is "TRANS", "EDGE", etc., ReadLaw would handle its specific parameters.
            // However, the structure of formula-structure in ACIS is typically:
            // formula-structure { "equation_string_or_main_law_type" num_vars {var1_law} {var2_law} ... }
            // So, if primaryLawString was "TRANS", its specific params (matrix, flags) would be read *inside* a ReadLaw call
            // if ReadFormulaStructure was designed to call ReadLaw for the main part.
            // The current design based on Python seems to take the first string as the literal equation or a simple type.
            // Let's stick to the provided structure: first string is primary, then num_vars, then var_laws.

            int numVars = GetInteger(record, ref chunkIndex, fieldName + ".NumVariables");
            for (int i = 0; i < numVars; i++)
            {
                // Each "variable" is itself a Law structure, potentially complex.
                mainLaw.Parameters.Add(ReadLaw(record, ref chunkIndex, header, $"{fieldName}.VariableLaw{i}"));
            }
            return mainLaw;
        }

        public static Helix ReadHelixData(AcisRecord record, ref int chunkIndex, AcisHeader header, string fieldName)
        {
            Logger.Info($"AcisParsingUtils: Reading HelixData for {fieldName}. Record: {record.Name} #{record.Index}");
            Helix helix = new Helix();

            helix.RadAngles = GetInterval(record, ref chunkIndex, header, 0.0, 2.0 * Math.PI, fieldName + ".RadAngles");
            helix.PosCenter = GetLocation(record, ref chunkIndex, header, fieldName + ".PosCenter");
            helix.DirMajor = GetLocation(record, ref chunkIndex, header, fieldName + ".DirMajor");
            helix.DirMinor = GetLocation(record, ref chunkIndex, header, fieldName + ".DirMinor");
            helix.DirPitch = GetLocation(record, ref chunkIndex, header, fieldName + ".DirPitch");
            helix.FacApex = (float)GetFloat(record, ref chunkIndex, fieldName + ".FacApex");
            helix.VecAxis = GetVector(record, ref chunkIndex, fieldName + ".VecAxis");

            // Skip the four null references (surface1, surface2, pcurve1, pcurve2)
            // Each ref node takes one chunk
            for(int i=0; i < 4; i++)
            {
                // We expect these to be "$-1" or similar null refs.
                var nullRef = GetRefNode(record, ref chunkIndex, $"{fieldName}.NullRef{i+1}");
                if (nullRef != null)
                {
                    Logger.Warning($"AcisParsingUtils: Expected null reference for Helix trailing data {i+1}, but got {nullRef.Record.Name}. Field: {fieldName}");
                }
            }

            return helix;
        }

        public static List<Tuple<int, int>> GetDcIndexMappings(AcisRecord record, ref int chunkIndex, string fieldName)
        {
            var mappings = new List<Tuple<int, int>>();
            int count = GetInteger(record, ref chunkIndex, fieldName + ".Count");
            for (int i = 0; i < count; i++)
            {
                int dcIdx = GetInteger(record, ref chunkIndex, $"{fieldName}.DcIdx{i}");
                int value = GetInteger(record, ref chunkIndex, $"{fieldName}.Value{i}");
                mappings.Add(new Tuple<int, int>(dcIdx, value));
            }
            return mappings;
        }
    }
}
