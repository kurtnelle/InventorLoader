using System;
using System.Collections.Generic;
using System.IO;

namespace InventorLoaderCs
{
    public class InventorReader
    {
        // Predefined list of segment names that might be expected in an Inventor file.
        // This list is based on common segments mentioned in the Python importer.
        private readonly List<string> _primarySegmentNames = new List<string>
        {
            // Process these first for metadata
            "RSeDb",
            "UFRxDoc",

            // Application specific data
            "AmAppSegmentType",
            "PmAppSegmentType",

            // BRep Data (Geometry)
            "AmBREPSegmentType",
            "MbBrepSegmentType",
            "PmBrepSegmentType",

            // Document Context (Features, Sketches, Parameters)
            "PmDcSegmentType",
            "AmDcSegmentType",
            "DlDocDcSegmentType",
            "DxDcSegmentType",

            // Graphics Data
            "PmGRxSegmentType",
            "AmGRxSegmentType",
            "MbGRxSegmentType",

            // Other specific segment types
            "FBAttributeSegment",
            "NotebookSegmentType",
            "PmResultSegmentType",
            "AmRxSegmentType",
            "DlDirectorySegmentType",
            "EeSceneSegmentType",
            "DlSheetDcSegmentType",
            "DlSheetDlSegmentType",
            "DlSheetSmSegmentType"
            // "NameTable" is often part of RSeDb, not a separate top-level segment in the same way.
        };

        private Dictionary<string, byte[]> GetSegmentDataFromOle(string filePath)
        {
            Logger.Info($"InventorReader: Simulating OLE read for {filePath}. Returning hardcoded segment data.");
            // Create dummy byte arrays for segments. In a real scenario, these would be read from the OLE file.
            // The content should be minimal but allow basic parsing by the respective readers.
            var segments = new Dictionary<string, byte[]>();

            // RSeDb: UID (Guid), Schema (int), VersionString1 (len32text16), SegInfoText (len32text16), SegInfoGuid (Guid), NumSegments (uint)
            // then NumSegments * (SegName (len32text16), SegType (len32text16), SegUID (len32text16))
            using (MemoryStream msRSeDb = new MemoryStream())
            using (BinaryWriter bwRSeDb = new BinaryWriter(msRSeDb))
            {
                bwRSeDb.Write(Guid.NewGuid().ToByteArray()); // DatabaseGuid
                bwRSeDb.Write(2700); // SchemaVersion
                WriteString16(bwRSeDb, "27.0.0.2700"); // VersionString1
                WriteString16(bwRSeDb, "Segment Info Text"); // SegInfo.Text
                bwRSeDb.Write(Guid.NewGuid().ToByteArray()); // SegInfo.Guid
                bwRSeDb.Write((uint)2); // NumSegments
                // Segment 1
                WriteString16(bwRSeDb, "AmBREPSegmentType");
                WriteString16(bwRSeDb, "BREP");
                WriteString16(bwRSeDb, Guid.NewGuid().ToString().ToUpper());
                // Segment 2
                WriteString16(bwRSeDb, "PmDcSegmentType");
                WriteString16(bwRSeDb, "DC");
                WriteString16(bwRSeDb, Guid.NewGuid().ToString().ToUpper());
                segments["RSeDb"] = msRSeDb.ToArray();
            }

            // UFRxDoc: Schema(int), Magic1(int), arr1(8b), Magic2(int), arr2(4b), VerStr(len32text16), FileName(len32text16), SrcFileName(len32text16), Date(8b), DocGuid(16b), VerGuid(16b)
            using (MemoryStream msUFRx = new MemoryStream())
            using (BinaryWriter bwUFRx = new BinaryWriter(msUFRx))
            {
                bwUFRx.Write(1); // Schema
                bwUFRx.Write(0x09072000); // Magic1
                bwUFRx.Write(new byte[8]); // arr1
                bwUFRx.Write(0x00000100); // Magic2
                bwUFRx.Write(new byte[4]); // arr2
                WriteString16(bwUFRx, "2023.0.0.12345"); // VersionString
                WriteString16(bwUFRx, "TestFile.ipt");   // FileName
                WriteString16(bwUFRx, "OriginalFile.ipt"); // SourceFileName
                bwUFRx.Write(DateTime.UtcNow.ToFileTimeUtc()); // CreationDate
                bwUFRx.Write(Guid.NewGuid().ToByteArray()); // DocGuid
                bwUFRx.Write(Guid.NewGuid().ToByteArray()); // VersionGuid
                segments["UFRxDoc"] = msUFRx.ToArray();
            }

            segments["AmBREPSegmentType"] = new byte[] { 0x01, 0x02, 0x03, 0x04 }; // Dummy
            segments["PmDcSegmentType"] = new byte[] { 0x05, 0x06, 0x07, 0x08 };   // Dummy
            segments["AmAppSegmentType"] = new byte[] { 0x09, 0x0A, 0x0B, 0x0C };  // Dummy

            return segments;
        }

        private void WriteString16(BinaryWriter bw, string s)
        {
            bw.Write((uint)s.Length);
            bw.Write(Encoding.Unicode.GetBytes(s));
        }

        public Inventor Read(string filePath, StreamWriter logWriter = null)
        {
            // Setup logger
            var originalLogWriter = Logger.LogWriter;
            if (logWriter != null) { Logger.LogWriter = logWriter; }
            else { Logger.LogWriter = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true }; }

            Logger.Info($"InventorReader: Starting to read file: {filePath}");
            Inventor inventorModel = new Inventor();
            VersionInfo fileVersionInfo = new VersionInfo(2023,0,0); // Default, to be overwritten by RSeDb or UFRxDoc

            Dictionary<string, byte[]> segmentDataMap = GetSegmentDataFromOle(filePath);

            // 1. Process RSeDb first to get metadata, especially segment directory and version
            if (segmentDataMap.TryGetValue("RSeDb", out byte[] rseDbBytes))
            {
                Logger.Info("InventorReader: Processing RSeDb segment.");
                RSeSegment rseDbSegment = new RSeSegment("RSeDb", fileVersionInfo); // Initial version
                rseDbSegment.Type = "RSeDb";
                RSeDbReader rseDbReader = new RSeDbReader(rseDbSegment);
                rseDbReader.ReadSegmentData(rseDbBytes, Logger.LogWriter);

                inventorModel.RSeDb = new RSeDatabase(); // Create the main RSeDatabase object
                // Populate inventorModel.RSeDb from rseDbSegment.ParsedContent
                if (rseDbSegment.ParsedContent.TryGetValue("DatabaseGuid", out var dbGuid)) inventorModel.RSeDb.Uid = new Uid(((Guid)dbGuid).ToString());
                if (rseDbSegment.ParsedContent.TryGetValue("SchemaVersion", out var schema)) inventorModel.RSeDb.Schema = (int)schema;
                if (rseDbSegment.ParsedContent.TryGetValue("VersionString1", out var verStr) && verStr is string)
                {
                    // TODO: Parse verStr into a VersionInfo object for inventorModel.RSeDb.Vers1
                    // For now, store string and potentially update fileVersionInfo if more detailed parsing is added
                    inventorModel.RSeDb.Vers1 = ParseVersionString((string)verStr) ?? fileVersionInfo;
                    fileVersionInfo = inventorModel.RSeDb.Vers1; // Update global file version
                    Logger.Info($"InventorReader: Updated fileVersionInfo from RSeDb: {fileVersionInfo.Major}.{fileVersionInfo.Minor}.{fileVersionInfo.Revision}");
                }
                if (rseDbSegment.ParsedContent.TryGetValue("SegmentDirectory", out var segDir) && segDir is List<SegmentEntryInfo> directory)
                {
                    inventorModel.RSeDb.SegInfo.SegmentDirectory = directory;
                }
                inventorModel.Segments["RSeDb"] = rseDbSegment;
            }
            else
            {
                Logger.Warning("InventorReader: RSeDb segment not found. Critical metadata might be missing.");
            }

            // 2. Process UFRxDoc for more metadata
            if (segmentDataMap.TryGetValue("UFRxDoc", out byte[] ufrxDocBytes))
            {
                Logger.Info("InventorReader: Processing UFRxDoc segment.");
                // Use fileVersionInfo obtained from RSeDb, or default if RSeDb was not present/parsed successfully
                RSeSegment ufrxSegment = new RSeSegment("UFRxDoc", fileVersionInfo);
                ufrxSegment.Type = "UFRxDoc";
                UFRxDocReader ufrxDocReader = new UFRxDocReader(ufrxSegment);
                ufrxDocReader.ReadSegmentData(ufrxDocBytes, Logger.LogWriter);

                if (ufrxSegment.ParsedContent.TryGetValue("Header1", out var header1Obj) && header1Obj is UFRxHeader1 header1)
                {
                    inventorModel.UFRxDoc.Header1 = header1;
                    if (header1.ParsedVersion != null) // If UFRxDocReader parses version string
                    {
                        fileVersionInfo = header1.ParsedVersion; // UFRxDoc might also provide/confirm version
                        Logger.Info($"InventorReader: fileVersionInfo updated/confirmed from UFRxDoc: {fileVersionInfo.Major}.{fileVersionInfo.Minor}.{fileVersionInfo.Revision}");
                    }
                }
                inventorModel.Segments["UFRxDoc"] = ufrxSegment;
            }
            else
            {
                Logger.Info("InventorReader: UFRxDoc segment not found.");
            }


            // 3. Process other segments using the obtained fileVersionInfo
            foreach (string segmentName in _primarySegmentNames)
            {
                if (segmentName == "RSeDb" || segmentName == "UFRxDoc") continue; // Already processed

                if (segmentDataMap.TryGetValue(segmentName, out byte[] segmentBytes))
                {
                    Logger.Info($"InventorReader: Processing segment: {segmentName}");
                    RSeSegment rseSegment = new RSeSegment(segmentName, fileVersionInfo); // Use updated version
                    rseSegment.Type = GetSegmentTypeFromName(segmentName);

                    SegmentReader segmentReader = null;
                    switch (rseSegment.Type)
                    {
                        // Cases from previous implementation
                        case "AmAppSegmentType":
                        case "PmAppSegmentType":
                            segmentReader = new AppReader(rseSegment);
                            break;

                        case "AmBREPSegmentType":
                        case "MbBrepSegmentType":
                        case "PmBrepSegmentType":
                            segmentReader = new BRepReader(rseSegment);
                            break;

                        case "PmDcSegmentType":
                        case "AmDcSegmentType":
                        case "DlDocDcSegmentType":
                        case "DxDcSegmentType":
                            segmentReader = new DCReader(rseSegment);
                            break;

                        case "FBAttributeSegment":
                            segmentReader = new FBAttributeReader(rseSegment);
                            break;
                        case "NotebookSegmentType":
                            segmentReader = new NotebookReader(rseSegment);
                            break;
                        case "PmResultSegmentType":
                        case "AmRxSegmentType":
                            segmentReader = new ResultReader(rseSegment);
                            break;
                        case "DlDirectorySegmentType":
                            segmentReader = new DirectoryReader(rseSegment);
                            break;
                        case "EeSceneSegmentType":
                            segmentReader = new EeSceneReader(rseSegment);
                            break;
                        case "PmGRxSegmentType":
                        case "AmGRxSegmentType":
                        case "MbGRxSegmentType":
                            segmentReader = new GraphicsReader(rseSegment);
                            break;
                        case "DlSheetDcSegmentType":
                        case "DlSheetDlSegmentType":
                        case "DlSheetSmSegmentType":
                            if (segmentName.Contains("Sm"))
                                segmentReader = new SheetSmReader(rseSegment);
                            else
                                segmentReader = new SheetDlReader(rseSegment);
                            break;
                        // NameTable is usually part of RSeDb, not a separate top-level segment.
                        // If it can be, a case for "NameTableSegmentUID_or_Type" would go here.
                        default:
                            Logger.Warning($"InventorReader: No specific reader configured for segment: {segmentName} (Type: {rseSegment.Type}).");
                            break;
                    }

                    if (segmentReader != null)
                    {
                        try
                        {
                            segmentReader.ReadSegmentData(segmentBytes, Logger.LogWriter);
                            inventorModel.Segments[segmentName] = rseSegment;
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"InventorReader: Error reading segment {segmentName}: {ex.Message}\n{ex.StackTrace}");
                        }
                    }
                }
                else
                {
                    Logger.Info($"InventorReader: Segment {segmentName} not found in preloaded data.");
                }
            }

            Logger.Info("InventorReader: Finished processing all specified segments.");
            Logger.LogWriter = originalLogWriter;
            return inventorModel;
        }

        private VersionInfo ParseVersionString(string versionString)
        {
            if (string.IsNullOrWhiteSpace(versionString)) return null;
            var parts = versionString.Split('.');
            if (parts.Length >= 2)
            {
                if (int.TryParse(parts[0], out int major) && int.TryParse(parts[1], out int minor))
                {
                    int revision = 0;
                    if (parts.Length >= 4 && int.TryParse(parts[3], out int revBuild)) // Often format is M.m.b.r (e.g. 15.0.211200.0)
                    {
                        revision = revBuild; // Or parts[2] if that's the revision
                    }
                    else if (parts.Length >= 3 && int.TryParse(parts[2], out revBuild))
                    {
                         revision = revBuild;
                    }
                    return new VersionInfo(major, minor, revision);
                }
            }
            Logger.Warning($"Could not parse version string: {versionString}");
            return null;
        }

        private string GetSegmentTypeFromName(string name)
        {
            if (name.Contains("AppSegment")) return "AmAppSegmentType"; // Generalize to AppSegment
            if (name.Contains("BREPSegment") || name.Contains("BrepSegment")) return "AmBREPSegmentType"; // Generalize
            if (name.Contains("DcSegment")) return "PmDcSegmentType"; // Generalize
            if (name.Contains("GRxSegment")) return "PmGRxSegmentType"; // Generalize
            if (name.Contains("RxSegment")) return "PmResultSegmentType"; // Generalize
            if (name.Contains("Sheet")) {
                 if (name.Contains("Sm")) return "DlSheetSmSegmentType";
                 return "DlSheetDlSegmentType"; // Generalize other sheets
            }
            // Direct matches or specific logic
            if (name == "RSeDb") return "RSeDb";
            if (name == "UFRxDoc") return "UFRxDoc";
            if (name == "FBAttributeSegment") return "FBAttributeSegment";
            if (name == "NotebookSegmentType") return "NotebookSegmentType";
            if (name == "DlDirectorySegmentType") return "DlDirectorySegmentType";
            if (name == "EeSceneSegmentType") return "EeSceneSegmentType";

            // Fallback or more sophisticated mapping needed if names are not this direct.
            // For now, many map directly to their Python class name pattern.
            return name;
        }
    }
}
