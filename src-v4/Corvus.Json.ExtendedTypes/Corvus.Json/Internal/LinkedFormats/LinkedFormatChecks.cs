// <copyright file="LinkedFormatChecks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;

namespace Corvus.Json.Internal;

/// <summary>
/// Runs the format checks linked from Corvus.Text.Json (<c>Corvus.Text.Json.Internal.FormatMatchers</c>) on the values
/// of this engine, so the two engines give the same answer for every format.
/// </summary>
internal static class LinkedFormatChecks
{
    /// <summary>
    /// A check of a UTF-8 encoded value.
    /// </summary>
    /// <param name="value">The UTF-8 encoded value.</param>
    /// <returns><see langword="true"/> if the value matches.</returns>
    internal delegate bool Utf8Check(ReadOnlySpan<byte> value);

    private const int StackallocByteThreshold = 256;

    /// <summary>
    /// Runs a check on the string value of an instance.
    /// </summary>
    /// <typeparam name="T">The type of the instance.</typeparam>
    /// <param name="instance">The instance, which is a string.</param>
    /// <param name="check">The check.</param>
    /// <returns><see langword="true"/> if the value matches.</returns>
    internal static bool Matches<T>(in T instance, Utf8Check check)
        where T : struct, IJsonValue<T>
    {
        return instance.AsString.TryGetValue(Run, check, out bool matches) && matches;

        static bool Run(ReadOnlySpan<byte> value, in Utf8Check check, out bool matches)
        {
            matches = check(value);
            return true;
        }
    }

    /// <summary>
    /// Runs a check on a UTF-16 value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="check">The check.</param>
    /// <returns><see langword="true"/> if the value matches.</returns>
    internal static bool Matches(ReadOnlySpan<char> value, Utf8Check check)
    {
        int maxBytes = Encoding.UTF8.GetMaxByteCount(value.Length);
        byte[]? rented = null;
        Span<byte> buffer = maxBytes <= StackallocByteThreshold
            ? stackalloc byte[StackallocByteThreshold]
            : (rented = ArrayPool<byte>.Shared.Rent(maxBytes));

        try
        {
            int written = Transcode(value, buffer);
            return written >= 0 && check(buffer.Slice(0, written));
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
    /// Runs a check on a UTF-16 value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="check">The check.</param>
    /// <returns><see langword="true"/> if the value matches.</returns>
    internal static bool Matches(string value, Utf8Check check) => Matches(value.AsSpan(), check);

    /// <summary>
    /// Writes UTF-16 text as UTF-8, refusing ill-formed text (a lone surrogate is not a character of any format).
    /// </summary>
    /// <returns>The bytes written, or -1 if the text is ill-formed.</returns>
    private static int Transcode(ReadOnlySpan<char> value, Span<byte> destination)
    {
        int written = 0;
        for (int i = 0; i < value.Length; i++)
        {
            int c = value[i];
            if (c < 0x80)
            {
                destination[written++] = (byte)c;
                continue;
            }

            if (char.IsSurrogate((char)c))
            {
                if (!char.IsHighSurrogate((char)c) || i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1]))
                {
                    return -1;
                }

                c = char.ConvertToUtf32((char)c, value[++i]);
            }

            if (c < 0x800)
            {
                destination[written++] = (byte)(0xC0 | (c >> 6));
                destination[written++] = (byte)(0x80 | (c & 0x3F));
            }
            else if (c < 0x10000)
            {
                destination[written++] = (byte)(0xE0 | (c >> 12));
                destination[written++] = (byte)(0x80 | ((c >> 6) & 0x3F));
                destination[written++] = (byte)(0x80 | (c & 0x3F));
            }
            else
            {
                destination[written++] = (byte)(0xF0 | (c >> 18));
                destination[written++] = (byte)(0x80 | ((c >> 12) & 0x3F));
                destination[written++] = (byte)(0x80 | ((c >> 6) & 0x3F));
                destination[written++] = (byte)(0x80 | (c & 0x3F));
            }
        }

        return written;
    }
}