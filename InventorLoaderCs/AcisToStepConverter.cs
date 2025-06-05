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
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        // Constructor taking individual coordinates
        public CartesianPoint(string name, double x, double y, double z) : base(name)
        {
            X = x; Y = y; Z = z;
        }
        // Keep constructor taking List<double> for compatibility if used elsewhere, or remove if not.
        public CartesianPoint(string name, List<double> coordinates) : base(name)
        {
            if (coordinates == null || coordinates.Count != 3) throw new ArgumentException("Coordinates list must contain 3 values.");
            X = coordinates[0]; Y = coordinates[1]; Z = coordinates[2];
        }
        public override string GetClassName() => "CARTESIAN_POINT";
        public override List<object> GetParameters() => new List<object> { Name, new List<double> { X, Y, Z } };
    }

    public class Direction : StepNamedEntity // DIRECTION
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        // Constructor taking individual ratios
        public Direction(string name, double x, double y, double z) : base(name)
        {
            // Ratios should ideally be normalized, but STEP doesn't strictly enforce for DIRECTION components.
            // Normalization is usually handled by the CreateDirection util.
            X = x; Y = y; Z = z;
        }
        // Keep constructor taking List<double> for compatibility or remove.
        public Direction(string name, List<double> ratios) : base(name)
        {
            if (ratios == null || ratios.Count != 3) throw new ArgumentException("Ratios list must contain 3 values.");
            X = ratios[0]; Y = ratios[1]; Z = ratios[2];
        }
        public override string GetClassName() => "DIRECTION";
        public override List<object> GetParameters() => new List<object> { Name, new List<double> { X, Y, Z } };
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
        public StepEnumWrapper CurveForm { get; set; }
        public bool ClosedCurve { get; set; } // LOGICAL in STEP, .T. .F.
        public bool SelfIntersect { get; set; } // LOGICAL in STEP
        public List<int> KnotMultiplicities { get; set; }
        public List<double> Knots { get; set; }
        public StepEnumWrapper KnotSpec { get; set; }

        public BSplineCurveWithKnots(string name, int degree, List<CartesianPoint> controlPoints,
                                     StepEnumWrapper form, bool closed, bool selfIntersect,
                                     List<int> mults, List<double> knots, StepEnumWrapper spec) : base(name)
        {
            Degree = degree; ControlPointsList = controlPoints; CurveForm = form;
            ClosedCurve = closed; SelfIntersect = selfIntersect; KnotMultiplicities = mults;
            Knots = knots; KnotSpec = spec;
        }
        public override string GetClassName() => "B_SPLINE_CURVE_WITH_KNOTS";
        public override List<object> GetParameters() => new List<object> { Name, Degree, ControlPointsList,
            CurveForm, ClosedCurve, SelfIntersect, KnotMultiplicities, Knots, KnotSpec };
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

    public class Circle : StepNamedEntity // CIRCLE
    {
        public Axis2Placement3D Position { get; set; }
        public double Radius { get; set; }
        public Circle(string name, Axis2Placement3D pos, double radius) : base(name)
        { Position = pos; Radius = radius; }
        public override string GetClassName() => "CIRCLE";
        public override List<object> GetParameters() => new List<object> { Name, Position, Radius };
    }

    public class Ellipse : StepNamedEntity // ELLIPSE
    {
        public Axis2Placement3D Position { get; set; }
        public double SemiAxis1 { get; set; } // Major radius
        public double SemiAxis2 { get; set; } // Minor radius
        public Ellipse(string name, Axis2Placement3D pos, double semiAxis1, double semiAxis2) : base(name)
        { Position = pos; SemiAxis1 = semiAxis1; SemiAxis2 = semiAxis2; }
        public override string GetClassName() => "ELLIPSE";
        public override List<object> GetParameters() => new List<object> { Name, Position, SemiAxis1, SemiAxis2 };
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
                                    StepEnumWrapper form, bool closed, bool selfIntersect,
                                    List<int> mults, List<double> knots, StepEnumWrapper spec,
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
        public StepEnumWrapper SurfaceForm { get; set; }
        public bool UClosed { get; set; }
        public bool VClosed { get; set; }
        public bool SelfIntersect { get; set; }
        public List<int> UMultiplicities { get; set; }
        public List<int> VMultiplicities { get; set; }
        public List<double> UKnots { get; set; }
        public List<double> VKnots { get; set; }
        public StepEnumWrapper KnotSpec { get; set; }

        public BSplineSurfaceWithKnots(string name, int uDegree, int vDegree, List<List<CartesianPoint>> controlPoints,
                                       StepEnumWrapper form, bool uClosed, bool vClosed, bool selfIntersect,
                                       List<int> uMults, List<int> vMults, List<double> uKnots, List<double> vKnots, StepEnumWrapper spec)
            : base(name)
        {
            UDegree = uDegree; VDegree = vDegree; ControlPointsList = controlPoints; SurfaceForm = form;
            UClosed = uClosed; VClosed = vClosed; SelfIntersect = selfIntersect;
            UMultiplicities = uMults; VMultiplicities = vMults; UKnots = uKnots; VKnots = vKnots; KnotSpec = spec;
        }

        public override string GetClassName() => "B_SPLINE_SURFACE_WITH_KNOTS";
        public override List<object> GetParameters() => new List<object> {
            Name, UDegree, VDegree, ControlPointsList, SurfaceForm,
            UClosed, VClosed, SelfIntersect,
            UMultiplicities, VMultiplicities, UKnots, VKnots, KnotSpec
        };
    }

    public class RationalBSplineSurface : BSplineSurfaceWithKnots // RATIONAL_B_SPLINE_SURFACE
    {
        public List<List<double>> WeightsData { get; set; } // Grid of weights

        public RationalBSplineSurface(string name, int uDegree, int vDegree, List<List<CartesianPoint>> controlPoints,
                                      StepEnumWrapper form, bool uClosed, bool vClosed, bool selfIntersect,
                                      List<int> uMults, List<int> vMults, List<double> uKnots, List<double> vKnots, StepEnumWrapper spec,
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

    // --- Style & Presentation Entities ---
    public class ColourRgb : StepNamedEntity // COLOUR_RGB (actually just COLOUR)
    {
        public double Red { get; set; }
        public double Green { get; set; }
        public double Blue { get; set; }
        public ColourRgb(string name, double r, double g, double b) : base(name)
        { Red = r; Green = g; Blue = b; } // Values 0-1 for STEP
        public override string GetClassName() => "COLOUR_RGB"; // This is the entity type name
        public override List<object> GetParameters() => new List<object> { Name, Red, Green, Blue };
    }

    public class SurfaceStyleFillArea : StepEntity // SURFACE_STYLE_FILL_AREA
    {
        public ColourRgb FillArea { get; set; }
        public SurfaceStyleFillArea(ColourRgb fillColour) : base() { FillArea = fillColour; }
        public override string GetClassName() => "SURFACE_STYLE_FILL_AREA";
        public override List<object> GetParameters() => new List<object> { FillArea };
    }

    public class SurfaceStyleUsage : StepEntity // SURFACE_STYLE_USAGE
    {
        public StepEnumWrapper Side { get; set; } // .BOTH., .POSITIVE., .NEGATIVE.
        public StepEntity Style { get; set; } // e.g. SurfaceStyleFillArea
        public SurfaceStyleUsage(StepEnumWrapper side, StepEntity style) : base()
        { Side = side; Style = style; }
        public override string GetClassName() => "SURFACE_STYLE_USAGE";
        public override List<object> GetParameters() => new List<object> { Side, Style };
    }

    public class PresentationStyleAssignment : StepEntity // PRESENTATION_STYLE_ASSIGNMENT
    {
        public List<StepEntity> Styles { get; set; } // List of (e.g. SurfaceStyleUsage)
        public PresentationStyleAssignment(List<StepEntity> styles) : base() { Styles = styles; }
        public override string GetClassName() => "PRESENTATION_STYLE_ASSIGNMENT";
        public override List<object> GetParameters() => new List<object> { Styles };
    }

    public class StyledItem : StepNamedEntity // STYLED_ITEM
    {
        public List<StepEntity> Styles { get; set; } // List of PresentationStyleAssignment
        public StepEntity Item { get; set; } // The item being styled (e.g., AdvancedFace)
        public StyledItem(string name, List<StepEntity> styles, StepEntity item) : base(name)
        { Styles = styles; Item = item; }
        public override string GetClassName() => "STYLED_ITEM";
        public override List<object> GetParameters() => new List<object> { Name, Styles, Item };
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
        private static Dictionary<string, EdgeLoop> _edgeLoops = new Dictionary<string, EdgeLoop>();
        private static Dictionary<string, FaceBound> _faceBounds = new Dictionary<string, FaceBound>();
        private static Dictionary<string, AdvancedFace> _advancedFaces = new Dictionary<string, AdvancedFace>();
        private static Dictionary<string, OpenShell> _openShells = new Dictionary<string, OpenShell>(); // Includes ClosedShell
        private static Dictionary<string, ManifoldSolidBRep> _manifoldSolidBReps = new Dictionary<string, ManifoldSolidBRep>();
        private static Dictionary<string, ShellBasedSurfaceModel> _shellBasedSurfaceModels = new Dictionary<string, ShellBasedSurfaceModel>();

        // Geometric entity caches
        private static Dictionary<string, Axis2Placement3D> _axis2Placements = new Dictionary<string, Axis2Placement3D>();
        private static Dictionary<string, Plane> _planes = new Dictionary<string, Plane>();
        private static Dictionary<string, Circle> _circles = new Dictionary<string, Circle>();
        private static Dictionary<string, Ellipse> _ellipses = new Dictionary<string, Ellipse>();
        private static Dictionary<string, CylindricalSurface> _cylindricalSurfaces = new Dictionary<string, CylindricalSurface>();
        private static Dictionary<string, ConicalSurface> _conicalSurfaces = new Dictionary<string, ConicalSurface>();
        private static Dictionary<string, SphericalSurface> _sphericalSurfaces = new Dictionary<string, SphericalSurface>();
        private static Dictionary<string, ToroidalSurface> _toroidalSurfaces = new Dictionary<string, ToroidalSurface>();
        private static Dictionary<string, BSplineCurveWithKnots> _bSplineCurves = new Dictionary<string, BSplineCurveWithKnots>();
        private static Dictionary<string, BSplineSurfaceWithKnots> _bSplineSurfaces = new Dictionary<string, BSplineSurfaceWithKnots>();

        // Style entity caches
        private static Dictionary<string, ColourRgb> _colourRgbs = new Dictionary<string, ColourRgb>();
        private static Dictionary<string, SurfaceStyleFillArea> _surfaceStyleFillAreas = new Dictionary<string, SurfaceStyleFillArea>();
        private static Dictionary<string, SurfaceStyleUsage> _surfaceStyleUsages = new Dictionary<string, SurfaceStyleUsage>();
        private static Dictionary<string, PresentationStyleAssignment> _presentationStyleAssignments = new Dictionary<string, PresentationStyleAssignment>();
        private static Dictionary<string, StyledItem> _styledItems = new Dictionary<string, StyledItem>();


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

            _planes.Clear();
            _circles.Clear();
            _ellipses.Clear();
            _conicalSurfaces.Clear();
            _sphericalSurfaces.Clear();
            _toroidalSurfaces.Clear();
            // Add new style caches
            _colourRgbs.Clear();
            _styledItems.Clear();
            _presentationStyleAssignments.Clear();
            _surfaceStyleUsages.Clear();
            _surfaceStyleFillAreas.Clear();
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
            if (acisModel != null && acisModel.Segments != null)
            {
                foreach (var segmentPair in acisModel.Segments)
                {
                    RSeSegment segment = segmentPair.Value;
                    if (segment.ParsedContent != null && segment.ParsedContent.TryGetValue("ACIS", out object acisReaderObj) && acisReaderObj is AcisReader acisReader)
                    {
                        Logger.Info($"Processing ACIS data from segment: {segment.Name}");
                        foreach (var record in acisReader.RecordsList)
                        {
                            if (record?.Entity == null) continue;

                            if (record.Entity is AcisPoint acisPoint)
                            {
                                CreateCartesianPoint(acisPoint.Position);
                            }
                            else if (record.Entity is CurveStraight cs)
                            {
                                CreateLine(cs);
                            }
                            else if (record.Entity is SurfacePlane sp)
                            {
                                CreatePlane(sp);
                            }
                            else if (record.Entity is AcisTransform at)
                            {
                                // Simplified: treat AcisTransform as defining a placement
                                // This requires extracting location, axis, refDir from Matrix4x4
                                // For now, creating a default placement if an AcisTransform is found
                                Vector3 location = at.Matrix.Translation;
                                Vector3 axis = Vector3.Normalize(new Vector3(at.Matrix.M13, at.Matrix.M23, at.Matrix.M33)); // Z-axis
                                Vector3 refDir = Vector3.Normalize(new Vector3(at.Matrix.M11, at.Matrix.M21, at.Matrix.M31)); // X-axis
                                CreateAxis2Placement3D(location, axis, refDir, $"PlacementForTransform{at.Index}");
                            }
                            // Add more types as needed
                        }
                    }
                }
            }
            else
            {
                 Logger.Warning("StepConverterUtils.Export: No ACIS model or segments provided for export. Creating default point.");
                 CreateCartesianPoint(new Vector3(10,20,30), "DefaultTestPointNoModel");
            }

            // Export all registered DATA entities that were created
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
        public static CartesianPoint CreateCartesianPoint(Vector3 vec, string name = "", bool useCache = true)
        {
            string key = $"CP_{vec.X:F8}_{vec.Y:F8}_{vec.Z:F8}"; // Increased precision for cache key
            if (useCache && _cartesianPoints.TryGetValue(key, out CartesianPoint cp))
            {
                return cp;
            }
            cp = new CartesianPoint(name, vec.X, vec.Y, vec.Z);
            if (useCache) _cartesianPoints[key] = cp;
            return cp;
        }

        public static Direction CreateDirection(Vector3 vec, string name = "", bool useCache = true)
        {
            var normalizedVec = vec.LengthSquared() > 1e-12f ? Vector3.Normalize(vec) : vec; // Avoid normalizing zero vector
            string key = $"DIR_{normalizedVec.X:F8}_{normalizedVec.Y:F8}_{normalizedVec.Z:F8}";
            if (useCache && _directions.TryGetValue(key, out Direction dir))
            {
                return dir;
            }
            dir = new Direction(name, normalizedVec.X, normalizedVec.Y, normalizedVec.Z);
            if (useCache) _directions[key] = dir;
            return dir;
        }

        public static Vector CreateVector(Direction orientation, double magnitude, string name = "", bool useCache = true)
        {
            string key = $"VEC_{orientation.Id}_{magnitude:F8}";
            if (useCache && !_vectors.TryGetValue(key, out var vec))
            {
                vec = new Vector(name, orientation, magnitude);
                if (useCache) _vectors[key] = vec;
                return vec;
            }
            // Fallback for no cache hit or if not using cache
            return _vectors.TryGetValue(key, out var cachedVec) ? cachedVec : new Vector(name, orientation, magnitude);
        }

        public static Line CreateLine(AcisCurveStraight acisLine, string name = "", bool useCache = true)
        {
            if (acisLine == null) return CreateDefaultLine(name);
            var pnt = CreateCartesianPoint(acisLine.Root, name + "_pnt", useCache);
            var dirVec = CreateDirection(acisLine.Dir, name + "_dir_orientation", useCache);
            // Assuming acisLine.Dir is a direction vector, its length is 1.
            // If acisLine.Dir was intended to store magnitude as well, that logic would need adjustment.
            // For a line, vector magnitude is implicitly infinite. STEP often uses unit vector for direction.
            // Let's create a STEP vector with magnitude 1.0 for normalized direction.
            var stepVec = CreateVector(dirVec, 1.0, name + "_vec", useCache);

            string key = $"LINE_{pnt.Id}_{stepVec.Id}";
            if (useCache && _lines.TryGetValue(key, out var line))
            {
                return line;
            }
            line = new Line(name, pnt, stepVec);
            if (useCache) _lines[key] = line;
            return line;
        }

        public static VertexPoint CreateVertexPoint(AcisVertex acisVertex, string name = "", bool useCache = true)
        {
            if (acisVertex?.PointEntity == null) return null; // Or throw/log
            var cp = CreateCartesianPoint(acisVertex.PointEntity.Position, name + "_geom", useCache);
            string key = $"VP_{cp.Id}";
            if (useCache && _vertexPoints.TryGetValue(key, out var vp))
            {
                return vp;
            }
            vp = new VertexPoint(name, cp);
            if (useCache) _vertexPoints[key] = vp;
            return vp;
        }

        public static EdgeCurve CreateEdgeCurve(AcisEdge acisEdge, string name = "", bool useCache = true)
        {
            if (acisEdge == null) return null;
            var startVp = CreateVertexPoint(acisEdge.StartVertex, name + "_start_v", useCache);
            var endVp = CreateVertexPoint(acisEdge.EndVertex, name + "_end_v", useCache);
            var curveGeom = CreateCurveGeometry(acisEdge.CurveEntity, name + "_curve", useCache);
            bool sense = acisEdge.Sense == SenseEnum.FORWARD || acisEdge.Sense == SenseEnum.UNKNOWN; // Default UNKNOWN to FORWARD for STEP

            string key = $"EC_{startVp?.Id}_{endVp?.Id}_{curveGeom?.Id}_{sense}";
             if (useCache && _edgeCurves.TryGetValue(key, out var ec))
            {
                return ec;
            }
            ec = new EdgeCurve(name, startVp, endVp, curveGeom, sense);
            if (useCache) _edgeCurves[key] = ec;
            return ec;
        }

        public static OrientedEdge CreateOrientedEdge(AcisCoEdge acisCoEdge, string name = "", bool useCache = true)
        {
            if (acisCoEdge?.EdgeEntity == null) return null;
            var edgeCurve = CreateEdgeCurve(acisCoEdge.EdgeEntity, name + "_edge", useCache);
            bool orientation = acisCoEdge.Sense == SenseEnum.FORWARD || acisCoEdge.Sense == SenseEnum.UNKNOWN;

            string key = $"OE_{edgeCurve?.Id}_{orientation}";
            if (useCache && _orientedEdges.TryGetValue(key, out var oe))
            {
                return oe;
            }
            oe = new OrientedEdge(name, edgeCurve, orientation);
            if (useCache) _orientedEdges[key] = oe;
            return oe;
        }

        public static EdgeLoop CreateEdgeLoop(AcisLoop acisLoop, string name = "", bool useCache = true)
        {
            if (acisLoop == null) return null;
            var orientedEdges = new List<OrientedEdge>();
            AcisCoEdge currentCoEdge = acisLoop.CoedgeEntity;
            if (currentCoEdge != null)
            {
                AcisCoEdge startCoEdge = currentCoEdge;
                do
                {
                    var oe = CreateOrientedEdge(currentCoEdge, name + $"_oe{orientedEdges.Count}", useCache);
                    if (oe != null) orientedEdges.Add(oe);
                    currentCoEdge = currentCoEdge.NextCoedge;
                } while (currentCoEdge != null && currentCoEdge != startCoEdge);
            }

            // Keying for lists can be complex. Using a simple count for now.
            string key = $"EL_{orientedEdges.Count}_{orientedEdges.FirstOrDefault()?.Id}_{name}";
            if (useCache && _edgeLoops.TryGetValue(key, out var el))
            {
                return el;
            }
            el = new EdgeLoop(name, orientedEdges);
            if (useCache) _edgeLoops[key] = el;
            return el;
        }

        public static FaceBound CreateFaceBound(AcisLoop acisLoop, bool orientation, string name = "", bool useCache = true)
        {
            var edgeLoop = CreateEdgeLoop(acisLoop, name + "_loop", useCache);
            string key = $"FB_{edgeLoop?.Id}_{orientation}";
            if (useCache && _faceBounds.TryGetValue(key, out var fb))
            {
                return fb;
            }
            fb = new FaceBound(name, edgeLoop, orientation);
            if (useCache) _faceBounds[key] = fb;
            return fb;
        }

        // CreateFaceOuterBound would be similar, just instantiating FaceOuterBound.

        public static Axis2Placement3D CreateAxis2Placement3D(Vector3 location, Vector3 axis, Vector3 refDir, string name = "", bool useCache = true)
        {
            var locPt = CreateCartesianPoint(location, name + "_loc", useCache);
            var axisDir = CreateDirection(axis, name + "_axis", useCache);
            var refDirection = CreateDirection(refDir, name + "_refdir", useCache);
            string key = $"A2P3D_{locPt.Id}_{axisDir.Id}_{refDirection.Id}";
            if (useCache && _axis2Placements.TryGetValue(key, out var a2p3d))
            {
                return a2p3d;
            }
            a2p3d = new Axis2Placement3D(name, locPt, axisDir, refDirection);
            if (useCache) _axis2Placements[key] = a2p3d;
            return a2p3d;
        }

        public static Plane CreatePlane(AcisSurfacePlane acisPlane, string name = "", bool useCache = true)
        {
            if (acisPlane == null) return null; // Or a default plane
            // Assuming acisPlane.Normal is the Z-axis, acisPlane.UvOrigin could define the X-axis direction relative to origin.
            // This needs a robust way to get an orthogonal X direction if UvOrigin is not just along X.
            // For now, a simplified assumption: UvOrigin gives a point on the plane, and we derive X axis.
            // A common way: if Normal is Z, X can be global X unless Normal is aligned with global X.
            Vector3 xAxisDirection = Vector3.Cross(Vector3.UnitY, acisPlane.Normal);
            if (xAxisDirection.LengthSquared() < 1e-9) xAxisDirection = Vector3.Cross(Vector3.UnitX, acisPlane.Normal);
            xAxisDirection = Vector3.Normalize(xAxisDirection);

            var position = CreateAxis2Placement3D(acisPlane.Root, acisPlane.Normal, xAxisDirection, name + "_pos", useCache);
            string key = $"PLANE_{position.Id}";
            if (useCache && _planes.TryGetValue(key, out var plane))
            {
                return plane;
            }
            plane = new Plane(name, position);
            if (useCache) _planes[key] = plane;
            return plane;
        }

        public static Circle CreateCircle(AcisCurveEllipse acisEllipse, string name = "", bool useCache = true)
        {
            if (acisEllipse == null || Math.Abs(acisEllipse.Ratio - 1.0) > 1e-6) return null; // Not a circle

            // Axis is Z, MajorAxisPoint helps define X. Location is Center.
            var position = CreateAxis2Placement3D(acisEllipse.Center, acisEllipse.Axis, Vector3.Normalize(acisEllipse.MajorAxisPoint - acisEllipse.Center), name + "_pos", useCache);
            double radius = (acisEllipse.MajorAxisPoint - acisEllipse.Center).Length();

            string key = $"CIRCLE_{position.Id}_{radius:F8}";
            if (useCache && _circles.TryGetValue(key, out var circle))
            {
                return circle;
            }
            circle = new Circle(name, position, radius);
            if (useCache) _circles[key] = circle;
            return circle;
        }

        public static Ellipse CreateEllipse(AcisCurveEllipse acisEllipse, string name = "", bool useCache = true)
        {
            if (acisEllipse == null) return null;
            var position = CreateAxis2Placement3D(acisEllipse.Center, acisEllipse.Axis, Vector3.Normalize(acisEllipse.MajorAxisPoint - acisEllipse.Center), name + "_pos", useCache);
            double semiAxis1 = (acisEllipse.MajorAxisPoint - acisEllipse.Center).Length();
            double semiAxis2 = semiAxis1 * acisEllipse.Ratio;

            string key = $"ELLIPSE_{position.Id}_{semiAxis1:F8}_{semiAxis2:F8}";
            if (useCache && _ellipses.TryGetValue(key, out var ellipse))
            {
                return ellipse;
            }
            ellipse = new Ellipse(name, position, semiAxis1, semiAxis2);
            if (useCache) _ellipses[key] = ellipse;
            return ellipse;
        }

        public static ConicalSurface CreateConicalSurface(AcisSurfaceCone acisCone, string name = "", bool useCache = true)
        {
            if (acisCone == null) return null;
            // RefDir: project RefAxisPoint onto plane normal to Axis, vector from Center to this projection.
            Vector3 axisNorm = Vector3.Normalize(acisCone.Axis);
            Vector3 centerToRefPt = acisCone.RefAxisPoint - acisCone.Center;
            Vector3 refDir = Vector3.Normalize(centerToRefPt - Vector3.Dot(centerToRefPt, axisNorm) * axisNorm);
            if (refDir.LengthSquared() < 1e-9) refDir = AcisUtils.GetArbitraryAxis(axisNorm); // Fallback if ref point on axis

            var placement = CreateAxis2Placement3D(acisCone.Center, acisCone.Axis, refDir, name + "_pos", useCache);
            double radius = acisCone.RadiusAtCenter; // Radius at Z=0 of placement
            double semiAngle = Math.Asin(acisCone.SineAngle); // SineAngle is sin of half angle

            string key = $"CONICALSF_{placement.Id}_{radius:F8}_{semiAngle:F8}";
            if (useCache && _conicalSurfaces.TryGetValue(key, out var conSurf))
            {
                return conSurf;
            }
            conSurf = new ConicalSurface(name, placement, radius, semiAngle);
            if (useCache) _conicalSurfaces[key] = conSurf;
            return conSurf;
        }

        public static CylindricalSurface CreateCylindricalSurfaceFromCone(AcisSurfaceCone acisConeAsCylinder, string name = "", bool useCache = true)
        {
            if (acisConeAsCylinder == null) return null;
             Vector3 axisNorm = Vector3.Normalize(acisConeAsCylinder.Axis);
            Vector3 centerToRefPt = acisConeAsCylinder.RefAxisPoint - acisConeAsCylinder.Center;
            Vector3 refDir = Vector3.Normalize(centerToRefPt - Vector3.Dot(centerToRefPt, axisNorm) * axisNorm);
            if (refDir.LengthSquared() < 1e-9) refDir = AcisUtils.GetArbitraryAxis(axisNorm);

            var placement = CreateAxis2Placement3D(acisConeAsCylinder.Center, acisConeAsCylinder.Axis, refDir, name + "_pos", useCache);
            double radius = acisConeAsCylinder.RadiusAtCenter;

            string key = $"CYLINDRICALSF_{placement.Id}_{radius:F8}";
            if (useCache && _cylindricalSurfaces.TryGetValue(key, out var cylSurf))
            {
                return cylSurf;
            }
            cylSurf = new CylindricalSurface(name, placement, radius);
            if (useCache) _cylindricalSurfaces[key] = cylSurf;
            return cylSurf;
        }


        public static SphericalSurface CreateSphericalSurface(AcisSurfaceSphere acisSphere, string name = "", bool useCache = true)
        {
            if (acisSphere == null) return null;
            // Axis is Pole (Z), RefDirection is UvOrigin (X)
            var placement = CreateAxis2Placement3D(acisSphere.Center, acisSphere.Pole, acisSphere.UvOrigin, name + "_pos", useCache);

            string key = $"SPHERICALSF_{placement.Id}_{acisSphere.Radius:F8}";
            if (useCache && _sphericalSurfaces.TryGetValue(key, out var sphSurf))
            {
                return sphSurf;
            }
            sphSurf = new SphericalSurface(name, placement, acisSphere.Radius);
            if (useCache) _sphericalSurfaces[key] = sphSurf;
            return sphSurf;
        }

        public static ToroidalSurface CreateToroidalSurface(AcisSurfaceTorus acisTorus, string name = "", bool useCache = true)
        {
            if (acisTorus == null) return null;
            // Axis is Z, RefDirection derived from UvOriginPoint
            Vector3 refDir = Vector3.Normalize(acisTorus.UvOriginPoint - acisTorus.Center);
             if (refDir.LengthSquared() < 1e-9) refDir = AcisUtils.GetArbitraryAxis(acisTorus.Axis);


            var placement = CreateAxis2Placement3D(acisTorus.Center, acisTorus.Axis, refDir, name + "_pos", useCache);

            string key = $"TOROIDALSF_{placement.Id}_{acisTorus.MajorRadius:F8}_{acisTorus.MinorRadius:F8}";
            if (useCache && _toroidalSurfaces.TryGetValue(key, out var torSurf))
            {
                return torSurf;
            }
            torSurf = new ToroidalSurface(name, placement, acisTorus.MajorRadius, acisTorus.MinorRadius);
            if (useCache) _toroidalSurfaces[key] = torSurf;
            return torSurf;
        }

        public static StepEntity CreateBSplineCurveGeometry(BSCurveData splineData, string name = "", bool useCache = true)
        {
            if (splineData == null) return null;

            List<CartesianPoint> controlPoints = splineData.Poles3D.Select((p, i) => CreateCartesianPoint(p, $"{name}_cp{i}", useCache)).ToList();

            // Default STEP enums
            StepEnumWrapper curveForm = new StepEnumWrapper("UNSPECIFIED_FORM"); // Or determine from data if possible
            StepEnumWrapper knotSpec =  new StepEnumWrapper("UNSPECIFIED");
            bool closedCurve = splineData.IsPeriodic; // Or specific logic for .T./.F.
            bool selfIntersect = false; // Default to .F. unless known

            string key = $"BSPLCF_{splineData.Degree}_{controlPoints.GetHashCode()}_{splineData.Knots.GetHashCode()}_{splineData.Multiplicities.GetHashCode()}_{splineData.IsRational}";

            if (useCache && _bSplineCurves.TryGetValue(key, out var existingCurve)) return existingCurve;

            if (splineData.IsRational)
            {
                var rationalCurve = new RationalBSplineCurve(name, splineData.Degree, controlPoints, curveForm, closedCurve, selfIntersect,
                                                          splineData.Multiplicities, splineData.Knots, knotSpec, splineData.Weights);
                if(useCache) _bSplineCurves[key] = rationalCurve;
                return rationalCurve;
            }
            else
            {
                var curve = new BSplineCurveWithKnots(name, splineData.Degree, controlPoints, curveForm, closedCurve, selfIntersect,
                                                      splineData.Multiplicities, splineData.Knots, knotSpec);
                if(useCache) _bSplineCurves[key] = curve;
                return curve;
            }
        }

        public static StepEntity CreateBSplineSurfaceGeometry(BSSurfaceData splineData, string name = "", bool useCache = true)
        {
            if (splineData == null) return null;

            List<List<CartesianPoint>> controlPointGrid = new List<List<CartesianPoint>>();
            for(int i=0; i < splineData.Poles.Count; i++)
            {
                var row = splineData.Poles[i];
                controlPointGrid.Add(row.Select((p, j) => CreateCartesianPoint(p, $"{name}_cp{i}_{j}", useCache)).ToList());
            }

            StepEnumWrapper surfaceForm = new StepEnumWrapper("UNSPECIFIED_FORM");
            StepEnumWrapper knotSpec = new StepEnumWrapper("UNSPECIFIED");
            bool uClosed = splineData.UPeriodic;
            bool vClosed = splineData.VPeriodic;
            bool selfIntersect = false; // Default

            string key = $"BSPLSF_{splineData.UDegree}_{splineData.VDegree}_{controlPointGrid.GetHashCode()}_{splineData.UKnots.GetHashCode()}_{splineData.VKnots.GetHashCode()}_{splineData.URational}";
            if(useCache && _bSplineSurfaces.TryGetValue(key, out var existingSurf)) return existingSurf;

            if (splineData.URational) // Assuming URational implies VRational for simplicity, matching BSSurfaceData
            {
                var rationalSurface = new RationalBSplineSurface(name, splineData.UDegree, splineData.VDegree, controlPointGrid, surfaceForm,
                                                               uClosed, vClosed, selfIntersect, splineData.UMultiplicities, splineData.VMultiplicities,
                                                               splineData.UKnots, splineData.VKnots, knotSpec, splineData.Weights);
                if(useCache) _bSplineSurfaces[key] = rationalSurface;
                return rationalSurface;
            }
            else
            {
                var surface = new BSplineSurfaceWithKnots(name, splineData.UDegree, splineData.VDegree, controlPointGrid, surfaceForm,
                                                          uClosed, vClosed, selfIntersect, splineData.UMultiplicities, splineData.VMultiplicities,
                                                          splineData.UKnots, splineData.VKnots, knotSpec);
                if(useCache) _bSplineSurfaces[key] = surface;
                return surface;
            }
        }


        private static Line CreateDefaultLine(string name = "DefaultLine")
        {
            var p0 = CreateCartesianPoint(Vector3.Zero, name + "_p0", false);
            var p1 = CreateCartesianPoint(Vector3.UnitX, name + "_p1", false);
            var dir = CreateDirection(Vector3.UnitX, name + "_dir", false);
            var vec = CreateVector(dir, 1.0, name + "_vec", false);
            return new Line(name, p0, vec); // Not cached by default
        }

        public static StepEntity CreateCurveGeometry(AcisCurve acisCurve, string name = "", bool useCache = true)
        {
            if (acisCurve == null) return null;
            switch (acisCurve)
            {
                case CurveStraight cs:
                    return CreateLine(cs, name, useCache);
                case CurveEllipse ce:
                    if (Math.Abs(ce.Ratio - 1.0) < 1e-6)
                        return CreateCircle(ce, name, useCache);
                    else
                        return CreateEllipse(ce, name, useCache);
                case CurveInt ci when ci.SplineGeometricData != null:
                    return CreateBSplineCurveGeometry(ci.SplineGeometricData, name, useCache);
                case CurveInt ci: // Fallback for CurveInt without direct spline data (e.g. law, helix)
                     Logger.Warning($"STEP conversion for specific CurveInt subtype '{ci.CurveSubtypeString}' (e.g. law, helix) not directly mapped to a simple STEP curve. Using default line for '{name}'.");
                    return CreateDefaultLine(name + "_UnsupportedIntCurveComplex");
                default:
                    Logger.Warning($"Unknown AcisCurve type '{acisCurve.GetType().FullName}' for STEP conversion. Using default line for '{name}'.");
                    return CreateDefaultLine(name + "_UnknownCurveType");
            }
        }

        public static Surface CreateSurfaceGeometry(AcisSurface acisSurface, string name = "", bool useCache = true)
        {
            if (acisSurface == null) return null;
            switch (acisSurface)
            {
                case SurfacePlane sp:
                    return CreatePlane(sp, name, useCache);
                case SurfaceCone sc:
                    return Math.Abs(sc.SineAngle) < 1e-9 ?
                           CreateCylindricalSurfaceFromCone(sc, name, useCache) :
                           CreateConicalSurface(sc, name, useCache);
                case SurfaceSphere ss:
                    return CreateSphericalSurface(ss, name, useCache);
                case SurfaceTorus st:
                    return CreateToroidalSurface(st, name, useCache);
                case SurfaceSpline sspl when sspl.SplineGeometricData != null:
                    return CreateBSplineSurfaceGeometry(sspl.SplineGeometricData, name, useCache) as Surface; // Ensure cast if Create returns StepEntity
                default:
                    Logger.Warning($"Unknown AcisSurface type '{acisSurface.GetType().FullName}' for STEP conversion. Returning null for '{name}'.");
                    return null; // Or return a default plane: CreatePlane(new AcisSurfacePlane(), name + "_default");
            }
        }

        public static Axis2Placement3D CreateAxis2Placement3D(Vector3 location, Vector3 axis, Vector3 refDir, string name = "", bool useCache = true)
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
