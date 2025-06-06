namespace InventorLoaderCs
{
    public class AcisHeader
    {
        public double Version { get; set; }
        public int Records { get; set; }
        public int Bodies { get; set; }
        public string ProductId { get; set; }
        public string ProdVer { get; set; } // Product Version
        public string Date { get; set; }
        public double Scale { get; set; }
        public double ResAbs { get; set; } // Absolute Resolution
        public double ResNor { get; set; } // Normal Resolution
        public string Format { get; set; } // "ACIS BinaryFile" or "ASM BinaryFile" or "ACIS Text" (though text is not stored here)

        // Flags for binary format variations
        public bool Is64BitRecordIndices { get; set; } = false; // For TAG_ENTITY_REF index size
        public bool Is64BitEnums { get; set; } = false;         // For TAG_ENUM_VALUE size
        public bool IsAsmBinaryFile8Format { get; set; } = false; // For TAG_LONG size and general ASM8 specifics

        public AcisHeader()
        {
            Scale = 1.0; // Default scale
        }
    }
}
