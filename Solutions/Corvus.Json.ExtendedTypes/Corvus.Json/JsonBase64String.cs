// <copyright file="JsonBase64String.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents a JSON base64 string.
/// </summary>
public readonly partial struct JsonBase64String
{
    /// <summary>
    /// Creates a new instance of the <see cref="JsonBase64String"/> struct from a byte arrary.
    /// </summary>
    /// <param name="value">The <see cref="ReadOnlySpan{T}"/> of <see cref="byte"/> from which to construct the Base64 content.</param>
    /// <returns>The base 64 encoded string represnetation of the byte array.</returns>
    /// <remarks>This encodes the byte array as a base 64 string.</remarks>
    public static JsonBase64String FromByteArray(ReadOnlySpan<byte> value)
    {
        return new JsonBase64String(StandardBase64.EncodeToString(value));
    }

    /// <summary>
    /// Get the base64 encoded string.
    /// </summary>
    /// <returns>The base 64 encoded string.</returns>
    [Obsolete("Use the standard GetString() method.")]
    public ReadOnlySpan<char> GetBase64EncodedString()
    {
        return this.GetString().AsSpan();
    }

    /// <summary>
    /// Gets the minimum size for a buffer to
    /// pass to <see cref="TryGetDecodedBase64Bytes(Span{byte}, out int)"/>.
    /// </summary>
    /// <returns>A buffer that will be of a suitable length decoding the base64 content.</returns>
    /// <remarks>This is not a zero-cost operation. If you know the expected maximum buffer size in advance,
    /// you can improve performance by pre-allocating a reasonable buffer size and calling <see cref="TryGetDecodedBase64Bytes(Span{byte}, out int)"/>.
    /// If the buffer was too small, the <c>written</c> value will be the desired buffer size.</remarks>
    public int GetDecodedBufferSize()
    {
        if ((this.backing & Backing.String) != 0)
        {
            return StandardBase64.GetDecodedBufferSize(this.stringBacking);
        }

        if (this.jsonElementBacking.ValueKind == JsonValueKind.String)
        {
            return StandardBase64.GetDecodedBufferSize(this.jsonElementBacking);
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Try to get the decoded base64 bytes.
    /// </summary>
    /// <param name="result">The span into which to write the bytes.</param>
    /// <param name="written">The number of bytes written.</param>
    /// <returns><see langword="true"/> if the bytes were successfully decoded.</returns>
    /// <remarks>
    /// If the <paramref name="result"/> buffer was too short for the decoded bytes, the method will return <see langword="false"/>,
    /// and <paramref name="written"/> will be a number representing a buffer of sufficient size to decode the value. Otherwise it will be <c>0</c>.
    /// </remarks>
    public bool TryGetDecodedBase64Bytes(Span<byte> result, out int written)
    {
        if ((this.backing & Backing.String) != 0)
        {
            return StandardBase64.Decode(this.stringBacking, result, out written);
        }

        if (this.jsonElementBacking.ValueKind == JsonValueKind.String)
        {
            return StandardBase64.Decode(this.jsonElementBacking, result, out written);
        }

        written = 0;
        return false;
    }

    /// <summary>
    /// Get the decoded base64 bytes.
    /// </summary>
    /// <returns>The base 64 bytes.</returns>
    [Obsolete("Use the TryDecodeBase64Bytes() method.")]
    public ReadOnlySpan<byte> GetDecodedBase64Bytes()
    {
        Span<byte> decoded = new byte[this.GetDecodedBufferSize()];
        if (this.TryGetDecodedBase64Bytes(decoded, out int written))
        {
            return decoded[..written];
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Get a value indicating whether this instance has a Base64-encoded byte array.
    /// </summary>
    /// <returns>The base 64 bytes.</returns>
    public bool HasBase64Bytes()
    {
        if ((this.backing & Backing.String) != 0)
        {
            return StandardBase64.HasBase64Bytes(this.stringBacking);
        }

        if (this.jsonElementBacking.ValueKind == JsonValueKind.String)
        {
            return StandardBase64.HasBase64Bytes(this.jsonElementBacking);
        }

        return false;
    }
}