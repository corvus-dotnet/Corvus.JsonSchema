// <copyright file="FormFieldReader.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using Corvus.Text.Json;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Deserializes <c>application/x-www-form-urlencoded</c> bodies into JSON,
/// using <see cref="Utf8Uri.TryUnescapeDataString"/> for percent-decoding
/// (the inverse of <see cref="Utf8Uri.TryEscapeDataString"/> used by
/// <see cref="FormUrlEncodedSerializer"/>).
/// </summary>
/// <remarks>
/// <para>
/// This is the server-side inverse of <see cref="FormUrlEncodedSerializer"/>.
/// It scans raw UTF-8 bytes, splitting on <c>&amp;</c> and <c>=</c>, unescapes
/// keys and values, and writes the result as a JSON object to a
/// <see cref="Utf8JsonWriter"/>.
/// </para>
/// </remarks>
public ref struct FormFieldReader
{
    private ReadOnlySpan<byte> remaining;

    /// <summary>
    /// Initializes a new instance of the <see cref="FormFieldReader"/> struct.
    /// </summary>
    /// <param name="body">The raw UTF-8 form-urlencoded body bytes.</param>
    public FormFieldReader(ReadOnlySpan<byte> body)
    {
        this.remaining = body;
    }

    /// <summary>
    /// Advances to the next form field, returning the raw (still percent-encoded)
    /// key and value spans.
    /// </summary>
    /// <param name="rawKey">The raw percent-encoded key bytes.</param>
    /// <param name="rawValue">The raw percent-encoded value bytes.</param>
    /// <returns><see langword="true"/> if a field was found; <see langword="false"/> if no more fields remain.</returns>
    public bool TryReadNext(out ReadOnlySpan<byte> rawKey, out ReadOnlySpan<byte> rawValue)
    {
        while (!this.remaining.IsEmpty)
        {
            ReadOnlySpan<byte> pair;
            int ampIdx = this.remaining.IndexOf((byte)'&');
            if (ampIdx < 0)
            {
                pair = this.remaining;
                this.remaining = default;
            }
            else
            {
                pair = this.remaining[..ampIdx];
                this.remaining = this.remaining[(ampIdx + 1)..];
            }

            if (pair.IsEmpty)
            {
                continue;
            }

            int eqIdx = pair.IndexOf((byte)'=');
            if (eqIdx < 0)
            {
                rawKey = pair;
                rawValue = default;
            }
            else
            {
                rawKey = pair[..eqIdx];
                rawValue = pair[(eqIdx + 1)..];
            }

            return true;
        }

        rawKey = default;
        rawValue = default;
        return false;
    }

    /// <summary>
    /// Unescapes a percent-encoded form field key or value using
    /// <see cref="Utf8Uri.TryUnescapeDataString"/>, with form-specific
    /// <c>+</c> → space handling.
    /// </summary>
    /// <param name="encoded">The percent-encoded UTF-8 bytes.</param>
    /// <param name="destination">The buffer to write decoded bytes into. Must be at least
    /// as large as <paramref name="encoded"/> (unescaping only shrinks data).</param>
    /// <returns>The number of decoded bytes written.</returns>
    public static int Unescape(ReadOnlySpan<byte> encoded, Span<byte> destination)
    {
        if (encoded.IsEmpty)
        {
            return 0;
        }

        // Form encoding uses '+' for spaces, but UnescapeDataString doesn't handle that.
        // If no '+' is present, delegate directly.
        int plusIdx = encoded.IndexOf((byte)'+');
        if (plusIdx < 0)
        {
            Utf8Uri.TryUnescapeDataString(encoded, destination, out int written);
            return written;
        }

        // Replace '+' with space before unescaping percent sequences.
        // TryUnescapeDataString requires non-overlapping source/dest, so we copy
        // into a temporary buffer with '+' replaced, then unescape into destination.
        byte[]? rented = null;
        Span<byte> temp = encoded.Length <= 256
            ? stackalloc byte[256]
            : (rented = ArrayPool<byte>.Shared.Rent(encoded.Length));

        try
        {
            Span<byte> source = temp[..encoded.Length];
            encoded.CopyTo(source);

            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == (byte)'+')
                {
                    source[i] = (byte)' ';
                }
            }

            Utf8Uri.TryUnescapeDataString(source, destination, out int written);
            return written;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>
    /// Deserializes a <c>application/x-www-form-urlencoded</c> body into a JSON object
    /// written to the specified <see cref="Utf8JsonWriter"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each form key becomes a JSON property name. Values are classified heuristically
    /// to match what <see cref="FormUrlEncodedSerializer"/> produces:
    /// <c>true</c>/<c>false</c> → boolean, empty → null, starts with <c>{</c>/<c>[</c>
    /// → raw JSON, numeric → number, else → string.
    /// </para>
    /// <para>
    /// When the same key appears multiple times (exploded array), the values are
    /// collected into a JSON array.
    /// </para>
    /// </remarks>
    /// <param name="formBody">The raw UTF-8 form-urlencoded body bytes.</param>
    /// <param name="writer">The JSON writer to write the resulting JSON object to.</param>
    public static void DeserializeToJson(ReadOnlySpan<byte> formBody, Utf8JsonWriter writer)
    {
        // Two-pass approach:
        // Pass 1: identify which keys appear multiple times (→ arrays).
        // Pass 2: write the JSON object.
        //
        // We compare raw (encoded) key bytes — identical source keys produce identical
        // encoded forms, so raw comparison is sufficient for grouping.

        // Pass 1: collect distinct raw keys and mark duplicates.
        // Form bodies are typically small with few keys; linear scan is adequate.
        Span<int> keyOffsets = stackalloc int[64];
        Span<int> keyLengths = stackalloc int[64];
        Span<bool> isArray = stackalloc bool[64];
        int keyCount = 0;

        FormFieldReader pass1 = new(formBody);
        while (pass1.TryReadNext(out ReadOnlySpan<byte> rawKey, out _))
        {
            bool found = false;
            for (int i = 0; i < keyCount; i++)
            {
                if (RawKeyEquals(formBody, keyOffsets[i], keyLengths[i], rawKey))
                {
                    isArray[i] = true;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                if (keyCount >= keyOffsets.Length)
                {
                    // More than 64 distinct keys is extremely unusual for form bodies.
                    // Fall back to writing all as scalar properties (arrays will become
                    // duplicate properties — still valid JSON, just not ideal).
                    break;
                }

                keyOffsets[keyCount] = GetOffset(formBody, rawKey);
                keyLengths[keyCount] = rawKey.Length;
                isArray[keyCount] = false;
                keyCount++;
            }
        }

        // Pass 2: write JSON.
        writer.WriteStartObject();

        Span<bool> arrayOpened = stackalloc bool[keyCount];
        arrayOpened.Clear();

        Span<byte> keyBuf = stackalloc byte[256];
        Span<byte> valBuf = stackalloc byte[1024];
        byte[]? rentedKeyBuf = null;
        byte[]? rentedValBuf = null;

        try
        {
            FormFieldReader pass2 = new(formBody);
            while (pass2.TryReadNext(out ReadOnlySpan<byte> rawKey, out ReadOnlySpan<byte> rawValue))
            {
                // Ensure decode buffers are large enough.
                Span<byte> kBuf = EnsureBuffer(rawKey.Length, ref keyBuf, ref rentedKeyBuf);
                Span<byte> vBuf = EnsureBuffer(rawValue.Length, ref valBuf, ref rentedValBuf);

                int keyLen = Unescape(rawKey, kBuf);
                int valLen = Unescape(rawValue, vBuf);
                ReadOnlySpan<byte> key = kBuf[..keyLen];
                ReadOnlySpan<byte> value = vBuf[..valLen];

                // Find key index.
                int idx = FindKeyIndex(formBody, keyOffsets, keyLengths, keyCount, rawKey);

                if (idx >= 0 && isArray[idx])
                {
                    if (!arrayOpened[idx])
                    {
                        writer.WritePropertyName(key);
                        writer.WriteStartArray();
                        arrayOpened[idx] = true;
                    }

                    WriteJsonValue(writer, value);
                }
                else
                {
                    WriteJsonProperty(writer, key, value);
                }
            }

            // Close open arrays.
            for (int i = 0; i < keyCount; i++)
            {
                if (arrayOpened[i])
                {
                    writer.WriteEndArray();
                }
            }

            writer.WriteEndObject();
        }
        finally
        {
            if (rentedKeyBuf is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedKeyBuf);
            }

            if (rentedValBuf is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedValBuf);
            }
        }
    }

    /// <summary>
    /// Writes a percent-decoded form value as the appropriate JSON token to a
    /// <see cref="Utf8JsonWriter"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Detects the value type heuristically (matching what
    /// <see cref="FormUrlEncodedSerializer"/> produces):
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>true</c>/<c>false</c> → JSON boolean</description></item>
    /// <item><description>Empty span → JSON null</description></item>
    /// <item><description>Starts with <c>{</c> or <c>[</c> → raw JSON (complex type)</description></item>
    /// <item><description>Looks numeric → raw JSON number</description></item>
    /// <item><description>Everything else → JSON string</description></item>
    /// </list>
    /// </remarks>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="decodedValue">The percent-decoded form value bytes (UTF-8).</param>
    public static void WriteJsonValue(
        Utf8JsonWriter writer,
        ReadOnlySpan<byte> decodedValue)
    {
        if (decodedValue.IsEmpty)
        {
            writer.WriteNullValue();
            return;
        }

        byte first = decodedValue[0];

        // JSON structure (object or array) — value was JSON-stringified.
        if (first is (byte)'{' or (byte)'[')
        {
            writer.WriteRawValue(decodedValue);
            return;
        }

        // Boolean literals.
        if (decodedValue.SequenceEqual("true"u8))
        {
            writer.WriteBooleanValue(true);
            return;
        }

        if (decodedValue.SequenceEqual("false"u8))
        {
            writer.WriteBooleanValue(false);
            return;
        }

        // Numeric: starts with digit or '-' (and has content after a leading dash).
        if ((first is (byte)'-' or (>= (byte)'0' and <= (byte)'9'))
            && decodedValue.Length > (first == (byte)'-' ? 1 : 0)
            && IsNumericLiteral(decodedValue))
        {
            writer.WriteRawValue(decodedValue);
            return;
        }

        // Default: write as JSON string.
        writer.WriteStringValue(decodedValue);
    }

    /// <summary>
    /// Writes a percent-decoded form value as a named JSON property with the appropriate
    /// value token.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="propertyName">The JSON property name (UTF-8).</param>
    /// <param name="decodedValue">The percent-decoded form value bytes (UTF-8).</param>
    public static void WriteJsonProperty(
        Utf8JsonWriter writer,
        ReadOnlySpan<byte> propertyName,
        ReadOnlySpan<byte> decodedValue)
    {
        writer.WritePropertyName(propertyName);
        WriteJsonValue(writer, decodedValue);
    }

    /// <summary>
    /// Rents a byte buffer from the shared pool.
    /// </summary>
    /// <param name="minimumSize">The minimum required size.</param>
    /// <returns>A rented byte array (may be larger than requested).</returns>
    /// <remarks>
    /// The caller must return the buffer via <see cref="Return"/> when done.
    /// This decouples callers from the specific pooling implementation.
    /// </remarks>
    public static byte[] Rent(int minimumSize) => ArrayPool<byte>.Shared.Rent(minimumSize);

    /// <summary>
    /// Returns a previously rented byte buffer to the shared pool.
    /// </summary>
    /// <param name="buffer">The buffer previously obtained from <see cref="Rent"/> or
    /// <see cref="RentBodyAsync"/>.</param>
    public static void Return(byte[] buffer) => ArrayPool<byte>.Shared.Return(buffer);

    /// <summary>
    /// Reads the entire body from a stream into a rented byte array.
    /// The caller must return the array via <see cref="Return"/> when done.
    /// </summary>
    /// <param name="stream">The request body stream.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// A tuple of the rented byte array and the number of valid bytes.
    /// The caller MUST call <see cref="Return"/> with the buffer when finished.
    /// </returns>
    public static async ValueTask<(byte[] Buffer, int Length)> RentBodyAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        const int InitialSize = 4096;
        byte[] buffer = Rent(InitialSize);
        int offset = 0;

        try
        {
            while (true)
            {
                if (offset == buffer.Length)
                {
                    byte[] newBuffer = Rent(buffer.Length * 2);
                    buffer.AsSpan(0, offset).CopyTo(newBuffer);
                    Return(buffer);
                    buffer = newBuffer;
                }

                int read = await stream.ReadAsync(
                    buffer.AsMemory(offset, buffer.Length - offset),
                    cancellationToken).ConfigureAwait(false);

                if (read == 0)
                {
                    break;
                }

                offset += read;
            }

            return (buffer, offset);
        }
        catch
        {
            Return(buffer);
            throw;
        }
    }

    private static bool IsNumericLiteral(ReadOnlySpan<byte> value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            byte b = value[i];
            if (b is not ((>= (byte)'0' and <= (byte)'9')
                or (byte)'-' or (byte)'+' or (byte)'.' or (byte)'e' or (byte)'E'))
            {
                return false;
            }
        }

        return true;
    }

    private static int GetOffset(ReadOnlySpan<byte> body, ReadOnlySpan<byte> slice)
    {
        // Calculate the byte offset of slice within body.
        // Both spans reference the same underlying memory.
        ref byte bodyRef = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(body);
        ref byte sliceRef = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(slice);
        return (int)System.Runtime.CompilerServices.Unsafe.ByteOffset(ref bodyRef, ref sliceRef);
    }

    private static bool RawKeyEquals(
        ReadOnlySpan<byte> body, int offset, int length, ReadOnlySpan<byte> rawKey)
    {
        return body.Slice(offset, length).SequenceEqual(rawKey);
    }

    private static int FindKeyIndex(
        ReadOnlySpan<byte> body,
        Span<int> offsets,
        Span<int> lengths,
        int count,
        ReadOnlySpan<byte> rawKey)
    {
        for (int i = 0; i < count; i++)
        {
            if (RawKeyEquals(body, offsets[i], lengths[i], rawKey))
            {
                return i;
            }
        }

        return -1;
    }

    private static Span<byte> EnsureBuffer(int needed, ref Span<byte> buffer, ref byte[]? rented)
    {
        if (needed <= buffer.Length)
        {
            return buffer;
        }

        if (rented is not null)
        {
            ArrayPool<byte>.Shared.Return(rented);
        }

        rented = ArrayPool<byte>.Shared.Rent(needed);
        buffer = rented;
        return buffer;
    }
}