// <copyright file="YamlToJsonConverter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Runtime.CompilerServices;
#if STJ
using Corvus.Yaml.Internal;
#else
using Corvus.Text.Json.Internal;
#endif

#if STJ
namespace Corvus.Yaml.Internal;
#else
namespace Corvus.Text.Json.Yaml.Internal;
#endif

/// <summary>
/// A combined YAML scanner and JSON emitter that converts UTF-8 YAML bytes
/// to JSON by writing directly to a <see cref="Utf8JsonWriter"/> in one pass.
/// </summary>
/// <remarks>
/// <para>
/// This is a <see langword="ref struct"/> that operates on <see cref="ReadOnlySpan{T}"/>
/// of UTF-8 bytes. It uses recursive descent to parse YAML structures and emit JSON tokens.
/// </para>
/// <para>
/// The converter handles: block mappings, block sequences, flow mappings, flow sequences,
/// all scalar styles (plain, double-quoted, single-quoted, literal block, folded block),
/// anchors, aliases, tags, document markers, directives, and comments.
/// </para>
/// </remarks>
internal ref struct YamlToJsonConverter
{
    /// <summary>
    /// The resolved tag kind for a YAML node.
    /// </summary>
    private enum YamlTagKind : byte
    {
        /// <summary>No tag present.</summary>
        None,

        /// <summary>Non-specific tag '!' — forces string for plain scalars.</summary>
        NonSpecific,

        /// <summary>!!str or !&lt;tag:yaml.org,2002:str&gt;.</summary>
        Str,

        /// <summary>!!int or !&lt;tag:yaml.org,2002:int&gt;.</summary>
        Int,

        /// <summary>!!float or !&lt;tag:yaml.org,2002:float&gt;.</summary>
        Float,

        /// <summary>!!bool or !&lt;tag:yaml.org,2002:bool&gt;.</summary>
        Bool,

        /// <summary>!!null or !&lt;tag:yaml.org,2002:null&gt;.</summary>
        Null,

        /// <summary>!!seq or !&lt;tag:yaml.org,2002:seq&gt;.</summary>
        Seq,

        /// <summary>!!map or !&lt;tag:yaml.org,2002:map&gt;.</summary>
        Map,

        /// <summary>Any other tag (custom/local) — ignored for JSON.</summary>
        Other,
    }

    private readonly ReadOnlySpan<byte> _buffer;
    private Utf8JsonWriter _writer;
    private readonly YamlReaderOptions _options;
#if STJ
    private readonly ArrayPoolBufferWriter? _bufferWriter;
#else
    private readonly IByteBufferWriter? _bufferWriter;
#endif
    private int _pos;
    private int _line;
    private int _column;
    private int _depth;

    // Anchor table: stores anchor names and their JSON byte positions in the output buffer.
    private byte[]? _anchorNameBuffer;
    private int _anchorNamesUsed;
    private byte[]? _anchorDataBuffer;
    private int _anchorDataUsed;
    private int _anchorCount;

    // Each anchor entry is 4 ints: (nameOffset, nameLength, dataOffset, dataLength) = 16 bytes
    // Stored in _anchorEntries, max 64 inline
    private const int AnchorEntrySize = 4;
    private int[]? _anchorEntries;

    // Active anchor tracking
    private int _activeAnchorNameStart;
    private int _activeAnchorNameLength;

    // TAG directive tracking: when %TAG !! is redefined to a non-standard prefix,
    // !!name tags should not be classified as standard YAML types.
    private bool _secondaryTagRedefined;

    /// <summary>
    /// Initializes a new converter for the given YAML buffer.
    /// </summary>
    /// <param name="yaml">The UTF-8 YAML bytes.</param>
    /// <param name="writer">The JSON writer to emit to.</param>
    /// <param name="options">The YAML reader options.</param>
    /// <param name="bufferWriter">Optional buffer writer for anchor/alias support.</param>
#if STJ
    public YamlToJsonConverter(ReadOnlySpan<byte> yaml, Utf8JsonWriter writer, YamlReaderOptions options, ArrayPoolBufferWriter? bufferWriter = null)
#else
    public YamlToJsonConverter(ReadOnlySpan<byte> yaml, Utf8JsonWriter writer, YamlReaderOptions options, IByteBufferWriter? bufferWriter = null)
#endif
    {
        _buffer = yaml;
        _writer = writer;
        _options = options;
        _bufferWriter = bufferWriter;
        _pos = 0;
        _line = 1;
        _column = 0;
        _depth = 0;
        _anchorNameBuffer = null;
        _anchorNamesUsed = 0;
        _anchorDataBuffer = null;
        _anchorDataUsed = 0;
        _anchorCount = 0;
        _anchorEntries = null;
        _activeAnchorNameStart = -1;
        _activeAnchorNameLength = 0;
        _secondaryTagRedefined = false;
    }

    /// <summary>
    /// Converts the YAML content to JSON, writing the result to the writer.
    /// </summary>
    public void Convert()
    {
        try
        {
            SkipBom();

            if (_options.DocumentMode == YamlDocumentMode.MultiAsArray)
            {
                ConvertMultiDocumentStream();
            }
            else
            {
                ConvertSingleDocument();
            }
        }
        finally
        {
            ReturnAnchorBuffers();
        }
    }

    /// <summary>
    /// Converts a YAML stream that may contain multiple documents, wrapping them in a JSON array.
    /// </summary>
    private void ConvertMultiDocumentStream()
    {
        _writer.WriteStartArray();

        bool wroteAnyDocument = false;

        while (true)
        {
            SkipWhitespaceAndComments();
            SkipDirectives();
            SkipWhitespaceAndComments();

            bool hasDocStart = SkipDocumentStartMarker();
            SkipWhitespaceAndComments();

            if (_pos >= _buffer.Length)
            {
                // If we got an explicit --- with nothing after, that's an empty document
                if (hasDocStart)
                {
                    _writer.WriteNullValue();
                }
                else if (!wroteAnyDocument)
                {
                    // Bare empty stream — single null document
                    _writer.WriteNullValue();
                }

                break;
            }

            // Check for document end/start marker immediately (empty document)
            if (IsDocumentEndOrStart())
            {
                if (hasDocStart)
                {
                    _writer.WriteNullValue();
                    wroteAnyDocument = true;
                }

                // Could be ... or another --- — consume document end if present
                if (IsDocumentEnd())
                {
                    SkipDocumentEndMarker();
                    SkipWhitespaceAndComments();
                    continue;
                }

                // Another --- means another document
                if (IsDocumentStart())
                {
                    continue;
                }

                break;
            }

            // There is content — parse a document
            // Reset anchor table for each document
            ResetAnchorTable();
            ConvertNode(-1, false);
            wroteAnyDocument = true;
            SkipWhitespaceAndComments();

            // Consume document end marker if present
            SkipDocumentEndMarker();
            SkipWhitespaceAndComments();
        }

        _writer.WriteEndArray();
    }

    /// <summary>
    /// Converts a single YAML document, throwing on multi-document content.
    /// </summary>
    private void ConvertSingleDocument()
    {
        // Skip any leading whitespace, comments, directives, and document markers
        SkipWhitespaceAndComments();
        SkipDirectives();
        SkipWhitespaceAndComments();
        bool hasDocStart = SkipDocumentStartMarker();
        SkipWhitespaceAndComments();

        if (_pos >= _buffer.Length)
        {
            // Empty document — write null
            _writer.WriteNullValue();
            return;
        }

        // Check if only document-end markers or comments remain
        if (IsDocumentEndOrStart())
        {
            _writer.WriteNullValue();

            // Consume the marker and check for more docs
            if (IsDocumentEnd())
            {
                SkipDocumentEndMarker();
                SkipWhitespaceAndComments();
            }

            CheckForSecondDocument();
            return;
        }

        // Parse the single document value
        ConvertNode(-1, false);
        SkipWhitespaceAndComments();

        // Handle document end marker
        SkipDocumentEndMarker();
        SkipWhitespaceAndComments();

        CheckForSecondDocument();
    }

    /// <summary>
    /// Checks if there is a second document and throws if so.
    /// </summary>
    private void CheckForSecondDocument()
    {
        if (_pos >= _buffer.Length)
        {
            return;
        }

        // Skip any trailing directives
        SkipDirectives();
        SkipWhitespaceAndComments();

        if (_pos >= _buffer.Length)
        {
            return;
        }

        // Check for another document start
        if (SkipDocumentStartMarker())
        {
            SkipWhitespaceAndComments();

            // If there's actual content or another marker after ---, it's multi-doc
            if (_pos < _buffer.Length)
            {
                ThrowMultipleDocuments();
            }
        }
    }

    /// <summary>
    /// Converts a single YAML node starting at the current position.
    /// </summary>
    /// <param name="parentIndent">The indentation level of the parent block context, or -1 for root.</param>
    /// <param name="inFlow">Whether we are inside a flow context (flow mapping or flow sequence).</param>
    private void ConvertNode(int parentIndent, bool inFlow)
    {
        SkipWhitespaceAndComments();

        if (_pos >= _buffer.Length)
        {
            _writer.WriteNullValue();
            return;
        }

        // Save the column and line before parsing node properties (anchor/tag),
        // because tags shift the position but the indent is anchored at the start.
        int nodeColumn = _column;
        int nodeLine = _line;

        // Parse node properties (anchor and tag) in either order
        YamlTagKind tag = YamlTagKind.None;
        bool hasAnchor = false;
        ParseNodeProperties(ref tag, ref hasAnchor);

        // Capture writer position before value for anchor storage
        long anchorStartPos = 0;
        if (hasAnchor && _bufferWriter != null)
        {
            _writer.Flush();
            anchorStartPos = _bufferWriter.WrittenSpan.Length;
        }

        if (_pos >= _buffer.Length)
        {
            WriteEmptyNodeWithTag(tag);
            FinishAnchor(hasAnchor, anchorStartPos);
            return;
        }

        // If properties moved us to a new line, check that the following content is
        // actually a child of this node and not a sibling at the same indent.
        // An anchor/tag at end-of-line with no value means the node is empty.
        // Exception: block sequences at the same indent as the parent mapping are
        // valid values (YAML spec allows "key:\n- item").
        if (!inFlow && (hasAnchor || tag != YamlTagKind.None)
            && _line > nodeLine && parentIndent >= 0 && _column <= parentIndent
            && !(_column == parentIndent
                 && _pos < _buffer.Length
                 && _buffer[_pos] == YamlConstants.Dash
                 && IsBlockSequenceEntry()))
        {
            WriteEmptyNodeWithTag(tag);
            FinishAnchor(hasAnchor, anchorStartPos);
            return;
        }

        byte c = _buffer[_pos];

        // If properties moved us to a different line, the content column determines
        // the node indent, not the property column (e.g., "--- !<tag>\ncontent"
        // has the tag at column 4 but content at column 0).
        if (_line > nodeLine)
        {
            nodeColumn = _column;
        }

        // Check for empty node in flow context (e.g., !!str followed by , or } or ])
        if (inFlow && IsFlowEmptyNode(c))
        {
            WriteEmptyNodeWithTag(tag);
            FinishAnchor(hasAnchor, anchorStartPos);
            return;
        }

        // Flow collections
        if (c == YamlConstants.OpenBrace)
        {
            ConvertFlowMapping();
            FinishAnchor(hasAnchor, anchorStartPos);
            return;
        }

        if (c == YamlConstants.OpenBracket)
        {
            ConvertFlowSequence();
            FinishAnchor(hasAnchor, anchorStartPos);
            return;
        }

        // Block scalar indicators
        if (c == YamlConstants.Pipe || c == YamlConstants.GreaterThan)
        {
            ConvertBlockScalar(parentIndent);
            FinishAnchor(hasAnchor, anchorStartPos);
            return;
        }

        // Alias — standalone value or mapping key
        if (c == YamlConstants.Asterisk)
        {
            // In block context, check if the alias is used as a mapping key
            // (e.g., "*alias : value" → {aliasValue: "value"})
            if (!inFlow && IsAliasMappingKey())
            {
                // Fall through to block mapping handling below
            }
            else
            {
                ConvertAlias();
                FinishAnchor(hasAnchor, anchorStartPos);
                return;
            }
        }

        // Block sequence indicator
        if (c == YamlConstants.Dash && IsBlockSequenceEntry())
        {
            ConvertBlockSequence(parentIndent);
            FinishAnchor(hasAnchor, anchorStartPos);
            return;
        }

        // Check for block mapping by looking ahead for a mapping value indicator.
        // This must be checked BEFORE quoted scalars because a quoted string followed
        // by ': ' is a mapping key, not a standalone value.
        if (IsBlockMappingStart(inFlow))
        {
            // In YAML, node properties on the same line as a block mapping's first key
            // belong to that key, not to the mapping itself.
            // E.g., "&a key: value" anchors the key "key", not the mapping {key: value}.
            // Properties on a different line anchor the mapping:
            // "&a\n  key: value" anchors the mapping.
            if ((hasAnchor || tag != YamlTagKind.None) && _line == nodeLine)
            {
                ConvertBlockMapping(parentIndent, nodeColumn, hasAnchor, tag);

                // Anchor was consumed by StoreKeyAnchor inside the mapping
                hasAnchor = false;
            }
            else
            {
                ConvertBlockMapping(parentIndent, nodeColumn);
            }

            FinishAnchor(hasAnchor, anchorStartPos);
            return;
        }

        // Quoted scalars (only reached when NOT a mapping key)
        if (c == YamlConstants.DoubleQuote)
        {
            ConvertDoubleQuotedScalar(parentIndent, inFlow, asValue: true);
            FinishAnchor(hasAnchor, anchorStartPos);
            return;
        }

        if (c == YamlConstants.SingleQuote)
        {
            ConvertSingleQuotedScalar(asValue: true);
            FinishAnchor(hasAnchor, anchorStartPos);
            return;
        }

        // Plain scalar
        ConvertPlainScalar(parentIndent, inFlow, tag);
        FinishAnchor(hasAnchor, anchorStartPos);
    }

    /// <summary>
    /// Parses node properties (anchor and tag) in any order.
    /// YAML allows both <c>&amp;anchor !!tag</c> and <c>!!tag &amp;anchor</c>.
    /// </summary>
    private void ParseNodeProperties(ref YamlTagKind tag, ref bool hasAnchor)
    {
        while (_pos < _buffer.Length)
        {
            byte c = _buffer[_pos];

            if (c == YamlConstants.Ampersand)
            {
                ReadAnchorName();
                hasAnchor = true;
                SkipWhitespaceAndComments();
                continue;
            }

            if (c == YamlConstants.Exclamation)
            {
                tag = ReadTag();
                SkipWhitespaceAndComments();
                continue;
            }

            break;
        }
    }

    /// <summary>
    /// Writes the appropriate value for an empty node based on its tag.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteEmptyNodeWithTag(YamlTagKind tag)
    {
        if (tag == YamlTagKind.Str)
        {
            _writer.WriteStringValue(ReadOnlySpan<byte>.Empty);
        }
        else
        {
            _writer.WriteNullValue();
        }
    }

    /// <summary>
    /// Checks if the current character indicates an empty node in flow context.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsFlowEmptyNode(byte c)
    {
        return c == YamlConstants.Comma
            || c == YamlConstants.CloseBrace
            || c == YamlConstants.CloseBracket;
    }

    /// <summary>
    /// Checks whether the current position is an alias (*name) followed by
    /// a block mapping value indicator (": "), meaning the alias is a key.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsAliasMappingKey()
    {
        int p = _pos + 1; // skip '*'

        // Skip alias name chars
        while (p < _buffer.Length
            && !YamlCharacters.IsWhitespaceOrLineBreak(_buffer[p])
            && !YamlCharacters.IsFlowIndicator(_buffer[p]))
        {
            p++;
        }

        // Skip spaces (not newlines)
        while (p < _buffer.Length && _buffer[p] == YamlConstants.Space)
        {
            p++;
        }

        // Check for ": " (colon followed by whitespace/end)
        if (p < _buffer.Length && _buffer[p] == YamlConstants.Colon)
        {
            int next = p + 1;
            return next >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[next]);
        }

        return false;
    }

    /// <summary>
    /// Checks whether the current position starts a block mapping.
    /// A block mapping is detected when the current line has a key followed by ": ".
    /// </summary>
    private bool IsBlockMappingStart(bool inFlow)
    {
        if (inFlow || _pos >= _buffer.Length)
        {
            return false;
        }

        // Save position to restore after look-ahead
        int savedPos = _pos;
        int savedLine = _line;
        int savedColumn = _column;

        bool result = ScanForMappingValueIndicator();

        // Restore position
        _pos = savedPos;
        _line = savedLine;
        _column = savedColumn;

        return result;
    }

    /// <summary>
    /// Scans the current line to find a mapping value indicator (": ").
    /// Handles skipping over quoted strings in the key.
    /// </summary>
    private bool ScanForMappingValueIndicator()
    {
        byte c = _buffer[_pos];

        // Explicit key indicator
        if (c == YamlConstants.QuestionMark)
        {
            int next = _pos + 1;
            if (next >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[next]))
            {
                return true;
            }
        }

        // If key starts with a quote, skip the quoted string then look for ': '
        if (c == YamlConstants.DoubleQuote)
        {
            _pos++;
            while (_pos < _buffer.Length && _buffer[_pos] != YamlConstants.DoubleQuote)
            {
                if (_buffer[_pos] == YamlConstants.Backslash)
                {
                    _pos++;
                }

                _pos++;
            }

            if (_pos < _buffer.Length)
            {
                _pos++;
            }

            // Look for ': ' after the quoted key
            while (_pos < _buffer.Length && !YamlCharacters.IsLineBreak(_buffer[_pos]))
            {
                if (_buffer[_pos] == YamlConstants.Colon)
                {
                    int next = _pos + 1;
                    if (next >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[next]))
                    {
                        return true;
                    }
                }

                _pos++;
            }

            return false;
        }

        if (c == YamlConstants.SingleQuote)
        {
            _pos++;
            while (_pos < _buffer.Length)
            {
                if (_buffer[_pos] == YamlConstants.SingleQuote)
                {
                    _pos++;

                    // '' is an escape, not the closing quote
                    if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.SingleQuote)
                    {
                        _pos++;
                        continue;
                    }

                    break;
                }

                _pos++;
            }

            // Look for ': ' after the quoted key
            while (_pos < _buffer.Length && !YamlCharacters.IsLineBreak(_buffer[_pos]))
            {
                if (_buffer[_pos] == YamlConstants.Colon)
                {
                    int next = _pos + 1;
                    if (next >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[next]))
                    {
                        return true;
                    }
                }

                _pos++;
            }

            return false;
        }

        // Plain scalar key — scan for ': ' without entering quote-skip mode
        // (quotes mid-key are valid plain scalar characters)
        while (_pos < _buffer.Length)
        {
            c = _buffer[_pos];

            if (YamlCharacters.IsLineBreak(c))
            {
                return false;
            }

            if (c == YamlConstants.Hash && _pos > 0 && YamlCharacters.IsWhitespace(_buffer[_pos - 1]))
            {
                return false;
            }

            if (c == YamlConstants.Colon)
            {
                int next = _pos + 1;
                if (next >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[next]))
                {
                    return true;
                }
            }

            _pos++;
        }

        return false;
    }

    /// <summary>
    /// Converts a block mapping to a JSON object.
    /// </summary>
    /// <param name="parentIndent">The indentation level of the parent node.</param>
    /// <param name="mappingIndent">The indentation level of this mapping.</param>
    /// <param name="outerKeyHasAnchor">
    /// True if the parent ConvertNode read an anchor that belongs to the first key
    /// (properties on the same line as the first key in YAML belong to the key, not the mapping).
    /// </param>
    /// <param name="outerKeyTag">
    /// Tag read by the parent ConvertNode for the first key (same-line rule).
    /// </param>
    private void ConvertBlockMapping(
        int parentIndent,
        int mappingIndent,
        bool outerKeyHasAnchor = false,
        YamlTagKind outerKeyTag = YamlTagKind.None)
    {
        EnterDepth();

        _writer.WriteStartObject();

        Span<int> bucketBuffer = stackalloc int[Utf8KeyHashSet.StackAllocBucketSize];
        Span<byte> entryBuffer = stackalloc byte[Utf8KeyHashSet.StackAllocEntrySize];
        Span<byte> keyByteBuffer = stackalloc byte[Utf8KeyHashSet.StackAllocKeyBufferSize];
        Utf8KeyHashSet keySet = new(8, bucketBuffer, entryBuffer, keyByteBuffer);
        try
        {
            bool first = true;
            while (_pos < _buffer.Length)
            {
                SkipWhitespaceAndComments();
                if (_pos >= _buffer.Length)
                {
                    break;
                }

                // Check indentation
                if (_column <= parentIndent && !first)
                {
                    break;
                }

                if (!first && _column != mappingIndent)
                {
                    if (_column < mappingIndent)
                    {
                        break;
                    }
                }

                // Check for document markers
                if (IsDocumentEndOrStart())
                {
                    break;
                }

                // Skip anchor/tag on key (in any order).
                // On the first iteration, merge any properties from the parent ConvertNode
                // (these are same-line properties that belong to the first key).
                YamlTagKind keyTag = first ? outerKeyTag : YamlTagKind.None;
                bool keyHasAnchor = first && outerKeyHasAnchor;
                ParseNodeProperties(ref keyTag, ref keyHasAnchor);

                if (_pos >= _buffer.Length)
                {
                    break;
                }

                // Handle explicit key indicator (?)
                bool explicitKey = false;
                if (_buffer[_pos] == YamlConstants.QuestionMark)
                {
                    int next = _pos + 1;
                    if (next >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[next]))
                    {
                        explicitKey = true;
                        Advance(1);
                        SkipWhitespaceAndComments();

                        // Parse properties that follow the explicit key indicator
                        // (e.g., "? !!str a" has the tag after the ?)
                        if (_pos < _buffer.Length)
                        {
                            ParseNodeProperties(ref keyTag, ref keyHasAnchor);
                        }
                    }
                }

                // Read the key — handle alias keys (*alias) specially
                if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Asterisk)
                {
                    // Alias as key
                    WriteAliasAsPropertyName();

                    // If the key also has an anchor, clear it to avoid dangling state.
                    if (keyHasAnchor)
                    {
                        _activeAnchorNameStart = -1;
                    }
                }
                else
                {
                    ReadOnlySpan<byte> key;
                    byte[]? rentedKeyBuffer = null;
                    if (explicitKey)
                    {
                        key = ReadExplicitKeyContent(mappingIndent, out rentedKeyBuffer);
                    }
                    else
                    {
                        key = ReadMappingKey(out rentedKeyBuffer);
                    }

                    try
                    {
                        // Duplicate key check
                        if (_options.DuplicateKeyBehavior == DuplicateKeyBehavior.Error)
                        {
                            if (!keySet.AddIfNotExists(key))
                            {
                                ThrowDuplicateKey();
                            }
                        }

                        // Write the property name (key is always a string in JSON)
                        _writer.WritePropertyName(key);

                        // If the key had an anchor, store it as a JSON string value
                        if (keyHasAnchor)
                        {
                            StoreKeyAnchor(key);
                        }
                    }
                    finally
                    {
                        if (rentedKeyBuffer != null)
                        {
                            ArrayPool<byte>.Shared.Return(rentedKeyBuffer);
                        }
                    }
                }

                // For explicit keys, ': ' can be on a new line (after comments)
                if (explicitKey)
                {
                    SkipWhitespaceAndComments();
                }
                else
                {
                    SkipWhitespaceOnly();
                }

                if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Colon)
                {
                    int next = _pos + 1;
                    bool isValueIndicator = next >= _buffer.Length
                        || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[next]);

                    if (isValueIndicator)
                    {
                        Advance(1);
                        SkipWhitespaceOnly();

                        // Check for value on the same line vs next line
                        if (_pos >= _buffer.Length || YamlCharacters.IsLineBreak(_buffer[_pos]) || IsComment())
                        {
                            // Value is on the next line (block value)
                            SkipWhitespaceAndComments();

                            // YAML spec: a block sequence at the same indent as a mapping key
                            // can be the mapping value
                            bool hasValue = _pos < _buffer.Length && !IsDocumentEndOrStart() &&
                                (_column > mappingIndent ||
                                 (_column == mappingIndent
                                  && _buffer[_pos] == YamlConstants.Dash && IsBlockSequenceEntry()));

                            if (hasValue)
                            {
                                ConvertNode(mappingIndent, false);
                            }
                            else
                            {
                                // Empty value → null
                                _writer.WriteNullValue();
                            }
                        }
                        else
                        {
                            // Value on same line
                            ConvertNode(mappingIndent, false);
                        }
                    }
                    else
                    {
                        // Colon not followed by whitespace — not a value indicator.
                        // For implicit keys this is an error; for explicit keys, null value.
                        if (explicitKey)
                        {
                            _writer.WriteNullValue();
                        }
                        else
                        {
                            ThrowExpectedMappingValue();
                        }
                    }
                }
                else if (explicitKey)
                {
                    // After an explicit key with no ':', write null
                    _writer.WriteNullValue();
                }
                else
                {
                    // No colon found after key — error
                    ThrowExpectedMappingValue();
                }

                first = false;
                SkipWhitespaceAndComments();
            }

            _writer.WriteEndObject();
        }
        finally
        {
            keySet.Dispose();
        }

        _depth--;
    }

    /// <summary>
    /// Reads a mapping key and returns the UTF-8 bytes.
    /// </summary>
    private ReadOnlySpan<byte> ReadMappingKey(out byte[]? rentedKeyBuffer)
    {
        rentedKeyBuffer = null;
        if (_pos >= _buffer.Length)
        {
            ThrowUnexpectedEnd();
        }

        byte c = _buffer[_pos];

        // Double-quoted key
        if (c == YamlConstants.DoubleQuote)
        {
            return ReadDoubleQuotedScalar(out rentedKeyBuffer);
        }

        // Single-quoted key
        if (c == YamlConstants.SingleQuote)
        {
            return ReadSingleQuotedScalar(out rentedKeyBuffer);
        }

        // Plain scalar key — read until ': ' or line break
        return ReadPlainScalarKey();
    }

    /// <summary>
    /// Reads an explicit key's content (after the '?' indicator).
    /// Supports block scalars, quoted scalars, and multi-line plain scalars.
    /// </summary>
    private ReadOnlySpan<byte> ReadExplicitKeyContent(int mappingIndent, out byte[]? rentedKeyBuffer)
    {
        rentedKeyBuffer = null;

        if (_pos >= _buffer.Length || YamlCharacters.IsLineBreak(_buffer[_pos]))
        {
            // Empty explicit key → empty string
            return ReadOnlySpan<byte>.Empty;
        }

        byte c = _buffer[_pos];

        // Block scalar key (| or >)
        if (c == YamlConstants.Pipe || c == YamlConstants.GreaterThan)
        {
            return ReadBlockScalarContent(mappingIndent, out rentedKeyBuffer);
        }

        // Double-quoted key
        if (c == YamlConstants.DoubleQuote)
        {
            return ReadDoubleQuotedScalar(out rentedKeyBuffer);
        }

        // Single-quoted key
        if (c == YamlConstants.SingleQuote)
        {
            return ReadSingleQuotedScalar(out rentedKeyBuffer);
        }

        // Plain scalar key — may be multi-line
        return ReadExplicitPlainKey(mappingIndent, out rentedKeyBuffer);
    }

    /// <summary>
    /// Reads a multi-line plain scalar key for an explicit key entry.
    /// The key terminates at a line starting with ':' or '?' at the mapping indent,
    /// or de-indent below the key content.
    /// </summary>
    private ReadOnlySpan<byte> ReadExplicitPlainKey(int mappingIndent, out byte[]? rentedKeyBuffer)
    {
        rentedKeyBuffer = null;
        int start = _pos;
        int end = _pos;

        // Read first line
        ReadPlainScalarLine(start, ref end, false);

        // Trim trailing whitespace
        while (end > start && YamlCharacters.IsWhitespace(_buffer[end - 1]))
        {
            end--;
        }

        // If not at a line break, return the single line
        if (_pos >= _buffer.Length || !YamlCharacters.IsLineBreak(_buffer[_pos]))
        {
            return _buffer.Slice(start, end - start);
        }

        // Check for continuation: next line must be indented past the mapping indent
        // and must not start with '?' or ':' at the mapping indent
        int savedPos = _pos;
        int savedLine = _line;
        int savedColumn = _column;

        SkipLineBreak();

        // Skip blank lines
        int blankLineCount = 0;
        while (_pos < _buffer.Length)
        {
            int lineStartPos = _pos;

            while (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Space)
            {
                _pos++;
                _column++;
            }

            if (_pos >= _buffer.Length)
            {
                _pos = savedPos;
                _line = savedLine;
                _column = savedColumn;
                return _buffer.Slice(start, end - start);
            }

            if (YamlCharacters.IsLineBreak(_buffer[_pos]))
            {
                blankLineCount++;
                SkipLineBreak();
                continue;
            }

            int indent = _pos - lineStartPos;

            // At or below mapping indent: check for ':' or '?' (explicit value/key indicator)
            if (indent <= mappingIndent)
            {
                _pos = savedPos;
                _line = savedLine;
                _column = savedColumn;
                return _buffer.Slice(start, end - start);
            }

            // Check for comment
            if (_buffer[_pos] == YamlConstants.Hash)
            {
                _pos = savedPos;
                _line = savedLine;
                _column = savedColumn;
                return _buffer.Slice(start, end - start);
            }

            if (IsDocumentEndOrStart())
            {
                _pos = savedPos;
                _line = savedLine;
                _column = savedColumn;
                return _buffer.Slice(start, end - start);
            }

            // Found continuation content — need to build multi-line result
            break;
        }

        if (_pos >= _buffer.Length)
        {
            _pos = savedPos;
            _line = savedLine;
            _column = savedColumn;
            return _buffer.Slice(start, end - start);
        }

        // Multi-line: build folded key in rented buffer
        int estimatedLength = Math.Max(end - start + 128, 256);
        rentedKeyBuffer = ArrayPool<byte>.Shared.Rent(estimatedLength);
        Span<byte> output = rentedKeyBuffer;
        int written = 0;

        // Copy first line
        ReadOnlySpan<byte> firstLine = _buffer.Slice(start, end - start);
        EnsureRentedCapacity(ref rentedKeyBuffer, ref output, written, firstLine.Length);
        firstLine.CopyTo(output.Slice(written));
        written += firstLine.Length;

        // Process continuation lines
        while (true)
        {
            // Write fold separator
            EnsureRentedCapacity(ref rentedKeyBuffer, ref output, written, blankLineCount + 1);
            if (blankLineCount > 0)
            {
                for (int i = 0; i < blankLineCount; i++)
                {
                    output[written++] = YamlConstants.LineFeed;
                }
            }
            else
            {
                output[written++] = YamlConstants.Space;
            }

            // Read this continuation line
            int lineStart = _pos;
            int lineEnd = _pos;
            ReadPlainScalarLine(lineStart, ref lineEnd, false);

            // Trim trailing whitespace
            while (lineEnd > lineStart && YamlCharacters.IsWhitespace(_buffer[lineEnd - 1]))
            {
                lineEnd--;
            }

            ReadOnlySpan<byte> lineContent = _buffer.Slice(lineStart, lineEnd - lineStart);
            EnsureRentedCapacity(ref rentedKeyBuffer, ref output, written, lineContent.Length);
            lineContent.CopyTo(output.Slice(written));
            written += lineContent.Length;

            // Check for another continuation line
            if (_pos >= _buffer.Length || !YamlCharacters.IsLineBreak(_buffer[_pos]))
            {
                break;
            }

            savedPos = _pos;
            savedLine = _line;
            savedColumn = _column;

            SkipLineBreak();

            blankLineCount = 0;
            bool foundContinuation = false;
            while (_pos < _buffer.Length)
            {
                int lineStartPos2 = _pos;

                while (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Space)
                {
                    _pos++;
                    _column++;
                }

                if (_pos >= _buffer.Length)
                {
                    break;
                }

                if (YamlCharacters.IsLineBreak(_buffer[_pos]))
                {
                    blankLineCount++;
                    SkipLineBreak();
                    continue;
                }

                int indent2 = _pos - lineStartPos2;

                if (indent2 <= mappingIndent || _buffer[_pos] == YamlConstants.Hash || IsDocumentEndOrStart())
                {
                    break;
                }

                foundContinuation = true;
                break;
            }

            if (!foundContinuation)
            {
                _pos = savedPos;
                _line = savedLine;
                _column = savedColumn;
                break;
            }
        }

        return rentedKeyBuffer.AsSpan(0, written);
    }

    /// <summary>
    /// Checks if the current position is a block sequence entry (- followed by whitespace).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsBlockSequenceEntry()
    {
        int next = _pos + 1;
        return next >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[next]);
    }

    /// <summary>
    /// Checks if the current position is at a mapping key indicator (plain scalar key followed by ': ').
    /// </summary>
    private bool IsMappingKeyIndicator()
    {
        return IsBlockMappingStart(false);
    }

    /// <summary>
    /// Converts a block sequence to a JSON array.
    /// </summary>
    private void ConvertBlockSequence(int parentIndent)
    {
        EnterDepth();

        _writer.WriteStartArray();

        int sequenceIndent = _column;

        while (_pos < _buffer.Length)
        {
            SkipWhitespaceAndComments();
            if (_pos >= _buffer.Length)
            {
                break;
            }

            // Check indentation
            if (_column != sequenceIndent)
            {
                break;
            }

            // Check for document markers
            if (IsDocumentEndOrStart())
            {
                break;
            }

            // Expect '- '
            if (_buffer[_pos] != YamlConstants.Dash || !IsBlockSequenceEntry())
            {
                break;
            }

            Advance(1); // skip '-'
            SkipWhitespaceOnly();

            // Check for empty entry (newline or comment immediately after -)
            if (_pos >= _buffer.Length || YamlCharacters.IsLineBreak(_buffer[_pos]) || IsComment())
            {
                SkipWhitespaceAndComments();
                if (_pos >= _buffer.Length || _column <= sequenceIndent || IsDocumentEndOrStart())
                {
                    _writer.WriteNullValue();
                }
                else
                {
                    // Content on the next line, indented more
                    ConvertNode(sequenceIndent, false);
                }
            }
            else
            {
                // Content on the same line after '- '
                // The compact notation: "- key: value" means the sequence entry is a mapping
                ConvertNode(sequenceIndent, false);
            }

            SkipWhitespaceAndComments();
        }

        _writer.WriteEndArray();
        _depth--;
    }

    /// <summary>
    /// Converts a flow mapping {key: value, ...} to a JSON object.
    /// </summary>
    private void ConvertFlowMapping()
    {
        EnterDepth();

        Advance(1); // skip '{'
        _writer.WriteStartObject();

        Span<int> bucketBuffer = stackalloc int[Utf8KeyHashSet.StackAllocBucketSize];
        Span<byte> entryBuffer = stackalloc byte[Utf8KeyHashSet.StackAllocEntrySize];
        Span<byte> keyByteBuffer = stackalloc byte[Utf8KeyHashSet.StackAllocKeyBufferSize];
        Utf8KeyHashSet keySet = new(8, bucketBuffer, entryBuffer, keyByteBuffer);
        try
        {
            SkipWhitespaceAndComments();

            while (_pos < _buffer.Length && _buffer[_pos] != YamlConstants.CloseBrace)
            {
                int posBeforeEntry = _pos;

                // Skip anchor/tag (in any order)
                YamlTagKind flowKeyTag = YamlTagKind.None;
                bool flowKeyHasAnchor = false;
                ParseNodeProperties(ref flowKeyTag, ref flowKeyHasAnchor);

                // Read key — handle alias keys
                if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Asterisk)
                {
                    WriteAliasAsPropertyName();
                }
                else
                {
                    ReadOnlySpan<byte> key = ReadFlowMappingKey(out byte[]? rentedKeyBuffer);
                    try
                    {
                        // Duplicate key check
                        if (_options.DuplicateKeyBehavior == DuplicateKeyBehavior.Error)
                        {
                            if (!keySet.AddIfNotExists(key))
                            {
                                ThrowDuplicateKey();
                            }
                        }

                        _writer.WritePropertyName(key);
                    }
                    finally
                    {
                        if (rentedKeyBuffer != null)
                        {
                            ArrayPool<byte>.Shared.Return(rentedKeyBuffer);
                        }
                    }
                }

                // Expect ':'
                SkipWhitespaceAndComments();
                if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Colon)
                {
                    Advance(1);
                    SkipWhitespaceAndComments();

                    if (_pos >= _buffer.Length)
                    {
                        _writer.WriteNullValue();
                    }
                    else if (_buffer[_pos] == YamlConstants.Comma || _buffer[_pos] == YamlConstants.CloseBrace)
                    {
                        _writer.WriteNullValue();
                    }
                    else
                    {
                        ConvertNode(-1, true);
                    }
                }
                else
                {
                    // Implicit null value (key without colon in flow)
                    _writer.WriteNullValue();
                }

                SkipWhitespaceAndComments();
                if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Comma)
                {
                    Advance(1);
                    SkipWhitespaceAndComments();
                }

                if (_pos == posBeforeEntry)
                {
                    ThrowUnexpectedCharacter();
                }
            }

            if (_pos >= _buffer.Length)
            {
                ThrowUnterminatedFlowMapping();
            }

            Advance(1); // skip '}'
            _writer.WriteEndObject();
        }
        finally
        {
            keySet.Dispose();
        }

        _depth--;
    }

    /// <summary>
    /// Converts a flow sequence [a, b, ...] to a JSON array.
    /// </summary>
    private void ConvertFlowSequence()
    {
        EnterDepth();

        Advance(1); // skip '['
        _writer.WriteStartArray();

        SkipWhitespaceAndComments();

        while (_pos < _buffer.Length && _buffer[_pos] != YamlConstants.CloseBracket)
        {
            int posBeforeEntry = _pos;

            // Check for explicit key indicator '?' in flow sequence
            // Creates a single-pair mapping: [? key : value] → [{"key": "value"}]
            if (_buffer[_pos] == YamlConstants.QuestionMark)
            {
                int next = _pos + 1;
                if (next >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[next]))
                {
                    ConvertExplicitFlowMappingEntry();
                    SkipWhitespaceAndComments();
                    if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Comma)
                    {
                        Advance(1);
                        SkipWhitespaceAndComments();
                    }

                    if (_pos == posBeforeEntry)
                    {
                        ThrowUnexpectedCharacter();
                    }

                    continue;
                }
            }

            // Check for implicit flow mapping: `key: value` inside a sequence
            // becomes {"key": "value"}
            if (IsImplicitFlowMappingEntry())
            {
                ConvertImplicitFlowMapping();
            }
            else
            {
                ConvertNode(-1, true);
            }

            SkipWhitespaceAndComments();
            if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Comma)
            {
                Advance(1);
                SkipWhitespaceAndComments();
            }

            // Safety: ensure we made progress to avoid infinite loops
            if (_pos == posBeforeEntry)
            {
                ThrowUnexpectedCharacter();
            }
        }

        if (_pos >= _buffer.Length)
        {
            ThrowUnterminatedFlowSequence();
        }

        Advance(1); // skip ']'
        _writer.WriteEndArray();
        _depth--;
    }

    /// <summary>
    /// Checks if the current position starts an implicit flow mapping entry
    /// (a key followed by ': ' inside a flow sequence).
    /// Keys may be plain, double-quoted, or single-quoted scalars.
    /// </summary>
    private bool IsImplicitFlowMappingEntry()
    {
        if (_pos >= _buffer.Length)
        {
            return false;
        }

        byte c = _buffer[_pos];

        // Explicit flow collections or aliases — not implicit mapping entries
        if (c == YamlConstants.OpenBrace || c == YamlConstants.OpenBracket
            || c == YamlConstants.Asterisk)
        {
            return false;
        }

        int p = _pos;

        // Skip anchor if present
        if (c == YamlConstants.Ampersand)
        {
            p++;
            while (p < _buffer.Length && !YamlCharacters.IsWhitespaceOrLineBreak(_buffer[p])
                && _buffer[p] != YamlConstants.Comma && _buffer[p] != YamlConstants.CloseBracket
                && _buffer[p] != YamlConstants.CloseBrace)
            {
                p++;
            }

            while (p < _buffer.Length && YamlCharacters.IsWhitespace(_buffer[p]))
            {
                p++;
            }

            if (p >= _buffer.Length)
            {
                return false;
            }

            c = _buffer[p];

            // After the anchor, if the next thing is a flow collection or alias,
            // it's not an implicit mapping entry (it's an anchored value)
            if (c == YamlConstants.OpenBrace || c == YamlConstants.OpenBracket
                || c == YamlConstants.Asterisk)
            {
                return false;
            }
        }

        // Skip past a quoted key
        if (c == YamlConstants.DoubleQuote)
        {
            p++;
            while (p < _buffer.Length && _buffer[p] != YamlConstants.DoubleQuote)
            {
                if (_buffer[p] == YamlConstants.Backslash && p + 1 < _buffer.Length)
                {
                    p++; // skip escaped char
                }

                p++;
            }

            if (p >= _buffer.Length)
            {
                return false;
            }

            p++; // skip closing quote

            // Skip whitespace after the quoted key
            while (p < _buffer.Length && YamlCharacters.IsWhitespace(_buffer[p]))
            {
                p++;
            }

            // Must be followed by ':'
            return p < _buffer.Length && _buffer[p] == YamlConstants.Colon
                && (p + 1 >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[p + 1])
                    || _buffer[p + 1] == YamlConstants.Comma || _buffer[p + 1] == YamlConstants.CloseBracket
                    || _buffer[p + 1] == YamlConstants.CloseBrace);
        }

        if (c == YamlConstants.SingleQuote)
        {
            p++;
            while (p < _buffer.Length)
            {
                if (_buffer[p] == YamlConstants.SingleQuote)
                {
                    if (p + 1 < _buffer.Length && _buffer[p + 1] == YamlConstants.SingleQuote)
                    {
                        p += 2; // escaped single quote
                        continue;
                    }

                    break;
                }

                p++;
            }

            if (p >= _buffer.Length)
            {
                return false;
            }

            p++; // skip closing quote

            while (p < _buffer.Length && YamlCharacters.IsWhitespace(_buffer[p]))
            {
                p++;
            }

            return p < _buffer.Length && _buffer[p] == YamlConstants.Colon
                && (p + 1 >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[p + 1])
                    || _buffer[p + 1] == YamlConstants.Comma || _buffer[p + 1] == YamlConstants.CloseBracket
                    || _buffer[p + 1] == YamlConstants.CloseBrace);
        }

        // Plain scalar key — scan for ': ' before any flow terminator
        while (p < _buffer.Length)
        {
            byte ch = _buffer[p];

            if (YamlCharacters.IsLineBreak(ch) || ch == YamlConstants.Comma
                || ch == YamlConstants.CloseBracket || ch == YamlConstants.CloseBrace)
            {
                return false;
            }

            if (ch == YamlConstants.Colon)
            {
                int next = p + 1;
                if (next >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[next])
                    || _buffer[next] == YamlConstants.Comma || _buffer[next] == YamlConstants.CloseBracket
                    || _buffer[next] == YamlConstants.CloseBrace)
                {
                    return true;
                }
            }

            p++;
        }

        return false;
    }

    /// <summary>
    /// Converts an implicit flow mapping entry (key: value) into a JSON object {key: value}.
    /// </summary>
    private void ConvertImplicitFlowMapping()
    {
        _writer.WriteStartObject();

        // Skip anchor/tag on key
        YamlTagKind implKeyTag = YamlTagKind.None;
        bool implKeyHasAnchor = false;
        ParseNodeProperties(ref implKeyTag, ref implKeyHasAnchor);

        // Read the key
        byte[]? rentedKeyBuffer = null;
        try
        {
            ReadOnlySpan<byte> key = ReadFlowMappingKey(out rentedKeyBuffer);
            _writer.WritePropertyName(key);
        }
        finally
        {
            if (rentedKeyBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(rentedKeyBuffer);
            }
        }

        SkipWhitespaceOnly();

        // Skip the colon
        if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Colon)
        {
            Advance(1);
        }

        SkipWhitespaceAndComments();

        // Read the value — if we're at a flow terminator, the value is null
        if (_pos >= _buffer.Length
            || _buffer[_pos] == YamlConstants.Comma
            || _buffer[_pos] == YamlConstants.CloseBracket
            || _buffer[_pos] == YamlConstants.CloseBrace)
        {
            _writer.WriteNullValue();
        }
        else
        {
            ConvertNode(-1, true);
        }

        _writer.WriteEndObject();
    }

    /// <summary>
    /// Converts an explicit flow mapping entry (? key : value) inside a flow sequence.
    /// </summary>
    private void ConvertExplicitFlowMappingEntry()
    {
        _writer.WriteStartObject();

        // Skip '?'
        Advance(1);
        SkipWhitespaceAndComments();

        // Read the key (multi-line plain scalar in flow context)
        byte[]? rentedKeyBuffer = null;
        try
        {
            ReadOnlySpan<byte> key;
            if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.DoubleQuote)
            {
                key = ReadDoubleQuotedScalar(out rentedKeyBuffer);
            }
            else if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.SingleQuote)
            {
                key = ReadSingleQuotedScalar(out rentedKeyBuffer);
            }
            else
            {
                key = ReadFlowPlainScalarKey(out rentedKeyBuffer);
            }

            _writer.WritePropertyName(key);
        }
        finally
        {
            if (rentedKeyBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(rentedKeyBuffer);
            }
        }

        SkipWhitespaceAndComments();

        // Skip ':' if present
        if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Colon)
        {
            int next = _pos + 1;
            if (next >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[next])
                || YamlCharacters.IsFlowIndicator(_buffer[next]))
            {
                Advance(1);
                SkipWhitespaceAndComments();

                // Read the value
                if (_pos >= _buffer.Length
                    || _buffer[_pos] == YamlConstants.Comma
                    || _buffer[_pos] == YamlConstants.CloseBracket
                    || _buffer[_pos] == YamlConstants.CloseBrace)
                {
                    _writer.WriteNullValue();
                }
                else
                {
                    ConvertNode(-1, true);
                }
            }
            else
            {
                _writer.WriteNullValue();
            }
        }
        else
        {
            _writer.WriteNullValue();
        }

        _writer.WriteEndObject();
    }

    /// <summary>
    /// Reads a key in a flow mapping context.
    /// </summary>
    private ReadOnlySpan<byte> ReadFlowMappingKey(out byte[]? rentedKeyBuffer)
    {
        rentedKeyBuffer = null;
        if (_pos >= _buffer.Length)
        {
            ThrowUnexpectedEnd();
        }

        byte c = _buffer[_pos];

        if (c == YamlConstants.DoubleQuote)
        {
            return ReadDoubleQuotedScalar(out rentedKeyBuffer);
        }

        if (c == YamlConstants.SingleQuote)
        {
            return ReadSingleQuotedScalar(out rentedKeyBuffer);
        }

        // Plain scalar key in flow context — terminates at ':', ',', '}', ']'
        return ReadFlowPlainScalarKey(out rentedKeyBuffer);
    }

    /// <summary>
    /// Converts a plain (unquoted) scalar, writing it to the JSON writer
    /// with type resolution based on the configured schema and optional tag override.
    /// </summary>
    private void ConvertPlainScalar(int parentIndent, bool inFlow, YamlTagKind tag = YamlTagKind.None)
    {
        ReadOnlySpan<byte> value = ReadPlainScalarValue(parentIndent, inFlow, tag);

        // If value is empty and we're past the start, ReadPlainScalarValue already wrote
        // (multi-line path writes directly)
        if (value.IsEmpty)
        {
            return;
        }

        WriteScalarWithTag(value, tag);
    }

    /// <summary>
    /// Writes a scalar value with tag-based type override.
    /// </summary>
    private void WriteScalarWithTag(ReadOnlySpan<byte> value, YamlTagKind tag)
    {
        switch (tag)
        {
            case YamlTagKind.Str:
            case YamlTagKind.NonSpecific:
                _writer.WriteStringValue(value);
                return;

            case YamlTagKind.Null:
                _writer.WriteNullValue();
                return;

            case YamlTagKind.Bool:
            {
                ScalarResolver.ScalarType st = ScalarResolver.Resolve(value, _options.Schema);
                if (st == ScalarResolver.ScalarType.True)
                {
                    _writer.WriteBooleanValue(true);
                }
                else if (st == ScalarResolver.ScalarType.False)
                {
                    _writer.WriteBooleanValue(false);
                }
                else
                {
                    // Fall back to schema resolution
                    ScalarResolver.WriteScalar(_writer, value, st);
                }

                return;
            }

            case YamlTagKind.Int:
            {
                ScalarResolver.ScalarType st = ScalarResolver.Resolve(value, _options.Schema);
                if (st == ScalarResolver.ScalarType.Integer || st == ScalarResolver.ScalarType.HexInteger
                    || st == ScalarResolver.ScalarType.OctalInteger)
                {
                    ScalarResolver.WriteScalar(_writer, value, st);
                }
                else
                {
                    // Force integer interpretation
                    ScalarResolver.WriteScalar(_writer, value, ScalarResolver.ScalarType.Integer);
                }

                return;
            }

            case YamlTagKind.Float:
            {
                ScalarResolver.ScalarType st = ScalarResolver.Resolve(value, _options.Schema);
                if (st == ScalarResolver.ScalarType.Float || st == ScalarResolver.ScalarType.InfinityOrNaN)
                {
                    ScalarResolver.WriteScalar(_writer, value, st);
                }
                else
                {
                    ScalarResolver.WriteScalar(_writer, value, ScalarResolver.ScalarType.Float);
                }

                return;
            }

            default:
            {
                // Normal schema resolution
                ScalarResolver.ScalarType scalarType = ScalarResolver.Resolve(value, _options.Schema);
                ScalarResolver.WriteScalar(_writer, value, scalarType);
                return;
            }
        }
    }

    /// <summary>
    /// Reads a plain scalar value (potentially multi-line with line folding).
    /// </summary>
    private ReadOnlySpan<byte> ReadPlainScalarValue(int parentIndent, bool inFlow, YamlTagKind tag = YamlTagKind.None)
    {
        int start = _pos;
        int end = _pos;

        // Read first line content
        ReadPlainScalarLine(start, ref end, inFlow);

        // Trim trailing whitespace from the first line
        while (end > start && YamlCharacters.IsWhitespace(_buffer[end - 1]))
        {
            end--;
        }

        // If not at a line break, return the single line
        if (_pos >= _buffer.Length || !YamlCharacters.IsLineBreak(_buffer[_pos]))
        {
            return _buffer.Slice(start, end - start);
        }

        // Check if there's a continuation line (multi-line plain scalar)
        // We need to look past blank lines to find the next content line
        int savedPos = _pos;
        int savedLine = _line;
        int savedColumn = _column;

        int blankLineCount = 0;
        if (!TryFindPlainScalarContinuation(parentIndent, inFlow, ref blankLineCount))
        {
            // Rewind — not a continuation
            _pos = savedPos;
            _line = savedLine;
            _column = savedColumn;
            return _buffer.Slice(start, end - start);
        }

        // Skip additional whitespace (tabs) as part of s-flow-line-prefix
        while (_pos < _buffer.Length && YamlCharacters.IsWhitespace(_buffer[_pos])
            && !YamlCharacters.IsLineBreak(_buffer[_pos]))
        {
            _pos++;
            _column++;
        }

        // Multi-line: build folded output with rented buffer
        byte[]? rentedBuffer = null;
        try
        {
            int estimatedLength = Math.Max(end - start + 128, 256);
            rentedBuffer = ArrayPool<byte>.Shared.Rent(estimatedLength);
            Span<byte> output = rentedBuffer;
            int written = 0;

            // Copy first line
            ReadOnlySpan<byte> firstLine = _buffer.Slice(start, end - start);
            EnsureRentedCapacity(ref rentedBuffer, ref output, written, firstLine.Length);
            firstLine.CopyTo(output.Slice(written));
            written += firstLine.Length;

            // Process continuation lines (we're already positioned at the first continuation content)
            while (true)
            {
                // Write fold separator: blank lines produce \n each, single fold = space
                EnsureRentedCapacity(ref rentedBuffer, ref output, written, blankLineCount + 1);
                if (blankLineCount > 0)
                {
                    for (int i = 0; i < blankLineCount; i++)
                    {
                        output[written++] = YamlConstants.LineFeed;
                    }
                }
                else
                {
                    output[written++] = YamlConstants.Space;
                }

                // Read this continuation line
                int lineStart = _pos;
                int lineEnd = _pos;
                ReadPlainScalarLine(lineStart, ref lineEnd, inFlow);

                // Trim trailing whitespace
                while (lineEnd > lineStart && YamlCharacters.IsWhitespace(_buffer[lineEnd - 1]))
                {
                    lineEnd--;
                }

                ReadOnlySpan<byte> lineContent = _buffer.Slice(lineStart, lineEnd - lineStart);
                EnsureRentedCapacity(ref rentedBuffer, ref output, written, lineContent.Length);
                lineContent.CopyTo(output.Slice(written));
                written += lineContent.Length;

                // Check for another continuation line
                if (_pos >= _buffer.Length || !YamlCharacters.IsLineBreak(_buffer[_pos]))
                {
                    break;
                }

                int saved2Pos = _pos;
                int saved2Line = _line;
                int saved2Column = _column;

                blankLineCount = 0;
                if (!TryFindPlainScalarContinuation(parentIndent, inFlow, ref blankLineCount))
                {
                    _pos = saved2Pos;
                    _line = saved2Line;
                    _column = saved2Column;
                    break;
                }

                // Skip additional whitespace (tabs) as part of s-flow-line-prefix
                while (_pos < _buffer.Length && YamlCharacters.IsWhitespace(_buffer[_pos])
                    && !YamlCharacters.IsLineBreak(_buffer[_pos]))
                {
                    _pos++;
                    _column++;
                }
            }

            // Write via the scalar resolver (tag-aware)
            ReadOnlySpan<byte> value = rentedBuffer.AsSpan(0, written);
            WriteScalarWithTag(value, tag);

            return ReadOnlySpan<byte>.Empty; // Signal that we already wrote
        }
        finally
        {
            if (rentedBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
    }

    /// <summary>
    /// Reads one line of a plain scalar, advancing _pos to the line break or terminator.
    /// </summary>
    private void ReadPlainScalarLine(int lineStart, ref int lineEnd, bool inFlow)
    {
        while (_pos < _buffer.Length)
        {
            byte c = _buffer[_pos];

            if (YamlCharacters.IsLineBreak(c))
            {
                break;
            }

            if (inFlow && (c == YamlConstants.Comma || c == YamlConstants.CloseBrace
                || c == YamlConstants.CloseBracket))
            {
                break;
            }

            // Comment check: # preceded by whitespace
            if (c == YamlConstants.Hash && _pos > lineStart && YamlCharacters.IsWhitespace(_buffer[_pos - 1]))
            {
                SkipToEndOfLine();
                break;
            }

            // Mapping value indicator in block context
            if (!inFlow && c == YamlConstants.Colon)
            {
                int next = _pos + 1;
                if (next >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[next]))
                {
                    break;
                }
            }

            // Mapping value indicator in flow context
            if (inFlow && c == YamlConstants.Colon)
            {
                int next = _pos + 1;
                if (next >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[next])
                    || _buffer[next] == YamlConstants.Comma || _buffer[next] == YamlConstants.CloseBrace
                    || _buffer[next] == YamlConstants.CloseBracket)
                {
                    break;
                }
            }

            _pos++;
            _column++;
            lineEnd = _pos;
        }
    }

    /// <summary>
    /// Checks if the plain scalar continues on the next line.
    /// Skips past the line break and any blank lines, counts them, and checks
    /// that the next content line is indented past parentIndent and is valid content.
    /// On success, _pos is at the start of the continuation content.
    /// The caller must save/restore position on failure.
    /// </summary>
    private bool TryFindPlainScalarContinuation(int parentIndent, bool inFlow, ref int blankLineCount)
    {
        // Skip the line break after the current content
        SkipLineBreak();

        // Skip blank lines (each blank line = optional whitespace + line break)
        while (_pos < _buffer.Length)
        {
            int lineStartPos = _pos;
            int lineStartLine = _line;
            int lineStartColumn = _column;

            // Skip whitespace on this line (spaces only count as indent)
            while (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Space)
            {
                _pos++;
                _column++;
            }

            if (_pos >= _buffer.Length)
            {
                return false;
            }

            // Skip any tabs that follow spaces (tabs don't count for indentation
            // but are whitespace — a line with only spaces+tabs is blank)
            while (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Tab)
            {
                _pos++;
                _column++;
            }

            if (_pos >= _buffer.Length)
            {
                return false;
            }

            if (YamlCharacters.IsLineBreak(_buffer[_pos]))
            {
                // This is a blank line
                blankLineCount++;
                SkipLineBreak();
                continue;
            }

            // In flow context, no indent check needed — content continues unless
            // we hit a flow indicator, mapping value indicator, or document marker
            if (inFlow)
            {
                byte fc = _buffer[_pos];
                if (fc == YamlConstants.Comma || fc == YamlConstants.CloseBrace
                    || fc == YamlConstants.CloseBracket || fc == YamlConstants.OpenBrace
                    || fc == YamlConstants.OpenBracket)
                {
                    return false;
                }

                // Mapping value indicator ': ' terminates the key, not continuation
                if (fc == YamlConstants.Colon)
                {
                    int next = _pos + 1;
                    if (next >= _buffer.Length
                        || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[next])
                        || YamlCharacters.IsFlowIndicator(_buffer[next]))
                    {
                        return false;
                    }
                }

                if (_buffer[_pos] == YamlConstants.Hash)
                {
                    return false;
                }

                if (IsDocumentEndOrStart())
                {
                    return false;
                }

                return true;
            }

            // Block context: check indentation
            int indent = _pos - lineStartPos;

            // Must be indented more than parent AND have valid continuation content
            if (indent <= parentIndent)
            {
                return false;
            }

            if (_buffer[_pos] == YamlConstants.Hash)
            {
                return false;
            }

            if (IsDocumentEndOrStart())
            {
                return false;
            }

            // Valid continuation — block indicators like '- ' and ': ' on
            // indented continuation lines are plain content, not collection starts.
            // Collection detection happens in ConvertNode, not here.
            return true;
        }

        return false;
    }

    /// <summary>
    /// Reads a plain scalar key (up to the ': ' indicator).
    /// </summary>
    private ReadOnlySpan<byte> ReadPlainScalarKey()
    {
        int start = _pos;
        int end = _pos;

        while (_pos < _buffer.Length)
        {
            byte c = _buffer[_pos];

            if (YamlCharacters.IsLineBreak(c))
            {
                break;
            }

            // Mapping value indicator
            if (c == YamlConstants.Colon)
            {
                int next = _pos + 1;
                if (next >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[next]))
                {
                    break;
                }
            }

            // Comment
            if (c == YamlConstants.Hash && _pos > start && YamlCharacters.IsWhitespace(_buffer[_pos - 1]))
            {
                break;
            }

            _pos++;
            _column++;
            end = _pos;
        }

        // Trim trailing whitespace
        while (end > start && YamlCharacters.IsWhitespace(_buffer[end - 1]))
        {
            end--;
        }

        return _buffer.Slice(start, end - start);
    }

    /// <summary>
    /// Reads a plain scalar key in flow context (terminates at : , } ]).
    /// </summary>
    private ReadOnlySpan<byte> ReadFlowPlainScalarKey(out byte[]? rentedKeyBuffer)
    {
        rentedKeyBuffer = null;
        int start = _pos;
        int end = _pos;

        // Read first line
        ReadPlainScalarLine(start, ref end, true);

        // Trim trailing whitespace
        while (end > start && YamlCharacters.IsWhitespace(_buffer[end - 1]))
        {
            end--;
        }

        // If not at a line break, return the single line directly from buffer
        if (_pos >= _buffer.Length || !YamlCharacters.IsLineBreak(_buffer[_pos]))
        {
            return _buffer.Slice(start, end - start);
        }

        // Check for continuation line (multi-line flow plain scalar key)
        int savedPos = _pos;
        int savedLine = _line;
        int savedColumn = _column;

        int blankLineCount = 0;
        if (!TryFindPlainScalarContinuation(-1, true, ref blankLineCount))
        {
            _pos = savedPos;
            _line = savedLine;
            _column = savedColumn;
            return _buffer.Slice(start, end - start);
        }

        // Skip additional whitespace (tabs) as part of s-flow-line-prefix
        while (_pos < _buffer.Length && YamlCharacters.IsWhitespace(_buffer[_pos])
            && !YamlCharacters.IsLineBreak(_buffer[_pos]))
        {
            _pos++;
            _column++;
        }

        // Multi-line: build folded key in rented buffer
        int estimatedLength = Math.Max(end - start + 128, 256);
        rentedKeyBuffer = ArrayPool<byte>.Shared.Rent(estimatedLength);
        Span<byte> output = rentedKeyBuffer;
        int written = 0;

        // Copy first line
        ReadOnlySpan<byte> firstLine = _buffer.Slice(start, end - start);
        EnsureRentedCapacity(ref rentedKeyBuffer, ref output, written, firstLine.Length);
        firstLine.CopyTo(output.Slice(written));
        written += firstLine.Length;

        // Process continuation lines
        while (true)
        {
            // Write fold separator: blank lines produce \n each, single fold = space
            EnsureRentedCapacity(ref rentedKeyBuffer, ref output, written, blankLineCount + 1);
            if (blankLineCount > 0)
            {
                for (int i = 0; i < blankLineCount; i++)
                {
                    output[written++] = YamlConstants.LineFeed;
                }
            }
            else
            {
                output[written++] = YamlConstants.Space;
            }

            // Read this continuation line
            int lineStart = _pos;
            int lineEnd = _pos;
            ReadPlainScalarLine(lineStart, ref lineEnd, true);

            // Trim trailing whitespace
            while (lineEnd > lineStart && YamlCharacters.IsWhitespace(_buffer[lineEnd - 1]))
            {
                lineEnd--;
            }

            ReadOnlySpan<byte> lineContent = _buffer.Slice(lineStart, lineEnd - lineStart);
            EnsureRentedCapacity(ref rentedKeyBuffer, ref output, written, lineContent.Length);
            lineContent.CopyTo(output.Slice(written));
            written += lineContent.Length;

            // Check for another continuation line
            if (_pos >= _buffer.Length || !YamlCharacters.IsLineBreak(_buffer[_pos]))
            {
                break;
            }

            int saved2Pos = _pos;
            int saved2Line = _line;
            int saved2Column = _column;

            blankLineCount = 0;
            if (!TryFindPlainScalarContinuation(-1, true, ref blankLineCount))
            {
                _pos = saved2Pos;
                _line = saved2Line;
                _column = saved2Column;
                break;
            }

            // Skip additional whitespace (tabs) as part of s-flow-line-prefix
            while (_pos < _buffer.Length && YamlCharacters.IsWhitespace(_buffer[_pos])
                && !YamlCharacters.IsLineBreak(_buffer[_pos]))
            {
                _pos++;
                _column++;
            }
        }

        return rentedKeyBuffer.AsSpan(0, written);
    }

    /// <summary>
    /// Converts a double-quoted scalar, processing escape sequences.
    /// </summary>
    private void ConvertDoubleQuotedScalar(int parentIndent, bool inFlow, bool asValue)
    {
        ReadOnlySpan<byte> value = ReadDoubleQuotedScalar(out byte[]? rentedBuffer);
        try
        {
            if (asValue)
            {
                _writer.WriteStringValue(value);
            }
        }
        finally
        {
            if (rentedBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
    }

    /// <summary>
    /// Reads and processes a double-quoted scalar, returning the unescaped UTF-8 bytes.
    /// The caller must return <paramref name="rentedBuffer"/> to the pool after use.
    /// </summary>
    private ReadOnlySpan<byte> ReadDoubleQuotedScalar(out byte[]? rentedBuffer)
    {
        rentedBuffer = null;
        Advance(1); // skip opening '"'

        int start = _pos;
        bool hasEscapes = false;

        // Fast scan: check if we need to process escapes
        while (_pos < _buffer.Length)
        {
            byte c = _buffer[_pos];
            if (c == YamlConstants.DoubleQuote)
            {
                if (!hasEscapes)
                {
                    // No escapes — return the raw slice
                    ReadOnlySpan<byte> result = _buffer.Slice(start, _pos - start);
                    Advance(1); // skip closing '"'
                    return result;
                }

                break;
            }

            if (c == YamlConstants.Backslash)
            {
                hasEscapes = true;
            }

            if (YamlCharacters.IsLineBreak(c))
            {
                hasEscapes = true; // Multi-line needs folding
            }

            _pos++;
            _column++;
        }

        if (_pos >= _buffer.Length)
        {
            ThrowUnterminatedDoubleQuotedScalar();
        }

        // Has escapes — process them using a rented buffer (no stackalloc since we return a span)
        _pos = start; // rewind
        _column = _column - (_pos - start); // approximate rewind of column

        int estimatedLength = Math.Max(_buffer.Length - start, 64);
        rentedBuffer = ArrayPool<byte>.Shared.Rent(estimatedLength);
        Span<byte> output = rentedBuffer;

        int written = 0;

        // Track the output position after the last non-literal-whitespace byte.
        // Escape sequences always count as content (even if they produce whitespace bytes).
        // Only literal space/tab bytes in the source don't update this marker.
        // When folding a line break, we trim back to this position instead of scanning
        // the output buffer (which can't distinguish escaped vs literal whitespace).
        int lastContentEnd = 0;

        while (_pos < _buffer.Length)
        {
            byte c = _buffer[_pos];

            if (c == YamlConstants.DoubleQuote)
            {
                Advance(1); // skip closing '"'
                return rentedBuffer.AsSpan(0, written);
            }

            if (c == YamlConstants.Backslash)
            {
                Advance(1);
                if (_pos >= _buffer.Length)
                {
                    ThrowUnterminatedDoubleQuotedScalar();
                }

                byte escaped = _buffer[_pos];
                Advance(1);

                EnsureRentedCapacity(ref rentedBuffer, ref output, written, 4);

                switch (escaped)
                {
                    case (byte)'0': output[written++] = 0x00; break;
                    case (byte)'a': output[written++] = 0x07; break;
                    case (byte)'b': output[written++] = 0x08; break;
                    case (byte)'t':
                    case YamlConstants.Tab:
                        // \t and \<TAB> both produce horizontal tab (YAML ns-esc-horizontal-tab)
                        output[written++] = 0x09;
                        break;
                    case (byte)'n': output[written++] = 0x0A; break;
                    case (byte)'v': output[written++] = 0x0B; break;
                    case (byte)'f': output[written++] = 0x0C; break;
                    case (byte)'r': output[written++] = 0x0D; break;
                    case (byte)'e': output[written++] = 0x1B; break;
                    case (byte)'"': output[written++] = 0x22; break;
                    case (byte)'/': output[written++] = 0x2F; break;
                    case (byte)'\\': output[written++] = 0x5C; break;
                    case YamlConstants.Space:
                        // \<SPACE> produces space (YAML ns-esc-space)
                        output[written++] = 0x20;
                        break;
                    case (byte)'N': // Next line (U+0085)
                        output[written++] = 0xC2;
                        output[written++] = 0x85;
                        break;
                    case (byte)'_': // Non-breaking space (U+00A0)
                        output[written++] = 0xC2;
                        output[written++] = 0xA0;
                        break;
                    case (byte)'L': // Line separator (U+2028)
                        output[written++] = 0xE2;
                        output[written++] = 0x80;
                        output[written++] = 0xA8;
                        break;
                    case (byte)'P': // Paragraph separator (U+2029)
                        output[written++] = 0xE2;
                        output[written++] = 0x80;
                        output[written++] = 0xA9;
                        break;
                    case (byte)'x':
                        EnsureRentedCapacity(ref rentedBuffer, ref output, written, 4);
                        written += WriteHexEscape(output.Slice(written), 2);
                        break;
                    case (byte)'u':
                        EnsureRentedCapacity(ref rentedBuffer, ref output, written, 4);
                        written += WriteHexEscape(output.Slice(written), 4);
                        break;
                    case (byte)'U':
                        EnsureRentedCapacity(ref rentedBuffer, ref output, written, 4);
                        written += WriteHexEscape(output.Slice(written), 8);
                        break;
                    case YamlConstants.LineFeed:
                    case YamlConstants.CarriageReturn:
                        // Escaped line break (s-double-escaped): \ directly before line break
                        // Per test-suite behavior, trailing whitespace before \ is NOT trimmed
                        // (only regular fold breaks trim trailing whitespace)
                        if (escaped == YamlConstants.CarriageReturn && _pos < _buffer.Length && _buffer[_pos] == YamlConstants.LineFeed)
                        {
                            Advance(1);
                        }

                        AdvanceLine();
                        SkipWhitespaceOnly();
                        break;
                    default:
                        ThrowInvalidEscapeSequence(escaped);
                        break;
                }

                // All escape sequences produce content — update the content marker
                // (except line break escape which doesn't write output)
                if (escaped != YamlConstants.LineFeed && escaped != YamlConstants.CarriageReturn)
                {
                    lastContentEnd = written;
                }
            }
            else if (YamlCharacters.IsLineBreak(c))
            {
                // Multi-line double-quoted: fold line breaks
                // Trim trailing literal whitespace (only bytes after last escape/content)
                written = lastContentEnd;

                SkipLineBreak();
                SkipWhitespaceOnly();

                // Count blank lines
                int blankLines = 0;
                while (_pos < _buffer.Length && YamlCharacters.IsLineBreak(_buffer[_pos]))
                {
                    blankLines++;
                    SkipLineBreak();
                    SkipWhitespaceOnly();
                }

                EnsureRentedCapacity(ref rentedBuffer, ref output, written, blankLines + 1);
                if (blankLines > 0)
                {
                    // Preserve blank lines as \n — first line break is discarded, rest retained
                    for (int i = 0; i < blankLines; i++)
                    {
                        output[written++] = YamlConstants.LineFeed;
                    }
                }
                else
                {
                    // Single line break → fold to space
                    output[written++] = YamlConstants.Space;
                }

                lastContentEnd = written;
            }
            else
            {
                EnsureRentedCapacity(ref rentedBuffer, ref output, written, 1);
                output[written++] = c;

                // Only update content marker for non-whitespace literal bytes
                if (c != YamlConstants.Space && c != YamlConstants.Tab)
                {
                    lastContentEnd = written;
                }

                Advance(1);
            }
        }

        ThrowUnterminatedDoubleQuotedScalar();
        return default; // unreachable
    }

    /// <summary>
    /// Converts a single-quoted scalar.
    /// </summary>
    private void ConvertSingleQuotedScalar(bool asValue)
    {
        ReadOnlySpan<byte> value = ReadSingleQuotedScalar(out byte[]? rentedBuffer);
        try
        {
            if (asValue)
            {
                _writer.WriteStringValue(value);
            }
        }
        finally
        {
            if (rentedBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
    }

    /// <summary>
    /// Reads a single-quoted scalar. The only escape is '' → '.
    /// The caller must return <paramref name="rentedBuffer"/> to the pool after use.
    /// </summary>
    private ReadOnlySpan<byte> ReadSingleQuotedScalar(out byte[]? rentedBuffer)
    {
        rentedBuffer = null;
        Advance(1); // skip opening '''

        int start = _pos;
        bool hasEscapes = false;

        // Fast scan for closing quote
        while (_pos < _buffer.Length)
        {
            byte c = _buffer[_pos];

            if (c == YamlConstants.SingleQuote)
            {
                int next = _pos + 1;
                if (next < _buffer.Length && _buffer[next] == YamlConstants.SingleQuote)
                {
                    // Escaped single quote ''
                    hasEscapes = true;
                    _pos += 2;
                    _column += 2;
                    continue;
                }

                if (!hasEscapes)
                {
                    ReadOnlySpan<byte> result = _buffer.Slice(start, _pos - start);
                    Advance(1); // skip closing '''
                    return result;
                }

                break;
            }

            if (YamlCharacters.IsLineBreak(c))
            {
                hasEscapes = true; // Multi-line needs folding
            }

            _pos++;
            _column++;
        }

        if (_pos >= _buffer.Length)
        {
            ThrowUnterminatedSingleQuotedScalar();
        }

        // Process escaped content using a rented buffer
        _pos = start;
        _column = _column - (_pos - start);

        int estimatedLength = Math.Max(_buffer.Length - start, 64);
        rentedBuffer = ArrayPool<byte>.Shared.Rent(estimatedLength);
        Span<byte> output = rentedBuffer;

        int written = 0;

        while (_pos < _buffer.Length)
        {
            byte c = _buffer[_pos];

            if (c == YamlConstants.SingleQuote)
            {
                int next = _pos + 1;
                if (next < _buffer.Length && _buffer[next] == YamlConstants.SingleQuote)
                {
                    EnsureRentedCapacity(ref rentedBuffer, ref output, written, 1);
                    output[written++] = YamlConstants.SingleQuote;
                    _pos += 2;
                    _column += 2;
                    continue;
                }

                Advance(1); // skip closing '''
                return rentedBuffer.AsSpan(0, written);
            }

            if (YamlCharacters.IsLineBreak(c))
            {
                // Multi-line single-quoted: fold line breaks
                // Trim trailing whitespace from current line
                while (written > 0 && (output[written - 1] == YamlConstants.Space || output[written - 1] == YamlConstants.Tab))
                {
                    written--;
                }

                SkipLineBreak();
                SkipWhitespaceOnly();

                int blankLines = 0;
                while (_pos < _buffer.Length && YamlCharacters.IsLineBreak(_buffer[_pos]))
                {
                    blankLines++;
                    SkipLineBreak();
                    SkipWhitespaceOnly();
                }

                EnsureRentedCapacity(ref rentedBuffer, ref output, written, blankLines + 1);
                if (blankLines > 0)
                {
                    for (int i = 0; i < blankLines; i++)
                    {
                        output[written++] = YamlConstants.LineFeed;
                    }
                }
                else
                {
                    output[written++] = YamlConstants.Space;
                }
            }
            else
            {
                EnsureRentedCapacity(ref rentedBuffer, ref output, written, 1);
                output[written++] = c;
                Advance(1);
            }
        }

        ThrowUnterminatedSingleQuotedScalar();
        return default;
    }

    /// <summary>
    /// Reads a block scalar (literal | or folded &gt;) content into a rented buffer.
    /// Used for explicit key block scalars where we need the bytes rather than
    /// writing to the JSON writer.
    /// </summary>
    private ReadOnlySpan<byte> ReadBlockScalarContent(int parentIndent, out byte[]? rentedKeyBuffer)
    {
        byte indicator = _buffer[_pos];
        bool literal = indicator == YamlConstants.Pipe;
        Advance(1);

        // Parse block scalar header
        int chompMode = 0;
        int explicitIndent = 0;

        while (_pos < _buffer.Length && !YamlCharacters.IsLineBreak(_buffer[_pos]))
        {
            byte c = _buffer[_pos];

            if (c == YamlConstants.Dash)
            {
                chompMode = -1;
                Advance(1);
            }
            else if (c == YamlConstants.Plus)
            {
                chompMode = 1;
                Advance(1);
            }
            else if (YamlCharacters.IsDigit(c) && c != YamlConstants.Zero)
            {
                explicitIndent = c - YamlConstants.Zero;
                Advance(1);
            }
            else if (c == YamlConstants.Space || c == YamlConstants.Tab)
            {
                Advance(1);
            }
            else if (c == YamlConstants.Hash)
            {
                SkipToEndOfLine();
                break;
            }
            else
            {
                ThrowInvalidBlockScalarHeader();
                break;
            }
        }

        if (_pos < _buffer.Length)
        {
            SkipLineBreak();
        }

        int contentIndent;
        if (explicitIndent > 0)
        {
            contentIndent = parentIndent + explicitIndent;
        }
        else
        {
            int detected = DetectBlockScalarIndent();
            if (detected < 0)
            {
                contentIndent = parentIndent + 1;
            }
            else
            {
                contentIndent = detected;
            }
        }

        if (contentIndent <= parentIndent)
        {
            rentedKeyBuffer = null;
            return ReadOnlySpan<byte>.Empty;
        }

        int estimatedLength = Math.Max(_buffer.Length - _pos, 256);
        rentedKeyBuffer = ArrayPool<byte>.Shared.Rent(estimatedLength);
        Span<byte> output = rentedKeyBuffer;
        int written = 0;

        bool hasContent = false;
        bool prevLineMoreIndented = false;
        int blankLineCount = 0;

        while (_pos < _buffer.Length)
        {
            int lineIndent = 0;
            while (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Space)
            {
                lineIndent++;
                _pos++;
                _column++;
            }

            if (_pos >= _buffer.Length || YamlCharacters.IsLineBreak(_buffer[_pos]))
            {
                int extraIndent = lineIndent - contentIndent;

                if (extraIndent > 0 && hasContent)
                {
                    WriteBlockScalarSeparator(
                        literal, hasContent, prevLineMoreIndented, true,
                        blankLineCount, ref written, ref output, ref rentedKeyBuffer);

                    EnsureOutputCapacity(ref output, ref rentedKeyBuffer, written, extraIndent);
                    for (int i = 0; i < extraIndent; i++)
                    {
                        output[written++] = YamlConstants.Space;
                    }

                    blankLineCount = 0;
                    hasContent = true;
                    prevLineMoreIndented = true;
                }
                else
                {
                    blankLineCount++;
                }

                if (_pos < _buffer.Length)
                {
                    SkipLineBreak();
                }

                continue;
            }

            if (lineIndent < contentIndent)
            {
                _pos -= lineIndent;
                _column -= lineIndent;
                break;
            }

            int extra = lineIndent - contentIndent;
            bool isMoreIndented = extra > 0 ||
                (_pos < _buffer.Length && YamlCharacters.IsWhitespace(_buffer[_pos]));

            WriteBlockScalarSeparator(
                literal, hasContent, prevLineMoreIndented, isMoreIndented,
                blankLineCount, ref written, ref output, ref rentedKeyBuffer);

            blankLineCount = 0;

            EnsureOutputCapacity(ref output, ref rentedKeyBuffer, written, extra);
            for (int i = 0; i < extra; i++)
            {
                output[written++] = YamlConstants.Space;
            }

            while (_pos < _buffer.Length && !YamlCharacters.IsLineBreak(_buffer[_pos]))
            {
                EnsureOutputCapacity(ref output, ref rentedKeyBuffer, written, 1);
                output[written++] = _buffer[_pos];
                _pos++;
                _column++;
            }

            if (_pos < _buffer.Length)
            {
                SkipLineBreak();
            }

            hasContent = true;
            prevLineMoreIndented = isMoreIndented;
        }

        // Apply chomping to determine trailing newlines
        if (hasContent)
        {
            EnsureOutputCapacity(ref output, ref rentedKeyBuffer, written, 1 + blankLineCount);
            if (chompMode >= 0)
            {
                // Clip: exactly one trailing newline; Keep: all trailing newlines
                output[written++] = YamlConstants.LineFeed;
            }

            if (chompMode > 0)
            {
                for (int i = 0; i < blankLineCount; i++)
                {
                    output[written++] = YamlConstants.LineFeed;
                }
            }

            // Strip (chompMode < 0): no trailing newlines — written stays as-is
        }

        return rentedKeyBuffer.AsSpan(0, written);
    }

    /// <summary>
    /// Converts a block scalar (literal | or folded &gt;) to a JSON string.
    /// </summary>
    private void ConvertBlockScalar(int parentIndent)
    {
        byte indicator = _buffer[_pos];
        bool literal = indicator == YamlConstants.Pipe;
        Advance(1);

        // Parse block scalar header: [chomping indicator][indentation indicator]
        int chompMode = 0; // 0 = clip, -1 = strip, 1 = keep
        int explicitIndent = 0;

        while (_pos < _buffer.Length && !YamlCharacters.IsLineBreak(_buffer[_pos]))
        {
            byte c = _buffer[_pos];

            if (c == YamlConstants.Dash)
            {
                chompMode = -1;
                Advance(1);
            }
            else if (c == YamlConstants.Plus)
            {
                chompMode = 1;
                Advance(1);
            }
            else if (YamlCharacters.IsDigit(c) && c != YamlConstants.Zero)
            {
                explicitIndent = c - YamlConstants.Zero;
                Advance(1);
            }
            else if (c == YamlConstants.Space || c == YamlConstants.Tab)
            {
                Advance(1);
            }
            else if (c == YamlConstants.Hash)
            {
                // Comment — skip to end of line
                SkipToEndOfLine();
                break;
            }
            else
            {
                ThrowInvalidBlockScalarHeader();
                break;
            }
        }

        // Skip the header line break
        if (_pos < _buffer.Length)
        {
            SkipLineBreak();
        }

        // Determine content indentation
        int contentIndent;
        if (explicitIndent > 0)
        {
            // YAML spec: content at n+m where n=parentIndent, m=explicitIndent
            contentIndent = parentIndent + explicitIndent;
        }
        else
        {
            int detected = DetectBlockScalarIndent();
            if (detected < 0)
            {
                // No non-empty lines: default to parentIndent + 1
                contentIndent = parentIndent + 1;
            }
            else
            {
                contentIndent = detected;
            }
        }

        if (contentIndent <= parentIndent)
        {
            // Content not sufficiently indented — empty block scalar
            ApplyChomping(_writer, ReadOnlySpan<byte>.Empty, chompMode);
            return;
        }

        // Read the content lines
        byte[]? rentedBuffer = null;
        int estimatedLength = _buffer.Length - _pos;
        Span<byte> output = estimatedLength <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedBuffer = ArrayPool<byte>.Shared.Rent(estimatedLength));

        try
        {
            int written = 0;
            bool hasContent = false;
            bool prevLineMoreIndented = false;
            int blankLineCount = 0;

            while (_pos < _buffer.Length)
            {
                // Count leading spaces for this line
                int lineIndent = 0;
                while (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Space)
                {
                    lineIndent++;
                    _pos++;
                    _column++;
                }

                // Check if line ends here (whitespace-only or blank)
                if (_pos >= _buffer.Length || YamlCharacters.IsLineBreak(_buffer[_pos]))
                {
                    int extraIndent = lineIndent - contentIndent;

                    if (extraIndent > 0 && hasContent)
                    {
                        // Whitespace-only line with extra indentation after real content — preserve as content
                        WriteBlockScalarSeparator(
                            literal, hasContent, prevLineMoreIndented, true,
                            blankLineCount, ref written, ref output, ref rentedBuffer);

                        EnsureOutputCapacity(ref output, ref rentedBuffer, written, extraIndent);
                        for (int i = 0; i < extraIndent; i++)
                        {
                            output[written++] = YamlConstants.Space;
                        }

                        blankLineCount = 0;
                        hasContent = true;
                        prevLineMoreIndented = true;
                    }
                    else
                    {
                        // True blank line (indent < contentIndent or at contentIndent with no extra)
                        blankLineCount++;
                    }

                    if (_pos < _buffer.Length)
                    {
                        SkipLineBreak();
                    }

                    continue;
                }

                // Check if indentation is sufficient for content
                if (lineIndent < contentIndent)
                {
                    // Line doesn't belong to block scalar — rewind
                    _pos -= lineIndent;
                    _column -= lineIndent;
                    break;
                }

                // Document markers (--- or ...) at column 0 always terminate block scalars,
                // even when contentIndent is 0 and the marker chars would otherwise be valid content.
                if (lineIndent == 0
                    && (YamlCharacters.IsDocumentMarker(_buffer, _pos, _column, YamlConstants.DocumentStartMarker)
                        || YamlCharacters.IsDocumentMarker(_buffer, _pos, _column, YamlConstants.DocumentEndMarker)))
                {
                    break;
                }

                int extra = lineIndent - contentIndent;

                // A line is "more-indented" if it has extra indent spaces OR
                // starts with a whitespace character (tab) after the content indent.
                // Per YAML spec: s-nb-folded-text starts with s-white after indent.
                bool isMoreIndented = extra > 0 ||
                    (_pos < _buffer.Length && YamlCharacters.IsWhitespace(_buffer[_pos]));

                WriteBlockScalarSeparator(
                    literal, hasContent, prevLineMoreIndented, isMoreIndented,
                    blankLineCount, ref written, ref output, ref rentedBuffer);

                blankLineCount = 0;

                // Write extra indentation beyond the content indent
                EnsureOutputCapacity(ref output, ref rentedBuffer, written, extra);
                for (int i = 0; i < extra; i++)
                {
                    output[written++] = YamlConstants.Space;
                }

                // Copy content until line break
                while (_pos < _buffer.Length && !YamlCharacters.IsLineBreak(_buffer[_pos]))
                {
                    EnsureOutputCapacity(ref output, ref rentedBuffer, written, 1);
                    output[written++] = _buffer[_pos];
                    _pos++;
                    _column++;
                }

                if (_pos < _buffer.Length)
                {
                    SkipLineBreak();
                }

                hasContent = true;
                prevLineMoreIndented = isMoreIndented;
            }

            // After the loop: add the last content line's trailing break + trailing blank lines
            if (hasContent)
            {
                EnsureOutputCapacity(ref output, ref rentedBuffer, written, 1 + blankLineCount);
                output[written++] = YamlConstants.LineFeed;
                for (int i = 0; i < blankLineCount; i++)
                {
                    output[written++] = YamlConstants.LineFeed;
                }
            }
            else
            {
                // No content at all — only trailing blank lines
                EnsureOutputCapacity(ref output, ref rentedBuffer, written, blankLineCount);
                for (int i = 0; i < blankLineCount; i++)
                {
                    output[written++] = YamlConstants.LineFeed;
                }
            }

            ApplyChomping(_writer, output.Slice(0, written), chompMode);
        }
        finally
        {
            if (rentedBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
    }

    /// <summary>
    /// Writes the separator between block scalar content lines, handling literal vs folded modes.
    /// </summary>
    /// <remarks>
    /// <para>Folded mode rules:</para>
    /// <list type="bullet">
    /// <item>Normal → Normal, no blanks: fold to space</item>
    /// <item>Normal → anything, N blanks: N line feeds (content break consumed)</item>
    /// <item>More-indented → anything, N blanks: 1 + N line feeds (content break preserved)</item>
    /// <item>Either more-indented, no blanks: 1 line feed (preserved)</item>
    /// </list>
    /// <para>Literal mode: always 1 line feed + N blank line feeds.</para>
    /// </remarks>
    private static void WriteBlockScalarSeparator(
        bool literal,
        bool hasContent,
        bool prevMoreIndented,
        bool currentMoreIndented,
        int blankLines,
        ref int written,
        ref Span<byte> output,
        ref byte[]? rentedBuffer)
    {
        if (!hasContent)
        {
            // Leading blank lines before the first content line
            EnsureOutputCapacity(ref output, ref rentedBuffer, written, blankLines);
            for (int i = 0; i < blankLines; i++)
            {
                output[written++] = YamlConstants.LineFeed;
            }

            return;
        }

        if (literal)
        {
            // Literal mode: always preserve 1 LF (content break) + N LFs (blank lines)
            EnsureOutputCapacity(ref output, ref rentedBuffer, written, 1 + blankLines);
            output[written++] = YamlConstants.LineFeed;
            for (int i = 0; i < blankLines; i++)
            {
                output[written++] = YamlConstants.LineFeed;
            }
        }
        else
        {
            // Folded mode: preserve break when either side is more-indented
            if (prevMoreIndented || currentMoreIndented)
            {
                // More-indented on either side: content break is preserved, plus blank LFs
                int count = 1 + blankLines;
                EnsureOutputCapacity(ref output, ref rentedBuffer, written, count);
                output[written++] = YamlConstants.LineFeed;
                for (int i = 0; i < blankLines; i++)
                {
                    output[written++] = YamlConstants.LineFeed;
                }
            }
            else if (blankLines > 0)
            {
                // Normal→Normal with blank lines: b-l-trimmed consumes break, blanks become LFs
                EnsureOutputCapacity(ref output, ref rentedBuffer, written, blankLines);
                for (int i = 0; i < blankLines; i++)
                {
                    output[written++] = YamlConstants.LineFeed;
                }
            }
            else
            {
                // Normal→Normal, no blanks: fold to space
                EnsureOutputCapacity(ref output, ref rentedBuffer, written, 1);
                output[written++] = YamlConstants.Space;
            }
        }
    }

    /// <summary>
    /// Detects the indentation level of a block scalar by looking at the first non-empty line.
    /// Returns -1 if no non-empty line is found (all blank or EOF).
    /// </summary>
    private int DetectBlockScalarIndent()
    {
        int savedPos = _pos;
        int savedLine = _line;
        int savedColumn = _column;

        while (_pos < _buffer.Length)
        {
            int lineIndent = 0;
            while (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Space)
            {
                lineIndent++;
                _pos++;
            }

            if (_pos >= _buffer.Length || YamlCharacters.IsLineBreak(_buffer[_pos]))
            {
                // Empty line — skip
                if (_pos < _buffer.Length)
                {
                    SkipLineBreak();
                }

                continue;
            }

            // Document markers at column 0 are not block scalar content
            if (lineIndent == 0
                && (YamlCharacters.IsDocumentMarker(_buffer, _pos, 0, YamlConstants.DocumentStartMarker)
                    || YamlCharacters.IsDocumentMarker(_buffer, _pos, 0, YamlConstants.DocumentEndMarker)))
            {
                _pos = savedPos;
                _line = savedLine;
                _column = savedColumn;
                return -1;
            }

            // Found a non-empty line — its indent is the content indent
            _pos = savedPos;
            _line = savedLine;
            _column = savedColumn;
            return lineIndent;
        }

        _pos = savedPos;
        _line = savedLine;
        _column = savedColumn;
        return -1;
    }

    /// <summary>
    /// Applies chomping to a block scalar and writes the result.
    /// </summary>
    private static void ApplyChomping(Utf8JsonWriter writer, ReadOnlySpan<byte> content, int chompMode)
    {
        if (content.IsEmpty)
        {
            // No content at all — empty string regardless of chomping
            writer.WriteStringValue(ReadOnlySpan<byte>.Empty);
            return;
        }

        switch (chompMode)
        {
            case -1: // Strip — remove all trailing newlines
                int end = content.Length;
                while (end > 0 && content[end - 1] == YamlConstants.LineFeed)
                {
                    end--;
                }

                writer.WriteStringValue(content.Slice(0, end));
                break;

            case 0: // Clip — single trailing newline
                end = content.Length;
                while (end > 0 && content[end - 1] == YamlConstants.LineFeed)
                {
                    end--;
                }

                if (end < content.Length)
                {
                    // Add exactly one newline back
                    end++;
                }

                writer.WriteStringValue(content.Slice(0, end));
                break;

            case 1: // Keep — preserve all trailing newlines
                writer.WriteStringValue(content);
                break;
        }
    }

    /// <summary>
    /// Reads a hex escape sequence and writes the UTF-8 bytes to the output.
    /// </summary>
    /// <param name="output">The output buffer to write to.</param>
    /// <param name="hexDigits">The number of hex digits to read (2, 4, or 8).</param>
    /// <returns>The number of UTF-8 bytes written.</returns>
    private int WriteHexEscape(scoped Span<byte> output, int hexDigits)
    {
        if (_pos + hexDigits > _buffer.Length)
        {
            ThrowUnexpectedEnd();
        }

        uint codePoint = 0;
        for (int i = 0; i < hexDigits; i++)
        {
            byte c = _buffer[_pos];
            if (!YamlCharacters.IsHexDigit(c))
            {
                ThrowInvalidEscapeSequence(c);
            }

            codePoint = (codePoint << 4) | (uint)HexValue(c);
            Advance(1);
        }

        return EncodeUtf8(codePoint, output);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HexValue(byte c)
    {
        if ((uint)(c - (byte)'0') <= 9)
        {
            return c - (byte)'0';
        }

        if ((uint)(c - (byte)'a') <= 5)
        {
            return c - (byte)'a' + 10;
        }

        return c - (byte)'A' + 10;
    }

    /// <summary>
    /// Encodes a Unicode code point as UTF-8 bytes.
    /// </summary>
    private static int EncodeUtf8(uint codePoint, Span<byte> output)
    {
        if (codePoint <= 0x7F)
        {
            output[0] = (byte)codePoint;
            return 1;
        }

        if (codePoint <= 0x7FF)
        {
            output[0] = (byte)(0xC0 | (codePoint >> 6));
            output[1] = (byte)(0x80 | (codePoint & 0x3F));
            return 2;
        }

        if (codePoint <= 0xFFFF)
        {
            output[0] = (byte)(0xE0 | (codePoint >> 12));
            output[1] = (byte)(0x80 | ((codePoint >> 6) & 0x3F));
            output[2] = (byte)(0x80 | (codePoint & 0x3F));
            return 3;
        }

        output[0] = (byte)(0xF0 | (codePoint >> 18));
        output[1] = (byte)(0x80 | ((codePoint >> 12) & 0x3F));
        output[2] = (byte)(0x80 | ((codePoint >> 6) & 0x3F));
        output[3] = (byte)(0x80 | (codePoint & 0x3F));
        return 4;
    }

    /// <summary>
    /// Advances the position by the specified number of bytes, updating line/column tracking.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Advance(int count)
    {
        _pos += count;
        _column += count;
    }

    /// <summary>
    /// Advances to the next line (resets column to 0, increments line).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AdvanceLine()
    {
        _line++;
        _column = 0;
    }

    /// <summary>
    /// Skips a line break (LF, CR, or CRLF).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SkipLineBreak()
    {
        if (_pos < _buffer.Length)
        {
            if (_buffer[_pos] == YamlConstants.CarriageReturn)
            {
                _pos++;
                if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.LineFeed)
                {
                    _pos++;
                }
            }
            else if (_buffer[_pos] == YamlConstants.LineFeed)
            {
                _pos++;
            }

            AdvanceLine();
        }
    }

    /// <summary>
    /// Skips whitespace (space and tab) but NOT line breaks.
    /// </summary>
    private void SkipWhitespaceOnly()
    {
        while (_pos < _buffer.Length && YamlCharacters.IsWhitespace(_buffer[_pos]))
        {
            _pos++;
            _column++;
        }
    }

    /// <summary>
    /// Skips whitespace, line breaks, and comments.
    /// </summary>
    private void SkipWhitespaceAndComments()
    {
        while (_pos < _buffer.Length)
        {
            byte c = _buffer[_pos];

            if (YamlCharacters.IsWhitespace(c))
            {
                _pos++;
                _column++;
                continue;
            }

            if (YamlCharacters.IsLineBreak(c))
            {
                SkipLineBreak();
                continue;
            }

            if (c == YamlConstants.Hash)
            {
                SkipToEndOfLine();
                continue;
            }

            break;
        }
    }

    /// <summary>
    /// Skips to the end of the current line (used for comments).
    /// </summary>
    private void SkipToEndOfLine()
    {
        while (_pos < _buffer.Length && !YamlCharacters.IsLineBreak(_buffer[_pos]))
        {
            _pos++;
            _column++;
        }
    }

    /// <summary>
    /// Checks if the current position is a comment (# character).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsComment()
    {
        return _pos < _buffer.Length && _buffer[_pos] == YamlConstants.Hash;
    }

    /// <summary>
    /// Skips a UTF-8 BOM if present at the start of the buffer.
    /// </summary>
    private void SkipBom()
    {
        if (_buffer.Length >= 3
            && _buffer[0] == YamlConstants.Bom0
            && _buffer[1] == YamlConstants.Bom1
            && _buffer[2] == YamlConstants.Bom2)
        {
            _pos = 3;
            _column = 0;
        }
    }

    /// <summary>
    /// Attempts to skip a document start marker (---).
    /// </summary>
    /// <returns>True if a document start marker was skipped.</returns>
    private bool SkipDocumentStartMarker()
    {
        if (YamlCharacters.IsDocumentMarker(_buffer, _pos, _column, YamlConstants.DocumentStartMarker))
        {
            _pos += 3;
            _column += 3;
            SkipWhitespaceAndComments();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to skip a document end marker (...).
    /// </summary>
    private void SkipDocumentEndMarker()
    {
        if (YamlCharacters.IsDocumentMarker(_buffer, _pos, _column, YamlConstants.DocumentEndMarker))
        {
            _pos += 3;
            _column += 3;
        }
    }

    /// <summary>
    /// Checks if the current position is at a document end (...) or start (---) marker.
    /// </summary>
    private bool IsDocumentEndOrStart()
    {
        return IsDocumentEnd() || IsDocumentStart();
    }

    /// <summary>
    /// Checks if the current position is at a document end (...) marker.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsDocumentEnd()
    {
        return YamlCharacters.IsDocumentMarker(_buffer, _pos, _column, YamlConstants.DocumentEndMarker);
    }

    /// <summary>
    /// Checks if the current position is at a document start (---) marker.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsDocumentStart()
    {
        return YamlCharacters.IsDocumentMarker(_buffer, _pos, _column, YamlConstants.DocumentStartMarker);
    }

    /// <summary>
    /// Reads an anchor (&amp;name) and stores its name for later association with a value.
    /// </summary>
    private void ReadAnchorName()
    {
        if (_pos >= _buffer.Length || _buffer[_pos] != YamlConstants.Ampersand)
        {
            return;
        }

        Advance(1); // skip '&'
        int nameStart = _pos;

        while (_pos < _buffer.Length
            && !YamlCharacters.IsWhitespaceOrLineBreak(_buffer[_pos])
            && !YamlCharacters.IsFlowIndicator(_buffer[_pos]))
        {
            Advance(1);
        }

        int nameLength = _pos - nameStart;

        // Store the anchor name in the name buffer
        EnsureAnchorNameCapacity(nameLength);
        _buffer.Slice(nameStart, nameLength).CopyTo(_anchorNameBuffer.AsSpan(_anchorNamesUsed));
        _activeAnchorNameStart = _anchorNamesUsed;
        _activeAnchorNameLength = nameLength;
        _anchorNamesUsed += nameLength;
    }

    /// <summary>
    /// Reads an alias (*name) and writes the anchored value to the JSON writer.
    /// </summary>
    private void ConvertAlias()
    {
        if (_pos >= _buffer.Length || _buffer[_pos] != YamlConstants.Asterisk)
        {
            return;
        }

        Advance(1); // skip '*'
        int nameStart = _pos;

        while (_pos < _buffer.Length
            && !YamlCharacters.IsWhitespaceOrLineBreak(_buffer[_pos])
            && !YamlCharacters.IsFlowIndicator(_buffer[_pos]))
        {
            Advance(1);
        }

        ReadOnlySpan<byte> aliasName = _buffer.Slice(nameStart, _pos - nameStart);

        // Look up the anchor in the table
        if (TryGetAnchorData(aliasName, out ReadOnlySpan<byte> anchorData))
        {
            _writer.WriteRawValue(anchorData, skipInputValidation: true);
        }
        else
        {
            // Undefined alias — write null
            _writer.WriteNullValue();
        }
    }

    /// <summary>
    /// Reads an alias name and writes the anchored value as a JSON property name.
    /// </summary>
    private void WriteAliasAsPropertyName()
    {
        Advance(1); // skip '*'
        int nameStart = _pos;

        while (_pos < _buffer.Length
            && !YamlCharacters.IsWhitespaceOrLineBreak(_buffer[_pos])
            && !YamlCharacters.IsFlowIndicator(_buffer[_pos]))
        {
            Advance(1);
        }

        ReadOnlySpan<byte> aliasName = _buffer.Slice(nameStart, _pos - nameStart);

        if (TryGetAnchorData(aliasName, out ReadOnlySpan<byte> anchorData))
        {
            // The stored data is JSON. For a property name, we need the raw text.
            // If it's a JSON string like "foo", strip the quotes to get foo.
            // Otherwise (number, bool, null, object, array), use the raw JSON as key.
            if (anchorData.Length >= 2 && anchorData[0] == (byte)'"' && anchorData[anchorData.Length - 1] == (byte)'"')
            {
                ReadOnlySpan<byte> inner = anchorData.Slice(1, anchorData.Length - 2);

                // Check if the inner content has JSON escapes
                if (inner.IndexOf((byte)'\\') < 0)
                {
                    // No escapes — use directly
                    _writer.WritePropertyName(inner);
                }
                else
                {
                    // Has escapes — need to unescape. Write property name from the full
                    // anchor data as raw value, letting the writer handle encoding.
                    // For JSON-escaped strings, we decode inline.
                    _writer.WritePropertyName(UnescapeJsonString(inner));
                }
            }
            else
            {
                // Non-string value — use raw JSON as property name
                _writer.WritePropertyName(anchorData);
            }
        }
        else
        {
            // Undefined alias
            _writer.WritePropertyName(ReadOnlySpan<byte>.Empty);
        }
    }

    /// <summary>
    /// Finishes anchor capture after the anchored value has been written.
    /// </summary>
    private void FinishAnchor(bool hasAnchor, long anchorStartPos)
    {
        if (!hasAnchor)
        {
            // No anchor on this node — do NOT clear _activeAnchorNameStart,
            // because a parent ConvertNode may still be tracking its own anchor.
            return;
        }

        if (_bufferWriter == null || _activeAnchorNameStart < 0)
        {
            _activeAnchorNameStart = -1;
            return;
        }

        _writer.Flush();
        long anchorEndPos = _bufferWriter.WrittenSpan.Length;
        int dataStart = (int)anchorStartPos;
        int dataLength = (int)(anchorEndPos - anchorStartPos);

        if (dataLength > 0)
        {
            // The Utf8JsonWriter may have prepended a list separator (comma) before the value.
            // Strip it so the anchor data contains only the pure JSON value.
            ReadOnlySpan<byte> captured = _bufferWriter.WrittenSpan.Slice(dataStart, dataLength);
            if (captured[0] == (byte)',')
            {
                dataStart++;
                dataLength--;
                captured = captured.Slice(1);
            }

            // Store the value data
            EnsureAnchorDataCapacity(dataLength);
            captured.CopyTo(_anchorDataBuffer.AsSpan(_anchorDataUsed));

            // Store the entry
            EnsureAnchorEntryCapacity();
            int entryBase = _anchorCount * AnchorEntrySize;
            _anchorEntries![entryBase] = _activeAnchorNameStart;
            _anchorEntries[entryBase + 1] = _activeAnchorNameLength;
            _anchorEntries[entryBase + 2] = _anchorDataUsed;
            _anchorEntries[entryBase + 3] = dataLength;
            _anchorCount++;
            _anchorDataUsed += dataLength;
        }
        else
        {
            // Anchor on null/empty value — store "null"
            ReadOnlySpan<byte> nullBytes = "null"u8;
            EnsureAnchorDataCapacity(nullBytes.Length);
            nullBytes.CopyTo(_anchorDataBuffer.AsSpan(_anchorDataUsed));

            EnsureAnchorEntryCapacity();
            int entryBase = _anchorCount * AnchorEntrySize;
            _anchorEntries![entryBase] = _activeAnchorNameStart;
            _anchorEntries[entryBase + 1] = _activeAnchorNameLength;
            _anchorEntries[entryBase + 2] = _anchorDataUsed;
            _anchorEntries[entryBase + 3] = nullBytes.Length;
            _anchorCount++;
            _anchorDataUsed += nullBytes.Length;
        }

        _activeAnchorNameStart = -1;
    }

    /// <summary>
    /// Stores the current active anchor with a JSON string value derived from
    /// the given key bytes. This is used for anchors on mapping keys, which
    /// are not captured by the normal FinishAnchor mechanism (since keys are
    /// written via WritePropertyName, not WriteStringValue).
    /// </summary>
    private void StoreKeyAnchor(ReadOnlySpan<byte> keyBytes)
    {
        if (_activeAnchorNameStart < 0)
        {
            return;
        }

        // Build the JSON string representation: "keyBytes" with escaping
        // First, compute the needed length: 2 for quotes + escaped content
        int escapedLength = 0;
        for (int i = 0; i < keyBytes.Length; i++)
        {
            byte b = keyBytes[i];
            if (b == (byte)'"' || b == (byte)'\\' || b < 0x20)
            {
                // Control chars use \uXXXX (6 bytes), " and \ use 2 bytes
                escapedLength += b < 0x20 ? 6 : 2;
            }
            else
            {
                escapedLength++;
            }
        }

        int totalLength = escapedLength + 2; // 2 for surrounding quotes
        EnsureAnchorDataCapacity(totalLength);
        Span<byte> dest = _anchorDataBuffer.AsSpan(_anchorDataUsed, totalLength);
        dest[0] = (byte)'"';
        int w = 1;
        for (int i = 0; i < keyBytes.Length; i++)
        {
            byte b = keyBytes[i];
            if (b == (byte)'"')
            {
                dest[w++] = (byte)'\\';
                dest[w++] = (byte)'"';
            }
            else if (b == (byte)'\\')
            {
                dest[w++] = (byte)'\\';
                dest[w++] = (byte)'\\';
            }
            else if (b < 0x20)
            {
                dest[w++] = (byte)'\\';
                dest[w++] = (byte)'u';
                dest[w++] = (byte)'0';
                dest[w++] = (byte)'0';
                dest[w++] = HexDigit(b >> 4);
                dest[w++] = HexDigit(b & 0x0F);
            }
            else
            {
                dest[w++] = b;
            }
        }

        dest[w] = (byte)'"';

        EnsureAnchorEntryCapacity();
        int entryBase = _anchorCount * AnchorEntrySize;
        _anchorEntries![entryBase] = _activeAnchorNameStart;
        _anchorEntries[entryBase + 1] = _activeAnchorNameLength;
        _anchorEntries[entryBase + 2] = _anchorDataUsed;
        _anchorEntries[entryBase + 3] = totalLength;
        _anchorCount++;
        _anchorDataUsed += totalLength;
        _activeAnchorNameStart = -1;

        static byte HexDigit(int v) => (byte)(v < 10 ? '0' + v : 'a' + v - 10);
    }

    /// <summary>
    /// Looks up an anchor by name and returns its JSON value data.
    /// </summary>
    private bool TryGetAnchorData(ReadOnlySpan<byte> name, out ReadOnlySpan<byte> data)
    {
        // Search backwards to find the most recent definition (handles redefinition)
        for (int i = _anchorCount - 1; i >= 0; i--)
        {
            int entryBase = i * AnchorEntrySize;
            int nameOffset = _anchorEntries![entryBase];
            int nameLength = _anchorEntries[entryBase + 1];
            int dataOffset = _anchorEntries[entryBase + 2];
            int dataLength = _anchorEntries[entryBase + 3];

            if (name.SequenceEqual(_anchorNameBuffer.AsSpan(nameOffset, nameLength)))
            {
                data = _anchorDataBuffer.AsSpan(dataOffset, dataLength);
                return true;
            }
        }

        data = default;
        return false;
    }

    /// <summary>
    /// Resets the anchor table for a new document.
    /// </summary>
    private void ResetAnchorTable()
    {
        _anchorNamesUsed = 0;
        _anchorDataUsed = 0;
        _anchorCount = 0;
        _activeAnchorNameStart = -1;
        _secondaryTagRedefined = false;
    }

    private void EnsureAnchorNameCapacity(int additional)
    {
        int required = _anchorNamesUsed + additional;
        if (_anchorNameBuffer == null || required > _anchorNameBuffer.Length)
        {
            byte[] newBuffer = ArrayPool<byte>.Shared.Rent(Math.Max(required, 256));
            if (_anchorNameBuffer != null)
            {
                _anchorNameBuffer.AsSpan(0, _anchorNamesUsed).CopyTo(newBuffer);
                ArrayPool<byte>.Shared.Return(_anchorNameBuffer);
            }

            _anchorNameBuffer = newBuffer;
        }
    }

    private void EnsureAnchorDataCapacity(int additional)
    {
        int required = _anchorDataUsed + additional;
        if (_anchorDataBuffer == null || required > _anchorDataBuffer.Length)
        {
            byte[] newBuffer = ArrayPool<byte>.Shared.Rent(Math.Max(required, 1024));
            if (_anchorDataBuffer != null)
            {
                _anchorDataBuffer.AsSpan(0, _anchorDataUsed).CopyTo(newBuffer);
                ArrayPool<byte>.Shared.Return(_anchorDataBuffer);
            }

            _anchorDataBuffer = newBuffer;
        }
    }

    private void EnsureAnchorEntryCapacity()
    {
        int required = (_anchorCount + 1) * AnchorEntrySize;
        if (_anchorEntries == null || required > _anchorEntries.Length)
        {
            int[] newEntries = ArrayPool<int>.Shared.Rent(Math.Max(required, 16 * AnchorEntrySize));
            if (_anchorEntries != null)
            {
                _anchorEntries.AsSpan(0, _anchorCount * AnchorEntrySize).CopyTo(newEntries);
                ArrayPool<int>.Shared.Return(_anchorEntries);
            }

            _anchorEntries = newEntries;
        }
    }

    private void ReturnAnchorBuffers()
    {
        if (_anchorNameBuffer != null)
        {
            ArrayPool<byte>.Shared.Return(_anchorNameBuffer);
            _anchorNameBuffer = null;
        }

        if (_anchorDataBuffer != null)
        {
            ArrayPool<byte>.Shared.Return(_anchorDataBuffer);
            _anchorDataBuffer = null;
        }

        if (_anchorEntries != null)
        {
            ArrayPool<int>.Shared.Return(_anchorEntries);
            _anchorEntries = null;
        }
    }

    /// <summary>
    /// Unescapes a JSON-encoded string (without surrounding quotes) by processing
    /// backslash escape sequences.
    /// </summary>
    private static ReadOnlySpan<byte> UnescapeJsonString(ReadOnlySpan<byte> escaped)
    {
        // Fast path: no escapes
        if (escaped.IndexOf((byte)'\\') < 0)
        {
            return escaped;
        }

        // Slow path: unescape into rented buffer
        byte[] buffer = ArrayPool<byte>.Shared.Rent(escaped.Length);
        int written = 0;

        for (int i = 0; i < escaped.Length; i++)
        {
            if (escaped[i] == (byte)'\\' && i + 1 < escaped.Length)
            {
                i++;
                switch (escaped[i])
                {
                    case (byte)'"': buffer[written++] = (byte)'"'; break;
                    case (byte)'\\': buffer[written++] = (byte)'\\'; break;
                    case (byte)'/': buffer[written++] = (byte)'/'; break;
                    case (byte)'n': buffer[written++] = (byte)'\n'; break;
                    case (byte)'t': buffer[written++] = (byte)'\t'; break;
                    case (byte)'r': buffer[written++] = (byte)'\r'; break;
                    case (byte)'b': buffer[written++] = (byte)'\b'; break;
                    case (byte)'f': buffer[written++] = (byte)'\f'; break;
                    default: buffer[written++] = escaped[i]; break;
                }
            }
            else
            {
                buffer[written++] = escaped[i];
            }
        }

        // Note: This returns a span into a rented array. The caller must use it before the array is returned.
        // In practice, WritePropertyName copies the data immediately.
        return buffer.AsSpan(0, written);
    }

    /// <summary>
    /// Reads a tag annotation and returns the resolved tag kind.
    /// </summary>
    private YamlTagKind ReadTag()
    {
        if (_pos >= _buffer.Length || _buffer[_pos] != YamlConstants.Exclamation)
        {
            return YamlTagKind.None;
        }

        Advance(1); // skip first '!'

        if (_pos >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[_pos]))
        {
            // Bare '!' — non-specific tag
            return YamlTagKind.NonSpecific;
        }

        if (_buffer[_pos] == YamlConstants.Exclamation)
        {
            // Secondary tag handle: !!name
            Advance(1);
            int nameStart = _pos;
            while (_pos < _buffer.Length
                && !YamlCharacters.IsWhitespaceOrLineBreak(_buffer[_pos])
                && !YamlCharacters.IsFlowIndicator(_buffer[_pos]))
            {
                Advance(1);
            }

            // If %TAG !! was redefined to a non-standard prefix,
            // !!name is NOT a standard YAML type.
            if (_secondaryTagRedefined)
            {
                return YamlTagKind.Other;
            }

            return ClassifyStandardTag(_buffer.Slice(nameStart, _pos - nameStart));
        }

        if (_buffer[_pos] == (byte)'<')
        {
            // Verbatim tag: !<uri>
            Advance(1); // skip '<'
            int uriStart = _pos;
            while (_pos < _buffer.Length && _buffer[_pos] != (byte)'>')
            {
                Advance(1);
            }

            ReadOnlySpan<byte> uri = _buffer.Slice(uriStart, _pos - uriStart);
            if (_pos < _buffer.Length)
            {
                Advance(1); // skip '>'
            }

            return ClassifyVerbatimTag(uri);
        }

        // Local or named handle tag: !name or !prefix!suffix
        while (_pos < _buffer.Length
            && !YamlCharacters.IsWhitespaceOrLineBreak(_buffer[_pos])
            && !YamlCharacters.IsFlowIndicator(_buffer[_pos]))
        {
            Advance(1);
        }

        return YamlTagKind.Other;
    }

    /// <summary>
    /// Classifies a standard YAML tag name (after !!).
    /// </summary>
    private static YamlTagKind ClassifyStandardTag(ReadOnlySpan<byte> name)
    {
        if (name.SequenceEqual("str"u8))
        {
            return YamlTagKind.Str;
        }

        if (name.SequenceEqual("int"u8))
        {
            return YamlTagKind.Int;
        }

        if (name.SequenceEqual("float"u8))
        {
            return YamlTagKind.Float;
        }

        if (name.SequenceEqual("bool"u8))
        {
            return YamlTagKind.Bool;
        }

        if (name.SequenceEqual("null"u8))
        {
            return YamlTagKind.Null;
        }

        if (name.SequenceEqual("seq"u8))
        {
            return YamlTagKind.Seq;
        }

        if (name.SequenceEqual("map"u8))
        {
            return YamlTagKind.Map;
        }

        return YamlTagKind.Other;
    }

    /// <summary>
    /// Classifies a verbatim tag URI (e.g., tag:yaml.org,2002:str).
    /// </summary>
    private static YamlTagKind ClassifyVerbatimTag(ReadOnlySpan<byte> uri)
    {
        ReadOnlySpan<byte> prefix = "tag:yaml.org,2002:"u8;
        if (uri.StartsWith(prefix))
        {
            return ClassifyStandardTag(uri.Slice(prefix.Length));
        }

        return YamlTagKind.Other;
    }

    /// <summary>
    /// Parses a %YAML or %TAG directive line. Detects when %TAG redefines
    /// the secondary tag handle (!!).
    /// </summary>
    private void SkipDirective()
    {
        if (_pos >= _buffer.Length || _buffer[_pos] != YamlConstants.Percent)
        {
            return;
        }

        int directiveStart = _pos;

        // Check for %TAG !! with non-standard prefix using lookahead
        // without advancing position, then skip the whole line.
        ReadOnlySpan<byte> tagSecondary = "%TAG !!"u8;
        if (_pos + tagSecondary.Length <= _buffer.Length
            && _buffer.Slice(_pos, tagSecondary.Length).SequenceEqual(tagSecondary))
        {
            // Found %TAG !! — check if the prefix is non-standard
            int p = _pos + tagSecondary.Length;

            // Skip whitespace
            while (p < _buffer.Length && _buffer[p] == YamlConstants.Space)
            {
                p++;
            }

            // Read the prefix
            int prefixStart = p;
            while (p < _buffer.Length && !YamlCharacters.IsWhitespaceOrLineBreak(_buffer[p]))
            {
                p++;
            }

            ReadOnlySpan<byte> prefix = _buffer.Slice(prefixStart, p - prefixStart);
            ReadOnlySpan<byte> standardPrefix = "tag:yaml.org,2002:"u8;

            if (!prefix.SequenceEqual(standardPrefix))
            {
                _secondaryTagRedefined = true;
            }
        }

        // Skip the entire directive line
        SkipToEndOfLine();
        if (_pos < _buffer.Length)
        {
            SkipLineBreak();
        }
    }

    /// <summary>
    /// Skips all leading directives (%YAML, %TAG, etc.) and their line breaks.
    /// </summary>
    private void SkipDirectives()
    {
        while (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Percent)
        {
            SkipDirective();
            SkipWhitespaceAndComments();
        }
    }

    /// <summary>
    /// Enters a new nesting level, throwing if the maximum depth is exceeded.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnterDepth()
    {
        _depth++;
        if (_depth > YamlConstants.MaxNestingDepth)
        {
            ThrowMaxNestingDepthExceeded();
        }
    }

    /// <summary>
    /// Ensures the output buffer has capacity for additional bytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EnsureOutputCapacity(ref Span<byte> output, ref byte[]? rentedBuffer, int written, int additional)
    {
        if (written + additional > output.Length)
        {
            GrowOutputBuffer(ref output, ref rentedBuffer, written, additional);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void GrowOutputBuffer(ref Span<byte> output, ref byte[]? rentedBuffer, int written, int additional)
    {
        int newSize = Math.Max(output.Length * 2, written + additional);
        byte[] newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
        output.Slice(0, written).CopyTo(newBuffer);

        if (rentedBuffer != null)
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }

        rentedBuffer = newBuffer;
        output = newBuffer;
    }

    /// <summary>
    /// Ensures a rented byte array has capacity, growing if needed.
    /// Used for escape processing where the buffer is always heap-allocated.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EnsureRentedCapacity(ref byte[] rentedBuffer, ref Span<byte> output, int written, int additional)
    {
        if (written + additional > output.Length)
        {
            GrowRentedBuffer(ref rentedBuffer, ref output, written, additional);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void GrowRentedBuffer(ref byte[] rentedBuffer, ref Span<byte> output, int written, int additional)
    {
        int newSize = Math.Max(rentedBuffer.Length * 2, written + additional);
        byte[] newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
        output.Slice(0, written).CopyTo(newBuffer);
        ArrayPool<byte>.Shared.Return(rentedBuffer);
        rentedBuffer = newBuffer;
        output = newBuffer;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowUnexpectedEnd()
    {
        throw new YamlException(
            SR.Format(SR.UnexpectedEndOfInput, _line, _column),
            _line,
            _column);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowUnexpectedCharacter()
    {
        byte c = _pos < _buffer.Length ? _buffer[_pos] : (byte)0;
        throw new YamlException(
            SR.Format(SR.UnexpectedCharacter, (char)c, _line, _column),
            _line,
            _column);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowDuplicateKey()
    {
        throw new YamlException(
            SR.Format(SR.DuplicateKey, _line, _column),
            _line,
            _column);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowMultipleDocuments()
    {
        throw new YamlException(
            SR.MultipleDocumentsNotAllowed,
            _line,
            _column);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowInvalidEscapeSequence(byte c)
    {
        throw new YamlException(
            SR.Format(SR.InvalidEscapeSequence, (char)c, _line, _column),
            _line,
            _column);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowUnterminatedDoubleQuotedScalar()
    {
        throw new YamlException(
            SR.Format(SR.UnterminatedDoubleQuotedScalar, _line, _column),
            _line,
            _column);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowUnterminatedSingleQuotedScalar()
    {
        throw new YamlException(
            SR.Format(SR.UnterminatedSingleQuotedScalar, _line, _column),
            _line,
            _column);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowUnterminatedFlowMapping()
    {
        throw new YamlException(
            SR.Format(SR.UnterminatedFlowMapping, _line, _column),
            _line,
            _column);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowUnterminatedFlowSequence()
    {
        throw new YamlException(
            SR.Format(SR.UnterminatedFlowSequence, _line, _column),
            _line,
            _column);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowMaxNestingDepthExceeded()
    {
        throw new YamlException(
            SR.Format(SR.MaxNestingDepthExceeded, YamlConstants.MaxNestingDepth, _line, _column),
            _line,
            _column);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowExpectedMappingValue()
    {
        throw new YamlException(
            SR.Format(SR.ExpectedMappingValue, _line, _column),
            _line,
            _column);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowInvalidBlockScalarHeader()
    {
        throw new YamlException(
            SR.Format(SR.InvalidBlockScalarHeader, _line, _column),
            _line,
            _column);
    }
}