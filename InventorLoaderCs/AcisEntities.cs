using System;
using System.Collections.Generic;
using System.Numerics;
using System.IO;

namespace InventorLoaderCs
{
    public enum SenseEnum { FORWARD, REVERSED, UNKNOWN }
    public enum SidesEnum { SINGLE, DOUBLE }
    public enum FaceSideEnum { OUT, IN }
    public enum RotationFlagEnum { NO_ROTATE, ROTATE }
    public enum ReflectionFlagEnum { NO_REFLECT, REFLECT }
    public enum ShearFlagEnum { NO_SHEAR, SHEAR }
    public enum SenseVEnum { FORWARD_V, REVERSE_V, UNKNOWN_V }


    public abstract class AcisEntity
    {
        public AcisRecord Record { get; set; }
        public AcisEntity Attrib { get; set; }
        public int History { get; set; }
        public object Shape { get; set; }

        protected bool ReadyToBuild { get; set; } = true;
        protected int CurrentChunkIndex { get; set; }

        public int Index => Record?.Index ?? -1;
        public string TypeName => Record?.Name ?? GetType().Name;
        public string SubtypeName { get; protected set; }

        protected AcisEntity(string subtypeName = null)
        {
            History = -1;
            SubtypeName = subtypeName ?? GetType().Name.ToLowerInvariant().Replace("acis", "") + "-entity";
        }

        public virtual int Set(AcisRecord record)
        {
            Record = record;
            var reader = AcisGlobalUtils.GetReader();
            var header = reader?.Header;

            CurrentChunkIndex = 0;
            if (record.Chunks.Count > CurrentChunkIndex)
                Attrib = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "Attribute");
            else Attrib = null;

            if (header != null && header.Version > 6.0)
            {
                if (record.Chunks.Count > CurrentChunkIndex)
                {
                    History = AcisParsingUtils.GetInteger(record, ref CurrentChunkIndex, "HistoryIndex");
                }
                else
                {
                    Logger.Warning($"AcisEntity.Set: Not enough chunks for HistoryIndex. Record: {record.Name} #{record.Index}, Version: {header.Version}");
                    History = -1;
                }
            }
            else History = -1;
            return CurrentChunkIndex;
        }
        public string GetName() { /* Deferred */ return null; }
        public ColorPlaceholder? GetColor() { /* Deferred */ return null; }
    }

    public abstract class Geometry : AcisEntity
    {
        protected Geometry(string subtypeName) : base(subtypeName) { }
        public override int Set(AcisRecord record)
        {
            base.Set(record);
            var header = AcisGlobalUtils.GetReader()?.Header;
            if (header == null) return CurrentChunkIndex;

            if (header.Version > 6.0)
            {
                if (Record.Chunks.Count > CurrentChunkIndex) AcisParsingUtils.GetRefNode(Record, ref CurrentChunkIndex, "GeometryUnknownRef1");
                else Logger.Warning($"Geometry.Set: Expected GeometryUnknownRef1 for {Record.Name} #{Record.Index}, missing. Version: {header.Version}");
            }
            if (header.Version > 10.0 && !(header.Format?.StartsWith("ASM") ?? false))
            {
                if (Record.Chunks.Count > CurrentChunkIndex) { CurrentChunkIndex++; Logger.Info($"Geometry.Set: Skipped chunk for version > 10.0 (non-ASM). Record: {Record.Name}"); }
                else Logger.Warning($"Geometry.Set: Expected chunk to skip for version > 10.0 (non-ASM) for {Record.Name} #{Record.Index}, missing.");
            }
            return CurrentChunkIndex;
        }
    }

    public class Topology : AcisEntity
    {
        protected Topology(string subtypeName = "topology-entity") : base(subtypeName) { }
        public override int Set(AcisRecord record)
        {
            base.Set(record);
            var header = AcisGlobalUtils.GetReader()?.Header;
            if (header == null) return CurrentChunkIndex;
            bool isAsm = header.Format?.StartsWith("ASM") ?? false;
            if (header.Version > 10.0 && !isAsm)
            {
                if (Record.Chunks.Count > CurrentChunkIndex) CurrentChunkIndex++;
                else Logger.Warning($"Topology.Set: Expected chunk skip for ver>10 non-ASM for {Record.Name} #{Record.Index}, missing.");
            }
            if (header.Version > 6.0)
            {
                if (Record.Chunks.Count > CurrentChunkIndex) CurrentChunkIndex++;
                else Logger.Warning($"Topology.Set: Expected chunk skip for ver>6 for {Record.Name} #{Record.Index}, missing.");
            }
            return CurrentChunkIndex;
        }
    }

    public class Point : Geometry
    {
        public Vector3 Position { get; set; }
        public Point() : base("point-entity") { }
        public override int Set(AcisRecord record)
        {
            base.Set(record);
            var header = AcisGlobalUtils.GetReader()?.Header;
            if (header == null) { Logger.Error("Point.Set: ACIS Header not available."); return CurrentChunkIndex; }
            Position = AcisParsingUtils.GetLocation(record, ref CurrentChunkIndex, header, "Position");
            return CurrentChunkIndex;
        }
    }

    public class Transform : AcisEntity
    {
        public Matrix4x4 Matrix { get; set; }
        public bool HasRotation { get; set; }
        public bool HasReflection { get; set; }
        public bool HasShear { get; set; }
        public Transform() : base("transform-entity") { Matrix = Matrix4x4.Identity; }
        public override int Set(AcisRecord record)
        {
            base.Set(record);
            var header = AcisGlobalUtils.GetReader()?.Header;
            if (header == null) { Logger.Error("Transform.Set: ACIS Header not available."); return CurrentChunkIndex; }
            double[] m = new double[13];
            for(int i=0; i<13; i++) m[i] = AcisParsingUtils.GetFloat(record, ref CurrentChunkIndex, $"MatrixVal{i}");
            float scale = (float)m[0];
            Matrix = new Matrix4x4(
                (float)m[1]*scale, (float)m[2]*scale, (float)m[3]*scale, 0,
                (float)m[4]*scale, (float)m[5]*scale, (float)m[6]*scale, 0,
                (float)m[7]*scale, (float)m[8]*scale, (float)m[9]*scale, 0,
                (float)m[10],      (float)m[11],      (float)m[12],     1
            );
            HasRotation = AcisParsingUtils.GetEnumByTag<RotationFlagEnum>(record, ref CurrentChunkIndex, "HasRotation") == RotationFlagEnum.ROTATE;
            HasReflection = AcisParsingUtils.GetEnumByTag<ReflectionFlagEnum>(record, ref CurrentChunkIndex, "HasReflection") == ReflectionFlagEnum.REFLECT;
            HasShear = AcisParsingUtils.GetEnumByTag<ShearFlagEnum>(record, ref CurrentChunkIndex, "HasShear") == ShearFlagEnum.SHEAR;
            return CurrentChunkIndex;
        }
    }

    public abstract class Curve : Geometry
    {
        protected Curve(string subtypeName) : base(subtypeName) { }
        public override int Set(AcisRecord record) { base.Set(record); return CurrentChunkIndex; }
        public virtual object Build(Vector3? start, Vector3? end) { throw new NotImplementedException($"Build not implemented for {TypeName}"); }
    }

    public class CurveStraight : Curve
    {
        public Vector3 Root { get; set; }
        public Vector3 Dir { get; set; }
        public Interval CurveRange { get; set; }
        public CurveStraight() : base("straight-curve") { }
        public override int Set(AcisRecord record)
        {
            base.Set(record);
            var header = AcisGlobalUtils.GetReader()?.Header;
            if (header == null) { Logger.Error("CurveStraight.Set: ACIS Header not available."); return CurrentChunkIndex; }
            Root = AcisParsingUtils.GetLocation(record, ref CurrentChunkIndex, header, "Root");
            Dir = AcisParsingUtils.GetVector(record, ref CurrentChunkIndex, "Dir");
            CurveRange = AcisParsingUtils.GetInterval(record, ref CurrentChunkIndex, header, double.NegativeInfinity, double.PositiveInfinity, "CurveRange");
            return CurrentChunkIndex;
        }
    }

    public class CurveEllipse : Curve
    {
        public Vector3 Center { get; set; }
        public Vector3 Axis { get; set; }
        public Vector3 MajorAxisPoint { get; set; }
        public double Ratio { get; set; }
        public Interval CurveRange { get; set; }
        public CurveEllipse() : base("ellipse-curve") { }
        public override int Set(AcisRecord record)
        {
            base.Set(record);
            var header = AcisGlobalUtils.GetReader()?.Header;
            if (header == null) { Logger.Error("CurveEllipse.Set: ACIS Header not available."); return CurrentChunkIndex; }
            Center = AcisParsingUtils.GetLocation(record, ref CurrentChunkIndex, header, "Center");
            Axis = AcisParsingUtils.GetVector(record, ref CurrentChunkIndex, "Axis");
            MajorAxisPoint = AcisParsingUtils.GetLocation(record, ref CurrentChunkIndex, header, "MajorAxisPoint");
            Ratio = AcisParsingUtils.GetFloat(record, ref CurrentChunkIndex, "Ratio");
            CurveRange = AcisParsingUtils.GetInterval(record, ref CurrentChunkIndex, header,
                                                  0.0, 2.0 * Math.PI, "CurveRange");
            return CurrentChunkIndex;
        }
    }

    public abstract class Surface : Geometry
    {
        protected Surface(string subtypeName) : base(subtypeName) { }
        public override int Set(AcisRecord record) { base.Set(record); return CurrentChunkIndex; }
        public virtual object Build(Face face = null) { throw new NotImplementedException($"Build not implemented for {TypeName}"); }
    }

    public class SurfacePlane : Surface
    {
        public Vector3 Root { get; set; }
        public Vector3 Normal { get; set; }
        public Vector3 UvOrigin { get; set; }
        public SenseEnum SenseV { get; set; }
        public Interval URange { get; set; }
        public Interval VRange { get; set; }
        public SurfacePlane() : base("plane-surface") { }
        public override int Set(AcisRecord record)
        {
            base.Set(record);
            var header = AcisGlobalUtils.GetReader()?.Header;
            if (header == null) { Logger.Error("SurfacePlane.Set: ACIS Header not available."); return CurrentChunkIndex; }
            Root = AcisParsingUtils.GetLocation(record, ref CurrentChunkIndex, header, "Root");
            Normal = AcisParsingUtils.GetVector(record, ref CurrentChunkIndex, "Normal");
            UvOrigin = AcisParsingUtils.GetLocation(record, ref CurrentChunkIndex, header, "UvOriginAsPoint");
            // In Python, SurfacePlane uses SENSEV which maps to "forward_v" / "reversed_v".
            // AcisParsingUtils.GetEnumByTag<SenseEnum> might need adjustment if SenseVEnum is different or strings don't match.
            // For now, assuming SenseVEnum is compatible or GetEnumByTag handles it.
            string senseVStr = AcisParsingUtils.GetText(record, ref CurrentChunkIndex, "SenseV_String");
            if (senseVStr.Equals("forward_v", StringComparison.OrdinalIgnoreCase)) SenseV = SenseEnum.FORWARD; // Map to existing SenseEnum
            else if (senseVStr.Equals("reversed_v", StringComparison.OrdinalIgnoreCase)) SenseV = SenseEnum.REVERSED;
            else SenseV = SenseEnum.UNKNOWN;

            URange = AcisParsingUtils.GetInterval(record, ref CurrentChunkIndex, header, double.NegativeInfinity, double.PositiveInfinity, "URange");
            VRange = AcisParsingUtils.GetInterval(record, ref CurrentChunkIndex, header, double.NegativeInfinity, double.PositiveInfinity, "VRange");
            return CurrentChunkIndex;
        }
    }

    public class SurfaceCone : Surface
    {
        public Vector3 Center { get; set; }
        public Vector3 Axis { get; set; }
        public Vector3 RefAxisPoint { get; set; }
        public double Ratio { get; set; }
        public Interval VRangePrimary { get; set; }
        public double SineAngle { get; set; }
        public double CosineAngle { get; set; }
        public double RadiusAtCenter { get; set; }
        public SenseEnum Sense { get; set; }
        public Interval URange { get; set; }
        public Interval VRangeSlant { get; set; }
        public Vector3? Apex { get; private set; }
        public SurfaceCone() : base("cone-surface") { }
        public override int Set(AcisRecord record)
        {
            base.Set(record);
            var header = AcisGlobalUtils.GetReader()?.Header;
            if (header == null) { Logger.Error("SurfaceCone.Set: ACIS Header not available."); return CurrentChunkIndex; }
            Center = AcisParsingUtils.GetLocation(record, ref CurrentChunkIndex, header, "Center");
            Axis = AcisParsingUtils.GetVector(record, ref CurrentChunkIndex, "Axis");
            RefAxisPoint = AcisParsingUtils.GetLocation(record, ref CurrentChunkIndex, header, "RefAxisPoint");
            Ratio = AcisParsingUtils.GetFloat(record, ref CurrentChunkIndex, "Ratio");
            VRangePrimary = AcisParsingUtils.GetInterval(record, ref CurrentChunkIndex, header,
                                                 double.NegativeInfinity, double.PositiveInfinity, "VRangePrimary");
            SineAngle = AcisParsingUtils.GetFloat(record, ref CurrentChunkIndex, "SineAngle");
            CosineAngle = AcisParsingUtils.GetFloat(record, ref CurrentChunkIndex, "CosineAngle");
            RadiusAtCenter = AcisParsingUtils.GetLength(record, ref CurrentChunkIndex, header, "RadiusAtCenter");
            Sense = AcisParsingUtils.GetEnumByTag<SenseEnum>(record, ref CurrentChunkIndex, "Sense");
            URange = AcisParsingUtils.GetInterval(record, ref CurrentChunkIndex, header,
                                               0.0, 2.0 * Math.PI, "URange");
            VRangeSlant = AcisParsingUtils.GetInterval(record, ref CurrentChunkIndex, header,
                                                 double.NegativeInfinity, double.PositiveInfinity, "VRangeSlant");
            CalculateApex();
            return CurrentChunkIndex;
        }
        private void CalculateApex()
        {
            if (Math.Abs(SineAngle) < 1e-9) Apex = null;
            else
            {
                Vector3 normAxis = Vector3.Normalize(Axis);
                double tanHalfAngle = SineAngle / CosineAngle;
                if (Math.Abs(tanHalfAngle) < 1e-9) Apex = null;
                else Apex = Center - normAxis * (float)(RadiusAtCenter / tanHalfAngle);
            }
        }
    }

    public class SurfaceSphere : Surface
    {
        public Vector3 Center { get; set; }
        public double Radius { get; set; }
        public Vector3 UvOrigin { get; set; }
        public Vector3 Pole { get; set; }
        public SenseVEnum SenseV { get; set; }
        public Interval URange { get; set; }
        public Interval VRange { get; set; }

        public SurfaceSphere() : base("sphere-surface") { }
        public override int Set(AcisRecord record)
        {
            base.Set(record);
            var header = AcisGlobalUtils.GetReader()?.Header;
            if (header == null) { Logger.Error("SurfaceSphere.Set: ACIS Header not available."); return CurrentChunkIndex; }

            Center = AcisParsingUtils.GetLocation(record, ref CurrentChunkIndex, header, "Center");
            Radius = AcisParsingUtils.GetLength(record, ref CurrentChunkIndex, header, "Radius");
            UvOrigin = AcisParsingUtils.GetVector(record, ref CurrentChunkIndex, "UvOrigin_RefAxis");
            Pole = AcisParsingUtils.GetVector(record, ref CurrentChunkIndex, "Pole_Axis");
            SenseV = AcisParsingUtils.GetEnumByTag<SenseVEnum>(record, ref CurrentChunkIndex, "SenseV");
            URange = AcisParsingUtils.GetInterval(record, ref CurrentChunkIndex, header, 0.0, 2.0 * Math.PI, "URange");
            VRange = AcisParsingUtils.GetInterval(record, ref CurrentChunkIndex, header, -Math.PI / 2.0, Math.PI / 2.0, "VRange");
            return CurrentChunkIndex;
        }
    }

    public class SurfaceTorus : Surface
    {
        public Vector3 Center { get; set; }
        public Vector3 Axis { get; set; }
        public double MajorRadius { get; set; }
        public double MinorRadius { get; set; }
        public Vector3 UvOriginPoint { get; set; }
        public SenseVEnum SenseV { get; set; }
        public Interval URange { get; set; }
        public Interval VRange { get; set; }

        public SurfaceTorus() : base("torus-surface") { }
        public override int Set(AcisRecord record)
        {
            base.Set(record);
            var header = AcisGlobalUtils.GetReader()?.Header;
            if (header == null) { Logger.Error("SurfaceTorus.Set: ACIS Header not available."); return CurrentChunkIndex; }

            Center = AcisParsingUtils.GetLocation(record, ref CurrentChunkIndex, header, "Center");
            Axis = AcisParsingUtils.GetVector(record, ref CurrentChunkIndex, "Axis");
            MajorRadius = AcisParsingUtils.GetLength(record, ref CurrentChunkIndex, header, "MajorRadius");
            MinorRadius = AcisParsingUtils.GetLength(record, ref CurrentChunkIndex, header, "MinorRadius");
            UvOriginPoint = AcisParsingUtils.GetLocation(record, ref CurrentChunkIndex, header, "UvOriginPoint");
            SenseV = AcisParsingUtils.GetEnumByTag<SenseVEnum>(record, ref CurrentChunkIndex, "SenseV");
            URange = AcisParsingUtils.GetInterval(record, ref CurrentChunkIndex, header, 0.0, 2.0 * Math.PI, "URange");
            VRange = AcisParsingUtils.GetInterval(record, ref CurrentChunkIndex, header, 0.0, 2.0 * Math.PI, "VRange");
            return CurrentChunkIndex;
        }
    }

    public class Vertex : Topology
    {
        public Edge OwnerEdge { get; set; }
        public Point PointEntity { get; set; }
        public Vertex() : base("vertex-entity") { }
        public override int Set(AcisRecord record)
        {
            base.Set(record);
            var header = AcisGlobalUtils.GetReader()?.Header;
            if (header == null) { Logger.Error("Vertex.Set: ACIS Header not available."); return CurrentChunkIndex; }
            OwnerEdge = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "OwnerEdge", "edge-entity") as Edge;
            bool isAsm = header.Format?.StartsWith("ASM") ?? false;
            int asmMajor = 0;
            if (isAsm && asmMajor > 217)
            {
                 if (Record.Chunks.Count > CurrentChunkIndex) CurrentChunkIndex++;
                 else Logger.Warning($"Vertex.Set: Expected chunk skip for ASM > 217 for {Record.Name} #{Record.Index}, missing.");
            }
            if (Record.Chunks.Count > CurrentChunkIndex && !(Record.Chunks[CurrentChunkIndex] is AcisChunkEntityRef))
            {
                 Logger.Info($"Vertex.Set: Skipping potential 'count' chunk for {Record.Name} #{Record.Index}. Chunk type: {Record.Chunks[CurrentChunkIndex].GetType().Name}");
                 CurrentChunkIndex++;
            }
            PointEntity = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "PointEntity", "point-entity") as Point;
            return CurrentChunkIndex;
        }
    }

    public class Edge : Topology
    {
        public Vertex StartVertex { get; set; }
        public Vertex EndVertex { get; set; }
        public CoEdge Coedge { get; set; }
        public Curve CurveEntity { get; set; }
        public double Parameter1 { get; set; }
        public double Parameter2 { get; set; }
        public SenseEnum Sense { get; set; }
        public string Text { get; set; }
        public object UnknownFT { get; set; }
        public Edge() : base("edge-entity") { }
        public override int Set(AcisRecord record)
        {
            base.Set(record);
            var header = AcisGlobalUtils.GetReader()?.Header;
            if (header == null) { Logger.Error("Edge.Set: ACIS Header not available."); return CurrentChunkIndex; }
            StartVertex = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "StartVertex", "vertex-entity") as Vertex;
            if (header.Version > 4.0) Parameter1 = AcisParsingUtils.GetFloat(record, ref CurrentChunkIndex, "Parameter1");
            else Parameter1 = 0.0;
            EndVertex = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "EndVertex", "vertex-entity") as Vertex;
            if (header.Version > 4.0) Parameter2 = AcisParsingUtils.GetFloat(record, ref CurrentChunkIndex, "Parameter2");
            else Parameter2 = 1.0;
            Coedge = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "Coedge", "coedge-entity") as CoEdge;
            CurveEntity = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "CurveEntity", "curve-entity") as Curve;
            Sense = AcisParsingUtils.GetEnumByTag<SenseEnum>(record, ref CurrentChunkIndex, "Sense");
            if (header.Version > 5.0) Text = AcisParsingUtils.GetText(record, ref CurrentChunkIndex, "Text");
            else Text = string.Empty;
            UnknownFT = AcisParsingUtils.GetUnknownFTPlaceholder(record, ref CurrentChunkIndex, "UnknownFT_Edge");
            return CurrentChunkIndex;
        }
    }

    public class CoEdge : Topology
    {
        public CoEdge NextCoedge { get; set; }
        public CoEdge PreviousCoedge { get; set; }
        public CoEdge PartnerCoedge { get; set; }
        public Edge EdgeEntity { get; set; }
        public SenseEnum Sense { get; set; }
        public AcisEntity OwnerLoopOrWire { get; set; }
        public Curve PCurveEntity { get; set; }
        public CoEdge() : base("coedge-entity") { }
        public override int Set(AcisRecord record)
        {
            base.Set(record);
            var header = AcisGlobalUtils.GetReader()?.Header;
            if (header == null) { Logger.Error("CoEdge.Set: ACIS Header not available."); return CurrentChunkIndex; }
            NextCoedge = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "NextCoedge", "coedge-entity") as CoEdge;
            PreviousCoedge = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "PreviousCoedge", "coedge-entity") as CoEdge;
            PartnerCoedge = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "PartnerCoedge", "coedge-entity") as CoEdge;
            EdgeEntity = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "EdgeEntity", "edge-entity") as Edge;
            Sense = AcisParsingUtils.GetEnumByTag<SenseEnum>(record, ref CurrentChunkIndex, "Sense");
            OwnerLoopOrWire = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "OwnerLoopOrWire");
            bool isAsm = header.Format?.StartsWith("ASM") ?? false;
            int asmMajor = 0;
            if (isAsm && asmMajor > 217)
            {
                if (Record.Chunks.Count > CurrentChunkIndex) CurrentChunkIndex++;
                else Logger.Warning($"CoEdge.Set: Expected chunk skip for ASM > 217 for {Record.Name} #{Record.Index}, missing.");
            }
            PCurveEntity = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "PCurveEntity", "pcurve-entity") as Curve;
            return CurrentChunkIndex;
        }
    }

    public class Loop : Topology
    {
        public Loop NextLoop { get; set; }
        public CoEdge CoedgeEntity { get; set; }
        public Face OwnerFace { get; set; }
        public object UnknownFT { get; set; }
        public Loop() : base("loop-entity") { }
        public override int Set(AcisRecord record)
        {
            base.Set(record);
            var header = AcisGlobalUtils.GetReader()?.Header;
            if (header == null) { Logger.Error("Loop.Set: ACIS Header not available."); return CurrentChunkIndex; }
            NextLoop = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "NextLoop", "loop-entity") as Loop;
            CoedgeEntity = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "CoedgeEntity", "coedge-entity") as CoEdge;
            OwnerFace = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "OwnerFace", "face-entity") as Face;
            UnknownFT = AcisParsingUtils.GetUnknownFTPlaceholder(record, ref CurrentChunkIndex, "UnknownFT_Loop");
            bool isAsm = header.Format?.StartsWith("ASM") ?? false;
            if (header.Version > 10.0 && !isAsm)
            {
                if (Record.Chunks.Count > CurrentChunkIndex) CurrentChunkIndex++;
                 else Logger.Warning($"Loop.Set: Expected chunk skip for ver>10 non-ASM for {Record.Name} #{Record.Index}, missing.");
            }
            return CurrentChunkIndex;
        }
    }

    public class Face : Topology
    {
        public Face NextFace { get; set; }
        public Loop LoopEntity { get; set; }
        public AcisEntity ParentShell { get; set; }
        public AcisEntity UnknownRef { get; set; }
        public Surface SurfaceGeom { get; set; }
        public SenseEnum Sense { get; set; } = SenseEnum.FORWARD;
        public SidesEnum Sides { get; set; } = SidesEnum.SINGLE;
        public FaceSideEnum? ContainmentSide { get; set; }
        public object UnknownFT2 { get; set; }
        public Face() : base("face-entity") { }
        public override int Set(AcisRecord record)
        {
            base.Set(record);
            var header = AcisGlobalUtils.GetReader()?.Header;
            if (header == null) { Logger.Error("Face.Set: ACIS Header not available."); return CurrentChunkIndex; }
            NextFace = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "NextFace", "face-entity") as Face;
            LoopEntity = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "LoopEntity", "loop-entity") as Loop;
            ParentShell = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "ParentShell");
            UnknownRef = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "UnknownRefFace");
            SurfaceGeom = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "SurfaceGeometry", "surface-entity") as Surface;
            Sense = AcisParsingUtils.GetEnumByTag<SenseEnum>(record, ref CurrentChunkIndex, "Sense");
            Sides = AcisParsingUtils.GetSides(record, ref CurrentChunkIndex, out FaceSideEnum? readContainmentSide, "SidesInfo");
            ContainmentSide = readContainmentSide;
            bool isAsm = header.Format?.StartsWith("ASM") ?? false;
            if (header.Version > 9.0 && !isAsm )
            {
                UnknownFT2 = AcisParsingUtils.GetUnknownFTPlaceholder(record, ref CurrentChunkIndex, "UnknownFT2_Face");
            }
            return CurrentChunkIndex;
        }
        public object Build() { if (ReadyToBuild) { ReadyToBuild = false; /* Defer */ } return Shape; }
    }

    public class Shell : Topology
    {
        public Shell NextShell { get; set; }
        public AcisEntity SubshellEntity { get; set; }
        public Face FaceEntity { get; set; }
        public Wire WireEntity { get; set; }
        public Lump OwnerLump { get; set; }
        public Shell() : base("shell-entity") { }
        public override int Set(AcisRecord record)
        {
            base.Set(record);
            var header = AcisGlobalUtils.GetReader()?.Header;
            if (header == null) { Logger.Error("Shell.Set: ACIS Header not available."); return CurrentChunkIndex; }
            NextShell = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "NextShell", "shell-entity") as Shell;
            SubshellEntity = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "SubshellEntity");
            FaceEntity = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "FaceEntity", "face-entity") as Face;
            if (header.Version > 1.7)
            {
                WireEntity = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "WireEntity", "wire-entity") as Wire;
            }
            OwnerLump = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "OwnerLump", "lump-entity") as Lump;
            return CurrentChunkIndex;
        }
    }

    public class Lump : Topology
    {
        public Lump NextLump { get; set; }
        public Shell ShellEntity { get; set; }
        public Body OwnerBody { get; set; }
        public object UnknownFT { get; set; }
        public Lump() : base("lump-entity") { }
        public override int Set(AcisRecord record)
        {
            base.Set(record);
            NextLump = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "NextLump", "lump-entity") as Lump;
            ShellEntity = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "ShellEntity", "shell-entity") as Shell;
            OwnerBody = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "OwnerBody", "body-entity") as Body;
            UnknownFT = AcisParsingUtils.GetUnknownFTPlaceholder(record, ref CurrentChunkIndex, "UnknownFT_Lump");
            return CurrentChunkIndex;
        }
    }

    public class Body : Topology
    {
        public Lump LumpEntity { get; set; }
        public Wire WireEntity { get; set; }
        public Transform TransformEntity { get; set; }
        public object UnknownFT { get; set; }
        public Body() : base("body-entity") { }
        public override int Set(AcisRecord record)
        {
            base.Set(record);
            var header = AcisGlobalUtils.GetReader()?.Header;
            if (header == null) { Logger.Error("Body.Set: ACIS Header not available."); return CurrentChunkIndex; }
            bool isAsm = header.Format?.StartsWith("ASM") ?? false;
            if (header.Version > 27.0 && !isAsm )
            {
                 if (Record.Chunks.Count > CurrentChunkIndex) CurrentChunkIndex++;
                 else Logger.Warning($"Body.Set: Expected chunk skip for ver>27 non-ASM for {Record.Name} #{Record.Index}, missing.");
            }
            LumpEntity = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "LumpEntity", "lump-entity") as Lump;
            WireEntity = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "WireEntity", "wire-entity") as Wire;
            TransformEntity = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "TransformEntity", "transform-entity") as Transform;
            UnknownFT = AcisParsingUtils.GetUnknownFTPlaceholder(record, ref CurrentChunkIndex, "UnknownFT_Body");
            return CurrentChunkIndex;
        }
    }

    public class Attributes : AcisEntity
    {
        public Attributes Next { get; set; }
        public Attributes Previous { get; set; }
        public AcisEntity Owner { get; set; }
        public Attributes() : base("attribute-entity"){}

        public override int Set(AcisRecord record)
        {
            base.Set(record);
            Next = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "NextAttribute") as Attributes;
            Previous = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "PreviousAttribute") as Attributes;
            Owner = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "OwnerAttribute");
            var header = AcisGlobalUtils.GetReader()?.Header;
            if (header != null) {
                bool isAsm = header.Format?.StartsWith("ASM") ?? false;
                 if (header.Version > 15.0 && !isAsm) {
                    Logger.Warning($"Attributes.Set: Python code skips 18 (bytes/chunks?) for ver>15 non-ASM for {Record.Name} #{Record.Index}. This skip is not precisely implemented.");
                 }
            }
            return CurrentChunkIndex;
        }
    }

    public class SubShell : Topology { public SubShell() : base("subshell-entity") { } public override int Set(AcisRecord record) { base.Set(record); Logger.Warning($"Set method for {TypeName} not fully implemented."); return CurrentChunkIndex;} }
    public class Wire : Topology { public Wire() : base("wire-entity") { } public override int Set(AcisRecord record) { base.Set(record); Logger.Warning($"Set method for {TypeName} not fully implemented."); return CurrentChunkIndex;} }

    // --- CurveInt and SurfaceSpline with Set methods and subtype dispatch ---
    public class BSCurveData // Placeholder
    {
        public bool IsNurbs { get; set; }
        public int Degree { get; set; }
        public string ClosureType { get; set; }
        public int KnotCount { get; set; }
        // Add lists for poles, knots, weights etc. later
    }

    public class CurveInt : Curve
    {
        public SenseEnum Sense { get; set; }
        public string CurveSubtype { get; set; } // "ref", "exact_int_cur", etc.
        public AcisEntity ReferencedCurve { get; set; }
        public BSCurveData SplineDetails { get; set; }

        public CurveInt() : base("intcurve-curve") { }

        public override int Set(AcisRecord record)
        {
            base.Set(record);
            var header = AcisGlobalUtils.GetReader()?.Header;
            if (header == null) { Logger.Error($"{TypeName}.Set: ACIS Header not available."); return CurrentChunkIndex; }

            Sense = AcisParsingUtils.GetEnumByTag<SenseEnum>(record, ref CurrentChunkIndex, "Sense");
            // Subtype parsing in ACIS Text is: "intcurve-curve I I { subtype C C C... }"
            // The actual subtype string ("ref", "exact_int_cur") is the first chunk inside the {}
            // This means we need to check if the next chunk is a SubtypeOpen.
            if (Record.Chunks.Count > CurrentChunkIndex && Record.Chunks[CurrentChunkIndex] is AcisChunkSubtypeOpen)
            {
                CurrentChunkIndex++; // Consume "{"
                CurveSubtype = AcisParsingUtils.GetText(record, ref CurrentChunkIndex, "CurveInt.Subtype");

                switch (CurveSubtype)
                {
                    case "ref":
                        int refId = AcisParsingUtils.GetInteger(record, ref CurrentChunkIndex, "ReferencedCurveID");
                        ReferencedCurve = record.Reader.GetSubtypeEntity(refId); // Uses AcisReader instance from record
                        if (ReferencedCurve != null) record.Reader.AddSubtypeEntity(ReferencedCurve); // Should this be done by CreateEntity?
                        break;
                    case "exact_int_cur":
                        ParseExactIntCurve(record, ref CurrentChunkIndex, header);
                        break;
                    // Add cases for other subtypes like "law_int_cur", "blend_int_cur" etc.
                    default:
                        Logger.Warning($"{TypeName}.Set: Unsupported subtype '{CurveSubtype}' for record {Record.Name} #{Record.Index}.");
                        // Consume remaining chunks within subtype block until "}"
                        while(CurrentChunkIndex < Record.Chunks.Count && !(Record.Chunks[CurrentChunkIndex] is AcisChunkSubtypeClose)) CurrentChunkIndex++;
                        break;
                }
                if (CurrentChunkIndex < Record.Chunks.Count && Record.Chunks[CurrentChunkIndex] is AcisChunkSubtypeClose) CurrentChunkIndex++; // Consume "}"
                else Logger.Warning($"{TypeName}.Set: Missing '}}' for subtype {CurveSubtype}. Record: {Record.Name} #{Record.Index}");
            }
            else
            {
                Logger.Error($"{TypeName}.Set: Expected SubtypeOpen '{{' not found for record {Record.Name} #{Record.Index}.");
            }
            // After the subtype block, there's usually a range
            if (CurrentChunkIndex < Record.Chunks.Count -1) // Ensure there are enough chunks for an interval
            {
                 // CurveRange = AcisParsingUtils.GetInterval(record, ref CurrentChunkIndex, header, double.NegativeInfinity, double.PositiveInfinity, "CurveInt.Range");
                 // The python code has self.range for CurveInt. For now, skipping.
            }
            return CurrentChunkIndex;
        }

        private void ParseExactIntCurve(AcisRecord record, ref int chunkIndex, AcisHeader header)
        {
            Logger.Info($"{TypeName}.ParseExactIntCurve: Parsing 'exact_int_cur' subtype.");
            SplineDetails = new BSCurveData();
            // Example from Python's CurveInt.setExact: reads singularity, then BS3Curve, then tolerance
            // Python's setCurve (called by setExact): singularity, spline_data (BS3Curve), tolerance
            string singularity = AcisParsingUtils.GetText(record, ref chunkIndex, "Singularity"); // Placeholder parsing
            // Further parsing for BS3Curve data (GetDimensionCurve, GetClosureCurve, points, knots, weights)
            // This is complex and deferred beyond basic dimension/closure.
            AcisParsingUtils.GetDimensionCurve(record, ref chunkIndex, out SplineDetails.IsNurbs, out SplineDetails.Degree, "ExactIntCurve.Dimension");
            AcisParsingUtils.GetClosureCurve(record, ref chunkIndex, out SplineDetails.ClosureType, out SplineDetails.KnotCount, "ExactIntCurve.Closure");
            // Skip points, knots, weights for now
            Logger.Warning($"{TypeName}.ParseExactIntCurve: Full BSpline data parsing (points, knots, weights) is deferred for 'exact_int_cur'.");
            // Consume a placeholder number of chunks for the rest of BS3Curve and tolerance
            // This needs to be refined based on actual ACIS structure for BS3Curve.
            // For now, let's assume it might take up many chunks, so we try to find the end of this specific subtype's data
            // or just log and don't advance chunkIndex much further within this helper.
        }
    }

    public class BSSurfaceData // Placeholder
    {
        public bool IsNurbs { get; set; }
        public int UDegree { get; set; }
        public int VDegree { get; set; }
        public string UClosure { get; set; }
        public string VClosure { get; set; }
        public string USingularity { get; set; }
        public string VSingularity { get; set; }
        public int UKnotCount { get; set; }
        public int VKnotCount { get; set; }
        // Add lists for poles, knots, weights etc. later
    }

    public class SurfaceSpline : Surface
    {
        public SenseEnum Sense { get; set; }
        public string SurfaceSubtype { get; set; } // "ref", "spl_sur", etc.
        public AcisEntity ReferencedSurface { get; set; }
        public BSSurfaceData SplineDetails { get; set; }

        public SurfaceSpline() : base("spline-surface") { }

        public override int Set(AcisRecord record)
        {
            base.Set(record);
            var header = AcisGlobalUtils.GetReader()?.Header;
            if (header == null) { Logger.Error($"{TypeName}.Set: ACIS Header not available."); return CurrentChunkIndex; }

            Sense = AcisParsingUtils.GetEnumByTag<SenseEnum>(record, ref CurrentChunkIndex, "Sense");

            if (Record.Chunks.Count > CurrentChunkIndex && Record.Chunks[CurrentChunkIndex] is AcisChunkSubtypeOpen)
            {
                CurrentChunkIndex++; // Consume "{"
                SurfaceSubtype = AcisParsingUtils.GetText(record, ref CurrentChunkIndex, "SurfaceSpline.Subtype");

                switch (SurfaceSubtype)
                {
                    case "ref":
                        int refId = AcisParsingUtils.GetInteger(record, ref CurrentChunkIndex, "ReferencedSurfaceID");
                        ReferencedSurface = record.Reader.GetSubtypeEntity(refId);
                        if (ReferencedSurface != null) record.Reader.AddSubtypeEntity(ReferencedSurface);
                        break;
                    case "spl_sur": // This is one of the common ones from Python SurfaceSpline.setSurfaceShape
                        ParseSplineSurfaceProper(record, ref CurrentChunkIndex, header);
                        break;
                    // Add cases for "cyl_spl_sur", "off_spl_sur", "rot_spl_sur", "sum_spl_sur", etc.
                    default:
                        Logger.Warning($"{TypeName}.Set: Unsupported subtype '{SurfaceSubtype}' for record {Record.Name} #{Record.Index}.");
                        while(CurrentChunkIndex < Record.Chunks.Count && !(Record.Chunks[CurrentChunkIndex] is AcisChunkSubtypeClose)) CurrentChunkIndex++;
                        break;
                }
                if (CurrentChunkIndex < Record.Chunks.Count && Record.Chunks[CurrentChunkIndex] is AcisChunkSubtypeClose) CurrentChunkIndex++;
                else Logger.Warning($"{TypeName}.Set: Missing '}}' for subtype {SurfaceSubtype}. Record: {Record.Name} #{Record.Index}");
            }
            else
            {
                 Logger.Error($"{TypeName}.Set: Expected SubtypeOpen '{{' not found for record {Record.Name} #{Record.Index}.");
            }
            // After the subtype block, there's usually URange and VRange
             if (CurrentChunkIndex < Record.Chunks.Count - 3) // Ensure there are enough for two intervals
            {
                // URange = AcisParsingUtils.GetInterval(record, ref CurrentChunkIndex, header, double.NegativeInfinity, double.PositiveInfinity, "SurfaceSpline.URange");
                // VRange = AcisParsingUtils.GetInterval(record, ref CurrentChunkIndex, header, double.NegativeInfinity, double.PositiveInfinity, "SurfaceSpline.VRange");
                // Skipping for now as Python setSubtype handles this after specific subtype parsing
            }
            return CurrentChunkIndex;
        }

        private void ParseSplineSurfaceProper(AcisRecord record, ref int chunkIndex, AcisHeader header)
        {
            Logger.Info($"{TypeName}.ParseSplineSurfaceProper: Parsing 'spl_sur' subtype.");
            SplineDetails = new BSSurfaceData();
            // Python: self.spline, self.tolerance, i = readSplineSurface(chunks, index, True)
            // readSplineSurface -> singularity, then BS3Surface or other data, then tolerance
            string singularity = AcisParsingUtils.GetText(record, ref chunkIndex, "Singularity"); // Placeholder
            // This is highly complex, involving GetDimensionSurface, GetClosureSurface, and then point/knot/weight lists.
            AcisParsingUtils.GetDimensionSurface(record, ref chunkIndex, out SplineDetails.IsNurbs, out SplineDetails.UDegree, out SplineDetails.VDegree, "SplineSurface.Dimension");
            AcisParsingUtils.GetClosureSurface(record, ref chunkIndex,
                out SplineDetails.UClosure, out SplineDetails.VClosure,
                out SplineDetails.USingularity, out SplineDetails.VSingularity,
                out SplineDetails.UKnotCount, out SplineDetails.VKnotCount, "SplineSurface.Closure");

            Logger.Warning($"{TypeName}.ParseSplineSurfaceProper: Full BSpline surface data parsing (points, knots, weights) is deferred for 'spl_sur'.");
            // Potentially read tolerance if version >= 2.0 after discontinuity info
        }
    }

    public class SurfaceCone : Surface { public SurfaceCone() : base("cone-surface") { /* Set Implemented in previous step */ } }
    public class CurveEllipse : Curve { public CurveEllipse() : base("ellipse-curve") { /* Set Implemented in previous step */ } }

    public class CurveComp : Curve { public CurveComp() : base("compcurv-curve") { } public override int Set(AcisRecord rec){ base.Set(rec); Logger.Warning($"Set for {TypeName} NI"); return CurrentChunkIndex;} }
    public class CurveDegenerate : Curve { public CurveDegenerate() : base("degenerate-curve") { } public override int Set(AcisRecord rec){ base.Set(rec); Logger.Warning($"Set for {TypeName} NI"); return CurrentChunkIndex;} }
    // CurveInt implemented above
    public class CurveIntInt : Curve { public CurveIntInt() : base("intcurve-intcurve-curve") { } public override int Set(AcisRecord rec){ base.Set(rec); Logger.Warning($"Set for {TypeName} NI"); return CurrentChunkIndex;} }
    public class CurveP : Curve { public CurveP() : base("pcurve-curve") { } public override int Set(AcisRecord rec){ base.Set(rec); Logger.Warning($"Set for {TypeName} NI"); return CurrentChunkIndex;} }
    public class SurfaceMesh : Surface { public SurfaceMesh() : base("meshsurf-surface") { } public override int Set(AcisRecord rec){ base.Set(rec); Logger.Warning($"Set for {TypeName} NI"); return CurrentChunkIndex;} }
    public class SurfaceSphere : Surface { public SurfaceSphere() : base("sphere-surface") { /* Set Implemented in previous step */ } }
    // SurfaceSpline implemented above
    public class SurfaceTorus : Surface { public SurfaceTorus() : base("torus-surface") { /* Set Implemented in previous step */ } }

    public class Attrib : Attributes { public Attrib() : base("attrib-attrib") { } }
    public class AttribADesk : Attributes { public AttribADesk() : base("adesk-attrib") { } }
    public class AttribADeskColor : AttribADesk { public AttribADeskColor() : base("color-adesk-attrib") { } }
    public class AttribADeskMaterial : AttribADesk { public AttribADeskMaterial() : base("material-adesk-attrib") { } }
    public class AttribADeskTrueColor : AttribADesk { public AttribADeskTrueColor() : base("truecolor-adesk-attrib") { } }
    public class AttribGen : Attributes { public AttribGen() : base("gen-attrib") { } }
    public class AttribGenName : AttribGen { public AttribGenName() : base("name_attrib-gen-attrib") { } }

    public class UnknownAcisEntity : AcisEntity
    {
        public UnknownAcisEntity() : base("unknown-entity") { }
        public override int Set(AcisRecord record)
        {
            base.Set(record);
            Logger.Info($"UnknownAcisEntity.Set called for record: {record.Name} #{record.Index}");
            return CurrentChunkIndex;
        }
    }
}
