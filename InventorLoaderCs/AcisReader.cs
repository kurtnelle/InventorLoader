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
        private Dictionary<long, AcisChunkEntityRef> _refChunks; // Changed key to long for 64-bit indices
        private List<AcisEntity> _subtypeEntities; // For entities like CurveInt subtypes
        private bool _isSpaceClaimFormat = false;


        public AcisReader()
        {
            Header = new AcisHeader();
            RecordsList = new List<AcisRecord>();
            _refChunks = new Dictionary<long, AcisChunkEntityRef>();
            _subtypeEntities = new List<AcisEntity>();
        }

        public void AddSubtypeEntity(AcisEntity entity)
        {
            if (entity != null)
            {
                _subtypeEntities.Add(entity);
                // entity.IndexInSubtypeList = _subtypeEntities.Count - 1; // Entity should handle its own index if needed
            }
        }

        public AcisEntity GetSubtypeEntity(int internalRef) // internalRef is 0-based index
        {
            if (internalRef >= 0 && internalRef < _subtypeEntities.Count)
            {
                return _subtypeEntities[internalRef];
            }
            Logger.Warning($"AcisReader: Invalid subtype entity reference: {internalRef}. Max is {_subtypeEntities.Count - 1}");
            return null;
        }

        private void AddRecord(AcisRecord record)
        {
            if (record.Index < 0)
            {
                RecordsList.Add(record);
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

            int nextRecordIndex = 0;
            RecordsList.Clear();
            _refChunks.Clear();
            _subtypeEntities.Clear();


            while (!reader.EndOfStream)
            {
                // Peek a line to check for EOF or only whitespace
                long originalPosition = reader.BaseStream.Position;
                string linePeek = reader.ReadLine();
                if (linePeek == null) break; // True EOF
                if (string.IsNullOrWhiteSpace(linePeek)) continue;

                // Rewind stream to read the line properly with _ReadNextToken
                reader.BaseStream.Position = originalPosition;
                reader.DiscardBufferedData(); // Important after seeking

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
            foreach (var record in RecordsList)
            {
                if (record != null && record.Entity == null && record.Name != "End-of-ACIS-data")
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
            // tokens[3] is flags, not explicitly stored in C# Header currently matching Python version

            if (Header.Version >= 2.0)
            {
                line = reader.ReadLine()?.Trim();
                if (line == null) throw new FileLoadException("ACIS file header is incomplete (product info missing).");

                Header.ProductId = _ReadLengthPrefixedStringFromLine(reader, ref line); // Corrected from ProdId
                Header.ProdVer = _ReadLengthPrefixedStringFromLine(reader, ref line);
                Header.Date = _ReadLengthPrefixedStringFromLine(reader, ref line);

                line = reader.ReadLine()?.Trim();
                if (line == null) throw new FileLoadException("ACIS file header is incomplete (scale/res info missing).");
                tokens = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                Header.Scale = double.Parse(tokens[0], CultureInfo.InvariantCulture);
                Header.ResAbs = double.Parse(tokens[1], CultureInfo.InvariantCulture);
                Header.ResNor = double.Parse(tokens[2], CultureInfo.InvariantCulture);
            }
            Logger.Info($"ACIS Text Header: Version={Header.Version}, Records={Header.Records}, ProdID='{Header.ProductId}'");
        }

        private string _ReadLengthPrefixedStringFromLine(StreamReader reader, ref string currentLine)
        {
            currentLine = currentLine.TrimStart();
            int firstSpace = currentLine.IndexOf(' ');
            if (firstSpace == -1) throw new FileLoadException("Invalid length-prefixed string format in header.");
            if (!int.TryParse(currentLine.Substring(0, firstSpace), out int length))
                throw new FileLoadException($"Invalid length in header string: {currentLine.Substring(0, firstSpace)}");

            string textValue;
            currentLine = currentLine.Substring(firstSpace + 1);
            if (length <= currentLine.Length)
            {
                textValue = currentLine.Substring(0, length);
                currentLine = currentLine.Substring(length);
            }
            else
            {
                 Logger.Error("Error reading length-prefixed string in ACIS Header, string spans multiple lines or format error.");
                 textValue = currentLine; // Take what's left
                 currentLine = "";
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

            AcisRecord record = new AcisRecord(recordName, this) { Index = recordIndex };

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

        private string _ReadNextToken(StreamReader reader)
        {
            StringBuilder token = new StringBuilder();
            int cInt;
            bool inString = false;
            int stringLength = 0;

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
                    reader.Read();
                    StringBuilder lenSb = new StringBuilder();
                    while((cInt = reader.Peek()) != -1 && char.IsDigit((char)cInt))
                    {
                        lenSb.Append((char)reader.Read());
                    }
                    if(!int.TryParse(lenSb.ToString(), out stringLength))
                    {
                        Logger.Error("Could not parse length after @");
                        return null;
                    }
                    if(reader.Peek() != ' ') { Logger.Error("Expected space after @<length>"); return null; }
                    reader.Read(); // Consume space
                    inString = true;
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
            // Check if _ReadNextToken identified an @-string and directly returns content
            // For now, assume if it's not special, it could be string content or ident
            bool wasAtString = token.Length > 0 && _lastTokenWasAtString; // Need a flag from _ReadNextToken
            _lastTokenWasAtString = false; // Reset flag

            return _TranslateChunkToken(token, wasAtString);
        }
        private bool _lastTokenWasAtString = false; // Helper flag for text string parsing

        private AcisChunk _TranslateChunkToken(string token, bool fromAtString = false)
        {
            if (token.StartsWith("$"))
            {
                if (long.TryParse(token.Substring(1), out long refId)) // Use long for refId
                {
                    if (!_refChunks.TryGetValue(refId, out var chunkRef))
                    {
                        chunkRef = new AcisChunkEntityRef(refId);
                        if(refId != -1) _refChunks[refId] = chunkRef;
                    }
                    return chunkRef;
                }
            }
            else if (token == "#") return new AcisChunkTerminator();
            else if (token == "{") return new AcisChunkSubtypeOpen();
            else if (token == "}") return new AcisChunkSubtypeClose();
            // Specific boolean text tokens
            else if (token.Equals("T", StringComparison.OrdinalIgnoreCase)) return new AcisChunkEnumValue(true);
            else if (token.Equals("F", StringComparison.OrdinalIgnoreCase)) return new AcisChunkEnumValue(false);

            if (fromAtString) // If it's from an @-string, it's the string content.
            {
                 return new AcisChunkUtf8U16(token); // Text ACIS strings are often AcisChunkUtf8U16 conceptually
            }
            else // Not an @-string, try other types
            {
                if (double.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out double dVal))
                    return new AcisChunkDouble(dVal);
                if (long.TryParse(token, out long lVal)) // Changed from int.TryParse to long.TryParse for consistency with AcisChunkLong
                    return new AcisChunkLong(lVal);

                // Try to match known enum strings like "forward", "reversed", etc.
                // AcisUtils.TryGetEnumValue returns an AcisChunkEnumValue with the original string if matched.
                if (AcisUtils.TryGetEnumValue(token, out var enumMapping))
                {
                    return enumMapping;
                }
                // Default for unrecognized non-numeric, non-special tokens not from @-strings
                return new AcisChunkIdent(token);
            }
        }

        public bool ReadBinary(BinaryReader reader)
        {
            AcisGlobalUtils.SetReader(this);
            if (!_ReadHeaderBinary(reader)) return false;

            int nextRecordIndex = 0;
            RecordsList.Clear();
            _refChunks.Clear();
            _subtypeEntities.Clear();

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
                if (record != null && record.Entity == null && record.Name != "End-of-ACIS-data")
                {
                    AcisGlobalUtils.CreateEntity(record);
                }
            }
            return true;
        }

        private bool _ReadHeaderBinary(BinaryReader reader)
        {
            byte[] formatBytes = reader.ReadBytes(15);
            Header.Format = Encoding.ASCII.GetString(formatBytes).TrimEnd('\0');

            if (Header.Format != "ACIS BinaryFile" && !Header.Format.StartsWith("ASM BinaryFile")) {
                Logger.Error($"Invalid ACIS binary file format: {Header.Format}");
                return false;
            }

            bool isAsm8 = Header.Format == "ASM BinaryFile8";
            Header.Is64BitRecordIndices = isAsm8;
            Header.Is64BitEnums = isAsm8;
            Header.IsAsmBinaryFile8Format = isAsm8; // Set this new flag

            Func<uint> readUIntForHeader = Header.Is64BitEnums ? (() => (uint)reader.ReadUInt64()) : reader.ReadUInt32;

            Header.Version = AcisUtils.IntToVersion((int)readUIntForHeader());
            Header.Records = (int)readUIntForHeader();
            Header.Bodies = (int)readUIntForHeader();
            uint flags = readUIntForHeader();

            Header.ProductId = (_ReadChunkBinarySpecific(reader, reader.ReadByte(), false) as AcisChunkUtf8U8)?.Val as string ?? "";
            Header.ProdVer = (_ReadChunkBinarySpecific(reader, reader.ReadByte(), false) as AcisChunkUtf8U8)?.Val as string ?? "";
            Header.Date = (_ReadChunkBinarySpecific(reader, reader.ReadByte(), false) as AcisChunkUtf8U8)?.Val as string ?? "";

            if (Header.ProductId == "SpaceClaim")
            {
                this._isSpaceClaimFormat = true;
                Logger.Info("SpaceClaim ACIS format detected.");
                // Consume potential extra SpaceClaim header chunks
                byte spaceClaimTag1 = reader.ReadByte();
                var spaceClaimChunk1 = _ReadChunkBinarySpecific(reader, spaceClaimTag1, false); // Read but don't store ref
                Logger.Info($"SpaceClaim specific header chunk 1: Tag={spaceClaimTag1}, Val={spaceClaimChunk1.Val}");
                if (spaceClaimChunk1.Tag == AcisConstants.TAG_TRUE) // If first was TRUE, read another
                {
                    byte spaceClaimTag2 = reader.ReadByte();
                    var spaceClaimChunk2 = _ReadChunkBinarySpecific(reader, spaceClaimTag2, false);
                    Logger.Info($"SpaceClaim specific header chunk 2: Tag={spaceClaimTag2}, Val={spaceClaimChunk2.Val}");
                }
            }

            Header.Scale = reader.ReadDouble();
            Header.ResAbs = reader.ReadDouble();
            Header.ResNor = reader.ReadDouble();

            Logger.Info($"ACIS Binary Header: Version={Header.Version}, Records={Header.Records}, ProdID='{Header.ProductId}', 64bitIdx={Header.Is64BitRecordIndices}, 64bitEnum={Header.Is64BitEnums}");
            return true;
        }

        private AcisRecord _ReadRecordBinaryInternal(BinaryReader reader, ref int nextRecordIndex)
        {
            byte firstByteOrTag = reader.ReadByte();
            long recordIndexL; // Use long for 64-bit indices
            List<string> nameParts = new List<string>();

            if (firstByteOrTag != AcisConstants.TAG_IDENT && firstByteOrTag != AcisConstants.TAG_SUBIDENT && firstByteOrTag != AcisConstants.TAG_TERMINATOR)
            {
                if (Header.Is64BitRecordIndices)
                {
                    byte[] indexBytes = new byte[8];
                    indexBytes[0] = firstByteOrTag;
                    for(int k=1; k<8; ++k) indexBytes[k] = reader.ReadByte();
                    recordIndexL = BitConverter.ToInt64(indexBytes,0);
                }
                else
                {
                    byte[] indexBytes = new byte[4];
                    indexBytes[0] = firstByteOrTag;
                    for(int k=1; k<4; ++k) indexBytes[k] = reader.ReadByte();
                    recordIndexL = BitConverter.ToInt32(indexBytes,0);
                }

                byte typeTag = reader.ReadByte();
                nameParts.Add(_ReadChunkBinarySpecific(reader, typeTag, false).Val as string);
            }
            else if (firstByteOrTag == AcisConstants.TAG_TERMINATOR)
            {
                 return new AcisRecord("End-of-ACIS-data", this) { Index = -2 };
            }
            else
            {
                recordIndexL = nextRecordIndex;
                nameParts.Add(_ReadChunkBinarySpecific(reader, firstByteOrTag, false).Val as string);
            }
            nextRecordIndex = Math.Max(nextRecordIndex, (int)recordIndexL + 1);

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

            AcisRecord record = new AcisRecord(recordName, this) { Index = (int)recordIndexL };
            if (record.Name == "End-of-ACIS-data") return record;

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
            switch(tag)
            {
                case AcisConstants.TAG_CHAR: chunk = new AcisChunkChar((char)reader.ReadByte()); break;
                case AcisConstants.TAG_SHORT: chunk = new AcisChunkShort(reader.ReadInt16()); break;
                case AcisConstants.TAG_LONG:
                    chunk = new AcisChunkLong(Header.IsAsmBinaryFile8Format ? reader.ReadInt64() : reader.ReadInt32());
                    break;
                case AcisConstants.TAG_INT64: chunk = new AcisChunkHuge(reader.ReadInt64()); break;
                case AcisConstants.TAG_FLOAT: chunk = new AcisChunkFloat(reader.ReadSingle()); break;
                case AcisConstants.TAG_DOUBLE: chunk = new AcisChunkDouble(reader.ReadDouble()); break;

                case AcisConstants.TAG_UTF8_U8:
                    chunk = new AcisChunkUtf8U8(AcisGlobalUtils.ReadStringWithByteLengthPrefix(reader, _isSpaceClaimFormat ? Encoding.GetEncoding("windows-1252") : Encoding.UTF8));
                    break;
                case AcisConstants.TAG_UTF8_U16:
                    // For TAG_UTF8_U16, ACIS usually means char count and UTF-16 encoding.
                    // SpaceClaim's use of windows-1252 here would be unusual but we follow the general flag.
                    // However, if it's truly UTF-16, windows-1252 would be wrong.
                    // Python code Acis.py uses UTF16 for TAG_UTF8_U16 regardless of SpaceClaim.
                    chunk = new AcisChunkUtf8U16(AcisGlobalUtils.ReadStringWithUInt16LengthPrefix(reader, Encoding.Unicode, true));
                    break;
                case AcisConstants.TAG_UTF8_U32_A:
                case AcisConstants.TAG_UTF8_U32_B:
                    // Similar to UTF16, typically means char count and UTF-32.
                    // Python code Acis.py uses UTF32 for these.
                    chunk = new AcisChunkUtf8U32A(AcisGlobalUtils.ReadStringWithUInt32LengthPrefix(reader, Encoding.UTF32, true));
                    break;

                case AcisConstants.TAG_TRUE: chunk = new AcisChunkEnumValue(true); break;
                case AcisConstants.TAG_FALSE: chunk = new AcisChunkEnumValue(false); break;

                case AcisConstants.TAG_ENTITY_REF:
                    long refId = Header.Is64BitRecordIndices ? reader.ReadInt64() : reader.ReadInt32();
                    if (storeRef && _refChunks.TryGetValue(refId, out var chunkRef)) { chunk = chunkRef; }
                    else { chunkRef = new AcisChunkEntityRef(refId); chunk = chunkRef; if (storeRef && refId != -1L) _refChunks[refId] = chunkRef; }
                    break;

                case AcisConstants.TAG_IDENT:
                    chunk = new AcisChunkIdent(AcisGlobalUtils.ReadStringWithByteLengthPrefix(reader, _isSpaceClaimFormat ? Encoding.GetEncoding("windows-1252") : Encoding.UTF8));
                    break;
                case AcisConstants.TAG_SUBIDENT:
                    chunk = new AcisChunkSubident(AcisGlobalUtils.ReadStringWithByteLengthPrefix(reader, _isSpaceClaimFormat ? Encoding.GetEncoding("windows-1252") : Encoding.UTF8));
                    break;

                case AcisConstants.TAG_SUBTYPE_OPEN: chunk = new AcisChunkSubtypeOpen(); break;
                case AcisConstants.TAG_SUBTYPE_CLOSE: chunk = new AcisChunkSubtypeClose(); break;
                case AcisConstants.TAG_TERMINATOR: chunk = new AcisChunkTerminator(); break;

                case AcisConstants.TAG_POSITION:  // Stored as doubles in ACIS binary spec
                    chunk = new AcisChunkPosition(new Vector3((float)reader.ReadDouble(), (float)reader.ReadDouble(), (float)reader.ReadDouble()));
                    break;
                case AcisConstants.TAG_VECTOR_3D:
                    chunk = new AcisChunkVector3D(new Vector3((float)reader.ReadDouble(), (float)reader.ReadDouble(), (float)reader.ReadDouble()));
                    break;
                case AcisConstants.TAG_VECTOR_2D:
                    chunk = new AcisChunkVector2D(new Vector2((float)reader.ReadDouble(), (float)reader.ReadDouble()));
                    break;

                case AcisConstants.TAG_ENUM_VALUE:
                    chunk = new AcisChunkEnumValue(Header.Is64BitEnums ? reader.ReadInt64() : reader.ReadInt32(), null);
                    break;

                default:
                    Logger.Error($"AcisReader: Unknown binary chunk tag: {tag:X2}");
                    throw new InvalidDataException($"Unknown binary chunk tag: {tag:X2}");
            }
            return chunk;
        }

        private void _ResolveChunkReferences()
        {
            Logger.Info("AcisReader: Resolving chunk references...");
            foreach (var refChunkPair in _refChunks)
            {
                long recordIndexL = refChunkPair.Key;
                AcisChunkEntityRef chunkRef = refChunkPair.Value;
                int recordIndex = (int)recordIndexL; // Assuming indices fit in int for List access

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
