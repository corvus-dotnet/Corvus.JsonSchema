using System.Buffers;
using System.Buffers.Text;
using Corvus.Text;

#if STJ && TOON
using System.Text.Json;

namespace Corvus.Toon.Internal;
#else
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Toon.Internal;
#endif

internal static class JsonToToonConverter
{
    public static void Convert(ref Utf8ToonWriter writer, ReadOnlySpan<byte> utf8Json)
    {
        Convert(ref writer, utf8Json, suppressKeyFolding: false);
    }

    private static void Convert(ref Utf8ToonWriter writer, ReadOnlySpan<byte> utf8Json, bool suppressKeyFolding)
    {
        if (!suppressKeyFolding &&
            writer.Options.KeyFolding != ToonKeyFolding.Off &&
            writer.Options.FlattenDepth > 0)
        {
            ConvertWithKeyFolding(ref writer, utf8Json);
            return;
        }

        Utf8JsonReader reader = new(utf8Json);
        if (!reader.Read())
        {
            writer.WriteRaw("null"u8);
            return;
        }

        WriteReaderValue(ref writer, ref reader, 0);
    }

    private static void ConvertWithKeyFolding(ref Utf8ToonWriter writer, ReadOnlySpan<byte> utf8Json)
    {
        Utf8JsonReader reader = new(utf8Json);
        if (!reader.Read())
        {
            writer.WriteRaw("null"u8);
            return;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            WriteReaderValue(ref writer, ref reader, 0);
            return;
        }

        Span<int> rootKeyBuckets = stackalloc int[Utf8KeyHashSet.StackAllocBucketSize];
        Span<byte> rootKeyEntries = stackalloc byte[Utf8KeyHashSet.StackAllocEntrySize];
        Span<byte> rootKeyBuffer = stackalloc byte[Utf8KeyHashSet.StackAllocKeyBufferSize];
        Utf8KeyHashSet rootKeys = new(16, rootKeyBuckets, rootKeyEntries, rootKeyBuffer);
        try
        {
            Utf8JsonReader probe = reader;
            if (!probe.Read())
            {
                ThrowHelper.ThrowToonException(SR.UnexpectedEndOfJsonObject);
            }

            Span<byte> decodeBuffer = stackalloc byte[JsonConstants.StackallocByteThreshold];
            while (probe.TokenType == JsonTokenType.PropertyName)
            {
                if (!probe.ValueIsEscaped)
                {
                    rootKeys.AddIfNotExists(probe.ValueSpan);
                }
                else
                {
                    byte[]? rentedArray = null;
                    int maxLen = probe.ValueSpan.Length;
                    Span<byte> buffer = maxLen <= JsonConstants.StackallocByteThreshold
                        ? decodeBuffer
                        : (rentedArray = ArrayPool<byte>.Shared.Rent(maxLen));

                    try
                    {
                        int written = probe.CopyString(buffer);
                        rootKeys.AddIfNotExists(buffer.Slice(0, written));
                    }
                    finally
                    {
                        if (rentedArray is not null)
                        {
                            ArrayPool<byte>.Shared.Return(rentedArray);
                        }
                    }
                }

                if (!probe.TrySkip())
                {
                    ThrowHelper.ThrowToonException(SR.UnexpectedEndOfJsonObject);
                }

                if (!probe.Read())
                {
                    ThrowHelper.ThrowToonException(SR.UnexpectedEndOfJsonObject);
                }
            }

            if (!reader.Read() || reader.TokenType == JsonTokenType.EndObject)
            {
                return;
            }

            bool first = true;
            Span<byte> initialPath = stackalloc byte[JsonConstants.StackallocByteThreshold];
            Span<byte> rootNameBuffer = stackalloc byte[JsonConstants.StackallocByteThreshold];
            Span<byte> childNameBuffer = stackalloc byte[JsonConstants.StackallocByteThreshold];
            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                if (!first)
                {
                    writer.WriteNewLine();
                }

                first = false;
                Utf8ValueStringBuilder pathBuilder = new(initialPath);
                try
                {
                    int segmentCount = 0;
                    bool canFold = true;
                    byte[]? rentedRootName = null;
                    scoped ReadOnlySpan<byte> rootName = reader.ValueSpan;
                    try
                    {
                        if (reader.ValueIsEscaped)
                        {
                            int maxLen = reader.ValueSpan.Length;
                            Span<byte> buffer = maxLen <= JsonConstants.StackallocByteThreshold
                                ? rootNameBuffer
                                : (rentedRootName = ArrayPool<byte>.Shared.Rent(maxLen));

                            int written = reader.CopyString(buffer);
                            rootName = buffer.Slice(0, written);
                        }

                        if (IsBareKeySegment(rootName))
                        {
                            pathBuilder.Append(rootName);
                            segmentCount = 1;
                        }
                        else
                        {
                            canFold = false;
                        }
                    }
                    finally
                    {
                        if (rentedRootName is not null)
                        {
                            ArrayPool<byte>.Shared.Return(rentedRootName);
                        }
                    }

                    if (canFold)
                    {
                        Utf8JsonReader valueReader = reader;
                        if (!valueReader.Read())
                        {
                            ThrowHelper.ThrowToonException(SR.UnexpectedEndOfJsonObject);
                        }

                        while (segmentCount < writer.Options.FlattenDepth && valueReader.TokenType == JsonTokenType.StartObject)
                        {
                            Utf8JsonReader propertyProbe = valueReader;
                            if (!propertyProbe.Read() || propertyProbe.TokenType != JsonTokenType.PropertyName)
                            {
                                break;
                            }

                            Utf8JsonReader endProbe = propertyProbe;
                            if (!endProbe.TrySkip())
                            {
                                ThrowHelper.ThrowToonException(SR.UnexpectedEndOfJsonObject);
                            }

                            if (!endProbe.Read() || endProbe.TokenType != JsonTokenType.EndObject)
                            {
                                break;
                            }

                            int previousLength = pathBuilder.Length;
                            pathBuilder.Append((byte)'.');
                            byte[]? rentedChildName = null;
                            scoped ReadOnlySpan<byte> childName = propertyProbe.ValueSpan;
                            try
                            {
                                if (propertyProbe.ValueIsEscaped)
                                {
                                    int maxLen = propertyProbe.ValueSpan.Length;
                                    Span<byte> buffer = maxLen <= JsonConstants.StackallocByteThreshold
                                        ? childNameBuffer
                                        : (rentedChildName = ArrayPool<byte>.Shared.Rent(maxLen));

                                    int written = propertyProbe.CopyString(buffer);
                                    childName = buffer.Slice(0, written);
                                }

                                if (!IsBareKeySegment(childName))
                                {
                                    pathBuilder.Length = previousLength;
                                    break;
                                }

                                pathBuilder.Append(childName);
                            }
                            finally
                            {
                                if (rentedChildName is not null)
                                {
                                    ArrayPool<byte>.Shared.Return(rentedChildName);
                                }
                            }

                            if (rootKeys.Contains(pathBuilder.AsSpan()))
                            {
                                canFold = false;
                                break;
                            }

                            segmentCount++;
                            if (!propertyProbe.Read())
                            {
                                ThrowHelper.ThrowToonException(SR.UnexpectedEndOfJsonObject);
                            }

                            valueReader = propertyProbe;
                        }
                    }

                    if (!canFold)
                    {
                        WriteRegularRootProperty(ref writer, ref reader);
                    }
                    else
                    {
                        AdvanceToFoldedValue(ref reader, segmentCount);
                        WriteReaderProperty(ref writer, pathBuilder.AsSpan(), ref reader, 0);

                        for (int i = 1; i < segmentCount; i++)
                        {
                            if (!reader.Read() || reader.TokenType != JsonTokenType.EndObject)
                            {
                                ThrowHelper.ThrowToonException(SR.UnexpectedEndOfJsonObject);
                            }
                        }
                    }
                }
                finally
                {
                    pathBuilder.Dispose();
                }

                if (!reader.Read())
                {
                    break;
                }
            }
        }
        finally
        {
            rootKeys.Dispose();
        }
    }

    private static void AdvanceToFoldedValue(scoped ref Utf8JsonReader reader, int segmentCount)
    {
        for (int i = 0; i < segmentCount; i++)
        {
            if (!reader.Read())
            {
                ThrowHelper.ThrowToonException(SR.UnexpectedEndOfJsonObject);
            }

            if (i < segmentCount - 1)
            {
                if (reader.TokenType != JsonTokenType.StartObject || !reader.Read() || reader.TokenType != JsonTokenType.PropertyName)
                {
                    ThrowHelper.ThrowToonException(SR.UnexpectedEndOfJsonObject);
                }
            }
        }
    }

    private static bool IsBareKeySegment(scoped ReadOnlySpan<byte> value)
    {
        if (value.Length == 0 || !IsIdentifierStart(value[0]))
        {
            return false;
        }

        for (int i = 1; i < value.Length; i++)
        {
            if (!IsIdentifierPart(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static int FindFieldIndex(ReadOnlySpan<byte> fieldBuffer, ReadOnlySpan<CellRange> fields, ReadOnlySpan<byte> field)
    {
        for (int i = 0; i < fields.Length; i++)
        {
            CellRange existing = fields[i];
            if (field.SequenceEqual(fieldBuffer.Slice(existing.Offset, existing.Length)))
            {
                return i;
            }
        }

        return -1;
    }

    private static void WriteReaderValue(ref Utf8ToonWriter writer, scoped ref Utf8JsonReader reader, int indent)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.StartObject:
                WriteReaderObject(ref writer, ref reader, indent);
                break;

            case JsonTokenType.StartArray:
                WriteReaderArray(ref writer, ref reader, indent);
                break;

            case JsonTokenType.String:
                WriteReaderString(ref writer, ref reader, isKey: false);
                break;

            case JsonTokenType.Number:
                writer.WriteNumberValue(reader.ValueSpan);
                break;

            case JsonTokenType.True:
                writer.WriteRaw("true"u8);
                break;

            case JsonTokenType.False:
                writer.WriteRaw("false"u8);
                break;

            case JsonTokenType.Null:
            default:
                writer.WriteRaw("null"u8);
                break;
        }
    }

    private static void WriteReaderObject(ref Utf8ToonWriter writer, scoped ref Utf8JsonReader reader, int indent)
    {
        if (!reader.Read() || reader.TokenType == JsonTokenType.EndObject)
        {
            return;
        }

        bool first = true;
        while (reader.TokenType == JsonTokenType.PropertyName)
        {
            Utf8JsonReader keyReader = reader;
            if (!first)
            {
                writer.WriteNewLine();
            }

            first = false;

            if (!reader.Read())
            {
                ThrowHelper.ThrowToonException(SR.UnexpectedEndOfJsonObject);
            }

            if (reader.TokenType == JsonTokenType.StartObject && TryWriteEmptyObjectProperty(ref writer, ref reader, ref keyReader, indent, writeIndent: true))
            {
                if (!reader.Read())
                {
                    break;
                }

                continue;
            }

            if (reader.TokenType == JsonTokenType.StartArray && TryWriteArrayProperty(ref writer, ref reader, ref keyReader, indent, writeIndent: true))
            {
                if (!reader.Read())
                {
                    break;
                }

                continue;
            }

            writer.WriteIndent(indent);
            WriteReaderString(ref writer, ref keyReader, isKey: true);

            if (IsPrimitive(reader.TokenType))
            {
                writer.WriteRaw(": "u8);
                WriteReaderValue(ref writer, ref reader, indent);
            }
            else
            {
                writer.WriteByte((byte)':');
                writer.WriteNewLine();
                WriteReaderValue(ref writer, ref reader, indent + 1);
            }

            if (!reader.Read())
            {
                break;
            }
        }
    }

    private static void WriteRegularRootProperty(ref Utf8ToonWriter writer, scoped ref Utf8JsonReader reader)
    {
        Utf8JsonReader keyReader = reader;
        if (!reader.Read())
        {
            ThrowHelper.ThrowToonException(SR.UnexpectedEndOfJsonObject);
        }

        if (reader.TokenType == JsonTokenType.StartObject && TryWriteEmptyObjectProperty(ref writer, ref reader, ref keyReader, 0, writeIndent: false))
        {
            return;
        }

        if (reader.TokenType == JsonTokenType.StartArray && TryWriteArrayProperty(ref writer, ref reader, ref keyReader, 0, writeIndent: false))
        {
            return;
        }

        WriteReaderString(ref writer, ref keyReader, isKey: true);
        if (IsPrimitive(reader.TokenType))
        {
            writer.WriteRaw(": "u8);
            WriteReaderValue(ref writer, ref reader, 0);
        }
        else
        {
            writer.WriteByte((byte)':');
            writer.WriteNewLine();
            WriteReaderValue(ref writer, ref reader, 1);
        }
    }

    private static void WriteReaderProperty(ref Utf8ToonWriter writer, scoped ReadOnlySpan<byte> key, scoped ref Utf8JsonReader reader, int indent)
    {
        if (reader.TokenType == JsonTokenType.StartObject && TryWriteEmptyObjectProperty(ref writer, ref reader, key, indent, writeIndent: false))
        {
            return;
        }

        if (reader.TokenType == JsonTokenType.StartArray && TryWriteArrayProperty(ref writer, ref reader, key, indent, writeIndent: false))
        {
            return;
        }

        writer.WriteKey(key);
        if (IsPrimitive(reader.TokenType))
        {
            writer.WriteRaw(": "u8);
            WriteReaderValue(ref writer, ref reader, indent);
        }
        else
        {
            writer.WriteByte((byte)':');
            writer.WriteNewLine();
            WriteReaderValue(ref writer, ref reader, indent + 1);
        }
    }

    private static void WriteReaderArray(ref Utf8ToonWriter writer, scoped ref Utf8JsonReader reader, int indent, bool writeIndent = true)
    {
        if (TryWritePrimitiveArray(ref writer, ref reader, indent, writeIndent))
        {
            return;
        }

        if (TryWriteTabularArray(ref writer, ref reader, indent, writeIndent))
        {
            return;
        }

        int itemCount = CountArrayItems(ref reader);
        if (itemCount == 0)
        {
            if (writeIndent)
            {
                writer.WriteIndent(indent);
                writer.WriteRaw("[]"u8);
            }
            else
            {
                writer.WriteRaw("[0]:"u8);
            }

            reader.Read();
            return;
        }

        if (writeIndent)
        {
            writer.WriteIndent(indent);
        }

        WriteExpandedArrayHeaderTail(ref writer, itemCount);
        WriteExpandedArrayItems(ref writer, ref reader, indent);
    }

    private static void WriteExpandedArrayItems(ref Utf8ToonWriter writer, scoped ref Utf8JsonReader reader, int indent)
    {
        if (!reader.Read() || reader.TokenType == JsonTokenType.EndArray)
        {
            return;
        }

        while (reader.TokenType != JsonTokenType.EndArray)
        {
            writer.WriteNewLine();
            writer.WriteIndent(indent + 1);
            writer.WriteByte((byte)'-');
            if (IsPrimitive(reader.TokenType))
            {
                writer.WriteByte((byte)' ');
                WriteReaderValue(ref writer, ref reader, indent);
            }
            else if (reader.TokenType == JsonTokenType.StartArray)
            {
                writer.WriteByte((byte)' ');
                WriteReaderArray(ref writer, ref reader, indent + 1, writeIndent: false);
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                WriteReaderObjectListItem(ref writer, ref reader, indent + 1);
            }
            else
            {
                writer.WriteNewLine();
                WriteReaderValue(ref writer, ref reader, indent + 1);
            }

            if (!reader.Read())
            {
                break;
            }
        }
    }

    private static void WriteReaderObjectListItem(ref Utf8ToonWriter writer, scoped ref Utf8JsonReader reader, int itemIndent)
    {
        if (!reader.Read() || reader.TokenType == JsonTokenType.EndObject)
        {
            return;
        }

        bool first = true;
        while (reader.TokenType == JsonTokenType.PropertyName)
        {
            Utf8JsonReader keyReader = reader;
            if (!reader.Read())
            {
                ThrowHelper.ThrowToonException(SR.UnexpectedEndOfJsonObject);
            }

            if (first)
            {
                writer.WriteByte((byte)' ');
            }
            else
            {
                writer.WriteNewLine();
                writer.WriteIndent(itemIndent + 1);
            }

            first = false;

            if (reader.TokenType == JsonTokenType.StartObject && TryWriteEmptyObjectProperty(ref writer, ref reader, ref keyReader, itemIndent + 1, writeIndent: false))
            {
                if (!reader.Read())
                {
                    break;
                }

                continue;
            }

            if (reader.TokenType == JsonTokenType.StartArray && TryWriteArrayProperty(ref writer, ref reader, ref keyReader, itemIndent + 1, writeIndent: false))
            {
                if (!reader.Read())
                {
                    break;
                }

                continue;
            }

            WriteReaderString(ref writer, ref keyReader, isKey: true);
            if (IsPrimitive(reader.TokenType))
            {
                writer.WriteRaw(": "u8);
                WriteReaderValue(ref writer, ref reader, itemIndent + 1);
            }
            else
            {
                writer.WriteByte((byte)':');
                writer.WriteNewLine();
                WriteReaderValue(ref writer, ref reader, itemIndent + 2);
            }

            if (!reader.Read())
            {
                break;
            }
        }
    }

    private static bool TryWriteEmptyObjectProperty(ref Utf8ToonWriter writer, scoped ref Utf8JsonReader reader, scoped ref Utf8JsonReader keyReader, int indent, bool writeIndent)
    {
        Utf8JsonReader probe = reader;
        if (!probe.Read() || probe.TokenType != JsonTokenType.EndObject)
        {
            return false;
        }

        if (writeIndent)
        {
            writer.WriteIndent(indent);
        }

        WriteReaderString(ref writer, ref keyReader, isKey: true);
        writer.WriteByte((byte)':');
        reader = probe;
        return true;
    }

    private static bool TryWriteEmptyObjectProperty(ref Utf8ToonWriter writer, scoped ref Utf8JsonReader reader, scoped ReadOnlySpan<byte> key, int indent, bool writeIndent)
    {
        Utf8JsonReader probe = reader;
        if (!probe.Read() || probe.TokenType != JsonTokenType.EndObject)
        {
            return false;
        }

        if (writeIndent)
        {
            writer.WriteIndent(indent);
        }

        writer.WriteKey(key);
        writer.WriteByte((byte)':');
        reader = probe;
        return true;
    }

    private static bool TryWriteArrayProperty(ref Utf8ToonWriter writer, scoped ref Utf8JsonReader reader, scoped ref Utf8JsonReader keyReader, int indent, bool writeIndent)
    {
        return TryWriteEmptyArrayProperty(ref writer, ref reader, ref keyReader, indent, writeIndent) ||
            TryWritePrimitiveArrayProperty(ref writer, ref reader, ref keyReader, indent, writeIndent) ||
            TryWriteTabularArrayProperty(ref writer, ref reader, ref keyReader, indent, writeIndent) ||
            TryWriteExpandedArrayProperty(ref writer, ref reader, ref keyReader, indent, writeIndent);
    }

    private static bool TryWriteArrayProperty(ref Utf8ToonWriter writer, scoped ref Utf8JsonReader reader, scoped ReadOnlySpan<byte> key, int indent, bool writeIndent)
    {
        return TryWriteEmptyArrayProperty(ref writer, ref reader, key, indent, writeIndent) ||
            TryWritePrimitiveArrayProperty(ref writer, ref reader, key, indent, writeIndent) ||
            TryWriteTabularArrayProperty(ref writer, ref reader, key, indent, writeIndent) ||
            TryWriteExpandedArrayProperty(ref writer, ref reader, key, indent, writeIndent);
    }

        private static bool TryWriteEmptyArrayProperty(ref Utf8ToonWriter writer, scoped ref Utf8JsonReader reader, scoped ref Utf8JsonReader keyReader, int indent, bool writeIndent)
        {
            Utf8JsonReader probe = reader;
            if (!probe.Read() || probe.TokenType != JsonTokenType.EndArray)
            {
                return false;
            }

            if (writeIndent)
            {
                writer.WriteIndent(indent);
            }

            WriteReaderString(ref writer, ref keyReader, isKey: true);
            writer.WriteRaw(": []"u8);
            reader = probe;
            return true;
        }

        private static bool TryWriteEmptyArrayProperty(ref Utf8ToonWriter writer, scoped ref Utf8JsonReader reader, scoped ReadOnlySpan<byte> key, int indent, bool writeIndent)
        {
            Utf8JsonReader probe = reader;
            if (!probe.Read() || probe.TokenType != JsonTokenType.EndArray)
            {
                return false;
            }

            if (writeIndent)
            {
                writer.WriteIndent(indent);
            }

            writer.WriteKey(key);
            writer.WriteRaw(": []"u8);
            reader = probe;
            return true;
        }

        private static bool TryWritePrimitiveArrayProperty(ref Utf8ToonWriter writer, scoped ref Utf8JsonReader reader, scoped ref Utf8JsonReader keyReader, int indent, bool writeIndent)
        {
            return TryWritePrimitiveArrayCore(ref writer, ref reader, ref keyReader, default, indent, hasKeyReader: true, hasKey: false, writeIndent);
        }

        private static bool TryWritePrimitiveArrayProperty(ref Utf8ToonWriter writer, scoped ref Utf8JsonReader reader, scoped ReadOnlySpan<byte> key, int indent, bool writeIndent)
        {
            Utf8JsonReader keyReader = reader;
            return TryWritePrimitiveArrayCore(ref writer, ref reader, ref keyReader, key, indent, hasKeyReader: false, hasKey: true, writeIndent);
        }

        private static bool TryWritePrimitiveArray(ref Utf8ToonWriter writer, scoped ref Utf8JsonReader reader, int indent, bool writeIndent)
        {
            Utf8JsonReader keyReader = reader;
            return TryWritePrimitiveArrayCore(ref writer, ref reader, ref keyReader, default, indent, hasKeyReader: false, hasKey: false, writeIndent);
        }

        private static bool TryWritePrimitiveArrayCore(ref Utf8ToonWriter writer, scoped ref Utf8JsonReader reader, scoped ref Utf8JsonReader keyReader, scoped ReadOnlySpan<byte> key, int indent, bool hasKeyReader, bool hasKey, bool writeIndent)
        {
            Span<CellRange> initialCells = stackalloc CellRange[16];
            ValueListBuilder<CellRange> cells = new(initialCells);
            Span<byte> initialCellBuffer = stackalloc byte[JsonConstants.StackallocByteThreshold];
            Utf8ValueStringBuilder cellBuffer = new(initialCellBuffer);
            Span<byte> initialDecodeBuffer = stackalloc byte[JsonConstants.StackallocByteThreshold];

            Utf8JsonReader probe = reader;
            if (!probe.Read() || probe.TokenType == JsonTokenType.EndArray)
            {
                cellBuffer.Dispose();
                cells.Dispose();
                return false;
            }

            try
            {
                while (probe.TokenType != JsonTokenType.EndArray)
                {
                    if (!IsPrimitive(probe.TokenType))
                    {
                        return false;
                    }

                    int cellStart = cellBuffer.Length;
                    switch (probe.TokenType)
                    {
                        case JsonTokenType.String:
                            byte[]? rentedCellArray = null;
                            scoped ReadOnlySpan<byte> cellValue = probe.ValueSpan;
                            try
                            {
                                if (probe.ValueIsEscaped)
                                {
                                    int maxLen = probe.ValueSpan.Length;
                                    if (maxLen <= JsonConstants.StackallocByteThreshold)
                                    {
                                        int written = probe.CopyString(initialDecodeBuffer);
                                        cellValue = initialDecodeBuffer.Slice(0, written);
                                    }
                                    else
                                    {
                                        rentedCellArray = ArrayPool<byte>.Shared.Rent(maxLen);
                                        int written = probe.CopyString(rentedCellArray);
                                        cellValue = rentedCellArray.AsSpan(0, written);
                                    }
                                }

                                AppendStringCell(ref cellBuffer, cellValue, writer.Options.Delimiter);
                            }
                            finally
                            {
                                if (rentedCellArray is not null)
                                {
                                    ArrayPool<byte>.Shared.Return(rentedCellArray);
                                }
                            }

                            break;

                        case JsonTokenType.Number:
                            Utf8ToonWriter.AppendTrustedJsonNumber(ref cellBuffer, probe.ValueSpan);
                            break;

                        case JsonTokenType.True:
                            cellBuffer.Append("true"u8);
                            break;

                        case JsonTokenType.False:
                            cellBuffer.Append("false"u8);
                            break;

                        default:
                            cellBuffer.Append("null"u8);
                            break;
                    }

                    cells.Append(new CellRange(cellStart, cellBuffer.Length - cellStart));
                    if (!probe.Read())
                    {
                        return false;
                    }
                }

                reader = probe;
                if (writeIndent)
                {
                    writer.WriteIndent(indent);
                }

                if (hasKeyReader)
                {
                    WriteReaderString(ref writer, ref keyReader, isKey: true);
                }
                else if (hasKey)
                {
                    writer.WriteKey(key);
                }

                WritePrimitiveArrayTail(ref writer, cellBuffer.AsSpan(), cells.AsSpan());
                return true;
            }
            catch
            {
                throw;
            }
            finally
            {
                cellBuffer.Dispose();
                cells.Dispose();
            }
        }

        private static void WritePrimitiveArrayTail(ref Utf8ToonWriter writer, scoped ReadOnlySpan<byte> cellBuffer, scoped ReadOnlySpan<CellRange> cells)
        {
            byte delimiter = GetDelimiterByte(writer.Options.Delimiter);
            writer.WriteByte((byte)'[');
            WriteInt32(ref writer, cells.Length);
            if (writer.Options.Delimiter != ToonDelimiter.Comma)
            {
                writer.WriteByte(delimiter);
            }

            writer.WriteRaw("]: "u8);
            WriteDelimitedValues(ref writer, cellBuffer, cells, delimiter);
        }

    private static bool TryWriteExpandedArrayProperty(ref Utf8ToonWriter writer, scoped ref Utf8JsonReader reader, scoped ref Utf8JsonReader keyReader, int indent, bool writeIndent)
    {
        int itemCount = CountArrayItems(ref reader);
        if (itemCount <= 0)
        {
            return false;
        }

        if (writeIndent)
        {
            writer.WriteIndent(indent);
        }

        WriteReaderString(ref writer, ref keyReader, isKey: true);
        WriteExpandedArrayHeaderTail(ref writer, itemCount);
        WriteExpandedArrayItems(ref writer, ref reader, indent);
        return true;
    }

    private static bool TryWriteExpandedArrayProperty(ref Utf8ToonWriter writer, scoped ref Utf8JsonReader reader, scoped ReadOnlySpan<byte> key, int indent, bool writeIndent)
    {
        int itemCount = CountArrayItems(ref reader);
        if (itemCount <= 0)
        {
            return false;
        }

        if (writeIndent)
        {
            writer.WriteIndent(indent);
        }

        writer.WriteKey(key);
        WriteExpandedArrayHeaderTail(ref writer, itemCount);
        WriteExpandedArrayItems(ref writer, ref reader, indent);
        return true;
    }

    private static void WriteExpandedArrayHeaderTail(ref Utf8ToonWriter writer, int itemCount)
    {
        byte delimiter = GetDelimiterByte(writer.Options.Delimiter);
        writer.WriteByte((byte)'[');
        WriteInt32(ref writer, itemCount);
        if (writer.Options.Delimiter != ToonDelimiter.Comma)
        {
            writer.WriteByte(delimiter);
        }

        writer.WriteRaw("]:"u8);
    }

    private static void AppendStringCell(ref Utf8ValueStringBuilder cellBuffer, scoped ReadOnlySpan<byte> cellValue, ToonDelimiter delimiter)
    {
        if (Utf8ToonWriter.IsBareString(cellValue, delimiter))
        {
            cellBuffer.Append(cellValue);
            return;
        }

        cellBuffer.Append((byte)'"');
        for (int i = 0; i < cellValue.Length; i++)
        {
            byte b = cellValue[i];
            switch (b)
            {
                case (byte)'"':
                    cellBuffer.Append("\\\""u8);
                    break;

                case (byte)'\\':
                    cellBuffer.Append("\\\\"u8);
                    break;

                case (byte)'\n':
                    cellBuffer.Append("\\n"u8);
                    break;

                case (byte)'\r':
                    cellBuffer.Append("\\r"u8);
                    break;

                case (byte)'\t':
                    cellBuffer.Append("\\t"u8);
                    break;

                default:
                    if (b < 0x20)
                    {
                        cellBuffer.Append("\\u00"u8);
                        cellBuffer.Append(ToHex((byte)(b >> 4)));
                        cellBuffer.Append(ToHex((byte)(b & 0x0F)));
                    }
                    else
                    {
                        cellBuffer.Append(b);
                    }

                    break;
            }
        }

        cellBuffer.Append((byte)'"');
    }

    private static void AppendNewLineAndIndent(ref Utf8ValueStringBuilder buffer, int indent, int indentSize)
    {
        buffer.Append((byte)'\n');
        AppendIndent(ref buffer, indent, indentSize);
    }

    private static void AppendIndent(ref Utf8ValueStringBuilder buffer, int indent, int indentSize)
    {
        int count = indent * indentSize;
        ReadOnlySpan<byte> spaces = "                "u8;
        while (count > 0)
        {
            int chunk = Math.Min(count, spaces.Length);
            buffer.Append(spaces.Slice(0, chunk));
            count -= chunk;
        }
    }

    private static void WriteReaderString(ref Utf8ToonWriter writer, scoped ref Utf8JsonReader reader, bool isKey)
    {
        if (!reader.ValueIsEscaped)
        {
            if (isKey)
            {
                writer.WriteKey(reader.ValueSpan);
            }
            else
            {
                writer.WriteStringValue(reader.ValueSpan);
            }

            return;
        }

        byte[]? rentedArray = null;
        int maxLen = reader.ValueSpan.Length;
        Span<byte> buffer = maxLen <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedArray = ArrayPool<byte>.Shared.Rent(maxLen));

        try
        {
            int written = reader.CopyString(buffer);
            if (isKey)
            {
                writer.WriteKey(buffer.Slice(0, written));
            }
            else
            {
                writer.WriteStringValue(buffer.Slice(0, written));
            }
        }
        finally
        {
            if (rentedArray is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
        }
    }

    private static bool IsPrimitive(JsonTokenType tokenType)
    {
        return tokenType is JsonTokenType.String or JsonTokenType.Number or JsonTokenType.True or JsonTokenType.False or JsonTokenType.Null;
    }

    private static bool TryWriteTabularArray(ref Utf8ToonWriter writer, scoped ref Utf8JsonReader reader, int indent, bool writeIndent)
    {
        Utf8JsonReader keyReader = reader;
        return TryWriteTabularArrayCore(ref writer, ref reader, ref keyReader, default, indent, hasKeyReader: false, hasKey: false, writeIndent);
    }

    private static bool TryWriteTabularArrayProperty(ref Utf8ToonWriter writer, scoped ref Utf8JsonReader reader, scoped ref Utf8JsonReader keyReader, int indent, bool writeIndent)
    {
        return TryWriteTabularArrayCore(ref writer, ref reader, ref keyReader, default, indent, hasKeyReader: true, hasKey: false, writeIndent);
    }

    private static bool TryWriteTabularArrayProperty(ref Utf8ToonWriter writer, scoped ref Utf8JsonReader reader, scoped ReadOnlySpan<byte> key, int indent, bool writeIndent)
    {
        Utf8JsonReader keyReader = reader;
        return TryWriteTabularArrayCore(ref writer, ref reader, ref keyReader, key, indent, hasKeyReader: false, hasKey: true, writeIndent);
    }

    private static bool TryWriteTabularArrayCore(ref Utf8ToonWriter writer, scoped ref Utf8JsonReader reader, scoped ref Utf8JsonReader keyReader, scoped ReadOnlySpan<byte> key, int indent, bool hasKeyReader, bool hasKey, bool writeIndent)
    {
        Utf8JsonReader probe = reader;
        if (!probe.Read() || probe.TokenType == JsonTokenType.EndArray)
        {
            return false;
        }

        Span<CellRange> initialFields = stackalloc CellRange[16];
        ValueListBuilder<CellRange> fields = new(initialFields);
        Span<CellRange> initialCells = stackalloc CellRange[64];
        ValueListBuilder<CellRange> cells = new(initialCells);

        Span<byte> initialFieldBuffer = stackalloc byte[JsonConstants.StackallocByteThreshold];
        Utf8ValueStringBuilder fieldBuffer = new(initialFieldBuffer);
        Span<byte> initialCellBuffer = stackalloc byte[JsonConstants.StackallocByteThreshold];
        Utf8ValueStringBuilder cellBuffer = new(initialCellBuffer);
        Span<byte> initialKeyDecodeBuffer = stackalloc byte[JsonConstants.StackallocByteThreshold];
        Span<byte> initialCellDecodeBuffer = stackalloc byte[JsonConstants.StackallocByteThreshold];

        try
        {
            int fieldCount = -1;
            int rowCount = 0;
            while (probe.TokenType != JsonTokenType.EndArray)
            {
                if (probe.TokenType != JsonTokenType.StartObject)
                {
                    return false;
                }

                if (!probe.Read())
                {
                    return false;
                }

                int currentField = 0;
                int rowCellStart = cells.Length;
                if (rowCount > 0)
                {
                    for (int i = 0; i < fieldCount; i++)
                    {
                        cells.Append(new CellRange(0, 0));
                    }
                }

                while (probe.TokenType == JsonTokenType.PropertyName)
                {
                    int fieldStart = fieldBuffer.Length;
                    byte[]? rentedKeyArray = null;
                    scoped ReadOnlySpan<byte> keyValue = probe.ValueSpan;
                    try
                    {
                        if (probe.ValueIsEscaped)
                        {
                            int maxLen = probe.ValueSpan.Length;
                            if (maxLen <= JsonConstants.StackallocByteThreshold)
                            {
                                int written = probe.CopyString(initialKeyDecodeBuffer);
                                keyValue = initialKeyDecodeBuffer.Slice(0, written);
                            }
                            else
                            {
                                rentedKeyArray = ArrayPool<byte>.Shared.Rent(maxLen);
                                int written = probe.CopyString(rentedKeyArray);
                                keyValue = rentedKeyArray.AsSpan(0, written);
                            }
                        }

                        if (IsBareKey(keyValue))
                        {
                            fieldBuffer.Append(keyValue);
                        }
                        else
                        {
                            fieldBuffer.Append((byte)'"');
                            for (int i = 0; i < keyValue.Length; i++)
                            {
                                byte b = keyValue[i];
                                switch (b)
                                {
                                    case (byte)'"':
                                        fieldBuffer.Append("\\\""u8);
                                        break;

                                    case (byte)'\\':
                                        fieldBuffer.Append("\\\\"u8);
                                        break;

                                    case (byte)'\n':
                                        fieldBuffer.Append("\\n"u8);
                                        break;

                                    case (byte)'\r':
                                        fieldBuffer.Append("\\r"u8);
                                        break;

                                    case (byte)'\t':
                                        fieldBuffer.Append("\\t"u8);
                                        break;

                                    default:
                                        if (b < 0x20)
                                        {
                                            fieldBuffer.Append("\\u00"u8);
                                            fieldBuffer.Append(ToHex((byte)(b >> 4)));
                                            fieldBuffer.Append(ToHex((byte)(b & 0x0F)));
                                        }
                                        else
                                        {
                                            fieldBuffer.Append(b);
                                        }

                                        break;
                                }
                            }

                            fieldBuffer.Append((byte)'"');
                        }
                    }
                    finally
                    {
                        if (rentedKeyArray is not null)
                        {
                            ArrayPool<byte>.Shared.Return(rentedKeyArray);
                        }
                    }

                    int fieldLength = fieldBuffer.Length - fieldStart;

                    if (!probe.Read() || !IsPrimitive(probe.TokenType))
                    {
                        return false;
                    }

                    int targetField = currentField;
                    if (rowCount == 0)
                    {
                        fields.Append(new CellRange(fieldStart, fieldLength));
                    }
                    else
                    {
                        ReadOnlySpan<byte> currentFieldName = fieldBuffer.AsSpan(fieldStart, fieldLength);
                        if (currentField < fieldCount &&
                            currentFieldName.SequenceEqual(fieldBuffer.AsSpan(fields[currentField].Offset, fields[currentField].Length)))
                        {
                            targetField = currentField;
                        }
                        else
                        {
                            targetField = FindFieldIndex(fieldBuffer.AsSpan(), fields.AsSpan().Slice(0, fieldCount), currentFieldName);
                        }

                        if (targetField < 0)
                        {
                            return false;
                        }

                        fieldBuffer.Length = fieldStart;
                    }

                    int cellStart = cellBuffer.Length;
                    switch (probe.TokenType)
                    {
                        case JsonTokenType.String:
                            byte[]? rentedCellArray = null;
                            scoped ReadOnlySpan<byte> cellValue = probe.ValueSpan;
                            try
                            {
                                if (probe.ValueIsEscaped)
                                {
                                    int maxLen = probe.ValueSpan.Length;
                                    if (maxLen <= JsonConstants.StackallocByteThreshold)
                                    {
                                        int written = probe.CopyString(initialCellDecodeBuffer);
                                        cellValue = initialCellDecodeBuffer.Slice(0, written);
                                    }
                                    else
                                    {
                                        rentedCellArray = ArrayPool<byte>.Shared.Rent(maxLen);
                                        int written = probe.CopyString(rentedCellArray);
                                        cellValue = rentedCellArray.AsSpan(0, written);
                                    }
                                }

                                AppendStringCell(ref cellBuffer, cellValue, writer.Options.Delimiter);
                            }
                            finally
                            {
                                if (rentedCellArray is not null)
                                {
                                    ArrayPool<byte>.Shared.Return(rentedCellArray);
                                }
                            }

                            break;

                        case JsonTokenType.Number:
                            Utf8ToonWriter.AppendTrustedJsonNumber(ref cellBuffer, probe.ValueSpan);
                            break;

                        case JsonTokenType.True:
                            cellBuffer.Append("true"u8);
                            break;

                        case JsonTokenType.False:
                            cellBuffer.Append("false"u8);
                            break;

                        default:
                            cellBuffer.Append("null"u8);
                            break;
                    }

                    CellRange cell = new(cellStart, cellBuffer.Length - cellStart);
                    if (rowCount == 0)
                    {
                        cells.Append(cell);
                    }
                    else
                    {
                        cells[rowCellStart + targetField] = cell;
                    }

                    currentField++;

                    if (!probe.Read())
                    {
                        return false;
                    }
                }

                if (probe.TokenType != JsonTokenType.EndObject || currentField == 0)
                {
                    return false;
                }

                if (rowCount == 0)
                {
                    fieldCount = currentField;
                }
                else if (currentField != fieldCount)
                {
                    return false;
                }

                rowCount++;

                if (!probe.Read())
                {
                    return false;
                }
            }

            if (rowCount == 0 || fieldCount <= 0)
            {
                return false;
            }

            if (writeIndent)
            {
                writer.WriteIndent(indent);
            }

            if (hasKeyReader)
            {
                WriteReaderString(ref writer, ref keyReader, isKey: true);
            }
            else if (hasKey)
            {
                writer.WriteKey(key);
            }

            WriteTabularArrayTail(ref writer, indent, fieldBuffer.AsSpan(), fields.AsSpan(), cellBuffer.AsSpan(), cells.AsSpan(), fieldCount, rowCount);
            reader = probe;
            return true;
        }
        finally
        {
            fieldBuffer.Dispose();
            cellBuffer.Dispose();
            fields.Dispose();
            cells.Dispose();
        }
    }

    private static void WriteTabularArrayTail(
        ref Utf8ToonWriter writer,
        int indent,
        scoped ReadOnlySpan<byte> fieldBuffer,
        scoped ReadOnlySpan<CellRange> fields,
        scoped ReadOnlySpan<byte> cellBuffer,
        scoped ReadOnlySpan<CellRange> cells,
        int fieldCount,
        int rowCount)
    {
        WriteTabularArrayHeader(ref writer, fieldBuffer, fields, fieldCount, rowCount);
        byte delimiter = GetDelimiterByte(writer.Options.Delimiter);

        for (int row = 0; row < rowCount; row++)
        {
            writer.WriteNewLine();
            writer.WriteIndent(indent + 1);
            WriteDelimitedValues(ref writer, cellBuffer, cells.Slice(row * fieldCount, fieldCount), delimiter);
        }
    }

    private static void WriteTabularArrayHeader(
        ref Utf8ToonWriter writer,
        scoped ReadOnlySpan<byte> fieldBuffer,
        scoped ReadOnlySpan<CellRange> fields,
        int fieldCount,
        int rowCount)
    {
        byte delimiter = GetDelimiterByte(writer.Options.Delimiter);

        writer.WriteByte((byte)'[');
        WriteInt32(ref writer, rowCount);
        if (writer.Options.Delimiter != ToonDelimiter.Comma)
        {
            writer.WriteByte(delimiter);
        }

        writer.WriteRaw("]{"u8);
        WriteDelimitedValues(ref writer, fieldBuffer, fields.Slice(0, fieldCount), delimiter);
        writer.WriteRaw("}:"u8);
    }

    private static void AppendTabularArrayHeader(
        ref Utf8ValueStringBuilder buffer,
        scoped ReadOnlySpan<byte> fieldBuffer,
        scoped ReadOnlySpan<CellRange> fields,
        int fieldCount,
        int rowCount,
        ToonDelimiter delimiterOption)
    {
        byte delimiter = GetDelimiterByte(delimiterOption);

        buffer.Append((byte)'[');
        buffer.Append(rowCount);
        if (delimiterOption != ToonDelimiter.Comma)
        {
            buffer.Append(delimiter);
        }

        buffer.Append("]{"u8);
        AppendDelimitedValues(ref buffer, fieldBuffer, fields.Slice(0, fieldCount), delimiter);
        buffer.Append("}:"u8);
    }

#if !STJ
#endif

    private static void WriteDelimitedValues(ref Utf8ToonWriter writer, scoped ReadOnlySpan<byte> buffer, scoped ReadOnlySpan<CellRange> values, byte delimiter)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0)
            {
                writer.WriteByte(delimiter);
            }

            CellRange range = values[i];
            writer.WriteRaw(buffer.Slice(range.Offset, range.Length));
        }
    }

    private static void AppendDelimitedValues(ref Utf8ValueStringBuilder destination, scoped ReadOnlySpan<byte> buffer, scoped ReadOnlySpan<CellRange> values, byte delimiter)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0)
            {
                destination.Append(delimiter);
            }

            CellRange range = values[i];
            destination.Append(buffer.Slice(range.Offset, range.Length));
        }
    }

    private static void WriteInt32(ref Utf8ToonWriter writer, int value)
    {
        Span<byte> buffer = stackalloc byte[11];
        _ = Utf8Formatter.TryFormat(value, buffer, out int written);

        writer.WriteRaw(buffer.Slice(0, written));
    }

    private static int CountArrayItems(scoped ref Utf8JsonReader reader)
    {
        Utf8JsonReader probe = reader;
        if (!probe.Read())
        {
            return -1;
        }

        if (probe.TokenType == JsonTokenType.EndArray)
        {
            return 0;
        }

        int count = 0;
        int nestedDepth = 0;
        while (true)
        {
            if (nestedDepth == 0)
            {
                if (probe.TokenType == JsonTokenType.EndArray)
                {
                    return count;
                }

                count++;
            }

            switch (probe.TokenType)
            {
                case JsonTokenType.StartArray:
                case JsonTokenType.StartObject:
                    nestedDepth++;
                    break;

                case JsonTokenType.EndArray:
                case JsonTokenType.EndObject:
                    nestedDepth--;
                    break;
            }

            if (!probe.Read())
            {
                return -1;
            }
        }
    }

    private static byte ToHex(byte value)
    {
        return (byte)(value < 10 ? value + (byte)'0' : value - 10 + (byte)'a');
    }

    private static bool IsBareKey(ReadOnlySpan<byte> value)
    {
        if (value.Length == 0 || !IsIdentifierStart(value[0]))
        {
            return false;
        }

        for (int i = 1; i < value.Length; i++)
        {
            if (!IsIdentifierPart(value[i]) && value[i] != '.')
            {
                return false;
            }
        }

        return true;
    }

    private static byte GetDelimiterByte(ToonDelimiter delimiter)
    {
        return delimiter switch
        {
            ToonDelimiter.Tab => (byte)'\t',
            ToonDelimiter.Pipe => (byte)'|',
            _ => (byte)',',
        };
    }

    private static bool IsIdentifierStart(byte value)
    {
        return (value >= (byte)'A' && value <= (byte)'Z') || (value >= (byte)'a' && value <= (byte)'z') || value == (byte)'_';
    }

    private static bool IsIdentifierPart(byte value)
    {
        return IsIdentifierStart(value) || (value >= (byte)'0' && value <= (byte)'9');
    }

    private readonly struct CellRange
    {
        public CellRange(int offset, int length)
        {
            this.Offset = offset;
            this.Length = length;
        }

        public int Offset { get; }

        public int Length { get; }
    }

#if STJ && TOON && !BUILDING_SOURCE_GENERATOR
    public static void Convert(ref Utf8ToonWriter writer, System.Text.Json.JsonElement element)
    {
        using ArrayPoolBufferWriter bufferWriter = new(256);
        using (Utf8JsonWriter jsonWriter = new(bufferWriter))
        {
            element.WriteTo(jsonWriter);
        }

        Convert(ref writer, bufferWriter.WrittenSpan);
    }
#elif !STJ
    public static void Convert<TElement>(ref Utf8ToonWriter writer, in TElement element)
        where TElement : struct, IJsonElement<TElement>
    {
        JsonElement jsonElement = JsonElement.From(in element);
        if (writer.Options.KeyFolding != ToonKeyFolding.Off &&
            writer.Options.FlattenDepth > 0 &&
            jsonElement.ValueKind == JsonValueKind.Object)
        {
            ConvertElementWithKeyFolding(ref writer, jsonElement);
            return;
        }

        WriteElementValue(ref writer, jsonElement, 0);
    }

    private static void ConvertElementWithKeyFolding(ref Utf8ToonWriter writer, JsonElement element)
    {
        Span<int> rootKeyBuckets = stackalloc int[Utf8KeyHashSet.StackAllocBucketSize];
        Span<byte> rootKeyEntries = stackalloc byte[Utf8KeyHashSet.StackAllocEntrySize];
        Span<byte> rootKeyBuffer = stackalloc byte[Utf8KeyHashSet.StackAllocKeyBufferSize];
        Utf8KeyHashSet rootKeys = new(16, rootKeyBuckets, rootKeyEntries, rootKeyBuffer);
        try
        {
            foreach (JsonProperty<JsonElement> property in element.EnumerateObject())
            {
                using UnescapedUtf8JsonString rootName = property.Utf8NameSpan;
                rootKeys.AddIfNotExists(rootName.Span);
            }

            bool first = true;
            Span<byte> initialPath = stackalloc byte[JsonConstants.StackallocByteThreshold];
            foreach (JsonProperty<JsonElement> property in element.EnumerateObject())
            {
                if (!first)
                {
                    writer.WriteNewLine();
                }

                first = false;
                Utf8ValueStringBuilder pathBuilder = new(initialPath);
                try
                {
                    bool canFold = true;
                    int segmentCount = 0;
                    JsonElement foldedValue = property.Value;
                    using (UnescapedUtf8JsonString rootName = property.Utf8NameSpan)
                    {
                        if (IsBareKeySegment(rootName.Span))
                        {
                            pathBuilder.Append(rootName.Span);
                            segmentCount = 1;
                        }
                        else
                        {
                            canFold = false;
                        }
                    }

                    while (canFold &&
                        segmentCount < writer.Options.FlattenDepth &&
                        foldedValue.ValueKind == JsonValueKind.Object &&
                        foldedValue.GetPropertyCount() == 1)
                    {
                        JsonProperty<JsonElement> childProperty = default;
                        foreach (JsonProperty<JsonElement> candidate in foldedValue.EnumerateObject())
                        {
                            childProperty = candidate;
                        }

                        int previousLength = pathBuilder.Length;
                        pathBuilder.Append((byte)'.');
                        using (UnescapedUtf8JsonString childName = childProperty.Utf8NameSpan)
                        {
                            if (!IsBareKeySegment(childName.Span))
                            {
                                pathBuilder.Length = previousLength;
                                break;
                            }

                            pathBuilder.Append(childName.Span);
                        }

                        if (rootKeys.Contains(pathBuilder.AsSpan()))
                        {
                            canFold = false;
                            break;
                        }

                        segmentCount++;
                        foldedValue = childProperty.Value;
                    }

                    if (!canFold)
                    {
                        WriteRegularElementRootProperty(ref writer, property);
                    }
                    else
                    {
                        WriteElementProperty(ref writer, pathBuilder.AsSpan(), foldedValue, 0);
                    }
                }
                finally
                {
                    pathBuilder.Dispose();
                }
            }
        }
        finally
        {
            rootKeys.Dispose();
        }
    }

    private static void WriteElementValue(ref Utf8ToonWriter writer, JsonElement element, int indent)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                WriteElementObject(ref writer, element, indent);
                break;

            case JsonValueKind.Array:
                WriteElementArray(ref writer, element, indent);
                break;

            case JsonValueKind.String:
                using (UnescapedUtf8JsonString value = element.GetUtf8String())
                {
                    writer.WriteStringValue(value.Span);
                }

                break;

            case JsonValueKind.Number:
                IJsonElement jsonElement = element;
                using (RawUtf8JsonString rawValue = jsonElement.ParentDocument.GetRawValue(jsonElement.ParentDocumentIndex, includeQuotes: false))
                {
                    writer.WriteTrustedJsonNumberValue(rawValue.Span);
                }

                break;

            case JsonValueKind.True:
                writer.WriteRaw("true"u8);
                break;

            case JsonValueKind.False:
                writer.WriteRaw("false"u8);
                break;

            case JsonValueKind.Null:
            default:
                writer.WriteRaw("null"u8);
                break;
        }
    }

    private static void WriteElementObject(ref Utf8ToonWriter writer, JsonElement element, int indent)
    {
        bool first = true;
        foreach (JsonProperty<JsonElement> property in element.EnumerateObject())
        {
            if (!first)
            {
                writer.WriteNewLine();
            }

            first = false;
            JsonElement value = property.Value;
            if (value.ValueKind == JsonValueKind.Object && TryWriteEmptyObjectElementProperty(ref writer, property, indent, writeIndent: true))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Array && TryWriteArrayElementProperty(ref writer, property, indent, writeIndent: true))
            {
                continue;
            }

            writer.WriteIndent(indent);
            WriteElementKey(ref writer, property);
            if (IsPrimitive(value.ValueKind))
            {
                writer.WriteRaw(": "u8);
                WriteElementValue(ref writer, value, indent);
            }
            else
            {
                writer.WriteByte((byte)':');
                writer.WriteNewLine();
                WriteElementValue(ref writer, value, indent + 1);
            }
        }
    }

    private static void WriteRegularElementRootProperty(ref Utf8ToonWriter writer, JsonProperty<JsonElement> property)
    {
        JsonElement value = property.Value;
        if (value.ValueKind == JsonValueKind.Object && TryWriteEmptyObjectElementProperty(ref writer, property, 0, writeIndent: false))
        {
            return;
        }

        if (value.ValueKind == JsonValueKind.Array && TryWriteArrayElementProperty(ref writer, property, 0, writeIndent: false))
        {
            return;
        }

        WriteElementKey(ref writer, property);
        if (IsPrimitive(value.ValueKind))
        {
            writer.WriteRaw(": "u8);
            WriteElementValue(ref writer, value, 0);
        }
        else
        {
            writer.WriteByte((byte)':');
            writer.WriteNewLine();
            WriteElementValue(ref writer, value, 1);
        }
    }

    private static void WriteElementProperty(ref Utf8ToonWriter writer, scoped ReadOnlySpan<byte> key, JsonElement value, int indent)
    {
        if (value.ValueKind == JsonValueKind.Object && TryWriteEmptyObjectElementProperty(ref writer, key, value, indent, writeIndent: false))
        {
            return;
        }

        if (value.ValueKind == JsonValueKind.Array && TryWriteArrayElementProperty(ref writer, key, value, indent, writeIndent: false))
        {
            return;
        }

        writer.WriteKey(key);
        if (IsPrimitive(value.ValueKind))
        {
            writer.WriteRaw(": "u8);
            WriteElementValue(ref writer, value, indent);
        }
        else
        {
            writer.WriteByte((byte)':');
            writer.WriteNewLine();
            WriteElementValue(ref writer, value, indent + 1);
        }
    }

    private static void WriteElementObjectListItem(ref Utf8ToonWriter writer, JsonElement element, int itemIndent)
    {
        bool first = true;
        foreach (JsonProperty<JsonElement> property in element.EnumerateObject())
        {
            JsonElement value = property.Value;
            if (first)
            {
                writer.WriteByte((byte)' ');
            }
            else
            {
                writer.WriteNewLine();
                writer.WriteIndent(itemIndent + 1);
            }

            first = false;
            if (value.ValueKind == JsonValueKind.Object && TryWriteEmptyObjectElementProperty(ref writer, property, itemIndent + 1, writeIndent: false))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Array && TryWriteArrayElementProperty(ref writer, property, itemIndent + 1, writeIndent: false))
            {
                continue;
            }

            WriteElementKey(ref writer, property);
            if (IsPrimitive(value.ValueKind))
            {
                writer.WriteRaw(": "u8);
                WriteElementValue(ref writer, value, itemIndent + 1);
            }
            else
            {
                writer.WriteByte((byte)':');
                writer.WriteNewLine();
                WriteElementValue(ref writer, value, itemIndent + 2);
            }
        }
    }

    private static void WriteElementArray(ref Utf8ToonWriter writer, JsonElement element, int indent, bool writeIndent = true)
    {
        if (TryWritePrimitiveElementArray(ref writer, element, indent, writeIndent))
        {
            return;
        }

        if (TryWriteTabularElementArray(ref writer, element, indent, writeIndent))
        {
            return;
        }

        int itemCount = element.GetArrayLength();
        if (itemCount == 0)
        {
            if (writeIndent)
            {
                writer.WriteIndent(indent);
                writer.WriteRaw("[]"u8);
            }
            else
            {
                writer.WriteRaw("[0]:"u8);
            }

            return;
        }

        if (writeIndent)
        {
            writer.WriteIndent(indent);
        }

        WriteExpandedArrayHeaderTail(ref writer, itemCount);
        WriteExpandedElementArrayItems(ref writer, element, indent);
    }

    private static void WriteExpandedElementArrayItems(ref Utf8ToonWriter writer, JsonElement element, int indent)
    {
        foreach (JsonElement item in element.EnumerateArray())
        {
            writer.WriteNewLine();
            writer.WriteIndent(indent + 1);
            writer.WriteByte((byte)'-');
            if (IsPrimitive(item.ValueKind))
            {
                writer.WriteByte((byte)' ');
                WriteElementValue(ref writer, item, indent);
            }
            else if (item.ValueKind == JsonValueKind.Array)
            {
                writer.WriteByte((byte)' ');
                WriteElementArray(ref writer, item, indent + 1, writeIndent: false);
            }
            else if (item.ValueKind == JsonValueKind.Object)
            {
                WriteElementObjectListItem(ref writer, item, indent + 1);
            }
            else
            {
                writer.WriteNewLine();
                WriteElementValue(ref writer, item, indent + 1);
            }
        }
    }

    private static bool TryWriteArrayElementProperty(ref Utf8ToonWriter writer, JsonProperty<JsonElement> property, int indent, bool writeIndent)
    {
        JsonElement value = property.Value;
        return TryWriteEmptyArrayElementProperty(ref writer, property, value, indent, writeIndent) ||
            TryWritePrimitiveElementArrayProperty(ref writer, property, value, indent, writeIndent) ||
            TryWriteTabularElementArrayProperty(ref writer, property, value, indent, writeIndent) ||
            TryWriteExpandedElementArrayProperty(ref writer, property, value, indent, writeIndent);
    }

    private static bool TryWriteArrayElementProperty(ref Utf8ToonWriter writer, scoped ReadOnlySpan<byte> key, JsonElement value, int indent, bool writeIndent)
    {
        return TryWriteEmptyArrayElementProperty(ref writer, key, value, indent, writeIndent) ||
            TryWritePrimitiveElementArrayProperty(ref writer, key, value, indent, writeIndent) ||
            TryWriteTabularElementArrayProperty(ref writer, key, value, indent, writeIndent) ||
            TryWriteExpandedElementArrayProperty(ref writer, key, value, indent, writeIndent);
    }

    private static bool TryWriteEmptyObjectElementProperty(ref Utf8ToonWriter writer, JsonProperty<JsonElement> property, int indent, bool writeIndent)
    {
        if (property.Value.GetPropertyCount() != 0)
        {
            return false;
        }

        if (writeIndent)
        {
            writer.WriteIndent(indent);
        }

        WriteElementKey(ref writer, property);
        writer.WriteByte((byte)':');
        return true;
    }

    private static bool TryWriteEmptyObjectElementProperty(ref Utf8ToonWriter writer, scoped ReadOnlySpan<byte> key, JsonElement value, int indent, bool writeIndent)
    {
        if (value.GetPropertyCount() != 0)
        {
            return false;
        }

        if (writeIndent)
        {
            writer.WriteIndent(indent);
        }

        writer.WriteKey(key);
        writer.WriteByte((byte)':');
        return true;
    }

    private static bool TryWriteEmptyArrayElementProperty(ref Utf8ToonWriter writer, JsonProperty<JsonElement> property, JsonElement value, int indent, bool writeIndent)
    {
        if (value.GetArrayLength() != 0)
        {
            return false;
        }

        if (writeIndent)
        {
            writer.WriteIndent(indent);
        }

        WriteElementKey(ref writer, property);
        writer.WriteRaw(": []"u8);
        return true;
    }

    private static bool TryWriteEmptyArrayElementProperty(ref Utf8ToonWriter writer, scoped ReadOnlySpan<byte> key, JsonElement value, int indent, bool writeIndent)
    {
        if (value.GetArrayLength() != 0)
        {
            return false;
        }

        if (writeIndent)
        {
            writer.WriteIndent(indent);
        }

        writer.WriteKey(key);
        writer.WriteRaw(": []"u8);
        return true;
    }

    private static bool TryWritePrimitiveElementArrayProperty(ref Utf8ToonWriter writer, JsonProperty<JsonElement> property, JsonElement value, int indent, bool writeIndent)
    {
        return TryWritePrimitiveElementArrayCore(ref writer, value, indent, writeIndent, hasProperty: true, property, default, hasKey: false);
    }

    private static bool TryWritePrimitiveElementArrayProperty(ref Utf8ToonWriter writer, scoped ReadOnlySpan<byte> key, JsonElement value, int indent, bool writeIndent)
    {
        return TryWritePrimitiveElementArrayCore(ref writer, value, indent, writeIndent, hasProperty: false, default, key, hasKey: true);
    }

    private static bool TryWritePrimitiveElementArray(ref Utf8ToonWriter writer, JsonElement element, int indent, bool writeIndent)
    {
        return TryWritePrimitiveElementArrayCore(ref writer, element, indent, writeIndent, hasProperty: false, default, default, hasKey: false);
    }

    private static bool TryWritePrimitiveElementArrayCore(ref Utf8ToonWriter writer, JsonElement element, int indent, bool writeIndent, bool hasProperty, JsonProperty<JsonElement> property, scoped ReadOnlySpan<byte> key, bool hasKey)
    {
        Span<CellRange> initialCells = stackalloc CellRange[16];
        ValueListBuilder<CellRange> cells = new(initialCells);
        Span<byte> initialCellBuffer = stackalloc byte[JsonConstants.StackallocByteThreshold];
        Utf8ValueStringBuilder cellBuffer = new(initialCellBuffer);

        try
        {
            if (element.GetArrayLength() == 0)
            {
                return false;
            }

            foreach (JsonElement item in element.EnumerateArray())
            {
                if (!IsPrimitive(item.ValueKind))
                {
                    return false;
                }

                int cellStart = cellBuffer.Length;
                AppendElementPrimitiveCell(ref cellBuffer, item, writer.Options.Delimiter);
                cells.Append(new CellRange(cellStart, cellBuffer.Length - cellStart));
            }

            if (writeIndent)
            {
                writer.WriteIndent(indent);
            }

            if (hasProperty)
            {
                WriteElementKey(ref writer, property);
            }
            else if (hasKey)
            {
                writer.WriteKey(key);
            }

            WritePrimitiveArrayTail(ref writer, cellBuffer.AsSpan(), cells.AsSpan());
            return true;
        }
        finally
        {
            cellBuffer.Dispose();
            cells.Dispose();
        }
    }

    private static bool TryWriteTabularElementArray(ref Utf8ToonWriter writer, JsonElement element, int indent, bool writeIndent)
    {
        return TryWriteTabularElementArrayCore(ref writer, element, indent, writeIndent, hasProperty: false, default, default, hasKey: false);
    }

    private static bool TryWriteTabularElementArrayProperty(ref Utf8ToonWriter writer, JsonProperty<JsonElement> property, JsonElement value, int indent, bool writeIndent)
    {
        return TryWriteTabularElementArrayCore(ref writer, value, indent, writeIndent, hasProperty: true, property, default, hasKey: false);
    }

    private static bool TryWriteTabularElementArrayProperty(ref Utf8ToonWriter writer, scoped ReadOnlySpan<byte> key, JsonElement value, int indent, bool writeIndent)
    {
        return TryWriteTabularElementArrayCore(ref writer, value, indent, writeIndent, hasProperty: false, default, key, hasKey: true);
    }

    private static bool TryWriteTabularElementArrayCore(ref Utf8ToonWriter writer, JsonElement element, int indent, bool writeIndent, bool hasProperty, JsonProperty<JsonElement> property, scoped ReadOnlySpan<byte> key, bool hasKey)
    {
        return TryWriteOrderedTabularElementArrayCore(ref writer, element, indent, writeIndent, hasProperty, property, key, hasKey) ||
            TryWriteBufferedTabularElementArrayCore(ref writer, element, indent, writeIndent, hasProperty, property, key, hasKey);
    }

    private static bool TryWriteOrderedTabularElementArrayCore(ref Utf8ToonWriter writer, JsonElement element, int indent, bool writeIndent, bool hasProperty, JsonProperty<JsonElement> property, scoped ReadOnlySpan<byte> key, bool hasKey)
    {
        Span<CellRange> initialFields = stackalloc CellRange[16];
        ValueListBuilder<CellRange> fields = new(initialFields);
        Span<CellRange> initialNames = stackalloc CellRange[16];
        ValueListBuilder<CellRange> names = new(initialNames);

        Span<byte> initialFieldBuffer = stackalloc byte[JsonConstants.StackallocByteThreshold];
        Utf8ValueStringBuilder fieldBuffer = new(initialFieldBuffer);
        Span<byte> initialNameBuffer = stackalloc byte[JsonConstants.StackallocByteThreshold];
        Utf8ValueStringBuilder nameBuffer = new(initialNameBuffer);
        Span<byte> initialRowBuffer = stackalloc byte[JsonConstants.StackallocByteThreshold];
        Utf8ValueStringBuilder rowBuffer = new(initialRowBuffer);
        Span<byte> initialHeaderBuffer = stackalloc byte[JsonConstants.StackallocByteThreshold];
        Utf8ValueStringBuilder headerBuffer = new(initialHeaderBuffer);
        Span<byte> initialRowPrefixBuffer = stackalloc byte[JsonConstants.StackallocByteThreshold];
        Utf8ValueStringBuilder rowPrefixBuffer = new(initialRowPrefixBuffer);

        try
        {
            int fieldCount = -1;
            int rowCount = 0;
            byte delimiter = GetDelimiterByte(writer.Options.Delimiter);
            int rowIndent = indent + 1;
            int indentSize = writer.Options.EffectiveIndentSize;
            AppendNewLineAndIndent(ref rowPrefixBuffer, rowIndent, indentSize);
            foreach (JsonElement row in element.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                int currentField = 0;
                rowBuffer.Append(rowPrefixBuffer.AsSpan());
                foreach (JsonProperty<JsonElement> rowProperty in row.EnumerateObject())
                {
                    JsonElement value = rowProperty.Value;
                    if (!IsPrimitive(value.ValueKind))
                    {
                        return false;
                    }

                    if (rowCount == 0)
                    {
                        if (JsonMarshal.IsPropertyNameEscaped(rowProperty))
                        {
                            using UnescapedUtf8JsonString fieldName = rowProperty.Utf8NameSpan;
                            AppendFirstRowElementField(ref fieldBuffer, ref fields, ref nameBuffer, ref names, fieldName.Span);
                        }
                        else
                        {
                            AppendFirstRowElementField(ref fieldBuffer, ref fields, ref nameBuffer, ref names, JsonMarshal.GetRawUtf8PropertyName(rowProperty));
                        }
                    }
                    else
                    {
                        if (currentField >= fieldCount)
                        {
                            return false;
                        }

                        CellRange name = names[currentField];
                        ReadOnlySpan<byte> expectedName = nameBuffer.AsSpan(name.Offset, name.Length);
                        bool nameMatches = JsonMarshal.IsPropertyNameEscaped(rowProperty)
                            ? rowProperty.NameEquals(expectedName)
                            : JsonMarshal.GetRawUtf8PropertyName(rowProperty).SequenceEqual(expectedName);
                        if (!nameMatches)
                        {
                            return false;
                        }
                    }

                    if (currentField > 0)
                    {
                        rowBuffer.Append(delimiter);
                    }

                    AppendElementPrimitiveCell(ref rowBuffer, value, writer.Options.Delimiter);
                    currentField++;
                }

                if (currentField == 0)
                {
                    return false;
                }

                if (rowCount == 0)
                {
                    fieldCount = currentField;
                }
                else if (currentField != fieldCount)
                {
                    return false;
                }

                rowCount++;
            }

            if (rowCount == 0 || fieldCount <= 0)
            {
                return false;
            }

            if (writeIndent)
            {
                AppendIndent(ref headerBuffer, indent, indentSize);
            }

            if (hasProperty)
            {
                AppendElementKey(ref headerBuffer, property);
            }
            else if (hasKey)
            {
                AppendElementFieldName(ref headerBuffer, key);
            }

            AppendTabularArrayHeader(ref headerBuffer, fieldBuffer.AsSpan(), fields.AsSpan(), fieldCount, rowCount, writer.Options.Delimiter);
            writer.WriteRaw(headerBuffer.AsSpan());
            writer.WriteRaw(rowBuffer.AsSpan());
            return true;
        }
        finally
        {
            fieldBuffer.Dispose();
            nameBuffer.Dispose();
            rowBuffer.Dispose();
            headerBuffer.Dispose();
            rowPrefixBuffer.Dispose();
            fields.Dispose();
            names.Dispose();
        }
    }

    private static bool TryWriteBufferedTabularElementArrayCore(ref Utf8ToonWriter writer, JsonElement element, int indent, bool writeIndent, bool hasProperty, JsonProperty<JsonElement> property, scoped ReadOnlySpan<byte> key, bool hasKey)
    {
        Span<CellRange> initialFields = stackalloc CellRange[16];
        ValueListBuilder<CellRange> fields = new(initialFields);
        Span<CellRange> initialCells = stackalloc CellRange[64];
        ValueListBuilder<CellRange> cells = new(initialCells);

        Span<byte> initialFieldBuffer = stackalloc byte[JsonConstants.StackallocByteThreshold];
        Utf8ValueStringBuilder fieldBuffer = new(initialFieldBuffer);
        Span<byte> initialCellBuffer = stackalloc byte[JsonConstants.StackallocByteThreshold];
        Utf8ValueStringBuilder cellBuffer = new(initialCellBuffer);

        try
        {
            int fieldCount = -1;
            int rowCount = 0;
            foreach (JsonElement row in element.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                int currentField = 0;
                int rowCellStart = cells.Length;
                if (rowCount > 0)
                {
                    for (int i = 0; i < fieldCount; i++)
                    {
                        cells.Append(new CellRange(0, 0));
                    }
                }

                foreach (JsonProperty<JsonElement> rowProperty in row.EnumerateObject())
                {
                    JsonElement value = rowProperty.Value;
                    if (!IsPrimitive(value.ValueKind))
                    {
                        return false;
                    }

                    int fieldStart = fieldBuffer.Length;
                    using (UnescapedUtf8JsonString fieldName = rowProperty.Utf8NameSpan)
                    {
                        AppendElementFieldName(ref fieldBuffer, fieldName.Span);
                    }

                    int targetField = currentField;
                    if (rowCount == 0)
                    {
                        fields.Append(new CellRange(fieldStart, fieldBuffer.Length - fieldStart));
                    }
                    else
                    {
                        ReadOnlySpan<byte> currentFieldName = fieldBuffer.AsSpan(fieldStart, fieldBuffer.Length - fieldStart);
                        if (currentField < fieldCount &&
                            currentFieldName.SequenceEqual(fieldBuffer.AsSpan(fields[currentField].Offset, fields[currentField].Length)))
                        {
                            targetField = currentField;
                        }
                        else
                        {
                            targetField = FindFieldIndex(fieldBuffer.AsSpan(), fields.AsSpan().Slice(0, fieldCount), currentFieldName);
                        }

                        if (targetField < 0)
                        {
                            return false;
                        }

                        fieldBuffer.Length = fieldStart;
                    }

                    int cellStart = cellBuffer.Length;
                    AppendElementPrimitiveCell(ref cellBuffer, value, writer.Options.Delimiter);
                    CellRange cell = new(cellStart, cellBuffer.Length - cellStart);
                    if (rowCount == 0)
                    {
                        cells.Append(cell);
                    }
                    else
                    {
                        cells[rowCellStart + targetField] = cell;
                    }

                    currentField++;
                }

                if (currentField == 0)
                {
                    return false;
                }

                if (rowCount == 0)
                {
                    fieldCount = currentField;
                }
                else if (currentField != fieldCount)
                {
                    return false;
                }

                rowCount++;
            }

            if (rowCount == 0 || fieldCount <= 0)
            {
                return false;
            }

            if (writeIndent)
            {
                writer.WriteIndent(indent);
            }

            if (hasProperty)
            {
                WriteElementKey(ref writer, property);
            }
            else if (hasKey)
            {
                writer.WriteKey(key);
            }

            WriteTabularArrayTail(ref writer, indent, fieldBuffer.AsSpan(), fields.AsSpan(), cellBuffer.AsSpan(), cells.AsSpan(), fieldCount, rowCount);
            return true;
        }
        finally
        {
            fieldBuffer.Dispose();
            cellBuffer.Dispose();
            fields.Dispose();
            cells.Dispose();
        }
    }

    private static bool TryWriteExpandedElementArrayProperty(ref Utf8ToonWriter writer, JsonProperty<JsonElement> property, JsonElement value, int indent, bool writeIndent)
    {
        int itemCount = value.GetArrayLength();
        if (itemCount <= 0)
        {
            return false;
        }

        if (writeIndent)
        {
            writer.WriteIndent(indent);
        }

        WriteElementKey(ref writer, property);
        WriteExpandedArrayHeaderTail(ref writer, itemCount);
        WriteExpandedElementArrayItems(ref writer, value, indent);
        return true;
    }

    private static bool TryWriteExpandedElementArrayProperty(ref Utf8ToonWriter writer, scoped ReadOnlySpan<byte> key, JsonElement value, int indent, bool writeIndent)
    {
        int itemCount = value.GetArrayLength();
        if (itemCount <= 0)
        {
            return false;
        }

        if (writeIndent)
        {
            writer.WriteIndent(indent);
        }

        writer.WriteKey(key);
        WriteExpandedArrayHeaderTail(ref writer, itemCount);
        WriteExpandedElementArrayItems(ref writer, value, indent);
        return true;
    }

    private static void AppendElementFieldName(ref Utf8ValueStringBuilder fieldBuffer, scoped ReadOnlySpan<byte> fieldName)
    {
        if (IsBareKey(fieldName))
        {
            fieldBuffer.Append(fieldName);
            return;
        }

        AppendQuotedUtf8(ref fieldBuffer, fieldName);
    }

    private static void AppendFirstRowElementField(
        ref Utf8ValueStringBuilder fieldBuffer,
        ref ValueListBuilder<CellRange> fields,
        ref Utf8ValueStringBuilder nameBuffer,
        ref ValueListBuilder<CellRange> names,
        scoped ReadOnlySpan<byte> fieldName)
    {
        int nameStart = nameBuffer.Length;
        nameBuffer.Append(fieldName);
        names.Append(new CellRange(nameStart, nameBuffer.Length - nameStart));

        int fieldStart = fieldBuffer.Length;
        AppendElementFieldName(ref fieldBuffer, fieldName);
        fields.Append(new CellRange(fieldStart, fieldBuffer.Length - fieldStart));
    }

    private static void AppendElementKey(ref Utf8ValueStringBuilder buffer, JsonProperty<JsonElement> property)
    {
        if (JsonMarshal.IsPropertyNameEscaped(property))
        {
            using UnescapedUtf8JsonString key = property.Utf8NameSpan;
            AppendElementFieldName(ref buffer, key.Span);
        }
        else
        {
            AppendElementFieldName(ref buffer, JsonMarshal.GetRawUtf8PropertyName(property));
        }
    }

    private static void AppendQuotedUtf8(ref Utf8ValueStringBuilder buffer, scoped ReadOnlySpan<byte> value)
    {
        buffer.Append((byte)'"');
        for (int i = 0; i < value.Length; i++)
        {
            byte b = value[i];
            switch (b)
            {
                case (byte)'"':
                    buffer.Append("\\\""u8);
                    break;

                case (byte)'\\':
                    buffer.Append("\\\\"u8);
                    break;

                case (byte)'\n':
                    buffer.Append("\\n"u8);
                    break;

                case (byte)'\r':
                    buffer.Append("\\r"u8);
                    break;

                case (byte)'\t':
                    buffer.Append("\\t"u8);
                    break;

                default:
                    if (b < 0x20)
                    {
                        buffer.Append("\\u00"u8);
                        buffer.Append(ToHex((byte)(b >> 4)));
                        buffer.Append(ToHex((byte)(b & 0x0F)));
                    }
                    else
                    {
                        buffer.Append(b);
                    }

                    break;
            }
        }

        buffer.Append((byte)'"');
    }

    private static void AppendElementPrimitiveCell(ref Utf8ValueStringBuilder cellBuffer, JsonElement element, ToonDelimiter delimiter)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                if (JsonMarshal.IsValueEscaped(element))
                {
                    using UnescapedUtf8JsonString cellValue = element.GetUtf8String();
                    AppendStringCell(ref cellBuffer, cellValue.Span, delimiter);
                }
                else
                {
                    using RawUtf8JsonString rawValue = JsonMarshal.GetRawUtf8Value(element);
                    ReadOnlySpan<byte> rawValueSpan = rawValue.Span;
                    AppendStringCell(ref cellBuffer, rawValueSpan.Slice(1, rawValueSpan.Length - 2), delimiter);
                }

                break;

            case JsonValueKind.Number:
                IJsonElement jsonElement = element;
                using (RawUtf8JsonString rawValue = jsonElement.ParentDocument.GetRawValue(jsonElement.ParentDocumentIndex, includeQuotes: false))
                {
                    Utf8ToonWriter.AppendTrustedJsonNumber(ref cellBuffer, rawValue.Span);
                }

                break;

            case JsonValueKind.True:
                cellBuffer.Append("true"u8);
                break;

            case JsonValueKind.False:
                cellBuffer.Append("false"u8);
                break;

            default:
                cellBuffer.Append("null"u8);
                break;
        }
    }

    private static void WriteElementPrimitiveCell(ref Utf8ToonWriter writer, JsonElement element, ToonDelimiter delimiter)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                if (JsonMarshal.IsValueEscaped(element))
                {
                    using UnescapedUtf8JsonString cellValue = element.GetUtf8String();
                    writer.WriteStringValue(cellValue.Span);
                }
                else
                {
                    using RawUtf8JsonString rawValue = JsonMarshal.GetRawUtf8Value(element);
                    ReadOnlySpan<byte> rawValueSpan = rawValue.Span;
                    writer.WriteStringValue(rawValueSpan.Slice(1, rawValueSpan.Length - 2));
                }

                break;

            case JsonValueKind.Number:
                IJsonElement jsonElement = element;
                using (RawUtf8JsonString rawValue = jsonElement.ParentDocument.GetRawValue(jsonElement.ParentDocumentIndex, includeQuotes: false))
                {
                    writer.WriteTrustedJsonNumberValue(rawValue.Span);
                }

                break;

            case JsonValueKind.True:
                writer.WriteRaw("true"u8);
                break;

            case JsonValueKind.False:
                writer.WriteRaw("false"u8);
                break;

            default:
                writer.WriteRaw("null"u8);
                break;
        }
    }

    private static void WriteElementKey(ref Utf8ToonWriter writer, JsonProperty<JsonElement> property)
    {
        if (JsonMarshal.IsPropertyNameEscaped(property))
        {
            using UnescapedUtf8JsonString key = property.Utf8NameSpan;
            writer.WriteKey(key.Span);
        }
        else
        {
            writer.WriteKey(JsonMarshal.GetRawUtf8PropertyName(property));
        }
    }

    private static bool IsPrimitive(JsonValueKind valueKind)
    {
        return valueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null;
    }
#endif
}