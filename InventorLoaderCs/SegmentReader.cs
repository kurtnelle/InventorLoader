namespace InventorLoaderCs
{
    public abstract class SegmentReader // Made abstract as it seems to be a base class
    {
        // Placeholder properties and methods based on usage in SegmentReaders.cs
        protected RSeSegment Segment { get; private set; }
        protected Dictionary<string, Action<SecNode>> _dataReaderMethods;

        protected SegmentReader(RSeSegment segment)
        {
            Segment = segment;
            _dataReaderMethods = new Dictionary<string, Action<SecNode>>(StringComparer.OrdinalIgnoreCase);
            PopulateDataReaderMethods();
        }

        protected abstract void PopulateDataReaderMethods(); // To be implemented by derived classes
        public abstract void ReadSegmentData(byte[] segmentData, System.IO.StreamWriter logFile); // To be implemented

        // Common method used by derived classes, can be basic here
        protected virtual void ReadBlock(SecNode node)
        {
            if (node == null)
            {
                Logger.Error("SegmentReader.ReadBlock: SecNode is null.");
                return;
            }

            if (_dataReaderMethods.TryGetValue(node.Uid, out var readerMethod))
            {
                try
                {
                    Logger.Info($"SegmentReader: Processing SecNode UID {node.Uid} with {readerMethod.Method.Name}");
                    readerMethod(node);
                }
                catch (Exception ex)
                {
                    Logger.Error($"SegmentReader.ReadBlock: Error processing SecNode UID {node.Uid} with method {readerMethod.Method.Name}: {ex.Message}\n{ex.StackTrace}");
                }
            }
            else
            {
                Logger.Warning($"SegmentReader: No specific reader method found for SecNode UID {node.Uid}. Node data may not be fully parsed.");
                // Basic logging of unhandled node for now
                node.ParsedContent["Summary"] = $"Unhandled SecNode UID: {node.Uid}";
            }
        }
    }
}
