using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace InventorLoaderCs
{
    public class AcisReader
    {
        public AcisHeader Header { get; private set; }
        public List<AcisRecord> RecordsList { get; private set; }
        private Dictionary<int, AcisChunkEntityRef> _refChunks; // To resolve references after all records are read

        public AcisReader()
        {
            Header = new AcisHeader();
            RecordsList = new List<AcisRecord>();
            _refChunks = new Dictionary<int, AcisChunkEntityRef>();
        }

        private void AddRecord(AcisRecord record)
        {
            if (record.Index < 0)
            {
                RecordsList.Add(record); // For special records like End-of-ACIS-data
                return;
            }
            while (RecordsList.Count <= record.Index)
            {
                RecordsList.Add(null);
            }
            RecordsList[record.Index] = record;
        }

        public bool ReadText(StreamReader reader)
        {
            AcisGlobalUtils.SetReader(this);
            _ReadHeaderText(reader);

            int nextRecordIndex = 0; // Used for records without an explicit index
            RecordsList.Clear(); // Clear previous data if any
            _refChunks.Clear();

            while (!reader.EndOfStream)
            {
                string linePeek = reader.ReadLine(); // Peek or read carefully
                if (string.IsNullOrWhiteSpace(linePeek)) continue; // Skip empty lines
                reader.BaseStream.Position -= Encoding.UTF8.GetByteCount(linePeek + Environment.NewLine); // Rewind

                AcisRecord record = _ReadRecordTextInternal(reader, ref nextRecordIndex);
                if (record != null)
                {
                    AddRecord(record);
                    if (record.Name == "End-of-ACIS-data") break;
                }
                else if (!reader.EndOfStream)
                {
                    Logger.Warning("AcisReader: Failed to read a text record or encountered unexpected format.");
                    break;
                }
            }
            _ResolveChunkReferences();
            // After all records are read and references resolved, create entities
            foreach (var record in RecordsList)
            {
                if (record != null && record.Entity == null) // Avoid processing special records or already processed ones
                {
                    AcisGlobalUtils.CreateEntity(record);
                }
            }
            return true;
        }

        private void _ReadHeaderText(StreamReader reader)
        {
            string line = reader.ReadLine()?.Trim();
            if (line == null) throw new FileLoadException("ACIS file is empty or header is missing.");

            string[] tokens = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            Header.Version = AcisUtils.IntToVersion(int.Parse(tokens[0]));
            Header.Records = int.Parse(tokens[1]);
            Header.Bodies = int.Parse(tokens[2]);
            // tokens[3] is flags

            if (Header.Version >= 2.0)
            {
                line = reader.ReadLine()?.Trim();
                if (line == null) throw new FileLoadException("ACIS file header is incomplete (product info missing).");

                Header.ProdId = _ReadLengthPrefixedStringFromLine(reader, ref line);
                Header.ProdVer = _ReadLengthPrefixedStringFromLine(reader, ref line);
                Header.Date = _ReadLengthPrefixedStringFromLine(reader, ref line);

                line = reader.ReadLine()?.Trim();
                if (line == null) throw new FileLoadException("ACIS file header is incomplete (scale/res info missing).");
                tokens = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                Header.Scale = double.Parse(tokens[0], CultureInfo.InvariantCulture);
                Header.ResAbs = double.Parse(tokens[1], CultureInfo.InvariantCulture);
                Header.ResNor = double.Parse(tokens[2], CultureInfo.InvariantCulture);
            }
            Logger.Info($"ACIS Text Header: Version={Header.Version}, Records={Header.Records}, ProdID='{Header.ProdId}'");
        }

        private string _ReadLengthPrefixedStringFromLine(StreamReader reader, ref string currentLine)
        {
            currentLine = currentLine.TrimStart();
            int firstSpace = currentLine.IndexOf(' ');
            if (firstSpace == -1) throw new FileLoadException("Invalid length-prefixed string format in header.");
            int length = int.Parse(currentLine.Substring(0, firstSpace));

            string textValue = "";
            // Check if the string is on the current line or continues to the next
            if (firstSpace + 1 + length <= currentLine.Length)
            {
                textValue = currentLine.Substring(firstSpace + 1, length);
                currentLine = currentLine.Substring(firstSpace + 1 + length);
            }
            else // Should not happen with how ACIS SAT files are typically formatted for header
            {
                 Logger.Error("Error reading length-prefixed string in ACIS Header, unexpected line break or format.");
                 textValue = currentLine.Substring(firstSpace + 1);
                 currentLine = ""; // Consumed whole line
            }
            return textValue;
        }

        private AcisRecord _ReadRecordTextInternal(StreamReader reader, ref int nextRecordIndex)
        {
            string currentToken = _ReadNextToken(reader);
            if (currentToken == null) return null;

            int recordIndex = -1;
            string recordName;

            if (currentToken.StartsWith("-"))
            {
                if (!int.TryParse(currentToken.Substring(1), out recordIndex))
                {
                     Logger.Error($"AcisReader: Could not parse record index from token: {currentToken}");
                     return null;
                }
                recordName = _ReadNextToken(reader);
                if (recordName == null) return null;
            }
            else
            {
                recordName = currentToken;
                recordIndex = nextRecordIndex;
            }
            nextRecordIndex = Math.Max(nextRecordIndex, recordIndex + 1);

            AcisRecord record = new AcisRecord(recordName) { Index = recordIndex };

            while (true)
            {
                AcisChunk chunk = _ReadChunkTextInternal(reader);
                if (chunk == null) {
                    Logger.Warning($"AcisReader: Abrupt end of file or error while reading chunks for record {record.Name} #{record.Index}.");
                    break;
                }
                record.Chunks.Add(chunk);
                if (chunk is AcisChunkTerminator) break;
            }
            return record;
        }

        private string _ReadNextToken(StreamReader reader) // Text ACIS specific token reader
        {
            StringBuilder token = new StringBuilder();
            int cInt;
            bool inString = false;
            int stringLength = 0;
            bool firstCharAfterAt = true;

            while ((cInt = reader.Peek()) != -1)
            {
                char c = (char)cInt;

                if (inString)
                {
                    token.Append((char)reader.Read());
                    if (token.Length == stringLength) break;
                }
                else if (char.IsWhiteSpace(c))
                {
                    reader.Read();
                    if (token.Length > 0) break;
                }
                else if (c == '@')
                {
                    if (token.Length > 0) break;
                    reader.Read(); // Consume '@'
                    // Now read the length
                    StringBuilder lenSb = new StringBuilder();
                    while((cInt = reader.Peek()) != -1 && char.IsDigit((char)cInt))
                    {
                        lenSb.Append((char)reader.Read());
                    }
                    if(!int.TryParse(lenSb.ToString(), out stringLength))
                    {
                        Logger.Error("Could not parse length after @");
                        return null; // Error
                    }
                    // Consume the space after length
                    if(reader.Peek() == ' ') reader.Read();
                    inString = true;
                    // token will now accumulate the string of stringLength
                }
                else if ("#(){}".Contains(c))
                {
                    if (token.Length > 0) break;
                    token.Append((char)reader.Read());
                    break;
                }
                else
                {
                    token.Append((char)reader.Read());
                }
            }
            return token.Length > 0 ? token.ToString() : null;
        }


        private AcisChunk _ReadChunkTextInternal(StreamReader reader)
        {
            string token = _ReadNextToken(reader);
            if (token == null) return null;
            return _TranslateChunkToken(token);
        }

        private AcisChunk _TranslateChunkToken(string token)
        {
            if (token.StartsWith("$"))
            {
                if (int.TryParse(token.Substring(1), out int refId))
                {
                    if (!_refChunks.TryGetValue(refId, out var chunkRef))
                    {
                        chunkRef = new AcisChunkEntityRef(refId);
                        if(refId != -1) _refChunks[refId] = chunkRef;
                    }
                    return chunkRef;
                }
            }
            // Note: String handling with "@len str" is now done by _ReadNextToken
            else if (token == "#") return new AcisChunkTerminator();
            else if (token == "{") return new AcisChunkSubtypeOpen();
            else if (token == "}") return new AcisChunkSubtypeClose();
            else if (token.Equals("0x0A", StringComparison.OrdinalIgnoreCase)) return new AcisChunkEnumValue(AcisConstants.TAG_TRUE, AcisConstants.TAG_TRUE, AcisUtils.BooleanValues);
            else if (token.Equals("0x0B", StringComparison.OrdinalIgnoreCase)) return new AcisChunkEnumValue(AcisConstants.TAG_FALSE, AcisConstants.TAG_FALSE, AcisUtils.BooleanValues);

            if (double.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out double dVal))
                return new AcisChunkDouble(dVal);
            if (long.TryParse(token, out long lVal))
                return new AcisChunkLong(lVal);

            // If it was a string read by @len logic, it's stored in token directly by _ReadNextToken
            // Otherwise, it's an identifier or enum text value
            // Check if it matches known enum text values (e.g. "forward", "reversed" for SENSE)
            // This part might need more context or a global enum string lookup
            if (AcisUtils.TryGetEnumValue(token, out var enumMapping))
            {
                return new AcisChunkEnumValue(AcisConstants.TAG_ENUM_VALUE, enumMapping.Value, enumMapping.PossibleValues);
            }

            return new AcisChunkIdent(token);
        }

        public bool ReadBinary(BinaryReader reader)
        {
            AcisGlobalUtils.SetReader(this);
            if (!_ReadHeaderBinary(reader)) return false;

            int nextRecordIndex = 0;
            RecordsList.Clear();
            _refChunks.Clear();

            try
            {
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    AcisRecord record = _ReadRecordBinaryInternal(reader, ref nextRecordIndex);
                    if (record != null)
                    {
                        AddRecord(record);
                        if (record.Name == "End-of-ACIS-data") break;
                    }
                    else break;
                }
            }
            catch (EndOfStreamException) { /* Expected */ }
            catch (Exception ex)
            {
                Logger.Error($"AcisReader: Error during binary record reading: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
            _ResolveChunkReferences();
            foreach (var record in RecordsList)
            {
                if (record != null && record.Entity == null)
                {
                    AcisGlobalUtils.CreateEntity(record);
                }
            }
            return true;
        }

        private bool _ReadHeaderBinary(BinaryReader reader)
        {
            byte[] formatBytes = reader.ReadBytes(15);
            Header.Format = Encoding.ASCII.GetString(formatBytes).TrimEnd('\0'); // Trim null chars

            if (Header.Format != "ACIS BinaryFile" && !Header.Format.StartsWith("ASM BinaryFile")) {
                Logger.Error($"Invalid ACIS binary file format: {Header.Format}");
                return false;
            }

            bool is64bit = Header.Format.EndsWith("8");
            Func<long> readPossiblyLongIndex = is64bit ? (Func<long>)reader.ReadInt64 : reader.ReadInt32;
            Func<uint> readUInt = is64bit ? (() => (uint)reader.ReadUInt64()) : reader.ReadUInt32;

            Header.Version = AcisUtils.IntToVersion((int)readUInt());
            Header.Records = (int)readUInt(); // Could be long, but typically fits int
            Header.Bodies = (int)readUInt();  // Could be long
            uint flags = readUInt();

            Header.ProdId = _ReadBinaryStringTagged(reader); // Assumes TAG_UTF8_U8
            Header.ProdVer = _ReadBinaryStringTagged(reader);
            Header.Date = _ReadBinaryStringTagged(reader);

            Header.Scale = reader.ReadDouble();
            Header.ResAbs = reader.ReadDouble();
            Header.ResNor = reader.ReadDouble();

            Logger.Info($"ACIS Binary Header: Version={Header.Version}, Records={Header.Records}, ProdID='{Header.ProdId}'");
            return true;
        }

        private string _ReadBinaryStringTagged(BinaryReader reader)
        {
            byte tag = reader.ReadByte(); // Expect TAG_UTF8_U8 or similar
            byte len;
            switch(tag)
            {
                case AcisConstants.TAG_UTF8_U8:  len = reader.ReadByte(); break;
                // Add TAG_UTF8_U16, TAG_UTF8_U32 if needed, reading appropriate length
                default: throw new InvalidDataException($"Expected string tag, got {tag}");
            }
            byte[] bytes = reader.ReadBytes(len);
            return Encoding.UTF8.GetString(bytes);
        }

        private AcisRecord _ReadRecordBinaryInternal(BinaryReader reader, ref int nextRecordIndex)
        {
            byte firstByteOrTag = reader.ReadByte();
            int recordIndex;
            List<string> nameParts = new List<string>();

            if (firstByteOrTag != AcisConstants.TAG_IDENT && firstByteOrTag != AcisConstants.TAG_SUBIDENT && firstByteOrTag != AcisConstants.TAG_TERMINATOR)
            {
                byte[] indexBytes = new byte[4]; // Assuming 32-bit index
                indexBytes[0] = firstByteOrTag;
                for(int k=1; k<4; ++k) indexBytes[k] = reader.ReadByte();
                recordIndex = BitConverter.ToInt32(indexBytes,0);

                byte typeTag = reader.ReadByte();
                nameParts.Add(_ReadChunkBinarySpecific(reader, typeTag, false).Val as string);
            }
            else if (firstByteOrTag == AcisConstants.TAG_TERMINATOR) // EOF marker
            {
                 return new AcisRecord("End-of-ACIS-data") { Index = -2 }; // Special index for EOF
            }
            else // Is TAG_IDENT or TAG_SUBIDENT
            {
                recordIndex = nextRecordIndex;
                nameParts.Add(_ReadChunkBinarySpecific(reader, firstByteOrTag, false).Val as string);
            }
            nextRecordIndex = Math.Max(nextRecordIndex, recordIndex + 1);

            while(reader.BaseStream.Position < reader.BaseStream.Length)
            {
                byte nextTag = reader.ReadByte();
                if (nextTag == AcisConstants.TAG_IDENT)
                {
                     nameParts.Add(_ReadChunkBinarySpecific(reader, nextTag, false).Val as string);
                     break;
                }
                if (nextTag == AcisConstants.TAG_SUBIDENT)
                {
                    nameParts.Add(_ReadChunkBinarySpecific(reader, nextTag, false).Val as string);
                }
                else
                {
                    reader.BaseStream.Position--;
                    break;
                }
            }
            string recordName = string.Join("-", nameParts.Where(s => !string.IsNullOrEmpty(s)));
            if (string.IsNullOrEmpty(recordName))
            {
                 Logger.Error("AcisReader: Could not determine record name in binary stream.");
                 return null;
            }

            AcisRecord record = new AcisRecord(recordName) { Index = recordIndex };

            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                AcisChunk chunk = _ReadChunkBinaryInternal(reader);
                if (chunk == null) {
                     Logger.Warning($"AcisReader: Abrupt end of binary stream or error for record {record.Name} #{record.Index}.");
                     break;
                }
                record.Chunks.Add(chunk);
                if (chunk is AcisChunkTerminator) break;
            }
            return record;
        }

        private AcisChunk _ReadChunkBinaryInternal(BinaryReader reader)
        {
            if (reader.BaseStream.Position >= reader.BaseStream.Length) return null;
            byte tag = reader.ReadByte();
            return _ReadChunkBinarySpecific(reader, tag);
        }

        private AcisChunk _ReadChunkBinarySpecific(BinaryReader reader, byte tag, bool storeRef = true)
        {
             AcisChunk chunk = null;
            if (tag == AcisConstants.TAG_CHAR) { chunk = new AcisChunkChar(); chunk.Val = reader.ReadChar(); }
            else if (tag == AcisConstants.TAG_SHORT) { chunk = new AcisChunkShort(); chunk.Val = reader.ReadInt16(); }
            else if (tag == AcisConstants.TAG_LONG) { chunk = new AcisChunkLong(); chunk.Val = Header.Format.EndsWith("8") ? reader.ReadInt64() : reader.ReadInt32(); }
            else if (tag == AcisConstants.TAG_INT64) { chunk = new AcisChunkHuge(); chunk.Val = reader.ReadInt64(); }
            else if (tag == AcisConstants.TAG_FLOAT) { chunk = new AcisChunkFloat(); chunk.Val = reader.ReadSingle(); }
            else if (tag == AcisConstants.TAG_DOUBLE) { chunk = new AcisChunkDouble(); chunk.Val = reader.ReadDouble(); }
            else if (tag == AcisConstants.TAG_UTF8_U8) { chunk = new AcisChunkUtf8U8(_ReadBinaryStringTagged(reader)); } // String reading needs tag again
            else if (tag == AcisConstants.TAG_UTF8_U16) { var len = reader.ReadUInt16(); chunk = new AcisChunkUtf8U16(Encoding.Unicode.GetString(reader.ReadBytes(len*2)));}
            else if (tag == AcisConstants.TAG_UTF8_U32_A || tag == AcisConstants.TAG_UTF8_U32_B) { var len = reader.ReadUInt32(); chunk = new AcisChunkUtf8U32A(Encoding.UTF8.GetString(reader.ReadBytes((int)len)));}
            else if (tag == AcisConstants.TAG_TRUE) { chunk = new AcisChunkEnumValue(tag, AcisConstants.TAG_TRUE, AcisUtils.BooleanValues); }
            else if (tag == AcisConstants.TAG_FALSE) { chunk = new AcisChunkEnumValue(tag, AcisConstants.TAG_FALSE, AcisUtils.BooleanValues); }
            else if (tag == AcisConstants.TAG_ENTITY_REF) {
                long refIdRaw = Header.Format.EndsWith("8") ? reader.ReadInt64() : reader.ReadInt32();
                int refId = (int)refIdRaw; // Assuming refs fit in int
                if (storeRef && _refChunks.TryGetValue(refId, out var chunkRef)) { chunk = chunkRef; }
                else { chunkRef = new AcisChunkEntityRef(refId); chunk = chunkRef; if(storeRef && refId != -1) _refChunks[refId] = chunkRef; }
            }
            else if (tag == AcisConstants.TAG_IDENT) { chunk = new AcisChunkIdent(_ReadBinaryStringTagged(reader));}
            else if (tag == AcisConstants.TAG_SUBIDENT) { chunk = new AcisChunkSubident(_ReadBinaryStringTagged(reader)); }
            else if (tag == AcisConstants.TAG_SUBTYPE_OPEN) { chunk = new AcisChunkSubtypeOpen(); }
            else if (tag == AcisConstants.TAG_SUBTYPE_CLOSE) { chunk = new AcisChunkSubtypeClose(); }
            else if (tag == AcisConstants.TAG_TERMINATOR) { chunk = new AcisChunkTerminator(); }
            else if (tag == AcisConstants.TAG_POSITION) { chunk = new AcisChunkPosition(new System.Numerics.Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle())); }
            else if (tag == AcisConstants.TAG_VECTOR_3D) { chunk = new AcisChunkVector3D(new System.Numerics.Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle())); }
            else if (tag == AcisConstants.TAG_VECTOR_2D) { chunk = new AcisChunkVector2D(new System.Numerics.Vector2(reader.ReadSingle(), reader.ReadSingle())); }
            else if (tag == AcisConstants.TAG_ENUM_VALUE) { chunk = new AcisChunkEnumValue(); chunk.Val = Header.Format.EndsWith("8") ? (int)reader.ReadInt64() : reader.ReadInt32(); }
            else { Logger.Error($"AcisReader: Unknown binary chunk tag: {tag:X2}"); throw new InvalidDataException($"Unknown binary chunk tag: {tag:X2}"); }
            return chunk;
        }

        private void _ResolveChunkReferences()
        {
            Logger.Info("AcisReader: Resolving chunk references...");
            foreach (var refChunkPair in _refChunks)
            {
                int recordIndex = refChunkPair.Key;
                AcisChunkEntityRef chunkRef = refChunkPair.Value;

                if (recordIndex >= 0 && recordIndex < RecordsList.Count && RecordsList[recordIndex] != null)
                {
                    chunkRef.Record = RecordsList[recordIndex];
                }
                else if (recordIndex != -1)
                {
                    Logger.Warning($"AcisReader: Could not resolve entity reference for index ${recordIndex}. Record not found or list padded with null.");
                }
            }
            Logger.Info("AcisReader: Finished resolving chunk references.");
        }
    }
}
