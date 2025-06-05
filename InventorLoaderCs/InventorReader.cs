using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
// Assuming SecNode, RSeSegment, Inventor, etc. are in this namespace or accessible

namespace InventorLoaderCs
{
    public class InventorReader
    {
        private readonly List<string> _primarySegmentNames = new List<string> { "RSeDb", "UFRxDoc" };

        private SegmentReader CreateReaderForSegment(string segmentName, RSeSegment segment)
        {
            string typeKey = GetSegmentTypeFromName(segmentName);

            if (KnownSegmentTypeToReaderMap.TryGetValue(typeKey, out Type readerType))
            {
                try
                {
                    return (SegmentReader)Activator.CreateInstance(readerType, segment);
                }
                catch (Exception ex)
                {
                    Logger.Error($"InventorReader.CreateReaderForSegment: Error creating reader of type {readerType.Name} for segment '{segmentName}'. Exception: {ex.Message}");
                }
            }

            Logger.Warning($"InventorReader.CreateReaderForSegment: No specific reader for segment type key '{typeKey}' (from name '{segmentName}'). Using generic SegmentReader.");
            // return new SegmentReader(segment); // This line would cause an error as SegmentReader is abstract
            // For now, returning null or throwing if a specific reader isn't found and a generic one can't be instantiated.
            // This depends on how SegmentReader placeholder is defined. If it's abstract, this must be handled.
            // Assuming the placeholder SegmentReader might be concrete or this path is for truly unknown types.
            // If SegmentReader is abstract, this line should be:
            // throw new InvalidOperationException($"No reader for {segmentName} and SegmentReader is abstract.");
            // For now, to match the provided file structure which might have had a concrete SegmentReader:
            return null; // Or a more specific fallback or error
        }

        private class ConceptualOleFileWrapper // Made non-static
        {
            private string _filePath;
            private StreamWriter _logFile;
            private List<string> _simulatedStreamNames = new List<string> {
                "RSeDb", "UFRxDoc", "AmBREPSegmentType", "PmDcSegmentType",
                "AmAppSegmentType", "GraphicsSeg", "EeSceneDynSegmentType",
                "UnknownStreamExample123", "EmptyStreamExample"
            };
            private Dictionary<string, byte[]> _simulatedStreamData = new Dictionary<string, byte[]>();

            // Static property to allow tests to override specific stream data
            public static byte[] OverrideAmBRepStreamData { get; set; } = null;

            public ConceptualOleFileWrapper(string filePath, StreamWriter logFile)
            {
                _filePath = filePath;
                _logFile = logFile;

                _simulatedStreamData["RSeDb"] = Encoding.UTF8.GetBytes("RSeDb_Simulated_Content_v4:SegmentDirectoryInfo;UFRxDoc;AmBREPSegmentType;PmDcSegmentType;AmAppSegmentType;GraphicsSeg;EeSceneDynSegmentType");
                _simulatedStreamData["UFRxDoc"] = Encoding.UTF8.GetBytes("UFRxDoc_Simulated_Content_v4:Version26.0;FileName=" + Path.GetFileName(filePath));
                // Default AmBREPSegmentType data, can be overridden by OverrideAmBRepStreamData
                _simulatedStreamData["AmBREPSegmentType"] = Encoding.ASCII.GetBytes("26.0.0 1000 ACIS 26.0 NT\n1 1 0\nplaceholder-unit 1\nEnd-of-ACIS-History-Marker-\nbody $ -1 $-1 $-1 $-1 $-1 $-1 $-1 $-1\nend\nEnd-of-ACIS-data\n");
                _simulatedStreamData["PmDcSegmentType"] = new byte[] { 0xDC, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F };
                _simulatedStreamData["AmAppSegmentType"] = new byte[] { 0xAA, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F };
                _simulatedStreamData["GraphicsSeg"] = new byte[] { 0x60, 0x05, 0x10, 0x15, 0x20, 0x25, 0x30, 0x35, 0x40, 0x45, 0x50, 0x55, 0x60, 0x65, 0x70, 0x75 };
                _simulatedStreamData["EeSceneDynSegmentType"] = new byte[] { 0xEE, 0x5C, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17 };
                _simulatedStreamData["EmptyStreamExample"] = new byte[0];
            }

            public bool TryOpen()
            {
                Logger.Info(_logFile, $"ConceptualOleFileWrapper: Simulating opening '{_filePath}'");
                return true;
            }

            public List<string> GetStreamNames()
            {
                Logger.Info(_logFile, "ConceptualOleFileWrapper: Simulating getting stream names.");
                return new List<string>(_simulatedStreamNames);
            }

            public byte[] TryReadStream(string streamName)
            {
                if (streamName == "AmBREPSegmentType" && OverrideAmBRepStreamData != null)
                {
                    Logger.Info(_logFile, $"ConceptualOleFileWrapper: Reading overridden stream '{streamName}', Length: {OverrideAmBRepStreamData.Length}.");
                    return OverrideAmBRepStreamData;
                }

                if (_simulatedStreamData.TryGetValue(streamName, out byte[] data))
                {
                    Logger.Info(_logFile, $"ConceptualOleFileWrapper: Simulating reading stream '{streamName}', Length: {data.Length}.");
                    return data;
                }
                if (_simulatedStreamNames.Contains(streamName)) {
                     Logger.Warning(_logFile, $"ConceptualOleFileWrapper: Stream '{streamName}' is known but has no specific simulated data. Returning empty byte array.");
                     return new byte[0];
                }
                Logger.Warning(_logFile, $"ConceptualOleFileWrapper: Stream '{streamName}' not found in known simulated streams. Returning null.");
                return null;
            }

            public void Close()
            {
                Logger.Info(_logFile, $"ConceptualOleFileWrapper: Simulating closing '{_filePath}'.");
            }
        }

        private static readonly Dictionary<string, Type> KnownSegmentTypeToReaderMap = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            { "RSeDb", typeof(RSeDbReader) },
            { "UFRxDoc", typeof(UFRxDocReader) },
            { "AmBREPSegmentType", typeof(BRepReader) },
            { "DefaultBRep", typeof(BRepReader) },
            { "PmDcSegmentType", typeof(DCReader) },
            { "DesignConstraints", typeof(DCReader) },
            { "AmAppSegmentType", typeof(AppReader) },
            { "ApplicationData", typeof(AppReader) },
            { "GraphicsSeg", typeof(GraphicsReader) },
            { "EeSceneDynSegmentType", typeof(EeSceneReader) }
        };

        private string GetSegmentTypeFromName(string segmentName)
        {
            if (segmentName.Equals("RSeDb", StringComparison.OrdinalIgnoreCase)) return "RSeDb";
            if (segmentName.Equals("UFRxDoc", StringComparison.OrdinalIgnoreCase)) return "UFRxDoc";
            if (segmentName.Equals("AmBREPSegmentType", StringComparison.OrdinalIgnoreCase) || segmentName.Equals("DefaultBRep", StringComparison.OrdinalIgnoreCase)) return "AmBREPSegmentType";
            if (segmentName.Equals("PmDcSegmentType", StringComparison.OrdinalIgnoreCase) || segmentName.Equals("DesignConstraints", StringComparison.OrdinalIgnoreCase)) return "PmDcSegmentType";
            if (segmentName.Equals("AmAppSegmentType", StringComparison.OrdinalIgnoreCase) || segmentName.Equals("ApplicationData", StringComparison.OrdinalIgnoreCase)) return "AmAppSegmentType";
            if (segmentName.Equals("GraphicsSeg", StringComparison.OrdinalIgnoreCase)) return "GraphicsSeg";
            if (segmentName.Equals("EeSceneDynSegmentType", StringComparison.OrdinalIgnoreCase)) return "EeSceneDynSegmentType";
            return segmentName;
        }

        public Inventor ReadFile(string filePath, StreamWriter logFile = null)
        {
            var inventorModel = new Inventor();
            logFile?.WriteLine($"InventorReader: Starting to read file: {filePath}");
            Logger.LogWriter = logFile;

            Dictionary<string, byte[]> segmentDataMap = new Dictionary<string, byte[]>();

            var oleFile = new ConceptualOleFileWrapper(filePath, logFile);
            if (!oleFile.TryOpen())
            {
                Logger.Error(logFile, $"Failed to open (simulated) OLE file: {filePath}");
                return null;
            }

            List<string> segmentsToProcessOrdered = new List<string>();

            try
            {
                List<string> streamNamesInFile = oleFile.GetStreamNames();
                Logger.Info(logFile, $"InventorReader: Streams available in (simulated) OLE: {string.Join(", ", streamNamesInFile)}");

                foreach (string streamName in streamNamesInFile)
                {
                    byte[] data = oleFile.TryReadStream(streamName);
                    if (data != null)
                    {
                        segmentDataMap[streamName] = data; // Store even if empty, to indicate presence
                        Logger.Info(logFile, $"InventorReader: Successfully loaded data for stream: {streamName}, Length: {data.Length}");
                    }
                    else
                    {
                         Logger.Warning(logFile, $"InventorReader: Stream '{streamName}' (reported by GetStreamNames) was not found or failed to read by TryReadStream.");
                    }
                }

                if (segmentDataMap.ContainsKey("RSeDb")) segmentsToProcessOrdered.Add("RSeDb");
                if (segmentDataMap.ContainsKey("UFRxDoc")) segmentsToProcessOrdered.Add("UFRxDoc");

                foreach(var streamName in segmentDataMap.Keys)
                {
                    if (!segmentsToProcessOrdered.Contains(streamName, StringComparer.OrdinalIgnoreCase))
                    {
                        segmentsToProcessOrdered.Add(streamName);
                    }
                }
                Logger.Info(logFile, $"InventorReader: Will attempt to process segments in order: {string.Join(", ", segmentsToProcessOrdered)}");
            }
            finally
            {
                oleFile.Close();
                ConceptualOleFileWrapper.OverrideAmBRepStreamData = null; // Reset override after use
            }

            foreach(string segmentName in segmentsToProcessOrdered)
            {
                if (!segmentDataMap.TryGetValue(segmentName, out byte[] currentSegmentData))
                {
                    Logger.Error(logFile, $"Internal Error: Segment '{segmentName}' was in processing list but not in data map after OLE read phase.");
                    continue;
                }

                if (currentSegmentData.Length == 0 && !segmentName.Equals("EmptyStreamExample", StringComparison.OrdinalIgnoreCase) )
                {
                     Logger.Warning(logFile, $"InventorReader: Segment '{segmentName}' is empty. Skipping processing by its reader unless it's an expected empty stream.");
                     continue;
                }

                var segment = inventorModel.Segments.ContainsKey(segmentName) ? inventorModel.Segments[segmentName] : new RSeSegment(segmentName);
                inventorModel.Segments[segmentName] = segment;

                if (segmentName.Equals("RSeDb", StringComparison.OrdinalIgnoreCase) && inventorModel.RSeDb != null)
                {
                     inventorModel.RSeDb.SegInfo = segment.SegInfo;
                }
                // TODO: Populate segment.Version from UFRxDoc after UFRxDoc is processed.
                // This requires UFRxDoc to be processed early if its version info is needed by other readers.

                SegmentReader reader = CreateReaderForSegment(segmentName, segment);
                logFile?.WriteLine($"InventorReader: Processing segment '{segmentName}' with reader '{reader.GetType().Name}'.");
                PreScanNodes(segment, currentSegmentData, logFile);
                reader.ReadSegmentData(currentSegmentData, logFile);
            }

            if (inventorModel.Segments.TryGetValue("AmAppSegmentType", out RSeSegment appSegment))
            {
                if (appSegment.ParsedContent.TryGetValue("iPropertiesDictionary", out object props) &&
                    props is Dictionary<string, Dictionary<object, Tuple<string, object>>> typedProps)
                {
                    inventorModel.iProperties = typedProps;
                    logFile?.WriteLine("InventorReader: Successfully transferred iProperties from AmAppSegmentType to Inventor model.");
                }
                else
                {
                    logFile?.WriteLine("InventorReader: iPropertiesData ('iPropertiesDictionary') not found or of incorrect type in AmAppSegmentType's ParsedContent.");
                }
            }

            logFile?.WriteLine("InventorReader: Finished reading file.");
            return inventorModel;
        }

        private void PreScanNodes(RSeSegment segment, byte[] segmentData, StreamWriter logFile)
        {
            logFile?.WriteLine($"InventorReader.PreScanNodes: Placeholder for segment '{segment.Name}'. Actual node scanning depends on segment type structure.");
        }
    }
}
