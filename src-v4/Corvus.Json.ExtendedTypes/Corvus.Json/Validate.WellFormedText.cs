// <copyright file="Validate.WellFormedText.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json;

/// <summary>
/// JsonSchema validation errors.
/// </summary>
public static partial class Validate
{
    private const string TextIsNotWellFormedMessage = "Validation - the value is not well-formed text: it has an escape of half of a surrogate pair with no other half, or bytes that are not UTF-8.";

    /// <summary>
    /// Whether a value's text is not well-formed: a string, or the name of one of an object's properties, that has an
    /// escape of half of a surrogate pair with no other half (such as <c>\ud800</c>), or bytes that are not UTF-8.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="instance">The value.</param>
    /// <returns><see langword="true"/> when the text of the value is not well-formed.</returns>
    /// <remarks>
    /// <para>
    /// The parser accepts such text, and reading it throws <see cref="InvalidOperationException"/>. Generated
    /// validation catches that exception only when this is <see langword="true"/> of the value it is validating, and
    /// reports the value not valid (https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/1021).
    /// </para>
    /// <para>
    /// It reads the raw text of the value, and is meant for the path on which reading the value has already thrown.
    /// </para>
    /// </remarks>
    public static bool IsNotWellFormedText<T>(in T instance)
        where T : struct, IJsonValue<T>
    {
#if BUILDING_SOURCE_GENERATOR
        return false;
#else
        if (!instance.HasJsonElementBacking)
        {
            return instance.ValueKind == JsonValueKind.String && instance.AsString.HasDotnetBacking && HasLoneSurrogate((string)instance.AsString);
        }

        JsonElement element = instance.AsJsonElement;
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                ReadOnlySpan<byte> raw = System.Runtime.InteropServices.JsonMarshal.GetRawUtf8Value(element);
                return !IsWellFormedJsonStringText(raw.Slice(1, raw.Length - 2));

            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (!IsWellFormedJsonStringText(System.Runtime.InteropServices.JsonMarshal.GetRawUtf8PropertyName(property)))
                    {
                        return true;
                    }
                }

                return false;

            default:
                return false;
        }
#endif
    }

    /// <summary>
    /// Reports that a value's text is not well-formed (see <see cref="IsNotWellFormedText{T}(in T)"/>).
    /// </summary>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="level">The validation level.</param>
    /// <returns>The validation context, with the value not valid.</returns>
    public static ValidationContext TextIsNotWellFormed(in ValidationContext validationContext, ValidationLevel level)
    {
        return level == ValidationLevel.Flag
            ? validationContext.WithResult(isValid: false)
            : validationContext.WithResult(isValid: false, TextIsNotWellFormedMessage);
    }

#if !BUILDING_SOURCE_GENERATOR
    /// <summary>
    /// Whether the raw text of a JSON string (between its quotes) is UTF-8 and every escape of a surrogate is one of a
    /// pair.
    /// </summary>
    private static bool IsWellFormedJsonStringText(ReadOnlySpan<byte> raw)
    {
        if (!IsUtf8(raw))
        {
            return false;
        }

        bool expectLow = false;
        for (int i = raw.IndexOf((byte)'\\'); i >= 0 && i < raw.Length; i++)
        {
            if (raw[i] != (byte)'\\')
            {
                if (expectLow)
                {
                    return false;
                }

                continue;
            }

            if (raw[i + 1] != (byte)'u')
            {
                if (expectLow)
                {
                    return false;
                }

                i++;
                continue;
            }

            // The parser accepted the text, so a \u has four hex digits.
            int codeUnit = 0;
            for (int d = i + 2; d < i + 6; d++)
            {
                int b = raw[d] | 0x20;
                codeUnit = (codeUnit << 4) + (b <= '9' ? b - '0' : b - 'a' + 10);
            }

            i += 5;
            bool high = codeUnit is >= 0xD800 and <= 0xDBFF;
            bool low = codeUnit is >= 0xDC00 and <= 0xDFFF;
            if (expectLow)
            {
                if (!low)
                {
                    return false;
                }

                expectLow = false;
            }
            else if (low)
            {
                return false;
            }
            else
            {
                expectLow = high;
            }
        }

        return !expectLow;
    }

    private static bool IsUtf8(ReadOnlySpan<byte> text)
    {
#if NET
        return System.Text.Unicode.Utf8.IsValid(text);
#else
        try
        {
            JsonReaderHelper.s_utf8Encoding.GetCharCount(text.ToArray());
            return true;
        }
        catch (System.Text.DecoderFallbackException)
        {
            return false;
        }
#endif
    }

    private static bool HasLoneSurrogate(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(text[i]))
            {
                return true;
            }
        }

        return false;
    }
#endif
}