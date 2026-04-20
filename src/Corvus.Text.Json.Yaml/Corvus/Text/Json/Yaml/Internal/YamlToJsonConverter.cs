// <copyright file="YamlToJsonConverter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Runtime.CompilerServices;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Yaml.Internal;

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
    private readonly ReadOnlySpan<byte> _buffer;
    private readonly Utf8JsonWriter _writer;
    private readonly YamlReaderOptions _options;
    private int _pos;
    private int _line;
    private int _column;
    private int _depth;

    /// <summary>
    /// Initializes a new converter for the given YAML buffer.
    /// </summary>
    /// <param name="yaml">The UTF-8 YAML bytes.</param>
    /// <param name="writer">The JSON writer to emit to.</param>
    /// <param name="options">The YAML reader options.</param>
    public YamlToJsonConverter(ReadOnlySpan<byte> yaml, Utf8JsonWriter writer, YamlReaderOptions options)
    {
        _buffer = yaml;
        _writer = writer;
        _options = options;
        _pos = 0;
        _line = 1;
        _column = 0;
        _depth = 0;
    }

    /// <summary>
    /// Converts the YAML content to JSON, writing the result to the writer.
    /// </summary>
    public void Convert()
    {
        SkipBom();

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
            return;
        }

        // Parse the single document value
        ConvertNode(-1, false);
        SkipWhitespaceAndComments();

        // Handle document end marker
        SkipDocumentEndMarker();
        SkipWhitespaceAndComments();

        // Skip any trailing directives and document start markers (second document)
        if (_pos < _buffer.Length)
        {
            // If there's another document, that's multi-doc
            SkipDirectives();
            SkipWhitespaceAndComments();

            if (_pos < _buffer.Length && SkipDocumentStartMarker())
            {
                SkipWhitespaceAndComments();

                // If there's actual content after the second ---, it's truly multi-document
                if (_pos < _buffer.Length && !IsDocumentEndOrStart())
                {
                    if (_options.DocumentMode == YamlDocumentMode.SingleRequired)
                    {
                        ThrowMultipleDocuments();
                    }

                    ThrowMultipleDocuments();
                }
            }

            // Any remaining non-whitespace after doc end is ignored (trailing content)
            SkipWhitespaceAndComments();
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

        // Skip anchor and tag annotations
        SkipAnchor();
        SkipWhitespaceAndComments();
        SkipTag();
        SkipWhitespaceAndComments();

        if (_pos >= _buffer.Length)
        {
            _writer.WriteNullValue();
            return;
        }

        byte c = _buffer[_pos];

        // Flow collections
        if (c == YamlConstants.OpenBrace)
        {
            ConvertFlowMapping();
            return;
        }

        if (c == YamlConstants.OpenBracket)
        {
            ConvertFlowSequence();
            return;
        }

        // Block scalar indicators
        if (c == YamlConstants.Pipe || c == YamlConstants.GreaterThan)
        {
            ConvertBlockScalar(parentIndent);
            return;
        }

        // Alias
        if (c == YamlConstants.Asterisk)
        {
            // Alias expansion: for now, write null (phase 4 will implement full anchor/alias)
            SkipAlias();
            _writer.WriteNullValue();
            return;
        }

        // Block sequence indicator
        if (c == YamlConstants.Dash && IsBlockSequenceEntry())
        {
            ConvertBlockSequence(parentIndent);
            return;
        }

        // Check for block mapping by looking ahead for a mapping value indicator.
        // This must be checked BEFORE quoted scalars because a quoted string followed
        // by ': ' is a mapping key, not a standalone value.
        int nodeColumn = _column;
        if (IsBlockMappingStart(inFlow))
        {
            ConvertBlockMapping(parentIndent, nodeColumn);
            return;
        }

        // Quoted scalars (only reached when NOT a mapping key)
        if (c == YamlConstants.DoubleQuote)
        {
            ConvertDoubleQuotedScalar(parentIndent, inFlow, asValue: true);
            return;
        }

        if (c == YamlConstants.SingleQuote)
        {
            ConvertSingleQuotedScalar(asValue: true);
            return;
        }

        // Plain scalar
        ConvertPlainScalar(parentIndent, inFlow);
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
    private void ConvertBlockMapping(int parentIndent, int mappingIndent)
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

                // Skip anchor/tag on key
                SkipAnchor();
                SkipWhitespaceAndComments();
                SkipTag();
                SkipWhitespaceAndComments();

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
                    }
                }

                // Read the key
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
                }
                finally
                {
                    if (rentedKeyBuffer != null)
                    {
                        ArrayPool<byte>.Shared.Return(rentedKeyBuffer);
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

                // Skip anchor/tag
                SkipAnchor();
                SkipWhitespaceAndComments();
                SkipTag();
                SkipWhitespaceAndComments();

                // Read key
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

        // Skip anchor if present
        SkipAnchor();
        SkipWhitespaceOnly();

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
    /// with type resolution based on the configured schema.
    /// </summary>
    private void ConvertPlainScalar(int parentIndent, bool inFlow)
    {
        ReadOnlySpan<byte> value = ReadPlainScalarValue(parentIndent, inFlow);

        // If value is empty and we're past the start, ReadPlainScalarValue already wrote
        // (multi-line path writes directly)
        if (value.IsEmpty)
        {
            return;
        }

        ScalarResolver.ScalarType scalarType = ScalarResolver.Resolve(value, _options.Schema);
        ScalarResolver.WriteScalar(_writer, value, scalarType);
    }

    /// <summary>
    /// Reads a plain scalar value (potentially multi-line with line folding).
    /// </summary>
    private ReadOnlySpan<byte> ReadPlainScalarValue(int parentIndent, bool inFlow)
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

            // Write via the scalar resolver
            ReadOnlySpan<byte> value = rentedBuffer.AsSpan(0, written);
            ScalarResolver.ScalarType scalarType = ScalarResolver.Resolve(value, _options.Schema);
            ScalarResolver.WriteScalar(_writer, value, scalarType);

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
        return YamlCharacters.IsDocumentMarker(_buffer, _pos, _column, YamlConstants.DocumentEndMarker)
            || YamlCharacters.IsDocumentMarker(_buffer, _pos, _column, YamlConstants.DocumentStartMarker);
    }

    /// <summary>
    /// Skips an anchor (&amp;name) annotation.
    /// </summary>
    private void SkipAnchor()
    {
        if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Ampersand)
        {
            Advance(1);
            while (_pos < _buffer.Length
                && !YamlCharacters.IsWhitespaceOrLineBreak(_buffer[_pos])
                && !YamlCharacters.IsFlowIndicator(_buffer[_pos]))
            {
                Advance(1);
            }
        }
    }

    /// <summary>
    /// Skips an alias (*name) reference.
    /// </summary>
    private void SkipAlias()
    {
        if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Asterisk)
        {
            Advance(1);
            while (_pos < _buffer.Length
                && !YamlCharacters.IsWhitespaceOrLineBreak(_buffer[_pos])
                && !YamlCharacters.IsFlowIndicator(_buffer[_pos]))
            {
                Advance(1);
            }
        }
    }

    /// <summary>
    /// Skips a tag (!tag or !!tag) annotation.
    /// </summary>
    private void SkipTag()
    {
        if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Exclamation)
        {
            Advance(1);
            if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Exclamation)
            {
                Advance(1);
            }

            while (_pos < _buffer.Length
                && !YamlCharacters.IsWhitespaceOrLineBreak(_buffer[_pos])
                && !YamlCharacters.IsFlowIndicator(_buffer[_pos]))
            {
                Advance(1);
            }
        }
    }

    /// <summary>
    /// Skips a %YAML or %TAG directive line.
    /// </summary>
    private void SkipDirective()
    {
        if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Percent)
        {
            SkipToEndOfLine();
            if (_pos < _buffer.Length)
            {
                SkipLineBreak();
            }
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