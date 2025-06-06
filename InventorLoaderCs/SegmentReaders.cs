using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Numerics; // For Matrix4x4

namespace InventorLoaderCs
{
    public abstract class StyleReader : SegmentReader
    {
        protected StyleReader(RSeSegment segment) : base(segment) { }
        protected int ReadHeaderSU32S(SecNode node, string typeName = null, StreamWriter logFile = null) { /* ... content from previous state ... */ node.LogAction(logFile, $"StyleReader.ReadHeaderSU32S for {typeName ?? node.Uid}"); node.ReadHeader0(logFile); node.ReadUInt32("StyleFlags", logFile); node.ReadChildRef("StyleParentRef", logFile); return node.CurrentReadOffset; }
        protected int SkipBlockSize(SecNode node, int currentOffsetToUse , int count = 1, StreamWriter logFile = null) { /* ... content from previous state ... */ int bytesToSkip = count * 4; node.LogAction(logFile, $"StyleReader.SkipBlockSize: Skipping {bytesToSkip} bytes from offset {node.CurrentReadOffset}."); node.SkipBytes(bytesToSkip, logFile, "BlockSizeSkip"); return node.CurrentReadOffset; }
    }

    public class EeSceneReader : StyleReader
    {
        public List<object> Faces { get; private set; }
        public List<object> Objects3D { get; private set; }
        public EeSceneReader(RSeSegment segment) : base(segment) { Faces = new List<object>(); Objects3D = new List<object>(); }
        protected override void PopulateDataReaderMethods() { /* ... content from previous state ... */ _dataReaderMethods["120284EF-E23E-4E82-8E3F-55A03A184489"] = Read_120284EF_Attributes; _dataReaderMethods["5194E9A3-11D3-11D2-911C-0000F8061098"] = Read_5194E9A3_Face; _dataReaderMethods["A79EACCF-11D1-11D2-910F-0000F8061098"] = Read_A79EACCF_3dObject; _dataReaderMethods["A79EACD3-11D1-11D2-910F-0000F8061098"] = Read_A79EACD3_Point3D; _dataReaderMethods["022AC1B1-11D2-0D35-6000-F99AC5361AB0"] = Read_ColorAttr; _dataReaderMethods["37DB9D1E-11D2-11D2-9111-0000F8061098"] = Read_37DB9D1E_SurfacePlane; }
        public override void ReadSegmentData(byte[] segmentData, StreamWriter logFile) { /* ... content from previous state ... */ logFile?.WriteLine($"EeSceneReader: Reading segment data for {Segment.Name}."); if (Segment.Nodes != null && Segment.Nodes.Count > 0) { foreach (var node in Segment.Nodes) ReadBlock(node); } else logFile?.WriteLine($"EeSceneReader: No pre-scanned nodes for {Segment.Name}."); }
        public void ReadHeader3dObject(SecNode node, StreamWriter logFile, string typeName = null, string ref1Name = "childObjectsListHeaderRef") { /* ... content from previous state ... */ node.LogAction(logFile, $"EeSceneReader.ReadHeader3dObject for {typeName ?? node.Uid}"); node.ReadHeader0(logFile); node.ReadUInt32("flags", logFile); node.ReadChildRef("styles", logFile); node.ReadChildRef(ref1Name, logFile); node.ReadParentRef("parent", logFile); node.ReadUInt32("u32_0", logFile); SkipBlockSize(node, node.CurrentReadOffset, 1, logFile); node.ParsedContent["object3D"] = true; }
        public void ReadHeaderAttribute(SecNode node, StreamWriter logFile, string typeName = null) { /* ... content from previous state ... */ node.LogAction(logFile, $"EeSceneReader.ReadHeaderAttribute for {typeName ?? node.Uid}"); ReadHeaderSU32S(node, typeName, logFile); node.ReadUInt8("u8_0", logFile); SkipBlockSize(node, node.CurrentReadOffset, 1, logFile); }
        public void Read_ColorAttr(SecNode node) { /* ... content from previous state ... */ Logger.Info($"EeSceneReader: Reading ColorAttr (UID: {node.Uid})"); SkipBlockSize(node, node.CurrentReadOffset, 1, Logger.LogWriter); node.ReadUInt8Array("Color.a0", 2, Logger.LogWriter); node.ReadColorRgba("Color.c0", Logger.LogWriter); node.ReadColorRgba("Color.diffuse", Logger.LogWriter); node.ReadColorRgba("Color.c2", Logger.LogWriter); node.ReadColorRgba("Color.c3", Logger.LogWriter); node.ReadUInt16Array("Color.a5", 2, Logger.LogWriter); node.ParsedContent["Summary"] = "Parsed Color Attribute"; }
        private void Read_120284EF_Attributes(SecNode node) { /* ... content from previous state ... */ Logger.Info($"EeSceneReader: Reading Attributes Collection (UID: {node.Uid})"); ReadHeaderSU32S(node, "AttributesCollection", Logger.LogWriter); node.ReadUInt8("u8_0", Logger.LogWriter); SkipBlockSize(node, node.CurrentReadOffset, 1, Logger.LogWriter); node.ParsedContent["Summary"] = "Parsed Attributes Collection"; }
        private void Read_5194E9A3_Face(SecNode node) { /* ... content from previous state ... */ Logger.Info($"EeSceneReader: Reading Face (UID: {node.Uid})"); ReadHeader3dObject(node, Logger.LogWriter, "Face", ref1Name: "surfaceRef"); node.ReadUInt8("u8_0", Logger.LogWriter); SkipBlockSize(node, node.CurrentReadOffset, 2, Logger.LogWriter); node.ReadFloat64Array("boundingBox", 6, Logger.LogWriter); SkipBlockSize(node, node.CurrentReadOffset, 1, Logger.LogWriter); node.ReadUInt32("key", Logger.LogWriter); node.ReadUInt32("u32_1", Logger.LogWriter); node.ReadUInt32("u32_2", Logger.LogWriter); Faces.Add(node.ParsedContent); node.ParsedContent["Summary"] = "Parsed Face"; }
        private void Read_A79EACCF_3dObject(SecNode node) { /* ... content from previous state ... */ Logger.Info($"EeSceneReader: Reading Generic 3dObject (UID: {node.Uid})"); ReadHeader3dObject(node, Logger.LogWriter, "Generic3dObject", ref1Name: "childObjectsListHeaderRef"); bool? hasTransform = node.ReadBoolean("Transformation.HasTransformFlag", Logger.LogWriter); if (hasTransform.HasValue && hasTransform.Value) { node.ReadTransformation3D("Transformation", Logger.LogWriter); } else { node.ParsedContent["Transformation"] = Matrix4x4.Identity; } Objects3D.Add(node.ParsedContent); node.ParsedContent["Summary"] = "Parsed Generic 3dObject"; }
        private void Read_A79EACD3_Point3D(SecNode node) { /* ... content from previous state ... */ Logger.Info($"EeSceneReader: Reading Point3D (UID: {node.Uid})"); ReadHeader3dObject(node, Logger.LogWriter, "Point3D"); node.ReadVector3DFloat64("Position", Logger.LogWriter); node.ReadFloat32("f32_0", Logger.LogWriter); node.ReadSInt32("s32_0", Logger.LogWriter); node.ReadUInt16("u16_0", Logger.LogWriter); node.ParsedContent["Summary"] = "Parsed Point3D"; }
        private void Read_37DB9D1E_SurfacePlane(SecNode node) { /* ... content from previous state ... */ Logger.Info($"EeSceneReader: Reading SurfacePlane (UID: {node.Uid})"); SkipBlockSize(node, node.CurrentReadOffset, 2, Logger.LogWriter); node.ReadParentRef("parentRef", Logger.LogWriter); SkipBlockSize(node, node.CurrentReadOffset, 1, Logger.LogWriter); node.ReadUInt32("u32_0", Logger.LogWriter); node.ReadUInt8("u8_0", Logger.LogWriter); node.ParsedContent["Summary"] = "Parsed SurfacePlane"; }
    }

    public class GraphicsReader : EeSceneReader
    {
        public GraphicsReader(RSeSegment segment) : base(segment) { }
        protected override void PopulateDataReaderMethods() { /* ... content from previous state ... */ base.PopulateDataReaderMethods(); _dataReaderMethods["4B26ED59-E839-11D1-8D78-0008C75E7068"] = Read_4B26ED59_Mesh; _dataReaderMethods["60FD1845-11D0-D79D-0008BFBB21EDDC09"] = Read_60FD1845_Sketch2D; _dataReaderMethods["CA7163A3-11D0-D3B2-0008BFBB21EDDC09"] = Read_CA7163A3_PartNode; _dataReaderMethods["A98235C4-11D0-D1F8-0008CABC0663DC09"] = Read_A98235C4_MeshPart; _dataReaderMethods["A79EACD1-F6C0-449B-9508-2283E9799B94"] = Read_A79EACD1_GraphicsNodeList; }
        protected void ReadHeaderU32RefU8List3(SecNode node, string typeName, string listName, StreamWriter logFile) { /* ... content from previous state ... */ Logger.Info($"GraphicsReader: Reading HeaderU32RefU8List3 for '{typeName}' (UID: {node.Uid})"); node.ReadHeader0(logFile); node.ReadUInt32($"{typeName}.Flags", logFile); node.ReadChildRef($"{typeName}.StyleOrParentRef", logFile); node.ReadUInt8($"{typeName}.ItemCountFromHeader", logFile); node.ReadChildRef($"{listName}_ListHeaderRef", logFile); node.ReadParentRef($"{typeName}.DCModelRef", logFile); node.ReadUInt32($"{typeName}.UnknownUInt32_1", logFile); node.ReadSInt32($"{typeName}.DCIndex", logFile); node.ParsedContent[$"{typeName}_HeaderU32RefU8List3_Processed"] = true; }
        private void Read_4B26ED59_Mesh(SecNode node) { /* ... content from previous state ... */ Logger.Info($"GraphicsReader: Reading Mesh (UID: {node.Uid})"); ReadHeaderU32RefU8List3(node, "Mesh", "MeshItemsList", Logger.LogWriter); if (Segment.Version.IsGreaterOrEqualTo(2017,0,0)) node.SkipBytes(4, Logger.LogWriter, "Version>2017 skip"); node.ParsedContent["Summary"] = "Parsed Mesh"; }
        private void Read_60FD1845_Sketch2D(SecNode node) { /* ... content from previous state ... */ Logger.Info($"GraphicsReader: Reading Sketch2D (UID: {node.Uid})"); node.ReadHeader0(Logger.LogWriter); node.ReadChildRef("SketchObjectRef", Logger.LogWriter); SkipBlockSize(node, node.CurrentReadOffset, 1, Logger.LogWriter); node.ReadUInt8("u8_1", Logger.LogWriter); SkipBlockSize(node, node.CurrentReadOffset, 1, Logger.LogWriter); node.ReadUInt32("Key", Logger.LogWriter); bool? hasTransform = node.ReadBoolean("Sketch2D.Transformation.HasTransformFlag", Logger.LogWriter); if (hasTransform.HasValue && hasTransform.Value) { node.ReadTransformation3D("Sketch2D.Transformation", Logger.LogWriter); } else { node.ParsedContent["Sketch2D.Transformation"] = Matrix4x4.Identity; } node.ParsedContent["Summary"] = "Parsed Sketch2D"; }
        private void Read_CA7163A3_PartNode(SecNode node) { /* ... content from previous state ... */ Logger.Info($"GraphicsReader: Reading PartNode (UID: {node.Uid})"); ReadHeaderU32RefU8List3(node, "PartNode", "ChildPartsList", Logger.LogWriter); node.ReadMaterial("MaterialInfo", Logger.LogWriter); node.ParsedContent["Summary"] = "Parsed PartNode"; }
        private void Read_A98235C4_MeshPart(SecNode node) { /* ... content from previous state ... */ Logger.Info($"GraphicsReader: Reading MeshPart (UID: {node.Uid})"); ReadHeaderU32RefU8List3(node, "MeshPart", "PartsList", Logger.LogWriter); node.ReadChildRef("Object3D_Ref", Logger.LogWriter); node.ReadTransformation3D("MeshPart.Transformation", Logger.LogWriter); node.ReadUInt32("MeshID", Logger.LogWriter); if (Segment.Version.IsGreaterOrEqualTo(2009,0,0)) { node.ReadLen32Text8("Text1_Post2008", Logger.LogWriter); } node.ParsedContent["Summary"] = $"Parsed MeshPart: MeshID='{node.ParsedContent.GetValueOrDefault("MeshID", "N/A")}'"; }
        private void Read_A79EACD1_GraphicsNodeList(SecNode node) { /* ... content from previous state ... */ Logger.Info($"GraphicsReader: Reading GraphicsNodeList (UID: {node.Uid})"); ReadHeader3dObject(node, Logger.LogWriter, "GraphicsNodeList", ref1Name: "objectsListHeader"); node.ReadChildRefList("objects", Logger.LogWriter); bool? hasTransform = node.ReadBoolean("Transformation.HasTransformFlag", Logger.LogWriter); if (hasTransform.HasValue && hasTransform.Value) { node.ReadTransformation3D("Transformation", Logger.LogWriter); } else { node.ParsedContent["Transformation"] = Matrix4x4.Identity; } node.ReadUInt32("u32_1_PostTransform", Logger.LogWriter); node.ReadBoolean("b0_PostTransform", Logger.LogWriter); node.ReadListOfVector3DFloat64("points", Logger.LogWriter); node.ReadListOfUInt32("indices", Logger.LogWriter); if (Segment.Version.IsGreaterOrEqualTo(2011,0,0)) { node.ReadChildRefList("lst3_Post2010", Logger.LogWriter); } node.ParsedContent["Summary"] = "Parsed GraphicsNodeList"; }
    }

    public class FBAttributeReader : SegmentReader { public FBAttributeReader(RSeSegment s) : base(s) { } protected override void PopulateDataReaderMethods() { } public override void ReadSegmentData(byte[] d, StreamWriter l) { /* Placeholder */ } }
    public class NotebookReader : SegmentReader { public NotebookReader(RSeSegment s) : base(s) { } protected override void PopulateDataReaderMethods() { } public override void ReadSegmentData(byte[] d, StreamWriter l) { /* Placeholder */ } }
    public class ResultReader : SegmentReader { public ResultReader(RSeSegment s) : base(s) { } protected override void PopulateDataReaderMethods() { } public override void ReadSegmentData(byte[] d, StreamWriter l) { /* Placeholder */ } }
    public class NameTableReader : SegmentReader { public NameTableReader(RSeSegment s) : base(s) { Read_NameTableData_UID = "CCE92042-C27B-11D1-8D6C-0008C75E7068"; } protected string Read_NameTableData_UID; protected override void PopulateDataReaderMethods() { _dataReaderMethods[Read_NameTableData_UID] = Read_NameTableData; } public override void ReadSegmentData(byte[] d, StreamWriter l) { if (d.Length > 0) { SecNode node = new SecNode(Read_NameTableData_UID, d, 0, d.Length); ReadBlock(node); } } private void Read_NameTableData(SecNode n) { /* Placeholder */ } }
    public class SheetDlReader : SegmentReader { public SheetDlReader(RSeSegment s) : base(s) { } protected override void PopulateDataReaderMethods() { } public override void ReadSegmentData(byte[] d, StreamWriter l) { /* Placeholder */ } }
    public class SheetSmReader : SegmentReader { public SheetSmReader(RSeSegment s) : base(s) { } protected override void PopulateDataReaderMethods() { } public override void ReadSegmentData(byte[] d, StreamWriter l) { /* Placeholder */ } }

    public class RSeDbReader : SegmentReader
    {
        public RSeDbReader(RSeSegment segment) : base(segment) { }
        protected override void PopulateDataReaderMethods() { }
        public override void ReadSegmentData(byte[] segmentData, StreamWriter logFile)
        {
            string dataStr = Encoding.UTF8.GetString(segmentData);
            if (dataStr.StartsWith("SIMULATED_RSEDB_PART:"))
            {
                Logger.Info(logFile, "RSeDbReader: Detected SIMULATED_RSEDB_PART data.");
                var segmentDirectory = new List<SegmentEntryInfo>();
                var entries = dataStr.Substring("SIMULATED_RSEDB_PART:".Length).Split(';');
                foreach (var entryStr in entries)
                {
                    var parts = entryStr.Split(',');
                    if (parts.Length == 3)
                    {
                        segmentDirectory.Add(new SegmentEntryInfo(name: parts[0], typeString: parts[1], uidString: parts[2]));
                        Logger.Info(logFile, $"RSeDbReader: Added simulated directory entry - Name: {parts[0]}, Type: {parts[1]}, UID: {parts[2]}");
                    }
                }
                if (this.Segment.SegInfo == null) this.Segment.SegInfo = new RSeSegInformation();
                this.Segment.SegInfo.SegmentDirectory = segmentDirectory;
                this.Segment.ParsedContent["SegmentDirectory"] = segmentDirectory;

                var rseDb = new RSeDatabase();
                rseDb.Schema = 0;
                rseDb.Txt = "Simulated RSeDb from SIMULATED_RSEDB_PART";
                this.Segment.ParsedContent["RSeDatabaseObject"] = rseDb;
                this.Segment.Acis = null;
                Logger.Info(logFile, $"RSeDbReader: Finished processing SIMULATED_RSEDB_PART data. {segmentDirectory.Count} entries loaded into SegInfo.SegmentDirectory.");
                return;
            }

            logFile?.WriteLine($"RSeDbReader: ReadSegmentData for {Segment.Name} - Length: {segmentData.Length}. Actual byte parsing logic placeholder.");
            this.Segment.ParsedContent["RawData"] = segmentData;
        }
    }

    public class UFRxDocReader : SegmentReader
    {
        public UFRxDocReader(RSeSegment s) : base(s) { }
        protected override void PopulateDataReaderMethods() { /* UFRxDoc might have internal SecNodes */ }
        public override void ReadSegmentData(byte[] segmentData, StreamWriter logFile)
        {
            string dataStr = Encoding.UTF8.GetString(segmentData);
            if (dataStr.StartsWith("UFRxDoc_Sim_Version:"))
            {
                Logger.Info(logFile, "UFRxDocReader: Detected UFRxDoc_Sim_Version data.");
                var header1 = new UFRxHeader1();
                var props = dataStr.Split(';');
                foreach(var prop in props)
                {
                    var kv = prop.Split(new[] {':'}, 2);
                    if (kv.Length == 2)
                    {
                        string key = kv[0].Trim();
                        string value = kv[1].Trim();
                        if (key == "UFRxDoc_Sim_Version") header1.VersionString = value;
                        else if (key == "FileName") header1.FileName = value;
                        else if (key == "DocGUID") header1.DocGuid = Guid.TryParse(value, out var guid) ? guid : Guid.Empty;
                        // TODO: Add more properties as needed by the simulation and UFRxHeader1
                    }
                }
                header1.ParsedVersion = new VersionInfo();
                if (!string.IsNullOrEmpty(header1.VersionString)) {
                    var parts = header1.VersionString.Split('.');
                    if (parts.Length > 0) int.TryParse(parts[0], out header1.ParsedVersion.Major);
                    if (parts.Length > 1) int.TryParse(parts[1], out header1.ParsedVersion.Minor);
                    // Add more detailed version parsing if needed
                }

                this.Segment.ParsedContent["Header1"] = header1;
                if (this.Segment.ParentModel != null) { // ParentModel is the Inventor object
                     if(this.Segment.ParentModel.UFRxDoc == null) this.Segment.ParentModel.UFRxDoc = new UFRxDocument();
                     this.Segment.ParentModel.UFRxDoc.Header1 = header1;
                     // Also try to set the segment's own version from this
                     this.Segment.Version = header1.ParsedVersion;
                }

                Logger.Info(logFile, $"UFRxDocReader: Finished processing UFRxDoc_Sim_Version data. Version: {header1.VersionString}");
                return;
            }

            logFile?.WriteLine($"UFRxDocReader: ReadSegmentData for {Segment.Name} - Length: {segmentData.Length}. Actual byte parsing logic placeholder.");
            this.Segment.ParsedContent["RawData"] = segmentData;
        }
    }

    public class AppReader : SegmentReader
    {
        private ColorPlaceholder? _defaultColor;
        public AppReader(RSeSegment segment) : base(segment) { }
        protected override void PopulateDataReaderMethods() { /* ... content from previous state ... */ _dataReaderMethods["10389219-5734-4737-9C3E-95271D857901"] = Read_ApplicationProperties; _dataReaderMethods["10D6C06B-73C0-4ACA-A268-550F83D0339F"] = Read_DefaultStyle; _dataReaderMethods["11FBECCD-B29E-4057-A277-75611C356307"] = Read_DefaultStyles; _dataReaderMethods["A27E58F3-2452-4687-A07E-F69DECF97E6B"] = Read_ReferencedFiles; _dataReaderMethods["B080E131-F87B-4A7F-A0CE-B0E341018F37"] = Read_UnitsOfMeasure; _dataReaderMethods["F4B9C0A2-11D1-11D2-910F-0000F8061098"] = Read_RenderStyleDef; _dataReaderMethods["F8A77A4E-11D1-11D2-910F-0000F8061098"] = Read_MaterialDef; _dataReaderMethods["DAF73D46-B525-11D1-8D78-0008C75E7068"] = Read_DocumentSettings; _dataReaderMethods["FB8BDD34-AA94-11D1-8D78-0008C75E7068"] = Read_ChangeManager; _dataReaderMethods["6759D86F-C308-11D1-8D6C-0008C75E7068"] = Read_RenderingStyleProperties; _dataReaderMethods["1C4CFF13-11D0-D1F8-0008CABC0663DC09"] = Read_TextStyleCollection; }
        public override void ReadSegmentData(byte[] segmentData, StreamWriter logFile) { /* ... content from previous state ... */ if (Segment.Nodes != null && Segment.Nodes.Count > 0) { foreach (var node in Segment.Nodes) ReadBlock(node); PostRead(logFile); } else logFile?.WriteLine($"AppReader: No pre-scanned nodes for {Segment.Name}."); }
        public void PostRead(StreamWriter logFile) { /* Placeholder */ }
        private void Read_ApplicationProperties(SecNode node) { /* ... content from previous state ... */ node.ReadHeader0(Logger.LogWriter); node.ReadLen32Text16("AppName", Logger.LogWriter); node.ReadUInt32("AppVersion", Logger.LogWriter); }
        private void Read_DefaultStyle(SecNode node) { /* ... content from previous state ... */ node.ReadHeader0(Logger.LogWriter); _defaultColor = node.ReadColorRgba("DefaultColor", Logger.LogWriter); }
        private void Read_DefaultStyles(SecNode node) { /* ... content from previous state ... */ Logger.Info($"AppReader: Reading DefaultStyles (UID: {node.Uid})"); node.ReadHeader0(Logger.LogWriter); node.ReadChildRef("DefaultMaterialRef", Logger.LogWriter); node.ReadChildRef("DefaultRenderingStyleRef", Logger.LogWriter); node.ReadChildRef("LayerCollectionRef", Logger.LogWriter); node.ReadChildRef("DefaultDimensionStyleRef", Logger.LogWriter); node.ReadChildRef("DefaultStandardRef", Logger.LogWriter); node.ParsedContent["Summary"] = "Parsed DefaultStyles"; }
        private void Read_ReferencedFiles(SecNode node) { /* ... content from previous state ... */ node.ReadHeader0(Logger.LogWriter); node.ReadChildRefList("FilesList", Logger.LogWriter); }
        private void Read_UnitsOfMeasure(SecNode node) { /* ... content from previous state ... */ node.ReadHeader0(Logger.LogWriter); node.ReadUInt32("UnitsFlags", Logger.LogWriter); node.ReadFloat64("LinearConversionFactor", Logger.LogWriter); }
        private void Read_RenderStyleDef(SecNode node) { /* ... content from previous state ... */ Logger.Info($"AppReader: Reading RenderStyleDefinition (UID: {node.Uid})"); ReadHeaderSU32S(node, "RenderStyleDefinition", Logger.LogWriter); node.ParsedContent["Summary"] = "Parsed RenderStyleDefinition (stub)"; }
        private void Read_MaterialDef(SecNode node) { /* ... content from previous state ... */ Logger.Info($"AppReader: Reading MaterialDefinition (UID: {node.Uid})"); ReadHeaderSU32S(node, "MaterialDefinition", Logger.LogWriter); node.ParsedContent["Summary"] = "Parsed MaterialDefinition (stub)"; }
        private void Read_DocumentSettings(SecNode node) { /* ... content from previous state ... */ Logger.Info($"AppReader: Reading DocumentSettings (UID: {node.Uid})"); node.ReadHeader0(Logger.LogWriter); node.ReadLen32Text16("SettingsName", Logger.LogWriter); node.ReadBoolean("EnableAdaptiveStatus", Logger.LogWriter); node.ReadBoolean("EnableRuntimeErrorChecking", Logger.LogWriter); node.ReadChildRef("UnitsOfMeasureRef", Logger.LogWriter); node.ReadChildRef("DefaultRenderStyleRef", Logger.LogWriter); node.ReadChildRef("ActiveDetailLevelRef", Logger.LogWriter); node.ReadChildRef("ActiveColorSchemeRef", Logger.LogWriter); node.ParsedContent["Summary"] = $"Parsed DocumentSettings: Name='{node.ParsedContent.GetValueOrDefault("SettingsName", "N/A")}'"; }
        private void Read_ChangeManager(SecNode node) { /* ... content from previous state ... */ Logger.Info($"AppReader: Reading ChangeManager (UID: {node.Uid})"); ReadHeaderSU32S(node, "ChangeManager", Logger.LogWriter); node.ReadUInt32("ChangeStamp", Logger.LogWriter); node.ReadLen32Text16("ManagerIdentifier", Logger.LogWriter); node.ParsedContent["Summary"] = $"Parsed ChangeManager: Stamp='{node.ParsedContent.GetValueOrDefault("ChangeStamp", "N/A")}'"; }
        private void Read_RenderingStyleProperties(SecNode node) { /* ... content from previous state, including bump map additions ... */ Logger.Info($"AppReader: Reading RenderingStyleProperties (UID: {node.Uid})"); ReadHeaderSU32S(node, "RenderingStyleProperties", Logger.LogWriter); node.ReadLen32Text16("StyleName", Logger.LogWriter); node.ReadColorRgba("AmbientColor", Logger.LogWriter); node.ReadColorRgba("DiffuseColor", Logger.LogWriter); node.ReadColorRgba("SpecularColor", Logger.LogWriter); node.ReadColorRgba("EmissiveColor", Logger.LogWriter); node.ReadFloat32("Shininess", Logger.LogWriter); node.ReadFloat32("Opacity", Logger.LogWriter); node.ReadLen32Text16("TextureFileRelative", Logger.LogWriter); node.ReadLen32Text16("TextureFileAbsolute", Logger.LogWriter); node.ReadSInt32("TextureType", Logger.LogWriter); node.ReadBoolean("TextureEnabled", Logger.LogWriter); node.ReadFloat32("TextureScaleU", Logger.LogWriter); node.ReadFloat32("TextureScaleV", Logger.LogWriter); node.ReadFloat32("TextureOffsetU", Logger.LogWriter); node.ReadFloat32("TextureOffsetV", Logger.LogWriter); node.ReadFloat32("TextureAngle", Logger.LogWriter); node.ReadColorRgba("TextureBlendColor", Logger.LogWriter); node.ReadUInt32("TextureFlags", Logger.LogWriter); if (Segment.Version.IsGreaterOrEqualTo(10, 0, 0)) { node.ReadFloat32("ReflectionFactor", Logger.LogWriter); node.ReadUInt32("RenderType", Logger.LogWriter); node.SkipBytes(4*8, Logger.LogWriter, "R10 floats/vectors (TextureTiling, Shear, NormalProjection, etc.)"); } if (Segment.Version.IsGreaterOrEqualTo(2009,0,0)) { node.ReadUInt32("IlluminationModel", Logger.LogWriter); node.SkipBytes(4, Logger.LogWriter, "R2009 u32_0"); } if (Segment.Version.IsGreaterOrEqualTo(6,0,0)) { node.ReadLen32Text16("FileMapBump", Logger.LogWriter); node.ReadBoolean("BumpMapEnabled", Logger.LogWriter); node.ReadFloat32("BumpMapScaleU", Logger.LogWriter); node.ReadFloat32("BumpMapScaleV", Logger.LogWriter); node.ReadFloat32("BumpMapOffsetU", Logger.LogWriter); node.ReadFloat32("BumpMapOffsetV", Logger.LogWriter); node.ReadFloat32("BumpMapAngle", Logger.LogWriter); } node.ParsedContent["Summary"] = $"Parsed RenderingStyleProperties: Name='{node.ParsedContent.GetValueOrDefault("StyleName", "N/A")}'"; }
        private void Read_TextStyleCollection(SecNode node) { /* ... content from previous state ... */ Logger.Info($"AppReader: Reading TextStyleCollection (UID: {node.Uid})"); ReadHeaderSU32S(node, "TextStyleCollection", Logger.LogWriter); node.ReadUInt32("UnknownFlags1", Logger.LogWriter); node.ReadUInt32("UnknownFlags2", Logger.LogWriter); node.ReadChildRefList("TextStyles", Logger.LogWriter); node.ParsedContent["Summary"] = "Parsed TextStyleCollection"; }
    }

    public class DCReader : SegmentReader
    {
        public DCReader(RSeSegment segment) : base(segment) { }
        protected override void PopulateDataReaderMethods() { /* ... content from previous state ... */ _dataReaderMethods["90874D16-11D0-D1F8-0008CABC0663DC09"] = Read_RDxPart; _dataReaderMethods["CE52DF3A-11D0-D2D0-0008CCBC0663DC09"] = Read_SketchLine2D; _dataReaderMethods["CA7163A3-11D0-D3B2-0008BFBB21EDDC09"] = Read_PMxPartNode; _dataReaderMethods["D31891C2-48BF-14C3-AA42-EA872A846B2A"] = Read_UFRxRef; _dataReaderMethods["0AA8AF46-E23E-4E82-8E3F-55A03A184489"] = Read_ParameterConstant; _dataReaderMethods["F8A77A03-11D1-11D2-910F-0000F8061098"] = Read_ParameterFunction; _dataReaderMethods["F8A77A05-11D1-11D2-910F-0000F8061098"] = Read_ParameterRef; _dataReaderMethods["RDXPARAMETER_UID_PLACEHOLDER"] = Read_Parameter; _dataReaderMethods["CE52DF3E-11D0-D2D0-0008CCBC0663DC09"] = Read_SketchPoint2D; _dataReaderMethods["CE52DF3B-11D0-D2D0-0008CCBC0663DC09"] = Read_SketchCircle2D; _dataReaderMethods["90874D94-11D0-D1F8-0008CABC0663DC09"] = Read_CoincidentConstraint2D; _dataReaderMethods["CE52DF3E-3DPT-4E82-8E3F-55A03A184489"] = Read_SketchPoint3D; _dataReaderMethods["8EF06C89-11D1-11D2-910F-0000F8061098"] = Read_SketchLine3D; _dataReaderMethods["9E43716A-11D2-0FA5-6000-84B7B035C3B0"] = Read_SketchCircle3D; _dataReaderMethods["90874D91-11D0-D1F8-0008CABC0663DC09"] = Read_Feature; _dataReaderMethods["729ABE28-11D1-11D2-910F-0000F8061098"] = Read_PartFeatureOperationEnum; _dataReaderMethods["DC_PART_COMP_DEF_UID_PLACEHOLDER"] = Read_PartComponentDefinition; _dataReaderMethods["90874D64-11D0-D1F8-0008CABC0663DC09"] = Read_RDxRefPlane; _dataReaderMethods["90874D66-11D0-D1F8-0008CABC0663DC09"] = Read_RDxWorkAxis; _dataReaderMethods["0E6870AE-11D0-D1F8-0008CABC0663DC09"] = Read_ObjectCollection; _dataReaderMethods["4E86F047-11D2-11D2-910F-0000F8061098"] = Read_AssemblyConstraint; _dataReaderMethods["90874D11-11D0-D1F8-0008CABC0663DC09"] = Read_PlanarSketch; _dataReaderMethods["338634AC-11D0-D1F8-0008CABC0663DC09"] = Read_RDxAssemblyComponentInstance; _dataReaderMethods["F3A02CB6-11D2-11D2-910F-0000F8061098"] = Read_RDxAssemblyOccurrence; _dataReaderMethods["DC_PARAM_PROXY_UID"] = Read_RDxParameterProxy; _dataReaderMethods["0B85010C-11D0-D1F8-0008CABC0663DC09"] = Read_RDxParameterProxy; _dataReaderMethods["258EC6E1-11D0-D1F8-0008CABC0663DC09"] = Read_RDxParameterProxy; _dataReaderMethods["5CC350B8-11D0-D1F8-0008CABC0663DC09"] = Read_RDxParameterProxy;  _dataReaderMethods["DC_BOOL_PARAM_UID"] = Read_RDxBooleanParameter; _dataReaderMethods["90874D28-11D0-D1F8-0008CABC0663DC09"] = Read_RDxBooleanParameter;  _dataReaderMethods["DC_EXTENT_UID"] = Read_RDxExtentPlaceholder; _dataReaderMethods["0CA07988-11D0-D1F8-0008CABC0663DC09"] = Read_RDxExtentPlaceholder;  }
        private void ReadHeaderS32ss(SecNode node, string typeName, StreamWriter logFile) { /* ... */ }
        private void ReadHeaderCntHdr3S(SecNode node, string typeName, StreamWriter logFile) { /* ... */ }
        private void ReadHeaderContent(SecNode node, string typeName, StreamWriter logFile, string nextRefName = "NextContentRef") { /* ... */ }
        private void Read_RDxPart(SecNode node) { /* Placeholder */ } private void Read_SketchLine2D(SecNode node) { /* Placeholder */ } private void Read_PMxPartNode(SecNode node) { /* Placeholder */ } private void Read_UFRxRef(SecNode node) { /* Placeholder */ } private void Read_ParameterConstant(SecNode node) { /* Placeholder */ } private void Read_ParameterFunction(SecNode node) { /* Placeholder */ } private void Read_ParameterRef(SecNode node) { /* Placeholder */ } private void Read_Parameter(SecNode node) { /* Placeholder */ } private void ReadHeaderSketch2DEntity(SecNode node, string s, StreamWriter sw) { /* Placeholder */ } private void ReadHeaderConstraint2D(SecNode node, string s, StreamWriter sw) { /* Placeholder */ } private void Read_SketchPoint2D(SecNode node) { /* Placeholder */ } private void Read_SketchCircle2D(SecNode node) { /* Placeholder */ } private void Read_CoincidentConstraint2D(SecNode node) { /* Placeholder */ } private void ReadHeaderSketch3DEntity(SecNode node, string s, StreamWriter sw) { /* Placeholder */ } private void Read_Feature(SecNode node) { Logger.Info($"DCReader: Reading Feature (UID: {node.Uid}) - Full parsing deferred."); } private void Read_PartFeatureOperationEnum(SecNode node) { /* Placeholder */ } private void Read_PartComponentDefinition(SecNode node) { /* Placeholder */ } private void Read_RDxRefPlane(SecNode node) { /* Placeholder */ } private void Read_RDxWorkAxis(SecNode node) { /* Placeholder */ }
        private void Read_ObjectCollection(SecNode node) { /* ... */ } private void Read_AssemblyConstraint(SecNode node) { /* ... */ } private void Read_PlanarSketch(SecNode node) { /* ... */ } private void Read_RDxAssemblyComponentInstance(SecNode node) { /* ... */ } private void Read_RDxAssemblyOccurrence(SecNode node) { /* ... */ }
        private void Read_RDxParameterProxy(SecNode node) { /* ... */ } private void Read_RDxBooleanParameter(SecNode node) { /* ... */ } private void Read_RDxExtentPlaceholder(SecNode node) { /* ... */ }
    }

    public class DirectoryReader : SegmentReader { public DirectoryReader(RSeSegment s) : base(s) { } protected override void PopulateDataReaderMethods() { } public override void ReadSegmentData(byte[] d, StreamWriter l) { /* Placeholder */ } }

    public static class Logger
    {
        public static StreamWriter LogWriter { get; set; }
        public static void Info(string message) { LogWriter?.WriteLine($"INFO: {message}"); Console.WriteLine($"INFO: {message}"); }
        public static void Info(StreamWriter logFileContext, string message) { (logFileContext ?? LogWriter)?.WriteLine($"INFO: {message}"); Console.WriteLine($"INFO: {message}"); }
        public static void Warning(string message) { LogWriter?.WriteLine($"WARNING: {message}"); Console.WriteLine($"WARNING: {message}"); }
        public static void Warning(StreamWriter logFileContext, string message) { (logFileContext ?? LogWriter)?.WriteLine($"WARNING: {message}"); Console.WriteLine($"WARNING: {message}"); }
        public static void Error(string message) { LogWriter?.WriteLine($"ERROR: {message}"); Console.WriteLine($"ERROR: {message}"); }
        public static void Error(StreamWriter logFileContext, string message) { (logFileContext ?? LogWriter)?.WriteLine($"ERROR: {message}"); Console.WriteLine($"ERROR: {message}"); }
    }
}
