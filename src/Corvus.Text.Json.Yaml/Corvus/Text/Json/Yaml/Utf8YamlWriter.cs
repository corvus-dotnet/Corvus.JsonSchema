// <copyright file="Utf8YamlWriter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

#if STJ
using Corvus.Yaml.Internal;

namespace Corvus.Yaml;
#else
using Corvus.Text.Json.Yaml.Internal;

namespace Corvus.Text.Json.Yaml;
#endif

/// <summary>
/// A high-performance, low-allocation ref struct that writes YAML content
/// to an <see cref="IBufferWriter{T}"/> or <see cref="System.IO.Stream"/>.
/// </summary>
/// <remarks>
/// <para>
/// Supports both block and flow collection styles. Block style uses indentation;
/// flow style uses inline comma-separated syntax.
/// </para>
/// <para>
/// When <see cref="YamlWriterOptions.SkipValidation"/> is <see langword="false"/>,
/// the writer validates structural correctness (e.g., property names must precede values
/// in mappings, containers must be properly closed).
/// </para>
/// <para>
/// This is a ref struct and must not be copied. Always pass by <c>ref</c> and
/// dispose exactly once via a <see langword="using"/> declaration or explicit
/// call to <see cref="Dispose"/>.
/// </para>
/// </remarks>
public ref struct Utf8YamlWriter
{
    private const int MaxStackDepth = 16;

    private IBufferWriter<byte>? _output;
    private System.IO.Stream? _stream;
    private ArrayBufferWriter<byte>? _arrayBufferWriter;

    private ValueListBuilder<WriteContext> _contextStack;
    private int _indentLevel;
    private readonly int _indentSize;
    private readonly bool _skipValidation;
    private bool _wroteRootValue;
    private bool _hasWrittenAny;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="Utf8YamlWriter"/> struct
    /// with a specified <paramref name="bufferWriter"/>.
    /// </summary>
    /// <param name="bufferWriter">The destination for the UTF-8 YAML output.</param>
    /// <param name="options">Options for controlling the writer behavior.</param>
    public Utf8YamlWriter(IBufferWriter<byte> bufferWriter, YamlWriterOptions options = default)
    {
        _output = bufferWriter ?? throw new ArgumentNullException(nameof(bufferWriter));
        _stream = null;
        _arrayBufferWriter = null;
        _indentSize = options.IndentSize > 0 ? options.IndentSize : 2;
        _skipValidation = options.SkipValidation;
        _indentLevel = -1;
        _wroteRootValue = false;
        _hasWrittenAny = false;
        _contextStack = new ValueListBuilder<WriteContext>(MaxStackDepth);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Utf8YamlWriter"/> struct
    /// with a specified <paramref name="utf8Stream"/>.
    /// </summary>
    /// <param name="utf8Stream">The stream to write UTF-8 YAML output to.</param>
    /// <param name="options">Options for controlling the writer behavior.</param>
    public Utf8YamlWriter(System.IO.Stream utf8Stream, YamlWriterOptions options = default)
    {
        if (utf8Stream is null)
        {
            throw new ArgumentNullException(nameof(utf8Stream));
        }

        if (!utf8Stream.CanWrite)
        {
            throw new ArgumentException(SR.StreamNotWritable);
        }

        _stream = utf8Stream;
        _output = null;
        _arrayBufferWriter = new ArrayBufferWriter<byte>();
        _indentSize = options.IndentSize > 0 ? options.IndentSize : 2;
        _skipValidation = options.SkipValidation;
        _indentLevel = -1;
        _wroteRootValue = false;
        _hasWrittenAny = false;
        _contextStack = new ValueListBuilder<WriteContext>(MaxStackDepth);
    }

    /// <summary>
    /// Gets the current depth (number of open containers).
    /// </summary>
    public int CurrentDepth => _contextStack.Length;

    /// <summary>
    /// Writes the start of a YAML mapping.
    /// </summary>
    /// <param name="style">The collection style to use.</param>
    public void WriteStartMapping(YamlCollectionStyle style = YamlCollectionStyle.Block)
    {
        if (!_skipValidation)
        {
            ValidateStartContainer();
        }

        WriteContainerPreamble(style);

        _contextStack.Append(new WriteContext(style, WriteState.MappingKey, 0));

        if (style == YamlCollectionStyle.Flow)
        {
            WriteRaw("{"u8);
        }
    }

    /// <summary>
    /// Writes the end of a YAML mapping.
    /// </summary>
    public void WriteEndMapping()
    {
        if (!_skipValidation)
        {
            ValidateEndMapping();
        }

        WriteContext ctx = _contextStack.Pop();

        if (ctx.Style == YamlCollectionStyle.Flow)
        {
            WriteRaw("}"u8);
        }
        else if (ctx.ItemCount > 0)
        {
            _indentLevel--;
        }

        CompleteValue();
    }

    /// <summary>
    /// Writes the start of a YAML sequence.
    /// </summary>
    /// <param name="style">The collection style to use.</param>
    public void WriteStartSequence(YamlCollectionStyle style = YamlCollectionStyle.Block)
    {
        if (!_skipValidation)
        {
            ValidateStartContainer();
        }

        WriteContainerPreamble(style);

        _contextStack.Append(new WriteContext(style, WriteState.SequenceItem, 0));

        if (style == YamlCollectionStyle.Flow)
        {
            WriteRaw("["u8);
        }
    }

    /// <summary>
    /// Writes the end of a YAML sequence.
    /// </summary>
    public void WriteEndSequence()
    {
        if (!_skipValidation)
        {
            ValidateEndSequence();
        }

        WriteContext ctx = _contextStack.Pop();

        if (ctx.Style == YamlCollectionStyle.Flow)
        {
            WriteRaw("]"u8);
        }
        else if (ctx.ItemCount > 0)
        {
            _indentLevel--;
        }

        CompleteValue();
    }

    /// <summary>
    /// Writes a property name in a mapping.
    /// </summary>
    /// <param name="utf8Name">The UTF-8 encoded property name.</param>
    public void WritePropertyName(scoped ReadOnlySpan<byte> utf8Name)
    {
        if (!_skipValidation)
        {
            ValidatePropertyName();
        }

        ref WriteContext ctx = ref _contextStack[_contextStack.Length - 1];

        if (ctx.Style == YamlCollectionStyle.Flow)
        {
            if (ctx.ItemCount > 0)
            {
                WriteRaw(", "u8);
            }

            WriteScalarValue(utf8Name, isKey: true);
            WriteRaw(": "u8);
        }
        else
        {
            if (ctx.ItemCount == 0)
            {
                _indentLevel++;
            }

            WriteNewlineAndIndent();
            WriteScalarValue(utf8Name, isKey: true);
            WriteRaw(":"u8);
        }

        ctx.State = WriteState.MappingValue;
    }

    /// <summary>
    /// Writes a string value, quoting it if necessary to preserve round-trip safety.
    /// </summary>
    /// <param name="utf8Value">The UTF-8 encoded string value.</param>
    public void WriteStringValue(scoped ReadOnlySpan<byte> utf8Value)
    {
        WriteValuePreamble();
        WriteScalarValue(utf8Value, isKey: false);
        CompleteValue();
    }

    /// <summary>
    /// Writes a raw numeric value.
    /// </summary>
    /// <param name="rawUtf8Number">The UTF-8 encoded numeric literal (e.g. from JSON).</param>
    public void WriteNumberValue(scoped ReadOnlySpan<byte> rawUtf8Number)
    {
        WriteValuePreamble();
        WriteRaw(rawUtf8Number);
        CompleteValue();
    }

    /// <summary>
    /// Writes a boolean value (<c>true</c> or <c>false</c>).
    /// </summary>
    /// <param name="value">The boolean value.</param>
    public void WriteBooleanValue(bool value)
    {
        WriteValuePreamble();
        WriteRaw(value ? "true"u8 : "false"u8);
        CompleteValue();
    }

    /// <summary>
    /// Writes a null value.
    /// </summary>
    public void WriteNullValue()
    {
        WriteValuePreamble();
        WriteRaw("null"u8);
        CompleteValue();
    }

    /// <summary>
    /// Writes an empty mapping as <c>{}</c>.
    /// </summary>
    public void WriteEmptyMapping()
    {
        WriteValuePreamble();
        WriteRaw("{}"u8);
        CompleteValue();
    }

    /// <summary>
    /// Writes an empty sequence as <c>[]</c>.
    /// </summary>
    public void WriteEmptySequence()
    {
        WriteValuePreamble();
        WriteRaw("[]"u8);
        CompleteValue();
    }

    /// <summary>
    /// Commits the YAML text written so far to the output destination.
    /// </summary>
    public void Flush()
    {
        if (_stream != null)
        {
            Debug.Assert(_arrayBufferWriter != null);

#if NETSTANDARD2_0
            // netstandard2.0 has no Stream.Write(ReadOnlySpan<byte>)
            System.Runtime.InteropServices.MemoryMarshal.TryGetArray(
                _arrayBufferWriter!.WrittenMemory,
                out ArraySegment<byte> segment);
            _stream.Write(segment.Array!, segment.Offset, segment.Count);
#elif NET
            _stream.Write(_arrayBufferWriter.WrittenSpan);
#else
            _stream.Write(_arrayBufferWriter.WrittenSpan);
#endif

            _arrayBufferWriter.Clear();
            _stream.Flush();
        }
    }

    /// <summary>
    /// Releases resources used by this writer.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            Flush();
            _contextStack.Dispose();
        }
    }

    private void WriteValuePreamble()
    {
        if (!_skipValidation)
        {
            ValidateValue();
        }

        if (_contextStack.Length == 0)
        {
            // Root value
            return;
        }

        ref WriteContext ctx = ref _contextStack[_contextStack.Length - 1];

        if (ctx.Style == YamlCollectionStyle.Flow)
        {
            if (ctx.State == WriteState.SequenceItem)
            {
                if (ctx.ItemCount > 0)
                {
                    WriteRaw(", "u8);
                }
            }

            // For mapping values in flow, the ": " was already written by WritePropertyName
        }
        else
        {
            if (ctx.State == WriteState.MappingValue)
            {
                // Block mapping value: " value" on same line after the colon
                WriteRaw(" "u8);
            }
            else if (ctx.State == WriteState.SequenceItem)
            {
                if (ctx.ItemCount == 0)
                {
                    _indentLevel++;
                }

                WriteNewlineAndIndent();
                WriteRaw("- "u8);
            }
        }
    }

    private void WriteContainerPreamble(YamlCollectionStyle style)
    {
        if (_contextStack.Length == 0)
        {
            // Root container — nothing to write before it
            return;
        }

        ref WriteContext ctx = ref _contextStack[_contextStack.Length - 1];

        if (ctx.Style == YamlCollectionStyle.Flow)
        {
            if (ctx.State == WriteState.SequenceItem && ctx.ItemCount > 0)
            {
                WriteRaw(", "u8);
            }

            // For mapping values in flow, the ": " was already written
        }
        else
        {
            if (ctx.State == WriteState.MappingValue)
            {
                if (style == YamlCollectionStyle.Flow)
                {
                    // Flow container as mapping value → inline after colon
                    WriteRaw(" "u8);
                }

                // Block container as mapping value → newline+indent handled by first child
            }
            else if (ctx.State == WriteState.SequenceItem)
            {
                if (ctx.ItemCount == 0)
                {
                    _indentLevel++;
                }

                WriteNewlineAndIndent();
                WriteRaw("- "u8);

                if (style == YamlCollectionStyle.Block)
                {
                    // The nested block container's first child will write its own indent,
                    // but we need to adjust because "- " already provides 2 chars of indent.
                    // The child's indent level will be one higher, which is correct.
                }
            }
        }
    }

    private void CompleteValue()
    {
        if (_contextStack.Length == 0)
        {
            _wroteRootValue = true;
            return;
        }

        ref WriteContext ctx = ref _contextStack[_contextStack.Length - 1];

        if (ctx.State == WriteState.MappingValue)
        {
            ctx.State = WriteState.MappingKey;
            ctx.ItemCount++;
        }
        else if (ctx.State == WriteState.SequenceItem)
        {
            ctx.ItemCount++;
        }
    }

    private void WriteNewlineAndIndent()
    {
        if (_wroteRootValue || HasWrittenContent())
        {
            WriteRaw("\n"u8);
        }

        int spaces = _indentLevel * _indentSize;
        if (spaces > 0)
        {
            Span<byte> indent = stackalloc byte[spaces];
            indent.Fill(YamlConstants.Space);
            WriteRaw(indent);
        }
    }

    private bool HasWrittenContent()
    {
        return _hasWrittenAny;
    }

    private void WriteScalarValue(scoped ReadOnlySpan<byte> utf8Value, bool isKey)
    {
        if (NeedsQuoting(utf8Value, isKey))
        {
            WriteDoubleQuoted(utf8Value);
        }
        else
        {
            WriteRaw(utf8Value);
        }
    }

    private void WriteDoubleQuoted(scoped ReadOnlySpan<byte> utf8Value)
    {
        WriteRaw("\""u8);

        int start = 0;
        for (int i = 0; i < utf8Value.Length; i++)
        {
            byte b = utf8Value[i];
            ReadOnlySpan<byte> escape = GetEscapeSequence(b);
            if (escape.Length > 0)
            {
                if (i > start)
                {
                    WriteRaw(utf8Value.Slice(start, i - start));
                }

                WriteRaw(escape);
                start = i + 1;
            }
        }

        if (start < utf8Value.Length)
        {
            WriteRaw(utf8Value.Slice(start));
        }

        WriteRaw("\""u8);
    }

    private static ReadOnlySpan<byte> GetEscapeSequence(byte b)
    {
        return b switch
        {
            (byte)'"' => "\\\""u8,
            (byte)'\\' => "\\\\"u8,
            (byte)'\0' => "\\0"u8,
            (byte)'\x07' => "\\a"u8,
            (byte)'\b' => "\\b"u8,
            (byte)'\t' => "\\t"u8,
            (byte)'\n' => "\\n"u8,
            (byte)'\r' => "\\r"u8,
            _ => default,
        };
    }

    private static bool NeedsQuoting(ReadOnlySpan<byte> utf8Value, bool isKey)
    {
        if (utf8Value.Length == 0)
        {
            return true;
        }

        byte first = utf8Value[0];

        // Indicators that are problematic at the start of a plain scalar
        if (first is YamlConstants.Ampersand
            or YamlConstants.Asterisk
            or YamlConstants.Exclamation
            or YamlConstants.Pipe
            or YamlConstants.GreaterThan
            or YamlConstants.SingleQuote
            or YamlConstants.DoubleQuote
            or YamlConstants.Percent
            or YamlConstants.At
            or YamlConstants.Backtick
            or YamlConstants.OpenBrace
            or YamlConstants.CloseBrace
            or YamlConstants.OpenBracket
            or YamlConstants.CloseBracket
            or YamlConstants.Comma
            or YamlConstants.Hash
            or YamlConstants.Space
            or YamlConstants.Tab)
        {
            return true;
        }

        // "- ", "? ", ": " at start
        if (utf8Value.Length >= 2 && utf8Value[1] == YamlConstants.Space)
        {
            if (first is YamlConstants.Dash or YamlConstants.QuestionMark or YamlConstants.Colon)
            {
                return true;
            }
        }

        // Single "-", "?", ":" is also problematic
        if (utf8Value.Length == 1 && first is YamlConstants.Dash or YamlConstants.QuestionMark or YamlConstants.Colon)
        {
            return true;
        }

        // Leading/trailing whitespace
        if (utf8Value[utf8Value.Length - 1] is YamlConstants.Space or YamlConstants.Tab)
        {
            return true;
        }

        // Check for problematic content within the value
        for (int i = 0; i < utf8Value.Length; i++)
        {
            byte b = utf8Value[i];

            // Control characters always need quoting
            if (b < 0x20 && b != YamlConstants.Tab)
            {
                return true;
            }

            // ": " or ":#" in the middle
            if (b == YamlConstants.Colon && i + 1 < utf8Value.Length)
            {
                byte next = utf8Value[i + 1];
                if (next is YamlConstants.Space or YamlConstants.LineFeed or YamlConstants.CarriageReturn)
                {
                    return true;
                }
            }

            // " #" (space followed by hash) is a comment
            if (b == YamlConstants.Space && i + 1 < utf8Value.Length && utf8Value[i + 1] == YamlConstants.Hash)
            {
                return true;
            }

            // Newlines
            if (b is YamlConstants.LineFeed or YamlConstants.CarriageReturn)
            {
                return true;
            }
        }

        // Check if value would be resolved as a non-string type by Core schema.
        // This covers: null/Null/NULL/~, true/false/True/False/TRUE/FALSE,
        // integers (decimal/hex/octal), floats, .inf/.nan, and YAML 1.1 bools.
        return LooksLikeReservedScalar(utf8Value);
    }

    /// <summary>
    /// Checks if a plain scalar would be resolved as a non-string type
    /// by the Core schema or common YAML 1.1 patterns.
    /// </summary>
    private static bool LooksLikeReservedScalar(ReadOnlySpan<byte> value)
    {
        // null variants
        if (value.Length <= 4)
        {
            if (value.SequenceEqual("null"u8) ||
                value.SequenceEqual("Null"u8) ||
                value.SequenceEqual("NULL"u8) ||
                value.SequenceEqual("~"u8))
            {
                return true;
            }
        }

        // bool variants (Core + YAML 1.1)
        if (value.Length <= 5)
        {
            if (value.SequenceEqual("true"u8) ||
                value.SequenceEqual("True"u8) ||
                value.SequenceEqual("TRUE"u8) ||
                value.SequenceEqual("false"u8) ||
                value.SequenceEqual("False"u8) ||
                value.SequenceEqual("FALSE"u8) ||
                value.SequenceEqual("yes"u8) ||
                value.SequenceEqual("Yes"u8) ||
                value.SequenceEqual("YES"u8) ||
                value.SequenceEqual("no"u8) ||
                value.SequenceEqual("No"u8) ||
                value.SequenceEqual("NO"u8) ||
                value.SequenceEqual("on"u8) ||
                value.SequenceEqual("On"u8) ||
                value.SequenceEqual("ON"u8) ||
                value.SequenceEqual("off"u8) ||
                value.SequenceEqual("Off"u8) ||
                value.SequenceEqual("OFF"u8) ||
                value.SequenceEqual("y"u8) ||
                value.SequenceEqual("Y"u8) ||
                value.SequenceEqual("n"u8) ||
                value.SequenceEqual("N"u8))
            {
                return true;
            }
        }

        // Numeric patterns: starts with digit, +, -, or .
        byte first = value[0];
        if (first is (>= (byte)'0' and <= (byte)'9') or (byte)'+' or (byte)'-' or (byte)'.')
        {
            // Could be integer, float, .inf, .nan, or octal/hex
            // Just quote conservatively if it starts with these
            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteRaw(scoped ReadOnlySpan<byte> utf8Data)
    {
        _hasWrittenAny = true;

        if (_stream != null)
        {
            Debug.Assert(_arrayBufferWriter != null);
            Span<byte> dest = _arrayBufferWriter!.GetSpan(utf8Data.Length);
            utf8Data.CopyTo(dest);
            _arrayBufferWriter.Advance(utf8Data.Length);
        }
        else
        {
            Debug.Assert(_output != null);
            Span<byte> dest = _output!.GetSpan(utf8Data.Length);
            utf8Data.CopyTo(dest);
            _output.Advance(utf8Data.Length);
        }
    }

    // ========================
    // Validation
    // ========================
    private void ValidateStartContainer()
    {
        if (_contextStack.Length == 0)
        {
            if (_wroteRootValue)
            {
                ThrowMultipleRootValues();
            }

            return;
        }

        ref WriteContext ctx = ref _contextStack[_contextStack.Length - 1];

        if (ctx.State == WriteState.MappingKey)
        {
            ThrowInvalidOperation(SR.WriterContainerAsMappingKey);
        }
    }

    private void ValidateEndMapping()
    {
        if (_contextStack.Length == 0)
        {
            ThrowInvalidOperation(SR.WriterEndMappingNoOpen);
        }

        ref WriteContext ctx = ref _contextStack[_contextStack.Length - 1];

        if (ctx.State == WriteState.MappingValue)
        {
            ThrowInvalidOperation(SR.WriterEndMappingValueExpected);
        }

        if (ctx.State == WriteState.SequenceItem)
        {
            ThrowInvalidOperation(SR.WriterEndMappingInSequence);
        }
    }

    private void ValidateEndSequence()
    {
        if (_contextStack.Length == 0)
        {
            ThrowInvalidOperation(SR.WriterEndSequenceNoOpen);
        }

        ref WriteContext ctx = ref _contextStack[_contextStack.Length - 1];

        if (ctx.State != WriteState.SequenceItem)
        {
            ThrowInvalidOperation(SR.WriterEndSequenceInMapping);
        }
    }

    private void ValidatePropertyName()
    {
        if (_contextStack.Length == 0)
        {
            ThrowInvalidOperation(SR.WriterPropertyNameOutsideMapping);
        }

        ref WriteContext ctx = ref _contextStack[_contextStack.Length - 1];

        if (ctx.State != WriteState.MappingKey)
        {
            ThrowInvalidOperation(SR.WriterPropertyNameValueExpected);
        }
    }

    private void ValidateValue()
    {
        if (_contextStack.Length == 0)
        {
            if (_wroteRootValue)
            {
                ThrowMultipleRootValues();
            }

            return;
        }

        ref WriteContext ctx = ref _contextStack[_contextStack.Length - 1];

        if (ctx.State == WriteState.MappingKey)
        {
            ThrowInvalidOperation(SR.WriterValuePropertyNameExpected);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowMultipleRootValues()
    {
        throw new InvalidOperationException(SR.WriterMultipleRootValues);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidOperation(string message)
    {
        throw new InvalidOperationException(message);
    }

    // ========================
    // Context tracking
    // ========================
    private enum WriteState : byte
    {
        MappingKey,
        MappingValue,
        SequenceItem,
    }

    private struct WriteContext
    {
        public YamlCollectionStyle Style;
        public WriteState State;
        public int ItemCount;

        public WriteContext(YamlCollectionStyle style, WriteState state, int itemCount)
        {
            Style = style;
            State = state;
            ItemCount = itemCount;
        }
    }
}