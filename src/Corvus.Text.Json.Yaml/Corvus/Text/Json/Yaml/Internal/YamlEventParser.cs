// <copyright file="YamlEventParser.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;

#if STJ
namespace Corvus.Yaml.Internal;
#else
namespace Corvus.Text.Json.Yaml.Internal;
#endif

internal ref struct YamlEventParser
{
    private readonly ReadOnlySpan<byte> _buffer;
    private readonly YamlEventCallback _callback;
    private readonly YamlReaderOptions _options;
    private int _pos;
    private int _line;
    private int _column;
    private int _depth;
    private byte[] _scratchBuffer;
    private int _scratchPos;

    // Tag handle storage: up to 8 handles per document
    // Each handle is stored as a pair of (handleStart, handleLen, prefixStart, prefixLen) in _tagHandles
    private int _tagHandleCount;
    private int[] _tagHandles; // 4 ints per entry: handleStart, handleLen, prefixStart, prefixLen
    private byte[] _tagHandleData; // concatenated handle+prefix strings

    // When an anchor/tag appears on the same line as a scalar key followed by ':',
    // the anchor/tag belongs to the key, not the mapping. These fields carry the
    // anchor/tag from the outer ParseNode into the first key's ParseNode call.
    private int _pendingKeyAnchorStart; // offset into _buffer
    private int _pendingKeyAnchorLength;
    private byte[]? _pendingKeyTagBuffer; // separate buffer for resolved tag (may not be in _buffer)
    private int _pendingKeyTagLength;

    // Tracked during ParseNodeProperties for anchor-on-key detection
    private int _lastAnchorStart;
    private int _lastAnchorLength;
    private int _lastAnchorLine;

    // Track whether directives were seen in the current document
    private bool _hasDirectives;
    private bool _hasYamlDirective;

    // Set by SkipWhitespaceAndComments when tab is seen at line start
    private bool _tabSeenInLeadingWhitespace;

    internal YamlEventParser(ReadOnlySpan<byte> buffer, YamlEventCallback callback, YamlReaderOptions options)
    {
        _buffer = buffer;
        _callback = callback;
        _options = options;
        _pos = 0;
        _line = 0;
        _column = 0;
        _depth = 0;
        _scratchBuffer = ArrayPool<byte>.Shared.Rent(256);
        _scratchPos = 0;
        _tagHandleCount = 0;
        _tagHandles = ArrayPool<int>.Shared.Rent(32);
        _tagHandleData = ArrayPool<byte>.Shared.Rent(256);
    }

    internal bool Parse()
    {
        try
        {
            return ParseAllDocuments();
        }
        finally
        {
            if (_scratchBuffer != null) { ArrayPool<byte>.Shared.Return(_scratchBuffer); }

            if (_tagHandles != null) { ArrayPool<int>.Shared.Return(_tagHandles); }

            if (_tagHandleData != null) { ArrayPool<byte>.Shared.Return(_tagHandleData); }

            if (_pendingKeyTagBuffer != null) { ArrayPool<byte>.Shared.Return(_pendingKeyTagBuffer); }
        }
    }

    private bool EmitEvent(YamlEventType type, int line, int column)
    {
        YamlEvent e = new(type, line, column);
        return _callback(in e);
    }

    private bool EmitDocumentEvent(YamlEventType type, bool isImplicit, int line, int column)
    {
        YamlEvent e = new(type, isImplicit, line, column);
        return _callback(in e);
    }

    private bool EmitCollectionStartEvent(YamlEventType type, int line, int column, ReadOnlySpan<byte> anchor, ReadOnlySpan<byte> tag, bool isFlowStyle = false)
    {
        YamlEvent e = new(type, anchor, tag, isFlowStyle, line, column);
        return _callback(in e);
    }

    private bool EmitScalarEvent(int line, int column, ReadOnlySpan<byte> value, YamlScalarStyle style, ReadOnlySpan<byte> anchor, ReadOnlySpan<byte> tag)
    {
        YamlEvent e = new(value, style, anchor, tag, line, column);
        return _callback(in e);
    }

    private bool EmitAliasEvent(ReadOnlySpan<byte> name, int line, int column)
    {
        YamlEvent e = YamlEvent.Alias(name, line, column);
        return _callback(in e);
    }

    private bool ParseAllDocuments()
    {
        if (!EmitEvent(YamlEventType.StreamStart, 1, 1)) { return false; }

        SkipBom();
        SkipWhitespaceAndComments();
        bool firstDoc = true;

        while (_pos < _buffer.Length)
        {
            // Skip document-end markers between documents
            while (IsDocumentEnd())
            {
                Advance(3);

                // After ..., rest of line must be whitespace/comment only
                SkipWhitespaceOnly();

                if (_pos < _buffer.Length && !YamlCharacters.IsLineBreak(_buffer[_pos]) && _buffer[_pos] != YamlConstants.Hash)
                {
                    ThrowContentAfterDocumentEndMarker();
                }

                SkipWhitespaceAndComments();
            }

            if (_pos >= _buffer.Length) { break; }

            if (!ParseSingleDocument(firstDoc)) { return false; }

            firstDoc = false;
            SkipWhitespaceAndComments();
        }

        return EmitEvent(YamlEventType.StreamEnd, _line + 1, _column + 1);
    }

    private bool ParseSingleDocument(bool firstDoc)
    {
        SkipDirectives();
        SkipWhitespaceAndComments();

        if (IsDocumentStart())
        {
            if (!EmitDocumentEvent(YamlEventType.DocumentStart, false, _line + 1, _column + 1)) { return false; }

            int docStartLine = _line;
            Advance(3);
            SkipWhitespaceAndComments();
            bool sameLineAsDocStart = _line == docStartLine;

            if (_pos < _buffer.Length && !IsDocumentEndOrStart())
            {
                if (!ParseNode(-1, false, sameLineValue: sameLineAsDocStart)) { return false; }
            }
            else
            {
                if (!EmitScalarEvent(_line + 1, _column + 1, default, YamlScalarStyle.Plain, default, default)) { return false; }
            }
        }
        else
        {
            // Directives without a following --- are invalid
            if (_hasDirectives)
            {
                ThrowDirectiveWithoutDocumentStart();
            }

            if (!EmitDocumentEvent(YamlEventType.DocumentStart, true, _line + 1, _column + 1)) { return false; }

            if (_pos < _buffer.Length && !IsDocumentEndOrStart())
            {
                if (!ParseNode(-1, false)) { return false; }
            }
            else
            {
                if (!EmitScalarEvent(_line + 1, _column + 1, default, YamlScalarStyle.Plain, default, default)) { return false; }
            }
        }

        SkipWhitespaceAndComments();

        // Validate that after the root node, only document markers or EOF remain
        if (_pos < _buffer.Length && !IsDocumentEndOrStart())
        {
            ThrowTrailingContentAfterNode();
        }

        if (IsDocumentEnd())
        {
            if (!EmitDocumentEvent(YamlEventType.DocumentEnd, false, _line + 1, _column + 1)) { return false; }

            Advance(3);

            // After ..., rest of line must be whitespace/comment only
            SkipWhitespaceOnly();

            if (_pos < _buffer.Length && !YamlCharacters.IsLineBreak(_buffer[_pos]) && _buffer[_pos] != YamlConstants.Hash)
            {
                ThrowContentAfterDocumentEndMarker();
            }

            SkipWhitespaceAndComments();
        }
        else
        {
            if (!EmitDocumentEvent(YamlEventType.DocumentEnd, true, _line + 1, _column + 1)) { return false; }
        }

        return true;
    }

    private bool ParseNode(int parentIndent, bool inFlow, bool sameLineValue = false, bool isKey = false, bool isSeqEntry = false)
    {
        SkipWhitespaceAndComments();

        // Reject tabs used for block indentation
        if (!inFlow && _tabSeenInLeadingWhitespace)
        {
            ThrowTabInIndentation();
        }

        if (_pos >= _buffer.Length)
        {
            return EmitScalarEvent(_line + 1, _column + 1, default, YamlScalarStyle.Plain, default, default);
        }

        ReadOnlySpan<byte> anchor = default;
        ReadOnlySpan<byte> tag = default;
        byte[]? resolvedTagBuffer = null;
        int propsLine = _line;
        int propsColumn = _column;

        // Check for pending anchor/tag from a parent ParseNode that detected anchor-on-key
        if (_pendingKeyAnchorLength > 0 || _pendingKeyTagLength > 0)
        {
            if (_pendingKeyAnchorLength > 0)
            {
                anchor = _buffer.Slice(_pendingKeyAnchorStart, _pendingKeyAnchorLength);
            }

            if (_pendingKeyTagLength > 0)
            {
                tag = _pendingKeyTagBuffer.AsSpan(0, _pendingKeyTagLength);
            }

            _pendingKeyAnchorStart = 0;
            _pendingKeyAnchorLength = 0;
            _pendingKeyTagLength = 0;

            // Also parse any additional properties on the content line.
            // Cross-line splits may store only anchor as pending; the tag
            // (e.g. !!str) on the content line still needs to be read.
            ReadOnlySpan<byte> extraAnchor = default;
            ReadOnlySpan<byte> extraTag = default;
            ParseNodeProperties(ref extraAnchor, ref extraTag, ref resolvedTagBuffer, inFlow);

            if (anchor.IsEmpty && !extraAnchor.IsEmpty)
            {
                anchor = extraAnchor;
            }

            if (tag.IsEmpty && !extraTag.IsEmpty)
            {
                tag = extraTag;
            }
        }
        else
        {
            ParseNodeProperties(ref anchor, ref tag, ref resolvedTagBuffer, inFlow);
        }

        try
        {
            if (_pos >= _buffer.Length)
            {
                return EmitScalarEvent(_line + 1, _column + 1, default, YamlScalarStyle.Plain, anchor, tag);
            }

            byte c = _buffer[_pos];

            // If properties (anchor/tag) were on a previous line and the content
            // has returned to the parent indent level, the node is an empty scalar
            // with the properties — the content belongs to the parent, not this node.
            // Exception: block sequence entries ('-') can legitimately start at the
            // parent indent when they are the value of a mapping key. But in a
            // sequence context (isSeqEntry), '-' at the parent indent IS a sibling.
            if ((!anchor.IsEmpty || !tag.IsEmpty) && _line != propsLine && parentIndent >= 0 && !isKey)
            {
                // Content strictly below parent indent — always an empty scalar
                if (_column < parentIndent)
                {
                    return EmitScalarEvent(propsLine + 1, propsColumn + 1, default, YamlScalarStyle.Plain, anchor, tag);
                }

                // Content at parent indent — empty scalar unless it's a block sequence
                // entry in a non-sequence context (where '-' starts the value collection)
                if (_column == parentIndent && (isSeqEntry || !IsBlockSequenceEntry()))
                {
                    return EmitScalarEvent(propsLine + 1, propsColumn + 1, default, YamlScalarStyle.Plain, anchor, tag);
                }
            }

            if (c == YamlConstants.Asterisk && !inFlow && !isKey && ScanForMappingValueIndicator())
            {
                int nodeIndent = _column;
                bool anchorOnKey = (!anchor.IsEmpty || !tag.IsEmpty) && _line == propsLine;

                if (anchorOnKey)
                {
                    SetPendingKeyProperties(anchor, tag);
                    return ParseBlockMapping(parentIndent, propsColumn, default, default);
                }

                return ParseBlockMapping(parentIndent, nodeIndent, anchor, tag);
            }

            if (c == YamlConstants.Asterisk)
            {
                // Aliases cannot have anchors or tags
                if (!anchor.IsEmpty || !tag.IsEmpty)
                {
                    ThrowAliasCannotHaveProperties();
                }

                return ParseAlias();
            }

            // Flow collections as block mapping keys: [flow]: block, {flow}: block
            if ((c == YamlConstants.OpenBrace || c == YamlConstants.OpenBracket) && !inFlow && !isKey && ScanForMappingValueIndicator())
            {
                int nodeIndent = _column;
                bool anchorOnKey = (!anchor.IsEmpty || !tag.IsEmpty) && _line == propsLine;

                if (anchorOnKey)
                {
                    SetPendingKeyProperties(anchor, tag);
                    return ParseBlockMapping(parentIndent, propsColumn, default, default);
                }

                return ParseBlockMapping(parentIndent, nodeIndent, anchor, tag);
            }

            if (c == YamlConstants.OpenBrace) { return ParseFlowMapping(anchor, tag); }

            if (c == YamlConstants.OpenBracket) { return ParseFlowSequence(anchor, tag); }

            if (c == YamlConstants.Pipe || c == YamlConstants.GreaterThan)
            {
                return ParseBlockScalar(parentIndent, anchor, tag);
            }

            if (!inFlow && !isKey && IsBlockSequenceEntry())
            {
                // Block sequence cannot start on the same line as a mapping value
                // (e.g. "key: - a" is invalid). But if properties (tags/anchors) moved
                // us to a new line, the block sequence is legitimately on its own line.
                if (sameLineValue && _line == propsLine)
                {
                    ThrowBlockCollectionOnSameLine();
                }

                // YAML spec: block collections cannot start on the same line as
                // node properties (anchor/tag). Properties must be on a prior line.
                if ((!anchor.IsEmpty || !tag.IsEmpty) && _line == propsLine)
                {
                    ThrowBlockCollectionOnSameLine();
                }

                return ParseBlockSequence(parentIndent, anchor, tag);
            }

            if (!inFlow && !isKey && !(sameLineValue && (c == YamlConstants.DoubleQuote || c == YamlConstants.SingleQuote)))
            {
                int nodeIndent = _column;

                if (c == YamlConstants.QuestionMark && (_pos + 1 >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[_pos + 1])))
                {
                    // Block mapping cannot start on the same line as ---
                    if (sameLineValue && _line == propsLine)
                    {
                        ThrowBlockCollectionOnSameLine();
                    }

                    return ParseBlockMapping(parentIndent, nodeIndent, anchor, tag);
                }

                if (ScanForMappingValueIndicator())
                {
                    // Block mapping cannot start on the same line as ---
                    if (sameLineValue && _line == propsLine)
                    {
                        ThrowBlockCollectionOnSameLine();
                    }

                    // If anchor/tag was found on the SAME line as the key content,
                    // it belongs to the key scalar, not the mapping.
                    // Use the pre-properties column as the mapping indent so subsequent
                    // entries at the same column are correctly included.
                    bool anchorOnKey = (!anchor.IsEmpty || !tag.IsEmpty) && _line == propsLine;

                    if (anchorOnKey)
                    {
                        SetPendingKeyProperties(anchor, tag);
                        return ParseBlockMapping(parentIndent, propsColumn, default, default);
                    }

                    // Cross-line property split: tag on earlier line (container),
                    // anchor on same line as content (key child).
                    // E.g.: !!map\n&a8 !!str key8: value7
                    if (!anchor.IsEmpty && !tag.IsEmpty && _lastAnchorLine != propsLine && _lastAnchorLine == _line)
                    {
                        SetPendingKeyProperties(anchor, default);
                        return ParseBlockMapping(parentIndent, nodeIndent, default, tag);
                    }

                    return ParseBlockMapping(parentIndent, nodeIndent, anchor, tag);
                }
            }

            if (c == YamlConstants.DoubleQuote)
            {
                int sl = _line; int sc = _column;
                ReadOnlySpan<byte> value = ReadDoubleQuotedScalar(out byte[]? rented, parentIndent);

                try { return EmitScalarEvent(sl + 1, sc + 1, value, YamlScalarStyle.DoubleQuoted, anchor, tag); }
                finally { if (rented != null) { ArrayPool<byte>.Shared.Return(rented); } }
            }

            if (c == YamlConstants.SingleQuote)
            {
                int sl = _line; int sc = _column;
                ReadOnlySpan<byte> value = ReadSingleQuotedScalar(out byte[]? rented, parentIndent);

                try { return EmitScalarEvent(sl + 1, sc + 1, value, YamlScalarStyle.SingleQuoted, anchor, tag); }
                finally { if (rented != null) { ArrayPool<byte>.Shared.Return(rented); } }
            }

            if (inFlow && IsFlowEmptyNode())
            {
                return EmitScalarEvent(_line + 1, _column + 1, default, YamlScalarStyle.Plain, anchor, tag);
            }

            return ParsePlainScalar(parentIndent, inFlow, anchor, tag);
        }
        finally
        {
            if (resolvedTagBuffer != null) { ArrayPool<byte>.Shared.Return(resolvedTagBuffer); }
        }
    }

    private void SetPendingKeyProperties(ReadOnlySpan<byte> anchor, ReadOnlySpan<byte> tag)
    {
        if (!anchor.IsEmpty)
        {
            _pendingKeyAnchorStart = _lastAnchorStart;
            _pendingKeyAnchorLength = _lastAnchorLength;
        }

        if (!tag.IsEmpty)
        {
            if (_pendingKeyTagBuffer == null || _pendingKeyTagBuffer.Length < tag.Length)
            {
                if (_pendingKeyTagBuffer != null)
                {
                    ArrayPool<byte>.Shared.Return(_pendingKeyTagBuffer);
                }

                _pendingKeyTagBuffer = ArrayPool<byte>.Shared.Rent(tag.Length);
            }

            tag.CopyTo(_pendingKeyTagBuffer);
            _pendingKeyTagLength = tag.Length;
        }
    }

    private void ParseNodeProperties(ref ReadOnlySpan<byte> anchor, ref ReadOnlySpan<byte> tag, ref byte[]? resolvedTagBuffer, bool inFlow = false)
    {
        while (_pos < _buffer.Length)
        {
            byte c = _buffer[_pos];

            if (c == YamlConstants.Ampersand)
            {
                // A node can have at most one anchor. If we already have one,
                // this anchor belongs to a child node — stop reading properties.
                if (!anchor.IsEmpty) { break; }

                Advance(1);
                _lastAnchorStart = _pos;
                _lastAnchorLine = _line;
                anchor = ReadAnchorName();
                _lastAnchorLength = anchor.Length;
                SkipWhitespaceAndComments();
                continue;
            }

            if (c == YamlConstants.Exclamation)
            {
                // A node can have at most one tag.
                if (!tag.IsEmpty) { break; }

                ReadOnlySpan<byte> rawTag = ReadTag();

                // YAML spec requires separation after a tag before content.
                // In flow context, flow indicators (comma, brackets, braces) are structural.
                // In block context, only flow collection openers are structural.
                if (_pos < _buffer.Length
                    && !YamlCharacters.IsWhitespaceOrLineBreak(_buffer[_pos])
                    && !(inFlow && YamlCharacters.IsFlowIndicator(_buffer[_pos]))
                    && !(!inFlow && (_buffer[_pos] == YamlConstants.OpenBrace || _buffer[_pos] == YamlConstants.OpenBracket)))
                {
                    ThrowUnexpectedCharacter(_buffer[_pos]);
                }

                // Free previous resolved tag buffer if any
                if (resolvedTagBuffer != null)
                {
                    ArrayPool<byte>.Shared.Return(resolvedTagBuffer);
                    resolvedTagBuffer = null;
                }

                tag = ResolveTag(rawTag, out resolvedTagBuffer);
                SkipWhitespaceAndComments();
                continue;
            }

            break;
        }
    }

    private ReadOnlySpan<byte> ReadAnchorName()
    {
        int start = _pos;

        while (_pos < _buffer.Length && !YamlCharacters.IsWhitespaceOrLineBreak(_buffer[_pos]) && !YamlCharacters.IsFlowIndicator(_buffer[_pos]))
        {
            Advance(1);
        }

        if (_pos == start) { ThrowUnexpectedCharacter(_pos < _buffer.Length ? _buffer[_pos] : (byte)0); }

        return _buffer.Slice(start, _pos - start);
    }

    private ReadOnlySpan<byte> ReadTag()
    {
        int start = _pos;
        Advance(1);

        if (_pos >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[_pos]))
        {
            return _buffer.Slice(start, _pos - start);
        }

        if (_buffer[_pos] == YamlConstants.Exclamation)
        {
            Advance(1);
            while (_pos < _buffer.Length && !YamlCharacters.IsWhitespaceOrLineBreak(_buffer[_pos]) && !YamlCharacters.IsFlowIndicator(_buffer[_pos])) { Advance(1); }

            return _buffer.Slice(start, _pos - start);
        }

        if (_buffer[_pos] == (byte)'<')
        {
            Advance(1);
            while (_pos < _buffer.Length && _buffer[_pos] != (byte)'>') { Advance(1); }

            if (_pos < _buffer.Length) { Advance(1); }

            return _buffer.Slice(start, _pos - start);
        }

        while (_pos < _buffer.Length && !YamlCharacters.IsWhitespaceOrLineBreak(_buffer[_pos]) && !YamlCharacters.IsFlowIndicator(_buffer[_pos])) { Advance(1); }

        return _buffer.Slice(start, _pos - start);
    }

    private bool ParseAlias()
    {
        int sl = _line; int sc = _column;
        Advance(1);
        int start = _pos;

        while (_pos < _buffer.Length && !YamlCharacters.IsWhitespaceOrLineBreak(_buffer[_pos]) && !YamlCharacters.IsFlowIndicator(_buffer[_pos])) { Advance(1); }

        return EmitAliasEvent(_buffer.Slice(start, _pos - start), sl + 1, sc + 1);
    }

    private void EnsureScratchCapacity(int needed)
    {
        if (_scratchPos + needed > _scratchBuffer.Length)
        {
            int newSize = Math.Max(_scratchBuffer.Length * 2, _scratchPos + needed);
            byte[] newBuf = ArrayPool<byte>.Shared.Rent(newSize);
            _scratchBuffer.AsSpan(0, _scratchPos).CopyTo(newBuf);
            ArrayPool<byte>.Shared.Return(_scratchBuffer);
            _scratchBuffer = newBuf;
        }
    }

    private bool ParseBlockMapping(int parentIndent, int mappingIndent, ReadOnlySpan<byte> anchor, ReadOnlySpan<byte> tag)
    {
        if (!EnterDepth()) { return false; }

        if (!EmitCollectionStartEvent(YamlEventType.MappingStart, _line + 1, _column + 1, anchor, tag)) { return false; }

        bool first = true;

        while (_pos < _buffer.Length)
        {
            SkipWhitespaceAndComments();
            if (_pos >= _buffer.Length) { break; }

            // Reject tabs used for block indentation
            if (_tabSeenInLeadingWhitespace && _column >= mappingIndent) { ThrowTabInIndentation(); }

            if (_column <= parentIndent && !first) { break; }

            if (!first && _column != mappingIndent)
            {
                if (_column < mappingIndent) { break; }

                ThrowUnexpectedCharacter(_buffer[_pos]);
            }

            if (IsDocumentEndOrStart()) { break; }

            if (_buffer[_pos] == YamlConstants.QuestionMark && (_pos + 1 >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[_pos + 1])))
            {
                Advance(1);
                SkipWhitespaceAndComments();

                if (_pos < _buffer.Length && !IsDocumentEndOrStart()
                    && (_column > mappingIndent || (_column == mappingIndent && IsBlockSequenceEntry())))
                {
                    if (!ParseNode(mappingIndent, false)) { return false; }
                }
                else
                {
                    if (!EmitScalarEvent(_line + 1, _column + 1, default, YamlScalarStyle.Plain, default, default)) { return false; }
                }

                SkipWhitespaceAndComments();

                if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Colon && (_pos + 1 >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[_pos + 1])))
                {
                    Advance(1);
                    SkipWhitespaceAndComments();

                    if (_pos >= _buffer.Length || _column < mappingIndent || IsDocumentEndOrStart())
                    {
                        if (!EmitScalarEvent(_line + 1, _column + 1, default, YamlScalarStyle.Plain, default, default)) { return false; }
                    }
                    else if (_column == mappingIndent && !IsBlockSequenceEntry())
                    {
                        if (!EmitScalarEvent(_line + 1, _column + 1, default, YamlScalarStyle.Plain, default, default)) { return false; }
                    }
                    else
                    {
                        if (!ParseNode(mappingIndent, false)) { return false; }
                    }
                }
                else
                {
                    if (!EmitScalarEvent(_line + 1, _column + 1, default, YamlScalarStyle.Plain, default, default)) { return false; }
                }
            }
            else if (_pendingKeyAnchorLength == 0 && _pendingKeyTagLength == 0
                && _buffer[_pos] == YamlConstants.Colon && (_pos + 1 >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[_pos + 1])))
            {
                // Empty key — emit empty scalar for the key, then parse value
                if (!EmitScalarEvent(_line + 1, _column + 1, default, YamlScalarStyle.Plain, default, default)) { return false; }

                int colonLine = _line;
                Advance(1);
                SkipWhitespaceAndComments();

                if (_pos >= _buffer.Length || IsDocumentEndOrStart())
                {
                    if (!EmitScalarEvent(_line + 1, _column + 1, default, YamlScalarStyle.Plain, default, default)) { return false; }
                }
                else if (_pos < _buffer.Length && _line == colonLine && !IsComment())
                {
                    if (!ParseNode(mappingIndent, false, sameLineValue: true)) { return false; }
                }
                else if (_column < mappingIndent)
                {
                    if (!EmitScalarEvent(_line + 1, _column + 1, default, YamlScalarStyle.Plain, default, default)) { return false; }
                }
                else if (_column == mappingIndent && !IsBlockSequenceEntry())
                {
                    if (!EmitScalarEvent(_line + 1, _column + 1, default, YamlScalarStyle.Plain, default, default)) { return false; }
                }
                else
                {
                    if (!ParseNode(mappingIndent, false)) { return false; }
                }
            }
            else
            {
                // Parse implicit key (isKey=true prevents infinite mapping recursion)
                int keyStartLine = _line;
                if (!ParseNode(mappingIndent, false, isKey: true)) { return false; }

                // YAML spec: implicit keys must not span multiple lines
                if (_line > keyStartLine)
                {
                    ThrowMultilineImplicitKeyNotAllowed();
                }

                SkipWhitespaceOnly();

                if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Colon && (_pos + 1 >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[_pos + 1])))
                {
                    int colonLine = _line;
                    Advance(1);
                    SkipWhitespaceAndComments();
                    bool tabInValue = _tabSeenInLeadingWhitespace;

                    if (_pos < _buffer.Length && _line == colonLine && !IsComment())
                    {
                        if (!ParseNode(mappingIndent, false, sameLineValue: true)) { return false; }
                    }
                    else if (_pos >= _buffer.Length || IsDocumentEndOrStart())
                    {
                        if (!EmitScalarEvent(_line + 1, _column + 1, default, YamlScalarStyle.Plain, default, default)) { return false; }
                    }
                    else if (_column < mappingIndent)
                    {
                        if (!EmitScalarEvent(_line + 1, _column + 1, default, YamlScalarStyle.Plain, default, default)) { return false; }
                    }
                    else if (_column == mappingIndent && !IsBlockSequenceEntry())
                    {
                        if (!EmitScalarEvent(_line + 1, _column + 1, default, YamlScalarStyle.Plain, default, default)) { return false; }
                    }
                    else
                    {
                        if (tabInValue) { ThrowTabInIndentation(); }

                        if (!ParseNode(mappingIndent, false)) { return false; }
                    }
                }
                else
                {
                    ThrowExpectedMappingValue();
                }
            }

            first = false;
            SkipWhitespaceAndComments();
        }

        if (!EmitEvent(YamlEventType.MappingEnd, _line + 1, _column + 1)) { return false; }

        _depth--;
        return true;
    }

    private bool ParseBlockSequence(int parentIndent, ReadOnlySpan<byte> anchor, ReadOnlySpan<byte> tag)
    {
        if (!EnterDepth()) { return false; }

        int seqIndent = _column;

        if (!EmitCollectionStartEvent(YamlEventType.SequenceStart, _line + 1, _column + 1, anchor, tag)) { return false; }

        while (_pos < _buffer.Length)
        {
            SkipWhitespaceAndComments();
            if (_pos >= _buffer.Length) { break; }

            // Reject tabs used for block indentation
            if (_tabSeenInLeadingWhitespace && _column >= seqIndent && IsBlockSequenceEntry()) { ThrowTabInIndentation(); }

            if (_column != seqIndent || !IsBlockSequenceEntry()) { break; }

            if (IsDocumentEndOrStart()) { break; }

            Advance(1);
            SkipWhitespaceOnly();

            if (_pos >= _buffer.Length || YamlCharacters.IsLineBreak(_buffer[_pos]) || IsComment())
            {
                SkipWhitespaceAndComments();

                if (_pos >= _buffer.Length || _column <= seqIndent || IsDocumentEndOrStart())
                {
                    if (!EmitScalarEvent(_line + 1, _column + 1, default, YamlScalarStyle.Plain, default, default)) { return false; }
                }
                else
                {
                    if (!ParseNode(seqIndent, false, isSeqEntry: true)) { return false; }
                }
            }
            else
            {
                if (!ParseNode(seqIndent, false, isSeqEntry: true)) { return false; }
            }
        }

        if (!EmitEvent(YamlEventType.SequenceEnd, _line + 1, _column + 1)) { return false; }

        _depth--;
        return true;
    }

    private bool ParseFlowMapping(ReadOnlySpan<byte> anchor, ReadOnlySpan<byte> tag)
    {
        EnterDepth();
        int sl = _line; int sc = _column;
        Advance(1);

        if (!EmitCollectionStartEvent(YamlEventType.MappingStart, sl + 1, sc + 1, anchor, tag, isFlowStyle: true)) { return false; }

        SkipWhitespaceAndComments();
        bool needComma = false;

        while (_pos < _buffer.Length && _buffer[_pos] != YamlConstants.CloseBrace)
        {
            if (needComma)
            {
                if (_buffer[_pos] != YamlConstants.Comma) { ThrowMissingFlowSeparator(); }

                Advance(1);
                SkipWhitespaceAndComments();
                if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.CloseBrace) { break; }
            }

            // Document markers (--- or ...) are forbidden inside flow collections
            if (IsDocumentEndOrStart())
            {
                ThrowUnexpectedCharacter(_buffer[_pos]);
            }

            // Handle ? explicit key
            if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.QuestionMark && (_pos + 1 >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[_pos + 1])))
            {
                Advance(1);
                SkipWhitespaceAndComments();

                if (_pos < _buffer.Length && _buffer[_pos] != YamlConstants.Colon && _buffer[_pos] != YamlConstants.Comma && _buffer[_pos] != YamlConstants.CloseBrace)
                {
                    if (!ParseNode(-1, true)) { return false; }
                }
                else
                {
                    if (!EmitScalarEvent(_line + 1, _column + 1, default, YamlScalarStyle.Plain, default, default)) { return false; }
                }
            }
            else
            {
                // Check for implicit empty key: ": value" in flow mapping
                if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Colon
                    && (_pos + 1 >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[_pos + 1]) || YamlCharacters.IsFlowIndicator(_buffer[_pos + 1])))
                {
                    // Empty key — emit null scalar, colon will be consumed below
                    if (!EmitScalarEvent(_line + 1, _column + 1, default, YamlScalarStyle.Plain, default, default)) { return false; }
                }
                else
                {
                    if (!ParseNode(-1, true)) { return false; }
                }
            }

            SkipWhitespaceAndComments();

            if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Colon)
            {
                Advance(1);
                SkipWhitespaceAndComments();

                if (_pos >= _buffer.Length || _buffer[_pos] == YamlConstants.Comma || _buffer[_pos] == YamlConstants.CloseBrace)
                {
                    if (!EmitScalarEvent(_line + 1, _column + 1, default, YamlScalarStyle.Plain, default, default)) { return false; }
                }
                else
                {
                    if (!ParseNode(-1, true)) { return false; }
                }
            }
            else
            {
                if (!EmitScalarEvent(_line + 1, _column + 1, default, YamlScalarStyle.Plain, default, default)) { return false; }
            }

            SkipWhitespaceAndComments();
            needComma = true;
        }

        if (_pos >= _buffer.Length) { ThrowUnterminatedFlowMapping(); }

        Advance(1);

        if (!EmitEvent(YamlEventType.MappingEnd, _line + 1, _column + 1)) { return false; }

        _depth--;
        return true;
    }

    private bool ParseFlowSequence(ReadOnlySpan<byte> anchor, ReadOnlySpan<byte> tag)
    {
        EnterDepth();
        int sl = _line; int sc = _column;
        Advance(1);

        if (!EmitCollectionStartEvent(YamlEventType.SequenceStart, sl + 1, sc + 1, anchor, tag, isFlowStyle: true)) { return false; }

        SkipWhitespaceAndComments();

        // Reject leading comma: [ , a ] is invalid
        if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Comma)
        {
            ThrowInvalidFlowSequenceEntry();
        }

        bool needComma = false;

        while (_pos < _buffer.Length && _buffer[_pos] != YamlConstants.CloseBracket)
        {
            if (needComma)
            {
                if (_buffer[_pos] != YamlConstants.Comma) { ThrowMissingFlowSeparator(); }

                Advance(1);
                SkipWhitespaceAndComments();
                if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.CloseBracket) { break; }

                // Double comma [a, , b] is invalid
                if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Comma)
                {
                    ThrowInvalidFlowSequenceEntry();
                }
            }

            // Document markers (--- or ...) are forbidden inside flow collections
            if (IsDocumentEndOrStart())
            {
                ThrowUnexpectedCharacter(_buffer[_pos]);
            }

            // Check for implicit mapping: key: value
            bool isImplicitMapping = false;

            if (_pos < _buffer.Length)
            {
                byte c = _buffer[_pos];

                if (c == YamlConstants.QuestionMark && (_pos + 1 >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[_pos + 1])))
                {
                    isImplicitMapping = true;
                }
                else if (c == YamlConstants.Colon && (_pos + 1 >= _buffer.Length || YamlCharacters.IsFlowIndicatorOrWhitespace(_buffer[_pos + 1])))
                {
                    isImplicitMapping = true;
                }
                else
                {
                    isImplicitMapping = ScanForFlowMappingValueIndicator();
                }
            }

            if (isImplicitMapping)
            {
                int ml = _line; int mc = _column;
                if (!EmitCollectionStartEvent(YamlEventType.MappingStart, ml + 1, mc + 1, default, default, isFlowStyle: true)) { return false; }

                if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.QuestionMark && (_pos + 1 >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[_pos + 1])))
                {
                    Advance(1);
                    SkipWhitespaceAndComments();

                    if (_pos < _buffer.Length && _buffer[_pos] != YamlConstants.Colon && _buffer[_pos] != YamlConstants.Comma && _buffer[_pos] != YamlConstants.CloseBracket)
                    {
                        if (!ParseNode(-1, true)) { return false; }
                    }
                    else
                    {
                        if (!EmitScalarEvent(_line + 1, _column + 1, default, YamlScalarStyle.Plain, default, default)) { return false; }
                    }
                }
                else if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Colon)
                {
                    // Empty key
                    if (!EmitScalarEvent(_line + 1, _column + 1, default, YamlScalarStyle.Plain, default, default)) { return false; }
                }
                else
                {
                    if (!ParseNode(-1, true)) { return false; }
                }

                SkipWhitespaceAndComments();

                if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Colon)
                {
                    Advance(1);
                    SkipWhitespaceAndComments();

                    if (_pos >= _buffer.Length || _buffer[_pos] == YamlConstants.Comma || _buffer[_pos] == YamlConstants.CloseBracket || _buffer[_pos] == YamlConstants.CloseBrace)
                    {
                        if (!EmitScalarEvent(_line + 1, _column + 1, default, YamlScalarStyle.Plain, default, default)) { return false; }
                    }
                    else
                    {
                        if (!ParseNode(-1, true)) { return false; }
                    }
                }
                else
                {
                    if (!EmitScalarEvent(_line + 1, _column + 1, default, YamlScalarStyle.Plain, default, default)) { return false; }
                }

                if (!EmitEvent(YamlEventType.MappingEnd, _line + 1, _column + 1)) { return false; }
            }
            else
            {
                if (!ParseNode(-1, true)) { return false; }
            }

            SkipWhitespaceAndComments();
            needComma = true;
        }

        if (_pos >= _buffer.Length) { ThrowUnterminatedFlowSequence(); }

        Advance(1);

        if (!EmitEvent(YamlEventType.SequenceEnd, _line + 1, _column + 1)) { return false; }

        _depth--;
        return true;
    }

    private bool ParsePlainScalar(int parentIndent, bool inFlow, ReadOnlySpan<byte> anchor, ReadOnlySpan<byte> tag)
    {
        byte[]? rentedBuffer = ArrayPool<byte>.Shared.Rent(256);
        int written = 0;
        Span<byte> output = rentedBuffer;
        int sl = _line; int sc = _column;

        // YAML spec: in flow context, - ? as first char require a following ns-plain-safe char
        // (: is excluded here because it serves as value indicator in flow mappings)
        if (inFlow && _pos < _buffer.Length)
        {
            byte first = _buffer[_pos];
            if (first == YamlConstants.Dash || first == YamlConstants.QuestionMark)
            {
                if (_pos + 1 >= _buffer.Length
                    || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[_pos + 1])
                    || YamlCharacters.IsFlowIndicator(_buffer[_pos + 1]))
                {
                    ThrowUnexpectedCharacter(first);
                }
            }
        }

        try
        {
            while (_pos < _buffer.Length)
            {
                byte c = _buffer[_pos];

                if (YamlCharacters.IsLineBreak(c))
                {
                    // Trim trailing whitespace from the line just written
                    while (written > 0 && YamlCharacters.IsWhitespace(output[written - 1])) { written--; }

                    if (inFlow)
                    {
                        SkipLineBreak();

                        // Skip comment lines after line break.
                        // Per YAML spec, a comment terminates the plain scalar.
                        bool foundComment = false;

                        while (true)
                        {
                            while (_pos < _buffer.Length && YamlCharacters.IsWhitespace(_buffer[_pos])) { Advance(1); }

                            if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Hash)
                            {
                                foundComment = true;

                                while (_pos < _buffer.Length && !YamlCharacters.IsLineBreak(_buffer[_pos])) { Advance(1); }

                                if (_pos < _buffer.Length) { SkipLineBreak(); }

                                continue;
                            }

                            break;
                        }

                        if (foundComment)
                        {
                            // Comment terminates the plain scalar
                            break;
                        }

                        EnsureOutputCapacity(ref output, ref rentedBuffer, written, 1);
                        output[written++] = YamlConstants.Space;
                        continue;
                    }

                    int savedPos = _pos; int savedLine = _line; int savedCol = _column;
                    SkipLineBreak();
                    int blanks = 0;

                    while (_pos < _buffer.Length && (YamlCharacters.IsLineBreak(_buffer[_pos]) || YamlCharacters.IsWhitespace(_buffer[_pos])))
                    {
                        if (YamlCharacters.IsLineBreak(_buffer[_pos])) { SkipLineBreak(); blanks++; }
                        else { Advance(1); }
                    }

                    if (_pos >= _buffer.Length || _column <= parentIndent || IsDocumentEndOrStart() || IsComment()
                        || (_buffer[_pos] == YamlConstants.Colon && (_pos + 1 >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[_pos + 1]))))
                    {
                        _pos = savedPos; _line = savedLine; _column = savedCol;
                        break;
                    }

                    if (blanks > 0)
                    {
                        for (int i = 0; i < blanks; i++) { EnsureOutputCapacity(ref output, ref rentedBuffer, written, 1); output[written++] = YamlConstants.LineFeed; }
                    }
                    else
                    {
                        EnsureOutputCapacity(ref output, ref rentedBuffer, written, 1);
                        output[written++] = YamlConstants.Space;
                    }

                    continue;
                }

                if (inFlow && (c == YamlConstants.Comma || c == YamlConstants.CloseBrace || c == YamlConstants.CloseBracket || c == YamlConstants.OpenBrace || c == YamlConstants.OpenBracket))
                {
                    break;
                }

                if (c == YamlConstants.Hash && _pos > 0 && YamlCharacters.IsWhitespace(_buffer[_pos - 1])) { break; }

                if (c == YamlConstants.Colon)
                {
                    int nx = _pos + 1;
                    if (nx >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[nx]) || (inFlow && YamlCharacters.IsFlowIndicator(_buffer[nx]))) { break; }
                }

                EnsureOutputCapacity(ref output, ref rentedBuffer, written, 1);
                output[written++] = c;
                Advance(1);
            }

            while (written > 0 && YamlCharacters.IsWhitespace(output[written - 1])) { written--; }

            return EmitScalarEvent(sl + 1, sc + 1, output.Slice(0, written), YamlScalarStyle.Plain, anchor, tag);
        }
        finally
        {
            if (rentedBuffer != null) { ArrayPool<byte>.Shared.Return(rentedBuffer); }
        }
    }

    private ReadOnlySpan<byte> ReadDoubleQuotedScalar(out byte[]? rentedBuffer, int minIndent)
    {
        rentedBuffer = ArrayPool<byte>.Shared.Rent(256);
        int written = 0;
        Span<byte> output = rentedBuffer;
        Advance(1);

        while (_pos < _buffer.Length)
        {
            byte c = _buffer[_pos];
            if (c == YamlConstants.DoubleQuote) { Advance(1); return rentedBuffer.AsSpan(0, written); }

            if (c == YamlConstants.Backslash)
            {
                if (_pos + 1 >= _buffer.Length) { ThrowUnterminatedDoubleQuotedScalar(); }

                byte nx = _buffer[_pos + 1];
                if (YamlCharacters.IsLineBreak(nx))
                {
                    // Escaped line break: discard \ and line break, but KEEP trailing whitespace
                    Advance(1);
                    SkipLineBreak();
                    while (_pos < _buffer.Length && YamlCharacters.IsWhitespace(_buffer[_pos])) { Advance(1); }

                    continue;
                }

                int escLen = WriteEscapeSequence(ref output, ref rentedBuffer, ref written);
                for (int i = 0; i < escLen; i++) { Advance(1); }

                continue;
            }

            if (YamlCharacters.IsLineBreak(c))
            {
                // Trim trailing whitespace (space and tab) before the fold
                while (written > 0 && (output[written - 1] == YamlConstants.Space || output[written - 1] == YamlConstants.Tab)) { written--; }

                SkipLineBreak();

                // Document markers terminate the scalar (unterminated string)
                if (IsDocumentEndOrStart()) { ThrowUnterminatedDoubleQuotedScalar(); }

                while (_pos < _buffer.Length && YamlCharacters.IsWhitespace(_buffer[_pos]) && !YamlCharacters.IsLineBreak(_buffer[_pos])) { Advance(1); }

                if (_pos < _buffer.Length && YamlCharacters.IsLineBreak(_buffer[_pos]))
                {
                    while (_pos < _buffer.Length && YamlCharacters.IsLineBreak(_buffer[_pos]))
                    {
                        EnsureOutputCapacity(ref output, ref rentedBuffer, written, 1);
                        output[written++] = YamlConstants.LineFeed;
                        SkipLineBreak();

                        // Document markers on blank lines also terminate
                        if (IsDocumentEndOrStart()) { ThrowUnterminatedDoubleQuotedScalar(); }

                        while (_pos < _buffer.Length && YamlCharacters.IsWhitespace(_buffer[_pos]) && !YamlCharacters.IsLineBreak(_buffer[_pos])) { Advance(1); }
                    }
                }
                else
                {
                    EnsureOutputCapacity(ref output, ref rentedBuffer, written, 1);
                    output[written++] = YamlConstants.Space;
                }

                continue;
            }

            EnsureOutputCapacity(ref output, ref rentedBuffer, written, 1);
            output[written++] = c;
            Advance(1);
        }

        ThrowUnterminatedDoubleQuotedScalar();
        return default;
    }

    private int WriteEscapeSequence(ref Span<byte> output, ref byte[]? rentedBuffer, ref int written)
    {
        byte esc = _buffer[_pos + 1];
        int consumed = 2;
        EnsureOutputCapacity(ref output, ref rentedBuffer, written, 4);

        switch (esc)
        {
            case (byte)'0': output[written++] = 0x00; break;
            case (byte)'a': output[written++] = 0x07; break;
            case (byte)'b': output[written++] = 0x08; break;
            case (byte)'t': case (byte)'\t': output[written++] = 0x09; break;
            case (byte)'n': output[written++] = 0x0A; break;
            case (byte)'v': output[written++] = 0x0B; break;
            case (byte)'f': output[written++] = 0x0C; break;
            case (byte)'r': output[written++] = 0x0D; break;
            case (byte)'e': output[written++] = 0x1B; break;
            case (byte)' ': output[written++] = 0x20; break;
            case (byte)'"': output[written++] = 0x22; break;
            case (byte)'/': output[written++] = 0x2F; break;
            case (byte)'\\': output[written++] = 0x5C; break;
            case (byte)'N': output[written++] = 0xC2; output[written++] = 0x85; break;
            case (byte)'_': output[written++] = 0xC2; output[written++] = 0xA0; break;
            case (byte)'L':
                EnsureOutputCapacity(ref output, ref rentedBuffer, written, 3);
                output[written++] = 0xE2; output[written++] = 0x80; output[written++] = 0xA8;
                break;
            case (byte)'P':
                EnsureOutputCapacity(ref output, ref rentedBuffer, written, 3);
                output[written++] = 0xE2; output[written++] = 0x80; output[written++] = 0xA9;
                break;
            case (byte)'x': consumed += WriteHexEscape(ref output, ref rentedBuffer, ref written, _pos + 2, 2); break;
            case (byte)'u': consumed += WriteHexEscape(ref output, ref rentedBuffer, ref written, _pos + 2, 4); break;
            case (byte)'U': consumed += WriteHexEscape(ref output, ref rentedBuffer, ref written, _pos + 2, 8); break;
            default: ThrowInvalidEscapeSequence(); break;
        }

        return consumed;
    }

    private int WriteHexEscape(ref Span<byte> output, ref byte[]? rentedBuffer, ref int written, int hexStart, int hexDigits)
    {
        if (hexStart + hexDigits > _buffer.Length) { ThrowInvalidEscapeSequence(); }

        int cp = 0;

        for (int i = 0; i < hexDigits; i++)
        {
            byte h = _buffer[hexStart + i];
            int d;
            if (h >= (byte)'0' && h <= (byte)'9') { d = h - (byte)'0'; }
            else if (h >= (byte)'a' && h <= (byte)'f') { d = h - (byte)'a' + 10; }
            else if (h >= (byte)'A' && h <= (byte)'F') { d = h - (byte)'A' + 10; }
            else { ThrowInvalidEscapeSequence(); return 0; }

            cp = (cp << 4) | d;
        }

        EnsureOutputCapacity(ref output, ref rentedBuffer, written, 4);
        if (cp <= 0x7F) { output[written++] = (byte)cp; }
        else if (cp <= 0x7FF) { output[written++] = (byte)(0xC0 | (cp >> 6)); output[written++] = (byte)(0x80 | (cp & 0x3F)); }
        else if (cp <= 0xFFFF) { output[written++] = (byte)(0xE0 | (cp >> 12)); output[written++] = (byte)(0x80 | ((cp >> 6) & 0x3F)); output[written++] = (byte)(0x80 | (cp & 0x3F)); }
        else if (cp <= 0x10FFFF) { output[written++] = (byte)(0xF0 | (cp >> 18)); output[written++] = (byte)(0x80 | ((cp >> 12) & 0x3F)); output[written++] = (byte)(0x80 | ((cp >> 6) & 0x3F)); output[written++] = (byte)(0x80 | (cp & 0x3F)); }
        else { ThrowInvalidEscapeSequence(); }

        return hexDigits;
    }

    private ReadOnlySpan<byte> ReadSingleQuotedScalar(out byte[]? rentedBuffer, int minIndent)
    {
        rentedBuffer = ArrayPool<byte>.Shared.Rent(256);
        int written = 0;
        Span<byte> output = rentedBuffer;
        Advance(1);

        while (_pos < _buffer.Length)
        {
            byte c = _buffer[_pos];

            if (c == YamlConstants.SingleQuote)
            {
                if (_pos + 1 < _buffer.Length && _buffer[_pos + 1] == YamlConstants.SingleQuote)
                {
                    EnsureOutputCapacity(ref output, ref rentedBuffer, written, 1);
                    output[written++] = YamlConstants.SingleQuote;
                    Advance(2);
                    continue;
                }

                Advance(1);
                return rentedBuffer.AsSpan(0, written);
            }

            if (YamlCharacters.IsLineBreak(c))
            {
                // Trim trailing whitespace (space and tab) before the fold
                while (written > 0 && (output[written - 1] == YamlConstants.Space || output[written - 1] == YamlConstants.Tab)) { written--; }

                SkipLineBreak();

                // Document markers terminate the scalar (unterminated string)
                if (IsDocumentEndOrStart()) { ThrowUnterminatedSingleQuotedScalar(); }

                while (_pos < _buffer.Length && YamlCharacters.IsWhitespace(_buffer[_pos]) && !YamlCharacters.IsLineBreak(_buffer[_pos])) { Advance(1); }

                if (_pos < _buffer.Length && YamlCharacters.IsLineBreak(_buffer[_pos]))
                {
                    while (_pos < _buffer.Length && YamlCharacters.IsLineBreak(_buffer[_pos]))
                    {
                        EnsureOutputCapacity(ref output, ref rentedBuffer, written, 1);
                        output[written++] = YamlConstants.LineFeed;
                        SkipLineBreak();

                        // Document markers on blank lines also terminate
                        if (IsDocumentEndOrStart()) { ThrowUnterminatedSingleQuotedScalar(); }

                        while (_pos < _buffer.Length && YamlCharacters.IsWhitespace(_buffer[_pos]) && !YamlCharacters.IsLineBreak(_buffer[_pos])) { Advance(1); }
                    }
                }
                else
                {
                    EnsureOutputCapacity(ref output, ref rentedBuffer, written, 1);
                    output[written++] = YamlConstants.Space;
                }

                continue;
            }

            EnsureOutputCapacity(ref output, ref rentedBuffer, written, 1);
            output[written++] = c;
            Advance(1);
        }

        ThrowUnterminatedSingleQuotedScalar();
        return default;
    }

    private bool ParseBlockScalar(int parentIndent, ReadOnlySpan<byte> anchor, ReadOnlySpan<byte> tag)
    {
        byte indicator = _buffer[_pos];
        int sl = _line; int sc = _column;
        Advance(1);
        bool literal = indicator == YamlConstants.Pipe;
        byte chomping = 0;
        int explicitIndent = 0;

        if (_pos < _buffer.Length)
        {
            byte c = _buffer[_pos];
            if (c == YamlConstants.Dash)
            {
                chomping = (byte)'-'; Advance(1);
                if (_pos < _buffer.Length && _buffer[_pos] >= (byte)'1' && _buffer[_pos] <= (byte)'9') { explicitIndent = _buffer[_pos] - (byte)'0'; Advance(1); }
            }
            else if (c == YamlConstants.Plus)
            {
                chomping = (byte)'+'; Advance(1);
                if (_pos < _buffer.Length && _buffer[_pos] >= (byte)'1' && _buffer[_pos] <= (byte)'9') { explicitIndent = _buffer[_pos] - (byte)'0'; Advance(1); }
            }
            else if (c >= (byte)'1' && c <= (byte)'9')
            {
                explicitIndent = c - (byte)'0'; Advance(1);
                if (_pos < _buffer.Length && (_buffer[_pos] == YamlConstants.Dash || _buffer[_pos] == YamlConstants.Plus)) { chomping = _buffer[_pos]; Advance(1); }
            }
        }

        // Validate that only whitespace and comments remain on the header line
        while (_pos < _buffer.Length && !YamlCharacters.IsLineBreak(_buffer[_pos]))
        {
            byte hc = _buffer[_pos];
            if (hc == YamlConstants.Space || hc == YamlConstants.Tab)
            {
                Advance(1);
            }
            else if (hc == YamlConstants.Hash)
            {
                if (_pos > 0 && !YamlCharacters.IsWhitespace(_buffer[_pos - 1]))
                {
                    ThrowCommentMustBePreceededByWhitespace();
                }

                SkipToEndOfLine();
                break;
            }
            else
            {
                ThrowInvalidBlockScalarHeader();
            }
        }

        if (_pos < _buffer.Length) { SkipLineBreak(); }

        byte[]? rentedBuffer = ArrayPool<byte>.Shared.Rent(256);
        int written = 0;
        Span<byte> output = rentedBuffer;

        try
        {
            int blockIndent = explicitIndent > 0 ? (Math.Max(parentIndent, 0) + explicitIndent) : 0;
            int trailingNewlines = 0;
            bool autoDetected = false;

            if (_pos < _buffer.Length && blockIndent == 0)
            {
                int saved = _pos; int savedLine = _line; int savedCol = _column;
                int maxLeadingEmptyIndent = 0;

                while (_pos < _buffer.Length)
                {
                    if (YamlCharacters.IsLineBreak(_buffer[_pos]))
                    {
                        SkipLineBreak();
                    }
                    else if (_buffer[_pos] == YamlConstants.Space)
                    {
                        Advance(1);
                    }
                    else
                    {
                        break;
                    }

                    // Track max indent of leading empty lines
                    if (_pos < _buffer.Length && YamlCharacters.IsLineBreak(_buffer[_pos]) && _column > maxLeadingEmptyIndent)
                    {
                        maxLeadingEmptyIndent = _column;
                    }
                }

                if (_pos < _buffer.Length && !IsDocumentEndOrStart())
                {
                    blockIndent = _column;
                    autoDetected = true;

                    // YAML spec §8.1.3: leading empty lines must not have more
                    // spaces than the first content line
                    if (maxLeadingEmptyIndent > blockIndent)
                    {
                        _pos = saved; _line = savedLine; _column = savedCol;
                        ThrowInvalidBlockScalarIndent();
                    }
                }

                _pos = saved; _line = savedLine; _column = savedCol;
            }

            // If detected indent is at or below parent level, the scalar is empty
            if (autoDetected && blockIndent <= parentIndent) { autoDetected = false; blockIndent = 0; }

            // Only force minimum indent when auto-detection found nothing
            if (!autoDetected && blockIndent == 0) { blockIndent = Math.Max(parentIndent + 1, 1); }

            bool firstContentLine = true;
            bool prevMoreIndented = false;

            while (_pos < _buffer.Length)
            {
                if (YamlCharacters.IsLineBreak(_buffer[_pos])) { trailingNewlines++; SkipLineBreak(); continue; }

                int lineStart = _pos;
                int lineStartCol = _column;
                int lineIndent = 0;
                while (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Space) { lineIndent++; Advance(1); }

                bool isBlankLine = _pos >= _buffer.Length || YamlCharacters.IsLineBreak(_buffer[_pos]);

                if (isBlankLine)
                {
                    // In literal mode, space-only lines with indent > blockIndent have content spaces
                    if (literal && lineIndent > blockIndent)
                    {
                        EmitPendingNewlines(ref output, ref rentedBuffer, ref written, trailingNewlines, literal, firstContentLine, false, ref prevMoreIndented);
                        trailingNewlines = 0;
                        firstContentLine = false;

                        int extraSpaces = lineIndent - blockIndent;
                        EnsureOutputCapacity(ref output, ref rentedBuffer, written, extraSpaces);
                        for (int i = 0; i < extraSpaces; i++) { output[written++] = YamlConstants.Space; }
                    }

                    continue;
                }

                if (lineIndent < blockIndent || (_column == lineIndent && IsDocumentEndOrStart())) { _pos = lineStart; _column = lineStartCol; break; }

                // More-indented: extra spaces OR first content char is whitespace (tab)
                bool moreIndented = lineIndent > blockIndent || (!literal && _pos < _buffer.Length && YamlCharacters.IsWhitespace(_buffer[_pos]));

                EmitPendingNewlines(ref output, ref rentedBuffer, ref written, trailingNewlines, literal, firstContentLine, moreIndented, ref prevMoreIndented);

                // Emit extra indent spaces as content
                if (lineIndent > blockIndent)
                {
                    int extraSpaces = lineIndent - blockIndent;
                    EnsureOutputCapacity(ref output, ref rentedBuffer, written, extraSpaces);
                    for (int i = 0; i < extraSpaces; i++) { output[written++] = YamlConstants.Space; }
                }

                trailingNewlines = 0;
                firstContentLine = false;
                prevMoreIndented = moreIndented;

                while (_pos < _buffer.Length && !YamlCharacters.IsLineBreak(_buffer[_pos]))
                {
                    EnsureOutputCapacity(ref output, ref rentedBuffer, written, 1);
                    output[written++] = _buffer[_pos];
                    Advance(1);
                }
            }

            if (chomping == YamlConstants.Plus)
            {
                for (int i = 0; i < trailingNewlines; i++) { EnsureOutputCapacity(ref output, ref rentedBuffer, written, 1); output[written++] = YamlConstants.LineFeed; }
            }
            else if (chomping != (byte)'-')
            {
                if (written > 0)
                {
                    EnsureOutputCapacity(ref output, ref rentedBuffer, written, 1);
                    output[written++] = YamlConstants.LineFeed;
                }
            }

            YamlScalarStyle style = literal ? YamlScalarStyle.Literal : YamlScalarStyle.Folded;
            return EmitScalarEvent(sl + 1, sc + 1, output.Slice(0, written), style, anchor, tag);
        }
        finally
        {
            if (rentedBuffer != null) { ArrayPool<byte>.Shared.Return(rentedBuffer); }
        }
    }

    private void EmitPendingNewlines(ref Span<byte> output, ref byte[]? rentedBuffer, ref int written, int trailingNewlines, bool literal, bool firstContentLine, bool currentMoreIndented, ref bool prevMoreIndented)
    {
        if (trailingNewlines == 0) { return; }

        if (firstContentLine)
        {
            for (int i = 0; i < trailingNewlines; i++) { EnsureOutputCapacity(ref output, ref rentedBuffer, written, 1); output[written++] = YamlConstants.LineFeed; }
        }
        else if (literal)
        {
            for (int i = 0; i < trailingNewlines; i++) { EnsureOutputCapacity(ref output, ref rentedBuffer, written, 1); output[written++] = YamlConstants.LineFeed; }
        }
        else if (prevMoreIndented || currentMoreIndented)
        {
            // More-indented transitions: all newlines are literal
            for (int i = 0; i < trailingNewlines; i++) { EnsureOutputCapacity(ref output, ref rentedBuffer, written, 1); output[written++] = YamlConstants.LineFeed; }
        }
        else
        {
            // Folded: single newline → space, multiple newlines → (count-1) LFs
            if (trailingNewlines == 1)
            {
                EnsureOutputCapacity(ref output, ref rentedBuffer, written, 1);
                output[written++] = YamlConstants.Space;
            }
            else
            {
                for (int i = 0; i < trailingNewlines - 1; i++) { EnsureOutputCapacity(ref output, ref rentedBuffer, written, 1); output[written++] = YamlConstants.LineFeed; }
            }
        }
    }

    private void Advance(int count) { for (int i = 0; i < count; i++) { _pos++; _column++; } }

    private void SkipLineBreak()
    {
        if (_pos >= _buffer.Length) { return; }

        if (_buffer[_pos] == YamlConstants.CarriageReturn)
        {
            _pos++;
            if (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.LineFeed) { _pos++; }

            _line++; _column = 0;
        }
        else if (_buffer[_pos] == YamlConstants.LineFeed)
        {
            _pos++; _line++; _column = 0;
        }
    }

    private void SkipWhitespaceOnly() { while (_pos < _buffer.Length && YamlCharacters.IsWhitespace(_buffer[_pos])) { Advance(1); } }

    private void SkipWhitespaceAndComments()
    {
        _tabSeenInLeadingWhitespace = false;
        bool atLineStart = _column == 0;

        while (_pos < _buffer.Length)
        {
            byte c = _buffer[_pos];

            if (YamlCharacters.IsWhitespace(c))
            {
                if (c == YamlConstants.Tab && atLineStart)
                {
                    _tabSeenInLeadingWhitespace = true;
                }

                Advance(1);
                continue;
            }

            if (YamlCharacters.IsLineBreak(c)) { SkipLineBreak(); atLineStart = true; continue; }

            if (c == YamlConstants.Hash)
            {
                // YAML spec: # is only a comment when preceded by whitespace or at line start
                if (_pos > 0 && !YamlCharacters.IsWhitespace(_buffer[_pos - 1])
                    && !YamlCharacters.IsLineBreak(_buffer[_pos - 1]))
                {
                    ThrowCommentMustBePreceededByWhitespace();
                }

                SkipToEndOfLine();
                continue;
            }

            break;
        }
    }

    private void SkipToEndOfLine() { while (_pos < _buffer.Length && !YamlCharacters.IsLineBreak(_buffer[_pos])) { Advance(1); } }

    private bool IsComment() { return _pos < _buffer.Length && _buffer[_pos] == YamlConstants.Hash; }

    private void SkipBom()
    {
        if (_buffer.Length >= 3 && _buffer[0] == YamlConstants.Bom0 && _buffer[1] == YamlConstants.Bom1 && _buffer[2] == YamlConstants.Bom2) { _pos = 3; }
    }

    private bool IsDocumentEndOrStart() { return IsDocumentStart() || IsDocumentEnd(); }

    private bool IsDocumentStart()
    {
        if (_column != 0 || _pos + 3 > _buffer.Length) { return false; }

        return _buffer[_pos] == YamlConstants.Dash && _buffer[_pos + 1] == YamlConstants.Dash && _buffer[_pos + 2] == YamlConstants.Dash
            && (_pos + 3 >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[_pos + 3]));
    }

    private bool IsDocumentEnd()
    {
        if (_column != 0 || _pos + 3 > _buffer.Length) { return false; }

        return _buffer[_pos] == YamlConstants.Period && _buffer[_pos + 1] == YamlConstants.Period && _buffer[_pos + 2] == YamlConstants.Period
            && (_pos + 3 >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[_pos + 3]));
    }

    private bool IsBlockSequenceEntry()
    {
        return _pos < _buffer.Length && _buffer[_pos] == YamlConstants.Dash
            && (_pos + 1 >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[_pos + 1]));
    }

    private bool ScanForFlowMappingValueIndicator()
    {
        int sp = _pos;
        int depth = 0;
        bool crossedLine = false;
        while (sp < _buffer.Length)
        {
            byte c = _buffer[sp];

            if (c == YamlConstants.Hash && sp > _pos && YamlCharacters.IsWhitespace(_buffer[sp - 1])) { break; }

            if (depth == 0 && c == YamlConstants.Colon)
            {
                // In flow context, ':' is a value indicator if:
                // 1. Followed by whitespace or flow indicator (standard), OR
                // 2. Immediately after a flow closing ('}', ']') or closing quote ('"', "'")
                bool validIndicator = sp + 1 >= _buffer.Length || YamlCharacters.IsFlowIndicatorOrWhitespace(_buffer[sp + 1]);

                if (!validIndicator && sp > _pos)
                {
                    byte prev = _buffer[sp - 1];
                    validIndicator = prev == YamlConstants.CloseBrace || prev == YamlConstants.CloseBracket
                        || prev == YamlConstants.DoubleQuote || prev == YamlConstants.SingleQuote;
                }

                if (validIndicator) { return !crossedLine; }
            }

            if (depth == 0 && (c == YamlConstants.Comma || c == YamlConstants.CloseBracket || c == YamlConstants.CloseBrace)) { return false; }

            if (c == YamlConstants.OpenBracket || c == YamlConstants.OpenBrace) { depth++; sp++; continue; }

            if (c == YamlConstants.CloseBracket || c == YamlConstants.CloseBrace) { depth--; sp++; continue; }

            if (c == YamlConstants.DoubleQuote)
            {
                sp++;
                while (sp < _buffer.Length && _buffer[sp] != YamlConstants.DoubleQuote)
                {
                    if (_buffer[sp] == YamlConstants.Backslash && sp + 1 < _buffer.Length) { sp++; }

                    sp++;
                }

                if (sp < _buffer.Length) { sp++; }

                continue;
            }

            if (c == YamlConstants.SingleQuote)
            {
                sp++;
                while (sp < _buffer.Length && _buffer[sp] != YamlConstants.SingleQuote) { sp++; }

                if (sp < _buffer.Length) { sp++; }

                if (sp < _buffer.Length && _buffer[sp] == YamlConstants.SingleQuote) { sp++; continue; }

                continue;
            }

            if (YamlCharacters.IsLineBreak(c)) { crossedLine = true; sp++; if (sp < _buffer.Length && c == YamlConstants.CarriageReturn && _buffer[sp] == YamlConstants.LineFeed) { sp++; } continue; }

            sp++;
        }

        return false;
    }

    private bool ScanForMappingValueIndicator()
    {
        int sp = _pos;
        while (sp < _buffer.Length)
        {
            byte c = _buffer[sp];
            if (YamlCharacters.IsLineBreak(c)) { return false; }

            // # is only a comment if preceded by whitespace (or at the start)
            if (c == YamlConstants.Hash && (sp == _pos || (sp > 0 && YamlCharacters.IsWhitespace(_buffer[sp - 1])))) { return false; }

            if (c == YamlConstants.Colon && (sp + 1 >= _buffer.Length || YamlCharacters.IsWhitespaceOrLineBreak(_buffer[sp + 1]))) { return true; }

            // Skip alias (*name) and anchor (&name) — their names can contain ':'
            if (c == YamlConstants.Asterisk || c == YamlConstants.Ampersand)
            {
                sp++;
                while (sp < _buffer.Length && !YamlCharacters.IsWhitespaceOrLineBreak(_buffer[sp]) && !YamlCharacters.IsFlowIndicator(_buffer[sp])) { sp++; }

                continue;
            }

            // Skip flow collections: [..] and {..} with nesting
            if (c == YamlConstants.OpenBracket || c == YamlConstants.OpenBrace)
            {
                byte close = c == YamlConstants.OpenBracket ? YamlConstants.CloseBracket : YamlConstants.CloseBrace;
                int depth = 1;
                sp++;

                while (sp < _buffer.Length && depth > 0)
                {
                    byte fc = _buffer[sp];
                    if (fc == c) { depth++; }
                    else if (fc == close) { depth--; }
                    else if (fc == YamlConstants.DoubleQuote)
                    {
                        sp++;
                        while (sp < _buffer.Length && _buffer[sp] != YamlConstants.DoubleQuote)
                        {
                            if (_buffer[sp] == YamlConstants.Backslash) { sp++; }

                            sp++;
                        }
                    }
                    else if (fc == YamlConstants.SingleQuote)
                    {
                        sp++;
                        while (sp < _buffer.Length && _buffer[sp] != YamlConstants.SingleQuote) { sp++; }
                    }

                    sp++;
                }

                continue;
            }

            // Only enter quote-skip mode if the quote is at the start or after whitespace
            if (c == YamlConstants.DoubleQuote && (sp == _pos || (sp > 0 && YamlCharacters.IsWhitespace(_buffer[sp - 1]))))
            {
                sp++;
                while (sp < _buffer.Length && _buffer[sp] != YamlConstants.DoubleQuote && !YamlCharacters.IsLineBreak(_buffer[sp]))
                {
                    if (_buffer[sp] == YamlConstants.Backslash) { sp++; }

                    sp++;
                }

                if (sp < _buffer.Length && _buffer[sp] == YamlConstants.DoubleQuote) { sp++; }

                continue;
            }

            if (c == YamlConstants.SingleQuote && (sp == _pos || (sp > 0 && YamlCharacters.IsWhitespace(_buffer[sp - 1]))))
            {
                sp++;
                while (sp < _buffer.Length && _buffer[sp] != YamlConstants.SingleQuote && !YamlCharacters.IsLineBreak(_buffer[sp])) { sp++; }

                if (sp < _buffer.Length && _buffer[sp] == YamlConstants.SingleQuote) { sp++; }

                continue;
            }

            sp++;
        }

        return false;
    }

    private bool IsFlowEmptyNode()
    {
        return _pos < _buffer.Length && (_buffer[_pos] == YamlConstants.Comma || _buffer[_pos] == YamlConstants.CloseBrace || _buffer[_pos] == YamlConstants.CloseBracket);
    }

    private bool EnterDepth()
    {
        if (_depth >= YamlConstants.MaxNestingDepth) { ThrowMaxNestingDepthExceeded(); return false; }

        _depth++;
        return true;
    }

    private void SkipDirectives()
    {
        _tagHandleCount = 0;
        _hasDirectives = false;
        _hasYamlDirective = false;
        int dataPos = 0;

        while (_pos < _buffer.Length && _buffer[_pos] == YamlConstants.Percent)
        {
            _hasDirectives = true;
            Advance(1);

            // Check for %YAML
            if (_pos + 4 <= _buffer.Length && _buffer[_pos] == (byte)'Y' && _buffer[_pos + 1] == (byte)'A' && _buffer[_pos + 2] == (byte)'M' && _buffer[_pos + 3] == (byte)'L')
            {
                if (_hasYamlDirective) { ThrowDuplicateYamlDirective(); }

                _hasYamlDirective = true;
                Advance(4);
                SkipWhitespaceOnly();

                // Read version (e.g., "1.2")
                int vStart = _pos;
                while (_pos < _buffer.Length && !YamlCharacters.IsWhitespace(_buffer[_pos]) && !YamlCharacters.IsLineBreak(_buffer[_pos])) { Advance(1); }

                // After the version, the rest of the line must be whitespace/comment only
                SkipWhitespaceOnly();

                if (_pos < _buffer.Length && !YamlCharacters.IsLineBreak(_buffer[_pos]) && _buffer[_pos] != YamlConstants.Hash)
                {
                    ThrowInvalidDirective();
                }

                SkipToEndOfLine();
                if (_pos < _buffer.Length) { SkipLineBreak(); }

                continue;
            }

            // Check for %TAG
            if (_pos + 3 < _buffer.Length && _buffer[_pos] == (byte)'T' && _buffer[_pos + 1] == (byte)'A' && _buffer[_pos + 2] == (byte)'G')
            {
                Advance(3);
                SkipWhitespaceOnly();

                // Read handle (e.g. "!!", "!e!", "!")
                int handleStart = _pos;
                while (_pos < _buffer.Length && !YamlCharacters.IsWhitespace(_buffer[_pos]) && !YamlCharacters.IsLineBreak(_buffer[_pos])) { Advance(1); }

                int handleLen = _pos - handleStart;
                SkipWhitespaceOnly();

                // Read prefix (e.g. "tag:yaml.org,2002:", "tag:example.com,2000:app/")
                int prefixStart = _pos;
                while (_pos < _buffer.Length && !YamlCharacters.IsWhitespace(_buffer[_pos]) && !YamlCharacters.IsLineBreak(_buffer[_pos])) { Advance(1); }

                int prefixLen = _pos - prefixStart;

                // Store the mapping
                if (_tagHandleCount < 8)
                {
                    int needed = dataPos + handleLen + prefixLen;
                    while (needed > _tagHandleData.Length)
                    {
                        byte[] newData = ArrayPool<byte>.Shared.Rent(_tagHandleData.Length * 2);
                        _tagHandleData.AsSpan(0, dataPos).CopyTo(newData);
                        ArrayPool<byte>.Shared.Return(_tagHandleData);
                        _tagHandleData = newData;
                    }

                    _buffer.Slice(handleStart, handleLen).CopyTo(_tagHandleData.AsSpan(dataPos));
                    int hDataStart = dataPos;
                    dataPos += handleLen;
                    _buffer.Slice(prefixStart, prefixLen).CopyTo(_tagHandleData.AsSpan(dataPos));
                    int pDataStart = dataPos;
                    dataPos += prefixLen;

                    int idx = _tagHandleCount * 4;
                    _tagHandles[idx] = hDataStart;
                    _tagHandles[idx + 1] = handleLen;
                    _tagHandles[idx + 2] = pDataStart;
                    _tagHandles[idx + 3] = prefixLen;
                    _tagHandleCount++;
                }
            }

            SkipToEndOfLine();
            if (_pos < _buffer.Length) { SkipLineBreak(); }
        }
    }

    private ReadOnlySpan<byte> ResolveTag(ReadOnlySpan<byte> rawTag, out byte[]? resolvedTagBuffer)
    {
        resolvedTagBuffer = null;

        // Verbatim tag: !<uri> → return uri (without !< and >)
        if (rawTag.Length >= 3 && rawTag[0] == (byte)'!' && rawTag[1] == (byte)'<')
        {
            return rawTag.Slice(2, rawTag.Length - 3);
        }

        // Secondary handle: !!suffix → resolve !! handle (default: tag:yaml.org,2002:)
        if (rawTag.Length >= 2 && rawTag[0] == (byte)'!' && rawTag[1] == (byte)'!')
        {
            ReadOnlySpan<byte> suffix = rawTag.Slice(2);
            return ResolveTagHandle("!!"u8, suffix, out resolvedTagBuffer);
        }

        // Named handle: !name!suffix
        if (rawTag.Length >= 3 && rawTag[0] == (byte)'!')
        {
            int secondBang = -1;
            for (int i = 1; i < rawTag.Length; i++)
            {
                if (rawTag[i] == (byte)'!')
                {
                    secondBang = i;
                    break;
                }
            }

            if (secondBang > 0)
            {
                ReadOnlySpan<byte> handle = rawTag.Slice(0, secondBang + 1);
                ReadOnlySpan<byte> suffix = rawTag.Slice(secondBang + 1);
                return ResolveTagHandle(handle, suffix, out resolvedTagBuffer);
            }
        }

        // Primary handle: !suffix → resolve ! handle (default: !)
        if (rawTag.Length >= 1 && rawTag[0] == (byte)'!')
        {
            ReadOnlySpan<byte> suffix = rawTag.Slice(1);
            return ResolveTagHandle("!"u8, suffix, out resolvedTagBuffer);
        }

        return rawTag;
    }

    private ReadOnlySpan<byte> ResolveTagHandle(ReadOnlySpan<byte> handle, ReadOnlySpan<byte> suffix, out byte[]? buffer)
    {
        buffer = null;

        // Search custom tag handles first
        for (int i = 0; i < _tagHandleCount; i++)
        {
            int idx = i * 4;
            ReadOnlySpan<byte> h = _tagHandleData.AsSpan(_tagHandles[idx], _tagHandles[idx + 1]);
            if (h.SequenceEqual(handle))
            {
                ReadOnlySpan<byte> prefix = _tagHandleData.AsSpan(_tagHandles[idx + 2], _tagHandles[idx + 3]);
                int totalLen = prefix.Length + suffix.Length;
                buffer = ArrayPool<byte>.Shared.Rent(totalLen);
                prefix.CopyTo(buffer);
                int decodedLen = CopyPercentDecoded(suffix, buffer.AsSpan(prefix.Length));
                return buffer.AsSpan(0, prefix.Length + decodedLen);
            }
        }

        // Default resolution for !!
        if (handle.Length == 2 && handle[0] == (byte)'!' && handle[1] == (byte)'!')
        {
            ReadOnlySpan<byte> defaultPrefix = "tag:yaml.org,2002:"u8;
            int totalLen = defaultPrefix.Length + suffix.Length;
            buffer = ArrayPool<byte>.Shared.Rent(totalLen);
            defaultPrefix.CopyTo(buffer);
            int decodedLen = CopyPercentDecoded(suffix, buffer.AsSpan(defaultPrefix.Length));
            return buffer.AsSpan(0, defaultPrefix.Length + decodedLen);
        }

        // Default resolution for ! (primary handle maps to !)
        if (handle.Length == 1 && handle[0] == (byte)'!')
        {
            int totalLen = 1 + suffix.Length;
            buffer = ArrayPool<byte>.Shared.Rent(totalLen);
            buffer[0] = (byte)'!';
            int decodedLen = CopyPercentDecoded(suffix, buffer.AsSpan(1));
            return buffer.AsSpan(0, 1 + decodedLen);
        }

        // Unknown handle — return raw
        return handle;
    }

    private static int CopyPercentDecoded(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        int written = 0;

        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] == (byte)'%' && i + 2 < source.Length)
            {
                int hi = HexDigitValue(source[i + 1]);
                int lo = HexDigitValue(source[i + 2]);

                if (hi >= 0 && lo >= 0)
                {
                    destination[written++] = (byte)((hi << 4) | lo);
                    i += 2;
                    continue;
                }
            }

            destination[written++] = source[i];
        }

        return written;
    }

    private static int HexDigitValue(byte c) => c switch
    {
        >= (byte)'0' and <= (byte)'9' => c - (byte)'0',
        >= (byte)'a' and <= (byte)'f' => c - (byte)'a' + 10,
        >= (byte)'A' and <= (byte)'F' => c - (byte)'A' + 10,
        _ => -1,
    };

    private void EnsureOutputCapacity(ref Span<byte> output, ref byte[]? rentedBuffer, int written, int needed)
    {
        if (written + needed > output.Length) { GrowOutputBuffer(ref output, ref rentedBuffer, written, needed); }
    }

    private void GrowOutputBuffer(ref Span<byte> output, ref byte[]? rentedBuffer, int written, int needed)
    {
        int newSize = Math.Max(output.Length * 2, written + needed);
        byte[] newArray = ArrayPool<byte>.Shared.Rent(newSize);
        output.Slice(0, written).CopyTo(newArray);
        if (rentedBuffer != null) { ArrayPool<byte>.Shared.Return(rentedBuffer); }

        rentedBuffer = newArray;
        output = newArray;
    }

    [DoesNotReturn]
    private void ThrowUnexpectedCharacter(byte c) { throw new YamlException(SR.Format(SR.UnexpectedCharacter, (char)c, _line + 1, _column + 1)); }

    [DoesNotReturn]
    private void ThrowUnexpectedEndOfInput() { throw new YamlException(SR.Format(SR.UnexpectedEndOfInput, _line + 1, _column + 1)); }

    [DoesNotReturn]
    private void ThrowUnterminatedDoubleQuotedScalar() { throw new YamlException(SR.Format(SR.UnterminatedDoubleQuotedScalar, _line + 1, _column + 1)); }

    [DoesNotReturn]
    private void ThrowUnterminatedSingleQuotedScalar() { throw new YamlException(SR.Format(SR.UnterminatedSingleQuotedScalar, _line + 1, _column + 1)); }

    [DoesNotReturn]
    private void ThrowUnterminatedFlowMapping() { throw new YamlException(SR.Format(SR.UnterminatedFlowMapping, _line + 1, _column + 1)); }

    [DoesNotReturn]
    private void ThrowUnterminatedFlowSequence() { throw new YamlException(SR.Format(SR.UnterminatedFlowSequence, _line + 1, _column + 1)); }

    [DoesNotReturn]
    private void ThrowMaxNestingDepthExceeded() { throw new YamlException(SR.Format(SR.MaxNestingDepthExceeded, YamlConstants.MaxNestingDepth, _line + 1, _column + 1)); }

    [DoesNotReturn]
    private void ThrowExpectedMappingValue() { throw new YamlException(SR.Format(SR.ExpectedMappingValue, _line + 1, _column + 1)); }

    [DoesNotReturn]
    private void ThrowInvalidEscapeSequence()
    {
        byte c = (_pos + 1 < _buffer.Length) ? _buffer[_pos + 1] : (byte)0;
        throw new YamlException(SR.Format(SR.InvalidEscapeSequence, (char)c, _line + 1, _column + 1));
    }

    [DoesNotReturn]
    private void ThrowMissingFlowSeparator() { throw new YamlException(SR.Format(SR.MissingFlowSeparator, _line + 1, _column + 1)); }

    [DoesNotReturn]
    private void ThrowMultipleDocumentsNotAllowed() { throw new YamlException(SR.MultipleDocumentsNotAllowed); }

    [DoesNotReturn]
    private void ThrowTabInIndentation() { throw new YamlException(SR.Format(SR.TabInIndentation, _line + 1, _column + 1)); }

    [DoesNotReturn]
    private void ThrowDuplicateYamlDirective() { throw new YamlException(SR.Format(SR.DuplicateYamlDirective, _line + 1, _column + 1)); }

    [DoesNotReturn]
    private void ThrowInvalidDirective() { throw new YamlException(SR.Format(SR.InvalidDirective, _line + 1, _column + 1)); }

    [DoesNotReturn]
    private void ThrowDirectiveWithoutDocumentStart() { throw new YamlException(SR.Format(SR.DirectiveWithoutDocumentStart, _line + 1, _column + 1)); }

    [DoesNotReturn]
    private void ThrowTrailingContentAfterNode() { throw new YamlException(SR.Format(SR.TrailingContentAfterNode, _line + 1, _column + 1)); }

    [DoesNotReturn]
    private void ThrowInvalidFlowSequenceEntry() { throw new YamlException(SR.Format(SR.InvalidFlowSequenceEntry, _line + 1, _column + 1)); }

    [DoesNotReturn]
    private void ThrowContentAfterDocumentEndMarker() { throw new YamlException(SR.Format(SR.ContentAfterDocumentEndMarker, _line + 1, _column + 1)); }

    [DoesNotReturn]
    private void ThrowBlockCollectionOnSameLine() { throw new YamlException(SR.Format(SR.BlockCollectionOnSameLine, _line + 1, _column + 1)); }

    [DoesNotReturn]
    private void ThrowMultilineImplicitKeyNotAllowed() { throw new YamlException(SR.Format(SR.MultilineImplicitKeyNotAllowed, _line + 1, _column + 1)); }

    [DoesNotReturn]
    private void ThrowMultipleAnchors() { throw new YamlException(SR.Format(SR.MultipleAnchors, _line + 1, _column + 1)); }

    [DoesNotReturn]
    private void ThrowDuplicateAnchor() { throw new YamlException(SR.Format(SR.DuplicateAnchor, _line + 1, _column + 1)); }

    [DoesNotReturn]
    private void ThrowInvalidTagHandle() { throw new YamlException(SR.Format(SR.InvalidTagHandle, _line + 1, _column + 1)); }

    private void ThrowCommentMustBePreceededByWhitespace() { throw new YamlException(SR.Format(SR.CommentMustBePreceededByWhitespace, _line + 1, _column + 1)); }

    private void ThrowInvalidBlockScalarHeader() { throw new YamlException(SR.Format(SR.InvalidBlockScalarHeader, _line + 1, _column + 1)); }

    private void ThrowContentOnDocumentStartLine() { throw new YamlException(SR.Format(SR.ContentOnDocumentStartLine, _line + 1, _column + 1)); }

    private void ThrowInvalidBlockScalarIndent() { throw new YamlException(SR.Format(SR.FlowContentUnderindented, _line + 1, _column + 1)); }

    [DoesNotReturn]
    private void ThrowAliasCannotHaveProperties() { throw new YamlException(SR.Format(SR.AliasCannotHaveProperties, _line + 1, _column + 1)); }
}