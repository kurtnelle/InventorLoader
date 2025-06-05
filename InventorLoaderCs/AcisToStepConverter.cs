using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;

// Assuming AcisHeader and AcisBody (from AcisReader.cs/AcisEntities.cs) will be available
// For now, let's define placeholder types if they are not yet created or accessible.
// Using existing InventorLoaderCs namespace for AcisModel types
// using InventorLoaderCs;

namespace InventorLoaderCs
{
    public abstract class StepEntity
    {
        private static int NextId = 1;
        public static void ResetId(int startId = 1) { NextId = startId; }

        public int Id { get; private set; }
        public bool HasBeenExported { get; set; }

        protected StepEntity()
        {
            Id = NextId++;
            StepConverterUtils.RegisterEntity(this);
        }

        public abstract List<object> GetParameters();
        public abstract string GetClassName(); // Should be uppercase STEP name

        public virtual string ExportStep()
        {
            if (HasBeenExported)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            // Optional: Add comments for ACIS entity reference if available
            // if (this is SomeStepEntity se && se.AcisRefId > 0) sb.AppendLine($"/* ACIS #{se.AcisRefId} */");

            sb.Append($"#{Id} = {GetClassName()}(");

            List<object> parameters = GetParameters();
            for (int i = 0; i < parameters.Count; i++)
            {
                sb.Append(StepConverterUtils.ObjToString(parameters[i]));
                if (i < parameters.Count - 1)
                {
                    sb.Append(",");
                }
            }
            sb.Append(");\n");

            HasBeenExported = true;
            sb.Append(ExportPropertiesStep());
            return sb.ToString();
        }

        public virtual string ExportPropertiesStep()
        {
            var sb = new StringBuilder();
            List<object> parameters = GetParameters(); // May need to access fields directly if GetParameters doesn't include all refs
            foreach (var param in parameters)
            {
                ProcessParamForExport(param, sb);
            }
            return sb.ToString();
        }

        protected void ProcessParamForExport(object param, StringBuilder sb)
        {
            if (param is StepEntity se && !se.HasBeenExported)
            {
                sb.Append(se.ExportStep());
            }
            else if (param is IEnumerable<object> list && !(param is string)) // Exclude strings from being iterated
            {
                foreach (var item in list)
                {
                    ProcessParamForExport(item, sb); // Recursive call for items in lists
                }
            }
        }

        public override string ToString() => $"#{Id}";
    }

    public class StepNamedEntity : StepEntity
    {
        public string Name { get; set; }
        protected StepNamedEntity(string name = "") : base() { Name = name; }
        public override List<object> GetParameters() => new List<object> { Name };
    }

    // --- Core Geometric and Topological STEP Entities ---
    public class CartesianPoint : StepNamedEntity // CARTESIAN_POINT
    {
        public List<double> Coordinates { get; set; }
        public CartesianPoint(string name, List<double> coordinates) : base(name) { Coordinates = coordinates; }
        public override string GetClassName() => "CARTESIAN_POINT";
        public override List<object> GetParameters() => new List<object> { Name, Coordinates };
    }

    public class Direction : StepNamedEntity // DIRECTION
    {
        public List<double> Ratios { get; set; }
        public Direction(string name, List<double> ratios) : base(name) { Ratios = ratios; }
        public override string GetClassName() => "DIRECTION";
        public override List<object> GetParameters() => new List<object> { Name, Ratios };
    }

    public class Vector : StepNamedEntity // VECTOR
    {
        public Direction Orientation { get; set; }
        public double Magnitude { get; set; }
        public Vector(string name, Direction orientation, double magnitude) : base(name)
        { Orientation = orientation; Magnitude = magnitude; }
        public override string GetClassName() => "VECTOR";
        public override List<object> GetParameters() => new List<object> { Name, Orientation, Magnitude };
    }

    public class Line : StepNamedEntity // LINE
    {
        public CartesianPoint Pnt { get; set; }
        public Vector Dir { get; set; }
        public Line(string name, CartesianPoint pnt, Vector dir) : base(name) { Pnt = pnt; Dir = dir; }
        public override string GetClassName() => "LINE";
        public override List<object> GetParameters() => new List<object> { Name, Pnt, Dir };
    }

    public class VertexPoint : StepNamedEntity // VERTEX_POINT
    {
        public CartesianPoint VertexGeometry { get; set; }
        public VertexPoint(string name, CartesianPoint vertexGeometry) : base(name) { VertexGeometry = vertexGeometry; }
        public override string GetClassName() => "VERTEX_POINT";
        public override List<object> GetParameters() => new List<object> { Name, VertexGeometry };
    }

    public class EdgeCurve : StepNamedEntity // EDGE_CURVE
    {
        public VertexPoint EdgeStart { get; set; }
        public VertexPoint EdgeEnd { get; set; }
        public StepEntity CurveGeometry { get; set; } // e.g., Line, Circle, BSplineCurveWithKnots
        public bool SameSense { get; set; }
        public EdgeCurve(string name, VertexPoint start, VertexPoint end, StepEntity curveGeom, bool sense) : base(name)
        { EdgeStart = start; EdgeEnd = end; CurveGeometry = curveGeom; SameSense = sense; }
        public override string GetClassName() => "EDGE_CURVE";
        public override List<object> GetParameters() => new List<object> { Name, EdgeStart, EdgeEnd, CurveGeometry, SameSense };
    }

    public class OrientedEdge : StepNamedEntity // ORIENTED_EDGE
    {
        // Name is often empty string for this one in STEP
        public EdgeCurve EdgeElement { get; set; } // This is simplified; STEP uses an edge_element choice.
        public bool Orientation { get; set; }
        public OrientedEdge(string name, EdgeCurve edgeElement, bool orientation) : base(name)
        { EdgeElement = edgeElement; Orientation = orientation; }
        public override string GetClassName() => "ORIENTED_EDGE";
        // STEP: ORIENTED_EDGE(*,*,*,EDGE_ELEMENT,ORIENTATION) - the three * are from supertypes.
        // For now, direct properties:
        public override List<object> GetParameters() => new List<object> { Name, new AcisStepAnyEntity(), new AcisStepAnyEntity(), EdgeElement, Orientation };
    }

    // Helper for * in STEP lists
    public class AcisStepAnyEntity
    {
        public override string ToString() => "*";
    }


    public class EdgeLoop : StepNamedEntity // EDGE_LOOP
    {
        public List<OrientedEdge> EdgeList { get; set; }
        public EdgeLoop(string name, List<OrientedEdge> edgeList) : base(name) { EdgeList = edgeList; }
        public override string GetClassName() => "EDGE_LOOP";
        public override List<object> GetParameters() => new List<object> { Name, EdgeList };
    }

    public class FaceBound : StepNamedEntity // FACE_BOUND
    {
        public EdgeLoop Bound { get; set; }
        public bool Orientation { get; set; }
        public FaceBound(string name, EdgeLoop bound, bool orientation) : base(name)
        { Bound = bound; Orientation = orientation; }
        public override string GetClassName() => "FACE_BOUND";
        public override List<object> GetParameters() => new List<object> { Name, Bound, Orientation };
    }

    public class FaceOuterBound : FaceBound // FACE_OUTER_BOUND
    {
        public FaceOuterBound(string name, EdgeLoop bound, bool orientation) : base(name, bound, orientation) { }
        public override string GetClassName() => "FACE_OUTER_BOUND";
    }


    public class AdvancedFace : StepNamedEntity // ADVANCED_FACE
    {
        public List<FaceBound> Bounds { get; set; }
        public Surface FaceGeometry { get; set; } // e.g. Plane, CylindricalSurface
        public bool SameSense { get; set; }
        public AdvancedFace(string name, List<FaceBound> bounds, Surface faceGeom, bool sense) : base(name)
        { Bounds = bounds; FaceGeometry = faceGeom; SameSense = sense; }
        public override string GetClassName() => "ADVANCED_FACE";
        public override List<object> GetParameters() => new List<object> { Name, Bounds, FaceGeometry, SameSense };
    }

    public class OpenShell : StepNamedEntity // OPEN_SHELL
    {
        public List<AdvancedFace> CfsFaces { get; set; } // Connected Face Set
        public OpenShell(string name, List<AdvancedFace> faces) : base(name) { CfsFaces = faces; }
        public override string GetClassName() => "OPEN_SHELL";
        public override List<object> GetParameters() => new List<object> { Name, CfsFaces };
    }

    // Placeholder for ClosedShell
    public class ClosedShell : OpenShell
    {
        public ClosedShell(string name, List<AdvancedFace> faces) : base(name, faces) { }
        public override string GetClassName() => "CLOSED_SHELL";
    }


    public class Axis2Placement3D : StepNamedEntity // AXIS2_PLACEMENT_3D
    {
        public CartesianPoint Location { get; set; }
        public Direction Axis { get; set; } // Z-axis
        public Direction RefDirection { get; set; } // X-axis
        public Axis2Placement3D(string name, CartesianPoint loc, Direction axis, Direction refDir) : base(name)
        { Location = loc; Axis = axis; RefDirection = refDir; }
        public override string GetClassName() => "AXIS2_PLACEMENT_3D";
        public override List<object> GetParameters() => new List<object> { Name, Location, Axis, RefDirection };
    }

    public class BSplineCurveWithKnots : StepNamedEntity // B_SPLINE_CURVE_WITH_KNOTS
    {
        public int Degree { get; set; }
        public List<CartesianPoint> ControlPointsList { get; set; }
        public string CurveForm { get; set; } // e.g. .POLYLINE_FORM.
        public bool ClosedCurve { get; set; }
        public bool SelfIntersect { get; set; }
        public List<int> KnotMultiplicities { get; set; }
        public List<double> Knots { get; set; }
        public string KnotSpec { get; set; } // e.g. .UNSPECIFIED.

        public BSplineCurveWithKnots(string name, int degree, List<CartesianPoint> controlPoints,
                                     string form, bool closed, bool selfIntersect,
                                     List<int> mults, List<double> knots, string spec) : base(name)
        {
            Degree = degree; ControlPointsList = controlPoints; CurveForm = form;
            ClosedCurve = closed; SelfIntersect = selfIntersect; KnotMultiplicities = mults;
            Knots = knots; KnotSpec = spec;
        }
        public override string GetClassName() => "B_SPLINE_CURVE_WITH_KNOTS";
        public override List<object> GetParameters() => new List<object> { Name, Degree, ControlPointsList,
            new StepEnumWrapper(CurveForm), ClosedCurve, SelfIntersect, KnotMultiplicities, Knots, new StepEnumWrapper(KnotSpec) };
    }

    // Wrapper for enums that should be exported like .ENUM_VALUE.
    public class StepEnumWrapper
    {
        public string Value { get; set; }
        public StepEnumWrapper(string value) { Value = value.ToUpper(); }
        public override string ToString() => $".{Value}.";
    }


    public class Plane : StepNamedEntity // PLANE (Subtype of ElementarySurface)
    {
        public Axis2Placement3D Position { get; set; }
        public Plane(string name, Axis2Placement3D position) : base(name) { Position = position; }
        public override string GetClassName() => "PLANE";
        public override List<object> GetParameters() => new List<object> { Name, Position };
    }

    public class CylindricalSurface : StepNamedEntity // CYLINDRICAL_SURFACE
    {
        public Axis2Placement3D Position { get; set; }
        public double Radius { get; set; }
        public CylindricalSurface(string name, Axis2Placement3D pos, double radius) : base(name)
        { Position = pos; Radius = radius; }
        public override string GetClassName() => "CYLINDRICAL_SURFACE";
        public override List<object> GetParameters() => new List<object> { Name, Position, Radius };
    }

    public class ConicalSurface : StepNamedEntity // CONICAL_SURFACE
    {
        public Axis2Placement3D Position { get; set; }
        public double Radius { get; set; }
        public double SemiAngle { get; set; } // In radians
        public ConicalSurface(string name, Axis2Placement3D pos, double radius, double semiAngle) : base(name)
        { Position = pos; Radius = radius; SemiAngle = semiAngle; }
        public override string GetClassName() => "CONICAL_SURFACE";
        public override List<object> GetParameters() => new List<object> { Name, Position, Radius, SemiAngle };
    }

    public class SphericalSurface : StepNamedEntity // SPHERICAL_SURFACE
    {
        public Axis2Placement3D Position { get; set; }
        public double Radius { get; set; }
        public SphericalSurface(string name, Axis2Placement3D pos, double radius) : base(name)
        { Position = pos; Radius = radius; }
        public override string GetClassName() => "SPHERICAL_SURFACE";
        public override List<object> GetParameters() => new List<object> { Name, Position, Radius };
    }

    public class ToroidalSurface : StepNamedEntity // TOROIDAL_SURFACE
    {
        public Axis2Placement3D Position { get; set; }
        public double MajorRadius { get; set; }
        public double MinorRadius { get; set; }
        public ToroidalSurface(string name, Axis2Placement3D pos, double majorRadius, double minorRadius) : base(name)
        { Position = pos; MajorRadius = majorRadius; MinorRadius = minorRadius; }
        public override string GetClassName() => "TOROIDAL_SURFACE";
        public override List<object> GetParameters() => new List<object> { Name, Position, MajorRadius, MinorRadius };
    }

    public class RationalBSplineCurve : BSplineCurveWithKnots // RATIONAL_B_SPLINE_CURVE
    {
        public List<double> WeightsData { get; set; }
        public RationalBSplineCurve(string name, int degree, List<CartesianPoint> controlPoints,
                                    string form, bool closed, bool selfIntersect,
                                    List<int> mults, List<double> knots, string spec,
                                    List<double> weights)
            : base(name, degree, controlPoints, form, closed, selfIntersect, mults, knots, spec)
        {
            WeightsData = weights;
        }
        public override string GetClassName() => "RATIONAL_B_SPLINE_CURVE";
        public override List<object> GetParameters()
        {
            var parameters = base.GetParameters(); // Gets Name, Degree, ControlPointsList, etc.
            parameters.Add(WeightsData);
            return parameters;
        }
    }

    public class BSplineSurfaceWithKnots : StepNamedEntity // B_SPLINE_SURFACE_WITH_KNOTS
    {
        public int UDegree { get; set; }
        public int VDegree { get; set; }
        public List<List<CartesianPoint>> ControlPointsList { get; set; } // List of lists (rows of points)
        public string SurfaceForm { get; set; } // e.g. .PLANE_SURF.
        public bool UClosed { get; set; }
        public bool VClosed { get; set; }
        public bool SelfIntersect { get; set; } // Note: STEP schema uses LOGICAL, not BOOLEAN for this often.
        public List<int> UMultiplicities { get; set; }
        public List<int> VMultiplicities { get; set; }
        public List<double> UKnots { get; set; }
        public List<double> VKnots { get; set; }
        public string KnotSpec { get; set; } // e.g. .UNSPECIFIED.

        public BSplineSurfaceWithKnots(string name, int uDegree, int vDegree, List<List<CartesianPoint>> controlPoints,
                                       string form, bool uClosed, bool vClosed, bool selfIntersect,
                                       List<int> uMults, List<int> vMults, List<double> uKnots, List<double> vKnots, string spec)
            : base(name)
        {
            UDegree = uDegree; VDegree = vDegree; ControlPointsList = controlPoints; SurfaceForm = form;
            UClosed = uClosed; VClosed = vClosed; SelfIntersect = selfIntersect;
            UMultiplicities = uMults; VMultiplicities = vMults; UKnots = uKnots; VKnots = vKnots; KnotSpec = spec;
        }

        public override string GetClassName() => "B_SPLINE_SURFACE_WITH_KNOTS";
        public override List<object> GetParameters() => new List<object> {
            Name, UDegree, VDegree, ControlPointsList, new StepEnumWrapper(SurfaceForm),
            UClosed, VClosed, SelfIntersect,
            UMultiplicities, VMultiplicities, UKnots, VKnots, new StepEnumWrapper(KnotSpec)
        };
    }

    public class RationalBSplineSurface : BSplineSurfaceWithKnots // RATIONAL_B_SPLINE_SURFACE
    {
        public List<List<double>> WeightsData { get; set; } // Grid of weights

        public RationalBSplineSurface(string name, int uDegree, int vDegree, List<List<CartesianPoint>> controlPoints,
                                      string form, bool uClosed, bool vClosed, bool selfIntersect,
                                      List<int> uMults, List<int> vMults, List<double> uKnots, List<double> vKnots, string spec,
                                      List<List<double>> weights)
            : base(name, uDegree, vDegree, controlPoints, form, uClosed, vClosed, selfIntersect, uMults, vMults, uKnots, vKnots, spec)
        {
            WeightsData = weights;
        }
        public override string GetClassName() => "RATIONAL_B_SPLINE_SURFACE";
        public override List<object> GetParameters()
        {
            var parameters = base.GetParameters();
            parameters.Add(WeightsData);
            return parameters;
        }
    }

    // Topological Representation Entities
    public class ManifoldSolidBRep : StepNamedEntity // MANIFOLD_SOLID_BREP
    {
        public ClosedShell Outer { get; set; }
        public ManifoldSolidBRep(string name, ClosedShell outer) : base(name) { Outer = outer; }
        public override string GetClassName() => "MANIFOLD_SOLID_BREP";
        public override List<object> GetParameters() => new List<object> { Name, Outer };
    }

    public class ShellBasedSurfaceModel : StepNamedEntity // SHELL_BASED_SURFACE_MODEL
    {
        public List<OpenShell> SbsmElements { get; set; } // List of shells
        public ShellBasedSurfaceModel(string name, List<OpenShell> elements) : base(name) { SbsmElements = elements; }
        public override string GetClassName() => "SHELL_BASED_SURFACE_MODEL";
        public override List<object> GetParameters() => new List<object> { Name, SbsmElements };
    }

    // Minimal STEP file structure entities
    public class FileDescription : StepEntity
    {
        public List<string> Description {get; set;}
        public string ImplementationLevel {get; set;}
        public FileDescription(List<string> desc, string implLevel) : base() {Description = desc; ImplementationLevel = implLevel;}
        public override string GetClassName() => "FILE_DESCRIPTION";
        public override List<object> GetParameters() => new List<object>{ Description, ImplementationLevel };
    }
    public class FileName : StepEntity
    {
        public string Name {get; set;}
        public string TimeStamp {get; set;}
        public List<string> Author {get; set;}
        public List<string> Organization {get; set;}
        public string PreprocessorVersion {get; set;}
        public string OriginatingSystem {get; set;}
        public string Authorisation {get; set;}
         public FileName(string name, string timeStamp, List<string> author, List<string> org, string prepVer, string origSys, string auth) : base()
        { Name = name; TimeStamp = timeStamp; Author = author; Organization = org; PreprocessorVersion = prepVer; OriginatingSystem = origSys; Authorisation = auth;}
        public override string GetClassName() => "FILE_NAME";
        public override List<object> GetParameters() => new List<object>{ Name, TimeStamp, Author, Organization, PreprocessorVersion, OriginatingSystem, Authorisation };
    }
     public class FileSchema : StepEntity
    {
        public List<string> SchemaIdentifiers {get; set;}
        public FileSchema(List<string> schemaIds) : base() {SchemaIdentifiers = schemaIds;}
        public override string GetClassName() => "FILE_SCHEMA";
        public override List<object> GetParameters() => new List<object>{ SchemaIdentifiers };
    }


    public static class StepConverterUtils
    {
        private static List<StepEntity> _allExportedEntities = new List<StepEntity>();
        private static Dictionary<string, CartesianPoint> _cartesianPoints = new Dictionary<string, CartesianPoint>();
        private static Dictionary<string, Direction> _directions = new Dictionary<string, Direction>();
        private static Dictionary<string, Vector> _vectors = new Dictionary<string, Vector>();
        private static Dictionary<string, Line> _lines = new Dictionary<string, Line>();
        private static Dictionary<string, VertexPoint> _vertexPoints = new Dictionary<string, VertexPoint>();
        private static Dictionary<string, EdgeCurve> _edgeCurves = new Dictionary<string, EdgeCurve>();
        private static Dictionary<string, OrientedEdge> _orientedEdges = new Dictionary<string, OrientedEdge>();

        // New caches for additional entities
        private static Dictionary<string, Axis2Placement3D> _axis2Placements = new Dictionary<string, Axis2Placement3D>();
        private static Dictionary<string, Plane> _planes = new Dictionary<string, Plane>();
        private static Dictionary<string, CylindricalSurface> _cylindricalSurfaces = new Dictionary<string, CylindricalSurface>();
        private static Dictionary<string, ConicalSurface> _conicalSurfaces = new Dictionary<string, ConicalSurface>();
        private static Dictionary<string, SphericalSurface> _sphericalSurfaces = new Dictionary<string, SphericalSurface>();
        private static Dictionary<string, ToroidalSurface> _toroidalSurfaces = new Dictionary<string, ToroidalSurface>();
        private static Dictionary<string, BSplineCurveWithKnots> _bSplineCurves = new Dictionary<string, BSplineCurveWithKnots>(); // Also for RationalBSplineCurve
        private static Dictionary<string, BSplineSurfaceWithKnots> _bSplineSurfaces = new Dictionary<string, BSplineSurfaceWithKnots>(); // Also for RationalBSplineSurface
        private static Dictionary<string, EdgeLoop> _edgeLoops = new Dictionary<string, EdgeLoop>();
        private static Dictionary<string, FaceBound> _faceBounds = new Dictionary<string, FaceBound>();
        private static Dictionary<string, AdvancedFace> _advancedFaces = new Dictionary<string, AdvancedFace>();
        private static Dictionary<string, OpenShell> _openShells = new Dictionary<string, OpenShell>(); // Includes ClosedShell
        private static Dictionary<string, ManifoldSolidBRep> _manifoldSolidBReps = new Dictionary<string, ManifoldSolidBRep>();
        private static Dictionary<string, ShellBasedSurfaceModel> _shellBasedSurfaceModels = new Dictionary<string, ShellBasedSurfaceModel>();


        internal static void RegisterEntity(StepEntity entity) => _allExportedEntities.Add(entity);

        public static void InitExport()
        {
            StepEntity.ResetId();
            _allExportedEntities.Clear();
            _cartesianPoints.Clear();
            _directions.Clear();
            _vectors.Clear();
            _lines.Clear();
            _vertexPoints.Clear();
            _edgeCurves.Clear();
            _orientedEdges.Clear();

            // Clear new caches
            _axis2Placements.Clear();
            _planes.Clear();
            _cylindricalSurfaces.Clear();
            _conicalSurfaces.Clear();
            _sphericalSurfaces.Clear();
            _toroidalSurfaces.Clear();
            _bSplineCurves.Clear();
            _bSplineSurfaces.Clear();
            _edgeLoops.Clear();
            _faceBounds.Clear();
            _advancedFaces.Clear();
            _openShells.Clear();
            _manifoldSolidBReps.Clear();
            _shellBasedSurfaceModels.Clear();
        }

        public static string Export(Inventor acisModel, string originalFileName, StreamWriter logFile = null)
        {
            InitExport();
            var stepBuilder = new StringBuilder();

            // HEADER
            stepBuilder.AppendLine("ISO-10303-21;");
            stepBuilder.AppendLine("HEADER;");
            new FileDescription(new List<string> { "Inventor Model via AcisToStepConverter" }, "2;1").ExportStep(); // Will register but not add to sb here
            new FileName(originalFileName, DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss"),
                         new List<string> { "Unknown Author" }, new List<string> { "Unknown Org" },
                         "InventorLoaderCs_v0.1", "Inventor", "Unknown Auth").ExportStep();
            new FileSchema(new List<string> { "AUTOMOTIVE_DESIGN { 1 0 10303 214 1 1 1 1 }" }).ExportStep(); // AP214 example
            // Export header entities first
            foreach(var entity in _allExportedEntities)
            {
                if(entity is FileDescription || entity is FileName || entity is FileSchema)
                    stepBuilder.Append(entity.ExportStep()); // These should have HasBeenExported=false first time
            }
             _allExportedEntities.RemoveAll(e => e is FileDescription || e is FileName || e is FileSchema); // Remove them so they are not re-exported
            StepEntity.ResetId(); // Reset ID for data section entities.
            _allExportedEntities.Clear(); // Clear list for data section entities


            stepBuilder.AppendLine("ENDSEC;");
            stepBuilder.AppendLine();
            stepBuilder.AppendLine("DATA;");

            // Simplified Traversal: Convert one hardcoded or simple entity
            // Example: Create and export a single point for testing
            if (acisModel != null) // Check if model exists
            {
                 // Try to find a point entity from the ACIS model to convert
                Point acisPoint = null;
                CurveStraight acisLine = null;

                foreach(var segment in acisModel.Segments.Values)
                {
                    if(segment.Nodes != null)
                    {
                        foreach(var secNode in segment.Nodes)
                        {
                            if(secNode.Entity is Point p) { acisPoint = p; break; }
                            if(secNode.Entity is CurveStraight cs) { acisLine = cs; break; }
                        }
                    }
                    if(acisPoint != null || acisLine != null) break;
                }

                if (acisPoint != null)
                {
                    CreateCartesianPoint(acisPoint.Position); // This creates and registers it
                }
                else if (acisLine != null)
                {
                    // Convert Acis CurveStraight to STEP Line
                    var startPoint = CreateCartesianPoint(acisLine.Root, "L_START");
                    var direction = CreateDirection(acisLine.Dir, "L_DIR");
                    var vector = CreateVector(direction, (acisLine.CurveRange.GetUpperLimit() - acisLine.CurveRange.GetLowerLimit()), "L_VEC"); // Approx length
                    CreateLine(startPoint, vector, "TestLine");
                }
                else
                {
                     Logger.Warning("StepConverterUtils.Export: No suitable ACIS point or line found for test export.");
                     // Create a default point if nothing found
                     CreateCartesianPoint(new Vector3(10,20,30), "DefaultTestPoint");
                }
            } else {
                 CreateCartesianPoint(new Vector3(10,20,30), "DefaultTestPointNoModel");
            }


            // Export all registered DATA entities
            foreach (var entity in _allExportedEntities)
            {
                stepBuilder.Append(entity.ExportStep());
            }

            stepBuilder.AppendLine("ENDSEC;");
            stepBuilder.AppendLine("END-ISO-10303-21;");

            FinalizeExport();
            return stepBuilder.ToString();
        }

        public static void FinalizeExport()
        {
            // _allExportedEntities is cleared by InitExport on next run.
        }

        // --- Create* Methods with Caching ---
        public static CartesianPoint CreateCartesianPoint(Vector3 vec, string name = "")
        {
            string key = $"{vec.X:F6},{vec.Y:F6},{vec.Z:F6}_{name}"; // Format to handle precision issues in keys
            if (!_cartesianPoints.TryGetValue(key, out CartesianPoint cp))
            {
                cp = new CartesianPoint(name, new List<double> { vec.X, vec.Y, vec.Z });
                _cartesianPoints[key] = cp;
            }
            return cp;
        }

        public static Direction CreateDirection(Vector3 vec, string name = "")
        {
            var normalizedVec = Vector3.Normalize(vec);
            string key = $"{normalizedVec.X:F6},{normalizedVec.Y:F6},{normalizedVec.Z:F6}_{name}";
            if (!_directions.TryGetValue(key, out Direction dir))
            {
                dir = new Direction(name, new List<double> { normalizedVec.X, normalizedVec.Y, normalizedVec.Z });
                _directions[key] = dir;
            }
            return dir;
        }

        public static Vector CreateVector(Direction orientation, double magnitude, string name = "")
        {
            // Key could be based on orientation.ToString() and magnitude
            string key = $"{orientation}_{magnitude}_{name}";
            if(!_vectors.TryGetValue(key, out var vec))
            {
                vec = new Vector(name, orientation, magnitude);
                _vectors[key] = vec;
            }
            return vec;
        }

        public static Line CreateLine(CartesianPoint pnt, Vector dir, string name = "")
        {
            string key = $"{pnt}_{dir}_{name}";
            if(!_lines.TryGetValue(key, out var line))
            {
                line = new Line(name, pnt, dir);
                _lines[key] = line;
            }
            return line;
        }

        public static VertexPoint CreateVertexPoint(CartesianPoint point, string name = "")
        {
            string key = $"{point}_{name}";
            if (!_vertexPoints.TryGetValue(key, out var vp))
            {
                vp = new VertexPoint(name, point);
                _vertexPoints[key] = vp;
            }
            return vp;
        }

        public static EdgeCurve CreateEdgeCurve(VertexPoint start, VertexPoint end, StepEntity curveGeom, bool sense, string name = "")
        {
            string key = $"{start}_{end}_{curveGeom}_{sense}_{name}";
            if(!_edgeCurves.TryGetValue(key, out var ec))
            {
                ec = new EdgeCurve(name, start, end, curveGeom, sense);
                _edgeCurves[key] = ec;
            }
            return ec;
        }

        public static OrientedEdge CreateOrientedEdge(EdgeCurve edgeElement, bool orientation, string name = "")
        {
            string key = $"{edgeElement}_{orientation}_{name}";
             if(!_orientedEdges.TryGetValue(key, out var oe))
            {
                oe = new OrientedEdge(name, edgeElement, orientation);
                _orientedEdges[key] = oe;
            }
            return oe;
        }

        public static EdgeLoop CreateEdgeLoop(List<OrientedEdge> edgeList, string name = "")
        {
            // Simple key for now, might need more robust hashing for list content if performance is an issue
            string key = $"EdgeLoop_{edgeList.GetHashCode()}_{name}";
            if (!_edgeLoops.TryGetValue(key, out var el))
            {
                el = new EdgeLoop(name, edgeList);
                _edgeLoops[key] = el;
            }
            return el;
        }

        public static FaceBound CreateFaceBound(EdgeLoop boundLoop, bool orientation, string name = "")
        {
            string key = $"{boundLoop}_{orientation}_{name}";
            if(!_faceBounds.TryGetValue(key, out var fb))
            {
                fb = new FaceBound(name, boundLoop, orientation);
                 _faceBounds[key] = fb;
            }
            return fb;
        }

        public static FaceOuterBound CreateFaceOuterBound(EdgeLoop boundLoop, bool orientation, string name = "")
        {
            // Using same cache as FaceBound for simplicity, or could have its own.
            string key = $"FaceOuterBound_{boundLoop}_{orientation}_{name}";
            if (!_faceBounds.TryGetValue(key, out var fobBase))
            {
                var fob = new FaceOuterBound(name, boundLoop, orientation);
                _faceBounds[key] = fob; // Store as FaceBound in cache
                return fob;
            }
            return fobBase as FaceOuterBound ?? new FaceOuterBound(name, boundLoop, orientation); // Recast or create new
        }

        public static AdvancedFace CreateAdvancedFace(List<FaceBound> bounds, Surface faceGeometry, bool sameSense, string name = "")
        {
            // Keying for lists can be complex. Using hash codes is a simplification.
            string key = $"AdvancedFace_{bounds.GetHashCode()}_{faceGeometry}_{sameSense}_{name}";
            if(!_advancedFaces.TryGetValue(key, out var af))
            {
                af = new AdvancedFace(name, bounds, faceGeometry, sameSense);
                _advancedFaces[key] = af;
            }
            return af;
        }

        public static OpenShell CreateOpenShell(List<AdvancedFace> faces, string name = "")
        {
            string key = $"OpenShell_{faces.GetHashCode()}_{name}";
            if(!_openShells.TryGetValue(key, out var os))
            {
                os = new OpenShell(name, faces);
                _openShells[key] = os;
            }
            return os;
        }

        public static ClosedShell CreateClosedShell(List<AdvancedFace> faces, string name = "")
        {
            string key = $"ClosedShell_{faces.GetHashCode()}_{name}";
            if (!_openShells.TryGetValue(key, out var csBase)) // Store in OpenShell cache
            {
                var cs = new ClosedShell(name, faces);
                _openShells[key] = cs;
                return cs;
            }
            return csBase as ClosedShell ?? new ClosedShell(name, faces);
        }

        public static ManifoldSolidBRep CreateManifoldSolidBRep(ClosedShell outerShell, string name = "")
        {
            string key = $"MSB_{outerShell}_{name}";
            if(!_manifoldSolidBReps.TryGetValue(key, out var msb))
            {
                msb = new ManifoldSolidBRep(name, outerShell);
                _manifoldSolidBReps[key] = msb;
            }
            return msb;
        }

        public static ShellBasedSurfaceModel CreateShellBasedSurfaceModel(List<OpenShell> shells, string name = "")
        {
            string key = $"SBSM_{shells.GetHashCode()}_{name}";
            if(!_shellBasedSurfaceModels.TryGetValue(key, out var sbsm))
            {
                sbsm = new ShellBasedSurfaceModel(name, shells);
                _shellBasedSurfaceModels[key] = sbsm;
            }
            return sbsm;
        }

        public static Axis2Placement3D CreateAxis2Placement3D(Vector3 location, Vector3 axis, Vector3 refDir, string name = "")
        {
            var locPt = CreateCartesianPoint(location);
            var axisDir = CreateDirection(axis);
            var refDirection = CreateDirection(refDir);
            string key = $"{locPt}_{axisDir}_{refDirection}_{name}";
            if (!_axis2Placements.TryGetValue(key, out var a2p3d))
            {
                a2p3d = new Axis2Placement3D(name, locPt, axisDir, refDirection);
                _axis2Placements[key] = a2p3d;
            }
            return a2p3d;
        }

        public static Plane CreatePlane(Axis2Placement3D position, string name = "")
        {
            string key = $"{position}_{name}";
            if (!_planes.TryGetValue(key, out var plane))
            {
                plane = new Plane(name, position);
                _planes[key] = plane;
            }
            return plane;
        }

        public static CylindricalSurface CreateCylindricalSurface(Axis2Placement3D position, double radius, string name = "")
        {
            string key = $"{position}_{radius}_{name}";
            if(!_cylindricalSurfaces.TryGetValue(key, out var cylSurf))
            {
                cylSurf = new CylindricalSurface(name, position, radius);
                _cylindricalSurfaces[key] = cylSurf;
            }
            return cylSurf;
        }

        public static ConicalSurface CreateConicalSurface(Axis2Placement3D position, double radius, double semiAngle, string name = "")
        {
            string key = $"{position}_{radius}_{semiAngle}_{name}";
            if(!_conicalSurfaces.TryGetValue(key, out var conSurf))
            {
                conSurf = new ConicalSurface(name, position, radius, semiAngle);
                _conicalSurfaces[key] = conSurf;
            }
            return conSurf;
        }

        public static SphericalSurface CreateSphericalSurface(Axis2Placement3D position, double radius, string name = "")
        {
            string key = $"{position}_{radius}_{name}";
            if(!_sphericalSurfaces.TryGetValue(key, out var sphSurf))
            {
                sphSurf = new SphericalSurface(name, position, radius);
                _sphericalSurfaces[key] = sphSurf;
            }
            return sphSurf;
        }

        public static ToroidalSurface CreateToroidalSurface(Axis2Placement3D position, double majorRadius, double minorRadius, string name = "")
        {
            string key = $"{position}_{majorRadius}_{minorRadius}_{name}";
            if(!_toroidalSurfaces.TryGetValue(key, out var torSurf))
            {
                torSurf = new ToroidalSurface(name, position, majorRadius, minorRadius);
                _toroidalSurfaces[key] = torSurf;
            }
            return torSurf;
        }

        public static BSplineCurveWithKnots CreateBSplineCurveWithKnots(
            int degree, List<CartesianPoint> controlPoints, string curveForm, bool closedCurve, bool selfIntersect,
            List<int> knotMultiplicities, List<double> knots, string knotSpec, string name = "")
        {
            // Keying for BSplines can be complex. This is a simplified key.
            string key = $"BSCurve_{degree}_{controlPoints.GetHashCode()}_{knotMultiplicities.GetHashCode()}_{knots.GetHashCode()}_{name}";
            if (!_bSplineCurves.TryGetValue(key, out var bsc))
            {
                bsc = new BSplineCurveWithKnots(name, degree, controlPoints, curveForm, closedCurve, selfIntersect, knotMultiplicities, knots, knotSpec);
                _bSplineCurves[key] = bsc;
            }
            return bsc;
        }

        public static RationalBSplineCurve CreateRationalBSplineCurve(
            int degree, List<CartesianPoint> controlPoints, string curveForm, bool closedCurve, bool selfIntersect,
            List<int> knotMultiplicities, List<double> knots, string knotSpec, List<double> weights, string name = "")
        {
            string key = $"RBSCurve_{degree}_{controlPoints.GetHashCode()}_{weights.GetHashCode()}_{name}";
             if (!_bSplineCurves.TryGetValue(key, out var rbscBase)) // Store in BSplineCurve cache
            {
                var rbsc = new RationalBSplineCurve(name, degree, controlPoints, curveForm, closedCurve, selfIntersect, knotMultiplicities, knots, knotSpec, weights);
                _bSplineCurves[key] = rbsc; // Store as BSplineCurveWithKnots (base)
                return rbsc;
            }
            return rbscBase as RationalBSplineCurve ?? new RationalBSplineCurve(name, degree, controlPoints, curveForm, closedCurve, selfIntersect, knotMultiplicities, knots, knotSpec, weights);
        }

        public static BSplineSurfaceWithKnots CreateBSplineSurfaceWithKnots(
            int uDegree, int vDegree, List<List<CartesianPoint>> controlPointsList, string surfaceForm,
            bool uClosed, bool vClosed, bool selfIntersect, List<int> uMultiplicities, List<int> vMultiplicities,
            List<double> uKnots, List<double> vKnots, string knotSpec, string name = "")
        {
            string key = $"BSSurf_{uDegree}_{vDegree}_{controlPointsList.GetHashCode()}_{name}";
            if(!_bSplineSurfaces.TryGetValue(key, out var bss))
            {
                bss = new BSplineSurfaceWithKnots(name, uDegree, vDegree, controlPointsList, surfaceForm, uClosed, vClosed, selfIntersect, uMultiplicities, vMultiplicities, uKnots, vKnots, knotSpec);
                _bSplineSurfaces[key] = bss;
            }
            return bss;
        }

        public static RationalBSplineSurface CreateRationalBSplineSurface(
            int uDegree, int vDegree, List<List<CartesianPoint>> controlPointsList, string surfaceForm,
            bool uClosed, bool vClosed, bool selfIntersect, List<int> uMultiplicities, List<int> vMultiplicities,
            List<double> uKnots, List<double> vKnots, string knotSpec, List<List<double>> weightsData, string name = "")
        {
            string key = $"RBSSurf_{uDegree}_{vDegree}_{controlPointsList.GetHashCode()}_{weightsData.GetHashCode()}_{name}";
            if(!_bSplineSurfaces.TryGetValue(key, out var rbssBase))
            {
                var rbss = new RationalBSplineSurface(name, uDegree, vDegree, controlPointsList, surfaceForm, uClosed, vClosed, selfIntersect, uMultiplicities, vMultiplicities, uKnots, vKnots, knotSpec, weightsData);
                _bSplineSurfaces[key] = rbss;
                return rbss;
            }
            return rbssBase as RationalBSplineSurface ?? new RationalBSplineSurface(name, uDegree, vDegree, controlPointsList, surfaceForm, uClosed, vClosed, selfIntersect, uMultiplicities, vMultiplicities, uKnots, vKnots, knotSpec, weightsData);
        }


        // --- Formatting Utilities ---
        public static string DoubleToString(double d)
        {
            if (Math.Abs(d) < 1e-12) return "0."; // Handle very small numbers as zero
            if (Math.Floor(d) == d && Math.Abs(d) < 1e10) return d.ToString("F1", CultureInfo.InvariantCulture); // Output "1.0" instead of "1"

            string s = d.ToString("G15", CultureInfo.InvariantCulture).ToUpperInvariant(); // Use G15 for precision
            if (s.Contains("E"))
            {
                // Ensure E has a + or - and exponent is at least one digit, though G15 usually handles this.
                // Example: 1.23E5 -> 1.23E+05 (STEP often expects this, though not strictly required by all viewers)
                // For now, standard G15 output is usually fine.
            }
            else if (!s.Contains("."))
            {
                s += "."; // Ensure non-scientific numbers have a decimal point
            }
            return s;
        }

        public static string BoolToString(bool b) => b ? ".T." : ".F.";

        public static string ObjToString(object o)
        {
            if (o == null) return "$";
            if (o is string s) return $"'{s.Replace("'", "''")}'";
            if (o is double d) return DoubleToString(d);
            if (o is float f) return DoubleToString(f); // Promote float to double for consistent formatting
            if (o is int i) return i.ToString();
            if (o is long l) return l.ToString();
            if (o is bool b) return BoolToString(b);
            if (o is StepEntity se) return se.ToString();
            if (o is AcisStepAnyEntity any) return any.ToString();
            if (o is StepEnumWrapper sew) return sew.ToString();
            if (o is Enum e) return $".{e.ToString().ToUpperInvariant()}.";
            if (o is IEnumerable<object> list) // Catches List<double>, List<CartesianPoint>, etc.
            {
                var sb = new StringBuilder("(");
                bool first = true;
                foreach (var item in list)
                {
                    if (!first) sb.Append(",");
                    sb.Append(ObjToString(item));
                    first = false;
                }
                sb.Append(")");
                return sb.ToString();
            }
            Logger.Warning($"StepConverterUtils.ObjToString: Unhandled object type {o.GetType().Name}. Using default ToString().");
            return o.ToString();
        }
    }
}
