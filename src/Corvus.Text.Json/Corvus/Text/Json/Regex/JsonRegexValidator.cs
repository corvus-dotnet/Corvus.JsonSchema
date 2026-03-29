// <copyright file="JsonRegexValidator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Corvus.Text.Json.Internal;

/// <summary>Builds a tree of RegexNodes from a regular expression.</summary>
internal ref struct JsonRegexValidator
{
    private const int CapDefaultSize = 64;

    private const int EscapeMaxBufferSize = 256;

    private const int MaxValueDiv10 = int.MaxValue / 10;

    private const int MaxValueMod10 = int.MaxValue % 10;

    private const int NodeDbDefaultSize = 256;

    // Implementation notes:
    // It would be nice to get rid of the comment modes, since the
    // ScanBlank() calls are just kind of duct-taped in.
    private const int OptionStackDefaultSize = 32;

    private const byte Q = 4;

    // quantifier          * + ? {
    private const byte S = 3;

    private const byte W = 1;

    // stopper             $ ( ) . [ \ ^ |
    private const byte Z = 2;

    private readonly ReadOnlySpan<char> _pattern;

    private JsonRegexNode _alternation;

    private int _autocap;

    private int _capcount;

    private ValueListBuilder<CapNameToCapNumberRow> _capNames;

    private ValueListBuilder<CapToPos> _caps;

    private int _captop;

    private JsonRegexNode _concatenation;

    private JsonRegexNode _group;

    private bool _ignoreNextParen;

    private ValueListBuilder<NodeDbRow> _nodeDb;

    private JsonRegexOptions _options;

    // NOTE: _optionsStack is ValueListBuilder<int> to ensure that
    // ArrayPool<int>.Shared, not ArrayPool<RegexOptions>.Shared,
    // will be created if the stackalloc'd capacity is ever exceeded.
    private ValueListBuilder<int> _optionsStack;

    private int _pos;

    private JsonRegexNode _stack;

    private JsonRegexNode _unit;

    private JsonRegexValidator(
            ReadOnlySpan<char> pattern,
            JsonRegexOptions options,
            Span<int> optionSpan,
            Span<NodeDbRow> nodeDbSpan,
            Span<CapNameToCapNumberRow> capNamesSpan,
            Span<CapToPos> capsSpan)
    {
        _pattern = pattern;
        _options = options;

        _optionsStack = new ValueListBuilder<int>(optionSpan);
        _stack = JsonRegexNode.Null;
        _group = JsonRegexNode.Null;
        _alternation = JsonRegexNode.Null;
        _concatenation = JsonRegexNode.Null;
        _unit = JsonRegexNode.Null;
        _pos = 0;
        _autocap = 0;
        _capcount = 0;
        _captop = 0;
        _ignoreNextParen = false;
    }

    /// <summary>For categorizing ASCII characters.</summary>
    private static ReadOnlySpan<byte> Category =>
    [
#pragma warning disable SA1515 // Single-line comment should be preceded by blank line
        // 0  1  2  3  4  5  6  7  8  9  A  B  C  D  E  F  0  1  2  3  4  5  6  7  8  9  A  B  C  D  E  F
#pragma warning restore SA1515
           0, 0, 0, 0, 0, 0, 0, 0, 0, W, W, 0, W, W, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,

        // !  "  #  $  %  &  '  (  )  *  +,  -  .  /  0  1  2  3  4  5  6  7  8  9  :;  <  =  >  ?
           W, 0, 0, Z, S, 0, 0, 0, S, S, Q, Q, 0, 0, S, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, Q,

        // @  A  B  C  D  E  F  G  H  I  J  K  L  M  N  O  P  Q  R  S  T  U  V  W  X  Y  Z  [  \  ]  ^  _
           0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, S, S, 0, S, 0,

        // '  a  b  c  d  e  f  g  h  i  j  k  l  m  n  o  p  q  r  s  t  u  v  w  x  y  z  {  |  }  ~
           0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, Q, S, 0, 0, 0
    ];

    // flag to skip capturing a parentheses group

    /// <summary>
    /// Validates a regular expression pattern with the specified options.
    /// </summary>
    /// <param name="pattern">The regular expression pattern to validate.</param>
    /// <param name="options">The options to use during validation.</param>
    /// <returns><see langword="true"/> if the pattern is valid; otherwise, <see langword="false"/>.</returns>
    public static bool Validate(ReadOnlySpan<char> pattern, JsonRegexOptions options)
    {
        using var validator = new JsonRegexValidator(
            pattern,
            options,
            stackalloc int[OptionStackDefaultSize],
            stackalloc NodeDbRow[NodeDbDefaultSize],
            stackalloc CapNameToCapNumberRow[CapDefaultSize],
            stackalloc CapToPos[CapDefaultSize]);

        if (!validator.CountCaptures(out _))
        {
            return false;
        }

        validator.Reset(options);

        return validator.ScanRegex();
    }

    /// <summary>
    /// Disposes the resources held by this validator.
    /// </summary>
    public void Dispose()
    {
        _optionsStack.Dispose();
        _nodeDb.Dispose();
        _capNames.Dispose();
        _caps.Dispose();
    }

    /// <summary>
    /// Creates a new node with the specified kind.
    /// </summary>
    /// <param name="nodeKind">The kind of node to create.</param>
    /// <returns>A new regex node.</returns>
    internal JsonRegexNode CreateNode(JsonRegexNodeKind nodeKind)
    {
        NodeDbRow row = new(-1, nodeKind, 0);
        _nodeDb.Append(row);
        return new(_nodeDb.Length - 1);
    }

    /// <summary>
    /// Gets the number of child nodes for the specified node index.
    /// </summary>
    /// <param name="idx">The index of the node.</param>
    /// <returns>The number of child nodes.</returns>
    internal readonly int GetChildCount(int idx)
    {
        Debug.Assert(idx >= 0 && idx < _nodeDb.Length, "Index out of bounds for node database.");
        return _nodeDb[idx].ChildCount;
    }

    /// <summary>
    /// Gets the node kind for the specified node index.
    /// </summary>
    /// <param name="idx">The index of the node.</param>
    /// <returns>The node kind, or <see cref="JsonRegexNodeKind.Unknown"/> if the index is invalid.</returns>
    internal readonly JsonRegexNodeKind GetNodeKind(int idx)
    {
        if (idx < 0)
        {
            return JsonRegexNodeKind.Unknown;
        }

        Debug.Assert(idx < _nodeDb.Length, "Index out of bounds for node database.");
        return _nodeDb[idx].NodeKind;
    }

    /// <summary>
    /// Gets the parent node for the specified node index.
    /// </summary>
    /// <param name="idx">The index of the node.</param>
    /// <returns>The parent node.</returns>
    internal readonly JsonRegexNode GetParent(int idx)
    {
        Debug.Assert(idx >= 0 && idx < _nodeDb.Length, "Index out of bounds for node database.");
        return _nodeDb[idx].Parent;
    }

    /// <summary>
    /// Sets the node kind for the specified node index.
    /// </summary>
    /// <param name="idx">The index of the node.</param>
    /// <param name="kind">The new node kind.</param>
    internal void SetKind(int idx, JsonRegexNodeKind kind)
    {
        Debug.Assert(idx >= 0 && idx < _nodeDb.Length, "Index out of bounds for node database.");
        NodeDbRow row = _nodeDb[idx];
        _nodeDb[idx] = new NodeDbRow(row.ParentIndex, kind, row.ChildCount);
    }

    /// <summary>
    /// Sets the parent node for a child node.
    /// </summary>
    /// <param name="child">The index of the child node.</param>
    /// <param name="parent">The index of the parent node.</param>
    internal void SetParent(int child, int parent)
    {
        Debug.Assert(child >= 0 && child < _nodeDb.Length, "Index out of bounds for node database.");
        Debug.Assert(parent >= -1 && parent < _nodeDb.Length, "Index out of bounds for node database.");
        NodeDbRow childRow = _nodeDb[child];
        _nodeDb[child] = new NodeDbRow(parent, childRow.NodeKind, childRow.ChildCount);
        if (parent >= 0)
        {
            NodeDbRow parentRow = _nodeDb[parent];
            _nodeDb[parent] = new NodeDbRow(parentRow.ParentIndex, parentRow.NodeKind, parentRow.ChildCount + 1);
        }
    }

    /// <summary>Returns true for those characters that begin a quantifier.</summary>
    private static bool IsQuantifier(char ch) => ch <= '{' && Category[ch] == Q;

    /// <summary>Returns true for whitespace.</summary>
    private static bool IsSpace(char ch) => ch <= ' ' && Category[ch] == W;

    // # stopper           #
    // whitespace          \t \n \f \r ' '

    /// <summary>Returns true for those characters that terminate a string of ordinary chars.</summary>
    private static bool IsSpecial(char ch) => ch <= '|' && Category[ch] >= S;

    /// <summary>Returns true for those characters including whitespace that terminate a string of ordinary chars.</summary>
    private static bool IsSpecialOrSpace(char ch) => ch <= '|' && Category[ch] >= W;

    /// <summary>Finish the current concatenation (in response to a |)</summary>
    private void AddAlternate()
    {
        // The | parts inside a Testgroup group go directly to the group
        if (_group.GetNodeKind(ref this) is JsonRegexNodeKind.ExpressionConditional or JsonRegexNodeKind.BackreferenceConditional)
        {
            _group.AddChild(ref this, _concatenation);
        }
        else
        {
            _alternation.AddChild(ref this, _concatenation);
        }

        _concatenation = CreateNode(JsonRegexNodeKind.Concatenate);
    }

    /// <summary>Finish the current group (in response to a ')' or end)</summary>
    private bool AddGroup()
    {
        JsonRegexNodeKind groupKind = _group.GetNodeKind(ref this);
        if (groupKind is JsonRegexNodeKind.ExpressionConditional or JsonRegexNodeKind.BackreferenceConditional)
        {
            _group.AddChild(ref this, _concatenation);
            int groupChildCount = _group.GetChildCount(ref this);
            if ((groupKind == JsonRegexNodeKind.BackreferenceConditional && groupChildCount > 2) || groupChildCount > 3)
            {
                return false;
            }
        }
        else
        {
            _alternation.AddChild(ref this, _concatenation);
            _group.AddChild(ref this, _alternation);
        }

        _unit = _group;
        return true;
    }

    /// <summary>Add a string to the last concatenate.</summary>
    private void AddToConcatenate(int pos, int cch, bool isReplacement)
    {
        switch (cch)
        {
            case 0:
                return;

            case 1:
                _concatenation.AddChild(ref this, CreateNode(JsonRegexNodeKind.One));
                break;

            case > 1 when (_options & JsonRegexOptions.IgnoreCase) == 0 || isReplacement || !JsonRegexCharClass.ParticipatesInCaseConversion(_pattern.Slice(pos, cch)):
                _concatenation.AddChild(ref this, CreateNode(JsonRegexNodeKind.Multi));
                break;

            default:
                for (int i = 0; i < cch; i++)
                {
                    _concatenation.AddChild(ref this, CreateNode(JsonRegexNodeKind.One));
                }

                break;
        }
    }

    private void AssignNameSlots()
    {
        for (int i = 0; i < _capNames.Length; i++)
        {
            while (IsCaptureSlot(_autocap, requireExplicit: (_options & JsonRegexOptions.ECMAScript) != 0))
            {
                _autocap++;
            }

            CapNameToCapNumberRow row = _capNames[i];
            NoteCaptureSlot(_autocap, row.CapNum, true);

            _autocap++;
        }
    }

    /// <summary>
    /// A prescanner for deducing the slots used for captures by doing a partial tokenization of the pattern.
    /// </summary>
    private bool CountCaptures(out JsonRegexOptions optionsFoundInPattern)
    {
        NoteCaptureSlot(0, 0, false);
        optionsFoundInPattern = JsonRegexOptions.None;
        _autocap = 1;

        while (_pos < _pattern.Length)
        {
            int pos = _pos;
            char ch = _pattern[_pos++];
            switch (ch)
            {
                case '\\':
                    if (_pos < _pattern.Length)
                    {
                        if (!ScanBackslash(scanOnly: true, out _))
                        {
                            return false;
                        }
                    }

                    break;

                case '#':
                    if ((_options & JsonRegexOptions.IgnorePatternWhitespace) != 0)
                    {
                        --_pos;
                        if (!ScanBlank())
                        {
                            return false;
                        }
                    }

                    break;

                case '[':
                    if (!ScanCharClass(caseInsensitive: false, scanOnly: true))
                    {
                        return false;
                    }

                    break;

                case ')':
                    if (_optionsStack.Length != 0)
                    {
                        _options = (JsonRegexOptions)_optionsStack.Pop();
                    }

                    break;

                case '(':
                    if (_pos + 1 < _pattern.Length && _pattern[_pos + 1] == '#' && _pattern[_pos] == '?')
                    {
                        // we have a comment (?#
                        --_pos;
                        if (!ScanBlank())
                        {
                            return false;
                        }
                    }
                    else
                    {
                        _optionsStack.Append((int)_options);
                        if (_pos < _pattern.Length && _pattern[_pos] == '?')
                        {
                            // we have (?...
                            _pos++;

                            if (_pos + 1 < _pattern.Length && (_pattern[_pos] == '<' || _pattern[_pos] == '\''))
                            {
                                // named group: (?<... or (?'...
                                _pos++;
                                ch = _pattern[_pos];

                                if (ch != '0' && JsonRegexCharClass.IsBoundaryWordChar(ch))
                                {
                                    if ((uint)(ch - '1') <= '9' - '1')
                                    {
                                        if (!ScanDecimal(out int dec))
                                        {
                                            return false;
                                        }

                                        NoteCaptureSlot(dec, pos, true);
                                    }
                                    else
                                    {
                                        if (!ScanCapname(out int start, out int end))
                                        {
                                            return false;
                                        }

                                        NoteCaptureName(start, end, pos);
                                    }
                                }
                            }
                            else
                            {
                                // (?...
                                // get the options if it's an option construct (?cimsx-cimsx...)
                                ScanOptions();
                                optionsFoundInPattern |= _options;

                                if (_pos < _pattern.Length)
                                {
                                    if (_pattern[_pos] == ')')
                                    {
                                        // (?cimsx-cimsx)
                                        _pos++;
                                        _optionsStack.Length--;
                                    }
                                    else if (_pattern[_pos] == '(')
                                    {
                                        // alternation construct: (?(foo)yes|no)
                                        // ignore the next paren so we don't capture the condition
                                        _ignoreNextParen = true;

                                        // break from here so we don't reset _ignoreNextParen
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Simple (unnamed) capture group.
                            // Add unnamed parentheses if ExplicitCapture is not set
                            // and the next parentheses is not ignored.
                            if ((_options & JsonRegexOptions.ExplicitCapture) == 0 && !_ignoreNextParen)
                            {
                                NoteCaptureSlot(_autocap++, pos, true);
                            }
                        }
                    }

                    _ignoreNextParen = false;
                    break;
            }
        }

        AssignNameSlots();

        return true;
    }

    /// <summary>True if the capture slot was noted</summary>
    private readonly bool IsCaptureSlot(int i, bool requireExplicit)
    {
        return TryGetCapturePos(i, requireExplicit, out _);
    }

    private readonly bool IsTrueQuantifier()
    {
        Debug.Assert(_pos < _pattern.Length, "The current reading position must not be at the end of the pattern");

        int startpos = _pos;
        char ch = _pattern[startpos];
        if (ch != '{')
        {
            return ch <= '{' && Category[ch] >= Q;
        }

        int pos = startpos;
        int nChars = _pattern.Length - _pos;
        while (--nChars > 0 && (uint)((ch = _pattern[++pos]) - '0') <= 9);

        if (nChars == 0 || pos - startpos == 1)
        {
            return false;
        }

        if (ch == '}')
        {
            return true;
        }

        if (ch != ',')
        {
            return false;
        }

        while (--nChars > 0 && (uint)((ch = _pattern[++pos]) - '0') <= 9);

        return nChars > 0 && ch == '}';
    }

    /// <summary>Notes a used capture slot</summary>
    private void NoteCaptureName(int start, int end, int index)
    {
        if (!TryGetCapNum(start, end, out _))
        {
            _capNames.Append(new CapNameToCapNumberRow(start, end, index));
        }
    }

    /// <summary>Notes a used capture slot</summary>
    private void NoteCaptureSlot(int i, int pos, bool isExplicit)
    {
        if (!TryGetCapturePos(i, requireExplicit: false, out _))
        {
            _caps.Append(new CapToPos(i, pos, isExplicit));
            _capcount++;

            if (_captop <= i)
            {
                _captop = i == int.MaxValue ? i : i + 1;
            }
        }
    }

    /// <summary>Scans X for \p{X} or \P{X}</summary>
    private bool ParseProperty(out ReadOnlySpan<char> result)
    {
        if (_pos + 2 >= _pattern.Length)
        {
            result = default;
            return false;
        }

        char ch = _pattern[_pos++];
        if (ch != '{')
        {
            result = default;
            return false;
        }

        int startpos = _pos;
        while (_pos < _pattern.Length)
        {
            ch = _pattern[_pos++];
            if (!(JsonRegexCharClass.IsBoundaryWordChar(ch) || ch == '-'))
            {
                --_pos;
                break;
            }
        }

        if (_pos == _pattern.Length || _pattern[_pos++] != '}')
        {
            result = default;
            return false;
        }

        result = _pattern.Slice(startpos, (_pos - 1) - startpos);
        return true;
    }

    /// <summary>Remember the pushed state (in response to a ')')</summary>
    private bool PopGroup()
    {
        _concatenation = _stack;
        _alternation = _concatenation.GetParent(ref this);
        _group = _alternation.GetParent(ref this);
        _stack = _group.GetParent(ref this);

        // The first () inside a Testgroup group goes directly to the group
        if (_group.GetNodeKind(ref this) == JsonRegexNodeKind.ExpressionConditional && _group.GetChildCount(ref this) == 0)
        {
            if (_unit.IsNull)
            {
                return false;
            }

            _group.AddChild(ref this, _unit);
            _unit = JsonRegexNode.Null;
        }

        return true;
    }

    /// <summary>Push the validator state (in response to an open paren)</summary>
    private void PushGroup()
    {
        _group.SetParent(ref this, _stack);
        _alternation.SetParent(ref this, _group);
        _concatenation.SetParent(ref this, _alternation);
        _stack = _concatenation;
    }

    /// <summary>Resets parsing to the beginning of the pattern</summary>
    private void Reset(JsonRegexOptions options)
    {
        _pos = 0;
        _autocap = 1;
        _ignoreNextParen = false;
        _optionsStack.Length = 0;
        _options = options;
        _stack = JsonRegexNode.Null;
    }

    /// <summary>Scans chars following a '\' (not counting the '\'), and returns a RegexNode for the type of atom scanned</summary>
    private bool ScanBackslash(bool scanOnly, out JsonRegexNode node)
    {
        Debug.Assert(_pos < _pattern.Length, "The current reading position must not be at the end of the pattern");

        char ch;
        switch (ch = _pattern[_pos])
        {
            case 'b':
            case 'B':
            case 'A':
            case 'G':
            case 'Z':
            case 'z':
                _pos++;
                node = scanOnly ? JsonRegexNode.Null :
                    CreateNode(TypeFromCode(ch));
                return true;

            case 'w':
                _pos++;
                node = scanOnly ? JsonRegexNode.Null :
                    CreateNode(JsonRegexNodeKind.Set);
                return true;

            case 'W':
                _pos++;
                node = scanOnly ? JsonRegexNode.Null :
                    CreateNode(JsonRegexNodeKind.Set);
                return true;

            case 's':
                _pos++;
                node = scanOnly ? JsonRegexNode.Null :
                    CreateNode(JsonRegexNodeKind.Set);
                return true;

            case 'S':
                _pos++;
                node = scanOnly ? JsonRegexNode.Null :
                    CreateNode(JsonRegexNodeKind.Set);
                return true;

            case 'd':
                _pos++;
                node = scanOnly ? JsonRegexNode.Null :
                    CreateNode(JsonRegexNodeKind.Set);
                return true;

            case 'D':
                _pos++;
                node = scanOnly ? JsonRegexNode.Null :
                    CreateNode(JsonRegexNodeKind.Set);
                return true;

            case 'p':
            case 'P':
                _pos++;
                if (scanOnly)
                {
                    node = JsonRegexNode.Null;
                    return true;
                }

                if (!ParseProperty(out ReadOnlySpan<char> propertyName))
                {
                    node = JsonRegexNode.Null;
                    return false;
                }

                if (!JsonRegexCharClass.ValidateCategoryName(propertyName))
                {
                    node = JsonRegexNode.Null;
                    return false;
                }

                node = CreateNode(JsonRegexNodeKind.Set);
                return true;

            default:
                return ScanBasicBackslash(scanOnly, out node);
        }
    }

    /// <summary>Scans \-style backreferences and character escapes</summary>
    private bool ScanBasicBackslash(bool scanOnly, out JsonRegexNode node)
    {
        Debug.Assert(_pos < _pattern.Length, "The current reading position must not be at the end of the pattern");

        int backpos = _pos;
        char close = '\0';
        bool angled = false;
        char ch = _pattern[_pos];

        // allow \k<foo> instead of \<foo>, which is now deprecated
        if (ch == 'k')
        {
            if (_pos + 1 < _pattern.Length)
            {
                _pos++;
                ch = _pattern[_pos++];
                if (ch is '<' or '\'')
                {
                    angled = true;
                    close = (ch == '\'') ? '\'' : '>';
                }
            }

            if (!angled || _pos == _pattern.Length)
            {
                node = JsonRegexNode.Null;
                return false;
            }

            ch = _pattern[_pos];
        }

        // Note angle without \g
        else if ((ch == '<' || ch == '\'') && _pos + 1 < _pattern.Length)
        {
            angled = true;
            close = (ch == '\'') ? '\'' : '>';
            _pos++;
            ch = _pattern[_pos];
        }

        // Try to parse backreference: \<1>
        if (angled && ch >= '0' && ch <= '9')
        {
            if (!ScanDecimal(out int capnum))
            {
                node = JsonRegexNode.Null;
                return false;
            }

            if (_pos < _pattern.Length && _pattern[_pos++] == close)
            {
                if (scanOnly)
                {
                    node = JsonRegexNode.Null;
                    return true;
                }

                if (IsCaptureSlot(capnum, requireExplicit: (_options & JsonRegexOptions.ECMAScript) != 0))
                {
                    node = CreateNode(JsonRegexNodeKind.Backreference);
                    return true;
                }

                node = JsonRegexNode.Null;
                return false;
            }
        }

        // Try to parse backreference or octal: \1
        else if (!angled && ch >= '1' && ch <= '9')
        {
            if ((_options & JsonRegexOptions.ECMAScript) != 0)
            {
                int capnum = -1;
                int newcapnum = ch - '0';
                int pos = _pos - 1;
                while (newcapnum <= _captop)
                {
                    if (IsCaptureSlot(newcapnum, requireExplicit: (_options & JsonRegexOptions.ECMAScript) != 0))
                    {
                        // If we have a capture pos and it greater than or equal to the current position
                        // then this is a forward reference not a backwards reference
                        bool isForwardReference = TryGetCapturePos(newcapnum, requireExplicit: (_options & JsonRegexOptions.ECMAScript) != 0, out int capturePos) && capturePos >= pos;
                        if (!isForwardReference)
                        {
                            capnum = newcapnum;
                        }
                    }

                    _pos++;
                    if (_pos == _pattern.Length || (ch = _pattern[_pos]) < '0' || ch > '9')
                    {
                        break;
                    }

                    newcapnum = newcapnum * 10 + (ch - '0');
                }

                if (capnum >= 0)
                {
                    node = scanOnly ? JsonRegexNode.Null : CreateNode(JsonRegexNodeKind.Backreference);
                    return true;
                }
            }
            else
            {
                if (!ScanDecimal(out int capnum))
                {
                    node = JsonRegexNode.Null;
                    return false;
                }

                if (scanOnly)
                {
                    node = JsonRegexNode.Null;
                    return true;
                }

                if (IsCaptureSlot(capnum, requireExplicit: (_options & JsonRegexOptions.ECMAScript) != 0))
                {
                    node = CreateNode(JsonRegexNodeKind.Backreference);
                    return true;
                }

                if (capnum <= 9)
                {
                    node = JsonRegexNode.Null;
                    return false;
                }
            }
        }

        // Try to parse backreference: \<foo>
        else if (angled && JsonRegexCharClass.IsBoundaryWordChar(ch))
        {
            if (!ScanCapname(out int capnameStart, out int capnameEnd))
            {
                node = JsonRegexNode.Null;
                return false;
            }

            if (_pos < _pattern.Length && _pattern[_pos++] == close)
            {
                if (scanOnly)
                {
                    node = JsonRegexNode.Null;
                    return true;
                }

                if (!TryGetCapNum(capnameStart, capnameEnd, out _))
                {
                    node = JsonRegexNode.Null;
                    return false;
                }

                node = CreateNode(JsonRegexNodeKind.Backreference);
                return true;
            }
        }

        // Not backreference: must be char code
        _pos = backpos;
        if (!ScanCharEscape(out _))
        {
            node = JsonRegexNode.Null;
            return false;
        }

        if (scanOnly)
        {
            node = JsonRegexNode.Null;
            return true;
        }

        node = CreateNode(JsonRegexNodeKind.One);
        return true;
    }

    /// <summary>Scans whitespace or x-mode comments</summary>
    private bool ScanBlank()
    {
        while (true)
        {
            if ((_options & JsonRegexOptions.IgnorePatternWhitespace) != 0)
            {
                while (_pos < _pattern.Length && IsSpace(_pattern[_pos]))
                {
                    _pos++;
                }
            }

            if ((_options & JsonRegexOptions.IgnorePatternWhitespace) != 0 && _pos < _pattern.Length && _pattern[_pos] == '#')
            {
                int idx = _pattern.Slice(_pos).IndexOf('\n');
                if (idx < 0)
                {
                    _pos = _pattern.Length;
                }
                else
                {
                    _pos += idx;
                }
            }
            else if (_pos + 2 < _pattern.Length && _pattern[_pos + 2] == '#' && _pattern[_pos + 1] == '?' && _pattern[_pos] == '(')
            {
                int idx = _pattern.Slice(_pos).IndexOf(')');
                if (idx < 0)
                {
                    _pos = _pattern.Length;
                    return false;
                }

                _pos += idx + 1;
            }
            else
            {
                break;
            }
        }

        return true;
    }

    /// <summary>Scans a capture name: consumes word chars</summary>
    private bool ScanCapname(out int start, out int end)
    {
        int startpos = _pos;

        while (_pos < _pattern.Length)
        {
            if (!JsonRegexCharClass.IsBoundaryWordChar(_pattern[_pos++]))
            {
                --_pos;
                break;
            }
        }

        start = startpos;
        end = _pos;
        return true;
    }

    /// <summary>Scans contents of [] (not including []'s)</summary>
    private bool ScanCharClass(bool caseInsensitive, bool scanOnly)
    {
        char ch;
        char chPrev = '\0';
        bool inRange = false;
        bool firstChar = true;
        bool closed = false;

        if (_pos < _pattern.Length && _pattern[_pos] == '^')
        {
            _pos++;

            if (_pos >= _pattern.Length)
            {
                return false;
            }

            if ((_options & JsonRegexOptions.ECMAScript) != 0 && _pattern[_pos] == ']')
            {
                firstChar = false;
            }
        }

        for (; _pos < _pattern.Length; firstChar = false)
        {
            bool translatedChar = false;
            ch = _pattern[_pos++];
            if (ch == ']')
            {
                if (!firstChar)
                {
                    closed = true;
                    break;
                }
            }
            else if (ch == '\\' && _pos < _pattern.Length)
            {
                switch (ch = _pattern[_pos++])
                {
                    case 'D':
                    case 'd':
                        if (!scanOnly)
                        {
                            if (inRange)
                            {
                                return false;
                            }
                        }

                        continue;

                    case 'S':
                    case 's':
                        if (!scanOnly)
                        {
                            if (inRange)
                            {
                                return false;
                            }
                        }

                        continue;

                    case 'W':
                    case 'w':
                        if (!scanOnly)
                        {
                            if (inRange)
                            {
                                return false;
                            }
                        }

                        continue;

                    case 'p':
                    case 'P':
                        if (!scanOnly)
                        {
                            if (inRange)
                            {
                                return false;
                            }

                            if (!ParseProperty(out ReadOnlySpan<char> propertyName))
                            {
                                return false;
                            }

                            if (!JsonRegexCharClass.ValidateCategoryName(propertyName))
                            {
                                return false;
                            }
                        }
                        else
                        {
                            if (!ParseProperty(out _))
                            {
                                return false;
                            }
                        }

                        continue;

                    case '-':
                        if (!scanOnly)
                        {
                            if (inRange)
                            {
                                if (chPrev > ch)
                                {
                                    return false;
                                }

                                inRange = false;
                                chPrev = '\0';
                            }
                            else
                            {
                                // NOP
                            }
                        }

                        continue;

                    default:
                        --_pos;
                        if (!ScanCharEscape(out ch)) // non-literal char
                        {
                            return false;
                        }

                        translatedChar = true;
                        break; // this break will only break out of the switch
                }
            }

            if (inRange)
            {
                inRange = false;
                if (!scanOnly)
                {
                    if (ch == '[' && !translatedChar && !firstChar)
                    {
                        // We thought we were in a range, but we're actually starting a subtraction.
                        // In that case, we'll add chPrev to our char class, skip the opening [, and
                        // scan the new character class recursively.
                        if (!ScanCharClass(caseInsensitive, scanOnly))
                        {
                            return false;
                        }

                        if (_pos < _pattern.Length && _pattern[_pos] != ']')
                        {
                            return false;
                        }
                    }
                    else
                    {
                        // a regular range, like a-z
                        if (chPrev > ch)
                        {
                            return false;
                        }
                    }
                }
            }
            else if (_pos + 1 < _pattern.Length && _pattern[_pos] == '-' && _pattern[_pos + 1] != ']')
            {
                // this could be the start of a range
                chPrev = ch;
                inRange = true;
                _pos++;
            }
            else if (_pos < _pattern.Length && ch == '-' && !translatedChar && _pattern[_pos] == '[' && !firstChar)
            {
                // we aren't in a range, and now there is a subtraction.  Usually this happens
                // only when a subtraction follows a range, like [a-z-[b]]
                _pos++;
                if (!ScanCharClass(caseInsensitive, scanOnly))
                {
                    return false;
                }

                if (!scanOnly)
                {
                    if (_pos < _pattern.Length && _pattern[_pos] != ']')
                    {
                        return false;
                    }
                }
            }
        }

        return closed;
    }

    /// <summary>Scans \ code for escape codes that map to single Unicode chars.</summary>
    private bool ScanCharEscape(out char result)
    {
        char ch = _pattern[_pos++];

        if (ch is >= '0' and <= '7')
        {
            --_pos;
            return ScanOctal(out result);
        }

        switch (ch)
        {
            case 'x':
                if (ScanHex(2, out int hex2))
                {
                    result = (char)hex2;
                    return true;
                }

                result = default;
                return false;

            case 'u':
                if (ScanHex(4, out int hex4))
                {
                    result = (char)hex4;
                    return true;
                }

                result = default;
                return false;

            case 'a':
                if ((_options & JsonRegexOptions.ECMAScript) == 0)
                {
                    result = '\u0007';
                    return true;
                }

                result = default;
                return false;

            case 'b':
                result = '\b';
                return true;

            case 'e':
                result = '\e';
                return true;

            case 'f':
                result = '\f';
                return true;

            case 'n':
                result = '\n';
                return true;

            case 'r':
                result = '\r';
                return true;

            case 't':
                result = '\t';
                return true;

            case 'v':
                result = '\u000B';
                return true;

            case '\\':
                result = '\\';
                return true;

            case 'c':
                return ScanControl(out result);

            default:
                if ((_options & JsonRegexOptions.ECMAScript) == 0 && JsonRegexCharClass.IsBoundaryWordChar(ch))
                {
                    result = default;
                    return false;
                }

                if ((_options & JsonRegexOptions.ECMAScript) != 0)
                {
                    result = default;
                    return false;
                }

                result = ch;
                return true;
        }
    }

    /// <summary>Grabs and converts an ASCII control character</summary>
    private bool ScanControl(out char result)
    {
        if (_pos == _pattern.Length)
        {
            result = default;
            return false;
        }

        char ch = _pattern[_pos++];

        // \ca interpreted as \cA
        if ((uint)(ch - 'a') <= 'z' - 'a')
        {
            ch = (char)(ch - ('a' - 'A'));
        }

        if ((ch = (char)(ch - '@')) < ' ')
        {
            result = ch;
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>Scans any number of decimal digits (pegs value at 2^31-1 if too large)</summary>
    private bool ScanDecimal(out int result)
    {
        int i = 0;
        int d;

        while (_pos < _pattern.Length && (uint)(d = (char)(_pattern[_pos] - '0')) <= 9)
        {
            _pos++;

            if (i > MaxValueDiv10 || (i == MaxValueDiv10 && d > MaxValueMod10))
            {
                result = default;
                return false;
            }

            i = (i * 10) + d;
        }

        result = i;
        return true;
    }

    /// <summary>
    /// Scans chars following a '(' (not counting the '('), and returns
    /// a RegexNode for the type of group scanned, or null if the group
    /// simply changed options (?cimsx-cimsx) or was a comment (#...).
    /// </summary>
    private bool ScanGroupOpen(out JsonRegexNode node)
    {
        // just return a RegexNode if we have:
        // 1. "(" followed by nothing
        // 2. "(x" where x != ?
        // 3. "(?)"
        if (_pos == _pattern.Length || _pattern[_pos] != '?' || (_pos + 1 < _pattern.Length && _pattern[_pos + 1] == ')'))
        {
            if ((_options & JsonRegexOptions.ExplicitCapture) != 0 || _ignoreNextParen)
            {
                _ignoreNextParen = false;
                node = CreateNode(JsonRegexNodeKind.Group);
                return true;
            }
            else
            {
                node = CreateNode(JsonRegexNodeKind.Capture);
                return true;
            }
        }

        _pos++;

        while (_pos != _pattern.Length)
        {
            JsonRegexNodeKind nodeType;
            char close = '>';
            char ch = _pattern[_pos++];
            switch (ch)
            {
                case ':':

                    // noncapturing group
                    nodeType = JsonRegexNodeKind.Group;
                    break;

                case '=':

                    // lookahead assertion
                    _options &= ~JsonRegexOptions.RightToLeft;
                    nodeType = JsonRegexNodeKind.PositiveLookaround;
                    break;

                case '!':

                    // negative lookahead assertion
                    _options &= ~JsonRegexOptions.RightToLeft;
                    nodeType = JsonRegexNodeKind.NegativeLookaround;
                    break;

                case '>':

                    // atomic subexpression
                    nodeType = JsonRegexNodeKind.Atomic;
                    break;

                case '\'':
                    close = '\'';
                    goto case '<'; // fallthrough

                case '<':
                    if (_pos == _pattern.Length)
                    {
                        goto BreakRecognize;
                    }

                    switch (ch = _pattern[_pos++])
                    {
                        case '=':
                            if (close == '\'')
                            {
                                goto BreakRecognize;
                            }

                            // lookbehind assertion
                            _options |= JsonRegexOptions.RightToLeft;
                            nodeType = JsonRegexNodeKind.PositiveLookaround;
                            break;

                        case '!':
                            if (close == '\'')
                            {
                                goto BreakRecognize;
                            }

                            // negative lookbehind assertion
                            _options |= JsonRegexOptions.RightToLeft;
                            nodeType = JsonRegexNodeKind.NegativeLookaround;
                            break;

                        default:
                            --_pos;
                            int capnum = -1;
                            int uncapnum = -1;
                            bool proceed = false;

                            // grab part before -
                            if ((uint)(ch - '0') <= 9)
                            {
                                if (!ScanDecimal(out capnum))
                                {
                                    node = JsonRegexNode.Null;
                                    return false;
                                }

                                if (!IsCaptureSlot(capnum, requireExplicit: (_options & JsonRegexOptions.ECMAScript) != 0))
                                {
                                    capnum = -1;
                                }

                                // check if we have bogus characters after the number
                                if (_pos < _pattern.Length && !(_pattern[_pos] == close || _pattern[_pos] == '-'))
                                {
                                    node = JsonRegexNode.Null;
                                    return false;
                                }

                                if (capnum == 0)
                                {
                                    node = JsonRegexNode.Null;
                                    return false;
                                }
                            }
                            else if (JsonRegexCharClass.IsBoundaryWordChar(ch))
                            {
                                if (!ScanCapname(out int capnameStart, out int capNameEnd))
                                {
                                    node = JsonRegexNode.Null;
                                    return false;
                                }

                                if (TryGetCapNum(capnameStart, capNameEnd, out int tmpCapnum))
                                {
                                    capnum = tmpCapnum;
                                }

                                // check if we have bogus character after the name
                                if (_pos < _pattern.Length && !(_pattern[_pos] == close || _pattern[_pos] == '-'))
                                {
                                    node = JsonRegexNode.Null;
                                    return false;
                                }
                            }
                            else if (ch == '-')
                            {
                                proceed = true;
                            }
                            else
                            {
                                // bad group name - starts with something other than a word character and isn't a number
                                node = JsonRegexNode.Null;
                                return false;
                            }

                            // grab part after - if any
                            if ((capnum != -1 || proceed) && _pos + 1 < _pattern.Length && _pattern[_pos] == '-')
                            {
                                _pos++;
                                ch = _pattern[_pos];

                                if ((uint)(ch - '0') <= 9)
                                {
                                    if (!ScanDecimal(out uncapnum))
                                    {
                                        node = JsonRegexNode.Null;
                                        return false;
                                    }

                                    if (!IsCaptureSlot(uncapnum, requireExplicit: (_options & JsonRegexOptions.ECMAScript) != 0))
                                    {
                                        node = JsonRegexNode.Null;
                                        return false;
                                    }

                                    // check if we have bogus characters after the number
                                    if (_pos < _pattern.Length && _pattern[_pos] != close)
                                    {
                                        node = JsonRegexNode.Null;
                                        return false;
                                    }
                                }
                                else if (JsonRegexCharClass.IsBoundaryWordChar(ch))
                                {
                                    if (!ScanCapname(out int uncapnameStart, out int uncapnameEnd))
                                    {
                                        node = JsonRegexNode.Null;
                                        return false;
                                    }

                                    if (TryGetCapNum(uncapnameStart, uncapnameEnd, out int tmpCapnum))
                                    {
                                        uncapnum = tmpCapnum;
                                    }
                                    else
                                    {
                                        node = JsonRegexNode.Null;
                                        return false;
                                    }

                                    // check if we have bogus character after the name
                                    if (_pos < _pattern.Length && _pattern[_pos] != close)
                                    {
                                        node = JsonRegexNode.Null;
                                        return false;
                                    }
                                }
                                else
                                {
                                    // bad group name - starts with something other than a word character and isn't a number
                                    node = JsonRegexNode.Null;
                                    return false;
                                }
                            }

                            // actually make the node
                            if ((capnum != -1 || uncapnum != -1) && _pos < _pattern.Length && _pattern[_pos++] == close)
                            {
                                node = CreateNode(JsonRegexNodeKind.Capture);
                                return true;
                            }

                            goto BreakRecognize;
                    }

                    break;

                case '(':

                    // conditional alternation construct (?(...) | )
                    int parenPos = _pos;
                    if (_pos < _pattern.Length)
                    {
                        ch = _pattern[_pos];

                        // check if the alternation condition is a backref
                        if (ch is >= '0' and <= '9')
                        {
                            if (!ScanDecimal(out int capnum))
                            {
                                node = JsonRegexNode.Null;
                                return false;
                            }

                            if (_pos < _pattern.Length && _pattern[_pos++] == ')')
                            {
                                if (IsCaptureSlot(capnum, requireExplicit: (_options & JsonRegexOptions.ECMAScript) != 0))
                                {
                                    node = CreateNode(JsonRegexNodeKind.BackreferenceConditional);
                                    return true;
                                }

                                node = JsonRegexNode.Null;
                                return false;
                            }

                            node = JsonRegexNode.Null;
                            return false;
                        }
                        else if (JsonRegexCharClass.IsBoundaryWordChar(ch))
                        {
                            if (!ScanCapname(out int capnameStart, out int capnameEnd))
                            {
                                node = JsonRegexNode.Null;
                                return false;
                            }

                            if (TryGetCapNum(capnameStart, capnameEnd, out _) && _pos < _pattern.Length && _pattern[_pos++] == ')')
                            {
                                node = CreateNode(JsonRegexNodeKind.BackreferenceConditional);
                                return true;
                            }
                        }
                    }

                    // not a backref
                    nodeType = JsonRegexNodeKind.ExpressionConditional;
                    _pos = parenPos - 1;       // jump to the start of the parentheses
                    _ignoreNextParen = true;    // but make sure we don't try to capture the insides

                    if (_pos + 2 < _pattern.Length && _pattern[_pos + 1] == '?')
                    {
                        // disallow comments in the condition
                        if (_pattern[_pos + 2] == '#')
                        {
                            node = JsonRegexNode.Null;
                            return false;
                        }

                        // disallow named capture group (?<..>..) in the condition
                        if (_pattern[_pos + 2] == '\'' || (_pos + 3 < _pattern.Length && _pattern[_pos + 2] == '<' && _pattern[_pos + 3] != '!' && _pattern[_pos + 3] != '='))
                        {
                            node = JsonRegexNode.Null;
                            return false;
                        }
                    }

                    break;

                default:
                    --_pos;

                    nodeType = JsonRegexNodeKind.Group;

                    // Disallow options in the children of a testgroup node
                    if (_group.GetNodeKind(ref this) != JsonRegexNodeKind.ExpressionConditional)
                    {
                        ScanOptions();
                    }

                    if (_pos == _pattern.Length)
                    {
                        goto BreakRecognize;
                    }

                    if ((ch = _pattern[_pos++]) == ')')
                    {
                        // NOTICE THAT WE HAVE A UNKNOWN NODE TYPE
                        // WITH A TRUE RETURN
                        node = JsonRegexNode.Null;
                        return true;
                    }

                    if (ch != ':')
                    {
                        goto BreakRecognize;
                    }

                    break;
            }

            node = CreateNode(nodeType);
            return true;
        }

    BreakRecognize:
        ;

        // break Recognize comes here
        node = JsonRegexNode.Null;
        return false;
    }

    /// <summary>Scans exactly c hex digits (c=2 for \xFF, c=4 for \uFFFF)</summary>
    private bool ScanHex(int c, out int hexResult)
    {
        int i = 0;

        if (_pos + c <= _pattern.Length)
        {
            for (; c > 0; --c)
            {
                char ch = _pattern[_pos++];
                int result = HexConverter.FromChar(ch);
                if (result == 0xFF)
                {
                    break;
                }

                i = (i * 0x10) + result;
            }
        }

        if (c > 0)
        {
            hexResult = 0;
            return false;
        }

        hexResult = (char)i;
        return true;
    }

    /// <summary>Scans up to three octal digits (stops before exceeding 0377)</summary>
    private bool ScanOctal(out char result)
    {
        // Consume octal chars only up to 3 digits and value 0377
        int c = Math.Min(3, _pattern.Length - _pos);
        int d;
        int i;
        for (i = 0; c > 0 && (uint)(d = _pattern[_pos] - '0') <= 7; --c)
        {
            _pos++;
            i = (i * 8) + d;
            if ((_options & JsonRegexOptions.ECMAScript) != 0 && i >= 0x20)
            {
                break;
            }
        }

        // Octal codes only go up to 255.  Any larger and the behavior that Perl follows
        // is simply to truncate the high bits.
        i &= 0xFF;

        result = (char)i;
        return true;
    }

    /// <summary>Scans cimsx-cimsx option string, stops at the first unrecognized char.</summary>
    private void ScanOptions()
    {
        for (bool off = false; _pos < _pattern.Length; _pos++)
        {
            char ch = _pattern[_pos];

            if (ch == '-')
            {
                off = true;
            }
            else if (ch == '+')
            {
                off = false;
            }
            else
            {
                JsonRegexOptions options = (char)(ch | 0x20) switch
                {
                    'i' => JsonRegexOptions.IgnoreCase,
                    'm' => JsonRegexOptions.Multiline,
                    'n' => JsonRegexOptions.ExplicitCapture,
                    's' => JsonRegexOptions.Singleline,
                    'x' => JsonRegexOptions.IgnorePatternWhitespace,
                    _ => JsonRegexOptions.None,
                };

                if (options == 0)
                {
                    return;
                }

                if (off)
                {
                    _options &= ~options;
                }
                else
                {
                    _options |= options;
                }
            }
        }
    }

    /// <summary>The main parsing function</summary>
    private bool ScanRegex()
    {
        char ch;

        // For the main Capture object, strip out the IgnoreCase option. The rest of the nodes will strip it out depending on the content
        // of each node.
        StartGroup(CreateNode(JsonRegexNodeKind.Capture));

        while (_pos < _pattern.Length)
        {
            bool isQuantifier = false;

            if (!ScanBlank())
            {
                return false;
            }

            int startpos = _pos;

            // move past all of the normal characters.  We'll stop when we hit some kind of control character,
            // or if IgnorePatternWhiteSpace is on, we'll stop when we see some whitespace.
            if ((_options & JsonRegexOptions.IgnorePatternWhitespace) != 0)
            {
                while (_pos < _pattern.Length && (!IsSpecialOrSpace(ch = _pattern[_pos]) || (ch == '{' && !IsTrueQuantifier())))
                    _pos++;
            }
            else
            {
                while (_pos < _pattern.Length && (!IsSpecial(ch = _pattern[_pos]) || (ch == '{' && !IsTrueQuantifier())))
                    _pos++;
            }

            int endpos = _pos;

            if (!ScanBlank())
            {
                return false;
            }

            if (_pos == _pattern.Length)
            {
                ch = '!'; // nonspecial, means at end
            }
            else if (IsSpecial(ch = _pattern[_pos]))
            {
                isQuantifier = IsQuantifier(ch);
                _pos++;
            }
            else
            {
                ch = ' '; // nonspecial, means at ordinary char
            }

            if (startpos < endpos)
            {
                int cchUnquantified = endpos - startpos - (isQuantifier ? 1 : 0);

                if (cchUnquantified > 0)
                {
                    AddToConcatenate(startpos, cchUnquantified, false);
                }

                if (isQuantifier)
                {
                    _unit = CreateNode(JsonRegexNodeKind.One);
                }
            }

            switch (ch)
            {
                case '!':
                    goto BreakOuterScan;

                case ' ':
                    goto ContinueOuterScan;

                case '[':
                {
                    if (!ScanCharClass((_options & JsonRegexOptions.IgnoreCase) != 0, scanOnly: false))
                    {
                        return false;
                    }

                    _unit = CreateNode(JsonRegexNodeKind.Set);
                }

                break;

                case '(':
                    _optionsStack.Append((int)_options);
                    if (!ScanGroupOpen(out JsonRegexNode grouper))
                    {
                        return false;
                    }

                    if (!grouper.IsNull)
                    {
                        PushGroup();
                        StartGroup(grouper);
                    }
                    else
                    {
                        _optionsStack.Length--;
                    }

                    continue;

                case '|':
                    AddAlternate();
                    goto ContinueOuterScan;

                case ')':
                    if (_stack.IsNull)
                    {
                        return false;
                    }

                    if (!AddGroup())
                    {
                        return false;
                    }

                    if (!PopGroup())
                    {
                        return false;
                    }

                    _options = (JsonRegexOptions)_optionsStack.Pop();

                    if (_unit.IsNull)
                    {
                        goto ContinueOuterScan;
                    }

                    break;

                case '\\':
                    if (_pos == _pattern.Length)
                    {
                        return false;
                    }

                    if (!ScanBackslash(scanOnly: false, out _unit))
                    {
                        return false;
                    }

                    break;

                case '^':
                    _unit = CreateNode((_options & JsonRegexOptions.Multiline) != 0 ? JsonRegexNodeKind.Bol : JsonRegexNodeKind.Beginning);
                    break;

                case '$':
                    _unit = CreateNode((_options & JsonRegexOptions.Multiline) != 0 ? JsonRegexNodeKind.Eol : JsonRegexNodeKind.EndZ);
                    break;

                case '.':
                    _unit = (_options & JsonRegexOptions.Singleline) != 0 ?
                        CreateNode(JsonRegexNodeKind.Set) :
                        CreateNode(JsonRegexNodeKind.Notone);
                    break;

                case '{':
                case '*':
                case '+':
                case '?':
                    if (_unit.IsNull)
                    {
                        return false;
                    }

                    --_pos;
                    break;

                default:
                    Debug.Fail($"Unexpected char {ch}");
                    break;
            }

            if (!ScanBlank())
            {
                return false;
            }

            if (_pos == _pattern.Length || !(_ = IsTrueQuantifier()))
            {
                _concatenation.AddChild(ref this, _unit);
                _unit = JsonRegexNode.Null;
                goto ContinueOuterScan;
            }

            ch = _pattern[_pos++];

            // Handle quantifiers
            while (!_unit.IsNull)
            {
                int min = 0, max = 0;

                switch (ch)
                {
                    case '*':
                        max = int.MaxValue;
                        break;

                    case '?':
                        max = 1;
                        break;

                    case '+':
                        min = 1;
                        max = int.MaxValue;
                        break;

                    case '{':
                        startpos = _pos;
                        if (!ScanDecimal(out int dec))
                        {
                            return false;
                        }

                        max = min = dec;
                        if (startpos < _pos)
                        {
                            if (_pos < _pattern.Length && _pattern[_pos] == ',')
                            {
                                _pos++;
                                if (_pos == _pattern.Length || _pattern[_pos] == '}')
                                {
                                    max = int.MaxValue;
                                }
                                else
                                {
                                    if (!ScanDecimal(out int maxDec))
                                    {
                                        return false;
                                    }

                                    max = maxDec;
                                }
                            }
                        }

                        if (startpos == _pos || _pos == _pattern.Length || _pattern[_pos++] != '}')
                        {
                            _concatenation.AddChild(ref this, _unit);
                            _unit = JsonRegexNode.Null;
                            _pos = startpos - 1;
                            goto ContinueOuterScan;
                        }

                        break;

                    default:
                        Debug.Fail($"Unexpected char {ch}");
                        break;
                }

                if (!ScanBlank())
                {
                    return false;
                }

                bool lazy = false;
                if (_pos < _pattern.Length && _pattern[_pos] == '?')
                {
                    _pos++;
                    lazy = true;
                }

                if (min > max)
                {
                    return false;
                }

                _concatenation!.AddChild(ref this, _unit.MakeQuantifier(ref this, lazy, min, max));
                _unit = JsonRegexNode.Null;
            }

        ContinueOuterScan:
            ;
        }

    BreakOuterScan:
        ;

        if (!_stack.IsNull)
        {
            return false;
        }

        if (!AddGroup())
        {
            return false;
        }

        return true;
    }

    /// <summary>Start a new round for the validator state (in response to an open paren or string start)</summary>
    private void StartGroup(JsonRegexNode openGroup)
    {
        _group = openGroup;
        _alternation = CreateNode(JsonRegexNodeKind.Alternate);
        _concatenation = CreateNode(JsonRegexNodeKind.Concatenate);
    }

    private readonly bool TryGetCapNum(int capnameStart, int capnameEnd, out int capnum)
    {
        for (int i = 0; i < _capNames.Length; i++)
        {
            CapNameToCapNumberRow row = _capNames[i];
            if (row.Start == capnameStart && row.End == capnameEnd)
            {
                capnum = row.CapNum;
                return true;
            }

            if (_pattern.Slice(row.Start, row.End - row.Start).SequenceEqual(_pattern.Slice(capnameStart, capnameEnd - capnameStart)))
            {
                capnum = row.CapNum;
                return true;
            }
        }

        capnum = default;
        return false;
    }

    private readonly bool TryGetCapturePos(int capnum, bool requireExplicit, out int capturePos)
    {
        for (int i = 0; i < _caps.Length; i++)
        {
            CapToPos row = _caps[i];
            if (row.CapNum == capnum && (row.IsExplicit || !requireExplicit))
            {
                capturePos = row.Pos;
                return true;
            }
        }

        capturePos = default;
        return false;
    }

    /// <summary>Returns the node kind for zero-length assertions with a \ code.</summary>
    private readonly JsonRegexNodeKind TypeFromCode(char ch) =>
        ch switch
        {
            'b' => (_options & JsonRegexOptions.ECMAScript) != 0 ? JsonRegexNodeKind.ECMABoundary : JsonRegexNodeKind.Boundary,
            'B' => (_options & JsonRegexOptions.ECMAScript) != 0 ? JsonRegexNodeKind.NonECMABoundary : JsonRegexNodeKind.NonBoundary,
            'A' => JsonRegexNodeKind.Beginning,
            'G' => JsonRegexNodeKind.Start,
            'Z' => JsonRegexNodeKind.EndZ,
            'z' => JsonRegexNodeKind.End,
            _ => JsonRegexNodeKind.Nothing,
        };

    /// <summary>
    /// Represents a capture name to capture number mapping.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CapNameToCapNumberRow
    {
        private readonly int _startOffset;

        private readonly int _endOffset;

        private readonly int _capNum;

        /// <summary>
        /// The size of this structure in bytes.
        /// </summary>
        internal const int Size = 12;

        /// <summary>
        /// Gets the start offset of the capture name.
        /// </summary>
        public int Start => _startOffset;

        /// <summary>
        /// Gets the end offset of the capture name.
        /// </summary>
        public int End => _endOffset;

        /// <summary>
        /// Gets the capture number.
        /// </summary>
        public int CapNum => _capNum;

        /// <summary>
        /// Initializes a new instance of the <see cref="CapNameToCapNumberRow"/> struct.
        /// </summary>
        /// <param name="startOffset">The start offset of the capture name.</param>
        /// <param name="endOffset">The end offset of the capture name.</param>
        /// <param name="capNum">The capture number.</param>
        public CapNameToCapNumberRow(int startOffset, int endOffset, int capNum)
        {
            _startOffset = startOffset;
            _endOffset = endOffset;
            _capNum = capNum;
        }
    }

    /// <summary>
    /// Represents a capture number to position mapping.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CapToPos
    {
        private readonly int _capNum;

        private readonly int _pos;

        private readonly bool _isExplicit;

        /// <summary>
        /// The size of this structure in bytes.
        /// </summary>
        internal const int Size = 8;

        /// <summary>
        /// Gets the capture number.
        /// </summary>
        public int CapNum => _capNum;

        /// <summary>
        /// Gets the position.
        /// </summary>
        public int Pos => _pos;

        public bool IsExplicit => _isExplicit;

        /// <summary>
        /// Initializes a new instance of the <see cref="CapToPos"/> struct.
        /// </summary>
        /// <param name="capNum">The capture number.</param>
        /// <param name="pos">The position.</param>
        /// <param name="isExplicit">True if the capture is explicit.</param>
        public CapToPos(int capNum, int pos, bool isExplicit)
        {
            _capNum = capNum;
            _pos = pos;
        }
    }

    /// <summary>
    /// A 12 byte buffer is used to store the node details.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NodeDbRow
    {
        private readonly int _parentIdx;

        private readonly int _childCount;

        private readonly int _nodeKind;

        /// <summary>
        /// The size of this structure in bytes.
        /// </summary>
        internal const int Size = 12;

        /// <summary>
        /// Initializes a new instance of the <see cref="NodeDbRow"/> struct.
        /// </summary>
        /// <param name="parentIdx">The parent node index.</param>
        /// <param name="nodeKind">The kind of node.</param>
        /// <param name="childCount">The number of child nodes.</param>
        public NodeDbRow(int parentIdx, JsonRegexNodeKind nodeKind, int childCount)
        {
            _parentIdx = parentIdx;
            _childCount = childCount;
            _nodeKind = (int)nodeKind;
        }

        /// <summary>
        /// Gets the kind of this node.
        /// </summary>
        public readonly JsonRegexNodeKind NodeKind => (JsonRegexNodeKind)_nodeKind;

        /// <summary>
        /// Gets the parent node, or <see cref="JsonRegexNode.Null"/> if this is the root.
        /// </summary>
        public JsonRegexNode Parent => _parentIdx < 0 ? JsonRegexNode.Null : new(_parentIdx);

        /// <summary>
        /// Gets the number of child nodes.
        /// </summary>
        public int ChildCount => _childCount;

        /// <summary>
        /// Gets the index of the parent node.
        /// </summary>
        public int ParentIndex => _parentIdx;
    }
}