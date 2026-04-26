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

            // Rent arrays for properties, UTF-16 sort keys, and sort indices
            JsonProperty<JsonElement>[] properties = ArrayPool<JsonProperty<JsonElement>>.Shared.Rent(count);
            ReadOnlyMemory<char>[] utf16Names = ArrayPool<ReadOnlyMemory<char>>.Shared.Rent(count);
            char[]?[] rentedNameChars = ArrayPool<char[]?>.Shared.Rent(count);
            int[] indices = ArrayPool<int>.Shared.Rent(count);

            try
            {
                // Collect properties and take ownership of UTF-16 name buffers for sorting
                int i = 0;
                foreach (JsonProperty<JsonElement> prop in element.EnumerateObject())
                {
                    properties[i] = prop;
                    UnescapedUtf16JsonString utf16Name = prop.Utf16NameSpan;
                    utf16Names[i] = utf16Name.TakeOwnership(out rentedNameChars[i]);
                    indices[i] = i;
                    i++;
                }

                // Sort indices by property name using UTF-16 code unit ordinal comparison
                Array.Sort(indices, 0, count, Utf16MemoryIndexComparer.Create(utf16Names));

                // Check for duplicate property names (I-JSON requirement)
                for (int j = 1; j < count; j++)
                {
                    if (utf16Names[indices[j]].Span.SequenceEqual(utf16Names[indices[j - 1]].Span))
                    {
                        throw new InvalidOperationException("Duplicate property name detected. JCS requires I-JSON compliant input (RFC 7493).");
                    }
                }

                // Emit properties in sorted order — write names from UTF-8 source
                for (int j = 0; j < count; j++)
                {
                    if (j > 0)
                    {
                        this.WriteByte((byte)',');
                    }

                    int idx = indices[j];

                    // Write property name as canonical UTF-8
                    using UnescapedUtf8JsonString utf8Name = properties[idx].Utf8NameSpan;
                    this.WriteCanonicalUtf8String(utf8Name.Span);
                    this.WriteByte((byte)':');
                    this.WriteElement(properties[idx].Value, depth + 1);
                }
            }
            finally
            {
                // Return rented char arrays from TakeOwnership
                for (int j = 0; j < count; j++)
                {
                    if (rentedNameChars[j] != null)
                    {
                        rentedNameChars[j]!.AsSpan(0, utf16Names[j].Length).Clear();
                        ArrayPool<char>.Shared.Return(rentedNameChars[j]!);
                    }
                }

                ArrayPool<JsonProperty<JsonElement>>.Shared.Return(properties, clearArray: true);
                ArrayPool<ReadOnlyMemory<char>>.Shared.Return(utf16Names, clearArray: true);
                ArrayPool<char[]?>.Shared.Return(rentedNameChars, clearArray: true);
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
            // Get the unescaped UTF-8 bytes and re-escape per JCS rules
            using UnescapedUtf8JsonString utf8Value = element.GetUtf8String();
            this.WriteCanonicalUtf8String(utf8Value.Span);
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
        /// Writes a UTF-8 byte span with JCS canonical escaping.
        /// </summary>
        /// <remarks>
        /// RFC 8785 §3.2.2.2 escaping rules:
        /// - <c>\"</c> and <c>\\</c> are always escaped.
        /// - Named escapes for <c>\b</c>, <c>\t</c>, <c>\n</c>, <c>\f</c>, <c>\r</c>.
        /// - <c>\uXXXX</c> (lowercase hex) for remaining control characters (U+0000–U+001F).
        /// - All other characters are written literally as UTF-8.
        /// </remarks>
        private void WriteCanonicalUtf8String(ReadOnlySpan<byte> utf8Value)
        {
            this.WriteByte((byte)'"');

            for (int i = 0; i < utf8Value.Length; i++)
            {
                byte b = utf8Value[i];

                switch (b)
                {
                    case (byte)'"':
                        this.WriteByte((byte)'\\');
                        this.WriteByte((byte)'"');
                        break;
                    case (byte)'\\':
                        this.WriteByte((byte)'\\');
                        this.WriteByte((byte)'\\');
                        break;
                    case 0x08: // \b
                        this.WriteByte((byte)'\\');
                        this.WriteByte((byte)'b');
                        break;
                    case 0x09: // \t
                        this.WriteByte((byte)'\\');
                        this.WriteByte((byte)'t');
                        break;
                    case 0x0A: // \n
                        this.WriteByte((byte)'\\');
                        this.WriteByte((byte)'n');
                        break;
                    case 0x0C: // \f
                        this.WriteByte((byte)'\\');
                        this.WriteByte((byte)'f');
                        break;
                    case 0x0D: // \r
                        this.WriteByte((byte)'\\');
                        this.WriteByte((byte)'r');
                        break;
                    default:
                        if (b < 0x20)
                        {
                            // Control chars U+0000-U+001F (excluding named ones above): \uXXXX
                            this.WriteByte((byte)'\\');
                            this.WriteByte((byte)'u');
                            this.WriteByte((byte)'0');
                            this.WriteByte((byte)'0');
                            this.WriteByte((byte)HexDigitLower((b >> 4) & 0xF));
                            this.WriteByte((byte)HexDigitLower(b & 0xF));
                        }
                        else
                        {
                            // All other bytes (ASCII printable, UTF-8 continuation/lead bytes)
                            // pass through as-is — they are valid UTF-8 and need no escaping.
                            this.WriteByte(b);
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

        private static char HexDigitLower(int nibble)
        {
            return (char)(nibble < 10 ? '0' + nibble : 'a' + nibble - 10);
        }
    }

    /// <summary>
    /// Compares indices by their corresponding UTF-16 char memory using ordinal comparison.
    /// </summary>
    private sealed class Utf16MemoryIndexComparer : IComparer<int>
    {
        private readonly ReadOnlyMemory<char>[] names;

        private Utf16MemoryIndexComparer(ReadOnlyMemory<char>[] names)
        {
            this.names = names;
        }

        public static Utf16MemoryIndexComparer Create(ReadOnlyMemory<char>[] names) => new(names);

        public int Compare(int x, int y) => this.names[x].Span.SequenceCompareTo(this.names[y].Span);
    }
}