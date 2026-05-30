using System.Buffers;
using System.Text;
using Corvus.Text;

#if STJ && TOON
using System.Text.Json;

namespace Corvus.Toon.Internal;
#else
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Toon.Internal;
#endif

internal ref struct ToonToJsonConverter
{
    private readonly ReadOnlySpan<byte> source;
    private readonly Utf8JsonWriter writer;
    private readonly ToonReaderOptions options;
    private readonly bool suppressNormalization;
    private int position;
    private int lineNumber;

    public ToonToJsonConverter(ReadOnlySpan<byte> source, Utf8JsonWriter writer, ToonReaderOptions options)
        : this(source, writer, options, false)
    {
    }

    private ToonToJsonConverter(ReadOnlySpan<byte> source, Utf8JsonWriter writer, ToonReaderOptions options, bool suppressNormalization)
    {
        this.source = source;
        this.writer = writer;
        this.options = options;
        this.suppressNormalization = suppressNormalization;
        this.position = HasUtf8Bom(source) ? 3 : 0;
        this.lineNumber = 1;
    }

    public void Convert()
    {
        if (!this.suppressNormalization && (!this.options.Strict || this.options.ExpandPaths != ToonPathExpansion.Off))
        {
            this.ConvertWithNormalization();
            return;
        }

        if (!this.TryPeekNextNonBlank(out ToonLine line))
        {
            this.writer.WriteStartObject();
            this.writer.WriteEndObject();
            return;
        }

        this.WriteNode(line.Indent);
        if (this.options.Strict && this.TryPeekNextNonBlank(out ToonLine trailing))
        {
            ThrowHelper.ThrowToonException(SR.UnexpectedRootContent, trailing.Number, trailing.Indent + 1);
        }
    }

    private void ConvertWithNormalization()
    {
        byte[] literalPathKeyBytes = [];
        int[] literalPathKeyOffsets = [];
        int[] literalPathKeyLengths = [];
        int literalPathKeyCount = 0;
        int literalPathKeyByteLength = 0;
        try
        {
            if (this.options.ExpandPaths != ToonPathExpansion.Off)
            {
                literalPathKeyBytes = ArrayPool<byte>.Shared.Rent(Utf8KeyHashSet.StackAllocKeyBufferSize);
                literalPathKeyOffsets = ArrayPool<int>.Shared.Rent(16);
                literalPathKeyLengths = ArrayPool<int>.Shared.Rent(16);
                this.CollectQuotedRootPathKeys(
                    ref literalPathKeyBytes,
                    ref literalPathKeyOffsets,
                    ref literalPathKeyLengths,
                    ref literalPathKeyCount,
                    ref literalPathKeyByteLength);
            }

            using ArrayPoolBufferWriter bufferWriter = new(Math.Max(this.source.Length, 256));
            ToonReaderOptions unexpandedOptions = new()
            {
                Strict = this.options.Strict,
                IndentSize = this.options.IndentSize,
                ExpandPaths = ToonPathExpansion.Off,
            };

            using (Utf8JsonWriter jsonWriter = new(bufferWriter))
            {
                ToonToJsonConverter converter = new(this.source, jsonWriter, unexpandedOptions, true);
                converter.Convert();
                jsonWriter.Flush();
            }

            Utf8JsonReader reader = new(bufferWriter.WrittenSpan);
            if (!reader.Read())
            {
                this.writer.WriteNullValue();
                return;
            }

            Span<TreeNode> initialNodes = stackalloc TreeNode[64];
            Span<byte> initialKeys = stackalloc byte[Utf8KeyHashSet.StackAllocKeyBufferSize];
            PooledJsonTree tree = new(
                bufferWriter.WrittenSpan,
                this.options,
                initialNodes,
                initialKeys,
                literalPathKeyBytes,
                literalPathKeyOffsets,
                literalPathKeyLengths,
                literalPathKeyCount);
            try
            {
                int root = tree.BuildRoot(reader);
                tree.Write(root, this.writer);
            }
            finally
            {
                tree.Dispose();
            }
        }
        finally
        {
            if (literalPathKeyBytes.Length > 0)
            {
                ArrayPool<byte>.Shared.Return(literalPathKeyBytes);
                ArrayPool<int>.Shared.Return(literalPathKeyOffsets);
                ArrayPool<int>.Shared.Return(literalPathKeyLengths);
            }
        }
    }

    private void CollectQuotedRootPathKeys(
        ref byte[] keyBytes,
        ref int[] keyOffsets,
        ref int[] keyLengths,
        ref int keyCount,
        ref int keyByteLength)
    {
        int savedPosition = this.position;
        int savedLineNumber = this.lineNumber;
        Span<byte> initialDecodedKey = stackalloc byte[JsonConstants.StackallocByteThreshold];
        Span<byte> initialNormalizedKey = stackalloc byte[JsonConstants.StackallocByteThreshold];
        while (this.TryPeekNextNonBlank(out ToonLine line))
        {
            this.ConsumeLine(line);
            if (line.Indent != 0)
            {
                continue;
            }

            ReadOnlySpan<byte> key = default;
            if (this.TryParseHeader(line.Content, out Header header) && header.HasKey)
            {
                key = Trim(header.Key);
            }
            else if (TryFindColon(line.Content, out int colon))
            {
                key = Trim(line.Content.Slice(0, colon));
            }

            if (key.Length < 2 || key[0] != (byte)'"' || key[key.Length - 1] != (byte)'"')
            {
                continue;
            }

            byte[]? rented = null;
            try
            {
                Utf8ValueStringBuilder normalized = new(initialNormalizedKey);
                try
                {
                    ReadOnlySpan<byte> token = NormalizeQuotedToken(key, ref normalized);
                    Utf8JsonReader reader = new(token);
                    if (!reader.Read() || reader.TokenType != JsonTokenType.String)
                    {
                        ThrowHelper.ThrowToonException(SR.InvalidQuotedToonKey, line.Number, line.Indent + 1);
                    }

                    int maxLen = reader.ValueSpan.Length;
                    Span<byte> buffer = maxLen <= initialDecodedKey.Length
                        ? initialDecodedKey
                        : (rented = ArrayPool<byte>.Shared.Rent(maxLen));
                    int written = reader.CopyString(buffer);
                    ReadOnlySpan<byte> decoded = buffer.Slice(0, written);
                    if (decoded.IndexOf((byte)'.') >= 0)
                    {
                        AddLiteralPathKey(decoded, ref keyBytes, ref keyOffsets, ref keyLengths, ref keyCount, ref keyByteLength);
                    }
                }
                finally
                {
                    normalized.Dispose();
                }
            }
            catch (JsonException ex)
            {
                ThrowHelper.ThrowToonException(SR.Format(SR.InvalidQuotedToonKeyWithMessage, ex.Message), line.Number, line.Indent + 1);
            }
            finally
            {
                if (rented is not null)
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }
        }

        this.position = savedPosition;
        this.lineNumber = savedLineNumber;
    }

    private static void AddLiteralPathKey(
        ReadOnlySpan<byte> key,
        ref byte[] keyBytes,
        ref int[] keyOffsets,
        ref int[] keyLengths,
        ref int keyCount,
        ref int keyByteLength)
    {
        for (int i = 0; i < keyCount; i++)
        {
            if (key.SequenceEqual(keyBytes.AsSpan(keyOffsets[i], keyLengths[i])))
            {
                return;
            }
        }

        if (keyCount == keyOffsets.Length)
        {
            int[] newOffsets = ArrayPool<int>.Shared.Rent(keyOffsets.Length * 2);
            int[] newLengths = ArrayPool<int>.Shared.Rent(keyLengths.Length * 2);
            keyOffsets.AsSpan(0, keyCount).CopyTo(newOffsets);
            keyLengths.AsSpan(0, keyCount).CopyTo(newLengths);
            ArrayPool<int>.Shared.Return(keyOffsets);
            ArrayPool<int>.Shared.Return(keyLengths);
            keyOffsets = newOffsets;
            keyLengths = newLengths;
        }

        if (keyByteLength + key.Length > keyBytes.Length)
        {
            byte[] newBytes = ArrayPool<byte>.Shared.Rent(Math.Max(keyBytes.Length * 2, keyByteLength + key.Length));
            keyBytes.AsSpan(0, keyByteLength).CopyTo(newBytes);
            ArrayPool<byte>.Shared.Return(keyBytes);
            keyBytes = newBytes;
        }

        keyOffsets[keyCount] = keyByteLength;
        keyLengths[keyCount] = key.Length;
        key.CopyTo(keyBytes.AsSpan(keyByteLength, key.Length));
        keyByteLength += key.Length;
        keyCount++;
    }

    private enum TreeNodeKind : byte
    {
        Raw,
        Object,
        Array,
    }

    private struct TreeNode
    {
        public TreeNodeKind Kind;
        public int NameOffset;
        public int NameLength;
        public int ValueOffset;
        public int ValueLength;
        public int FirstChild;
        public int LastChild;
        public int NextSibling;
    }

    private ref struct PooledJsonTree
    {
        private readonly ReadOnlySpan<byte> json;
        private readonly ToonReaderOptions options;
        private Span<TreeNode> nodes;
        private TreeNode[]? rentedNodes;
        private Span<byte> keys;
        private byte[]? rentedKeys;
        private readonly byte[] literalPathKeyBytes;
        private readonly int[] literalPathKeyOffsets;
        private readonly int[] literalPathKeyLengths;
        private readonly int literalPathKeyCount;
        private int nodeCount;
        private int keyLength;

        public PooledJsonTree(
            ReadOnlySpan<byte> json,
            ToonReaderOptions options,
            Span<TreeNode> initialNodes,
            Span<byte> initialKeys,
            byte[] literalPathKeyBytes,
            int[] literalPathKeyOffsets,
            int[] literalPathKeyLengths,
            int literalPathKeyCount)
        {
            this.json = json;
            this.options = options;
            this.nodes = initialNodes;
            this.rentedNodes = null;
            this.keys = initialKeys;
            this.rentedKeys = null;
            this.literalPathKeyBytes = literalPathKeyBytes;
            this.literalPathKeyOffsets = literalPathKeyOffsets;
            this.literalPathKeyLengths = literalPathKeyLengths;
            this.literalPathKeyCount = literalPathKeyCount;
            this.nodeCount = 0;
            this.keyLength = 0;
        }

        public int BuildRoot(Utf8JsonReader reader)
        {
            if (reader.TokenType == JsonTokenType.StartObject && this.options.ExpandPaths != ToonPathExpansion.Off)
            {
                return this.BuildObject(ref reader, true);
            }

            return this.BuildValue(ref reader, false);
        }

        public void Write(int nodeIndex, Utf8JsonWriter writer)
        {
            TreeNode node = this.nodes[nodeIndex];
            switch (node.Kind)
            {
                case TreeNodeKind.Object:
                    writer.WriteStartObject();
                    for (int child = node.FirstChild; child >= 0; child = this.nodes[child].NextSibling)
                    {
                        TreeNode childNode = this.nodes[child];
                        writer.WritePropertyName(this.keys.Slice(childNode.NameOffset, childNode.NameLength));
                        this.Write(child, writer);
                    }

                    writer.WriteEndObject();
                    break;

                case TreeNodeKind.Array:
                    writer.WriteStartArray();
                    for (int child = node.FirstChild; child >= 0; child = this.nodes[child].NextSibling)
                    {
                        this.Write(child, writer);
                    }

                    writer.WriteEndArray();
                    break;

                default:
                    writer.WriteRawValue(this.json.Slice(node.ValueOffset, node.ValueLength), skipInputValidation: true);
                    break;
            }
        }

        public void Dispose()
        {
            if (this.rentedNodes is not null)
            {
                ArrayPool<TreeNode>.Shared.Return(this.rentedNodes);
                this.rentedNodes = null;
            }

            if (this.rentedKeys is not null)
            {
                ArrayPool<byte>.Shared.Return(this.rentedKeys);
                this.rentedKeys = null;
            }
        }

        private int BuildValue(scoped ref Utf8JsonReader reader, bool expandRoot)
        {
            return reader.TokenType switch
            {
                JsonTokenType.StartObject => this.BuildObject(ref reader, expandRoot),
                JsonTokenType.StartArray => this.BuildArray(ref reader),
                _ => this.AddRawNode((int)reader.TokenStartIndex, checked((int)(reader.BytesConsumed - reader.TokenStartIndex))),
            };
        }

        private int BuildObject(scoped ref Utf8JsonReader reader, bool expandRoot)
        {
            int objectNode = this.AddContainerNode(TreeNodeKind.Object);
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return objectNode;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    ThrowHelper.ThrowToonException(SR.ExpectedObjectMember);
                }

                int nameOffset = this.CopyPropertyName(ref reader, out int nameLength);
                if (!reader.Read())
                {
                    ThrowHelper.ThrowToonException(SR.UnexpectedEndOfJsonObject);
                }

                int valueNode = this.BuildValue(ref reader, false);
                ReadOnlySpan<byte> name = this.keys.Slice(nameOffset, nameLength);
                if (expandRoot && !this.ContainsLiteralPathKey(name) && IsExpandablePath(name))
                {
                    this.AddExpandedProperty(objectNode, nameOffset, nameLength, valueNode);
                }
                else
                {
                    this.SetNodeName(valueNode, nameOffset, nameLength);
                    this.AddProperty(objectNode, valueNode);
                }
            }

            ThrowHelper.ThrowToonException(SR.UnexpectedEndOfJsonObject);
            return objectNode;
        }

        private int BuildArray(scoped ref Utf8JsonReader reader)
        {
            int arrayNode = this.AddContainerNode(TreeNodeKind.Array);
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    return arrayNode;
                }

                int child = this.BuildValue(ref reader, false);
                this.AppendChild(arrayNode, child);
            }

            ThrowHelper.ThrowToonException(SR.UnexpectedEndOfJsonObject);
            return arrayNode;
        }

        private bool ContainsLiteralPathKey(ReadOnlySpan<byte> name)
        {
            for (int i = 0; i < this.literalPathKeyCount; i++)
            {
                if (name.SequenceEqual(this.literalPathKeyBytes.AsSpan(this.literalPathKeyOffsets[i], this.literalPathKeyLengths[i])))
                {
                    return true;
                }
            }

            return false;
        }

        private void AddExpandedProperty(int rootNode, int pathOffset, int pathLength, int valueNode)
        {
            int current = rootNode;
            int segmentStart = pathOffset;
            int pathEnd = pathOffset + pathLength;
            for (int i = pathOffset; i <= pathEnd; i++)
            {
                if (i != pathEnd && this.keys[i] != (byte)'.')
                {
                    continue;
                }

                int segmentLength = i - segmentStart;
                bool isLast = i == pathEnd;
                if (isLast)
                {
                    this.SetNodeNameFromExistingKey(valueNode, segmentStart, segmentLength);
                    this.AddProperty(current, valueNode);
                    return;
                }

                int existing = this.FindChild(current, this.keys.Slice(segmentStart, segmentLength));
                if (existing >= 0)
                {
                    if (this.nodes[existing].Kind == TreeNodeKind.Object)
                    {
                        current = existing;
                    }
                    else
                    {
                        if (this.options.Strict)
                        {
                            ThrowHelper.ThrowToonException(SR.ToonPathExpansionConflict);
                        }

                        int replacement = this.AddContainerNode(TreeNodeKind.Object);
                        this.SetNodeNameFromExistingKey(replacement, segmentStart, segmentLength);
                        this.ReplaceNode(existing, replacement);
                        current = existing;
                    }
                }
                else
                {
                    int child = this.AddContainerNode(TreeNodeKind.Object);
                    this.SetNodeNameFromExistingKey(child, segmentStart, segmentLength);
                    this.AppendChild(current, child);
                    current = child;
                }

                segmentStart = i + 1;
            }
        }

        private void AddProperty(int objectNode, int valueNode)
        {
            TreeNode value = this.nodes[valueNode];
            int existing = this.FindChild(objectNode, this.keys.Slice(value.NameOffset, value.NameLength));
            if (existing >= 0)
            {
                if (this.options.Strict)
                {
                    ThrowHelper.ThrowToonException(SR.ToonPathExpansionConflict);
                }

                this.ReplaceNode(existing, valueNode);
                return;
            }

            this.AppendChild(objectNode, valueNode);
        }

        private void AppendChild(int parentNode, int childNode)
        {
            TreeNode parent = this.nodes[parentNode];
            if (parent.FirstChild < 0)
            {
                parent.FirstChild = childNode;
            }
            else
            {
                TreeNode last = this.nodes[parent.LastChild];
                last.NextSibling = childNode;
                this.nodes[parent.LastChild] = last;
            }

            parent.LastChild = childNode;
            this.nodes[parentNode] = parent;
        }

        private void ReplaceNode(int targetNode, int replacementNode)
        {
            TreeNode target = this.nodes[targetNode];
            TreeNode replacement = this.nodes[replacementNode];
            replacement.NameOffset = target.NameOffset;
            replacement.NameLength = target.NameLength;
            replacement.NextSibling = target.NextSibling;
            this.nodes[targetNode] = replacement;
        }

        private int FindChild(int objectNode, ReadOnlySpan<byte> name)
        {
            for (int child = this.nodes[objectNode].FirstChild; child >= 0; child = this.nodes[child].NextSibling)
            {
                TreeNode childNode = this.nodes[child];
                if (name.SequenceEqual(this.keys.Slice(childNode.NameOffset, childNode.NameLength)))
                {
                    return child;
                }
            }

            return -1;
        }

        private int AddContainerNode(TreeNodeKind kind)
        {
            int index = this.AddNode();
            this.nodes[index] = new TreeNode
            {
                Kind = kind,
                NameOffset = 0,
                NameLength = 0,
                ValueOffset = 0,
                ValueLength = 0,
                FirstChild = -1,
                LastChild = -1,
                NextSibling = -1,
            };
            return index;
        }

        private int AddRawNode(int valueOffset, int valueLength)
        {
            int index = this.AddNode();
            this.nodes[index] = new TreeNode
            {
                Kind = TreeNodeKind.Raw,
                NameOffset = 0,
                NameLength = 0,
                ValueOffset = valueOffset,
                ValueLength = valueLength,
                FirstChild = -1,
                LastChild = -1,
                NextSibling = -1,
            };
            return index;
        }

        private int AddNode()
        {
            if (this.nodeCount == this.nodes.Length)
            {
                this.GrowNodes();
            }

            return this.nodeCount++;
        }

        private int CopyPropertyName(scoped ref Utf8JsonReader reader, out int length)
        {
            int maxLength = reader.ValueSpan.Length;
            this.EnsureKeyCapacity(maxLength);
            int offset = this.keyLength;
            if (reader.ValueIsEscaped)
            {
                length = reader.CopyString(this.keys.Slice(offset, maxLength));
            }
            else
            {
                reader.ValueSpan.CopyTo(this.keys.Slice(offset, maxLength));
                length = maxLength;
            }

            this.keyLength += length;
            return offset;
        }

        private void SetNodeName(int nodeIndex, int nameOffset, int nameLength)
        {
            TreeNode node = this.nodes[nodeIndex];
            node.NameOffset = nameOffset;
            node.NameLength = nameLength;
            this.nodes[nodeIndex] = node;
        }

        private void SetNodeNameFromExistingKey(int nodeIndex, int nameOffset, int nameLength)
        {
            int copiedOffset = this.CopyExistingKey(nameOffset, nameLength);
            this.SetNodeName(nodeIndex, copiedOffset, nameLength);
        }

        private int CopyExistingKey(int offset, int length)
        {
            this.EnsureKeyCapacity(length);
            int copiedOffset = this.keyLength;
            this.keys.Slice(offset, length).CopyTo(this.keys.Slice(copiedOffset, length));
            this.keyLength += length;
            return copiedOffset;
        }

        private void EnsureKeyCapacity(int additionalLength)
        {
            if (this.keyLength + additionalLength <= this.keys.Length)
            {
                return;
            }

            int newSize = Math.Max(this.keys.Length * 2, this.keyLength + additionalLength);
            byte[] newArray = ArrayPool<byte>.Shared.Rent(newSize);
            this.keys.Slice(0, this.keyLength).CopyTo(newArray);
            if (this.rentedKeys is not null)
            {
                ArrayPool<byte>.Shared.Return(this.rentedKeys);
            }

            this.rentedKeys = newArray;
            this.keys = newArray;
        }

        private void GrowNodes()
        {
            int newSize = this.nodes.Length * 2;
            TreeNode[] newArray = ArrayPool<TreeNode>.Shared.Rent(newSize);
            this.nodes.Slice(0, this.nodeCount).CopyTo(newArray);
            if (this.rentedNodes is not null)
            {
                ArrayPool<TreeNode>.Shared.Return(this.rentedNodes);
            }

            this.rentedNodes = newArray;
            this.nodes = newArray;
        }
    }

    private static bool IsExpandablePath(ReadOnlySpan<byte> key)
    {
        int start = 0;
        bool hasDot = false;
        for (int i = 0; i <= key.Length; i++)
        {
            if (i != key.Length && key[i] != (byte)'.')
            {
                continue;
            }

            if (i == start || !IsIdentifierSegment(key.Slice(start, i - start)))
            {
                return false;
            }

            hasDot |= i < key.Length;
            start = i + 1;
        }

        return hasDot;
    }

    private static bool IsIdentifierSegment(ReadOnlySpan<byte> segment)
    {
        byte first = segment[0];
        if (!IsIdentifierStart(first))
        {
            return false;
        }

        for (int i = 1; i < segment.Length; i++)
        {
            if (!IsIdentifierPart(segment[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsIdentifierStart(byte value)
    {
        return (value >= (byte)'A' && value <= (byte)'Z') ||
            (value >= (byte)'a' && value <= (byte)'z') ||
            value == (byte)'_';
    }

    private static bool IsIdentifierPart(byte value)
    {
        return IsIdentifierStart(value) || (value >= (byte)'0' && value <= (byte)'9');
    }

    private void WriteNode(int indent)
    {
        if (!this.TryPeekNextNonBlank(out ToonLine line))
        {
            this.writer.WriteNullValue();
            return;
        }

        if (line.Indent < indent)
        {
            ThrowHelper.ThrowToonException(SR.UnexpectedIndentation, line.Number, line.Indent + 1);
        }

        if (this.TryParseHeader(line.Content, out Header header) && !header.HasKey)
        {
            this.ConsumeLine(line);
            this.WriteArray(header, line.Indent + this.options.EffectiveIndentSize, line.Number);
            return;
        }

        if (StartsWith(line.Content, "- "u8) || line.Content.SequenceEqual("-"u8))
        {
            this.WriteImplicitArray(line.Indent);
            return;
        }

        if (TryFindColon(line.Content, out _))
        {
            this.WriteObject(line.Indent);
            return;
        }

        if (this.options.Strict && !line.Content.SequenceEqual("[]"u8) && IndexOfUnquoted(line.Content, (byte)'[') >= 0)
        {
            ThrowHelper.ThrowToonException(SR.InvalidToonArrayHeader, line.Number, line.Indent + 1);
        }

        this.ConsumeLine(line);
        this.WriteScalar(line.Content, line.Number, line.Indent + 1);
    }

    private void WriteObject(int indent)
    {
        this.writer.WriteStartObject();
        int[] buckets = ArrayPool<int>.Shared.Rent(Utf8KeyHashSet.StackAllocBucketSize);
        byte[] entries = ArrayPool<byte>.Shared.Rent(Utf8KeyHashSet.StackAllocEntrySize);
        byte[] keyBuffer = ArrayPool<byte>.Shared.Rent(Utf8KeyHashSet.StackAllocKeyBufferSize);
        Utf8KeyHashSet keys = new(16, buckets.AsSpan(0, Utf8KeyHashSet.StackAllocBucketSize), entries.AsSpan(0, Utf8KeyHashSet.StackAllocEntrySize), keyBuffer.AsSpan(0, Utf8KeyHashSet.StackAllocKeyBufferSize));
        try
        {
            while (this.TryPeekNextNonBlank(out ToonLine line))
            {
                if (line.Indent < indent)
                {
                    break;
                }

                if (line.Indent != indent)
                {
                    ThrowHelper.ThrowToonException(SR.UnexpectedIndentationInObject, line.Number, line.Indent + 1);
                }

                if (StartsWith(line.Content, "- "u8) || line.Content.SequenceEqual("-"u8))
                {
                    break;
                }

                this.ConsumeLine(line);

                if (this.TryParseHeader(line.Content, out Header header) && header.HasKey)
                {
                    this.WritePropertyNameAndCheckDuplicate(header.Key, ref keys, line);
                    this.WriteArray(header, line.Indent + this.options.EffectiveIndentSize, line.Number);
                    continue;
                }

                if (!TryFindColon(line.Content, out int colon))
                {
                    ThrowHelper.ThrowToonException(SR.ExpectedObjectMember, line.Number, line.Indent + 1);
                }

                this.WritePropertyNameAndCheckDuplicate(Trim(line.Content.Slice(0, colon)), ref keys, line);

                ReadOnlySpan<byte> value = Trim(line.Content.Slice(colon + 1));
                if (value.Length > 0)
                {
                    if (value.SequenceEqual("[]"u8))
                    {
                        this.writer.WriteStartArray();
                        this.writer.WriteEndArray();
                    }
                    else
                    {
                        this.WriteScalar(value, line.Number, line.Indent + colon + 2);
                    }
                }
                else if (this.TryPeekNextNonBlank(out ToonLine nested) && nested.Indent > line.Indent)
                {
                    if (!this.TryParseHeader(nested.Content, out _) &&
                        !StartsWith(nested.Content, "- "u8) &&
                        !nested.Content.SequenceEqual("-"u8) &&
                        !TryFindColon(nested.Content, out _))
                    {
                        ThrowHelper.ThrowToonException(SR.ExpectedObjectMember, nested.Number, nested.Indent + 1);
                    }

                    this.WriteNode(nested.Indent);
                }
                else
                {
                    this.writer.WriteStartObject();
                    this.writer.WriteEndObject();
                }
            }
        }
        finally
        {
            keys.Dispose();
            ArrayPool<int>.Shared.Return(buckets);
            ArrayPool<byte>.Shared.Return(entries);
            ArrayPool<byte>.Shared.Return(keyBuffer);
        }

        this.writer.WriteEndObject();
    }

    private void WriteArray(Header header, int bodyIndent, int headerLine)
    {
        this.writer.WriteStartArray();
        int count = 0;

        if (header.Tail.Length > 0)
        {
            CellEnumerator cells = new(header.Tail, header.Delimiter);
            while (cells.MoveNext(out ReadOnlySpan<byte> cell))
            {
                this.WriteScalar(cell, headerLine, 1);
                count++;
            }
        }
        else if (header.Fields.Length > 0)
        {
            while (true)
            {
                this.ThrowIfStrictBlankLineBeforeArrayItem(bodyIndent);
                if (!this.TryPeekNextNonBlank(out ToonLine row) || row.Indent != bodyIndent)
                {
                    break;
                }

                this.ConsumeLine(row);
                CellEnumerator fields = new(header.Fields, header.Delimiter);
                CellEnumerator rowCells = new(row.Content, header.Delimiter);
                this.writer.WriteStartObject();
                int cellCount = 0;
                while (rowCells.MoveNext(out ReadOnlySpan<byte> cell))
                {
                    if (!fields.MoveNext(out ReadOnlySpan<byte> field))
                    {
                        ThrowHelper.ThrowToonException(SR.ToonTabularRowWidthMismatch, row.Number, row.Indent + 1);
                    }

                    this.WritePropertyName(field, row.Number, row.Indent + 1);
                    this.WriteScalar(cell, row.Number, row.Indent + 1);
                    cellCount++;
                }

                if (fields.MoveNext(out _) || cellCount == 0)
                {
                    ThrowHelper.ThrowToonException(SR.ToonTabularRowWidthMismatch, row.Number, row.Indent + 1);
                }

                this.writer.WriteEndObject();
                count++;
            }
        }
        else
        {
            count = this.WriteArrayItems(bodyIndent);
        }

        if (this.options.Strict && count != header.Length)
        {
            ThrowHelper.ThrowToonException(SR.Format(SR.ToonArrayHeaderCountMismatch, header.Length, count), headerLine, 1);
        }

        this.writer.WriteEndArray();
    }

    private int WriteArrayItems(int bodyIndent)
    {
        int count = 0;

        while (true)
        {
            this.ThrowIfStrictBlankLineBeforeArrayItem(bodyIndent);
            if (!this.TryPeekNextNonBlank(out ToonLine item) || item.Indent != bodyIndent)
            {
                break;
            }

            this.ConsumeLine(item);
            ReadOnlySpan<byte> content = item.Content;
            if (StartsWith(content, "- "u8))
            {
                content = Trim(content.Slice(2));
            }
            else if (content.SequenceEqual("-"u8))
            {
                content = [];
            }

            if (content.Length == 0)
            {
                if (this.TryPeekNextNonBlank(out ToonLine nested) && nested.Indent > item.Indent)
                {
                    this.WriteNode(nested.Indent);
                }
                else
                {
                    this.writer.WriteStartObject();
                    this.writer.WriteEndObject();
                }
            }
            else if (this.TryParseHeader(content, out Header header))
            {
                if (header.HasKey)
                {
                    this.WriteArrayItemObject(content, item, item.Indent + this.options.EffectiveIndentSize);
                }
                else
                {
                    this.WriteArray(header, item.Indent + this.options.EffectiveIndentSize, item.Number);
                }
            }
            else if (TryFindColon(content, out int colon))
            {
                this.WriteArrayItemObject(content, item, item.Indent + this.options.EffectiveIndentSize);
            }
            else
            {
                this.WriteScalar(content, item.Number, item.Indent + 1);
            }

            count++;
        }

        return count;
    }

    private void WriteArrayItemObject(ReadOnlySpan<byte> firstProperty, ToonLine item, int propertyIndent)
    {
        this.writer.WriteStartObject();
        int[] buckets = ArrayPool<int>.Shared.Rent(Utf8KeyHashSet.StackAllocBucketSize);
        byte[] entries = ArrayPool<byte>.Shared.Rent(Utf8KeyHashSet.StackAllocEntrySize);
        byte[] keyBuffer = ArrayPool<byte>.Shared.Rent(Utf8KeyHashSet.StackAllocKeyBufferSize);
        Utf8KeyHashSet keys = new(16, buckets.AsSpan(0, Utf8KeyHashSet.StackAllocBucketSize), entries.AsSpan(0, Utf8KeyHashSet.StackAllocEntrySize), keyBuffer.AsSpan(0, Utf8KeyHashSet.StackAllocKeyBufferSize));
        try
        {
            this.WriteObjectProperty(firstProperty, item, propertyIndent, ref keys);
            this.WriteObjectContinuation(propertyIndent, ref keys);
        }
        finally
        {
            keys.Dispose();
            ArrayPool<int>.Shared.Return(buckets);
            ArrayPool<byte>.Shared.Return(entries);
            ArrayPool<byte>.Shared.Return(keyBuffer);
        }

        this.writer.WriteEndObject();
    }

    private void WriteObjectContinuation(int propertyIndent, scoped ref Utf8KeyHashSet keys)
    {
        while (this.TryPeekNextNonBlank(out ToonLine line))
        {
            if (line.Indent < propertyIndent)
            {
                break;
            }

            if (line.Indent != propertyIndent)
            {
                ThrowHelper.ThrowToonException(SR.UnexpectedIndentationInObject, line.Number, line.Indent + 1);
            }

            if (StartsWith(line.Content, "- "u8) || line.Content.SequenceEqual("-"u8))
            {
                break;
            }

            this.ConsumeLine(line);
            this.WriteObjectProperty(line.Content, line, propertyIndent, ref keys);
        }
    }

    private void WriteObjectProperty(ReadOnlySpan<byte> content, ToonLine line, int propertyIndent, scoped ref Utf8KeyHashSet keys)
    {
        if (this.TryParseHeader(content, out Header header) && header.HasKey)
        {
            this.WritePropertyNameAndCheckDuplicate(header.Key, ref keys, line);
            this.WriteArray(header, propertyIndent + this.options.EffectiveIndentSize, line.Number);
            return;
        }

        if (!TryFindColon(content, out int colon))
        {
            ThrowHelper.ThrowToonException(SR.ExpectedObjectMember, line.Number, line.Indent + 1);
        }

        this.WritePropertyNameAndCheckDuplicate(Trim(content.Slice(0, colon)), ref keys, line);

        ReadOnlySpan<byte> value = Trim(content.Slice(colon + 1));
        if (value.Length > 0)
        {
            if (value.SequenceEqual("[]"u8))
            {
                this.writer.WriteStartArray();
                this.writer.WriteEndArray();
            }
            else
            {
                this.WriteScalar(value, line.Number, propertyIndent + colon + 2);
            }
        }
        else if (this.TryPeekNextNonBlank(out ToonLine nested) && nested.Indent > propertyIndent)
        {
            if (!this.TryParseHeader(nested.Content, out _) &&
                !StartsWith(nested.Content, "- "u8) &&
                !nested.Content.SequenceEqual("-"u8) &&
                !TryFindColon(nested.Content, out _))
            {
                ThrowHelper.ThrowToonException(SR.ExpectedObjectMember, nested.Number, nested.Indent + 1);
            }

            this.WriteNode(nested.Indent);
        }
        else
        {
            this.writer.WriteStartObject();
            this.writer.WriteEndObject();
        }
    }

    private void WriteImplicitArray(int indent)
    {
        this.writer.WriteStartArray();
        this.WriteArrayItems(indent);
        this.writer.WriteEndArray();
    }

    private void WriteScalar(scoped ReadOnlySpan<byte> value, int line, int column)
    {
        value = Trim(value);

        if (value.Length == 0)
        {
            this.writer.WriteStringValue(ReadOnlySpan<byte>.Empty);
            return;
        }

        if (value.SequenceEqual("[]"u8))
        {
            this.writer.WriteStartArray();
            this.writer.WriteEndArray();
            return;
        }

        if (value.SequenceEqual("{}"u8))
        {
            this.writer.WriteStartObject();
            this.writer.WriteEndObject();
            return;
        }

        if (value[0] == (byte)'"')
        {
            this.WriteJsonStringToken(value, line, column);
            return;
        }

        if (value.SequenceEqual("true"u8))
        {
            this.writer.WriteBooleanValue(true);
            return;
        }

        if (value.SequenceEqual("false"u8))
        {
            this.writer.WriteBooleanValue(false);
            return;
        }

        if (value.SequenceEqual("null"u8))
        {
            this.writer.WriteNullValue();
            return;
        }

        if (IsJsonNumber(value))
        {
            this.WriteCanonicalJsonNumber(value);
            return;
        }

        this.writer.WriteStringValue(value);
    }

    private void WriteCanonicalJsonNumber(scoped ReadOnlySpan<byte> value)
    {
        Span<byte> initialDigits = stackalloc byte[JsonConstants.StackallocByteThreshold];
        Utf8ValueStringBuilder digits = new(initialDigits);
        Span<byte> initialOutput = stackalloc byte[JsonConstants.StackallocByteThreshold];
        Utf8ValueStringBuilder output = new(initialOutput);
        try
        {
            int index = 0;
            bool isNegative = false;
            if (value[index] == (byte)'-')
            {
                isNegative = true;
                index++;
            }

            int integerDigitCount = 0;
            while (index < value.Length && IsDigit(value[index]))
            {
                digits.Append(value[index]);
                integerDigitCount++;
                index++;
            }

            if (index < value.Length && value[index] == (byte)'.')
            {
                index++;
                while (index < value.Length && IsDigit(value[index]))
                {
                    digits.Append(value[index]);
                    index++;
                }
            }

            int exponent = 0;
            if (index < value.Length && (value[index] == (byte)'e' || value[index] == (byte)'E'))
            {
                index++;
                bool exponentIsNegative = false;
                if (value[index] == (byte)'+' || value[index] == (byte)'-')
                {
                    exponentIsNegative = value[index] == (byte)'-';
                    index++;
                }

                while (index < value.Length && IsDigit(value[index]))
                {
                    exponent = checked((exponent * 10) + value[index] - (byte)'0');
                    index++;
                }

                if (exponentIsNegative)
                {
                    exponent = -exponent;
                }
            }

            int decimalPoint = integerDigitCount + exponent;
            int digitStart = 0;
            int digitEnd = digits.Length;
            ReadOnlySpan<byte> digitSpan = digits.AsSpan();
            while (digitStart < digitEnd && digitSpan[digitStart] == (byte)'0')
            {
                digitStart++;
                decimalPoint--;
            }

            if (digitStart == digitEnd)
            {
                this.writer.WriteRawValue("0"u8, skipInputValidation: true);
                return;
            }

            while (digitEnd > digitStart && digitEnd - digitStart > decimalPoint && digitSpan[digitEnd - 1] == (byte)'0')
            {
                digitEnd--;
            }

            if (isNegative)
            {
                output.Append((byte)'-');
            }

            ReadOnlySpan<byte> significantDigits = digitSpan.Slice(digitStart, digitEnd - digitStart);
            if (decimalPoint <= 0)
            {
                output.Append("0."u8);
                for (int i = 0; i < -decimalPoint; i++)
                {
                    output.Append((byte)'0');
                }

                output.Append(significantDigits);
            }
            else if (decimalPoint >= significantDigits.Length)
            {
                output.Append(significantDigits);
                int zeros = decimalPoint - significantDigits.Length;
                for (int i = 0; i < zeros; i++)
                {
                    output.Append((byte)'0');
                }
            }
            else
            {
                output.Append(significantDigits.Slice(0, decimalPoint));
                output.Append((byte)'.');
                output.Append(significantDigits.Slice(decimalPoint));
            }

            this.writer.WriteRawValue(output.AsSpan(), skipInputValidation: true);
        }
        finally
        {
            output.Dispose();
            digits.Dispose();
        }
    }

    private void WriteJsonStringToken(scoped ReadOnlySpan<byte> value, int line, int column)
    {
        try
        {
            Span<byte> initial = stackalloc byte[JsonConstants.StackallocByteThreshold];
            Utf8ValueStringBuilder normalized = new(initial);
            try
            {
                ReadOnlySpan<byte> token = NormalizeQuotedToken(value, ref normalized);
                Utf8JsonReader reader = new(token);
                if (!reader.Read() || reader.TokenType != JsonTokenType.String)
                {
                    ThrowHelper.ThrowToonException(SR.InvalidQuotedToonString, line, column);
                }

                byte[]? rented = null;
                int maxLen = reader.ValueSpan.Length;
                Span<byte> buffer = maxLen <= JsonConstants.StackallocByteThreshold
                    ? stackalloc byte[JsonConstants.StackallocByteThreshold]
                    : (rented = ArrayPool<byte>.Shared.Rent(maxLen));

                try
                {
                    int written = reader.CopyString(buffer);
                    this.writer.WriteStringValue(buffer.Slice(0, written));
                }
                finally
                {
                    if (rented is not null)
                    {
                        ArrayPool<byte>.Shared.Return(rented);
                    }
                }
            }
            finally
            {
                normalized.Dispose();
            }
        }
        catch (JsonException ex)
        {
            ThrowHelper.ThrowToonException(SR.Format(SR.InvalidQuotedToonStringWithMessage, ex.Message), line, column);
        }
        catch (InvalidOperationException ex)
        {
            ThrowHelper.ThrowToonException(SR.Format(SR.InvalidQuotedToonStringWithMessage, ex.Message), line, column);
        }
    }

    private void WritePropertyNameAndCheckDuplicate(scoped ReadOnlySpan<byte> key, scoped ref Utf8KeyHashSet keys, ToonLine line)
    {
        key = Trim(key);
        if (key.Length == 0)
        {
            ThrowHelper.ThrowToonException(SR.ObjectKeysMustNotBeEmpty, line.Number, line.Indent + 1);
        }

        if (key.Length >= 2 && key[0] == (byte)'"' && key[key.Length - 1] == (byte)'"')
        {
            this.WriteQuotedPropertyNameAndCheckDuplicate(key, line.Number, line.Indent + 1, ref keys);
            return;
        }

        this.writer.WritePropertyName(key);
        this.CheckDuplicate(ref keys, key, line.Number, line.Indent + 1);
    }

    private void WritePropertyName(scoped ReadOnlySpan<byte> key, int line, int column)
    {
        key = Trim(key);
        if (key.Length == 0)
        {
            ThrowHelper.ThrowToonException(SR.ObjectKeysMustNotBeEmpty, line, column);
        }

        if (key.Length >= 2 && key[0] == (byte)'"' && key[key.Length - 1] == (byte)'"')
        {
            this.WriteQuotedPropertyName(key, line, column);
            return;
        }

        this.writer.WritePropertyName(key);
    }

    private void WriteQuotedPropertyName(scoped ReadOnlySpan<byte> key, int line, int column)
    {
        try
        {
            Span<byte> initial = stackalloc byte[JsonConstants.StackallocByteThreshold];
            Utf8ValueStringBuilder normalized = new(initial);
            try
            {
                ReadOnlySpan<byte> token = NormalizeQuotedToken(key, ref normalized);
                Utf8JsonReader reader = new(token);
                if (!reader.Read() || reader.TokenType != JsonTokenType.String)
                {
                    ThrowHelper.ThrowToonException(SR.InvalidQuotedToonKey, line, column);
                }

                byte[]? rented = null;
                int maxLen = reader.ValueSpan.Length;
                Span<byte> buffer = maxLen <= JsonConstants.StackallocByteThreshold
                    ? stackalloc byte[JsonConstants.StackallocByteThreshold]
                    : (rented = ArrayPool<byte>.Shared.Rent(maxLen));

                try
                {
                    int written = reader.CopyString(buffer);
                    ReadOnlySpan<byte> unescaped = buffer.Slice(0, written);
                    this.writer.WritePropertyName(unescaped);
                }
                finally
                {
                    if (rented is not null)
                    {
                        ArrayPool<byte>.Shared.Return(rented);
                    }
                }
            }
            finally
            {
                normalized.Dispose();
            }
        }
        catch (JsonException ex)
        {
            ThrowHelper.ThrowToonException(SR.Format(SR.InvalidQuotedToonKeyWithMessage, ex.Message), line, column);
        }
        catch (InvalidOperationException ex)
        {
            ThrowHelper.ThrowToonException(SR.Format(SR.InvalidQuotedToonKeyWithMessage, ex.Message), line, column);
        }
    }

    private void WriteQuotedPropertyNameAndCheckDuplicate(scoped ReadOnlySpan<byte> key, int line, int column, scoped ref Utf8KeyHashSet keys)
    {
        try
        {
            Span<byte> initial = stackalloc byte[JsonConstants.StackallocByteThreshold];
            Utf8ValueStringBuilder normalized = new(initial);
            try
            {
                ReadOnlySpan<byte> token = NormalizeQuotedToken(key, ref normalized);
                Utf8JsonReader reader = new(token);
                if (!reader.Read() || reader.TokenType != JsonTokenType.String)
                {
                    ThrowHelper.ThrowToonException(SR.InvalidQuotedToonKey, line, column);
                }

                byte[]? rented = null;
                int maxLen = reader.ValueSpan.Length;
                Span<byte> buffer = maxLen <= JsonConstants.StackallocByteThreshold
                    ? stackalloc byte[JsonConstants.StackallocByteThreshold]
                    : (rented = ArrayPool<byte>.Shared.Rent(maxLen));

                try
                {
                    int written = reader.CopyString(buffer);
                    ReadOnlySpan<byte> unescaped = buffer.Slice(0, written);
                    this.writer.WritePropertyName(unescaped);
                    this.CheckDuplicate(ref keys, unescaped, line, column);
                }
                finally
                {
                    if (rented is not null)
                    {
                        ArrayPool<byte>.Shared.Return(rented);
                    }
                }
            }
            finally
            {
                normalized.Dispose();
            }
        }
        catch (JsonException ex)
        {
            ThrowHelper.ThrowToonException(SR.Format(SR.InvalidQuotedToonKeyWithMessage, ex.Message), line, column);
        }
        catch (InvalidOperationException ex)
        {
            ThrowHelper.ThrowToonException(SR.Format(SR.InvalidQuotedToonKeyWithMessage, ex.Message), line, column);
        }
    }

    private void CheckDuplicate(scoped ref Utf8KeyHashSet keys, scoped ReadOnlySpan<byte> key, int line, int column)
    {
        if (this.options.Strict && !keys.AddIfNotExists(key))
        {
            ThrowHelper.ThrowToonException(SR.Format(SR.DuplicateToonKey, DecodeToString(key)), line, column);
        }
    }

    private static ReadOnlySpan<byte> NormalizeQuotedToken(ReadOnlySpan<byte> token, ref Utf8ValueStringBuilder builder)
    {
        int tabIndex = token.IndexOf((byte)'\t');
        if (tabIndex < 0)
        {
            return token;
        }

        builder.Append(token.Slice(0, tabIndex));
        for (int i = tabIndex; i < token.Length; i++)
        {
            byte value = token[i];
            if (value == (byte)'\t')
            {
                builder.Append((byte)'\\');
                builder.Append((byte)'t');
            }
            else
            {
                builder.Append(value);
            }
        }

        return builder.AsSpan();
    }

    private bool TryParseHeader(ReadOnlySpan<byte> content, out Header header)
    {
        header = default;
        if (!TryFindColon(content, out int colon))
        {
            return false;
        }

        ReadOnlySpan<byte> head = Trim(content.Slice(0, colon));
        int bracketStart = IndexOfUnquoted(head, (byte)'[');
        if (bracketStart < 0)
        {
            return false;
        }

        int bracketEnd = IndexOfUnquoted(head.Slice(bracketStart + 1), (byte)']');
        if (bracketEnd < 0)
        {
            if (!this.options.Strict)
            {
                return false;
            }

            ThrowHelper.ThrowToonException(SR.InvalidToonArrayHeader, this.lineNumber, 1);
        }

        bracketEnd += bracketStart + 1;
        ReadOnlySpan<byte> key = bracketStart == 0 ? ReadOnlySpan<byte>.Empty : Trim(head.Slice(0, bracketStart));
        ReadOnlySpan<byte> countAndDelimiter = head.Slice(bracketStart + 1, bracketEnd - bracketStart - 1);
        if (!TryParseCountAndDelimiter(countAndDelimiter, out int count, out ToonDelimiter delimiter))
        {
            if (!this.options.Strict)
            {
                return false;
            }

            ThrowHelper.ThrowToonException(SR.InvalidToonArrayCount, this.lineNumber, bracketStart + 2);
        }

        ReadOnlySpan<byte> fields = ReadOnlySpan<byte>.Empty;
        ReadOnlySpan<byte> afterBracketRaw = head.Slice(bracketEnd + 1);
        if (this.options.Strict && afterBracketRaw.Length > 0 && afterBracketRaw[0] == (byte)' ')
        {
            ThrowHelper.ThrowToonException(SR.InvalidToonTabularFieldList, this.lineNumber, bracketEnd + 2);
        }

        ReadOnlySpan<byte> afterBracket = Trim(afterBracketRaw);
        if (afterBracket.Length > 0)
        {
            if (afterBracket[0] != (byte)'{' || afterBracket[afterBracket.Length - 1] != (byte)'}')
            {
                if (!this.options.Strict)
                {
                    return false;
                }

                ThrowHelper.ThrowToonException(SR.InvalidToonTabularFieldList, this.lineNumber, bracketEnd + 2);
            }

            fields = afterBracket.Slice(1, afterBracket.Length - 2);
        }

        header = new Header(bracketStart != 0, key, count, delimiter, fields, Trim(content.Slice(colon + 1)));
        return true;
    }

    private bool TryPeekNextNonBlank(out ToonLine line)
    {
        int pos = this.position;
        int number = this.lineNumber;
        for (; pos <= this.source.Length;)
        {
            int start = pos;
            int end = start;
            while (end < this.source.Length && this.source[end] != (byte)'\n')
            {
                end++;
            }

            int next = end < this.source.Length ? end + 1 : this.source.Length + 1;
            ReadOnlySpan<byte> raw = this.source.Slice(start, end - start);
            if (raw.Length > 0 && raw[raw.Length - 1] == (byte)'\r')
            {
                raw = raw.Slice(0, raw.Length - 1);
            }

            int indent = 0;
            while (indent < raw.Length && raw[indent] == (byte)' ')
            {
                indent++;
            }

            if (indent < raw.Length && raw[indent] == (byte)'\t')
            {
                ThrowHelper.ThrowToonException(SR.TabsNotValidToonIndentation, number, indent + 1);
            }

            ReadOnlySpan<byte> content = TrimEnd(raw.Slice(indent));
            if (content.Length > 0)
            {
                if (this.options.Strict && indent % this.options.EffectiveIndentSize != 0)
                {
                    ThrowHelper.ThrowToonException(SR.ToonIndentationNotMultiple, number, indent + 1);
                }

                line = new ToonLine(number, indent, content, next);
                return true;
            }

            pos = next;
            number++;
        }

        line = default;
        return false;
    }

    private void ConsumeLine(ToonLine line)
    {
        this.position = line.NextIndex;
        this.lineNumber = line.Number + 1;
    }

    private void ThrowIfStrictBlankLineBeforeArrayItem(int bodyIndent)
    {
        if (!this.options.Strict)
        {
            return;
        }

        int pos = this.position;
        int number = this.lineNumber;
        int blankLine = 0;
        for (; pos <= this.source.Length;)
        {
            int start = pos;
            int end = start;
            while (end < this.source.Length && this.source[end] != (byte)'\n')
            {
                end++;
            }

            int next = end < this.source.Length ? end + 1 : this.source.Length + 1;
            ReadOnlySpan<byte> raw = this.source.Slice(start, end - start);
            if (raw.Length > 0 && raw[raw.Length - 1] == (byte)'\r')
            {
                raw = raw.Slice(0, raw.Length - 1);
            }

            int indent = 0;
            while (indent < raw.Length && raw[indent] == (byte)' ')
            {
                indent++;
            }

            if (TrimEnd(raw.Slice(indent)).Length == 0)
            {
                blankLine = blankLine == 0 ? number : blankLine;
                pos = next;
                number++;
                continue;
            }

            if (blankLine != 0 && indent >= bodyIndent)
            {
                ThrowHelper.ThrowToonException(SR.BlankLineInToonArray, blankLine, 1);
            }

            return;
        }
    }

    private static bool TryFindColon(ReadOnlySpan<byte> value, out int index)
    {
        index = IndexOfUnquoted(value, (byte)':');
        return index >= 0;
    }

    private static bool HasUtf8Bom(ReadOnlySpan<byte> value)
    {
        return value.Length >= 3 && value[0] == 0xEF && value[1] == 0xBB && value[2] == 0xBF;
    }

    private static bool StartsWith(ReadOnlySpan<byte> value, ReadOnlySpan<byte> prefix)
    {
        return value.Length >= prefix.Length && value.Slice(0, prefix.Length).SequenceEqual(prefix);
    }

    private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
    {
        int start = 0;
        int end = value.Length - 1;
        while (start < value.Length && IsSpace(value[start]))
        {
            start++;
        }

        while (end >= start && IsSpace(value[end]))
        {
            end--;
        }

        return start > end ? ReadOnlySpan<byte>.Empty : value.Slice(start, end - start + 1);
    }

    private static ReadOnlySpan<byte> TrimEnd(ReadOnlySpan<byte> value)
    {
        int end = value.Length - 1;
        while (end >= 0 && IsSpace(value[end]))
        {
            end--;
        }

        return end < 0 ? ReadOnlySpan<byte>.Empty : value.Slice(0, end + 1);
    }

    private static bool IsSpace(byte value)
    {
        return value is (byte)' ' or (byte)'\t';
    }

    private static bool IsDigit(byte value)
    {
        return value >= (byte)'0' && value <= (byte)'9';
    }

    private static int IndexOfUnquoted(ReadOnlySpan<byte> value, byte target)
    {
        bool inQuotes = false;
        bool escaped = false;
        for (int i = 0; i < value.Length; i++)
        {
            byte b = value[i];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (b == (byte)'\\' && inQuotes)
            {
                escaped = true;
                continue;
            }

            if (b == (byte)'"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && b == target)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool TryParseCountAndDelimiter(ReadOnlySpan<byte> value, out int count, out ToonDelimiter delimiter)
    {
        delimiter = ToonDelimiter.Comma;
        count = 0;

        if (value.Length == 0)
        {
            return false;
        }

        int digitLength = 0;
        while (digitLength < value.Length && value[digitLength] >= (byte)'0' && value[digitLength] <= (byte)'9')
        {
            count = checked((count * 10) + value[digitLength] - (byte)'0');
            digitLength++;
        }

        if (digitLength == 0 || (digitLength > 1 && value[0] == (byte)'0'))
        {
            return false;
        }

        if (digitLength == value.Length)
        {
            return true;
        }

        if (digitLength + 1 != value.Length)
        {
            return false;
        }

        delimiter = value[digitLength] switch
        {
            (byte)',' => ToonDelimiter.Comma,
            (byte)'\t' => ToonDelimiter.Tab,
            (byte)'|' => ToonDelimiter.Pipe,
            _ => delimiter,
        };

        return value[digitLength] is (byte)',' or (byte)'\t' or (byte)'|';
    }

    private static bool IsJsonNumber(ReadOnlySpan<byte> value)
    {
        int index = 0;
        if (value.Length == 0)
        {
            return false;
        }

        if (value[index] == (byte)'-')
        {
            index++;
            if (index == value.Length)
            {
                return false;
            }
        }

        if (value[index] == (byte)'0')
        {
            index++;
            if (index < value.Length && IsDigit(value[index]))
            {
                return false;
            }
        }
        else if (value[index] >= (byte)'1' && value[index] <= (byte)'9')
        {
            do
            {
                index++;
            }
            while (index < value.Length && IsDigit(value[index]));
        }
        else
        {
            return false;
        }

        if (index < value.Length && value[index] == (byte)'.')
        {
            index++;
            if (index == value.Length || !IsDigit(value[index]))
            {
                return false;
            }

            do
            {
                index++;
            }
            while (index < value.Length && IsDigit(value[index]));
        }

        if (index < value.Length && (value[index] == (byte)'e' || value[index] == (byte)'E'))
        {
            index++;
            if (index < value.Length && (value[index] == (byte)'+' || value[index] == (byte)'-'))
            {
                index++;
            }

            if (index == value.Length || !IsDigit(value[index]))
            {
                return false;
            }

            do
            {
                index++;
            }
            while (index < value.Length && IsDigit(value[index]));
        }

        return index == value.Length;
    }

    private static string DecodeToString(ReadOnlySpan<byte> value)
    {
#if NET
        return Encoding.UTF8.GetString(value);
#else
        unsafe
        {
            fixed (byte* pValue = value)
            {
                return Encoding.UTF8.GetString(pValue, value.Length);
            }
        }
#endif
    }

    private ref struct CellEnumerator
    {
        private readonly ReadOnlySpan<byte> value;
        private readonly byte delimiter;
        private int position;
        private bool complete;

        public CellEnumerator(ReadOnlySpan<byte> value, ToonDelimiter delimiter)
        {
            this.value = value;
            this.delimiter = delimiter switch
            {
                ToonDelimiter.Tab => (byte)'\t',
                ToonDelimiter.Pipe => (byte)'|',
                _ => (byte)',',
            };
            this.position = 0;
            this.complete = false;
        }

        public bool MoveNext(out ReadOnlySpan<byte> cell)
        {
            if (this.complete)
            {
                cell = default;
                return false;
            }

            bool inQuotes = false;
            bool escaped = false;
            int start = this.position;
            int end = this.value.Length;
            for (int i = this.position; i < this.value.Length; i++)
            {
                byte b = this.value[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (b == (byte)'\\' && inQuotes)
                {
                    escaped = true;
                    continue;
                }

                if (b == (byte)'"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (!inQuotes && b == this.delimiter)
                {
                    end = i;
                    this.position = i + 1;
                    cell = Trim(this.value.Slice(start, end - start));
                    return true;
                }
            }

            this.complete = true;
            cell = Trim(this.value.Slice(start, end - start));
            return this.value.Length > 0 || start == 0;
        }
    }

    private readonly ref struct ToonLine
    {
        public ToonLine(int number, int indent, ReadOnlySpan<byte> content, int nextIndex)
        {
            this.Number = number;
            this.Indent = indent;
            this.Content = content;
            this.NextIndex = nextIndex;
        }

        public int Number { get; }

        public int Indent { get; }

        public ReadOnlySpan<byte> Content { get; }

        public int NextIndex { get; }
    }

    private readonly ref struct Header
    {
        public Header(bool hasKey, ReadOnlySpan<byte> key, int length, ToonDelimiter delimiter, ReadOnlySpan<byte> fields, ReadOnlySpan<byte> tail)
        {
            this.HasKey = hasKey;
            this.Key = key;
            this.Length = length;
            this.Delimiter = delimiter;
            this.Fields = fields;
            this.Tail = tail;
        }

        public bool HasKey { get; }

        public ReadOnlySpan<byte> Key { get; }

        public int Length { get; }

        public ToonDelimiter Delimiter { get; }

        public ReadOnlySpan<byte> Fields { get; }

        public ReadOnlySpan<byte> Tail { get; }
    }
}