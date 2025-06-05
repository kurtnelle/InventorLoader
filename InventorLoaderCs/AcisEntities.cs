using System;
using System.Collections.Generic;
using System.Numerics;
using System.IO;
using System.Linq; // Needed for Sum()

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
        public int IndexInSubtypeList { get; set; } = -1;


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
        public CurveEllipse() : base("ellipse-curve") { /* Set Implemented */ }
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
            string senseVStr = AcisParsingUtils.GetText(record, ref CurrentChunkIndex, "SenseV_String");
            if (senseVStr.Equals("forward_v", StringComparison.OrdinalIgnoreCase)) SenseV = SenseEnum.FORWARD;
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
        public SurfaceCone() : base("cone-surface") { /* Set Implemented */ }
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

        public SurfaceSphere() : base("sphere-surface") { /* Set Implemented */ }
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

        public SurfaceTorus() : base("torus-surface") { /* Set Implemented */ }
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
    // BSCurveData and BSSurfaceData are now in ImporterClasses.cs

    public class CurveInt : Curve
    {
        public SenseEnum Sense { get; set; }
        public string CurveSubtypeString { get; set; } // Renamed from Subtype to avoid confusion with AcisEntity.SubtypeName
        public AcisEntity ReferencedCurve { get; set; }
        public BSCurveData SplineGeometricData { get; set; }
        public Law LawCurveData { get; set; }
        public Helix HelixCurveData { get; set; }

        // Properties for "offset" subtype (off_int_cur / offset_int_cur)
        public Curve BaseCurveForOffset { get; set; }
        public double StartParamOffset { get; set; }
        public double EndParamOffset { get; set; }
        public Vector3 OffsetVector { get; set; }
        public string OTxt1 { get; set; } // Placeholder for 'oTxt1'
        public int OI { get; set; }       // Placeholder for 'oI'
        public string OTxt2 { get; set; } // Placeholder for 'oTxt2'
        public int OJ { get; set; }       // Placeholder for 'oJ'

        // Properties for "projected" subtype (proj_int_cur)
        public Curve CurveToProject { get; set; }
        public Surface ProjectionTargetSurface { get; set; }
        public Interval ProjectionRange { get; set; } // Assuming Interval is suitable
        public bool ProjectionDirectionFlag { get; set; } // For the optional boolean
        public Vector3 ProjectionDirection { get; set; } // For the optional vector if boolean is true

        public CurveInt() : base("intcurve-curve") { }

        public override int Set(AcisRecord record)
        {
            base.Set(record); // Sets CurrentChunkIndex via AcisEntity.Set
            var header = AcisGlobalUtils.GetReader()?.Header;
            if (header == null) { Logger.Error($"{TypeName}.Set: ACIS Header not available."); return CurrentChunkIndex; }

            Sense = AcisParsingUtils.GetEnumByTag<SenseEnum>(record, ref CurrentChunkIndex, "Sense");

            if (Record.Chunks.Count > CurrentChunkIndex && Record.Chunks[CurrentChunkIndex] is AcisChunkSubtypeOpen)
            {
                CurrentChunkIndex++;
                CurveSubtypeString = AcisParsingUtils.GetText(record, ref CurrentChunkIndex, "CurveInt.Subtype");
                Logger.Info($"{TypeName}.Set: subtype is {CurveSubtypeString} for record {Record.Name} #{Record.Index}");

                switch (CurveSubtypeString)
                {
                    case "ref":
                        int refId = AcisParsingUtils.GetInteger(record, ref CurrentChunkIndex, "ReferencedCurveID");
                        ReferencedCurve = record.Reader.GetSubtypeEntity(refId);
                        break;
                    case "exact_int_cur":
                        ParseExactIntCurve(record, ref CurrentChunkIndex, header);
                        break;
                    case "law_int_cur":
                        ParseLawIntCurve(record, ref CurrentChunkIndex, header);
                        break;
                    case "helix_int_cur":
                        ParseHelixIntCurve(record, ref CurrentChunkIndex, header);
                        break;
                    case "off_int_cur":
                    case "offset_int_cur":
                        ParseOffsetIntCurve(record, ref CurrentChunkIndex, header);
                        break;
                    case "proj_int_cur":
                        ParseProjectedIntCurve(record, ref CurrentChunkIndex, header);
                        break;
                    case "comp_int_cur":
                    case "defm_int_cur":
                    case "spring_int_cur":
                    // case "off_int_cur": // Handled above
                    // case "offset_int_cur": // Handled above
                    case "off_surf_int_cur":
                    case "para_silh_int_cur":
                    case "par_int_cur":
                    // case "proj_int_cur": // Handled above
                    case "surf_int_cur":
                    case "int_int_cur":
                    case "taper_silh_int_cur":
                        Logger.Warning($"{TypeName}.Set: Parsing for subtype '{CurveSubtypeString}' is complex and deferred. Consuming remaining chunks in block.");
                        while(CurrentChunkIndex < Record.Chunks.Count && !(Record.Chunks[CurrentChunkIndex] is AcisChunkSubtypeClose)) CurrentChunkIndex++;
                        break;
                    default:
                        Logger.Warning($"{TypeName}.Set: Unsupported subtype '{CurveSubtypeString}' for record {Record.Name} #{Record.Index}. Consuming remaining chunks in block.");
                        while(CurrentChunkIndex < Record.Chunks.Count && !(Record.Chunks[CurrentChunkIndex] is AcisChunkSubtypeClose)) CurrentChunkIndex++;
                        break;
                }
                if (CurrentChunkIndex < Record.Chunks.Count && Record.Chunks[CurrentChunkIndex] is AcisChunkSubtypeClose) CurrentChunkIndex++;
                else Logger.Warning($"{TypeName}.Set: Missing '}}' for subtype {CurveSubtypeString}. Record: {Record.Name} #{Record.Index}");
            }
            else
            {
                 Logger.Error($"{TypeName}.Set: Expected SubtypeOpen '{{' not found for record {Record.Name} #{Record.Index}. Chunk: {(Record.Chunks.Count > CurrentChunkIndex ? Record.Chunks[CurrentChunkIndex]?.Val.ToString() : "EOF")}");
            }

            if (Record.Chunks.Count > CurrentChunkIndex + 1)
            {
                // ParsedContent["CurveIntMainRange"] = AcisParsingUtils.GetInterval(record, ref CurrentChunkIndex, header, double.NegativeInfinity, double.PositiveInfinity, "CurveIntMainRange");
            }
            return CurrentChunkIndex;
        }

        private void ParseExactIntCurve(AcisRecord record, ref int chunkIndex, AcisHeader header)
        {
            Logger.Info($"{TypeName}.ParseExactIntCurve: Parsing 'exact_int_cur' subtype for record {Record.Name} #{Record.Index}");
            SplineGeometricData = new BSCurveData();

            string singularity = AcisParsingUtils.GetText(record, ref chunkIndex, "Singularity");

            if (singularity.Equals("full", StringComparison.OrdinalIgnoreCase))
            {
                AcisParsingUtils.GetDimensionCurve(record, ref chunkIndex, out bool isNurbsTemp, out int degreeTemp, "ExactIntCurve.Dimension");
                SplineGeometricData.IsRational = isNurbsTemp;
                SplineGeometricData.Degree = degreeTemp;

                AcisParsingUtils.GetClosureCurve(record, ref chunkIndex, out string closureTypeTemp, out int knotCountFromHeaderTemp, "ExactIntCurve.Closure");
                SplineGeometricData.IsPeriodic = closureTypeTemp.Equals("periodic", StringComparison.OrdinalIgnoreCase);
                // BSCurveData.KnotCountFromHeader = knotCountFromHeaderTemp; // Not directly on BSCurveData

                if (!AcisParsingUtils.ReadKnotsAndMults(record, ref chunkIndex, knotCountFromHeaderTemp,
                                                 out SplineGeometricData.Knots, out SplineGeometricData.Multiplicities, "ExactIntCurve.UKnots")) return;

                if (SplineGeometricData.Multiplicities.Any())
                {
                     int expectedPoleCount = SplineGeometricData.Multiplicities.Sum() - SplineGeometricData.Degree - 1;
                     if (SplineGeometricData.IsPeriodic) {
                         expectedPoleCount = SplineGeometricData.Multiplicities.Take(SplineGeometricData.Multiplicities.Count-1).Sum();
                     }
                     AcisParsingUtils.AdjustKnotsAndMults(SplineGeometricData.Knots, SplineGeometricData.Multiplicities, SplineGeometricData.Degree);
                     AcisParsingUtils.ReadPoints3DList(record, ref chunkIndex, header, SplineGeometricData, expectedPoleCount, "ExactIntCurve.Poles");
                } else {
                    Logger.Warning($"{TypeName}.ParseExactIntCurve: Multiplicities not populated, cannot calculate pole count for {Record.Name} #{Record.Index}");
                }
                if (record.Chunks.Count > chunkIndex)
                {
                    double tolerance = AcisParsingUtils.GetLength(record, ref chunkIndex, header, "ExactIntCurve.Tolerance");
                }
            }
            else if (singularity.Equals("v", StringComparison.OrdinalIgnoreCase))
            {
                SplineGeometricData.IsRational = false;
                SplineGeometricData.IsPeriodic = false;
                SplineGeometricData.Degree = 3;
                Logger.Warning($"{TypeName}.ParseExactIntCurve: Singularity 'v' with direct knot array reading not yet fully implemented for {Record.Name} #{Record.Index}.");
            }
            else Logger.Warning($"{TypeName}.ParseExactIntCurve: Singularity '{singularity}' not fully handled for {Record.Name} #{Record.Index}.");
        }

        private void ParseLawIntCurve(AcisRecord record, ref int chunkIndex, AcisHeader header)
        {
            Logger.Info($"{TypeName}.ParseLawIntCurve: Parsing 'law_int_cur' subtype for record {Record.Name} #{Record.Index}");
            // Read the primary formula structure (main law string/type and its initial set of variable laws)
            LawCurveData = AcisParsingUtils.ReadFormulaStructure(record, ref chunkIndex, header, "LawCurveData.FormulaStructure");

            // After the formula-structure block, ACIS files for law_int_cur can have an additional list of laws.
            // Python: self.vars += readLawList(chunks, i) -> readLawList reads count, then 'count' laws.
            if (LawCurveData != null)
            {
                // Check if there are more chunks available for the count of additional laws
                if (record.Chunks.Count > chunkIndex)
                {
                    int additionalLawCount = AcisParsingUtils.GetInteger(record, ref chunkIndex, "LawCurveData.AdditionalLawCount");
                    for (int i = 0; i < additionalLawCount; i++)
                    {
                        Law additionalLaw = AcisParsingUtils.ReadLaw(record, ref chunkIndex, header, $"LawCurveData.AdditionalLaw{i}");
                        if (additionalLaw != null)
                        {
                            LawCurveData.Parameters.Add(additionalLaw);
                        }
                        else
                        {
                            Logger.Error($"{TypeName}.ParseLawIntCurve: Failed to parse additional law {i} for record {Record.Name} #{Record.Index}");
                            break;
                        }
                    }
                }
                else
                {
                    Logger.Info($"{TypeName}.ParseLawIntCurve: No more chunks after FormulaStructure for additional laws. Record: {Record.Name} #{Record.Index}");
                }
            }
            else
            {
                Logger.Error($"{TypeName}.ParseLawIntCurve: Failed to parse main FormulaStructure. Record: {Record.Name} #{Record.Index}");
            }
        }

        private void ParseHelixIntCurve(AcisRecord record, ref int chunkIndex, AcisHeader header)
        {
            Logger.Info($"{TypeName}.ParseHelixIntCurve: Parsing 'helix_int_cur' subtype for record {Record.Name} #{Record.Index}");
            HelixCurveData = AcisParsingUtils.ReadHelixData(record, ref chunkIndex, header, "HelixCurveData");

            HelixCurveData.ProjectionSurface1 = AcisParsingUtils.GetRefNode(record, ref chunkIndex, "Helix.ProjectionSurface1", "surface-entity") as Surface;

            string pcurve1DimType = AcisParsingUtils.GetText(record, ref chunkIndex, "Helix.PCurve1DimTypeCheck");
            if (pcurve1DimType != null && !pcurve1DimType.Equals("nullbs", StringComparison.OrdinalIgnoreCase))
            {
                chunkIndex--;
                HelixCurveData.ProjectionPCurve1 = new BSCurveData();
                AcisParsingUtils.GetDimensionCurve(record, ref chunkIndex, out bool isNurbs1, out int degree1, "Helix.PCurve1Dim");
                HelixCurveData.ProjectionPCurve1.IsRational = isNurbs1;
                HelixCurveData.ProjectionPCurve1.Degree = degree1;

                AcisParsingUtils.GetClosureCurve(record, ref chunkIndex, out string closure1, out int knotCount1, "Helix.PCurve1Closure");
                HelixCurveData.ProjectionPCurve1.IsPeriodic = closure1.Equals("periodic", StringComparison.OrdinalIgnoreCase);

                if (!AcisParsingUtils.ReadKnotsAndMults(record, ref chunkIndex, knotCount1,
                                                 out HelixCurveData.ProjectionPCurve1.Knots,
                                                 out HelixCurveData.ProjectionPCurve1.Multiplicities, "Helix.PCurve1Knots"))
                {
                    Logger.Error($"{TypeName}.ParseHelixIntCurve: Failed to read knots/mults for PCurve1. Record: {Record.Name} #{Record.Index}");
                    HelixCurveData.ProjectionPCurve1 = null;
                }
                else
                {
                    if (HelixCurveData.ProjectionPCurve1.Multiplicities.Any())
                    {
                        int expectedPoleCount1 = HelixCurveData.ProjectionPCurve1.Multiplicities.Sum() - HelixCurveData.ProjectionPCurve1.Degree - 1;
                        if (HelixCurveData.ProjectionPCurve1.IsPeriodic)
                        {
                            expectedPoleCount1 = HelixCurveData.ProjectionPCurve1.Multiplicities.Take(HelixCurveData.ProjectionPCurve1.Multiplicities.Count -1).Sum();
                        }

                        AcisParsingUtils.AdjustKnotsAndMults(HelixCurveData.ProjectionPCurve1.Knots, HelixCurveData.ProjectionPCurve1.Multiplicities, HelixCurveData.ProjectionPCurve1.Degree);
                        if (!AcisParsingUtils.ReadPoints2DList(record, ref chunkIndex, header, HelixCurveData.ProjectionPCurve1, expectedPoleCount1, "Helix.PCurve1Poles"))
                        {
                             Logger.Error($"{TypeName}.ParseHelixIntCurve: Failed to read poles for PCurve1. Record: {Record.Name} #{Record.Index}");
                             HelixCurveData.ProjectionPCurve1 = null;
                        }
                    } else {
                        Logger.Warning($"{TypeName}.ParseHelixIntCurve: PCurve1 Multiplicities not populated for {Record.Name} #{Record.Index}");
                        HelixCurveData.ProjectionPCurve1 = null;
                    }
                }
            } else if (pcurve1DimType == null) {
                 Logger.Warning($"{TypeName}.ParseHelixIntCurve: Expected dimension type for PCurve1 (e.g., 'nubs', 'nurbs', 'nullbs'), but found nothing. Record: {Record.Name} #{Record.Index}");
            }

            HelixCurveData.ProjectionSurface2 = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "Helix.ProjectionSurface2", "surface-entity") as Surface;

            string pcurve2DimType = AcisParsingUtils.GetText(record, ref chunkIndex, "Helix.PCurve2DimTypeCheck");
            if (pcurve2DimType != null && !pcurve2DimType.Equals("nullbs", StringComparison.OrdinalIgnoreCase))
            {
                chunkIndex--;
                HelixCurveData.ProjectionPCurve2 = new BSCurveData();
                AcisParsingUtils.GetDimensionCurve(record, ref chunkIndex, out bool isNurbs2, out int degree2, "Helix.PCurve2Dim");
                HelixCurveData.ProjectionPCurve2.IsRational = isNurbs2;
                HelixCurveData.ProjectionPCurve2.Degree = degree2;

                AcisParsingUtils.GetClosureCurve(record, ref chunkIndex, out string closure2, out int knotCount2, "Helix.PCurve2Closure");
                HelixCurveData.ProjectionPCurve2.IsPeriodic = closure2.Equals("periodic", StringComparison.OrdinalIgnoreCase);

                if (!AcisParsingUtils.ReadKnotsAndMults(record, ref chunkIndex, knotCount2,
                                                 out HelixCurveData.ProjectionPCurve2.Knots,
                                                 out HelixCurveData.ProjectionPCurve2.Multiplicities, "Helix.PCurve2Knots"))
                {
                     Logger.Error($"{TypeName}.ParseHelixIntCurve: Failed to read knots/mults for PCurve2. Record: {Record.Name} #{Record.Index}");
                     HelixCurveData.ProjectionPCurve2 = null;
                }
                else
                {
                     if (HelixCurveData.ProjectionPCurve2.Multiplicities.Any())
                    {
                        int expectedPoleCount2 = HelixCurveData.ProjectionPCurve2.Multiplicities.Sum() - HelixCurveData.ProjectionPCurve2.Degree - 1;
                         if (HelixCurveData.ProjectionPCurve2.IsPeriodic)
                        {
                            expectedPoleCount2 = HelixCurveData.ProjectionPCurve2.Multiplicities.Take(HelixCurveData.ProjectionPCurve2.Multiplicities.Count-1).Sum();
                        }

                        AcisParsingUtils.AdjustKnotsAndMults(HelixCurveData.ProjectionPCurve2.Knots, HelixCurveData.ProjectionPCurve2.Multiplicities, HelixCurveData.ProjectionPCurve2.Degree);
                        if(!AcisParsingUtils.ReadPoints2DList(record, ref chunkIndex, header, HelixCurveData.ProjectionPCurve2, expectedPoleCount2, "Helix.PCurve2Poles"))
                        {
                            Logger.Error($"{TypeName}.ParseHelixIntCurve: Failed to read poles for PCurve2. Record: {Record.Name} #{Record.Index}");
                            HelixCurveData.ProjectionPCurve2 = null;
                        }
                    } else {
                        Logger.Warning($"{TypeName}.ParseHelixIntCurve: PCurve2 Multiplicities not populated for {Record.Name} #{Record.Index}");
                        HelixCurveData.ProjectionPCurve2 = null;
                    }
                }
            } else if (pcurve2DimType == null) {
                Logger.Warning($"{TypeName}.ParseHelixIntCurve: Expected dimension type for PCurve2, but found nothing. Record: {Record.Name} #{Record.Index}");
            }
        }

        private void ParseOffsetIntCurve(AcisRecord record, ref int chunkIndex, AcisHeader header)
        {
            Logger.Info($"{TypeName}.ParseOffsetIntCurve: Parsing '{CurveSubtypeString}' subtype for record {Record.Name} #{Record.Index}");

            // Python's setOffset calls setSurfaceCurve first, which reads the B-Spline.
            // So, we parse the B-Spline data of the offset curve itself first.
            ParseExactIntCurve(record, ref chunkIndex, header);

            BaseCurveForOffset = AcisParsingUtils.GetRefNode(record, ref chunkIndex, "BaseCurveForOffset", "curve-entity") as Curve;
            StartParamOffset = AcisParsingUtils.GetFloat(record, ref chunkIndex, "StartParamOffset");
            EndParamOffset = AcisParsingUtils.GetFloat(record, ref chunkIndex, "EndParamOffset");
            OffsetVector = AcisParsingUtils.GetVector(record, ref chunkIndex, "OffsetVector");

            // Additional fields from Python's importerConstants for offset curve
            OTxt1 = AcisParsingUtils.GetText(record, ref chunkIndex, "OTxt1"); // e.g., "parameter_space"
            OI = AcisParsingUtils.GetInteger(record, ref chunkIndex, "OI");     // e.g., 0
            OTxt2 = AcisParsingUtils.GetText(record, ref chunkIndex, "OTxt2"); // e.g., "parameter_space"
            OJ = AcisParsingUtils.GetInteger(record, ref chunkIndex, "OJ");     // e.g., 0

            // Python code also reads 'oTol' (tolerance)
            double oTol = AcisParsingUtils.GetFloat(record, ref chunkIndex, "OffsetTolerance");
            // This tolerance might be stored or used as needed.
        }

        private void ParseProjectedIntCurve(AcisRecord record, ref int chunkIndex, AcisHeader header)
        {
            Logger.Info($"{TypeName}.ParseProjectedIntCurve: Parsing 'proj_int_cur' subtype for record {Record.Name} #{Record.Index}");

            // Python's setProject seems to call setSurfaceCurve first.
            ParseExactIntCurve(record, ref chunkIndex, header);

            CurveToProject = AcisParsingUtils.GetRefNode(record, ref chunkIndex, "CurveToProject", "curve-entity") as Curve;
            ProjectionTargetSurface = AcisParsingUtils.GetRefNode(record, ref chunkIndex, "ProjectionTargetSurface", "surface-entity") as Surface;

            // Handle optional boolean and interval as per Python's setProject
            // Python: projDir = getBool(chunks, i)
            //         if projDir: self.projVec = getVec(chunks, i)
            //         self.projRange = getInterval(chunks, i, header, MIN_INF, MAX_INF)

            // Check if the next chunk is a boolean ident ('T' or 'F') or part of an interval
            // This is a bit tricky without knowing the exact chunk structure.
            // Assuming boolean comes first if present.
            if (record.Chunks.Count > chunkIndex)
            {
                var nextChunkVal = record.Chunks[chunkIndex].Val;
                if (nextChunkVal is string strVal && (strVal.Equals("T", StringComparison.OrdinalIgnoreCase) || strVal.Equals("F", StringComparison.OrdinalIgnoreCase)))
                {
                    ProjectionDirectionFlag = AcisParsingUtils.GetBool(record, ref chunkIndex, "ProjectionDirectionFlag");
                    if (ProjectionDirectionFlag)
                    {
                        ProjectionDirection = AcisParsingUtils.GetVector(record, ref chunkIndex, "ProjectionDirection");
                    }
                }
            }
            // Whether boolean was present or not, an interval should follow.
            ProjectionRange = AcisParsingUtils.GetInterval(record, ref chunkIndex, header, double.NegativeInfinity, double.PositiveInfinity, "ProjectionRange");
        }

    }

    // BSSurfaceData is now in ImporterClasses.cs

    public class SurfaceSpline : Surface
    {
        public SenseEnum Sense { get; set; }
        public string SurfaceSubtype { get; set; }
        public AcisEntity ReferencedSurface { get; set; }
        public BSSurfaceData SplineGeometricData { get; set; }
        public Curve ProfileCurve { get; set; }
        public Vector3 AxisVector { get; set; }
        public Vector3 CenterPoint { get; set; }
        public Vector3 DirectionVector { get; set; }

        // Properties for "offset" subtype (off_spl_sur)
        public Surface BaseSurfaceForOffset { get; set; }
        public double OffsetDistance { get; set; }
        public SenseEnum USenseOffset { get; set; } // Or specific enum if ACIS has one for this
        public SenseEnum VSenseOffset { get; set; } // Or specific enum

        // Properties for "ruled" subtype (rule_sur)
        public Curve ProfileCurve1 { get; set; }
        public Curve ProfileCurve2 { get; set; }

        public SurfaceSpline() : base("spline-surface") { }

        public override int Set(AcisRecord record)
        {
            base.Set(record);
            var header = AcisGlobalUtils.GetReader()?.Header;
            if (header == null) { Logger.Error($"{TypeName}.Set: ACIS Header not available."); return CurrentChunkIndex; }

            Sense = AcisParsingUtils.GetEnumByTag<SenseEnum>(record, ref CurrentChunkIndex, "Sense");

            if (Record.Chunks.Count > CurrentChunkIndex && Record.Chunks[CurrentChunkIndex] is AcisChunkSubtypeOpen)
            {
                CurrentChunkIndex++;
                SurfaceSubtype = AcisParsingUtils.GetText(record, ref CurrentChunkIndex, "SurfaceSpline.Subtype");
                Logger.Info($"{TypeName}.Set: subtype is {SurfaceSubtype} for record {Record.Name} #{Record.Index}");

                switch (SurfaceSubtype)
                {
                    case "ref":
                        int refId = AcisParsingUtils.GetInteger(record, ref CurrentChunkIndex, "ReferencedSurfaceID");
                        ReferencedSurface = record.Reader.GetSubtypeEntity(refId);
                        break;
                    case "spl_sur":
                        ParseSplineSurfaceProper(record, ref CurrentChunkIndex, header);
                        break;
                    case "cyl_spl_sur":
                        ParseCylSplineSurface(record, ref CurrentChunkIndex, header);
                        break;
                    case "rot_spl_sur":
                        ParseRotSplineSurface(record, ref CurrentChunkIndex, header);
                        break;
                    case "off_spl_sur":
                        ParseOffsetSplineSurface(record, ref CurrentChunkIndex, header);
                        break;
                    case "rule_sur":
                        ParseRuledSplineSurface(record, ref CurrentChunkIndex, header);
                        break;
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
                 Logger.Error($"{TypeName}.Set: Expected SubtypeOpen '{{' not found for record {Record.Name} #{Record.Index}. Chunk: {(Record.Chunks.Count > CurrentChunkIndex ? Record.Chunks[CurrentChunkIndex]?.Val : "EOF")}");
            }
            return CurrentChunkIndex;
        }

        private void ParseSplineSurfaceProper(AcisRecord record, ref int chunkIndex, AcisHeader header)
        {
            Logger.Info($"{TypeName}.ParseSplineSurfaceProper: Parsing 'spl_sur' data for record {Record.Name} #{Record.Index}");
            SplineGeometricData = new BSSurfaceData();

            string singularity = AcisParsingUtils.GetText(record, ref chunkIndex, "SplineSurface.Singularity");
            SplineGeometricData.USingularity = singularity;

            AcisParsingUtils.GetDimensionSurface(record, ref chunkIndex, out bool isNurbsTemp,
                                               out int uDegreeTemp, out int vDegreeTemp, "SplineSurface.Dimension");
            SplineGeometricData.URational = isNurbsTemp; // Assuming IsRational applies to U for BSSurfaceData
            SplineGeometricData.VRational = isNurbsTemp; // Assuming IsRational applies to V for BSSurfaceData
            SplineGeometricData.UDegree = uDegreeTemp;
            SplineGeometricData.VDegree = vDegreeTemp;

            AcisParsingUtils.GetClosureSurface(record, ref chunkIndex,
                out string uClosureTemp, out string vClosureTemp,
                out SplineGeometricData.USingularity, out SplineGeometricData.VSingularity, // These might be different from the first singularity
                out int uKnotCountFromHeaderTemp, out int vKnotCountFromHeaderTemp, "SplineSurface.Closure");

            SplineGeometricData.UPeriodic = uClosureTemp.Equals("periodic", StringComparison.OrdinalIgnoreCase);
            SplineGeometricData.VPeriodic = vClosureTemp.Equals("periodic", StringComparison.OrdinalIgnoreCase);
            // SplineGeometricData.UKnotCountFromHeader = uKnotCountFromHeaderTemp; // Not on BSSurfaceData
            // SplineGeometricData.VKnotCountFromHeader = vKnotCountFromHeaderTemp; // Not on BSSurfaceData

            AcisParsingUtils.ReadKnotsAndMults(record, ref chunkIndex, uKnotCountFromHeaderTemp,
                                             out SplineGeometricData.UKnots, out SplineGeometricData.UMultiplicities, "SplineSurface.UKnots");
            AcisParsingUtils.ReadKnotsAndMults(record, ref chunkIndex, vKnotCountFromHeaderTemp,
                                             out SplineGeometricData.VKnots, out SplineGeometricData.VMultiplicities, "SplineSurface.VKnots");

            if (SplineGeometricData.UMultiplicities.Any() && SplineGeometricData.VMultiplicities.Any())
            {
                AcisParsingUtils.AdjustKnotsAndMults(SplineGeometricData.UKnots, SplineGeometricData.UMultiplicities, SplineGeometricData.UDegree);
                AcisParsingUtils.AdjustKnotsAndMults(SplineGeometricData.VKnots, SplineGeometricData.VMultiplicities, SplineGeometricData.VDegree);

                int expectedUPoleCount = SplineGeometricData.UMultiplicities.Sum() - SplineGeometricData.UDegree - 1;
                int expectedVPoleCount = SplineGeometricData.VMultiplicities.Sum() - SplineGeometricData.VDegree - 1;
                if (SplineGeometricData.UPeriodic) expectedUPoleCount = SplineGeometricData.UMultiplicities.Take(SplineGeometricData.UMultiplicities.Count-1).Sum();
                if (SplineGeometricData.VPeriodic) expectedVPoleCount = SplineGeometricData.VMultiplicities.Take(SplineGeometricData.VMultiplicities.Count-1).Sum();

                AcisParsingUtils.ReadPoints3DSurface(record, ref chunkIndex, header, SplineGeometricData,
                                                   expectedUPoleCount, expectedVPoleCount, "SplineSurface.Poles");
            } else {
                 Logger.Warning($"{TypeName}.ParseSplineSurfaceProper: UMultiplicities or VMultiplicities not populated, cannot calculate pole count for {Record.Name} #{Record.Index}");
            }
            Logger.Warning($"{TypeName}.ParseSplineSurfaceProper: DiscontinuityInfo and Tolerance parsing is deferred for 'spl_sur'.");
        }

        private void ParseCylSplineSurface(AcisRecord record, ref int chunkIndex, AcisHeader header)
        {
            Logger.Info($"{TypeName}.ParseCylSplineSurface: Parsing 'cyl_spl_sur' subtype for record {Record.Name} #{Record.Index}");
            ProfileCurve = AcisParsingUtils.GetRefNode(record, ref chunkIndex, "ProfileCurve", "curve-entity") as Curve;
            AxisVector = AcisParsingUtils.GetVector(record, ref chunkIndex, "AxisVector");
            CenterPoint = AcisParsingUtils.GetLocation(record, ref chunkIndex, header, "CenterPoint");
            ParseSplineSurfaceProper(record, ref chunkIndex, header);
        }

        private void ParseRotSplineSurface(AcisRecord record, ref int chunkIndex, AcisHeader header)
        {
            Logger.Info($"{TypeName}.ParseRotSplineSurface: Parsing 'rot_spl_sur' subtype for record {Record.Name} #{Record.Index}");
            ProfileCurve = AcisParsingUtils.GetRefNode(record, ref chunkIndex, "ProfileCurve", "curve-entity") as Curve;
            CenterPoint = AcisParsingUtils.GetLocation(record, ref chunkIndex, header, "LocationPoint");
            DirectionVector = AcisParsingUtils.GetVector(record, ref chunkIndex, "DirectionVector");
            ParseSplineSurfaceProper(record, ref chunkIndex, header);
        }

        private void ParseOffsetSplineSurface(AcisRecord record, ref int chunkIndex, AcisHeader header)
        {
            Logger.Info($"{TypeName}.ParseOffsetSplineSurface: Parsing 'off_spl_sur' subtype for record {Record.Name} #{Record.Index}");

            BaseSurfaceForOffset = AcisParsingUtils.GetRefNode(record, ref chunkIndex, "BaseSurfaceForOffset", "surface-entity") as Surface;
            OffsetDistance = AcisParsingUtils.GetFloat(record, ref chunkIndex, "OffsetDistance");

            // Assuming USenseOffset and VSenseOffset are stored as standard sense enums or similar tags
            // Python code uses getSense(chunks, i) which maps "forward" to 0, "reversed" to 1
            // Let's assume GetEnumByTag or a similar utility can parse these if they are standard string tags
            // Or, if they are simple integer flags, GetInteger would be used.
            // For now, using GetEnumByTag as a placeholder for how sense might be stored.
            USenseOffset = AcisParsingUtils.GetEnumByTag<SenseEnum>(record, ref chunkIndex, "USenseOffset");
            VSenseOffset = AcisParsingUtils.GetEnumByTag<SenseEnum>(record, ref chunkIndex, "VSenseOffset");

            // After parsing offset-specific data, parse the B-Spline data of the offset surface itself
            ParseSplineSurfaceProper(record, ref chunkIndex, header);
        }

        private void ParseRuledSplineSurface(AcisRecord record, ref int chunkIndex, AcisHeader header)
        {
            Logger.Info($"{TypeName}.ParseRuledSplineSurface: Parsing 'rule_sur' subtype for record {Record.Name} #{Record.Index}");

            ProfileCurve1 = AcisParsingUtils.GetRefNode(record, ref chunkIndex, "ProfileCurve1", "curve-entity") as Curve;
            ProfileCurve2 = AcisParsingUtils.GetRefNode(record, ref chunkIndex, "ProfileCurve2", "curve-entity") as Curve;

            // After parsing the two profile curves, parse the B-Spline data of the ruled surface itself
            ParseSplineSurfaceProper(record, ref chunkIndex, header);
        }
    }

    // public class SurfaceCone : Surface { public SurfaceCone() : base("cone-surface") { /* Set Implemented */ } } // Already defined with Set
    // public class CurveEllipse : Curve { public CurveEllipse() : base("ellipse-curve") { /* Set Implemented */ } } // Already defined with Set
    // public class SurfaceSphere : Surface { public SurfaceSphere() : base("sphere-surface") { /* Set Implemented */ } } // Already defined with Set
    // public class SurfaceTorus : Surface { public SurfaceTorus() : base("torus-surface") { /* Set Implemented */ } } // Already defined with Set

    public class CurveComp : Curve { public CurveComp() : base("compcurv-curve") { } public override int Set(AcisRecord rec){ base.Set(rec); Logger.Warning($"Set for {TypeName} NI"); return CurrentChunkIndex;} }
    public class CurveDegenerate : Curve { public CurveDegenerate() : base("degenerate-curve") { } public override int Set(AcisRecord rec){ base.Set(rec); Logger.Warning($"Set for {TypeName} NI"); return CurrentChunkIndex;} }
    // CurveInt implemented above
    public class CurveIntInt : Curve { public CurveIntInt() : base("intcurve-intcurve-curve") { } public override int Set(AcisRecord rec){ base.Set(rec); Logger.Warning($"Set for {TypeName} NI"); return CurrentChunkIndex;} }

    public class CurveP : Curve
    {
        public int PcurveType { get; set; }
        public SenseEnum Sense { get; set; }
        public string PcurveSubtypeString { get; set; }

        public AcisEntity ReferencedPCurveEntity { get; set; }

        public BSCurveData PcurveSplineData { get; set; }
        public double Tolerance { get; set; }
        public Surface SurfaceForExppc { get; set; }

        public Curve CurveForTypeNot0 { get; set; }
        public double UParam { get; set; }
        public double VParam { get; set; }

        public CurveP() : base("pcurve-curve") { }

        public override int Set(AcisRecord record)
        {
            base.Set(record);
            var header = AcisGlobalUtils.GetReader()?.Header;
            if (header == null) { Logger.Error($"{TypeName}.Set: ACIS Header not available."); return CurrentChunkIndex; }

            PcurveType = AcisParsingUtils.GetInteger(record, ref CurrentChunkIndex, "PcurveType");

            if (PcurveType == 0)
            {
                ParsePSubtype(record, ref CurrentChunkIndex, header);
            }
            else
            {
                CurveForTypeNot0 = AcisParsingUtils.GetRefNode(record, ref CurrentChunkIndex, "CurveForTypeNot0", "curve-entity") as Curve;
                UParam = AcisParsingUtils.GetFloat(record, ref CurrentChunkIndex, "UParam");
                VParam = AcisParsingUtils.GetFloat(record, ref CurrentChunkIndex, "VParam");
                Logger.Info($"{TypeName}.Set: Parsed direct curve reference with U={UParam}, V={VParam}.");
            }
            return CurrentChunkIndex;
        }

        private void ParsePSubtype(AcisRecord record, ref int chunkIndex, AcisHeader header)
        {
            Sense = AcisParsingUtils.GetEnumByTag<SenseEnum>(record, ref chunkIndex, "Sense");

            if (Record.Chunks.Count > chunkIndex && Record.Chunks[chunkIndex] is AcisChunkSubtypeOpen)
            {
                chunkIndex++;
                PcurveSubtypeString = AcisParsingUtils.GetText(record, ref chunkIndex, "Pcurve.SubtypeString");
                Logger.Info($"{TypeName}.ParsePSubtype: subtype is {PcurveSubtypeString} for record {Record.Name} #{Record.Index}");

                switch (PcurveSubtypeString?.ToLowerInvariant())
                {
                    case "ref":
                        int refId = AcisParsingUtils.GetInteger(record, ref chunkIndex, "ReferencedPCurveID");
                        ReferencedPCurveEntity = record.Reader.GetSubtypeEntity(refId);
                        break;
                    case "exppc":
                    case "exp_par_cur":
                        PcurveSplineData = new BSCurveData();
                        AcisParsingUtils.GetDimensionCurve(record, ref chunkIndex, out PcurveSplineData.IsRational, out PcurveSplineData.Degree, "PcurveDim");
                        AcisParsingUtils.GetClosureCurve(record, ref chunkIndex, out string closureType,
                                                        // out PcurveSplineData.KnotCountFromHeader, // Not on BSCurveData
                                                        out int knotCount, // Use local var
                                                        "PcurveClosure");
                        PcurveSplineData.IsPeriodic = closureType.Equals("periodic", StringComparison.OrdinalIgnoreCase);

                        if (!AcisParsingUtils.ReadKnotsAndMults(record, ref chunkIndex, knotCount,
                                                         out PcurveSplineData.Knots, out PcurveSplineData.Multiplicities, "PcurveKnots"))
                            { Logger.Error("Failed to read pcurve knots and mults."); break; }

                        if (PcurveSplineData.Multiplicities.Any())
                        {
                            int expectedPoleCount = PcurveSplineData.Multiplicities.Sum() - PcurveSplineData.Degree - 1;
                            if (PcurveSplineData.IsPeriodic) expectedPoleCount = PcurveSplineData.Multiplicities.Take(PcurveSplineData.Multiplicities.Count-1).Sum();

                            AcisParsingUtils.AdjustKnotsAndMults(PcurveSplineData.Knots, PcurveSplineData.Multiplicities, PcurveSplineData.Degree);
                            if(!AcisParsingUtils.ReadPoints2DList(record, ref chunkIndex, header, PcurveSplineData, expectedPoleCount, "PcurvePoles"))
                                { Logger.Error("Failed to read pcurve 2D poles."); break; }
                        } else { Logger.Warning($"{TypeName}.ParsePSubtype (exppc): Multiplicities not populated for {Record.Name} #{Record.Index}"); }

                        Tolerance = AcisParsingUtils.GetFloat(record, ref chunkIndex, "Tolerance");

                        bool isAsm = header.Format?.StartsWith("ASM") ?? false;
                        if (header.Version > 11.0 && !isAsm) {
                            if(Record.Chunks.Count > chunkIndex) chunkIndex++;
                            else Logger.Warning($"{TypeName}.ParsePSubtype (exppc): Not enough chunks for version skip.");
                        }

                        SurfaceForExppc = AcisParsingUtils.GetRefNode(record, ref chunkIndex, "SurfaceForExppc", "surface-entity") as Surface;
                        break;
                    case "imppc":
                    case "imp_par_cur":
                        Logger.Info($"{TypeName}.ParsePSubtype: Attempting to parse embedded CurveInt for 'imppc' subtype.");
                        Logger.Warning("imppc/imp_par_cur pcurve subtype parsing is complex and deferred. Skipping conceptual block.");
                        AcisParsingUtils.GetRefNode(record, ref chunkIndex, "EmbeddedCurveIntRef_Placeholder");
                        break;
                    default:
                        Logger.Warning($"{TypeName}.ParsePSubtype: Unsupported pcurve subtype '{PcurveSubtypeString}' for record {Record.Name} #{Record.Index}.");
                        while(chunkIndex < Record.Chunks.Count && !(Record.Chunks[chunkIndex] is AcisChunkSubtypeClose)) chunkIndex++;
                        break;
                }
                if (chunkIndex < Record.Chunks.Count && Record.Chunks[chunkIndex] is AcisChunkSubtypeClose) chunkIndex++;
                else Logger.Warning($"{TypeName}.ParsePSubtype: Missing '}}' for subtype {PcurveSubtypeString}. Record: {Record.Name} #{Record.Index}");
            }
            else { Logger.Error($"{TypeName}.ParsePSubtype: Expected SubtypeOpen '{{' not found after Sense. Current chunk: {(Record.Chunks.Count > chunkIndex ? Record.Chunks[CurrentChunkIndex].Val.ToString() : "EOF")}"); }
        }
    }
    public class SurfaceMesh : Surface { public SurfaceMesh() : base("meshsurf-surface") { } public override int Set(AcisRecord rec){ base.Set(rec); Logger.Warning($"Set for {TypeName} NI"); return CurrentChunkIndex;} }

    public class Attrib : Attributes { public Attrib() : base("attrib-attrib") { } }

    public class AttribADesk : Attributes
    {
        public AttribADesk() : base("adesk-attrib") { }
        public AttribADesk(string subtype) : base(subtype) { } // Allow derived to set specific subtype
        public override int Set(AcisRecord record)
        {
            return base.Set(record); // Base Attributes.Set handles common fields
        }
    }

    public class AttribADeskColor : AttribADesk
    {
        public double Red { get; set; } public double Green { get; set; } public double Blue { get; set; }
        public AttribADeskColor() : base("color-adesk-attrib") { }
        public override int Set(AcisRecord record)
        {
            base.Set(record);
            // Example: Read color if specific format is known for this non-truecolor version
            Logger.Warning($"Set for {TypeName} (ADeskColor) needs specific parsing if any.");
            return CurrentChunkIndex;
        }
    }
    public class AttribADeskMaterial : AttribADesk
    {
        public AttribADeskMaterial() : base("material-adesk-attrib") { }
        public override int Set(AcisRecord record)
        {
            base.Set(record);
            Logger.Warning($"Set for {TypeName} (ADeskMaterial) needs specific parsing if any.");
            return CurrentChunkIndex;
        }
    }

    public class AttribADeskTrueColor : AttribADesk
    {
        public byte Alpha { get; set; }
        public byte Red { get; set; }
        public byte Green { get; set; }
        public byte Blue { get; set; }
        public AttribADeskTrueColor() : base("truecolor-adesk-attrib") { }
        public override int Set(AcisRecord record)
        {
            base.Set(record);
            int rgbaInt = AcisParsingUtils.GetInteger(record, ref CurrentChunkIndex, "RGBA_Integer");
            Alpha = (byte)((rgbaInt >> 24) & 0xFF);
            Red = (byte)((rgbaInt >> 16) & 0xFF);
            Green = (byte)((rgbaInt >> 8) & 0xFF);
            Blue = (byte)(rgbaInt & 0xFF);
            return CurrentChunkIndex;
        }
    }

    public class AttribGen : Attributes
    {
        public AttribGen() : base("gen-attrib") { }
        public AttribGen(string subtype) : base(subtype) { }
        public override int Set(AcisRecord record)
        {
            return base.Set(record);
        }
    }

    public class AttribGenName : AttribGen
    {
        public string Text { get; set; }
        public AttribGenName() : base("name_attrib-gen-attrib") { }
        public override int Set(AcisRecord record)
        {
            base.Set(record);
            var header = AcisGlobalUtils.GetReader()?.Header;
            if (header == null) { Logger.Error($"{TypeName}.Set: ACIS Header not available."); return CurrentChunkIndex; }
            bool isAsm = header.Format?.StartsWith("ASM") ?? false;

            if (header.Version < 16.0 || isAsm)
            {
                Logger.Info($"{TypeName}.Set: Skipping 4 chunks for version < 16 or ASM model.");
                for(int k=0; k<4 && Record.Chunks.Count > CurrentChunkIndex; ++k) CurrentChunkIndex++;
            }
            if (Record.Chunks.Count > CurrentChunkIndex)
                Text = AcisParsingUtils.GetText(record, ref CurrentChunkIndex, "TextValue");
            else
                Logger.Warning($"{TypeName}.Set: Not enough chunks to read TextValue for {Record.Name} #{Record.Index}");
            return CurrentChunkIndex;
        }
    }
    public class AttribStRgbColor : Attributes
    {
        public double Red { get; set; }
        public double Green { get; set; }
        public double Blue { get; set; }
        public AttribStRgbColor() : base("rgb_color-st-attrib"){}
        public override int Set(AcisRecord record)
        {
            base.Set(record);
            Red = AcisParsingUtils.GetFloat(record, ref CurrentChunkIndex, "Red");
            Green = AcisParsingUtils.GetFloat(record, ref CurrentChunkIndex, "Green");
            Blue = AcisParsingUtils.GetFloat(record, ref CurrentChunkIndex, "Blue");
            return CurrentChunkIndex;
        }
    }

    // Naming Attributes
    public class AttribNamingMatching : Attributes // Base for NMX specific attributes
    {
        public AttribNamingMatching() : base("matching_criteria-nmx-attrib") { } // Default subtype
        public AttribNamingMatching(string subtype) : base(subtype) { } // Allow derived to set specific subtype

        public override int Set(AcisRecord record)
        {
            return base.Set(record); // Handles common attribute fields
        }
    }

    public class AttribNamingMatchingNMxFFColorEntity : AttribNamingMatching
    {
        public List<int> IntParams { get; set; }
        public string EntityName { get; set; }
        public List<Tuple<int, int>> DcIndexMappings { get; set; }

        public AttribNamingMatchingNMxFFColorEntity() : base("nmx_ff_color_entity_matching_criteria-nmx-attrib")
        {
            IntParams = new List<int>();
            DcIndexMappings = new List<Tuple<int, int>>();
        }

        public override int Set(AcisRecord record)
        {
            base.Set(record); // Call Attributes.Set via AttribNamingMatching.Set

            // Parse 2 integers
            IntParams.Add(AcisParsingUtils.GetInteger(record, ref CurrentChunkIndex, "IntParam1"));
            IntParams.Add(AcisParsingUtils.GetInteger(record, ref CurrentChunkIndex, "IntParam2"));

            EntityName = AcisParsingUtils.GetText(record, ref CurrentChunkIndex, "EntityName");

            DcIndexMappings = AcisParsingUtils.GetDcIndexMappings(record, ref CurrentChunkIndex, "DcIndexMappings");

            return CurrentChunkIndex;
        }
    }

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
