using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace InventorLoaderCs
{
    public abstract class StyleReader : SegmentReader
    {
        protected StyleReader(RSeSegment segment) : base(segment) { }

        protected int ReadHeaderSU32S(SecNode node, string typeName = null, StreamWriter logFile = null)
        {
            node.LogAction(logFile, $"StyleReader.ReadHeaderSU32S called for {typeName ?? node.Uid}");
            node.ReadHeader0(logFile);
            node.ReadUInt32("StyleFlags", logFile);
            node.ReadChildRef("StyleParentRef", logFile);
            return node.CurrentReadOffset;
        }

        protected int SkipBlockSize(SecNode node, int currentOffsetToUse , int count = 1, StreamWriter logFile = null)
        {
            int bytesToSkip = count * 4;
            node.LogAction(logFile, $"StyleReader.SkipBlockSize: Skipping {bytesToSkip} bytes (count: {count}) from offset {node.CurrentReadOffset}.");
            node.SkipBytes(bytesToSkip, logFile, "BlockSizeSkip");
            return node.CurrentReadOffset;
        }
    }

    public class EeSceneReader : StyleReader
    {
        public List<object> Faces { get; private set; }
        public List<object> Objects3D { get; private set; }

        public EeSceneReader(RSeSegment segment) : base(segment)
        {
            Faces = new List<object>();
            Objects3D = new List<object>();
        }

        protected override void PopulateDataReaderMethods()
        {
            _dataReaderMethods["120284EF-E23E-4E82-8E3F-55A03A184489"] = Read_120284EF_Attributes;
            _dataReaderMethods["5194E9A3-11D3-11D2-911C-0000F8061098"] = Read_5194E9A3_Face;
            _dataReaderMethods["A79EACCF-11D1-11D2-910F-0000F8061098"] = Read_A79EACCF_3dObject;
            _dataReaderMethods["A79EACD3-11D1-11D2-910F-0000F8061098"] = Read_A79EACD3_Point3D;
            _dataReaderMethods["022AC1B1-11D2-0D35-6000-F99AC5361AB0"] = Read_ColorAttr;
            _dataReaderMethods["37DB9D1E-11D2-11D2-9111-0000F8061098"] = Read_37DB9D1E_SurfacePlane;
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

        public void ReadHeader3dObject(SecNode node, StreamWriter logFile, string typeName = null, string ref1Name = "numRef")
        {
            node.LogAction(logFile, $"EeSceneReader.ReadHeader3dObject for {typeName ?? node.Uid}");
            node.ReadHeader0(logFile);
            node.ReadUInt32("flags", logFile);
            node.ReadChildRef("styles", logFile);
            node.ReadChildRef(ref1Name, logFile);
            node.ReadParentRef("parent", logFile);
            node.ReadUInt32("u32_0", logFile);
            SkipBlockSize(node, node.CurrentReadOffset, 1, logFile);
            node.ParsedContent["object3D"] = true;
        }

        public void ReadHeaderAttribute(SecNode node, StreamWriter logFile, string typeName = null)
        {
            node.LogAction(logFile, $"EeSceneReader.ReadHeaderAttribute for {typeName ?? node.Uid}");
            ReadHeaderSU32S(node, typeName, logFile);
            node.ReadUInt8("u8_0", logFile);
            SkipBlockSize(node, node.CurrentReadOffset, 1, logFile);
        }

        public void Read_ColorAttr(SecNode node)
        {
            Logger.Info($"EeSceneReader: Reading ColorAttr (UID: {node.Uid})");
            SkipBlockSize(node, node.CurrentReadOffset, 1, Logger.LogWriter);
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
            node.ParsedContent["Summary"] = "Parsed Attributes Collection";
            Logger.Warning("EeSceneReader: Read_120284EF_Attributes list reading part is deferred.");
        }

        private void Read_5194E9A3_Face(SecNode node)
        {
            Logger.Info($"EeSceneReader: Reading Face (UID: {node.Uid})");
            ReadHeader3dObject(node, Logger.LogWriter, "Face", ref1Name: "surfaceRef");
            node.ReadUInt8("u8_0", Logger.LogWriter);
            SkipBlockSize(node, node.CurrentReadOffset, 2, Logger.LogWriter);
            node.ReadFloat64Array("boundingBox", 6, Logger.LogWriter);
            SkipBlockSize(node, node.CurrentReadOffset, 1, Logger.LogWriter);
            node.ReadUInt32("key", Logger.LogWriter);
            node.ReadUInt32("u32_1", Logger.LogWriter);
            node.ReadUInt32("u32_2", Logger.LogWriter);
            Faces.Add(node.ParsedContent);
            node.ParsedContent["Summary"] = "Parsed Face";
            Logger.Warning("EeSceneReader: Read_5194E9A3_Face list reading for edges is deferred.");
        }

        private void Read_A79EACCF_3dObject(SecNode node)
        {
            Logger.Info($"EeSceneReader: Reading Generic 3dObject (UID: {node.Uid})");
            ReadHeader3dObject(node, Logger.LogWriter, "Generic3dObject", ref1Name: "childObjectsRef");
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
            node.LogAction(Logger.LogWriter, "EeSceneReader.ReadHeaderSurface for SurfacePlane");
            SkipBlockSize(node, node.CurrentReadOffset, 2, Logger.LogWriter);
            node.ReadParentRef("parentRef", Logger.LogWriter);
            SkipBlockSize(node, node.CurrentReadOffset, 1, Logger.LogWriter);
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
            base.PopulateDataReaderMethods();

            _dataReaderMethods["4B26ED59-E839-11D1-8D78-0008C75E7068"] = Read_4B26ED59_Mesh;
            _dataReaderMethods["60FD1845-11D0-D79D-0008BFBB21EDDC09"] = Read_60FD1845_Sketch2D;
            _dataReaderMethods["CA7163A3-11D0-D3B2-0008BFBB21EDDC09"] = Read_CA7163A3_PartNode;
        }

        private void Read_4B26ED59_Mesh(SecNode node)
        {
            Logger.Info($"GraphicsReader: Reading Mesh (UID: {node.Uid})");
            node.LogAction(Logger.LogWriter, "GraphicsReader.ReadHeaderU32RefU8List3 pattern for Mesh");
            node.ReadHeader0(Logger.LogWriter);
            node.ReadUInt32("MeshFlags", Logger.LogWriter);
            node.ReadChildRef("StyleOrParentRef", Logger.LogWriter);
            node.ReadUInt8("ItemCount", Logger.LogWriter);
            node.ReadParentRef("DCModelRef", Logger.LogWriter);
            node.ReadUInt32("u32_1", Logger.LogWriter);
            node.ReadSInt32("DCIndex", Logger.LogWriter);
            if (Version.IsGreaterThan(2017,0,0)) node.SkipBytes(4, Logger.LogWriter, "Version>2017 skip");
            node.ParsedContent["Summary"] = "Parsed Mesh";
            Logger.Warning("GraphicsReader: Read_4B26ED59_Mesh list reading and detailed header is deferred.");
        }

        private void Read_60FD1845_Sketch2D(SecNode node)
        {
            Logger.Info($"GraphicsReader: Reading Sketch2D (UID: {node.Uid})");
            node.LogAction(Logger.LogWriter, "GraphicsReader.ReadHeaderU32RefU8List3 pattern for Sketch2D");
            node.ReadHeader0(Logger.LogWriter);
            node.ReadChildRef("SketchObjectRef", Logger.LogWriter);
            SkipBlockSize(node, node.CurrentReadOffset, 1, Logger.LogWriter);
            node.ReadUInt8("u8_1", Logger.LogWriter);
            SkipBlockSize(node, node.CurrentReadOffset, 1, Logger.LogWriter);
            node.ReadUInt32("Key", Logger.LogWriter);
            node.SkipBytes(12*4 + 3*4, Logger.LogWriter, "Transformation3D placeholder");
            node.ParsedContent["Summary"] = "Parsed Sketch2D";
            Logger.Warning("GraphicsReader: Read_60FD1845_Sketch2D lists and transformation reading is deferred.");
        }

        private void Read_CA7163A3_PartNode(SecNode node)
        {
            Logger.Info($"GraphicsReader: Reading PartNode (UID: {node.Uid})");
            node.LogAction(Logger.LogWriter, "GraphicsReader.ReadHeaderU32RefU8List3 pattern for PartNode");
            node.ReadHeader0(Logger.LogWriter);
            node.ReadUInt32("u32_1", Logger.LogWriter);
            SkipBlockSize(node, node.CurrentReadOffset, 1, Logger.LogWriter);
            node.ReadMaterial("MaterialInfo", Logger.LogWriter);
            node.ParsedContent["Summary"] = "Parsed PartNode";
            Logger.Warning("GraphicsReader: Read_CA7163A3_PartNode list and detailed header reading is deferred.");
        }
    }

    public class FBAttributeReader : SegmentReader
    {
        public FBAttributeReader(RSeSegment segment) : base(segment) { }

        protected override void PopulateDataReaderMethods()
        {
            _dataReaderMethods["080ED92F-E23E-4E82-8E3F-55A03A184489"] = Read_FB_GenericAttribute; // Example UID
            _dataReaderMethods["28C25C43-11D1-11D2-910F-0000F8061098"] = Read_FB_StringAttribute;  // Example UID
        }

        public override void ReadSegmentData(byte[] segmentData, StreamWriter logFile)
        {
            logFile?.WriteLine($"FBAttributeReader: Reading segment data for {Segment.Name}. Total size: {segmentData.Length}");
            if (Segment.Nodes != null && Segment.Nodes.Count > 0)
            {
                foreach (var node in Segment.Nodes) ReadBlock(node);
            }
            else logFile?.WriteLine($"FBAttributeReader: No pre-scanned nodes for {Segment.Name}.");
            logFile?.WriteLine($"FBAttributeReader: Finished reading for {Segment.Name}.");
        }

        private void Read_FB_GenericAttribute(SecNode node)
        {
            Logger.Info($"FBAttributeReader: Reading FB_GenericAttribute (UID: {node.Uid})");
            node.ReadHeader0(Logger.LogWriter);
            node.ReadUInt32("AttributeFlags", Logger.LogWriter);
            node.ReadLen32Text16("AttributeName", Logger.LogWriter);
            node.ParsedContent["Summary"] = "Parsed FB_GenericAttribute";
        }

        private void Read_FB_StringAttribute(SecNode node)
        {
            Logger.Info($"FBAttributeReader: Reading FB_StringAttribute (UID: {node.Uid})");
            node.ReadHeader0(Logger.LogWriter);
            node.ReadLen32Text16("StringValue", Logger.LogWriter);
            node.ParsedContent["Summary"] = "Parsed FB_StringAttribute";
        }
    }

    public class NotebookReader : SegmentReader
    {
        public NotebookReader(RSeSegment segment) : base(segment) { }

        protected override void PopulateDataReaderMethods()
        {
            _dataReaderMethods["D81CDE47-11D2-65F7-6000-5DBEAD9287B0"] = Read_NBxEntry;
            _dataReaderMethods["74E34413-11D2-5AEB-6000-5BBEAD9287B0"] = Read_NBxFolder;
        }

        public override void ReadSegmentData(byte[] segmentData, StreamWriter logFile)
        {
            logFile?.WriteLine($"NotebookReader: Reading segment data for {Segment.Name}. Total size: {segmentData.Length}");
            if (Segment.Nodes != null && Segment.Nodes.Count > 0)
            {
                foreach (var node in Segment.Nodes) ReadBlock(node);
            }
            else logFile?.WriteLine($"NotebookReader: No pre-scanned nodes for {Segment.Name}.");
            logFile?.WriteLine($"NotebookReader: Finished reading for {Segment.Name}.");
        }

        private void Read_NBxEntry(SecNode node)
        {
            Logger.Info($"NotebookReader: Reading NBxEntry (UID: {node.Uid})");
            node.ReadHeader0(Logger.LogWriter);
            node.ReadLen32Text16("EntryName", Logger.LogWriter);
            node.ReadUInt32("EntryType", Logger.LogWriter);
            node.ParsedContent["Summary"] = "Parsed NBxEntry";
        }

        private void Read_NBxFolder(SecNode node)
        {
            Logger.Info($"NotebookReader: Reading NBxFolder (UID: {node.Uid})");
            node.ReadHeader0(Logger.LogWriter);
            node.ReadLen32Text16("FolderName", Logger.LogWriter);
            node.ParsedContent["Summary"] = "Parsed NBxFolder";
            Logger.Warning("NotebookReader: Child entry parsing in Read_NBxFolder is deferred.");
        }
    }

    public class ResultReader : SegmentReader
    {
        public ResultReader(RSeSegment segment) : base(segment) { }

        protected override void PopulateDataReaderMethods()
        {
            _dataReaderMethods["PMxResultSegmentType_SomeUID"] = Read_ResultDataBlock; // Example
        }

        public override void ReadSegmentData(byte[] segmentData, StreamWriter logFile)
        {
            logFile?.WriteLine($"ResultReader: Reading segment data for {Segment.Name}. Total size: {segmentData.Length}");
            if (Segment.Nodes != null && Segment.Nodes.Count > 0)
            {
                foreach (var node in Segment.Nodes) ReadBlock(node);
            }
            else logFile?.WriteLine($"ResultReader: No pre-scanned nodes for {Segment.Name}.");
            logFile?.WriteLine($"ResultReader: Finished reading for {Segment.Name}.");
        }

        private void Read_ResultDataBlock(SecNode node)
        {
            Logger.Info($"ResultReader: Reading ResultDataBlock (UID: {node.Uid})");
            node.ReadHeader0(Logger.LogWriter);
            node.ReadUInt32("ResultStatus", Logger.LogWriter);
            node.ReadFloat64("ResultValue", Logger.LogWriter);
            node.ParsedContent["Summary"] = "Parsed ResultDataBlock";
        }
    }

    public class NameTableReader : SegmentReader
    {
        public const string NameTableDataUID = "CCE92042-C27B-11D1-8D6C-0008C75E7068";

        public NameTableReader(RSeSegment segment) : base(segment) { }

        protected override void PopulateDataReaderMethods()
        {
            _dataReaderMethods[NameTableDataUID] = Read_NameTableData;
        }

        public override void ReadSegmentData(byte[] segmentData, StreamWriter logFile)
        {
            logFile?.WriteLine($"NameTableReader: Reading segment data for {Segment.Name}. Total size: {segmentData.Length}");
            // NameTable segment usually contains one main block (the table itself)
            if (segmentData.Length > 0)
            {
                // Assuming the entire segmentData is for the NameTableData block.
                // In a real scenario, one might read a UID first if the segment could contain other block types.
                SecNode node = new SecNode(NameTableDataUID, segmentData, 0, segmentData.Length);
                ReadBlock(node);
            }
            else if (Segment.Nodes != null && Segment.Nodes.Count > 0) // Fallback if pre-scanned
            {
                 Logger.Info("NameTableReader: Processing pre-scanned nodes.");
                foreach (var node in Segment.Nodes) ReadBlock(node);
            }
            else logFile?.WriteLine($"NameTableReader: Segment data is empty for {Segment.Name}.");
            logFile?.WriteLine($"NameTableReader: Finished reading for {Segment.Name}.");
        }

        private NtEntry ReadNtEntry(SecNode node, string propertyNamePrefix, StreamWriter logFile)
        {
            int? key = node.ReadSInt32($"{propertyNamePrefix}_Key", logFile);
            int? nameTableIndex = node.ReadSInt32($"{propertyNamePrefix}_NameTableIndex", logFile);

            if (key.HasValue && nameTableIndex.HasValue)
            {
                return new NtEntry(nameTableIndex.Value, key.Value);
            }
            return null; // Or throw exception
        }

        private void Read_NameTableData(SecNode node)
        {
            Logger.Info($"NameTableReader: Reading NameTableData (UID: {node.Uid})");
            node.ReadHeader0(Logger.LogWriter); // Conceptual

            uint? lastIdx = node.ReadUInt32("LastIndex", Logger.LogWriter);
            uint? count = node.ReadUInt32("EntryCount", Logger.LogWriter);

            if (lastIdx.HasValue) node.ParsedContent["LastIndex"] = lastIdx.Value;
            if (!count.HasValue)
            {
                Logger.Error("NameTableReader: Could not read entry count.");
                return;
            }

            var entries = new Dictionary<int, string>(); // Or List<NtEntry>
            node.ParsedContent["NameTableEntries_Raw"] = entries; // Store the dictionary/list

            for (int i = 0; i < count.Value; i++)
            {
                if (node.IsEof(Logger.LogWriter))
                {
                    Logger.Error($"NameTableReader: Unexpected EOF while reading entry {i + 1} of {count.Value}.");
                    break;
                }

                // Python: key, i = getSInt32(data, i)
                // Python: val, i = getSInt32(data, i)
                // Python: txt, i = getLen32Text8(data, i)
                int? entryKey = node.ReadSInt32($"Entry_{i}_Key", Logger.LogWriter);
                int? entryVal = node.ReadSInt32($"Entry_{i}_Value", Logger.LogWriter); // This might be the RSeSegmentObject index
                // The text seems to be missing from NtEntry in ImporterClasses, NtEntry only has NameTable and Key.
                // Assuming the text is the "name" associated with the key.
                // For now, I'll read it but NtEntry class might need adjustment if this text is part of it.
                // This is based on the structure of NMxNameTable in Python.
                string entryName = ImporterUtils.GetLen32Text8(node.FullDataBuffer, node.CurrentReadOffset).Value;
                node.CurrentReadOffset = ImporterUtils.GetLen32Text8(node.FullDataBuffer, node.CurrentReadOffset).NewOffset;


                if (entryKey.HasValue)
                {
                    entries[entryKey.Value] = entryName;
                    node.LogAction(Logger.LogWriter, $"Read NameTable Entry: Key={entryKey.Value}, Value(Index?)={entryVal?.ToString() ?? "N/A"}, Name='{entryName}'");
                }
                else
                {
                     Logger.Warning($"NameTableReader: Failed to read key for entry {i}. Skipping.");
                }
            }
            node.ParsedContent["Summary"] = $"Parsed NameTableData with {entries.Count} entries.";
        }
    }

    public class SheetDlReader : SegmentReader
    {
        public SheetDlReader(RSeSegment segment) : base(segment) { }

        protected override void PopulateDataReaderMethods()
        {
            // Example UIDs - replace with actual from importerSheetDL.py
            _dataReaderMethods["48EB8607-11D2-11D2-910F-0000F8061098"] = Read_Sheet_ViewInfo;
            _dataReaderMethods["B32BF6AC-11D1-11D2-910F-0000F8061098"] = Read_Sheet_Parameters;
        }

        public override void ReadSegmentData(byte[] segmentData, StreamWriter logFile)
        {
            logFile?.WriteLine($"SheetDlReader: Reading segment data for {Segment.Name}. Total size: {segmentData.Length}");
            if (Segment.Nodes != null && Segment.Nodes.Count > 0)
            {
                foreach (var node in Segment.Nodes) ReadBlock(node);
            }
            else logFile?.WriteLine($"SheetDlReader: No pre-scanned nodes for {Segment.Name}.");
            logFile?.WriteLine($"SheetDlReader: Finished reading for {Segment.Name}.");
        }

        private void Read_Sheet_ViewInfo(SecNode node)
        {
            Logger.Info($"SheetDlReader: Reading Sheet_ViewInfo (UID: {node.Uid})");
            node.ReadHeader0(Logger.LogWriter);
            node.ReadLen32Text16("ViewName", Logger.LogWriter);
            node.ReadFloat64("Scale", Logger.LogWriter);
            node.ParsedContent["Summary"] = "Parsed Sheet_ViewInfo";
        }

        private void Read_Sheet_Parameters(SecNode node)
        {
            Logger.Info($"SheetDlReader: Reading Sheet_Parameters (UID: {node.Uid})");
            node.ReadHeader0(Logger.LogWriter);
            node.ReadFloat64("SheetWidth", Logger.LogWriter);
            node.ReadFloat64("SheetHeight", Logger.LogWriter);
            node.ParsedContent["Summary"] = "Parsed Sheet_Parameters";
        }
    }

    public class SheetSmReader : SegmentReader
    {
        public SheetSmReader(RSeSegment segment) : base(segment) { }

        protected override void PopulateDataReaderMethods()
        {
            // Example UIDs - replace with actual from importerSheetSM.py
            _dataReaderMethods["F4A2F948-11D3-11D2-911C-0000F8061098"] = Read_SheetMetal_Properties;
            _dataReaderMethods["CDB613FA-11D1-11D2-910F-0000F8061098"] = Read_SheetMetal_BendInfo;
        }

        public override void ReadSegmentData(byte[] segmentData, StreamWriter logFile)
        {
            logFile?.WriteLine($"SheetSmReader: Reading segment data for {Segment.Name}. Total size: {segmentData.Length}");
            if (Segment.Nodes != null && Segment.Nodes.Count > 0)
            {
                foreach (var node in Segment.Nodes) ReadBlock(node);
            }
            else logFile?.WriteLine($"SheetSmReader: No pre-scanned nodes for {Segment.Name}.");
            logFile?.WriteLine($"SheetSmReader: Finished reading for {Segment.Name}.");
        }

        private void Read_SheetMetal_Properties(SecNode node)
        {
            Logger.Info($"SheetSmReader: Reading SheetMetal_Properties (UID: {node.Uid})");
            node.ReadHeader0(Logger.LogWriter);
            node.ReadFloat64("Thickness", Logger.LogWriter);
            node.ReadLen32Text16("MaterialName", Logger.LogWriter);
            node.ParsedContent["Summary"] = "Parsed SheetMetal_Properties";
        }

        private void Read_SheetMetal_BendInfo(SecNode node)
        {
            Logger.Info($"SheetSmReader: Reading SheetMetal_BendInfo (UID: {node.Uid})");
            node.ReadHeader0(Logger.LogWriter);
            node.ReadFloat64("BendRadius", Logger.LogWriter);
            node.ReadFloat64("KFactor", Logger.LogWriter);
            node.ParsedContent["Summary"] = "Parsed SheetMetal_BendInfo";
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
            // UID "CE52DF3A-11D0-D2D0-0008CCBC0663DC09" now maps to Read_SketchLine2D
            _dataReaderMethods["CE52DF3A-11D0-D2D0-0008CCBC0663DC09"] = Read_SketchLine2D;
            _dataReaderMethods["CA7163A3-11D0-D3B2-0008BFBB21EDDC09"] = Read_PMxPartNode;
            _dataReaderMethods["D31891C2-48BF-14C3-AA42-EA872A846B2A"] = Read_UFRxRef;

            // Parameter related UIDs
            _dataReaderMethods["0AA8AF46-E23E-4E82-8E3F-55A03A184489"] = Read_ParameterConstant;
            _dataReaderMethods["F8A77A03-11D1-11D2-910F-0000F8061098"] = Read_ParameterFunction;
            _dataReaderMethods["F8A77A05-11D1-11D2-910F-0000F8061098"] = Read_ParameterRef;
            _dataReaderMethods["RDXPARAMETER_UID_PLACEHOLDER"] = Read_Parameter;

            // Sketch Geometry UIDs (2D)
            _dataReaderMethods["CE52DF3E-11D0-D2D0-0008CCBC0663DC09"] = Read_SketchPoint2D;
            _dataReaderMethods["CE52DF3B-11D0-D2D0-0008CCBC0663DC09"] = Read_SketchCircle2D;
            // Constraint UIDs (2D)
            _dataReaderMethods["90874D94-11D0-D1F8-0008CABC0663DC09"] = Read_CoincidentConstraint2D;

            // Sketch Geometry UIDs (3D)
            _dataReaderMethods["CE52DF3E-3DPT-4E82-8E3F-55A03A184489"] = Read_SketchPoint3D; // Placeholder UID for SketchPoint3D
            _dataReaderMethods["8EF06C89-11D1-11D2-910F-0000F8061098"] = Read_SketchLine3D;
            _dataReaderMethods["9E43716A-11D2-0FA5-6000-84B7B035C3B0"] = Read_SketchCircle3D;

            // Feature Definition UIDs
            _dataReaderMethods["90874D91-11D0-D1F8-0008CABC0663DC09"] = Read_Feature;
            _dataReaderMethods["729ABE28-11D1-11D2-910F-0000F8061098"] = Read_PartFeatureOperationEnum;
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
            // This method was previously Read_RDxLine2. Renaming/adjusting for SketchLine2D.
            // The original Read_RDxLine2 based on Python's Read_CE52DF3A reads pos & dir (Float64_2D).
            // The previous C# Read_RDxLine2 read GUID and CrossRefs. This indicates a mismatch or different entities sharing UIDs.
            // For this task, implementing as per Python's Read_CE52DF3A for SketchLine2D.
            Logger.Info($"DCReader: Reading SketchLine2D (UID: {node.Uid})");
            ReadHeaderSketch2DEntity(node, "SketchLine2D", Logger.LogWriter);

            node.ReadVector2D("pos", Logger.LogWriter, useFloat32: false); // Python uses ReadFloat64_2D
            node.ReadVector2D("dir", Logger.LogWriter, useFloat32: false); // Python uses ReadFloat64_2D

            node.ParsedContent["Summary"] = "Parsed SketchLine2D";
        }

        // Corresponds to Python Read_0AA8AF46 (ParameterConstant)
        private void Read_ParameterConstant(SecNode node)
        {
            Logger.Info($"DCReader: Reading ParameterConstant (UID: {node.Uid})");
            node.ReadHeader0(Logger.LogWriter); // Assuming a common header structure for DC nodes

            node.ReadChildRef("unitRef", Logger.LogWriter, expectedType: "RDxParameterUnit"); // Placeholder type
            node.ReadFloat64("value", Logger.LogWriter);

            // Parameters usually have a name, often part of their base structure or header
            // If not in a common header read by ReadHeader0, it might be here.
            // Example: node.ReadLen32Text16("ParameterName", Logger.LogWriter);
            // For now, assume name is handled by a more generic mechanism or is part of referenced objects.

            node.ParsedContent["ParameterType"] = "Constant";
            node.ParsedContent["Summary"] = $"Parsed ParameterConstant: Value={node.ParsedContent["value"]}";
        }

        // Corresponds to Python Read_F8A77A03 (ParameterFunction)
        private void Read_ParameterFunction(SecNode node)
        {
            Logger.Info($"DCReader: Reading ParameterFunction (UID: {node.Uid})");
            node.ReadHeader0(Logger.LogWriter);

            node.ReadEnum16<Functions>("functionEnum", Logger.LogWriter);
            node.ReadChildRefList("operands", Logger.LogWriter); // Reads count then list of ChildRefs

            // Example: node.ReadLen32Text16("ParameterName", Logger.LogWriter);

            node.ParsedContent["ParameterType"] = "Function";
            node.ParsedContent["Summary"] = $"Parsed ParameterFunction: Type={node.ParsedContent.GetValueOrDefault("functionEnum", "N/A")}, OperandsCount={ (node.ParsedContent.GetValueOrDefault("operands") as List<object>)?.Count ?? 0 }";
        }

        // Corresponds to Python Read_F8A77A05 (ParameterRef)
        private void Read_ParameterRef(SecNode node)
        {
            Logger.Info($"DCReader: Reading ParameterRef (UID: {node.Uid})");
            node.ReadHeader0(Logger.LogWriter);

            node.ReadChildRef("referencedParameter", Logger.LogWriter, expectedType: "RDxParameter"); // Or RDxReal etc.

            // Example: node.ReadLen32Text16("ParameterName", Logger.LogWriter);

            node.ParsedContent["ParameterType"] = "Reference";
            node.ParsedContent["Summary"] = $"Parsed ParameterRef: Ref={node.ParsedContent.GetValueOrDefault("referencedParameter", "N/A")}";
        }

        // Corresponds to Python Read_90874D26 (RDxReal - Parameter main node)
        private void Read_Parameter(SecNode node)
        {
            Logger.Info($"DCReader: Reading Parameter (RDxReal) (UID: {node.Uid})");
            // In Python, Read_90874D26 calls self.ReadHeaderParameter(node, 'Parameter')
            // We'll use ReadHeader0 for now, or create ReadHeaderParameter in SecNode if a common pattern is clear.
            node.ReadHeader0(Logger.LogWriter);

            node.ReadLen32Text16("Name", Logger.LogWriter);
            node.ReadChildRef("ValueReference", Logger.LogWriter); // Ref to ParameterConstant, Function, Ref etc.
            node.ReadChildRef("UnitReference", Logger.LogWriter, expectedType: "RDxParameterUnit");
            node.ReadSInt16("UnknownSInt16_1", Logger.LogWriter); // uknVal1 in Python
            node.ReadEnum16<Tolerances>("Tolerance", Logger.LogWriter);
            node.ReadUInt32("Flags", Logger.LogWriter);
            node.ReadSInt16("Precision", Logger.LogWriter);
            node.ReadBoolean("IsExported", Logger.LogWriter);
            node.ReadBoolean("IsKey", Logger.LogWriter);
            node.ReadBoolean("IsCustom", Logger.LogWriter);
            node.ReadSInt32("RowId", Logger.LogWriter);

            if (!node.IsEof(Logger.LogWriter)) // Check if there's more data for the comment
            {
                node.ReadLen32Text16("Comment", Logger.LogWriter);
            }

            node.ParsedContent["Summary"] = $"Parsed Parameter (RDxReal): Name='{node.ParsedContent.GetValueOrDefault("Name", "N/A")}'";
        }

        // Sketch Entity Header Helper
        private void ReadHeaderSketch2DEntity(SecNode node, string typeName, StreamWriter logFile)
        {
            Logger.Info($"DCReader: Reading Header for Sketch2D Entity '{typeName}' (UID: {node.Uid})");
            node.ReadHeader0(logFile); // Common base header

            node.ReadUInt32("SketchEntityFlags", logFile); // Example: flags, ID, etc.
            node.ReadGuid("SketchEntityID", logFile);
            node.ReadChildRef("Attributes", logFile); // attributes_
            node.ReadCrossRef("Geometry", logFile); // geometry -> This might be the actual line/circle/point data itself if not inline
            node.ReadChildRef("ParentSketch", logFile, expectedType: "RDxSketch"); // sketch_
            node.ReadUInt32("OrderInSketch", logFile); // sketch_order_

            // Python's importerDC.SegmentReader.ReadHeaderSketch2DEntity also reads:
            // crossRef (constraint_mgr_)
            // uInt32 (geom_idx_)
            // uInt8 (driven)
            // uInt8 (refGeom)
            // uInt8 (valGeom)
            // list2 (_TYP_NODE_REF_, constraints)
            node.ReadCrossRef("ConstraintManager", logFile);
            node.ReadUInt32("GeometryIndex", logFile);
            node.ReadUInt8("Driven", logFile);
            node.ReadUInt8("ReferenceGeometry", logFile);
            node.ReadUInt8("ConstructionGeometry", logFile); // Assuming valGeom means construction

            node.ReadChildRefList("Constraints", logFile); // Placeholder for list of constraints

            node.ParsedContent[$"{typeName}_HeaderProcessed"] = true;
        }

        // Constraint Header Helper
        private void ReadHeaderConstraint2D(SecNode node, string typeName, StreamWriter logFile)
        {
            Logger.Info($"DCReader: Reading Header for Constraint2D '{typeName}' (UID: {node.Uid})");
            node.ReadHeader0(logFile); // Common base header

            node.ReadGuid("ConstraintID", logFile);
            node.ReadChildRef("ParentSketch", logFile, expectedType: "RDxSketch");
            node.ReadBoolean("IsActive", logFile); // active_
            node.ReadBoolean("IsDriven", logFile); // driven_

            node.ParsedContent[$"{typeName}_HeaderProcessed"] = true;
        }

        // Corresponds to Python Read_CE52DF3E (RDxPoint2D)
        private void Read_SketchPoint2D(SecNode node)
        {
            Logger.Info($"DCReader: Reading SketchPoint2D (UID: {node.Uid})");
            ReadHeaderSketch2DEntity(node, "SketchPoint2D", Logger.LogWriter);

            node.ReadVector2D("pos", Logger.LogWriter, useFloat32: false); // x_, y_

            // Reading lists for endPointOf and centerOf (Conceptual)
            // Python: self.ReadList2(node, i, _TYP_CROSS_REF_, 'endPointOf')
            // This implies a count followed by cross-references.
            uint? endPointOfCount = node.ReadUInt32("endPointOf_count", Logger.LogWriter);
            if (endPointOfCount.HasValue)
            {
                var endPointOfList = new List<object>();
                for (int k = 0; k < endPointOfCount.Value; k++)
                    endPointOfList.Add(node.ReadCrossRef($"endPointOf_{k}", Logger.LogWriter));
                node.ParsedContent["endPointOf"] = endPointOfList;
            }

            uint? centerOfCount = node.ReadUInt32("centerOf_count", Logger.LogWriter);
            if (centerOfCount.HasValue)
            {
                var centerOfList = new List<object>();
                for (int k = 0; k < centerOfCount.Value; k++)
                    centerOfList.Add(node.ReadCrossRef($"centerOf_{k}", Logger.LogWriter));
                node.ParsedContent["centerOf"] = centerOfList;
            }

            node.ParsedContent["Summary"] = $"Parsed SketchPoint2D: Pos={node.ParsedContent.GetValueOrDefault("pos", "N/A")}";
        }

        // Corresponds to Python Read_CE52DF3B (RDxCircle2D)
        private void Read_SketchCircle2D(SecNode node)
        {
            Logger.Info($"DCReader: Reading SketchCircle2D (UID: {node.Uid})");
            ReadHeaderSketch2DEntity(node, "SketchCircle2D", Logger.LogWriter);

            node.ReadCrossRef("center", Logger.LogWriter, expectedType: "RDxPoint2D"); // center_
            node.ReadFloat64("r", Logger.LogWriter); // radius_
            node.ReadUInt8("u8_0", Logger.LogWriter); // ukn_c0_

            node.ParsedContent["Summary"] = $"Parsed SketchCircle2D: Radius={node.ParsedContent.GetValueOrDefault("r", "N/A")}";
        }

        // Corresponds to Python Read_90874D94 (RDxCoincidentConstraint2D)
        private void Read_CoincidentConstraint2D(SecNode node)
        {
            Logger.Info($"DCReader: Reading CoincidentConstraint2D (UID: {node.Uid})");
            ReadHeaderConstraint2D(node, "CoincidentConstraint2D", Logger.LogWriter);

            node.ReadCrossRef("entity1", Logger.LogWriter); // entity1_
            node.ReadCrossRef("entity2", Logger.LogWriter); // entity2_

            node.ParsedContent["Summary"] = "Parsed CoincidentConstraint2D";
        }

        // 3D Sketch Entity Header Helper
        private void ReadHeaderSketch3DEntity(SecNode node, string typeName, StreamWriter logFile)
        {
            Logger.Info($"DCReader: Reading Header for Sketch3D Entity '{typeName}' (UID: {node.Uid})");
            node.ReadHeader0(logFile); // Common base header

            node.ReadGuid("Sketch3DEntityID", logFile);
            node.ReadChildRef("Attributes", logFile);
            node.ReadCrossRef("Geometry3D", logFile); // -> actual 3D geometry data
            node.ReadChildRef("ParentSketch3D", logFile, expectedType: "RDxSketch3D");
            node.ReadUInt32("OrderInSketch3D", logFile);
            node.ReadUInt8("Driven3D", logFile);
            node.ReadBoolean("IsConstruction3D", logFile); // from isConstruction_

            node.ParsedContent[$"{typeName}_HeaderProcessed"] = true;
        }

        // Feature Header Helper
        private void ReadHeaderFeature(SecNode node, string typeName, StreamWriter logFile)
        {
            Logger.Info($"DCReader: Reading Header for Feature '{typeName}' (UID: {node.Uid})");
            node.ReadHeader0(logFile); // Common base header

            node.ReadLen32Text16("FeatureName", logFile);
            node.ReadChildRef("Attributes", logFile);
            node.ReadBoolean("IsSuppressed", logFile); // suppressed_
            node.ReadUInt32("FeatureOrderIndex", logFile); // order_index_
            node.ReadGuid("FeatureGUID", logFile); // guid_
            // ... other common feature fields based on Python's importerDC.SegmentReader.ReadHeaderFeature ...
            // Example: node.ReadChildRef("OwningPart", logFile); // owner_part_

            node.ParsedContent[$"{typeName}_HeaderProcessed"] = true;
        }

        // Enum Header Helper (generic for nodes primarily defining an enum)
        private void ReadHeaderEnum(SecNode node, string typeName, StreamWriter logFile)
        {
            Logger.Info($"DCReader: Reading Header for Enum Type '{typeName}' (UID: {node.Uid})");
            node.ReadHeader0(logFile); // Common base header
            node.ReadLen32Text16("EnumName", logFile); // Or a fixed name if not dynamic
            // Potentially other common fields for enum nodes
            node.ParsedContent[$"{typeName}_HeaderProcessed"] = true;
        }

        // Corresponds to Python Read_CE52DF3E (for Point3D - using new UID)
        private void Read_SketchPoint3D(SecNode node)
        {
            Logger.Info($"DCReader: Reading SketchPoint3D (UID: {node.Uid})");
            ReadHeaderSketch3DEntity(node, "SketchPoint3D", Logger.LogWriter);

            node.ReadVector3DFloat64("pos", Logger.LogWriter);

            // Reading lists for endPointOf and centerOf
            uint? endPointOfCount = node.ReadUInt32("endPointOf_count", Logger.LogWriter);
            if (endPointOfCount.HasValue)
            {
                var endPointOfList = new List<object>();
                for (int k = 0; k < endPointOfCount.Value; k++)
                    endPointOfList.Add(node.ReadCrossRef($"endPointOf_{k}", Logger.LogWriter));
                node.ParsedContent["endPointOf"] = endPointOfList;
            }

            uint? centerOfCount = node.ReadUInt32("centerOf_count", Logger.LogWriter);
            if (centerOfCount.HasValue)
            {
                var centerOfList = new List<object>();
                for (int k = 0; k < centerOfCount.Value; k++)
                    centerOfList.Add(node.ReadCrossRef($"centerOf_{k}", Logger.LogWriter));
                node.ParsedContent["centerOf"] = centerOfList;
            }
            node.ParsedContent["Summary"] = $"Parsed SketchPoint3D: Pos={node.ParsedContent.GetValueOrDefault("pos", "N/A")}";
        }

        // Corresponds to Python Read_8EF06C89 (SketchLine3D)
        private void Read_SketchLine3D(SecNode node)
        {
            Logger.Info($"DCReader: Reading SketchLine3D (UID: {node.Uid})");
            ReadHeaderSketch3DEntity(node, "SketchLine3D", Logger.LogWriter);

            node.ReadVector3DFloat64("pos", Logger.LogWriter);
            node.ReadVector3DFloat64("dir", Logger.LogWriter);

            node.ParsedContent["Summary"] = "Parsed SketchLine3D";
        }

        // Corresponds to Python Read_9E43716A (SketchCircle3D/Arc3D)
        private void Read_SketchCircle3D(SecNode node)
        {
            Logger.Info($"DCReader: Reading SketchCircle3D/Arc3D (UID: {node.Uid})");
            ReadHeaderSketch3DEntity(node, "SketchCircle3D", Logger.LogWriter);

            node.ReadVector3DFloat64("pos", Logger.LogWriter);    // Center point
            node.ReadVector3DFloat64("normal", Logger.LogWriter); // Normal vector of the circle's plane
            node.ReadVector3DFloat64("m", Logger.LogWriter);      // Major axis vector (defines zero angle direction)
            node.ReadFloat64("r", Logger.LogWriter);              // Radius
            node.ReadAngle("startAngle", Logger.LogWriter);       // Start angle in radians
            node.ReadAngle("sweepAngle", Logger.LogWriter);       // Sweep angle in radians (2*PI for full circle)

            node.ParsedContent["Summary"] = $"Parsed SketchCircle3D: Radius={node.ParsedContent.GetValueOrDefault("r", "N/A")}";
        }

        // Corresponds to Python Read_90874D91 (Feature)
        private void Read_Feature(SecNode node)
        {
            Logger.Info($"DCReader: Reading Feature (UID: {node.Uid})");
            ReadHeaderFeature(node, "GenericFeature", Logger.LogWriter);

            node.ReadUInt32("outlineItem", Logger.LogWriter); // outline_item_
            node.ReadChildRefList("properties", Logger.LogWriter); // properties_
            node.ReadUInt32("u32_1", Logger.LogWriter); // ukn_0_ (unknown or flags)

            node.ParsedContent["Summary"] = $"Parsed Feature: Name='{node.ParsedContent.GetValueOrDefault("FeatureName", "N/A")}'";
        }

        // Corresponds to Python Read_729ABE28 (PartFeatureOperationEnum)
        private void Read_PartFeatureOperationEnum(SecNode node)
        {
            Logger.Info($"DCReader: Reading PartFeatureOperationEnum (UID: {node.Uid})");
            ReadHeaderEnum(node, "PartFeatureOperationEnum", Logger.LogWriter); // Assuming a generic enum header

            node.ReadEnum16<PartFeatureOperationEnumPlaceholder>("Operation", Logger.LogWriter);

            node.ParsedContent["Summary"] = $"Parsed PartFeatureOperationEnum: Op='{node.ParsedContent.GetValueOrDefault("Operation", "N/A")}'";
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

    public class RSeDbReader : SegmentReader
    {
        public RSeDbReader(RSeSegment segment) : base(segment) { }

        protected override void PopulateDataReaderMethods()
        {
            // RSeDb segment might have a main block or specific UIDs for its parts.
            // For now, assuming most logic is in ReadSegmentData or a primary Read_ method.
            // Example: _dataReaderMethods["RSEDB_MAIN_UID"] = Read_RSeDbMain;
        }

        public override void ReadSegmentData(byte[] segmentData, StreamWriter logFile)
        {
            Logger.Info($"RSeDbReader: Reading segment data for {Segment.Name}. Total size: {segmentData.Length}");
            SecNode node = new SecNode(Segment.Name, segmentData, 0, segmentData.Length); // Treat whole segment as one node for now

            // Conceptual parsing based on Python's ReadRSeDb and ReadRSeSegInfo
            // This is highly simplified. A real implementation would parse specific structures.

            // RSeDatabase part
            node.ReadGuid("DatabaseGuid", logFile); // Placeholder for UID
            node.ReadSInt32("SchemaVersion", logFile); // Placeholder for Schema
            string dbVersionStr = node.ReadLen32Text16("VersionString1", logFile); // Placeholder for Vers1 string
            // TODO: Parse dbVersionStr into Segment.Version or a VersionInfo object in ParsedContent

            // RSeSegInformation part (often follows or is embedded)
            node.ReadLen32Text16("SegInfo.Text", logFile);
            node.ReadGuid("SegInfo.Guid", logFile);
            // ... other simple fields of RSeSegInformation ...

            // Segment Directory (crucial part)
            uint? numSegments = node.ReadUInt32("SegmentDirectory.Count", logFile);
            if (numSegments.HasValue)
            {
                var segmentDirectory = new List<SegmentEntryInfo>();
                for (int i = 0; i < numSegments.Value; i++)
                {
                    if (node.IsEof(logFile)) break;
                    string segName = node.ReadLen32Text16($"SegmentDirectory[{i}].Name", logFile);
                    string segType = node.ReadLen32Text16($"SegmentDirectory[{i}].TypeString", logFile);
                    string segUid = node.ReadLen32Text16($"SegmentDirectory[{i}].UidString", logFile); // Or ReadGuid if it's a binary GUID
                    // Could also read OLE storage index or other metadata here
                    if (segName != null && segType != null && segUid != null)
                    {
                        segmentDirectory.Add(new SegmentEntryInfo(segName, segType, segUid));
                    }
                }
                node.ParsedContent["SegmentDirectory"] = segmentDirectory;
                Logger.Info($"RSeDbReader: Parsed {segmentDirectory.Count} segment entries from directory.");
            }

            // Store some top-level info directly on the segment for InventorReader to use
            Segment.ParsedContent["DBSchemaVersion"] = node.ParsedContent.GetValueOrDefault("SchemaVersion");
            Segment.ParsedContent["DBVersionString1"] = node.ParsedContent.GetValueOrDefault("VersionString1");

            Logger.Info($"RSeDbReader: Finished reading for {Segment.Name}.");
        }
    }

    public class UFRxDocReader : SegmentReader
    {
        public UFRxDocReader(RSeSegment segment) : base(segment) { }

        protected override void PopulateDataReaderMethods()
        {
            // UFRxDoc might have specific UIDs for its internal blocks, or might be read sequentially.
            // Example: _dataReaderMethods["UFRX_HEADER1_UID"] = Read_UFRxHeader1_Block;
        }

        public override void ReadSegmentData(byte[] segmentData, StreamWriter logFile)
        {
            Logger.Info($"UFRxDocReader: Reading segment data for {Segment.Name}. Total size: {segmentData.Length}");
            SecNode node = new SecNode(Segment.Name, segmentData, 0, segmentData.Length);

            var header1 = new UFRxHeader1();

            header1.Schema = node.ReadSInt32("Header1.Schema", logFile) ?? 0;
            header1.Magic1 = node.ReadSInt32("Header1.Magic1", logFile) ?? 0;
            // Skip 8 byte array (arr1 in Python)
            node.SkipBytes(8, logFile, "Header1.arr1");
            header1.Magic2 = node.ReadSInt32("Header1.Magic2", logFile) ?? 0;
            // Skip 4 byte array (arr2 in Python)
            node.SkipBytes(4, logFile, "Header1.arr2");

            header1.VersionString = node.ReadLen32Text16("Header1.VersionString", logFile);
            // TODO: Parse VersionString into header1.ParsedVersion (VersionInfo object)

            header1.FileName = node.ReadLen32Text16("Header1.FileName", logFile);
            header1.SourceFileName = node.ReadLen32Text16("Header1.SourceFileName", logFile);

            // Python reads DateTime (creation_date), GUID (doc_guid), GUID (version_guid)
            // These are complex types, for now, let's skip their size or read as raw bytes/placeholders
            // Assuming DateTime is 8 bytes (FILETIME), GUID is 16 bytes
            node.SkipBytes(8, logFile, "Header1.CreationDate_Placeholder");
            header1.DocGuid = node.ReadGuid("Header1.DocGuid", logFile) ?? Guid.Empty;
            header1.VersionGuid = node.ReadGuid("Header1.VersionGuid", logFile) ?? Guid.Empty;

            // Store the parsed header
            node.ParsedContent["Header1"] = header1;
            Segment.ParsedContent["Header1"] = header1; // Make it easily accessible for InventorReader

            Logger.Info($"UFRxDocReader: Finished reading for {Segment.Name}. Parsed Header1 Schema: {header1.Schema}");
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
