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

        public override string GetClassName()
        {
            // This is a base implementation.
            // Specific derived classes should override this to return their actual STEP entity type name.
            // For example, a 'CartesianPoint' class derived from StepNamedEntity
            // would override this to return "CARTESIAN_POINT".
            // Returning the C# class name uppercase is a fallback.
            return GetType().Name.ToUpperInvariant();
        }
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

    // Helper for optional parameters in STEP ($)
    public struct StepDollarNotApplicable
    {
        public override string ToString() => "$";
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

    // Adjusted ShellBasedSurfaceModel to inherit from Representation
    public class ShellBasedSurfaceModel : Representation // SHELL_BASED_SURFACE_MODEL
    {
        // SbsmElements are the 'Items' of this representation.
        // The constructor will pass 'elements' (cast to List<StepEntity>) to the base Representation constructor.
        public ShellBasedSurfaceModel(string name, List<OpenShell> elements, RepresentationContext context = null)
            : base(name, context, elements?.Cast<StepEntity>().ToList())
        {
            // Items are set in the base class. ContextOfItems is also set in base, can be null if not provided.
            // If context is null here, a default might be needed or handled by the caller.
            // For now, allowing null context to be passed to base.
        }
        public override string GetClassName() => "SHELL_BASED_SURFACE_MODEL";
        // Parameters: (Name, Items_List, Context_Of_Items)
        public override List<object> GetParameters() => new List<object> { Name, Items, ContextOfItems };
    }

    // --- Assembly Related STEP Entities ---
    public class RepresentationMap : StepNamedEntity
    {
        public Axis2Placement3D MappingOrigin { get; } // Defines the placement of the mapped representation
        public StepEntity MappedRepresentation { get; } // The canonical representation being mapped

        public RepresentationMap(string name, Axis2Placement3D mappingOrigin, StepEntity mappedRepresentation) : base(name)
        {
            MappingOrigin = mappingOrigin;
            MappedRepresentation = mappedRepresentation;
        }
        public override string GetClassName() => "REPRESENTATION_MAP";
        // Standard REPRESENTATION_MAP parameters are (mapping_origin, mapped_representation). Name is implicit.
        public override List<object> GetParameters() => new List<object> { MappingOrigin, MappedRepresentation };
    }

    public class MappedItem : StepNamedEntity // MAPPED_ITEM
    {
        public RepresentationMap MappingSource { get; } // How the item is mapped (transform + canonical rep)
        public ShapeDefinitionRepresentation MappingTarget { get; } // What canonical part definition this item is an instance of

        public MappedItem(string name, RepresentationMap mappingSource, ShapeDefinitionRepresentation mappingTarget) : base(name)
        {
            MappingSource = mappingSource;
            MappingTarget = mappingTarget;
        }
        public override string GetClassName() => "MAPPED_ITEM";
        public override List<object> GetParameters() => new List<object> { Name, MappingSource, MappingTarget };
    }

    public class AssemblyShapeRepresentation : Representation
    {
        // Items list (containing MappedItems or direct ShapeDefinitionRepresentations of sub-assemblies/parts) is inherited.
        // ContextOfItems is also inherited.
        public AssemblyShapeRepresentation(string name, List<StepEntity> items, RepresentationContext context)
            : base(name, context, items)
        {
        }
        // Using a common generic representation type. AP214/AP242 might use more specific ones like
        // 'MECHANICAL_DESIGN_GEOMETRIC_PRESENTATION_REPRESENTATION' or similar.
        public override string GetClassName() => "SHAPE_REPRESENTATION";
        public override List<object> GetParameters() => new List<object> { Name, Items, ContextOfItems };
    }

    // --- Product Structure STEP Entities (NAUO) ---
    public class NextAssemblyUsageOccurrence : StepNamedEntity
    {
        public string NauoId { get; } // Instance ID
        public string Description { get; }
        public ProductDefinition RelatingProductDefinition { get; } // Assembly definition
        public ProductDefinition RelatedProductDefinition { get; }  // Component definition
        public string ReferenceDesignator { get; } // Optional

        public NextAssemblyUsageOccurrence(string name, string id, string description,
                                           ProductDefinition relatingPd, ProductDefinition relatedPd,
                                           string referenceDesignator = "") : base(name)
        {
            NauoId = id;
            Description = description;
            RelatingProductDefinition = relatingPd;
            RelatedProductDefinition = relatedPd;
            ReferenceDesignator = string.IsNullOrEmpty(referenceDesignator) ? "$" : $"'{referenceDesignator}'";
        }

        public override string GetClassName() => "NEXT_ASSEMBLY_USAGE_OCCURRENCE";

        // Parameters: ID, Name, Description, Relating_PD, Related_PD, Reference_Designator (optional)
        public override List<object> GetParameters() => new List<object> {
            NauoId, Name, Description,
            RelatingProductDefinition, RelatedProductDefinition,
            ReferenceDesignator // Already formatted with quotes or $
        };
    }

    public class ContextDependentShapeRepresentation : StepNamedEntity
    {
        public ShapeDefinitionRepresentation RepresentationRelation; // Link to the SDR this CDSR contextualizes
        public ProductDefinitionShape RepresentedProductDefinitionShape; // Link to the PDS of the component part

        public ContextDependentShapeRepresentation(string name,
                                                   ShapeDefinitionRepresentation representationToContextualize,
                                                   ProductDefinitionShape representedProductDefinitionShape) : base(name)
        {
            RepresentationRelation = representationToContextualize;
            RepresentedProductDefinitionShape = representedProductDefinitionShape;
        }

        public override string GetClassName() => "CONTEXT_DEPENDENT_SHAPE_REPRESENTATION";

        // Parameters: Name (implicit), RepresentationRelation, RepresentedProductDefinitionShape
        // The actual STEP entity is (CONTEXT_OF_REPRESENTATION, REPRESENTATION_RELATIONSHIP)
        // CONTEXT_OF_REPRESENTATION is the ProductDefinitionShape (or similar)
        // REPRESENTATION_RELATIONSHIP contains the name, description, rep_1, rep_2
        // This simplified C# class aims to capture the core links for AP203/AP214 usage.
        // A more precise mapping might involve a SHAPE_REPRESENTATION_RELATIONSHIP entity.
        // For now, assuming parameters are (Name, ContextOfShape (PDS), ShapeRepresentationInContext (SDR))
        // Let's adjust based on typical AP214 usage:
        // CONTEXT_DEPENDENT_SHAPE_REPRESENTATION(SHAPE_REPRESENTATION_RELATIONSHIP_WITH_TRANSFORMATION(), REPRESENTED_PRODUCT_RELATION.PRODUCT_DESIGN_VERSION);
        // This suggests the first parameter is more complex.
        // Simpler usage for now: (Representation To Contextualize, ProductDefinitionShape of component)
        // The 'Name' from StepNamedEntity is often not directly part of CDSR parameters.
        // CDSR links a specific representation use (like a MAPPED_ITEM via an SDR) to a defining PDS.
        public override List<object> GetParameters() => new List<object> {
            RepresentationRelation, // The SDR that holds the MAPPED_ITEM or canonical shape for this instance
            RepresentedProductDefinitionShape // The PDS of the component part definition
        };
        // Name property from StepNamedEntity will be part of the base class export if that class includes it.
        // For CDSR, the name is often empty or context-specific and might not be a direct parameter in the simplest form.
        // Let's assume GetParameters() should list only the specific parameters of CDSR itself.
    }


    // --- Style & Presentation Entities ---

    public class CurveStyle : StepNamedEntity // CURVE_STYLE
    {
        public ColourRgb CurveColour { get; }
        public object CurveWidth { get; } // Can be MeasureWithUnit (double for now) or StepDollarNotApplicable

        // Simplified constructor: curve_font is often optional and complex.
        // For now, only color and width.
        public CurveStyle(string name, ColourRgb curveColour, object curveWidth) : base(name)
        {
            CurveColour = curveColour;
            CurveWidth = curveWidth; // Store as object, ObjToString will handle $ or double
        }

        public override string GetClassName() => "CURVE_STYLE";

        // Parameters: Name, CurveFont (omitted for now, so $), CurveColour, CurveWidth
        public override List<object> GetParameters() => new List<object> {
            Name,
            new StepDollarNotApplicable(), // Placeholder for CURVE_FONT
            CurveColour,
            CurveWidth
        };
    }

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

    // --- Product Definition STEP Entities ---
    public class ApplicationContext : StepNamedEntity
    {
        public string DescriptionAttribute { get; } // Renamed to avoid conflict with base Name
        public ApplicationContext(string name, string description) : base(name) { DescriptionAttribute = description; }
        public override string GetClassName() => "APPLICATION_CONTEXT";
        public override List<object> GetParameters() => new List<object> { DescriptionAttribute }; // Name is implicit
    }

    public class ProductContext : StepNamedEntity
    {
        public ApplicationContext FrameOfReference { get; }
        public string DisciplineType { get; }
        public ProductContext(string name, ApplicationContext frame, string disciplineType) : base(name)
        {
            FrameOfReference = frame;
            DisciplineType = disciplineType;
        }
        public override string GetClassName() => "PRODUCT_CONTEXT";
        public override List<object> GetParameters() => new List<object> { Name, FrameOfReference, DisciplineType };
    }

    public class Product : StepNamedEntity
    {
        public string ProductId { get; }
        public string Description { get; }
        public List<ProductContext> FrameOfReference { get; } // List of ProductContext
        public Product(string name, string id, string description, List<ProductContext> frameOfReference) : base(name)
        {
            ProductId = id;
            Description = description;
            FrameOfReference = frameOfReference ?? new List<ProductContext>();
        }
        public override string GetClassName() => "PRODUCT";
        public override List<object> GetParameters() => new List<object> { ProductId, Name, Description, FrameOfReference };
    }

    public class ProductDefinitionFormation : StepNamedEntity
    {
        public string FormationId { get; } // Changed from ProductId to avoid confusion, often product.id
        public Product OfProduct { get; }
        public ProductDefinitionFormation(string name, string id, Product product) : base(name)
        {
            FormationId = id;
            OfProduct = product;
        }
        public override string GetClassName() => "PRODUCT_DEFINITION_FORMATION";
        public override List<object> GetParameters() => new List<object> { FormationId, Name, OfProduct }; // Name is description here
    }

    public class ProductDefinitionContext : StepNamedEntity
    {
        public ApplicationContext FrameOfReference { get; }
        public string LifeCycleStage { get; }
        public ProductDefinitionContext(string name, ApplicationContext frame, string lifeCycleStage) : base(name)
        {
            FrameOfReference = frame;
            LifeCycleStage = lifeCycleStage;
        }
        public override string GetClassName() => "PRODUCT_DEFINITION_CONTEXT";
        public override List<object> GetParameters() => new List<object> { Name, FrameOfReference, LifeCycleStage };
    }

    public class ProductDefinition : StepNamedEntity
    {
        public string DefinitionId { get; }
        public ProductDefinitionFormation Formation { get; }
        public ProductDefinitionContext FrameOfReference { get; } // Context of this definition
        public ProductDefinition(string name, string id, ProductDefinitionFormation formation, ProductDefinitionContext frame) : base(name)
        {
            DefinitionId = id;
            Formation = formation;
            FrameOfReference = frame;
        }
        public override string GetClassName() => "PRODUCT_DEFINITION";
        public override List<object> GetParameters() => new List<object> { DefinitionId, Name, Formation, FrameOfReference };
    }

    public class ProductDefinitionShape : StepNamedEntity
    {
        public string Description { get; }
        public ProductDefinition DefinitionOfShape { get; }
        public ProductDefinitionShape(string name, string description, ProductDefinition definitionOfShape) : base(name)
        {
            Description = string.IsNullOrEmpty(description) ? "Shape Definition" : description; // Ensure description is not null for STEP
            DefinitionOfShape = definitionOfShape;
        }
        public override string GetClassName() => "PRODUCT_DEFINITION_SHAPE";
        public override List<object> GetParameters() => new List<object> { Name, Description, DefinitionOfShape };
    }

    public abstract class Representation : StepNamedEntity // Abstract base for different representations
    {
        public RepresentationContext ContextOfItems { get; set; }
        public List<StepEntity> Items { get; protected set; } // Items that make up the representation

        protected Representation(string name, RepresentationContext context, List<StepEntity> items = null) : base(name)
        {
            ContextOfItems = context;
            Items = items ?? new List<StepEntity>();
        }
        // GetParameters in derived classes will need to include ContextOfItems and Items.
    }

    public class RepresentationContext : StepNamedEntity // Minimal placeholder
    {
        public string ContextType { get; set; } // e.g., 'GM' for Geometric Model
        public RepresentationContext(string name, string contextType) : base(name)
        {
            ContextType = contextType;
        }
        public override string GetClassName() => "REPRESENTATION_CONTEXT";
        public override List<object> GetParameters() => new List<object> { Name, ContextType };
    }

    // Adjust ShellBasedSurfaceModel to inherit from Representation
    // public class ShellBasedSurfaceModel : Representation ... (will adjust this later where SBSM is defined)


    public class ShapeDefinitionRepresentation : StepNamedEntity
    {
        public ProductDefinitionShape Definition { get; }
        public StepEntity UsedRepresentation { get; set; } // Changed to StepEntity to allow ShellBasedSurfaceModel etc.
        public ShapeDefinitionRepresentation(string name, ProductDefinitionShape definition, StepEntity usedRepresentation) : base(name)
        {
            Definition = definition;
            UsedRepresentation = usedRepresentation;
        }
        public override string GetClassName() => "SHAPE_DEFINITION_REPRESENTATION";
        public override List<object> GetParameters() => new List<object> { Definition, UsedRepresentation };
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
        private static Dictionary<string, CurveStyle> _curveStyles = new Dictionary<string, CurveStyle>(); // New cache for CurveStyle

        // Product definition entity caches
        private static Dictionary<string, ApplicationContext> _applicationContexts = new Dictionary<string, ApplicationContext>();
        private static Dictionary<string, ProductContext> _productContexts = new Dictionary<string, ProductContext>();
        private static Dictionary<string, Product> _products = new Dictionary<string, Product>();
        private static Dictionary<string, ProductDefinitionFormation> _productDefinitionFormations = new Dictionary<string, ProductDefinitionFormation>();
        private static Dictionary<string, ProductDefinitionContext> _productDefinitionContexts = new Dictionary<string, ProductDefinitionContext>();
        private static Dictionary<string, ProductDefinition> _productDefinitions = new Dictionary<string, ProductDefinition>();
        private static Dictionary<string, ProductDefinitionShape> _productDefinitionShapes = new Dictionary<string, ProductDefinitionShape>();
        private static Dictionary<string, ShapeDefinitionRepresentation> _shapeDefinitionRepresentations = new Dictionary<string, ShapeDefinitionRepresentation>();

        // Assembly entity caches
        private static Dictionary<string, RepresentationContext> _representationContexts = new Dictionary<string, RepresentationContext>();
        private static Dictionary<string, RepresentationMap> _representationMaps = new Dictionary<string, RepresentationMap>();
        private static Dictionary<string, MappedItem> _mappedItems = new Dictionary<string, MappedItem>();
        private static Dictionary<string, AssemblyShapeRepresentation> _assemblyShapeRepresentations = new Dictionary<string, AssemblyShapeRepresentation>();
        private static Dictionary<string, NextAssemblyUsageOccurrence> _nauos = new Dictionary<string, NextAssemblyUsageOccurrence>();
        private static Dictionary<string, ContextDependentShapeRepresentation> _cdsr = new Dictionary<string, ContextDependentShapeRepresentation>();

        // Caches for MSB/SBSM and their shells (OpenShell/ClosedShell are already cached via _openShells)
        // ManifoldSolidBRep and ShellBasedSurfaceModel are types of Representation, which are not typically cached by geometric key.
        // Their uniqueness comes from their constituent shells and context.
        // However, if specific instances need to be reused by ID, they could be cached.
        // For now, no new specific caches for MSB/SBSM, they are registered like other StepEntities.
        // OpenShell and ClosedShell will be cached by their factory methods if needed.
        // The existing _openShells can serve for both OpenShell and ClosedShell as ClosedShell derives from OpenShell.


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
            _curveStyles.Clear(); // Clear new cache

            // Clear product definition caches
            _applicationContexts.Clear();
            _productContexts.Clear();
            _products.Clear();
            _productDefinitionFormations.Clear();
            _productDefinitionContexts.Clear();
            _productDefinitions.Clear();
            _productDefinitionShapes.Clear();
            _shapeDefinitionRepresentations.Clear();

            // Clear assembly entity caches
            _representationContexts.Clear();
            _representationMaps.Clear();
            _mappedItems.Clear();
            _assemblyShapeRepresentations.Clear();
            _nauos.Clear();
            _cdsr.Clear();
            // No new specific caches for MSB/SBSM to clear here, _openShells covers shells.
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

            // --- Global Contexts ---
            var appContext = CreateApplicationContext("Product definition schema for ACIS models via InventorLoaderCs", "AppContext");
            var defaultRepContext = CreateRepresentationContext("GM", "DefaultGeomContext");


            // --- Simulate and Prepare iProperties for Product Description ---
            // Simulate iProperties for testing if not already populated by a reader
            if (acisModel.iProperties == null)
                acisModel.iProperties = new Dictionary<string, Dictionary<object, Tuple<string, object>>>();

            if (!acisModel.iProperties.ContainsKey("Inventor Summary Information"))
            {
                var sumInfo = new Dictionary<object, Tuple<string, object>>();
                sumInfo[4] = new Tuple<string, object>("Author", "AcisToStep C# Importer"); // Key 4 for Author
                sumInfo[6] = new Tuple<string, object>("Comments", "File processed by custom C# ACIS to STEP converter."); // Key 6 for Comments
                acisModel.iProperties["Inventor Summary Information"] = sumInfo;
            }
            if (!acisModel.iProperties.ContainsKey("Design Tracking Properties"))
            {
                var desProps = new Dictionary<object, Tuple<string, object>>();
                desProps[5] = new Tuple<string, object>("Part Number", Path.GetFileNameWithoutExtension(originalFileName) + "-ASSY"); // Key 5 for PartNumber
                desProps[29] = new Tuple<string, object>("Description", "Assembly top level product."); // Key 29 for Description
                acisModel.iProperties["Design Tracking Properties"] = desProps;
            }

            var sbDescription = new System.Text.StringBuilder();
            if (acisModel.iProperties.TryGetValue("Inventor Summary Information", out var summaryInfo))
            {
                if (summaryInfo.TryGetValue(4, out var authorTuple)) sbDescription.Append($"Author: {authorTuple.Item2}; ");
                if (summaryInfo.TryGetValue(6, out var commentsTuple)) sbDescription.Append($"Comments: {commentsTuple.Item2}; ");
            }
            if (acisModel.iProperties.TryGetValue("Design Tracking Properties", out var designProps))
            {
                if (designProps.TryGetValue(5, out var partNoTuple)) sbDescription.Append($"PartNo: {partNoTuple.Item2}; ");
                // Using key 29 for "Description" from iProperties for the product description field itself is redundant
                // if we are constructing a summary. Let's pick another relevant one or keep it concise.
                // if (designProps.TryGetValue(29, out var fileDescTuple)) sbDescription.Append($"FileDesc: {fileDescTuple.Item2}; ");
            }
            string productDescription = sbDescription.ToString().Trim();
            if (string.IsNullOrEmpty(productDescription)) productDescription = "No description available.";
            // Ensure description length is within typical STEP limits (e.g., 256 or 4000 chars, though PRODUCT description is usually shorter)
            if (productDescription.Length > 250) productDescription = productDescription.Substring(0, 250) + "...";


            // --- Product Definitions (Loop 1) ---
            // Store: Product, ProductDefinition, ProductDefinitionShape, CanonicalSDR, CanonicalShapeContent
            var bodyToProductDataMap = new Dictionary<AcisBody, (Product Product, ProductDefinition ProdDef, ProductDefinitionShape PDS, ShapeDefinitionRepresentation CanonicalSDR, StepEntity CanonicalShapeContent)>();

            List<AcisBody> allAcisBodies = new List<AcisBody>();
            if (acisModel != null && acisModel.Segments != null)
            {
                foreach (var segmentPair in acisModel.Segments)
                {
                    if (segmentPair.Value.ParsedContent != null &&
                        segmentPair.Value.ParsedContent.TryGetValue("ACIS", out object acisReaderObj) &&
                        acisReaderObj is AcisReader acisReader)
                    {
                        allAcisBodies.AddRange(AcisUtils.GetBodies(acisReader));
                    }
                }
            }

            int partCounter = 0;
            foreach (var body in allAcisBodies.Distinct())
            {
                partCounter++;
                string partId = $"{Path.GetFileNameWithoutExtension(originalFileName)}_part{partCounter}";
                string partName = $"Part_{body.Index}";

                var partProduct = CreateProduct(partId, partName, $"Canonical product for ACIS body {body.Index}", new List<ProductContext>());
                var partProductContext = CreateProductContext(partProduct, appContext, "mechanical", $"Ctx_{partName}");
                partProduct.FrameOfReference.Add(partProductContext);

                var partFormation = CreateProductDefinitionFormation(partProduct, "_formation", $"Formation_{partName}");
                var partDefContext = CreateProductDefinitionContext(appContext, "design", $"DesignCtx_{partName}");
                var partProdDef = CreateProductDefinition(partProduct, partFormation, partDefContext, "_definition", $"Def_{partName}");
                var partPDS = CreateProductDefinitionShape(partProdDef, $"Shape_{partName}", $"Shape of {partName}");

                List<StepEntity> shellsForThisBody = new List<StepEntity>();
                bool bodyIsLikelySolid = true; // Assume solid unless an open shell is found

                var lumps = body.GetLumps();
                if (!lumps.Any()) bodyIsLikelySolid = false; // No lumps means no solid

                foreach (var lump in lumps)
                {
                    List<AcisFace> allLumpFaces = new List<AcisFace>();
                    var acisShellsInLump = lump.GetShells();
                    if (!acisShellsInLump.Any())
                    {
                        // If a lump has no shells, it might indicate non-solid or empty geometry for this part of the body
                        bodyIsLikelySolid = false;
                        continue;
                    }

                    foreach (var acisShell in acisShellsInLump)
                    {
                        allLumpFaces.AddRange(acisShell.GetFaces());
                    }

                    if (!allLumpFaces.Any())
                    {
                        bodyIsLikelySolid = false; // No faces in this lump's shells
                        continue;
                    }

                    // Attempt to create one shell per lump. If a body has multiple lumps, it will result in multiple shells in SBSM.
                    // The heuristic bodyIsLikelySolid passed to CreateShellFromAcisFaces is an initial guess.
                    StepEntity stepShell = CreateShellFromAcisFaces(allLumpFaces, null, bodyIsLikelySolid,
                                                                    $"Shell_Lump_{lump.Index}_Body_{body.Index}", out bool createdClosedShell);

                    if (stepShell != null)
                    {
                        shellsForThisBody.Add(stepShell);
                        if (!createdClosedShell)
                        {
                            bodyIsLikelySolid = false; // If any shell created for a lump is not closed, the body isn't a single MSB.
                        }
                    }
                    else // No shell could be created from this lump's faces
                    {
                        bodyIsLikelySolid = false;
                    }
                }

                StepEntity canonicalShapeContent;
                if (bodyIsLikelySolid && shellsForThisBody.Count == 1 && shellsForThisBody[0] is ClosedShell closedOuterShell)
                {
                    canonicalShapeContent = CreateManifoldSolidBRep(closedOuterShell, $"MSB_Body_{body.Index}");
                }
                else
                {
                    if (bodyIsLikelySolid && shellsForThisBody.Count > 1)
                    {
                        Logger.Info($"Body {body.Index} resulted in multiple shells ({shellsForThisBody.Count}) but was initially considered solid. Creating ShellBasedSurfaceModel.");
                    }
                    else if (bodyIsLikelySolid && shellsForThisBody.Any() && !(shellsForThisBody[0] is ClosedShell))
                    {
                         Logger.Info($"Body {body.Index} resulted in an open shell but was initially considered solid. Creating ShellBasedSurfaceModel.");
                    }
                    canonicalShapeContent = CreateShellBasedSurfaceModel(shellsForThisBody, defaultRepContext, $"SBSM_Body_{body.Index}");
                }

                var canonicalSDR = CreateShapeDefinitionRepresentation(partPDS, canonicalShapeContent, $"SDR_{partName}");
                bodyToProductDataMap[body] = (partProduct, partProdDef, partPDS, canonicalSDR, canonicalShapeContent);
            }

            // --- Assembly Definition ---
            string asmFileId = Path.GetFileNameWithoutExtension(originalFileName);
            string asmProdId = $"{asmFileId}_assembly_product";
            string asmName = $"{asmFileId}_Assembly";

            // Use the generated productDescription for the assembly product
            var asmProduct = CreateProduct(asmProdId, asmName, productDescription, new List<ProductContext>());
            var asmProductContext = CreateProductContext(asmProduct, appContext, "mechanical", $"Ctx_{asmName}");
            asmProduct.FrameOfReference.Add(asmProductContext);

            var asmFormation = CreateProductDefinitionFormation(asmProduct, "_asm_formation", $"Formation_{asmName}");
            var asmDefContext = CreateProductDefinitionContext(appContext, "design", $"DesignCtx_{asmName}");
            var asmProdDef = CreateProductDefinition(asmProduct, asmFormation, asmDefContext, "_asm_definition", $"Def_{asmName}");
            var asmPDS = CreateProductDefinitionShape(asmProdDef, $"Shape_{asmName}", $"Shape of assembly {asmName}");

            List<StepEntity> assemblyRootShapeItems = new List<StepEntity>(); // Items for the assembly's root shape representation

            // --- Instancing (Loop 2) ---
            int instanceCounter = 0;
            foreach (var body in allAcisBodies)
            {
                instanceCounter++;
                if (!bodyToProductDataMap.TryGetValue(body, out var productData)) // productData includes partPDS now
                {
                    Logger.Warning($"Could not find canonical product data for ACIS body {body.Index} during instancing. Skipping.");
                    continue;
                }

                Matrix4x4? bodyTransformMatrix = body.TransformEntity?.Matrix;
                string instanceNameSuffix = $"_Instance{instanceCounter}_Body{body.Index}";

                StepEntity representationInAssemblyContext;
                ShapeDefinitionRepresentation sdrForThisInstanceOfPart;

                if (bodyTransformMatrix.HasValue && !bodyTransformMatrix.Value.IsIdentity)
                {
                    var stepTransformAxisPlacement = CreateAxis2Placement3D(bodyTransformMatrix.Value, $"Placement{instanceNameSuffix}");
                    var repMap = CreateRepresentationMap(stepTransformAxisPlacement, productData.CanonicalShapeContent, $"Map{instanceNameSuffix}");
                    var mappedItem = CreateMappedItem(repMap, productData.CanonicalSDR, $"MappedItem{instanceNameSuffix}");

                    representationInAssemblyContext = mappedItem;

                    // Create an instance-specific SHAPE_REPRESENTATION containing just this MAPPED_ITEM
                    var instanceSpecificRepItems = new List<StepEntity> { mappedItem };
                    // This AssemblyShapeRepresentation is for the instance, not the whole assembly.
                    // It uses the same defaultRepContext as the canonical part's shape.
                    var instanceShapeRep = CreateAssemblyShapeRepresentation(instanceSpecificRepItems, defaultRepContext, $"Rep_Inst{instanceNameSuffix}");
                    sdrForThisInstanceOfPart = CreateShapeDefinitionRepresentation(productData.PDS, instanceShapeRep, $"SDR_Inst{instanceNameSuffix}");
                }
                else
                {
                    // No transform or identity transform: use the canonical shape directly in the assembly's items.
                    representationInAssemblyContext = productData.CanonicalShapeContent;
                    sdrForThisInstanceOfPart = productData.CanonicalSDR; // Use the canonical SDR for CDSR link
                }

                assemblyRootShapeItems.Add(representationInAssemblyContext);

                // Create NAUO to establish product hierarchy
                string nauoInstanceId = $"NAUO_Instance_{instanceCounter}";
                string nauoInstanceName = $"Usage_of_{productData.Product.Name}_in_{asmName}";
                string nauoDescription = $"This is instance {instanceCounter} of part {productData.Product.Name}";
                var nauo = CreateNextAssemblyUsageOccurrence(asmProdDef, productData.ProdDef, nauoInstanceId, nauoInstanceName, nauoDescription);

                // Create CDSR to link the NAUO's component PDS to its specific representation in this assembly context
                CreateContextDependentShapeRepresentation(sdrForThisInstanceOfPart, productData.PDS, $"CDSR{instanceNameSuffix}");
            }

            // --- Finalize Assembly SDR ---
            // The assembly's root shape representation contains all MAPPED_ITEMs or direct canonical shapes.
            AssemblyShapeRepresentation assemblyRootShapeRep = CreateAssemblyShapeRepresentation(assemblyRootShapeItems, defaultRepContext, $"AsmRootRep_{asmName}");
            ShapeDefinitionRepresentation assemblyMasterSDR = CreateShapeDefinitionRepresentation(asmPDS, assemblyRootShapeRep, $"MasterSDR_{asmName}");


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
        public static CartesianPoint CreateCartesianPoint(Vector3 vec, string name = "", bool useCache = true)
        {
            /*
            string key = $"CP_{vec.X:F8}_{vec.Y:F8}_{vec.Z:F8}"; // Increased precision for cache key
            if (useCache && _cartesianPoints.TryGetValue(key, out CartesianPoint cp))
            {
                return cp;
            }
            cp = new CartesianPoint(name, vec.X, vec.Y, vec.Z);
            if (useCache) _cartesianPoints[key] = cp;
            return cp;
            */
            throw new NotImplementedException();
        }

        public static Direction CreateDirection(Vector3 vec, string name = "", bool useCache = true)
        {
            /*
            var normalizedVec = vec.LengthSquared() > 1e-12f ? Vector3.Normalize(vec) : vec; // Avoid normalizing zero vector
            string key = $"DIR_{normalizedVec.X:F8}_{normalizedVec.Y:F8}_{normalizedVec.Z:F8}";
            if (useCache && _directions.TryGetValue(key, out Direction dir))
            {
                return dir;
            }
            dir = new Direction(name, normalizedVec.X, normalizedVec.Y, normalizedVec.Z);
            if (useCache) _directions[key] = dir;
            return dir;
            */
            throw new NotImplementedException();
        }

        public static Vector CreateVector(Direction orientation, double magnitude, string name = "", bool useCache = true)
        {
            /*
            string key = $"VEC_{orientation.Id}_{magnitude:F8}";
            if (useCache && !_vectors.TryGetValue(key, out var vec))
            {
                vec = new Vector(name, orientation, magnitude);
                if (useCache) _vectors[key] = vec;
                return vec;
            }
            // Fallback for no cache hit or if not using cache
            return _vectors.TryGetValue(key, out var cachedVec) ? cachedVec : new Vector(name, orientation, magnitude);
            */
            throw new NotImplementedException();
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
            /*
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
            */
            throw new NotImplementedException();
        }

        public static EdgeCurve CreateEdgeCurve(AcisEdge acisEdge, string name = "", bool useCache = true)
        {
            /*
            if (acisEdge == null) return null;
            var startVp = CreateVertexPoint(acisEdge.StartVertex, name + "_start_v", useCache);
            var endVp = CreateVertexPoint(acisEdge.EndVertex, name + "_end_v", useCache);
            var curveGeom = CreateCurveGeometry(acisEdge.CurveEntity, name + "_curve", useCache);
            bool sense = acisEdge.Sense == SenseEnum.FORWARD || acisEdge.Sense == SenseEnum.UNKNOWN; // Default UNKNOWN to FORWARD for STEP

            string key = $"EC_{startVp?.Id}_{endVp?.Id}_{curveGeom?.Id}_{sense}";
             if (useCache && _edgeCurves.TryGetValue(key, out var ec))
            {
                // Potentially apply style even to cached edge if style wasn't part of its original creation key
                // For now, style is applied on new creation.
                return ec;
            }
            var stepEdgeCurve = new EdgeCurve(name, startVp, endVp, curveGeom, sense);
            if (useCache) _edgeCurves[key] = stepEdgeCurve;

            // Apply styles if attributes exist
            if (acisEdge.AttribList != null)
            {
                foreach (var attr in acisEdge.AttribList)
                {
                    if (attr is AttribStRgbColor colorAttr)
                    {
                        // Convert 0-1 double to 0-255 byte for CreateColourRgb if its signature expects that,
                        // or adjust CreateColourRgb to take doubles. Assuming CreateColourRgb takes doubles 0-1.
                        ColourRgb stepColor = CreateColourRgb(colorAttr.Red, colorAttr.Green, colorAttr.Blue, $"Color_Edge_{acisEdge.Index}");
                        CurveStyle curveStyle = CreateCurveStyle(stepColor, null, $"Style_Edge_{acisEdge.Index}"); // width = null for now

                        // PresentationStyleAssignment expects a list of styles.
                        // CurveStyle is a "PRESENTATION_STYLE_SELECT" item.
                        // For AP203/214, CURVE_STYLE is usually part of PRESENTATION_STYLE_ASSIGNMENT.
                        var stylesForPsa = new List<StepEntity> { curveStyle };
                        PresentationStyleAssignment psa = CreatePresentationStyleAssignment(stylesForPsa, $"PSA_Edge_{acisEdge.Index}");

                        var styledItemStyles = new List<StepEntity> { psa }; // STYLED_ITEM takes a list of PRESENTATION_STYLE_ASSIGNMENT
                        CreateStyledItem($"StyledEdge_{acisEdge.Index}", styledItemStyles, stepEdgeCurve);

                        break; // Apply first color found, for simplicity
                    }
                    // Add checks for other style attributes if needed (e.g., line width, font)
                }
            }
            return stepEdgeCurve;
            */
            throw new NotImplementedException();
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
            /*
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
            */
            throw new NotImplementedException();
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


        // Overload for creating Axis2Placement3D from a Matrix4x4
        public static Axis2Placement3D CreateAxis2Placement3D(Matrix4x4 matrix, string name = "", bool useCache = true)
        {
            /*
            Vector3 location = matrix.Translation;

            // Extract rotation part of the matrix to transform direction vectors
            Matrix4x4 rotationMatrix = matrix;
            rotationMatrix.Translation = Vector3.Zero; // Remove translation part

            Vector3 zAxis = Vector3.TransformNormal(Vector3.UnitZ, rotationMatrix);
            Vector3 xAxis = Vector3.TransformNormal(Vector3.UnitX, rotationMatrix);

            // Ensure Axis and RefDirection are not collinear, and RefDirection is perpendicular to Axis
            // This is a simplified approach; robust orthogonalization might be needed.
            if (Vector3.Dot(zAxis, xAxis) > 0.999 || Vector3.Dot(zAxis, xAxis) < -0.999) // Check if nearly collinear
            {
                // If Z' and X' are collinear, pick Y' as ref_dir if possible
                xAxis = Vector3.TransformNormal(Vector3.UnitY, rotationMatrix);
                if (Vector3.Dot(zAxis, xAxis) > 0.999 || Vector3.Dot(zAxis, xAxis) < -0.999)
                {
                    // If Z' and Y' are also collinear (shouldn't happen if Z' is valid),
                    // use an arbitrary perpendicular for xAxis.
                    xAxis = AcisUtils.GetArbitraryAxis(zAxis); // Assumes AcisUtils.GetArbitraryAxis exists
                }
            }

            // Ensure RefDirection is perpendicular to Axis (Gram-Schmidt simplified)
            xAxis = Vector3.Normalize(xAxis - Vector3.Dot(xAxis, Vector3.Normalize(zAxis)) * Vector3.Normalize(zAxis));


            return CreateAxis2Placement3D(location, Vector3.Normalize(zAxis), Vector3.Normalize(xAxis), name, useCache);
            */
            throw new NotImplementedException();
        }

        // --- Factory Methods for Product Definition Entities ---

        public static ApplicationContext CreateApplicationContext(string description, string name = "", bool useCache = true)
        {
            // Name for ApplicationContext is often fixed or derived, description is key.
            string defaultedName = string.IsNullOrEmpty(name) ? "Application Context" : name;
            string key = $"APPCTX_{defaultedName}_{description}";
            if (useCache && _applicationContexts.TryGetValue(key, out var ctx)) return ctx;

            var newCtx = new ApplicationContext(defaultedName, description);
            if (useCache) _applicationContexts[key] = newCtx;
            return newCtx;
        }

        public static ProductContext CreateProductContext(Product product, ApplicationContext appCtx, string discipline, string name = "", bool useCache = true)
        {
            string defaultedName = string.IsNullOrEmpty(name) ? $"ProductContextFor_{product.ProductId}" : name;
            string key = $"PRODCTX_{defaultedName}_{appCtx.Id}_{discipline}";
            if (useCache && _productContexts.TryGetValue(key, out var pCtx)) return pCtx;

            var newPCtx = new ProductContext(defaultedName, appCtx, discipline);
            if (useCache) _productContexts[key] = newPCtx;
            return newPCtx;
        }

        public static Product CreateProduct(string id, string productName, string description, List<ProductContext> contexts, string name = "", bool useCache = true)
        {
            string defaultedName = string.IsNullOrEmpty(name) ? productName : name; // Use productName for STEP name if 'name' (for entity label) is empty
            string key = $"PROD_{id}_{defaultedName}";
            if (useCache && _products.TryGetValue(key, out var prod)) return prod;

            var newProd = new Product(defaultedName, id, description, contexts);
            if (useCache) _products[key] = newProd;
            return newProd;
        }

        public static ProductDefinitionFormation CreateProductDefinitionFormation(Product product, string idSuffix = "_formation", string name = "", bool useCache = true)
        {
            string formationId = product.ProductId + idSuffix;
            string defaultedName = string.IsNullOrEmpty(name) ? $"FormationFor_{product.ProductId}" : name;
            string key = $"PRODDEFFORM_{formationId}";
            if (useCache && _productDefinitionFormations.TryGetValue(key, out var pdf)) return pdf;

            var newPdf = new ProductDefinitionFormation(defaultedName, formationId, product);
            if (useCache) _productDefinitionFormations[key] = newPdf;
            return newPdf;
        }

        public static ProductDefinitionContext CreateProductDefinitionContext(ApplicationContext appCtx, string lifeCycleStage, string name = "", bool useCache = true)
        {
            string defaultedName = string.IsNullOrEmpty(name) ? $"ProductDefContext_{lifeCycleStage}" : name;
            string key = $"PRODDEFCTX_{defaultedName}_{appCtx.Id}_{lifeCycleStage}";
            if (useCache && _productDefinitionContexts.TryGetValue(key, out var pdCtx)) return pdCtx;

            var newPdCtx = new ProductDefinitionContext(defaultedName, appCtx, lifeCycleStage);
            if (useCache) _productDefinitionContexts[key] = newPdCtx;
            return newPdCtx;
        }

        public static ProductDefinition CreateProductDefinition(Product product, ProductDefinitionFormation formation, ProductDefinitionContext context, string idSuffix = "_definition", string name = "", bool useCache = true)
        {
            string definitionId = product.ProductId + idSuffix;
            string defaultedName = string.IsNullOrEmpty(name) ? $"DefinitionFor_{product.ProductId}" : name;
            string key = $"PRODDEF_{definitionId}";
            if (useCache && _productDefinitions.TryGetValue(key, out var pd)) return pd;

            var newPd = new ProductDefinition(defaultedName, definitionId, formation, context);
            if (useCache) _productDefinitions[key] = newPd;
            return newPd;
        }

        public static ProductDefinitionShape CreateProductDefinitionShape(ProductDefinition prodDef, string name = "", string description = "", bool useCache = true)
        {
            string defaultedName = string.IsNullOrEmpty(name) ? $"ShapeOf_{prodDef.DefinitionId}" : name;
            string defaultedDescription = string.IsNullOrEmpty(description) ? "Main shape" : description;
            string key = $"PDSHAPE_{defaultedName}_{prodDef.Id}";
            if (useCache && _productDefinitionShapes.TryGetValue(key, out var pds)) return pds;

            var newPds = new ProductDefinitionShape(defaultedName, defaultedDescription, prodDef);
            if (useCache) _productDefinitionShapes[key] = newPds;
            return newPds;
        }

        public static ShapeDefinitionRepresentation CreateShapeDefinitionRepresentation(ProductDefinitionShape pds, StepEntity shapeRep, string name = "", bool useCache = true)
        {
            // Name for SHAPE_DEFINITION_REPRESENTATION is often context-specific or empty.
            string defaultedName = string.IsNullOrEmpty(name) ? $"ShapeRepFor_{pds.Name}" : name;
            string key = $"SHAPEDEFREP_{defaultedName}_{pds.Id}_{shapeRep.Id}";
            if (useCache && _shapeDefinitionRepresentations.TryGetValue(key, out var sdr)) return sdr;

            var newSdr = new ShapeDefinitionRepresentation(defaultedName, pds, shapeRep);
            if (useCache) _shapeDefinitionRepresentations[key] = newSdr;
            return newSdr;
        }


        // --- Factory Methods for Styles (continued) ---
        public static CurveStyle CreateCurveStyle(ColourRgb color, double? width, string name = "", bool useCache = true)
        {
            object widthObj = width.HasValue ? (object)width.Value : new StepDollarNotApplicable();
            // Key needs to account for width presence/absence
            string widthKey = width.HasValue ? width.Value.ToString("F8", CultureInfo.InvariantCulture) : "$";
            string key = $"CRVSTYLE_{name}_{color.Id}_{widthKey}";

            if (useCache && _curveStyles.TryGetValue(key, out var style)) return style;

            var newStyle = new CurveStyle(name, color, widthObj);
            if (useCache) _curveStyles[key] = newStyle;
            return newStyle;
        }

        // CreatePresentationStyleAssignment might already exist or needs to be verified/added if not.
        // Assuming it exists based on previous subtasks involving surface styles.
        // If not, it would be:
        /*
        public static PresentationStyleAssignment CreatePresentationStyleAssignment(List<StepEntity> styles, string name = "", bool useCache = true)
        {
            // Simplified key
            string key = $"PSA_{name}_{styles.FirstOrDefault()?.Id}_{styles.Count}";
            if(useCache && _presentationStyleAssignments.TryGetValue(key, out var psa)) return psa;

            var newPsa = new PresentationStyleAssignment(styles); // Name is not part of PSA entity in STEP
            if(useCache) _presentationStyleAssignments[key] = newPsa;
            return newPsa;
        }
        */

        // --- Factory Methods for Assembly Entities ---

        public static RepresentationContext CreateRepresentationContext(string contextType, string name = "DefaultContext", bool useCache = true)
        {
            string key = $"REPCTX_{name}_{contextType}";
            if (useCache && _representationContexts.TryGetValue(key, out var ctx)) return ctx;

            var newCtx = new RepresentationContext(name, contextType);
            if (useCache) _representationContexts[key] = newCtx;
            return newCtx;
        }

        public static RepresentationMap CreateRepresentationMap(Axis2Placement3D mappingOrigin, StepEntity mappedRepresentation, string name = "", bool useCache = true)
        {
            string defaultedName = string.IsNullOrEmpty(name) ? $"MapForRep{mappedRepresentation.Id}" : name;
            string key = $"REPMAP_{defaultedName}_{mappingOrigin.Id}_{mappedRepresentation.Id}";
            if (useCache && _representationMaps.TryGetValue(key, out var map)) return map;

            var newMap = new RepresentationMap(defaultedName, mappingOrigin, mappedRepresentation);
            if (useCache) _representationMaps[key] = newMap;
            return newMap;
        }

        public static MappedItem CreateMappedItem(RepresentationMap mappingSource, ShapeDefinitionRepresentation mappingTarget, string name = "", bool useCache = true)
        {
            string defaultedName = string.IsNullOrEmpty(name) ? $"MappedItem_{mappingSource.Id}_{mappingTarget.Id}" : name;
            string key = $"MAPPEDITEM_{defaultedName}"; // Name should be unique enough for cache key if used
            if (useCache && _mappedItems.TryGetValue(key, out var item)) return item;

            var newItem = new MappedItem(defaultedName, mappingSource, mappingTarget);
            if (useCache) _mappedItems[key] = newItem;
            return newItem;
        }

        public static AssemblyShapeRepresentation CreateAssemblyShapeRepresentation(List<StepEntity> items, RepresentationContext context, string name = "", bool useCache = true)
        {
            string defaultedName = string.IsNullOrEmpty(name) ? "AssemblyRepresentation" : name;
            // Cache key for a list-based entity can be tricky. Using context and first item id if present.
            string listIndicator = items.Any() ? items.First().Id.ToString() : "empty";
            string key = $"ASMREPSHAPE_{defaultedName}_{context.Id}_{items.Count}_{listIndicator}";
            if (useCache && _assemblyShapeRepresentations.TryGetValue(key, out var rep)) return rep;

            var newRep = new AssemblyShapeRepresentation(defaultedName, items, context);
            if (useCache) _assemblyShapeRepresentations[key] = newRep;
            return newRep;
        }


        public static NextAssemblyUsageOccurrence CreateNextAssemblyUsageOccurrence(
            ProductDefinition assemblyDef, ProductDefinition componentDef,
            string instanceId, string instanceName = "", string description = "", string refDes = "",
            bool useCache = true)
        {
            string defaultedName = string.IsNullOrEmpty(instanceName) ? $"Instance_{instanceId}" : instanceName;
            string defaultedDescription = string.IsNullOrEmpty(description) ? $"Occurrence of {componentDef.Name} in {assemblyDef.Name}" : description;

            // Key for NAUO should uniquely identify the parent-child-instance relationship
            string key = $"NAUO_{assemblyDef.Id}_{componentDef.Id}_{instanceId}";
            if (useCache && _nauos.TryGetValue(key, out var nauo)) return nauo;

            var newNauo = new NextAssemblyUsageOccurrence(defaultedName, instanceId, defaultedDescription, assemblyDef, componentDef, refDes);
            if (useCache) _nauos[key] = newNauo;
            return newNauo;
        }


        public static ContextDependentShapeRepresentation CreateContextDependentShapeRepresentation(
            ShapeDefinitionRepresentation shapeRepInContext, ProductDefinitionShape definingPds,
            string name = "", bool useCache = true)
        {
            string defaultedName = string.IsNullOrEmpty(name) ? $"CDSR_ForSDR{shapeRepInContext.Id}_PDS{definingPds.Id}" : name;
            string key = $"CDSR_{shapeRepInContext.Id}_{definingPds.Id}"; // Key based on the two main linked entities
            if (useCache && _cdsr.TryGetValue(key, out var cdsr)) return cdsr;

            var newCdsr = new ContextDependentShapeRepresentation(defaultedName, shapeRepInContext, definingPds);
            if (useCache) _cdsr[key] = newCdsr;
            return newCdsr;
        }


        // --- Factory methods for MSB/SBSM and Shells ---

        public static OpenShell CreateOpenShell(List<AdvancedFace> faces, string name = "", bool useCache = true)
        {
            // Simplified cache key for example; a more robust key would consider face IDs.
            string key = $"OS_{name}_{faces.Count}_{faces.FirstOrDefault()?.Id}";
            if (useCache && _openShells.TryGetValue(key, out var shell)) return shell;

            var newShell = new OpenShell(name, faces);
            if (useCache) _openShells[key] = newShell;
            return newShell;
        }

        public static ClosedShell CreateClosedShell(List<AdvancedFace> faces, string name = "", bool useCache = true)
        {
            // Simplified cache key
            string key = $"CS_{name}_{faces.Count}_{faces.FirstOrDefault()?.Id}";
            // Note: _openShells can store ClosedShell instances as ClosedShell derives from OpenShell.
            if (useCache && _openShells.TryGetValue(key, out var shell) && shell is ClosedShell closedShell) return closedShell;

            var newShell = new ClosedShell(name, faces);
            if (useCache) _openShells[key] = newShell; // Store in _openShells cache
            return newShell;
        }

        // New helper method
        public static StepEntity CreateShellFromAcisFaces(IEnumerable<AcisFace> acisFaces, Matrix4x4? worldTransform,
                                                          bool attemptClosedShell, string name, out bool wasClosed, bool useCache = true)
        {
            wasClosed = false;
            var advFaces = new List<AdvancedFace>();
            foreach (var acisFace in acisFaces)
            {
                // Pass the worldTransform to CreateAdvancedFace if faces can be transformed individually.
                // For canonical part representations, worldTransform should be null here.
                var advFace = CreateAdvancedFace(acisFace, worldTransform, false, $"{name}_face{advFaces.Count}", useCache);
                if (advFace != null) advFaces.Add(advFace);
            }

            if (!advFaces.Any())
            {
                Logger.Warning($"CreateShellFromAcisFaces: No advanced faces created for shell '{name}'. Returning null.");
                return null;
            }

            if (attemptClosedShell) // Heuristic: if we're trying for a solid and got faces
            {
                // Simple heuristic: if it's supposed to be closed and we have faces, assume it is for now.
                // Real check would involve analyzing edge sharing, manifoldness, etc.
                wasClosed = true;
                return CreateClosedShell(advFaces, name, useCache);
            }
            else
            {
                return CreateOpenShell(advFaces, name, useCache);
            }
        }


        public static ManifoldSolidBRep CreateManifoldSolidBRep(ClosedShell outerShell, string name = "", bool useCache = true)
        {
            // MSBs are typically unique by their shell.
            string key = $"MSB_{name}_{outerShell.Id}";
            if (useCache && _manifoldSolidBReps.TryGetValue(key, out var msb)) return msb;

            var newMsb = new ManifoldSolidBRep(name, outerShell);
            if (useCache) _manifoldSolidBReps[key] = newMsb;
            return newMsb;
        }

        // Modified CreateShellBasedSurfaceModel to accept List<StepEntity> for shells
        public static ShellBasedSurfaceModel CreateShellBasedSurfaceModel(List<StepEntity> shells, RepresentationContext context, string name = "", bool useCache = true)
        {
            // SBSMs are typically unique by their constituent shells and context.
            // Simplified cache key:
            string listIndicator = shells.Any() ? shells.First().Id.ToString() : "empty";
            string key = $"SBSM_{name}_{context.Id}_{shells.Count}_{listIndicator}";
            if (useCache && _shellBasedSurfaceModels.TryGetValue(key, out var sbsm)) return sbsm;

            // Cast shells to OpenShell if necessary, or ensure ShellBasedSurfaceModel handles List<StepEntity> correctly.
            // For now, assuming ShellBasedSurfaceModel's constructor and Items property can handle List<StepEntity>
            // where items are expected to be OpenShell or ClosedShell.
            var newSbsm = new ShellBasedSurfaceModel(name, shells.OfType<OpenShell>().ToList(), context); // Constructor expects List<OpenShell>
            if (useCache) _shellBasedSurfaceModels[key] = newSbsm;
            return newSbsm;
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
            if (o is StepDollarNotApplicable) return "$"; // Added handler for $
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
