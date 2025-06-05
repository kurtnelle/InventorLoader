using System;
using System.IO;
using System.Text;

namespace InventorLoaderCs;

/// <summary>
/// Represents an OLE10 native data structure used inside some Inventor files.
/// </summary>
public sealed class OleNative
{
    public uint Size { get; private set; }
    public ushort Header { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public string OrgPath { get; private set; } = string.Empty;
    public uint FormatId { get; private set; }
    public string DataPath { get; private set; } = string.Empty;
    public uint DataLength { get; private set; }
    public byte[] Data { get; private set; } = Array.Empty<byte>();
    public string OrgPathW { get; private set; } = string.Empty;
    public string LabelW { get; private set; } = string.Empty;
    public string DefPathW { get; private set; } = string.Empty;

    public void Read(BinaryReader reader)
    {
        Size = reader.ReadUInt32();
        Header = reader.ReadUInt16();
        Label = reader.ReadCString();
        OrgPath = reader.ReadCString();
        FormatId = reader.ReadUInt32();
        DataPath = reader.ReadLengthPrefixedString8();
        DataLength = reader.ReadUInt32();
        Data = reader.ReadBytes((int)DataLength);

        if (reader.BaseStream.Length - reader.BaseStream.Position > 12)
        {
            OrgPathW = reader.ReadLengthPrefixedString16();
            LabelW = reader.ReadLengthPrefixedString16();
            DefPathW = reader.ReadLengthPrefixedString16();
        }
    }
}
