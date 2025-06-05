using System.Collections.Generic;
using System.Text;

namespace InventorLoaderCs
{
    public class AcisRecord
    {
        public List<AcisChunk> Chunks { get; set; }
        public string Name { get; set; } // Type of ACIS entity, e.g., "body", "face-surface"
        public int Index { get; set; }   // Entity index, e.g., -123
        public AcisEntity Entity { get; set; } // The parsed entity object

        public AcisRecord(string name)
        {
            Name = name;
            Chunks = new List<AcisChunk>();
            Index = -1; // Default, can be set after parsing name like "-123"
            Entity = null;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            if (Index >= 0)
            {
                sb.Append($"-{Index} ");
            }
            sb.Append(Name);
            sb.Append(" ");
            foreach (var chunk in Chunks)
            {
                sb.Append(chunk.ToString());
            }
            return sb.ToString().TrimEnd();
        }
    }
}
