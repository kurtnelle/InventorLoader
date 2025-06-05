using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace InventorLoaderCs
{
    // Minimal StyleReader base class, inheriting from SegmentReader
    public abstract class StyleReader : SegmentReader
    {
        protected StyleReader(RSeSegment segment) : base(segment) { }

        // Placeholder for ReadHeaderSU32S, often a common pattern for style-related attributes
        protected int ReadHeaderSU32S(SecNode node, string typeName = null, StreamWriter logFile = null)
        {
            node.LogAction(logFile, $"StyleReader.ReadHeaderSU32S called for {typeName ?? node.Uid}");
            node.ReadHeader0(logFile); // Assumes a base header structure
            node.ReadUInt32("StyleFlags", logFile); // Example field
            node.ReadChildRef("StyleParentRef", logFile); // Example field
            // ... more common fields for this header type
            return node.CurrentReadOffset;
        }

        // Placeholder for skipBlockSize, which seems to skip based on some internal block size logic
        // or a predefined count. For now, it's a simple skip.
        protected int SkipBlockSize(SecNode node, int currentOffset, int count = 1, StreamWriter logFile = null)
        {
            int bytesToSkip = count * 4; // Example: assume block size is 4 bytes
            node.LogAction(logFile, $"StyleReader.SkipBlockSize: Skipping {bytesToSkip} bytes (count: {count})");
            node.SkipBytes(bytesToSkip, logFile, "BlockSizeSkip");
            return node.CurrentReadOffset;
        }
    }

    public class EeSceneReader : StyleReader
    {
        public List<object> Faces { get; private set; } // Placeholder for parsed face objects
        public List<object> Objects3D { get; private set; } // Placeholder for parsed 3D objects

        public EeSceneReader(RSeSegment segment) : base(segment)
        {
            Faces = new List<object>();
            Objects3D = new List<object>();
        }

        protected override void PopulateDataReaderMethods()
        {
            // UIDs from importerEeScene.py (ensure keys match SecNode.Uid format)
            _dataReaderMethods["120284EF-E23E-4E82-8E3F-55A03A184489"] = Read_120284EF_Attributes; // Python: Read_120284EF
            _dataReaderMethods["5194E9A3-11D3-11D2-911C-0000F8061098"] = Read_5194E9A3_Face;       // Python: Read_5194E9A3
            _dataReaderMethods["A79EACCF-11D1-11D2-910F-0000F8061098"] = Read_A79EACCF_3dObject;  // Python: Read_A79EACCF
            _dataReaderMethods["A79EACD3-11D1-11D2-910F-0000F8061098"] = Read_A79EACD3_Point3D;   // Python: Read_A79EACD3
            _dataReaderMethods["022AC1B1-11D2-0D35-6000-F99AC5361AB0"] = Read_ColorAttr; // From StyleReader in Python, commonly used. It's actually a style property.
            _dataReaderMethods["37DB9D1E-11D2-11D2-9111-0000F8061098"] = Read_37DB9D1E_SurfacePlane; // Example Surface
            // Add more mappings...
        }

        public override void ReadSegmentData(byte[] segmentData, StreamWriter logFile)
        {
            logFile?.WriteLine($"EeSceneReader: Reading segment data for {Segment.Name}. Total size: {segmentData.Length}");
            if (Segment.Nodes != null && Segment.Nodes.Count > 0)
            {
                logFile?.WriteLine($"EeSceneReader: Processing {Segment.Nodes.Count} pre-scanned SecNodes.");
                foreach (var node in Segment.Nodes) ReadBlock(node);
            }
            else logFile?.WriteLine($"EeSceneReader: No pre-scanned nodes for {Segment.Name}. Block identification logic within ReadSegmentData is required.");
            logFile?.WriteLine($"EeSceneReader: Finished reading segment data for {Segment.Name}.");
        }

        // Core helper methods from Python EeSceneReader
        public void ReadHeader3dObject(SecNode node, StreamWriter logFile, string typeName = null, string ref1Name = "numRef")
        {
            node.LogAction(logFile, $"EeSceneReader.ReadHeader3dObject for {typeName ?? node.Uid}");
            node.ReadHeader0(logFile);
            node.ReadUInt32("flags", logFile);
            node.ReadChildRef("styles", logFile);
            node.ReadChildRef(ref1Name, logFile); // Could be 'surface' or 'numRef' etc.
            node.ReadParentRef("parent", logFile);
            node.ReadUInt32("u32_0", logFile);
            SkipBlockSize(node, node.CurrentReadOffset, 1, logFile); // Uses inherited SkipBlockSize
            node.ParsedContent["object3D"] = true; // Mark this node as representing a 3D object
        }

        public void ReadHeaderAttribute(SecNode node, StreamWriter logFile, string typeName = null)
        {
            node.LogAction(logFile, $"EeSceneReader.ReadHeaderAttribute for {typeName ?? node.Uid}");
            ReadHeaderSU32S(node, typeName, logFile); // Uses inherited ReadHeaderSU32S
            node.ReadUInt8("u8_0", logFile);
            SkipBlockSize(node, node.CurrentReadOffset, 1, logFile);
        }

        // Specific Read_* methods
        public void Read_ColorAttr(SecNode node) // Matches Python's Read_ColorAttr
        {
            Logger.Info($"EeSceneReader: Reading ColorAttr (UID: {node.Uid})");
            SkipBlockSize(node, node.CurrentReadOffset, 1, Logger.LogWriter); // Matches the `i = self.skipBlockSize(offset)`
            node.ReadUInt8Array("Color.a0", 2, Logger.LogWriter);
            node.ReadColorRgba("Color.c0", Logger.LogWriter);
            node.ReadColorRgba("Color.diffuse", Logger.LogWriter);
            node.ReadColorRgba("Color.c2", Logger.LogWriter);
            node.ReadColorRgba("Color.c3", Logger.LogWriter);
            node.ReadUInt16Array("Color.a5", 2, Logger.LogWriter);
            node.ParsedContent["Summary"] = "Parsed Color Attribute";
        }

        private void Read_120284EF_Attributes(SecNode node)
        {
            Logger.Info($"EeSceneReader: Reading Attributes Collection (UID: {node.Uid})");
            ReadHeaderSU32S(node, "AttributesCollection", Logger.LogWriter);
            node.ReadUInt8("u8_0", Logger.LogWriter);
            SkipBlockSize(node, node.CurrentReadOffset, 1, Logger.LogWriter);
            // node.ReadList2(i, importerSegNode._TYP_NODE_REF_, 'attributes') - List parsing deferred
            node.ParsedContent["Summary"] = "Parsed Attributes Collection";
            Logger.Warning("EeSceneReader: Read_120284EF_Attributes list reading part is deferred.");
        }

        private void Read_5194E9A3_Face(SecNode node)
        {
            Logger.Info($"EeSceneReader: Reading Face (UID: {node.Uid})");
            ReadHeader3dObject(node, Logger.LogWriter, "Face", ref1Name: "surfaceRef");
            // node.ReadList2(i, importerSegNode._TYP_NODE_REF_, 'edges') - List parsing deferred
            node.ReadUInt8("u8_0", Logger.LogWriter);
            SkipBlockSize(node, node.CurrentReadOffset, 2, Logger.LogWriter);
            node.ReadFloat64Array("boundingBox", 6, Logger.LogWriter);
            SkipBlockSize(node, node.CurrentReadOffset, 1, Logger.LogWriter);
            node.ReadUInt32("key", Logger.LogWriter);
            node.ReadUInt32("u32_1", Logger.LogWriter);
            node.ReadUInt32("u32_2", Logger.LogWriter);
            Faces.Add(node.ParsedContent); // Store parsed data
            node.ParsedContent["Summary"] = "Parsed Face";
            Logger.Warning("EeSceneReader: Read_5194E9A3_Face list reading for edges is deferred.");
        }

        private void Read_A79EACCF_3dObject(SecNode node)
        {
            Logger.Info($"EeSceneReader: Reading Generic 3dObject (UID: {node.Uid})");
            ReadHeader3dObject(node, Logger.LogWriter, "Generic3dObject", ref1Name: "childObjectsRef");
            // node.ReadList2 for child objects deferred
            // ReadOptionalTransformation(node, i) - Transformation reading deferred
            Objects3D.Add(node.ParsedContent);
            node.ParsedContent["Summary"] = "Parsed Generic 3dObject";
            Logger.Warning("EeSceneReader: Read_A79EACCF_3dObject list and transformation reading is deferred.");
        }

        private void Read_A79EACD3_Point3D(SecNode node)
        {
            Logger.Info($"EeSceneReader: Reading Point3D (UID: {node.Uid})");
            ReadHeader3dObject(node, Logger.LogWriter, "Point3D");
            node.ReadVector3DFloat64("Position", Logger.LogWriter);
            node.ReadFloat32("f32_0", Logger.LogWriter);
            node.ReadSInt32("s32_0", Logger.LogWriter);
            node.ReadUInt16("u16_0", Logger.LogWriter);
            node.ParsedContent["Summary"] = "Parsed Point3D";
        }

        private void Read_37DB9D1E_SurfacePlane(SecNode node)
        {
            Logger.Info($"EeSceneReader: Reading SurfacePlane (UID: {node.Uid})");
            // Simplified from ReadHeaderSurface
            node.LogAction(Logger.LogWriter, "EeSceneReader.ReadHeaderSurface for SurfacePlane");
            SkipBlockSize(node, node.CurrentReadOffset, 2, Logger.LogWriter);
            node.ReadParentRef("parentRef", Logger.LogWriter);
            SkipBlockSize(node, node.CurrentReadOffset, 1, Logger.LogWriter);
            // node.ReadList2 for dcIndices deferred
            node.ReadUInt32("u32_0", Logger.LogWriter);
            node.ReadUInt8("u8_0", Logger.LogWriter);
            node.ParsedContent["Summary"] = "Parsed SurfacePlane";
            Logger.Warning("EeSceneReader: Read_37DB9D1E_SurfacePlane list reading for dcIndices is deferred.");
        }
    }

    public class GraphicsReader : EeSceneReader
    {
        public GraphicsReader(RSeSegment segment) : base(segment) { }

        protected override void PopulateDataReaderMethods()
        {
            base.PopulateDataReaderMethods(); // Include EeSceneReader's methods

            // UIDs from importerGraphics.py
            _dataReaderMethods["4B26ED59-E839-11D1-8D78-0008C75E7068"] = Read_4B26ED59_Mesh;         // Python: Read_4B26ED59 (Mesh)
            _dataReaderMethods["60FD1845-11D0-D79D-0008BFBB21EDDC09"] = Read_60FD1845_Sketch2D;    // Python: Read_60FD1845 (Sketch2D)
            _dataReaderMethods["CA7163A3-11D0-D3B2-0008BFBB21EDDC09"] = Read_CA7163A3_PartNode;     // Python: Read_CA7163A3 (PartNode)
            // Add more GraphicsReader specific UIDs
        }

        // ReadSegmentData can often be inherited if block processing is standardized via Segment.Nodes
        // If Graphics segments have a unique top-level structure, override it.
        // public override void ReadSegmentData(byte[] segmentData, StreamWriter logFile)
        // {
        //     base.ReadSegmentData(segmentData, logFile); // Or custom logic
        // }

        private void Read_4B26ED59_Mesh(SecNode node)
        {
            Logger.Info($"GraphicsReader: Reading Mesh (UID: {node.Uid})");
            // ReadHeaderU32RefU8List3(node, 'Mesh', 'parts') - This pattern is common in Python
            node.LogAction(Logger.LogWriter, "GraphicsReader.ReadHeaderU32RefU8List3 pattern for Mesh");
            node.ReadHeader0(Logger.LogWriter);
            node.ReadUInt32("MeshFlags", Logger.LogWriter); // Placeholder for u32_0 from header
            node.ReadChildRef("StyleOrParentRef", Logger.LogWriter); // Placeholder for ref0
            node.ReadUInt8("ItemCount", Logger.LogWriter); // Placeholder for u8_0
            // node.ReadList3 for parts deferred
            node.ReadParentRef("DCModelRef", Logger.LogWriter); // Placeholder for parentRef from header
            node.ReadUInt32("u32_1", Logger.LogWriter);
            node.ReadSInt32("DCIndex", Logger.LogWriter); // ReadIndexDC
            if (Version.IsGreaterThan(2017,0,0)) node.SkipBytes(4, Logger.LogWriter, "Version>2017 skip");
            node.ParsedContent["Summary"] = "Parsed Mesh";
            Logger.Warning("GraphicsReader: Read_4B26ED59_Mesh list reading and detailed header is deferred.");
        }

        private void Read_60FD1845_Sketch2D(SecNode node)
        {
            Logger.Info($"GraphicsReader: Reading Sketch2D (UID: {node.Uid})");
            node.LogAction(Logger.LogWriter, "GraphicsReader.ReadHeaderU32RefU8List3 pattern for Sketch2D");
            node.ReadHeader0(Logger.LogWriter); // u32_0, ref0, u8_0
            node.ReadChildRef("SketchObjectRef", Logger.LogWriter); // obj ref
            SkipBlockSize(node, node.CurrentReadOffset, 1, Logger.LogWriter);
            node.ReadUInt8("u8_1", Logger.LogWriter);
            SkipBlockSize(node, node.CurrentReadOffset, 1, Logger.LogWriter);
            node.ReadUInt32("Key", Logger.LogWriter); // 'index' in Python
            // ReadTransformation3D - complex, deferred
            node.SkipBytes(12*4 + 3*4, Logger.LogWriter, "Transformation3D placeholder"); // Approximate size of Transform
            // ReadList6 (x2), ReadList6 - deferred
            node.ParsedContent["Summary"] = "Parsed Sketch2D";
            Logger.Warning("GraphicsReader: Read_60FD1845_Sketch2D lists and transformation reading is deferred.");
        }

        private void Read_CA7163A3_PartNode(SecNode node)
        {
            Logger.Info($"GraphicsReader: Reading PartNode (UID: {node.Uid})");
            node.LogAction(Logger.LogWriter, "GraphicsReader.ReadHeaderU32RefU8List3 pattern for PartNode");
            node.ReadHeader0(Logger.LogWriter); // u32_0, ref0, u8_0
            // ReadList3 for items deferred
            node.ReadUInt32("u32_1", Logger.LogWriter);
            // ReadList6 for outlines deferred
            SkipBlockSize(node, node.CurrentReadOffset, 1, Logger.LogWriter);
            node.ReadMaterial("MaterialInfo", Logger.LogWriter);
            node.ParsedContent["Summary"] = "Parsed PartNode";
            Logger.Warning("GraphicsReader: Read_CA7163A3_PartNode list and detailed header reading is deferred.");
        }
    }


    public class AppReader : SegmentReader
    {
        private ColorPlaceholder? _defaultColor;

        public AppReader(RSeSegment segment) : base(segment) { }

        protected override void PopulateDataReaderMethods()
        {
            _dataReaderMethods["10389219-5734-4737-9C3E-95271D857901"] = Read_ApplicationProperties;
            _dataReaderMethods["10D6C06B-73C0-4ACA-A268-550F83D0339F"] = Read_DefaultStyle;
            _dataReaderMethods["11FBECCD-B29E-4057-A277-75611C356307"] = Read_StyleDefinitions;
            _dataReaderMethods["A27E58F3-2452-4687-A07E-F69DECF97E6B"] = Read_ReferencedFiles;
            _dataReaderMethods["B080E131-F87B-4A7F-A0CE-B0E341018F37"] = Read_UnitsOfMeasure;
        }

        public override void ReadSegmentData(byte[] segmentData, StreamWriter logFile)
        {
            logFile?.WriteLine($"AppReader: Reading segment data for {Segment.Name}. Total size: {segmentData.Length}");
            if (Segment.Nodes != null && Segment.Nodes.Count > 0)
            {
                logFile?.WriteLine($"AppReader: Processing {Segment.Nodes.Count} pre-scanned SecNodes.");
                foreach (var node in Segment.Nodes) ReadBlock(node);
                PostRead(logFile);
            }
            else logFile?.WriteLine($"AppReader: No pre-scanned nodes for {Segment.Name}.");
            logFile?.WriteLine($"AppReader: Finished reading segment data for {Segment.Name}.");
        }

        public void PostRead(StreamWriter logFile)
        {
            logFile?.WriteLine("AppReader: Executing PostRead logic.");
            if (_defaultColor.HasValue)
            {
                 Logger.Info($"AppReader PostRead: Default color (from field) set to {_defaultColor.Value}");
            }
            else
            {
                var defaultStyleNode = Segment.Nodes.FirstOrDefault(n => n.Uid == "10D6C06B-73C0-4ACA-A268-550F83D0339F" && n.ParsedContent.ContainsKey("Color"));
                if (defaultStyleNode != null && defaultStyleNode.ParsedContent["Color"] is ColorPlaceholder color)
                {
                     Logger.Info($"AppReader PostRead: Default color (from parsed node) set to {color}");
                }
            }
        }

        private void Read_ApplicationProperties(SecNode node)
        {
            Logger.Info($"AppReader: Reading ApplicationProperties (UID: {node.Uid})");
            node.ReadHeader0(Logger.LogWriter);
            node.ReadLen32Text16("ApplicationName", Logger.LogWriter);
            node.ReadUInt32("AppVersionMajor", Logger.LogWriter);
            node.ParsedContent["Summary"] = "Parsed Application Properties";
        }

        private void Read_DefaultStyle(SecNode node)
        {
            Logger.Info($"AppReader: Reading DefaultStyle (UID: {node.Uid})");
            node.ReadHeader0(Logger.LogWriter);
            _defaultColor = node.ReadColorRgba("Color", Logger.LogWriter);
            node.ParsedContent["Summary"] = "Parsed Default Style";
        }

        private void Read_StyleDefinitions(SecNode node)
        {
            Logger.Info($"AppReader: Reading StyleDefinitions (UID: {node.Uid})");
            node.ReadHeader0(Logger.LogWriter);
            node.ParsedContent["Summary"] = "Parsed Style Definitions";
        }

        private void Read_ReferencedFiles(SecNode node)
        {
            Logger.Info($"AppReader: Reading ReferencedFiles (UID: {node.Uid})");
            node.ReadHeader0(Logger.LogWriter);
            node.ParsedContent["Summary"] = "Parsed Referenced Files";
        }

        private void Read_UnitsOfMeasure(SecNode node)
        {
            Logger.Info($"AppReader: Reading UnitsOfMeasure (UID: {node.Uid})");
            node.ReadHeader0(Logger.LogWriter);
            node.ReadUInt32("UnitsFlags", Logger.LogWriter);
            node.ReadFloat64("LinearConversionFactor", Logger.LogWriter);
            node.ParsedContent["Summary"] = "Parsed Units Of Measure";
        }
    }

    public class BRepReader : SegmentReader
    {
        public const string AcisBinaryDataUID = "E9132E94-8726-11D1-8D6C-0008C75E7068";
        public const string AcisTextDataUID = "009A1CC4-8727-11D1-8D6C-0008C75E7068";

        public BRepReader(RSeSegment segment) : base(segment) { }

        protected override void PopulateDataReaderMethods()
        {
            _dataReaderMethods[AcisBinaryDataUID] = Read_AcisBinaryData;
            _dataReaderMethods[AcisTextDataUID] = Read_AcisTextData;
            _dataReaderMethods["D5E25341"] = Read_D5E25341;
        }

        public override void ReadSegmentData(byte[] segmentData, StreamWriter logFile)
        {
            logFile?.WriteLine($"BRepReader: Reading segment data for {Segment.Name}. Total size: {segmentData.Length}");
            if (Segment.Nodes != null && Segment.Nodes.Count > 0)
            {
                logFile?.WriteLine($"BRepReader: Processing {Segment.Nodes.Count} pre-scanned SecNodes.");
                foreach (var node in Segment.Nodes) ReadBlock(node);
            }
            else
            {
                if (segmentData.Length > 0)
                {
                    Logger.Warning($"BRepReader: No pre-scanned nodes for {Segment.Name}. Attempting to read segment data as a single ACIS block. This requires knowing if it's text or binary ACIS.");
                    SecNode acisNode = new SecNode(AcisBinaryDataUID, segmentData, 0, segmentData.Length);
                    Read_AcisBinaryData(acisNode);
                }
            }
            logFile?.WriteLine($"BRepReader: Finished reading segment data for {Segment.Name}.");
        }

        private void Read_AcisBinaryData(SecNode node)
        {
            Logger.Info($"BRepReader: Reading AcisBinaryData (UID: {node.Uid}) for segment {Segment.Name}");
            node.ReadHeader0(Logger.LogWriter);
            byte[] acisData = node.ReadBytes("AcisRawData", node.Size - (node.CurrentReadOffset - node.Offset), Logger.LogWriter);
            node.ParsedContent["AcisDataSize"] = acisData?.Length ?? 0;
            Logger.Info($"BRepReader: Stored reference to ACIS binary data, size: {acisData?.Length ?? 0}.");
        }

        private void Read_AcisTextData(SecNode node)
        {
            Logger.Info($"BRepReader: Reading AcisTextData (UID: {node.Uid}) for segment {Segment.Name}");
            node.ReadHeader0(Logger.LogWriter);
            string acisText = Encoding.ASCII.GetString(node.FullDataBuffer, node.CurrentReadOffset, node.Size - (node.CurrentReadOffset - node.Offset));
            node.CurrentReadOffset = node.Offset + node.Size;
            node.ParsedContent["AcisTextData"] = acisText;
            node.ParsedContent["AcisTextDataLength"] = acisText.Length;
            Logger.Info($"BRepReader: Stored ACIS text data, length: {acisText.Length}.");
        }

        private void Read_D5E25341(SecNode node)
        {
            Logger.Info($"BRepReader: Reading D5E25341 (UID: {node.Uid}) for segment {Segment.Name}");
            node.ReadHeader0(Logger.LogWriter);
            node.ReadUInt32("UnknownProp1_D5E25341", Logger.LogWriter);
            node.ReadUInt32("UnknownProp2_D5E25341", Logger.LogWriter);
            node.ParsedContent["Summary"] = "Parsed D5E25341 block";
        }
    }

    public class DCReader : SegmentReader
    {
        public DCReader(RSeSegment segment) : base(segment) { }

        protected override void PopulateDataReaderMethods()
        {
            _dataReaderMethods["90874D16-11D0-D1F8-0008CABC0663DC09"] = Read_RDxPart;
            _dataReaderMethods["CE52DF3A-11D0-D2D0-0008CCBC0663DC09"] = Read_RDxLine2;
            _dataReaderMethods["CA7163A3-11D0-D3B2-0008BFBB21EDDC09"] = Read_PMxPartNode;
            _dataReaderMethods["D31891C2-48BF-14C3-AA42-EA872A846B2A"] = Read_UFRxRef;
        }

        public override void ReadSegmentData(byte[] segmentData, StreamWriter logFile)
        {
            logFile?.WriteLine($"DCReader: Reading segment data for {Segment.Name}. Total size: {segmentData.Length}");
            if (Segment.Nodes != null && Segment.Nodes.Count > 0)
            {
                logFile?.WriteLine($"DCReader: Processing {Segment.Nodes.Count} pre-scanned SecNodes.");
                foreach (var node in Segment.Nodes) ReadBlock(node);
            }
            else
            {
                 logFile?.WriteLine($"DCReader: No pre-scanned nodes for {Segment.Name}. Block identification logic within ReadSegmentData is required.");
            }
            logFile?.WriteLine($"DCReader: Finished reading segment data for {Segment.Name}.");
        }

        private void Read_RDxPart(SecNode node)
        {
            Logger.Info($"DCReader: Reading RDxPart (UID: {node.Uid})");
            node.ReadHeader0(Logger.LogWriter);
            node.ReadUInt32("PartFlags", Logger.LogWriter);
            node.ReadLen32Text16("PartName", Logger.LogWriter);
            node.ParsedContent["Summary"] = "Parsed RDxPart";
        }

        private void Read_RDxLine2(SecNode node)
        {
            Logger.Info($"DCReader: Reading RDxLine2 (UID: {node.Uid})");
            node.ReadHeader0(Logger.LogWriter);
            node.ReadGuid("LineID", Logger.LogWriter);
            node.ReadCrossRef("StartPointRef", Logger.LogWriter, "RDxPoint2");
            node.ReadCrossRef("EndPointRef", Logger.LogWriter, "RDxPoint2");
            node.ParsedContent["Summary"] = "Parsed RDxLine2";
        }

        private void Read_PMxPartNode(SecNode node)
        {
            Logger.Info($"DCReader: Reading PMxPartNode (UID: {node.Uid})");
            node.ReadHeader0(Logger.LogWriter);
            node.ReadLen32Text16("NodeName", Logger.LogWriter);
            node.ReadCrossRef("ReferencedPart", Logger.LogWriter, "RDxPart");
            node.ParsedContent["Summary"] = "Parsed PMxPartNode";
        }

        private void Read_UFRxRef(SecNode node)
        {
            Logger.Info($"DCReader: Reading UFRxRef (UID: {node.Uid})");
            node.ReadHeader0(Logger.LogWriter);
            node.ReadUInt32("RefType", Logger.LogWriter);
            node.ReadLen32Text16("RefName", Logger.LogWriter);
            node.ReadGuid("RefObjectGuid", Logger.LogWriter);
            node.ParsedContent["Summary"] = "Parsed UFRxRef";
        }
    }

    public class DirectoryReader : SegmentReader
    {
        public const string DirHeaderUID = "685F7AF4";
        public const string DirEntriesUID = "3E9F410E";

        public DirectoryReader(RSeSegment segment) : base(segment) { }

        protected override void PopulateDataReaderMethods()
        {
            _dataReaderMethods[DirHeaderUID] = Read_DirHeader;
            _dataReaderMethods[DirEntriesUID] = Read_DirEntries;
        }

        public override void ReadSegmentData(byte[] segmentData, StreamWriter logFile)
        {
            logFile?.WriteLine($"DirectoryReader: Reading segment data for {Segment.Name}. Total size: {segmentData.Length}");
            if (Segment.Nodes != null && Segment.Nodes.Count > 0)
            {
                logFile?.WriteLine($"DirectoryReader: Processing {Segment.Nodes.Count} pre-scanned SecNodes.");
                foreach (var node in Segment.Nodes) ReadBlock(node);
            }
            else if (segmentData.Length > 0)
            {
                logFile?.WriteLine($"DirectoryReader: No pre-scanned nodes. Attempting to read known blocks.");
                int currentOffset = 0;
                if (segmentData.Length > 4)
                {
                    int block1Size = Math.Min(100, segmentData.Length - currentOffset);
                    SecNode node1 = new SecNode(DirHeaderUID, segmentData, currentOffset, block1Size);
                    ReadBlock(node1);
                    currentOffset += (node1.CurrentReadOffset - node1.Offset);

                    if (currentOffset < segmentData.Length -4)
                    {
                         int block2Size = segmentData.Length - currentOffset;
                         SecNode node2 = new SecNode(DirEntriesUID, segmentData, currentOffset, block2Size);
                         ReadBlock(node2);
                    }
                }
            }
            else logFile?.WriteLine($"DirectoryReader: Segment data is empty for {Segment.Name}.");
            logFile?.WriteLine($"DirectoryReader: Finished reading segment data for {Segment.Name}.");
        }

        private void Read_DirHeader(SecNode node)
        {
            Logger.Info($"DirectoryReader: Reading DirHeader (UID: {node.Uid}) for segment {Segment.Name}");
            node.ReadHeader0(Logger.LogWriter);
            node.ParsedContent["Summary"] = $"Parsed content for DirHeader (UID {node.Uid})";
            Logger.Info($"DirectoryReader: Finished Read_DirHeader for UID {node.Uid}. Offset: {node.CurrentReadOffset}");
        }

        private void Read_DirEntries(SecNode node)
        {
            Logger.Info($"DirectoryReader: Reading DirEntries (UID: {node.Uid}) for segment {Segment.Name}");
            node.ReadHeader0(Logger.LogWriter);
            node.ParsedContent["Summary"] = $"Parsed content for DirEntries (UID {node.Uid})";
            Logger.Info($"DirectoryReader: Finished Read_DirEntries for UID {node.Uid}. Offset: {node.CurrentReadOffset}");
        }
    }

    public static class Logger
    {
        public static StreamWriter LogWriter { get; set; }

        public static void Info(string message) { LogWriter?.WriteLine($"INFO: {message}"); Console.WriteLine($"INFO: {message}"); }
        public static void Warning(string message) { LogWriter?.WriteLine($"WARNING: {message}"); Console.WriteLine($"WARNING: {message}"); }
        public static void Error(string message) { LogWriter?.WriteLine($"ERROR: {message}"); Console.WriteLine($"ERROR: {message}"); }
    }
}
