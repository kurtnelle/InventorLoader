using System;

namespace InventorLoaderCs
{
    public class AcisHeader
    {
        public double Version { get; set; }
        public int Records { get; set; } // Total number of records
        public int Bodies { get; set; }  // Number of body entities
        public int Flags { get; set; }   // File flags or options
        public string ProdId { get; set; }
        public string ProdVer { get; set; }
        public string Date { get; set; }
        public double Scale { get; set; }
        public double ResAbs { get; set; } // Absolute resolution
        public double ResNor { get; set; } // Normalization resolution (angular tolerance)

        // For binary files
        public string Format { get; set; } // e.g., "ACIS BinaryFile"
        public Tuple<int,int,int,int> AsmVersion { get; set; } // For ASM files

        public AcisHeader()
        {
            Version = 7.0; // Default or common version
            ProdId = "InventorLoaderCs";
            ProdVer = "1.0";
            Date = DateTime.Now.ToString("ddd, MMM dd HH:mm:ss yyyy"); // Example format
            Scale = 1.0;
            ResAbs = 1e-06;
            ResNor = 1e-10;
        }

        public override string ToString()
        {
            // Basic string representation for text SAT files
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"{VersionToInt(Version)} {Records} {Bodies} {Flags}");
            if (Version >= 2.0)
            {
                sb.AppendLine($"{ProdId.Length} {ProdId} {ProdVer.Length} {ProdVer} {Date.Length} {Date}");
                sb.AppendLine($"{Scale} {ResAbs} {ResNor}");
            }
            return sb.ToString();
        }

        private int VersionToInt(double version)
        {
            int major = (int)version;
            int minor = (int)((version - major) * 100); // Assuming two decimal places for minor
            return major * 100 + minor;
        }
    }
}
