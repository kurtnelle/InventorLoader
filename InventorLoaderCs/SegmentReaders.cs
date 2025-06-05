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
            else logFile?.WriteLine($"EeSceneReader: No pre-scanned nodes for {Segment.Name}.");
            logFile?.WriteLine($"EeSceneReader: Finished reading segment data for {Segment.Name}.");
        }

        public void ReadHeader3dObject(SecNode node, StreamWriter logFile, string typeName = null, string ref1Name = "childObjectsListHeaderRef")
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
            // Content based on previous successful parsing/subtask
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
        }

        private void Read_A79EACCF_3dObject(SecNode node) // 3D Object Group/Node
        {
            Logger.Info($"EeSceneReader: Reading Generic 3dObject (UID: {node.Uid})");
            ReadHeader3dObject(node, Logger.LogWriter, "Generic3dObject", ref1Name: "childObjectsListHeaderRef");
            Logger.Info($"EeSceneReader.Read_A79EACCF_3dObject: Child objects list expected via ref: {node.ParsedContent.GetValueOrDefault("childObjectsListHeaderRef")}");
            // Python: i = self.ReadOptionalTransformation(node, i)
            // Inlining ReadOptionalTransformation3D logic:
            bool? hasTransform = node.ReadBoolean("Transformation.HasTransformFlag", Logger.LogWriter);
            if (hasTransform.HasValue && hasTransform.Value)
            {
                node.ReadTransformation3D("Transformation", Logger.LogWriter); // Uses SecNode.ReadTransformation3D
            }
            else
            {
                node.ParsedContent["Transformation"] = Matrix4x4.Identity;
            }
            Objects3D.Add(node.ParsedContent);
            node.ParsedContent["Summary"] = "Parsed Generic 3dObject with Optional Transformation";
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
            SkipBlockSize(node, node.CurrentReadOffset, 2, Logger.LogWriter); // Simplified from ReadHeaderSurface
            node.ReadParentRef("parentRef", Logger.LogWriter);
            SkipBlockSize(node, node.CurrentReadOffset, 1, Logger.LogWriter);
            node.ReadUInt32("u32_0", Logger.LogWriter);
            node.ReadUInt8("u8_0", Logger.LogWriter);
            node.ParsedContent["Summary"] = "Parsed SurfacePlane";
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
            _dataReaderMethods["A98235C4-11D0-D1F8-0008CABC0663DC09"] = Read_A98235C4_MeshPart;
            _dataReaderMethods["A79EACD1-F6C0-449B-9508-2283E9799B94"] = Read_A79EACD1_GraphicsNodeList;
        }

        protected void ReadHeaderU32RefU8List3(SecNode node, string typeName, string listName, StreamWriter logFile)
        {
            Logger.Info($"GraphicsReader: Reading HeaderU32RefU8List3 for '{typeName}' (UID: {node.Uid})");
            node.ReadHeader0(logFile);
            node.ReadUInt32($"{typeName}.Flags", logFile);
            node.ReadChildRef($"{typeName}.StyleOrParentRef", logFile);
            node.ReadUInt8($"{typeName}.ItemCountFromHeader", logFile);
            node.ReadChildRef($"{listName}_ListHeaderRef", logFile);
            node.ReadParentRef($"{typeName}.DCModelRef", logFile);
            node.ReadUInt32($"{typeName}.UnknownUInt32_1", logFile);
            node.ReadSInt32($"{typeName}.DCIndex", logFile);
            node.ParsedContent[$"{typeName}_HeaderU32RefU8List3_Processed"] = true;
        }

        private void Read_4B26ED59_Mesh(SecNode node)
        {
            Logger.Info($"GraphicsReader: Reading Mesh (UID: {node.Uid})");
            ReadHeaderU32RefU8List3(node, "Mesh", "MeshItemsList", Logger.LogWriter);
            if (Version.IsGreaterThan(2017,0,0)) node.SkipBytes(4, Logger.LogWriter, "Version>2017 skip");
            node.ParsedContent["Summary"] = "Parsed Mesh";
        }

        private void Read_60FD1845_Sketch2D(SecNode node)
        {
            Logger.Info($"GraphicsReader: Reading Sketch2D (UID: {node.Uid})");
            node.ReadHeader0(Logger.LogWriter);
            node.ReadChildRef("SketchObjectRef", Logger.LogWriter);
            SkipBlockSize(node, node.CurrentReadOffset, 1, Logger.LogWriter);
            node.ReadUInt8("u8_1", Logger.LogWriter);
            SkipBlockSize(node, node.CurrentReadOffset, 1, Logger.LogWriter);
            node.ReadUInt32("Key", Logger.LogWriter);
            bool? hasTransform = node.ReadBoolean("Sketch2D.Transformation.HasTransformFlag", Logger.LogWriter);
            if (hasTransform.HasValue && hasTransform.Value) { node.ReadTransformation3D("Sketch2D.Transformation", Logger.LogWriter); }
            else { node.ParsedContent["Sketch2D.Transformation"] = Matrix4x4.Identity; }
            node.ParsedContent["Summary"] = "Parsed Sketch2D";
        }

        private void Read_CA7163A3_PartNode(SecNode node)
        {
            Logger.Info($"GraphicsReader: Reading PartNode (UID: {node.Uid})");
            ReadHeaderU32RefU8List3(node, "PartNode", "ChildPartsList", Logger.LogWriter);
            node.ReadMaterial("MaterialInfo", Logger.LogWriter);
            node.ParsedContent["Summary"] = "Parsed PartNode";
        }

        private void Read_A98235C4_MeshPart(SecNode node)
        {
            Logger.Info($"GraphicsReader: Reading MeshPart (UID: {node.Uid})");
            ReadHeaderU32RefU8List3(node, "MeshPart", "PartsList", Logger.LogWriter);
            node.ReadChildRef("Object3D_Ref", Logger.LogWriter);
            node.ReadTransformation3D("MeshPart.Transformation", Logger.LogWriter);
            node.ReadUInt32("MeshID", Logger.LogWriter);
            if (Segment.Version.IsGreaterOrEqualTo(2009,0,0))
            {
                node.ReadLen32Text8("Text1_Post2008", Logger.LogWriter);
            }
            node.ParsedContent["Summary"] = $"Parsed MeshPart: MeshID='{node.ParsedContent.GetValueOrDefault("MeshID", "N/A")}'";
        }

        private void Read_A79EACD1_GraphicsNodeList(SecNode node)
        {
            Logger.Info($"GraphicsReader: Reading GraphicsNodeList (UID: {node.Uid})");
            ReadHeader3dObject(node, Logger.LogWriter, "GraphicsNodeList", ref1Name: "objectsListHeader");
            node.ReadChildRefList("objects", Logger.LogWriter);
            bool? hasTransform = node.ReadBoolean("Transformation.HasTransformFlag", Logger.LogWriter);
            if (hasTransform.HasValue && hasTransform.Value) { node.ReadTransformation3D("Transformation", Logger.LogWriter); }
            else { node.ParsedContent["Transformation"] = Matrix4x4.Identity; }
            node.ReadUInt32("u32_1_PostTransform", Logger.LogWriter);
            node.ReadBoolean("b0_PostTransform", Logger.LogWriter);
            node.ReadListOfVector3DFloat64("points", Logger.LogWriter);
            node.ReadListOfUInt32("indices", Logger.LogWriter);
            if (Segment.Version.IsGreaterOrEqualTo(2011,0,0))
            {
                node.ReadChildRefList("lst3_Post2010", Logger.LogWriter);
            }
            node.ParsedContent["Summary"] = "Parsed GraphicsNodeList";
        }
    }

    public class FBAttributeReader : SegmentReader { public FBAttributeReader(RSeSegment s) : base(s) { } protected override void PopulateDataReaderMethods() { } public override void ReadSegmentData(byte[] d, StreamWriter l) { } }
    public class NotebookReader : SegmentReader { public NotebookReader(RSeSegment s) : base(s) { } protected override void PopulateDataReaderMethods() { } public override void ReadSegmentData(byte[] d, StreamWriter l) { } }
    public class ResultReader : SegmentReader { public ResultReader(RSeSegment s) : base(s) { } protected override void PopulateDataReaderMethods() { } public override void ReadSegmentData(byte[] d, StreamWriter l) { } }
    public class NameTableReader : SegmentReader { public NameTableReader(RSeSegment s) : base(s) { Read_NameTableData_UID = "CCE92042-C27B-11D1-8D6C-0008C75E7068"; } protected string Read_NameTableData_UID; protected override void PopulateDataReaderMethods() { _dataReaderMethods[Read_NameTableData_UID] = Read_NameTableData; } public override void ReadSegmentData(byte[] d, StreamWriter l) { if (d.Length > 0) { SecNode node = new SecNode(Read_NameTableData_UID, d, 0, d.Length); ReadBlock(node); } } private void Read_NameTableData(SecNode n) { } }
    public class SheetDlReader : SegmentReader { public SheetDlReader(RSeSegment s) : base(s) { } protected override void PopulateDataReaderMethods() { } public override void ReadSegmentData(byte[] d, StreamWriter l) { } }
    public class SheetSmReader : SegmentReader { public SheetSmReader(RSeSegment s) : base(s) { } protected override void PopulateDataReaderMethods() { } public override void ReadSegmentData(byte[] d, StreamWriter l) { } }

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
            _dataReaderMethods["F4B9C0A2-11D1-11D2-910F-0000F8061098"] = Read_RenderStyleDef;
            _dataReaderMethods["F8A77A4E-11D1-11D2-910F-0000F8061098"] = Read_MaterialDef;
            _dataReaderMethods["DAF73D46-B525-11D1-8D78-0008C75E7068"] = Read_DocumentSettings;
            _dataReaderMethods["FB8BDD34-AA94-11D1-8D78-0008C75E7068"] = Read_ChangeManager;
            _dataReaderMethods["6759D86F-C308-11D1-8D6C-0008C75E7068"] = Read_RenderingStyleProperties; // Already present, will be enhanced
            _dataReaderMethods["11FBECCD-B29E-4057-A277-75611C356307"] = Read_DefaultStyles; // Renamed from StyleDefinitions for clarity and function
            _dataReaderMethods["1C4CFF13-11D0-D1F8-0008CABC0663DC09"] = Read_TextStyleCollection; // UID from Python for TextStyleCollection
        }

        public override void ReadSegmentData(byte[] segmentData, StreamWriter logFile)
        {
            if (Segment.Nodes != null && Segment.Nodes.Count > 0) { foreach (var node in Segment.Nodes) ReadBlock(node); PostRead(logFile); }
            else logFile?.WriteLine($"AppReader: No pre-scanned nodes for {Segment.Name}.");
        }

        public void PostRead(StreamWriter logFile) { /* ... */ }
        private void Read_ApplicationProperties(SecNode node) { node.ReadHeader0(Logger.LogWriter); node.ReadLen32Text16("AppName", Logger.LogWriter); node.ReadUInt32("AppVersion", Logger.LogWriter); }
        private void Read_DefaultStyle(SecNode node) { node.ReadHeader0(Logger.LogWriter); _defaultColor = node.ReadColorRgba("DefaultColor", Logger.LogWriter); }
        // Renamed Read_StyleDefinitions to Read_DefaultStyles to better match its Python counterpart UID and function
        private void Read_DefaultStyles(SecNode node)
        {
            Logger.Info($"AppReader: Reading DefaultStyles (UID: {node.Uid})");
            node.ReadHeader0(Logger.LogWriter);
            // Python's Read_11FBECCD reads multiple ChildRefs:
            // DefaultMaterial, DefaultRenderingStyle, LayerCollection, DefaultDimensionStyle, etc.
            // For now, reading as a generic list. Specific ChildRefs can be parsed if their meaning is clear.
            node.ReadChildRef("DefaultMaterialRef", Logger.LogWriter);
            node.ReadChildRef("DefaultRenderingStyleRef", Logger.LogWriter);
            node.ReadChildRef("LayerCollectionRef", Logger.LogWriter);
            node.ReadChildRef("DefaultDimensionStyleRef", Logger.LogWriter);
            node.ReadChildRef("DefaultStandardRef", Logger.LogWriter);
            // ... potentially more ChildRefs based on file version or specific document type
            node.ParsedContent["Summary"] = "Parsed DefaultStyles (structure is approximate, read several ChildRefs)";
        }
        private void Read_ReferencedFiles(SecNode node) { node.ReadHeader0(Logger.LogWriter); node.ReadChildRefList("FilesList", Logger.LogWriter); }
        private void Read_UnitsOfMeasure(SecNode node) { node.ReadHeader0(Logger.LogWriter); node.ReadUInt32("UnitsFlags", Logger.LogWriter); node.ReadFloat64("LinearConversionFactor", Logger.LogWriter); }

        private void Read_RenderStyleDef(SecNode node)
        {
            Logger.Info($"AppReader: Reading RenderStyleDefinition (UID: {node.Uid})");
            ReadHeaderSU32S(node, "RenderStyleDefinition", Logger.LogWriter);
            node.ParsedContent["Summary"] = "Parsed RenderStyleDefinition (stub)";
        }
        private void Read_MaterialDef(SecNode node)
        {
            Logger.Info($"AppReader: Reading MaterialDefinition (UID: {node.Uid})");
            ReadHeaderSU32S(node, "MaterialDefinition", Logger.LogWriter);
            node.ParsedContent["Summary"] = "Parsed MaterialDefinition (stub)";
        }
        private void Read_DocumentSettings(SecNode node)
        {
            Logger.Info($"AppReader: Reading DocumentSettings (UID: {node.Uid})");
            node.ReadHeader0(Logger.LogWriter);
            node.ReadLen32Text16("SettingsName", Logger.LogWriter);
            node.ReadBoolean("EnableAdaptiveStatus", Logger.LogWriter);
            node.ReadBoolean("EnableRuntimeErrorChecking", Logger.LogWriter);
            node.ReadChildRef("UnitsOfMeasureRef", Logger.LogWriter);
            node.ReadChildRef("DefaultRenderStyleRef", Logger.LogWriter);
            node.ReadChildRef("ActiveDetailLevelRef", Logger.LogWriter);
            node.ReadChildRef("ActiveColorSchemeRef", Logger.LogWriter);
            node.ParsedContent["Summary"] = $"Parsed DocumentSettings: Name='{node.ParsedContent.GetValueOrDefault("SettingsName", "N/A")}'";
        }
        private void Read_ChangeManager(SecNode node)
        {
            Logger.Info($"AppReader: Reading ChangeManager (UID: {node.Uid})");
            ReadHeaderSU32S(node, "ChangeManager", Logger.LogWriter);
            node.ReadUInt32("ChangeStamp", Logger.LogWriter);
            node.ReadLen32Text16("ManagerIdentifier", Logger.LogWriter);
            node.ParsedContent["Summary"] = $"Parsed ChangeManager: Stamp='{node.ParsedContent.GetValueOrDefault("ChangeStamp", "N/A")}'";
        }
        private void Read_RenderingStyleProperties(SecNode node)
        {
            Logger.Info($"AppReader: Reading RenderingStyleProperties (UID: {node.Uid})");
            ReadHeaderSU32S(node, "RenderingStyleProperties", Logger.LogWriter);
            node.ReadLen32Text16("StyleName", Logger.LogWriter);
            node.ReadColorRgba("AmbientColor", Logger.LogWriter);
            node.ReadColorRgba("DiffuseColor", Logger.LogWriter);
            node.ReadColorRgba("SpecularColor", Logger.LogWriter);
            node.ReadColorRgba("EmissiveColor", Logger.LogWriter);
            node.ReadFloat32("Shininess", Logger.LogWriter);
            node.ReadFloat32("Opacity", Logger.LogWriter);
            node.ReadLen32Text16("TextureFileRelative", Logger.LogWriter);
            node.ReadLen32Text16("TextureFileAbsolute", Logger.LogWriter);
            node.ReadSInt32("TextureType", Logger.LogWriter);
            node.ReadBoolean("TextureEnabled", Logger.LogWriter);
            node.ReadFloat32("TextureScaleU", Logger.LogWriter);
            node.ReadFloat32("TextureScaleV", Logger.LogWriter);
            node.ReadFloat32("TextureOffsetU", Logger.LogWriter);
            node.ReadFloat32("TextureOffsetV", Logger.LogWriter);
            node.ReadFloat32("TextureAngle", Logger.LogWriter);
            node.ReadColorRgba("TextureBlendColor", Logger.LogWriter);
            node.ReadUInt32("TextureFlags", Logger.LogWriter);
            if (Segment.Version.IsGreaterOrEqualTo(10, 0, 0))
            {
                node.ReadFloat32("ReflectionFactor", Logger.LogWriter);
                node.ReadUInt32("RenderType", Logger.LogWriter);
                // Python reads: Vec2d "TextureTilingFactor", Vec2d "TextureShear", Vec2d "TextureNormalProjectionVector"
                // Each Vec2d is 2 floats. So 2*4 + 2*4 + 2*4 = 24 bytes.
                // The existing C# SkipBytes(4*8) = 32 bytes. This might be for 4 Vec2d or other data.
                // Let's assume the 32 bytes is correct for now, possibly covering more than just these 3 Vec2d.
                node.SkipBytes(4*8, Logger.LogWriter, "R10 floats/vectors (TextureTiling, Shear, NormalProjection, etc.)");
            }
            if (Segment.Version.IsGreaterOrEqualTo(2009,0,0))
            {
                node.ReadUInt32("IlluminationModel", Logger.LogWriter);
                node.SkipBytes(4, Logger.LogWriter, "R2009 u32_0");
            }
            // Python's Read_6759D86F also reads BumpMapFile and related parameters
            // These might be version dependent or always present after a certain point.
            // Adding them here, assuming they follow the previously parsed fields.
            // This part needs verification against actual file structures or more detailed Python logic.
            if (Segment.Version.IsGreaterOrEqualTo(6,0,0)) // Assuming bump maps were introduced around R6 or similar
            {
                 node.ReadLen32Text16("FileMapBump", Logger.LogWriter);
                 node.ReadBoolean("BumpMapEnabled", Logger.LogWriter);
                 node.ReadFloat32("BumpMapScaleU", Logger.LogWriter);
                 node.ReadFloat32("BumpMapScaleV", Logger.LogWriter);
                 node.ReadFloat32("BumpMapOffsetU", Logger.LogWriter);
                 node.ReadFloat32("BumpMapOffsetV", Logger.LogWriter);
                 node.ReadFloat32("BumpMapAngle", Logger.LogWriter);
                 // There might be more bump-related flags or properties.
            }

            node.ParsedContent["Summary"] = $"Parsed RenderingStyleProperties: Name='{node.ParsedContent.GetValueOrDefault("StyleName", "N/A")}'";
        }

        private void Read_TextStyleCollection(SecNode node)
        {
            Logger.Info($"AppReader: Reading TextStyleCollection (UID: {node.Uid})");
            // Python's Read_1C4CFF13 reads a header (like ReadHeaderSU32S)
            // then a List2 of NodeRefs to TextStyles.
            ReadHeaderSU32S(node, "TextStyleCollection", Logger.LogWriter); // Common style header
            // After the header, specific fields for TextStyleCollection might exist before the list.
            // Python example shows: ReadUInt32 "u32_0", ReadUInt32 "u32_1"
            node.ReadUInt32("UnknownFlags1", Logger.LogWriter);
            node.ReadUInt32("UnknownFlags2", Logger.LogWriter);

            node.ReadChildRefList("TextStyles", Logger.LogWriter); // Reads List2 of NodeRefs
            node.ParsedContent["Summary"] = "Parsed TextStyleCollection";
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
            if (Segment.Nodes != null && Segment.Nodes.Count > 0) { foreach (var node in Segment.Nodes) ReadBlock(node); }
            else if (segmentData.Length > 0) { SecNode acisNode = new SecNode(AcisBinaryDataUID, segmentData, 0, segmentData.Length); Read_AcisBinaryData(acisNode); }
        }
        private void Read_AcisBinaryData(SecNode node)
        {
            Logger.Info($"BRepReader: Reading AcisBinaryData (UID: {node.Uid})");
            node.ReadHeader0(Logger.LogWriter);
            int acisDataOffset = node.CurrentReadOffset;
            int acisDataSize = node.Offset + node.Size - acisDataOffset;
            if (acisDataSize <=0) { node.ParsedContent["ACIS_Error"] = "No data"; return; }
            byte[] acisData = new byte[acisDataSize];
            Array.Copy(node.FullDataBuffer, acisDataOffset, acisData, 0, acisDataSize);
            AcisReader acisReader = new AcisReader();
            using (MemoryStream ms = new MemoryStream(acisData)) using (BinaryReader br = new BinaryReader(ms))
            { try { if (acisReader.ReadBinary(br)) node.ParsedContent["ACIS"] = acisReader; else node.ParsedContent["ACIS_Error"] = "Parsing failed"; }
                catch (Exception ex) { node.ParsedContent["ACIS_Error"] = $"Exception: {ex.Message}"; } }
            node.CurrentReadOffset = acisDataOffset + acisDataSize;
        }
        private void Read_AcisTextData(SecNode node)  { /* ... similar to binary ... */ }
        private void Read_D5E25341(SecNode node) { node.ReadHeader0(Logger.LogWriter); node.ReadUInt32("PropCount", Logger.LogWriter); node.ReadUInt32("Flags", Logger.LogWriter); }
    }

    public class DCReader : SegmentReader
    {
        public DCReader(RSeSegment segment) : base(segment) { }

        public override void ReadSegmentData(byte[] segmentData, StreamWriter logFile)
        {
            // Placeholder implementation
            logFile?.WriteLine($"DCReader.ReadSegmentData called for segment {Segment?.Name}. Data length: {segmentData?.Length ?? -1} - placeholder implementation");
            // Typically, this method would iterate through SecNodes if they exist for this segment type,
            // or parse segmentData directly if it's a known flat structure.
            if (Segment?.Nodes != null && Segment.Nodes.Any())
            {
                foreach (var node in Segment.Nodes)
                {
                    ReadBlock(node); // Uses SegmentReader.ReadBlock
                }
            }
            else if (segmentData != null && segmentData.Length > 0)
            {
                // If there are no pre-scanned nodes, but there is data,
                // this might indicate a segment type that isn't node-based
                // or where node scanning failed/wasn't applicable.
                logFile?.WriteLine($"DCReader.ReadSegmentData: Segment {Segment?.Name} has raw data but no SecNodes. Direct parsing not yet implemented for this reader.");
            }
            else
            {
                logFile?.WriteLine($"DCReader.ReadSegmentData: No data or nodes to process for segment {Segment?.Name}.");
            }
        }

        protected override void PopulateDataReaderMethods()
        {
            _dataReaderMethods["90874D16-11D0-D1F8-0008CABC0663DC09"] = Read_RDxPart;
            _dataReaderMethods["CE52DF3A-11D0-D2D0-0008CCBC0663DC09"] = Read_SketchLine2D;
            _dataReaderMethods["CA7163A3-11D0-D3B2-0008BFBB21EDDC09"] = Read_PMxPartNode;
            _dataReaderMethods["D31891C2-48BF-14C3-AA42-EA872A846B2A"] = Read_UFRxRef;
            _dataReaderMethods["0AA8AF46-E23E-4E82-8E3F-55A03A184489"] = Read_ParameterConstant;
            _dataReaderMethods["F8A77A03-11D1-11D2-910F-0000F8061098"] = Read_ParameterFunction;
            _dataReaderMethods["F8A77A05-11D1-11D2-910F-0000F8061098"] = Read_ParameterRef;
            _dataReaderMethods["RDXPARAMETER_UID_PLACEHOLDER"] = Read_Parameter;
            _dataReaderMethods["CE52DF3E-11D0-D2D0-0008CCBC0663DC09"] = Read_SketchPoint2D;
            _dataReaderMethods["CE52DF3B-11D0-D2D0-0008CCBC0663DC09"] = Read_SketchCircle2D;
            _dataReaderMethods["90874D94-11D0-D1F8-0008CABC0663DC09"] = Read_CoincidentConstraint2D;
            _dataReaderMethods["CE52DF3E-3DPT-4E82-8E3F-55A03A184489"] = Read_SketchPoint3D;
            _dataReaderMethods["8EF06C89-11D1-11D2-910F-0000F8061098"] = Read_SketchLine3D;
            _dataReaderMethods["9E43716A-11D2-0FA5-6000-84B7B035C3B0"] = Read_SketchCircle3D;
            _dataReaderMethods["90874D91-11D0-D1F8-0008CABC0663DC09"] = Read_Feature;
            _dataReaderMethods["729ABE28-11D1-11D2-910F-0000F8061098"] = Read_PartFeatureOperationEnum;
            _dataReaderMethods["DC_PART_COMP_DEF_UID_PLACEHOLDER"] = Read_PartComponentDefinition;
            _dataReaderMethods["90874D64-11D0-D1F8-0008CABC0663DC09"] = Read_RDxRefPlane;
            _dataReaderMethods["90874D66-11D0-D1F8-0008CABC0663DC09"] = Read_RDxWorkAxis;
            _dataReaderMethods["0E6870AE-11D0-D1F8-0008CABC0663DC09"] = Read_ObjectCollection;
            _dataReaderMethods["4E86F047-11D2-11D2-910F-0000F8061098"] = Read_AssemblyConstraint;
            _dataReaderMethods["90874D11-11D0-D1F8-0008CABC0663DC09"] = Read_PlanarSketch;
            _dataReaderMethods["338634AC-11D0-D1F8-0008CABC0663DC09"] = Read_RDxAssemblyComponentInstance;
            _dataReaderMethods["F3A02CB6-11D2-11D2-910F-0000F8061098"] = Read_RDxAssemblyOccurrence;

            // Placeholders for new methods related to Feature properties
            _dataReaderMethods["DC_PARAM_PROXY_UID"] = Read_RDxParameterProxy; // Generic proxy UID
            _dataReaderMethods["0B85010C-11D0-D1F8-0008CABC0663DC09"] = Read_RDxParameterProxy; // ExtrudeFeatureParameterProxy
            _dataReaderMethods["258EC6E1-11D0-D1F8-0008CABC0663DC09"] = Read_RDxParameterProxy; // RevolveFeatureParameterProxy
            _dataReaderMethods["5CC350B8-11D0-D1F8-0008CABC0663DC09"] = Read_RDxParameterProxy; // GenericFeatureParameterProxy (example)

            _dataReaderMethods["DC_BOOL_PARAM_UID"] = Read_RDxBooleanParameter; // Generic boolean param UID
            _dataReaderMethods["90874D28-11D0-D1F8-0008CABC0663DC09"] = Read_RDxBooleanParameter; // BooleanParameter

            _dataReaderMethods["DC_EXTENT_UID"] = Read_RDxExtentPlaceholder; // Generic extent UID
            _dataReaderMethods["0CA07988-11D0-D1F8-0008CABC0663DC09"] = Read_RDxExtentPlaceholder; // ExtrusionFeatureExtent (example)
        }

        private void ReadHeaderS32ss(SecNode node, string typeName, StreamWriter logFile)
        {
            node.ReadHeader0(logFile); node.ReadLen32Text16($"{typeName}.Name", logFile);
            node.ReadSInt32($"{typeName}.Flags", logFile); node.ReadChildRef($"{typeName}.AttributesRef", logFile);
            node.ReadChildRef($"{typeName}.ParentRef", logFile); node.ParsedContent[$"{typeName}_HeaderS32ss_Processed"] = true;
        }
        private void ReadHeaderCntHdr3S(SecNode node, string typeName, StreamWriter logFile)
        {   node.ReadHeader0(logFile); node.ReadUInt32("Count", logFile); node.ReadChildRef("NextRef", logFile); }
        private void ReadHeaderContent(SecNode node, string typeName, StreamWriter logFile, string nextRefName = "NextContentRef")
        {   node.ReadHeader0(logFile); node.ReadLen32Text16("Name", logFile); node.ReadChildRef("AttributesRef", logFile); node.ReadChildRef(nextRefName, logFile); }

        private void Read_RDxPart(SecNode node) { /* ... */ }
        private void Read_SketchLine2D(SecNode node) { /* ... */ }
        private void Read_PMxPartNode(SecNode node) { /* ... */ }
        private void Read_UFRxRef(SecNode node) { /* ... */ }
        private void Read_ParameterConstant(SecNode node) { /* ... */ }
        private void Read_ParameterFunction(SecNode node) { /* ... */ }
        private void Read_ParameterRef(SecNode node) { /* ... */ }
        private void Read_Parameter(SecNode node) { /* ... */ }
        private void ReadHeaderSketch2DEntity(SecNode node, string s, StreamWriter sw) { /* ... */ }
        private void ReadHeaderConstraint2D(SecNode node, string s, StreamWriter sw) { /* ... */ }
        private void Read_SketchPoint2D(SecNode node) { /* ... */ }
        private void Read_SketchCircle2D(SecNode node) { /* ... */ }
        private void Read_CoincidentConstraint2D(SecNode node) { /* ... */ }
        private void ReadHeaderSketch3DEntity(SecNode node, string s, StreamWriter sw) { /* ... */ }
        private void ReadHeaderFeature(SecNode node, string s, StreamWriter sw) { /* ... */ }
        private void ReadHeaderEnum(SecNode node, string s, StreamWriter sw) { /* ... */ }
        private void Read_SketchPoint3D(SecNode node) { /* ... */ }
        private void Read_SketchLine3D(SecNode node) { /* ... */ }
        private void Read_SketchCircle3D(SecNode node) { /* ... */ }
        private void Read_Feature(SecNode node) { /* ... */ }
        private void Read_PartFeatureOperationEnum(SecNode node) { /* ... */ }
        private void Read_PartComponentDefinition(SecNode node) { /* ... */ }
        private void Read_RDxRefPlane(SecNode node) { /* ... */ }
        private void Read_RDxWorkAxis(SecNode node) { /* ... */ }

        private void Read_ObjectCollection(SecNode node)
        {
            ReadHeaderCntHdr3S(node, "ObjectCollection", Logger.LogWriter);
            node.ReadChildRefList("items", Logger.LogWriter);
        }
        private void Read_AssemblyConstraint(SecNode node)
        {
            ReadHeaderContent(node, "AssemblyConstraint", Logger.LogWriter);
            node.SkipBytes(4, Logger.LogWriter); node.ReadSInt32("s32_0", Logger.LogWriter);
            node.SkipBytes(4, Logger.LogWriter); node.ReadCrossRef("ref_1", Logger.LogWriter);
            node.SkipBytes(4, Logger.LogWriter); node.ReadUInt8("style", Logger.LogWriter);
            node.ReadChildRefList("selection", Logger.LogWriter); node.ReadChildRefList("values", Logger.LogWriter);
        }
        private void Read_PlanarSketch(SecNode node)
        {
            ReadHeaderS32ss(node, "PlanarSketch", Logger.LogWriter);
            node.SkipBytes(4, Logger.LogWriter);
            node.ReadUInt32("numEntitiesHeader", Logger.LogWriter);
            node.ReadChildRefList("entities", Logger.LogWriter);
            node.ReadCrossRef("transformation", Logger.LogWriter);
        }
        private void Read_RDxAssemblyComponentInstance(SecNode node)
        {
            ReadHeaderContent(node, "RDxAssemblyComponentInstance", Logger.LogWriter);
            node.ReadCrossRef("ref_1_Unknown", Logger.LogWriter); node.ReadCrossRef("ref_2_Unknown", Logger.LogWriter);
            node.ReadCrossRef("TransformationRef", Logger.LogWriter); node.ReadCrossRef("PartRef", Logger.LogWriter);
            node.ReadChildRefList("lst0_CrossReferences", Logger.LogWriter);
            node.ReadChildRef("ref_3_ChildRef", Logger.LogWriter);
            if (Segment.Version.IsGreaterOrEqualTo(2017,0,0)) { node.SkipBytes(4, Logger.LogWriter); }
            node.ReadChildRef("ref_4_ChildRef", Logger.LogWriter);
        }
        private void Read_RDxAssemblyOccurrence(SecNode node)
        {
            node.ReadHeader0(Logger.LogWriter);
            node.ReadCrossRef("ref_0_ComponentDefinition", Logger.LogWriter);
            node.ReadTransformation3D("Transformation1", Logger.LogWriter);
            node.ReadTransformation3D("Transformation2", Logger.LogWriter);
            node.ReadBoolean("b0_IsGrounded", Logger.LogWriter);
            if (Segment.Version.IsLowerThan(2017,0,0)) { node.SkipBytes(18 * 4, Logger.LogWriter); }
            node.ReadUInt32("u32_2_Unknown", Logger.LogWriter);
            node.ReadChildRef("ref_3_Attributes", Logger.LogWriter);
            node.ReadUInt32("u32_3_ActiveState", Logger.LogWriter);
            if (Segment.Version.IsLowerThan(2017,0,0)) { node.SkipBytes(1, Logger.LogWriter); }
            if (Segment.Version.IsGreaterOrEqualTo(2013,0,0))
            { if (Segment.Version.IsLowerThan(2017,0,0)) { /* Skip complex maps */ } else { node.ReadChildRef("ref_4_Unknown", Logger.LogWriter); } }
        }

        // New methods for DCReader
        private void Read_RDxParameterProxy(SecNode node)
        {
            Logger.Info($"DCReader: Reading RDxParameterProxy (UID: {node.Uid})");
            // This is a generic handler. Specific proxies might have a few leading bytes/flags
            // before the ChildRef, but the ChildRef is the most crucial part.
            // Example Python proxies often read:
            // - u32 flags
            // - u8 unknown/type
            // - ChildRef "ReferencedParameter"
            // For a generic placeholder, we'll focus on the ChildRef.
            // If specific proxies have a consistent small header, that could be skipped or read here.
            // node.SkipBytes(5, Logger.LogWriter, "Skipping potential proxy header (flags + byte)"); // Example skip
            node.ReadChildRef("ReferencedParameter", Logger.LogWriter);
            node.ParsedContent["Summary"] = $"Parsed RDxParameterProxy, refers to: {node.ParsedContent.GetValueOrDefault("ReferencedParameter", "N/A")}";
        }

        private void Read_RDxBooleanParameter(SecNode node)
        {
            Logger.Info($"DCReader: Reading RDxBooleanParameter (UID: {node.Uid})");
            // Python's Read_90874D28 for BooleanParameter reads:
            // - Header (assuming ReadHeaderParameter or similar if standardized)
            // - Value (Boolean)
            // For this method, we assume any common parameter header is handled elsewhere or by a more generic Read_Parameter.
            // This focuses on reading the boolean value itself.
            bool? value = node.ReadBoolean("Value", Logger.LogWriter); // SecNode.ReadBoolean handles reading various forms of boolean
            node.ParsedContent["Value"] = value;
            node.ParsedContent["Summary"] = $"Parsed RDxBooleanParameter: Value='{value}'";
            // It might also have a name or other common parameter fields if not handled by a generic wrapper.
        }

        private void Read_RDxExtentPlaceholder(SecNode node)
        {
            Logger.Info($"DCReader: Reading RDxExtentPlaceholder (UID: {node.Uid})");
            // Based on Python's Read_0CA07988 (ExtrusionFeatureExtent) which reads:
            // - Header
            // - ExtentType (Enum as CrossRef)
            // - Distance1 (CrossRef to Parameter)
            // - Distance2 (CrossRef to Parameter)
            // - ToGeometry (CrossRef to Face/Plane)
            // This placeholder will read these CrossRefs.
            // A common header for extents, if any, would be handled by a wrapper.
            node.ReadCrossRef("ExtentTypeEnum", Logger.LogWriter);    // Enum defining the type of extent (e.g., Distance, ToNext, FromTo)
            node.ReadCrossRef("DistanceParameter1", Logger.LogWriter); // Ref to a Parameter node for the primary distance
            node.ReadCrossRef("DistanceParameter2", Logger.LogWriter); // Ref to a Parameter node for a secondary distance (e.g., in FromTo)
            node.ReadCrossRef("ToFaceReference", Logger.LogWriter);    // Ref to a geometry (e.g., face, workplane) for "To" extents
            node.ParsedContent["Summary"] = "Parsed RDxExtentPlaceholder (structure based on ExtrusionFeatureExtent)";
        }
    }

    public class DirectoryReader : SegmentReader { public DirectoryReader(RSeSegment s) : base(s) { } protected override void PopulateDataReaderMethods() { } public override void ReadSegmentData(byte[] d, StreamWriter l) { } }
    public class RSeDbReader : SegmentReader { public RSeDbReader(RSeSegment s) : base(s) { } protected override void PopulateDataReaderMethods() { } public override void ReadSegmentData(byte[] d, StreamWriter l) { } }
    public class UFRxDocReader : SegmentReader { public UFRxDocReader(RSeSegment s) : base(s) { } protected override void PopulateDataReaderMethods() { } public override void ReadSegmentData(byte[] d, StreamWriter l) { } }

}
