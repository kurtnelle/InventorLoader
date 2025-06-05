using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization; // For CultureInfo.InvariantCulture
// Assuming AcisHeader and AcisBody (from AcisReader.cs/AcisEntities.cs) will be available
// For now, let's define placeholder types if they are not yet created or accessible.
// using InventorLoaderCs; // This would be used if AcisHeader/AcisBody are in the same project.

// Placeholder for AcisBody if not defined elsewhere yet
namespace InventorLoaderCs
{
    // public class AcisHeader { /* ... */ } // Assuming this is defined
    public class AcisBody { /* ... */ }   // Placeholder
}

namespace InventorLoaderCs
{
    public abstract class StepEntity
    {
        private static int NextId = 1;
        public int Id { get; private set; }
        public bool HasBeenExported { get; set; }

        protected StepEntity()
        {
            Id = NextId++;
            StepConverterUtils.RegisterEntity(this); // Register entity upon creation
        }

        public abstract List<object> GetParameters();
        public abstract string GetClassName();

        public virtual string ExportStep()
        {
            if (HasBeenExported)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.Append($"#{Id} = {GetClassName().ToUpper()}(");

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

            // Export referenced entities (properties)
            sb.Append(ExportPropertiesStep());

            return sb.ToString();
        }

        public virtual string ExportPropertiesStep()
        {
            var sb = new StringBuilder();
            List<object> parameters = GetParameters();
            foreach (var param in parameters)
            {
                if (param is StepEntity se && !se.HasBeenExported)
                {
                    sb.Append(se.ExportStep());
                }
                else if (param is IEnumerable<object> list)
                {
                    foreach (var item in list)
                    {
                        if (item is StepEntity listItemSe && !listItemSe.HasBeenExported)
                        {
                            sb.Append(listItemSe.ExportStep());
                        }
                    }
                }
            }
            return sb.ToString();
        }

        public override string ToString()
        {
            return $"#{Id}"; // Used when this entity is a parameter in another entity
        }
    }

    public class StepNamedEntity : StepEntity
    {
        public string Name { get; set; }

        protected StepNamedEntity(string name = "") : base()
        {
            Name = name;
        }

        public override List<object> GetParameters()
        {
            var paramsList = new List<object> { Name };
            return paramsList;
        }
    }

    public class ColourRgb : StepNamedEntity
    {
        public double Red { get; set; }
        public double Green { get; set; }
        public double Blue { get; set; }

        public ColourRgb(string name, double red, double green, double blue) : base(name)
        {
            Red = red;
            Green = green;
            Blue = blue;
        }

        public override string GetClassName() => "COLOUR_RGB";

        public override List<object> GetParameters()
        {
            var paramsList = base.GetParameters();
            paramsList.AddRange(new List<object> { Red, Green, Blue });
            return paramsList;
        }
    }

    public class CartesianPoint : StepNamedEntity
    {
        public List<double> Coordinates { get; set; }

        public CartesianPoint(string name, List<double> coordinates) : base(name)
        {
            Coordinates = coordinates ?? new List<double> { 0.0, 0.0, 0.0 };
        }

        public override string GetClassName() => "CARTESIAN_POINT";

        public override List<object> GetParameters()
        {
            var paramsList = base.GetParameters();
            paramsList.Add(Coordinates);
            return paramsList;
        }
    }

    public class Direction : StepNamedEntity
    {
        public List<double> Ratios { get; set; }

        public Direction(string name, List<double> ratios) : base(name)
        {
            Ratios = ratios ?? new List<double> { 0.0, 0.0, 1.0 };
        }

        public override string GetClassName() => "DIRECTION";

        public override List<object> GetParameters()
        {
            var paramsList = base.GetParameters();
            paramsList.Add(Ratios);
            return paramsList;
        }
    }

    public class Line : StepNamedEntity // Assuming LINE is a named entity in STEP
    {
        public CartesianPoint Pnt { get; set; }
        public Vector Dir { get; set; } // Assuming a Vector STEP Entity will be defined

        public Line(string name, CartesianPoint pnt, Vector dir) : base(name)
        {
            Pnt = pnt;
            Dir = dir;
        }

        public override string GetClassName() => "LINE";

        public override List<object> GetParameters()
        {
            var paramsList = base.GetParameters();
            paramsList.AddRange(new List<object> { Pnt, Dir });
            return paramsList;
        }
    }

    // Placeholder for Vector STEP entity, as used by Line
    public class Vector : StepNamedEntity
    {
        public Direction Orientation { get; set; }
        public double Magnitude { get; set; }

        public Vector(string name, Direction orientation, double magnitude) : base(name)
        {
            Orientation = orientation;
            Magnitude = magnitude;
        }
        public override string GetClassName() => "VECTOR";
        public override List<object> GetParameters()
        {
            var paramsList = base.GetParameters();
            paramsList.AddRange(new List<object>{ Orientation, Magnitude });
            return paramsList;
        }
    }


    public static class StepConverterUtils
    {
        private static List<StepEntity> _entities = new List<StepEntity>();
        private static Dictionary<string, CartesianPoint> PointsCartesian = new Dictionary<string, CartesianPoint>();
        private static Dictionary<string, Direction> Directions = new Dictionary<string, Direction>();
        // Add other caches as needed: _lines, _ellipses, etc.

        internal static void RegisterEntity(StepEntity entity)
        {
            _entities.Add(entity);
        }

        public static void InitExport()
        {
            _entities.Clear();
            PointsCartesian.Clear();
            Directions.Clear();
            // Reset NextId in StepEntity if it's not handled by re-initializing the static context
            // StepEntity.ResetId(); // Would require a method in StepEntity
        }

        public static string Export(string filename, AcisHeader header, List<AcisBody> bodies)
        {
            InitExport();

            // STEP Header
            var sb = new StringBuilder();
            sb.AppendLine("ISO-10303-21;");
            sb.AppendLine("HEADER;");
            sb.AppendLine($"FILE_DESCRIPTION(('FreeCAD Model'),'2;1');"); // Example description
            sb.AppendLine($"FILE_NAME('{filename.Replace("\\", "\\\\")}',"); // Escape backslashes in filename
            sb.AppendLine($"'{DateTime.Now:yyyy-MM-ddTHH:mm:ss}',");
            sb.AppendLine($"('Author Name'),"); // Placeholder
            sb.AppendLine($"('Organization Name'),"); // Placeholder
            sb.AppendLine($"'InventorLoaderCs',"); // Preprocessor version
            sb.AppendLine($"'FreeCAD','Authored Product');"); // Originating system, authorization
            sb.AppendLine("FILE_SCHEMA (('AUTOMOTIVE_DESIGN { 1 0 10303 214 1 1 1 1}'));"); // Example schema
            sb.AppendLine("ENDSEC;");
            sb.AppendLine();
            sb.AppendLine("DATA;");

            // --- Core STEP content creation starts here ---
            // This part is highly complex and depends on traversing the ACIS model (bodies)
            // and creating corresponding STEP entities.

            // Example: Create an application protocol definition (mandatory for many viewers)
            // ApplicationProtocolDefinition appDef = new ApplicationProtocolDefinition();
            // sb.Append(appDef.ExportStep());


            // TODO: Convert AcisBody list to STEP entities
            // This would involve:
            // 1. Traversing each AcisBody, its Lumps, Shells, Faces, Edges, Vertices.
            // 2. Creating corresponding STEP entities (ADVANCED_FACE, EDGE_CURVE, VERTEX_POINT, etc.)
            //    using helper methods like CreateCartesianPoint, CreateDirection.
            // 3. Managing relationships between these entities.
            // 4. Appending their ExportStep() string to sb.

            // For now, just export registered entities (which might be few if not created during body processing)
            foreach (var entity in _entities)
            {
                 if (!entity.HasBeenExported) // Ensure properties are exported if entity was created but not directly added to main list
                 {
                    sb.Append(entity.ExportStep());
                 }
            }

            // --- Core STEP content creation ends here ---

            sb.AppendLine("ENDSEC;");
            sb.AppendLine("END-ISO-10303-21;");

            // Write to file (example)
            // System.IO.File.WriteAllText(filename, sb.ToString());

            FinalizeExport();
            return sb.ToString(); // Or return path to file
        }

        public static void FinalizeExport()
        {
            // Clean up if necessary
            _entities.Clear(); // Good practice to clear static collections
            PointsCartesian.Clear();
            Directions.Clear();
        }

        public static CartesianPoint CreateCartesianPoint(System.Numerics.Vector3 vec, string name = "")
        {
            string key = $"{vec.X},{vec.Y},{vec.Z},{name}";
            if (!PointsCartesian.TryGetValue(key, out CartesianPoint cp))
            {
                cp = new CartesianPoint(name, new List<double> { vec.X, vec.Y, vec.Z });
                PointsCartesian[key] = cp;
            }
            return cp;
        }

        public static Direction CreateDirection(System.Numerics.Vector3 vec, string name = "")
        {
            var normalizedVec = System.Numerics.Vector3.Normalize(vec);
            string key = $"{normalizedVec.X},{normalizedVec.Y},{normalizedVec.Z},{name}";
            if (!Directions.TryGetValue(key, out Direction dir))
            {
                dir = new Direction(name, new List<double> { normalizedVec.X, normalizedVec.Y, normalizedVec.Z });
                Directions[key] = dir;
            }
            return dir;
        }

        public static string DoubleToString(double d)
        {
            if (d == 0.0) return "0.";
            // Basic formatting, Acis2Step.py has more complex logic for E notation
            return d.ToString("G17", CultureInfo.InvariantCulture).ToUpper();
        }

        public static string BoolToString(bool b)
        {
            return b ? ".T." : ".F.";
        }

        public static string ObjToString(object o)
        {
            if (o == null) return "$";
            if (o is string s) return $"'{s.Replace("'", "''")}'"; // Escape single quotes
            if (o is double d) return DoubleToString(d);
            if (o is float f) return DoubleToString(f);
            if (o is int i) return i.ToString();
            if (o is long l) return l.ToString();
            if (o is bool b) return BoolToString(b);
            if (o is StepEntity se) return se.ToString(); // Returns "#ID"
            if (o is Enum) return $".{o.ToString().ToUpper()}."; // For enums like .BOTH.
            if (o is IEnumerable<object> list)
            {
                var sb = new StringBuilder("(");
                var first = true;
                foreach (var item in list)
                {
                    if (!first) sb.Append(",");
                    sb.Append(ObjToString(item));
                    first = false;
                }
                sb.Append(")");
                return sb.ToString();
            }
            // Add more type handlers as needed
            return o.ToString();
        }
    }
}
