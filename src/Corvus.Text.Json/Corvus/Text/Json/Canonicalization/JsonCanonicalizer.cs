// <copyright file="JsonCanonicalizer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Generic;

namespace Corvus.Text.Json.Canonicalization;

/// <summary>
/// Produces canonical JSON output per RFC 8785 (JSON Canonicalization Scheme — JCS).
/// </summary>
/// <remarks>
/// <para>
/// JCS defines a deterministic serialization of JSON values:
/// </para>
/// <list type="bullet">
/// <item><description>Object properties are sorted by UTF-16 code unit values of the property names.</description></item>
/// <item><description>Numbers use ECMAScript 6 <c>Number.toString()</c> formatting.</description></item>
/// <item><description>Strings use minimal escaping (only control characters, backslash, and double-quote).</description></item>
/// <item><description>No whitespace between structural tokens.</description></item>
/// </list>
/// <para>
/// Input must conform to I-JSON (RFC 7493): no duplicate property names, and all numbers must be
/// representable as IEEE 754 double-precision values.
/// </para>
/// </remarks>
public static class JsonCanonicalizer
{
    private const int MaxDepth = 64;

    /// <summary>
    /// Canonicalizes a JSON element per RFC 8785 and writes the result to a byte span.
    /// </summary>
    /// <param name="element">The element to canonicalize.</param>
    /// <param name="destination">The buffer to write the canonical bytes to.</param>
    /// <param name="bytesWritten">The number of bytes written.</param>
    /// <returns><see langword="true"/> if the canonical output fits in the destination;
    /// <see langword="false"/> if the buffer is too small.</returns>
    /// <exception cref="InvalidOperationException">The input contains duplicate property names.</exception>
    public static bool TryCanonicalize(in JsonElement element, Span<byte> destination, out int bytesWritten)
    {
        CanonicalWriter writer = new(destination);
        writer.WriteElement(element, 0);
        bytesWritten = writer.BytesWritten;
        return !writer.Overflow;
    }

    /// <summary>
    /// Canonicalizes a JSON element per RFC 8785 and returns the result as a byte array.
    /// </summary>
    /// <param name="element">The element to canonicalize.</param>
    /// <returns>The canonical JSON bytes.</returns>
    /// <exception cref="InvalidOperationException">The input contains duplicate property names.</exception>
    public static byte[] Canonicalize(in JsonElement element)
    {
        // Try stackalloc first for small documents
        Span<byte> stackBuffer = stackalloc byte[JsonConstants.StackallocByteThreshold];
        if (TryCanonicalize(element, stackBuffer, out int bytesWritten))
        {
            return stackBuffer.Slice(0, bytesWritten).ToArray();
        }

        // Grow via ArrayPool for larger documents
        int bufferSize = JsonConstants.StackallocByteThreshold * 2;
        while (true)
        {
            byte[] rented = ArrayPool<byte>.Shared.Rent(bufferSize);

            try
            {
                if (TryCanonicalize(element, rented, out bytesWritten))
                {
                    return rented.AsSpan(0, bytesWritten).ToArray();
                }

                bufferSize = checked(bufferSize * 2);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>
    /// Writes canonical JSON to a span, tracking position and overflow.
    /// </summary>
    private ref struct CanonicalWriter
    {
        private readonly Span<byte> destination;
        private int position;
        private bool overflow;

        public CanonicalWriter(Span<byte> destination)
        {
            this.destination = destination;
            this.position = 0;
            this.overflow = false;
        }

        /// <summary>
        /// Gets the number of bytes written so far.
        /// </summary>
        public readonly int BytesWritten => this.position;

        /// <summary>
        /// Gets a value indicating whether the writer has overflowed.
        /// </summary>
        public readonly bool Overflow => this.overflow;

        /// <summary>
        /// Writes a JSON element in canonical form.
        /// </summary>
        public void WriteElement(in JsonElement element, int depth)
        {
            if (this.overflow)
            {
                return;
            }

            if (depth > MaxDepth)
            {
                throw new InvalidOperationException("JSON depth exceeds the maximum supported depth for canonicalization.");
            }

            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    this.WriteObject(element, depth);
                    break;
                case JsonValueKind.Array:
                    this.WriteArray(element, depth);
                    break;
                case JsonValueKind.String:
                    this.WriteStringValue(element);
                    break;
                case JsonValueKind.Number:
                    this.WriteNumber(element);
                    break;
                case JsonValueKind.True:
                    this.WriteLiteral("true"u8);
                    break;
                case JsonValueKind.False:
                    this.WriteLiteral("false"u8);
                    break;
                case JsonValueKind.Null:
                    this.WriteLiteral("null"u8);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported JSON value kind.");
            }
        }

        private void WriteObject(in JsonElement element, int depth)
        {
            this.WriteByte((byte)'{');

            // First pass: count properties
            int count = 0;
            ObjectEnumerator<JsonElement> counter = element.EnumerateObject();
            while (counter.MoveNext())
            {
                count++;
            }

            if (count == 0)
            {
                this.WriteByte((byte)'}');
                return;
            }

            // Rent arrays for properties and their sort keys
            JsonProperty<JsonElement>[] properties = ArrayPool<JsonProperty<JsonElement>>.Shared.Rent(count);
            string[] names = ArrayPool<string>.Shared.Rent(count);
            int[] indices = ArrayPool<int>.Shared.Rent(count);

            try
            {
                int i = 0;
                foreach (JsonProperty<JsonElement> prop in element.EnumerateObject())
                {
                    properties[i] = prop;
                    names[i] = prop.Name;
                    indices[i] = i;
                    i++;
                }

                // Sort indices by property name using UTF-16 ordinal comparison
                Array.Sort(indices, 0, count, Utf16OrdinalIndexComparer.Create(names));

                // Check for duplicate property names (I-JSON requirement)
                for (int j = 1; j < count; j++)
                {
                    if (string.Equals(names[indices[j]], names[indices[j - 1]], StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Duplicate property name detected. JCS requires I-JSON compliant input (RFC 7493).");
                    }
                }

                // Emit properties in sorted order
                for (int j = 0; j < count; j++)
                {
                    if (j > 0)
                    {
                        this.WriteByte((byte)',');
                    }

                    int idx = indices[j];
                    this.WriteCanonicalString(names[idx]);
                    this.WriteByte((byte)':');
                    this.WriteElement(properties[idx].Value, depth + 1);
                }
            }
            finally
            {
                ArrayPool<JsonProperty<JsonElement>>.Shared.Return(properties, clearArray: true);
                ArrayPool<string>.Shared.Return(names, clearArray: true);
                ArrayPool<int>.Shared.Return(indices);
            }

            this.WriteByte((byte)'}');
        }

        private void WriteArray(in JsonElement element, int depth)
        {
            this.WriteByte((byte)'[');
            bool first = true;

            foreach (JsonElement item in element.EnumerateArray())
            {
                if (!first)
                {
                    this.WriteByte((byte)',');
                }

                first = false;
                this.WriteElement(item, depth + 1);
            }

            this.WriteByte((byte)']');
        }

        private void WriteStringValue(in JsonElement element)
        {
            // Get the unescaped string value and re-escape per JCS rules
            string? value = element.GetString();
            this.WriteCanonicalString(value ?? string.Empty);
        }

        private void WriteNumber(in JsonElement element)
        {
            if (!element.TryGetDouble(out double value))
            {
                throw new InvalidOperationException("Number cannot be represented as IEEE 754 double.");
            }

            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new InvalidOperationException("NaN and Infinity are not valid in JSON canonicalization.");
            }

            // Write ES6-formatted number directly to destination
            if (this.overflow)
            {
                return;
            }

            Span<byte> remaining = this.destination.Slice(this.position);
            if (Es6NumberFormatter.TryFormat(value, remaining, out int numberBytesWritten))
            {
                this.position += numberBytesWritten;
            }
            else
            {
                this.overflow = true;
            }
        }

        /// <summary>
        /// Writes a string with JCS canonical escaping.
        /// </summary>
        /// <remarks>
        /// RFC 8785 §3.2.2.2 escaping rules:
        /// - <c>\"</c> and <c>\\</c> are always escaped.
        /// - Named escapes for <c>\b</c>, <c>\t</c>, <c>\n</c>, <c>\f</c>, <c>\r</c>.
        /// - <c>\uXXXX</c> (lowercase hex) for remaining control characters (U+0000–U+001F).
        /// - All other characters are written literally as UTF-8.
        /// </remarks>
        private void WriteCanonicalString(string value)
        {
            this.WriteByte((byte)'"');

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];

                switch (c)
                {
                    case '"':
                        this.WriteByte((byte)'\\');
                        this.WriteByte((byte)'"');
                        break;
                    case '\\':
                        this.WriteByte((byte)'\\');
                        this.WriteByte((byte)'\\');
                        break;
                    case '\b':
                        this.WriteByte((byte)'\\');
                        this.WriteByte((byte)'b');
                        break;
                    case '\t':
                        this.WriteByte((byte)'\\');
                        this.WriteByte((byte)'t');
                        break;
                    case '\n':
                        this.WriteByte((byte)'\\');
                        this.WriteByte((byte)'n');
                        break;
                    case '\f':
                        this.WriteByte((byte)'\\');
                        this.WriteByte((byte)'f');
                        break;
                    case '\r':
                        this.WriteByte((byte)'\\');
                        this.WriteByte((byte)'r');
                        break;
                    default:
                        if (c < ' ')
                        {
                            // Control chars U+0000-U+001F (excluding named ones above): \uXXXX
                            this.WriteByte((byte)'\\');
                            this.WriteByte((byte)'u');
                            this.WriteByte((byte)HexDigitLower((c >> 12) & 0xF));
                            this.WriteByte((byte)HexDigitLower((c >> 8) & 0xF));
                            this.WriteByte((byte)HexDigitLower((c >> 4) & 0xF));
                            this.WriteByte((byte)HexDigitLower(c & 0xF));
                        }
                        else if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                        {
                            // Supplementary character: write as UTF-8 (4 bytes)
                            int codePoint = char.ConvertToUtf32(c, value[i + 1]);
                            this.WriteUtf8CodePoint(codePoint);
                            i++; // skip low surrogate
                        }
                        else
                        {
                            // BMP character: write as UTF-8
                            this.WriteUtf8Char(c);
                        }

                        break;
                }
            }

            this.WriteByte((byte)'"');
        }

        private void WriteLiteral(ReadOnlySpan<byte> literal)
        {
            this.WriteBytes(literal);
        }

        private void WriteByte(byte b)
        {
            if (this.overflow)
            {
                return;
            }

            if (this.position >= this.destination.Length)
            {
                this.overflow = true;
                return;
            }

            this.destination[this.position++] = b;
        }

        private void WriteBytes(ReadOnlySpan<byte> bytes)
        {
            if (this.overflow)
            {
                return;
            }

            if (this.position + bytes.Length > this.destination.Length)
            {
                this.overflow = true;
                return;
            }

            bytes.CopyTo(this.destination.Slice(this.position));
            this.position += bytes.Length;
        }

        private void WriteUtf8Char(char c)
        {
            if (c < 0x80)
            {
                this.WriteByte((byte)c);
            }
            else if (c < 0x800)
            {
                this.WriteByte((byte)(0xC0 | (c >> 6)));
                this.WriteByte((byte)(0x80 | (c & 0x3F)));
            }
            else
            {
                this.WriteByte((byte)(0xE0 | (c >> 12)));
                this.WriteByte((byte)(0x80 | ((c >> 6) & 0x3F)));
                this.WriteByte((byte)(0x80 | (c & 0x3F)));
            }
        }

        private void WriteUtf8CodePoint(int codePoint)
        {
            if (codePoint < 0x80)
            {
                this.WriteByte((byte)codePoint);
            }
            else if (codePoint < 0x800)
            {
                this.WriteByte((byte)(0xC0 | (codePoint >> 6)));
                this.WriteByte((byte)(0x80 | (codePoint & 0x3F)));
            }
            else if (codePoint < 0x10000)
            {
                this.WriteByte((byte)(0xE0 | (codePoint >> 12)));
                this.WriteByte((byte)(0x80 | ((codePoint >> 6) & 0x3F)));
                this.WriteByte((byte)(0x80 | (codePoint & 0x3F)));
            }
            else
            {
                this.WriteByte((byte)(0xF0 | (codePoint >> 18)));
                this.WriteByte((byte)(0x80 | ((codePoint >> 12) & 0x3F)));
                this.WriteByte((byte)(0x80 | ((codePoint >> 6) & 0x3F)));
                this.WriteByte((byte)(0x80 | (codePoint & 0x3F)));
            }
        }

        private static char HexDigitLower(int nibble)
        {
            return (char)(nibble < 10 ? '0' + nibble : 'a' + nibble - 10);
        }
    }

    /// <summary>
    /// Compares indices by their corresponding string names using ordinal (UTF-16 code unit) comparison.
    /// </summary>
    private sealed class Utf16OrdinalIndexComparer : IComparer<int>
    {
        private readonly string[] names;

        private Utf16OrdinalIndexComparer(string[] names)
        {
            this.names = names;
        }

        public static Utf16OrdinalIndexComparer Create(string[] names) => new(names);

        public int Compare(int x, int y) => string.CompareOrdinal(this.names[x], this.names[y]);
    }
}