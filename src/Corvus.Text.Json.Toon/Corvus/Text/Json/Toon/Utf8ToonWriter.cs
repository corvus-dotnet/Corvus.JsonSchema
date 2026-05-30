using System.Buffers;
using System.Runtime.CompilerServices;
using Corvus.Text;

#if STJ && TOON
using Corvus.Toon.Internal;

namespace Corvus.Toon;
#else
using Corvus.Text.Json.Toon.Internal;

namespace Corvus.Text.Json.Toon;
#endif

/// <summary>
/// Writes UTF-8 TOON content to an <see cref="IBufferWriter{T}"/> or stream.
/// </summary>
public ref struct Utf8ToonWriter
{
    private IBufferWriter<byte>? output;
    private System.IO.Stream? stream;
    private readonly ToonWriterOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="Utf8ToonWriter"/> struct.
    /// </summary>
    /// <param name="output">The output buffer writer.</param>
    /// <param name="options">The writer options.</param>
    public Utf8ToonWriter(IBufferWriter<byte> output, ToonWriterOptions options = default)
    {
        this.output = output ?? throw new ArgumentNullException(nameof(output));
        this.stream = null;
        this.options = options;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Utf8ToonWriter"/> struct.
    /// </summary>
    /// <param name="stream">The writable UTF-8 stream.</param>
    /// <param name="options">The writer options.</param>
    public Utf8ToonWriter(System.IO.Stream stream, ToonWriterOptions options = default)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (!stream.CanWrite)
        {
            ThrowHelper.ThrowArgumentException_StreamNotWritable(nameof(stream));
        }

        this.output = null;
        this.stream = stream;
        this.options = options;
    }

    internal ToonWriterOptions Options => this.options;

    /// <summary>
    /// Writes the start of a TOON object.
    /// </summary>
    /// <remarks>
    /// TOON objects have no explicit opening marker; call this to make object-writing code symmetric with
    /// <see cref="WriteObjectEnd"/>.
    /// </remarks>
    public void WriteObjectStart()
    {
    }

    /// <summary>
    /// Writes the end of a TOON object.
    /// </summary>
    /// <remarks>
    /// TOON objects have no explicit closing marker; call this to make object-writing code symmetric with
    /// <see cref="WriteObjectStart"/>.
    /// </remarks>
    public void WriteObjectEnd()
    {
    }

    /// <summary>
    /// Writes a TOON array header for a root array.
    /// </summary>
    /// <param name="count">The declared item count.</param>
    /// <param name="depth">The indentation depth.</param>
    public void WriteArrayHeader(int count, int depth = 0)
    {
        this.WriteIndent(depth);
        this.WriteArrayHeaderCore(count);
        this.WriteByte((byte)':');
    }

    /// <summary>
    /// Writes a TOON array header for a named array property.
    /// </summary>
    /// <param name="key">The UTF-8 encoded property key.</param>
    /// <param name="count">The declared item count.</param>
    /// <param name="depth">The indentation depth.</param>
    public void WriteArrayHeader(scoped ReadOnlySpan<byte> key, int count, int depth = 0)
    {
        this.WriteIndent(depth);
        this.WriteKey(key);
        this.WriteArrayHeaderCore(count);
        this.WriteByte((byte)':');
    }

    /// <summary>
    /// Writes a TOON tabular array header for a root array.
    /// </summary>
    /// <param name="count">The declared row count.</param>
    /// <param name="encodedFields">The already-encoded and delimiter-separated UTF-8 field list.</param>
    /// <param name="depth">The indentation depth.</param>
    public void WriteTabularHeader(int count, scoped ReadOnlySpan<byte> encodedFields, int depth = 0)
    {
        this.WriteIndent(depth);
        this.WriteArrayHeaderCore(count);
        this.WriteByte((byte)'{');
        this.WriteRaw(encodedFields);
        this.WriteRaw("}:"u8);
    }

    /// <summary>
    /// Writes a TOON tabular array header for a named array property.
    /// </summary>
    /// <param name="key">The UTF-8 encoded property key.</param>
    /// <param name="count">The declared row count.</param>
    /// <param name="encodedFields">The already-encoded and delimiter-separated UTF-8 field list.</param>
    /// <param name="depth">The indentation depth.</param>
    public void WriteTabularHeader(scoped ReadOnlySpan<byte> key, int count, scoped ReadOnlySpan<byte> encodedFields, int depth = 0)
    {
        this.WriteIndent(depth);
        this.WriteKey(key);
        this.WriteArrayHeaderCore(count);
        this.WriteByte((byte)'{');
        this.WriteRaw(encodedFields);
        this.WriteRaw("}:"u8);
    }

    /// <summary>
    /// Writes an already-encoded TOON tabular row.
    /// </summary>
    /// <param name="encodedRow">The already-encoded and delimiter-separated UTF-8 row values.</param>
    /// <param name="depth">The indentation depth.</param>
    public void WriteRow(scoped ReadOnlySpan<byte> encodedRow, int depth = 1)
    {
        this.WriteIndent(depth);
        this.WriteRaw(encodedRow);
    }

    /// <summary>
    /// Writes a TOON key-value pair whose value is a string scalar.
    /// </summary>
    /// <param name="key">The UTF-8 encoded property key.</param>
    /// <param name="value">The UTF-8 encoded string value.</param>
    /// <param name="depth">The indentation depth.</param>
    public void WriteKeyValue(scoped ReadOnlySpan<byte> key, scoped ReadOnlySpan<byte> value, int depth = 0)
    {
        this.WriteIndent(depth);
        this.WriteKey(key);
        this.WriteRaw(": "u8);
        this.WriteStringValue(value);
    }

    /// <summary>
    /// Writes a TOON list item whose value is a string scalar.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value.</param>
    /// <param name="depth">The indentation depth.</param>
    public void WriteListItem(scoped ReadOnlySpan<byte> value, int depth = 0)
    {
        this.WriteIndent(depth);
        this.WriteRaw("- "u8);
        this.WriteStringValue(value);
    }

    /// <summary>
    /// Flushes buffered TOON content to the underlying stream, if this writer was created for a stream.
    /// </summary>
    public void Flush()
    {
    }

    /// <summary>
    /// Releases resources used by the writer.
    /// </summary>
    public void Dispose()
    {
        this.Flush();
        this.output = null;
        this.stream = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteIndent(int depth)
    {
        int count = Math.Max(0, depth) * this.options.EffectiveIndentSize;
        if (count == 0)
        {
            return;
        }

        ReadOnlySpan<byte> spaces = "                "u8;
        while (count > 0)
        {
            int chunk = Math.Min(count, spaces.Length);
            this.WriteRaw(spaces.Slice(0, chunk));
            count -= chunk;
        }
    }

    /// <summary>
    /// Writes a line feed.
    /// </summary>
    public void WriteNewLine()
    {
        this.WriteByte((byte)'\n');
    }

    /// <summary>
    /// Writes a TOON key, quoting it when required by the TOON key grammar.
    /// </summary>
    /// <param name="key">The UTF-8 encoded key.</param>
    public void WriteKey(scoped ReadOnlySpan<byte> key)
    {
        if (IsBareKey(key))
        {
            this.WriteRaw(key);
        }
        else
        {
            this.WriteQuoted(key);
        }
    }

    /// <summary>
    /// Writes a TOON string value, quoting it when required to preserve JSON string semantics.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteStringValue(scoped ReadOnlySpan<byte> value)
    {
        if (IsBareString(value, this.options.Delimiter))
        {
            this.WriteRaw(value);
        }
        else
        {
            this.WriteQuoted(value);
        }
    }

    /// <summary>
    /// Writes a TOON numeric value.
    /// </summary>
    /// <param name="value">The UTF-8 encoded numeric value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteNumberValue(scoped ReadOnlySpan<byte> value)
    {
        this.WriteTrustedJsonNumberValue(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteTrustedJsonNumberValue(scoped ReadOnlySpan<byte> value)
    {
        if (IsCanonicalInteger(value))
        {
            this.WriteRaw(value);
            return;
        }

        this.WriteCanonicalNumberSlow(value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void WriteCanonicalNumberSlow(scoped ReadOnlySpan<byte> value)
    {
        Span<byte> initialBuffer = stackalloc byte[JsonConstants.StackallocByteThreshold];
        Utf8ValueStringBuilder builder = new(initialBuffer);
        try
        {
            AppendCanonicalNumberSlow(ref builder, value);
            this.WriteRaw(builder.AsSpan());
        }
        finally
        {
            builder.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteRaw(scoped ReadOnlySpan<byte> value)
    {
        if (this.output is IBufferWriter<byte> writer)
        {
            Span<byte> destination = writer.GetSpan(value.Length);
            value.CopyTo(destination);
            writer.Advance(value.Length);
            return;
        }

        if (this.stream is System.IO.Stream destinationStream)
        {
#if NET
            destinationStream.Write(value);
#else
            byte[] rented = ArrayPool<byte>.Shared.Rent(value.Length);
            try
            {
                value.CopyTo(rented);
                destinationStream.Write(rented, 0, value.Length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
#endif
            return;
        }

        throw new ObjectDisposedException(nameof(Utf8ToonWriter));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteByte(byte value)
    {
        if (this.output is IBufferWriter<byte> writer)
        {
            Span<byte> destination = writer.GetSpan(1);
            destination[0] = value;
            writer.Advance(1);
            return;
        }

        if (this.stream is System.IO.Stream destinationStream)
        {
            destinationStream.WriteByte(value);
            return;
        }

        throw new ObjectDisposedException(nameof(Utf8ToonWriter));
    }

    private void WriteArrayHeaderCore(int count)
    {
        Span<byte> buffer = stackalloc byte[11];
        _ = System.Buffers.Text.Utf8Formatter.TryFormat(count, buffer, out int written);

        byte delimiter = this.options.Delimiter switch
        {
            ToonDelimiter.Tab => (byte)'\t',
            ToonDelimiter.Pipe => (byte)'|',
            _ => (byte)0,
        };

        this.WriteByte((byte)'[');
        this.WriteRaw(buffer.Slice(0, written));
        if (delimiter != 0)
        {
            this.WriteByte(delimiter);
        }

        this.WriteByte((byte)']');
    }

    private void WriteQuoted(scoped ReadOnlySpan<byte> value)
    {
        this.WriteByte((byte)'"');
        foreach (byte b in value)
        {
            switch (b)
            {
                case (byte)'"':
                    this.WriteRaw("\\\""u8);
                    break;

                case (byte)'\\':
                    this.WriteRaw("\\\\"u8);
                    break;

                case (byte)'\n':
                    this.WriteRaw("\\n"u8);
                    break;

                case (byte)'\r':
                    this.WriteRaw("\\r"u8);
                    break;

                case (byte)'\t':
                    this.WriteRaw("\\t"u8);
                    break;

                default:
                    if (b < 0x20)
                    {
                        this.WriteRaw("\\u00"u8);
                        this.WriteByte(ToHex((byte)(b >> 4)));
                        this.WriteByte(ToHex((byte)(b & 0x0F)));
                    }
                    else
                    {
                        this.WriteByte(b);
                    }

                    break;
            }
        }

        this.WriteByte((byte)'"');
    }

    private static bool IsBareKey(ReadOnlySpan<byte> value)
    {
        if (value.Length == 0 || !IsIdentifierStart(value[0]))
        {
            return false;
        }

        for (int i = 1; i < value.Length; i++)
        {
            if (!IsIdentifierPart(value[i]) && value[i] != (byte)'.')
            {
                return false;
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsBareString(ReadOnlySpan<byte> value, ToonDelimiter delimiter)
    {
        if (value.Length == 0 ||
            value[0] == (byte)' ' ||
            value[value.Length - 1] == (byte)' ' ||
            value[0] == (byte)'-' ||
            value.SequenceEqual("true"u8) ||
            value.SequenceEqual("false"u8) ||
            value.SequenceEqual("null"u8))
        {
            return false;
        }

        byte delimiterByte = delimiter switch
        {
            ToonDelimiter.Tab => (byte)'\t',
            ToonDelimiter.Pipe => (byte)'|',
            _ => (byte)',',
        };

        bool couldBeJsonNumber = IsDigit(value[0]);
        JsonNumberScanState numberState = couldBeJsonNumber ? JsonNumberScanState.Integer : JsonNumberScanState.Invalid;
        for (int i = 0; i < value.Length; i++)
        {
            byte b = value[i];
            if (b < 0x20 ||
                b == delimiterByte ||
                b is (byte)':' or (byte)'[' or (byte)']' or (byte)'{' or (byte)'}' or (byte)'"' or (byte)'\\')
            {
                return false;
            }

            if (couldBeJsonNumber)
            {
                numberState = AdvanceJsonNumberScan(numberState, b);
                couldBeJsonNumber = numberState != JsonNumberScanState.Invalid;
            }
        }

        return !IsCompleteJsonNumberScan(numberState);
    }

    internal static void AppendCanonicalNumber(scoped ref Utf8ValueStringBuilder destination, scoped ReadOnlySpan<byte> value)
    {
        AppendTrustedJsonNumber(ref destination, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void AppendTrustedJsonNumber(scoped ref Utf8ValueStringBuilder destination, scoped ReadOnlySpan<byte> value)
    {
        if (IsCanonicalInteger(value))
        {
            destination.Append(value);
            return;
        }

        AppendCanonicalNumberSlow(ref destination, value);
    }

    private static void AppendCanonicalNumberSlow(scoped ref Utf8ValueStringBuilder destination, scoped ReadOnlySpan<byte> value)
    {
        if (value.Length == 0)
        {
            return;
        }

        int index = 0;
        bool negative = value[index] == (byte)'-';
        if (negative)
        {
            index++;
        }

        int intStart = index;
        while (index < value.Length && IsDigit(value[index]))
        {
            index++;
        }

        int intLength = index - intStart;
        if (intLength == 0)
        {
            destination.Append(value);
            return;
        }

        int fractionStart = index;
        int fractionLength = 0;
        if (index < value.Length && value[index] == (byte)'.')
        {
            index++;
            fractionStart = index;
            while (index < value.Length && IsDigit(value[index]))
            {
                index++;
            }

            fractionLength = index - fractionStart;
        }

        int exponent = 0;
        if (index < value.Length && (value[index] == (byte)'e' || value[index] == (byte)'E'))
        {
            index++;
            bool negativeExponent = false;
            if (index < value.Length && (value[index] == (byte)'+' || value[index] == (byte)'-'))
            {
                negativeExponent = value[index] == (byte)'-';
                index++;
            }

            int exponentStart = index;
            while (index < value.Length && IsDigit(value[index]))
            {
                if (exponent < 1_000_000)
                {
                    exponent = (exponent * 10) + (value[index] - (byte)'0');
                }

                index++;
            }

            if (index == exponentStart)
            {
                destination.Append(value);
                return;
            }

            if (negativeExponent)
            {
                exponent = -exponent;
            }
        }

        if (index != value.Length)
        {
            destination.Append(value);
            return;
        }

        int totalDigits = intLength + fractionLength;
        int firstSignificantDigit = 0;
        while (firstSignificantDigit < totalDigits &&
            GetNumberDigit(value, intStart, intLength, fractionStart, firstSignificantDigit) == (byte)'0')
        {
            firstSignificantDigit++;
        }

        if (firstSignificantDigit == totalDigits)
        {
            destination.Append((byte)'0');
            return;
        }

        int lastSignificantDigit = totalDigits - 1;
        while (lastSignificantDigit > firstSignificantDigit &&
            GetNumberDigit(value, intStart, intLength, fractionStart, lastSignificantDigit) == (byte)'0')
        {
            lastSignificantDigit--;
        }

        int significantLength = lastSignificantDigit - firstSignificantDigit + 1;
        int point = intLength + exponent - firstSignificantDigit;
        int zeroPadding = point <= 0 ? -point : Math.Max(0, point - significantLength);
        if (zeroPadding > 1_000_000)
        {
            destination.Append(value);
            return;
        }

        if (negative)
        {
            destination.Append((byte)'-');
        }

        if (point <= 0)
        {
            destination.Append("0."u8);
            destination.Append((byte)'0', -point);
            AppendNumberDigits(ref destination, value, intStart, intLength, fractionStart, firstSignificantDigit, lastSignificantDigit + 1);
            return;
        }

        if (point >= significantLength)
        {
            AppendNumberDigits(ref destination, value, intStart, intLength, fractionStart, firstSignificantDigit, lastSignificantDigit + 1);
            destination.Append((byte)'0', point - significantLength);
            return;
        }

        int pointIndex = firstSignificantDigit + point;
        AppendNumberDigits(ref destination, value, intStart, intLength, fractionStart, firstSignificantDigit, pointIndex);
        destination.Append((byte)'.');
        AppendNumberDigits(ref destination, value, intStart, intLength, fractionStart, pointIndex, lastSignificantDigit + 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsCanonicalInteger(scoped ReadOnlySpan<byte> value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        int index = 0;
        if (value[0] == (byte)'-')
        {
            if (value.Length == 1 || value[1] == (byte)'0')
            {
                return false;
            }

            index = 1;
        }

        if (value[index] == (byte)'0')
        {
            return index == value.Length - 1;
        }

        if (value[index] < (byte)'1' || value[index] > (byte)'9')
        {
            return false;
        }

        for (int i = index + 1; i < value.Length; i++)
        {
            if (!IsDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static JsonNumberScanState AdvanceJsonNumberScan(JsonNumberScanState state, byte value)
    {
        return state switch
        {
            JsonNumberScanState.Integer => IsDigit(value)
                ? JsonNumberScanState.Integer
                : value == (byte)'.'
                    ? JsonNumberScanState.FractionStart
                    : value is (byte)'e' or (byte)'E'
                        ? JsonNumberScanState.ExponentStart
                        : JsonNumberScanState.Invalid,

            JsonNumberScanState.FractionStart => IsDigit(value) ? JsonNumberScanState.Fraction : JsonNumberScanState.Invalid,

            JsonNumberScanState.Fraction => IsDigit(value)
                ? JsonNumberScanState.Fraction
                : value is (byte)'e' or (byte)'E'
                    ? JsonNumberScanState.ExponentStart
                    : JsonNumberScanState.Invalid,

            JsonNumberScanState.ExponentStart => value is (byte)'+' or (byte)'-'
                ? JsonNumberScanState.ExponentSign
                : IsDigit(value)
                    ? JsonNumberScanState.Exponent
                    : JsonNumberScanState.Invalid,

            JsonNumberScanState.ExponentSign => IsDigit(value) ? JsonNumberScanState.Exponent : JsonNumberScanState.Invalid,

            JsonNumberScanState.Exponent => IsDigit(value) ? JsonNumberScanState.Exponent : JsonNumberScanState.Invalid,

            _ => JsonNumberScanState.Invalid,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsCompleteJsonNumberScan(JsonNumberScanState state)
    {
        return state is JsonNumberScanState.Integer or JsonNumberScanState.Fraction or JsonNumberScanState.Exponent;
    }

    private static void AppendNumberDigits(scoped ref Utf8ValueStringBuilder destination, scoped ReadOnlySpan<byte> value, int intStart, int intLength, int fractionStart, int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            destination.Append(GetNumberDigit(value, intStart, intLength, fractionStart, i));
        }
    }

    private static byte GetNumberDigit(scoped ReadOnlySpan<byte> value, int intStart, int intLength, int fractionStart, int digitIndex)
    {
        return digitIndex < intLength
            ? value[intStart + digitIndex]
            : value[fractionStart + digitIndex - intLength];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDigit(byte value)
    {
        return value >= (byte)'0' && value <= (byte)'9';
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsIdentifierStart(byte value)
    {
        return (value >= (byte)'A' && value <= (byte)'Z') || (value >= (byte)'a' && value <= (byte)'z') || value == (byte)'_';
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsIdentifierPart(byte value)
    {
        return IsIdentifierStart(value) || (value >= (byte)'0' && value <= (byte)'9');
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ToHex(byte value)
    {
        return (byte)(value < 10 ? value + (byte)'0' : value - 10 + (byte)'a');
    }

    private enum JsonNumberScanState
    {
        Invalid,
        Integer,
        FractionStart,
        Fraction,
        ExponentStart,
        ExponentSign,
        Exponent,
    }
}