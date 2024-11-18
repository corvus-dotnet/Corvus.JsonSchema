// <copyright file="Formatting.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Formatting utilities.
/// </summary>
public static class Formatting
{
    /// <summary>
    /// The maximum length of an identifier, according to CS0645.
    /// </summary>
    public const int MaxIdentifierLength = 512;

    private static readonly string[] Keywords =
    [
        "abstract", "as", "base", "bool",
        "break", "byte", "case", "catch",
        "char", "checked", "class", "const",
        "continue", "decimal", "default", "delegate",
        "do", "double", "else", "enum",
        "event", "explicit", "extern", "false",
        "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit",
        "in", "int", "interface", "internal",
        "is",  "item", "lock", "long", "namespace",
        "new", "null", "object", "operator",
        "out", "override", "params", "private",
        "protected", "public", "readonly", "ref",
        "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string",
        "struct", "switch", "this", "throw",
        "true", "try", "typeof", "uint",
        "ulong", "unchecked", "unsafe", "ushort",
        "using", "virtual", "void", "volatile",
        "while",
        "Equals", "Finalize", "GetHashCode", "GetType",
        "MemberwiseClone", "ReferenceEquals", "ToString"
    ];

    private static readonly string[] ReservedNames =
    [
        "Add",
        "AddRange",
        "As",
        "AsAny",
        "AsArray",
        "AsBoolean",
        "AsImmutableList",
        "AsImmutableListBuilder",
        "AsJsonElement",
        "AsObject",
        "AsString",
        "Concatenate",
        "ConstInstance",
        "Count",
        "Create",
        "CreateFromSerializedInstance",
        "DefaultInstance",
        "Dimension",
        "Directory",
        "EnumerateArray",
        "Equals",
        "EqualsString",
        "EqualsUtf8Bytes",
        "FromAny",
        "FromItems",
        "FromProperties",
        "FromRange",
        "FromValues",
        "GetArrayLength",
        "GetBoolean",
        "GetDate",
        "GetDateTime",
        "GetDecodedBase64Bytes",
        "GetDecodedBufferSize",
        "GetEnumerator",
        "GetGuid",
        "GetHashCode",
        "GetIPAddress",
        "GetJsonDocument",
        "GetRegex",
        "GetString",
        "GetTime",
        "HasBase64Bytes",
        "HasDotnetBacking",
        "HasJsonElementBacking",
        "HasProperties",
        "HasProperty",
        "Insert",
        "InsertRange",
        "Item",
        "Items",
        "Match",
        "Null",
        "Parse",
        "Rank",
        "Remove",
        "RemoveAt",
        "RemoveProperty",
        "RemoveRange",
        "SchemaLocation",
        "SetProperty",
        "Stream",
        "ToString",
        "TryCreateUriTemplateParser",
        "TryGetBoolean",
        "TryGetDate",
        "TryGetDateTime",
        "TryGetDecodedBase64Bytes",
        "TryGetGuid",
        "TryGetIPAddress",
        "TryGetJsonDocument",
        "TryGetNumericValues",
        "TryGetProperty",
        "TryGetString",
        "TryGetTime",
        "TryGetUnescapedString",
        "TryGetUri",
        "TryGetUriTemplate",
        "Undefined",
        "Validate",
        "ValueBufferSize",
        "ValueKind",
        "WriteTo",
        "__CorvusArrayHelpers",
        "__CorvusObjectHelpers"
    ];

    private static ReadOnlySpan<char> EntitySuffix => "Entity".AsSpan();

    private static ReadOnlySpan<char> TypePrefix => "Type".AsSpan();

    private static ReadOnlySpan<char> ArraySuffix => "Array".AsSpan();

    private static ReadOnlySpan<char> PropertySuffix => "Property".AsSpan();

    /// <summary>
    /// Format a name for a property declaration.
    /// </summary>
    /// <param name="propertyNameBuffer">The buffer containing the property name to format.</param>
    /// <param name="writtenLength">The length of the property name in the buffer.</param>
    /// <returns>The length of the formatted name in the <paramref name="propertyNameBuffer"/>.</returns>
    public static int FormatPropertyNameComponent(Span<char> propertyNameBuffer, int writtenLength)
    {
        writtenLength = Formatting.ToPascalCase(propertyNameBuffer[..writtenLength]);
        writtenLength = Formatting.FixReservedWords(propertyNameBuffer, writtenLength, "V".AsSpan(), "Value".AsSpan());
        writtenLength = Formatting.FixReservedWords(ReservedNames, propertyNameBuffer, writtenLength, "V".AsSpan(), "Value".AsSpan());
        if (writtenLength == 0)
        {
            PropertySuffix.CopyTo(propertyNameBuffer);
            writtenLength = PropertySuffix.Length;
        }

        return writtenLength;
    }

    /// <summary>
    /// Format a name for a type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to format the name.</param>
    /// <param name="corvusTypeName">The type name to format. This will be copied into the <paramref name="typeNameBuffer"/>.</param>
    /// <param name="typeNameBuffer">The buffer into which to format that name.</param>
    /// <returns>The length of the formatted name in the <paramref name="typeNameBuffer"/>.</returns>
    public static int FormatTypeNameComponent(TypeDeclaration typeDeclaration, ReadOnlySpan<char> corvusTypeName, Span<char> typeNameBuffer)
    {
        if (!corvusTypeName.TryCopyTo(typeNameBuffer))
        {
            ThrowIdentifierTooLongException(typeDeclaration);
        }

        int writtenLength = Formatting.ToPascalCase(typeNameBuffer[..corvusTypeName.Length]);
        writtenLength = Formatting.FixReservedWords(typeNameBuffer, writtenLength, TypePrefix, Formatting.EntitySuffix);
        writtenLength = Formatting.FixReservedWords(ReservedNames, typeNameBuffer, writtenLength, TypePrefix, Formatting.EntitySuffix);

        if (writtenLength == 0)
        {
            EntitySuffix.CopyTo(typeNameBuffer);
            writtenLength = EntitySuffix.Length;
        }

        return writtenLength;
    }

    /// <summary>
    /// Apply a numeric suffix to the buffer.
    /// </summary>
    /// <param name="value">The numeric suffix.</param>
    /// <param name="buffer">The buffer into which to format the numeric suffix.</param>
    /// <returns>The number of characters written.</returns>
    /// <exception cref="ArgumentException">There was not enough space in the buffer.</exception>
    public static int ApplySuffix(int value, Span<char> buffer)
    {
#if NET8_0_OR_GREATER
        value.TryFormat(buffer, out int written);
        return written;
#else
        if (value < 0)
        {
            throw new ArgumentException("Value must be positive.", nameof(value));
        }

        // Calculate the length of the integer
        int length = value == 0 ? 1 : (int)Math.Floor(Math.Log10(value) + 1);
        if (buffer.Length < length)
        {
            throw new ArgumentException("Destination array is too small.");
        }

        int index = length;

        do
        {
            buffer[--index] = (char)('0' + (value % 10));
            value /= 10;
        }
        while (value > 0);

        return length; // Return the number of characters written
#endif
    }

    /// <summary>
    /// Apply a standard suffix to the name in the namespace.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="typeNameBuffer">The buffer into which to format that name.</param>
    /// <param name="candidate">The current candidate name.</param>
    /// <returns>The number of characters in the buffer.</returns>
    public static int ApplyStandardSuffix(TypeDeclaration typeDeclaration, Span<char> typeNameBuffer, ReadOnlySpan<char> candidate)
    {
        if (
            (typeDeclaration.AllowedCoreTypes() & CoreTypes.Array) != 0
            && !candidate.EndsWith(ArraySuffix))
        {
            ArraySuffix.CopyTo(typeNameBuffer[candidate.Length..]);
            return candidate.Length + ArraySuffix.Length;
        }
        else if (!candidate.EndsWith(EntitySuffix))
        {
            EntitySuffix.CopyTo(typeNameBuffer[candidate.Length..]);
            return candidate.Length + EntitySuffix.Length;
        }
        else
        {
            Formatting.FixReservedWords(typeNameBuffer, candidate.Length, TypePrefix, ReadOnlySpan<char>.Empty);
        }

        return candidate.Length;
    }

    /// <summary>
    /// Format a composite name into the type name buffer.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="typeNameBuffer">The bfufer into which to format the name.</param>
    /// <param name="first">The first part of the name.</param>
    /// <param name="second">The second part of the name.</param>
    /// <returns>The length of the name.</returns>
    public static int FormatCompositeName(TypeDeclaration typeDeclaration, Span<char> typeNameBuffer, ReadOnlySpan<char> first, ReadOnlySpan<char> second)
    {
        if (!first.TryCopyTo(typeNameBuffer))
        {
            Formatting.ThrowIdentifierTooLongException(typeDeclaration);
        }

        if (!second.TryCopyTo(typeNameBuffer[first.Length..]))
        {
            Formatting.ThrowIdentifierTooLongException(typeDeclaration);
        }

        return first.Length + second.Length;
    }

    /// <summary>
    /// Gets the required buffer length for the formatting operations on the string.
    /// </summary>
    /// <param name="stringLength">The length of string which requires formatting.</param>
    /// <param name="leadingDigitPrefix">The prefix to apply to a name with leading digits.</param>
    /// <param name="collisionSuffix">The suffix to apply to a name if there is a collision.</param>
    /// <returns>The required buffer length.</returns>
    public static int GetBufferLength(int stringLength, ReadOnlySpan<char> leadingDigitPrefix, ReadOnlySpan<char> collisionSuffix)
    {
        return stringLength + leadingDigitPrefix.Length + collisionSuffix.Length;
    }

    /// <summary>
    /// Convert the given name to <c>camelCase</c>.
    /// </summary>
    /// <param name="name">The name to convert.</param>
    /// <returns>The number of characters in the output.</returns>
    public static int ToCamelCase(Span<char> name)
    {
        if (name.Length == 0)
        {
            return 0;
        }

        // We could possibly do a better job here by using the "in place" access to the underlying
        // string data; however, we'd still end up with a single buffer copy of at least the length
        // of the string data, so I think this is a "reasonable" approach.
        return FixCasing(name, false, true);
    }

    /// <summary>
    /// Convert the given name to <c>PascalCase</c>, in place.
    /// </summary>
    /// <param name="name">The name to convert.</param>
    /// <returns>The number of characters in the output span.</returns>
    public static int ToPascalCase(Span<char> name)
    {
        if (name.Length == 0)
        {
            return 0;
        }

        // We could possibly do a better job here by using the "in place" access to the underlying
        // string data; however, we'd still end up with a single buffer copy of at least the length
        // of the string data, so I think this is a "reasonable" approach.
        return FixCasing(name, true, false);
    }

    /// <summary>
    /// Fix any reserved words issues in place.
    /// </summary>
    /// <param name="buffer">The buffer containing the chars to fix.</param>
    /// <param name="length">The length of the string in the buffer.</param>
    /// <param name="leadingDigitPrefix">The prefix to prepend in the leading character was a digit.</param>
    /// <param name="collisionSuffix">The suffix to append if there was a collision.</param>
    /// <returns>The number of characters in the resulting string.</returns>
    /// <remarks>
    /// Call <see cref="GetBufferLength(int, ReadOnlySpan{char}, ReadOnlySpan{char})"/> to get the
    /// required buffer length for the string.
    /// </remarks>
    public static int FixReservedWords(Span<char> buffer, int length, ReadOnlySpan<char> leadingDigitPrefix, ReadOnlySpan<char> collisionSuffix)
    {
        return FixReservedWords(Keywords, buffer, length, leadingDigitPrefix, collisionSuffix);
    }

    /// <summary>
    /// Fix any reserved words issues in place.
    /// </summary>
    /// <param name="keywords">The keywords to fix.</param>
    /// <param name="buffer">The buffer containing the chars to fix.</param>
    /// <param name="length">The length of the string in the buffer.</param>
    /// <param name="leadingDigitPrefix">The prefix to prepend in the leading character was a digit.</param>
    /// <param name="collisionSuffix">The suffix to append if there was a collision.</param>
    /// <returns>The number of characters in the resulting string.</returns>
    /// <remarks>
    /// Call <see cref="GetBufferLength(int, ReadOnlySpan{char}, ReadOnlySpan{char})"/> to get the
    /// required buffer length for the string.
    /// </remarks>
    public static int FixReservedWords(string[] keywords, Span<char> buffer, int length, ReadOnlySpan<char> leadingDigitPrefix, ReadOnlySpan<char> collisionSuffix)
    {
        if (length == 0)
        {
            return 0;
        }

        ReadOnlySpan<char> v = buffer[..length];

        foreach (string k in keywords)
        {
            if (k.AsSpan().SequenceEqual(v))
            {
                if (collisionSuffix.IsEmpty)
                {
                    collisionSuffix = EntitySuffix;
                }

                collisionSuffix.CopyTo(buffer[length..]);
                return length + collisionSuffix.Length;
            }
        }

        if (char.IsDigit(v[0]))
        {
            v.CopyTo(buffer[leadingDigitPrefix.Length..]);
            leadingDigitPrefix.CopyTo(buffer);
            return length + leadingDigitPrefix.Length;
        }

        return length;
    }

    /// <summary>
    /// Apply the array suffix to the span.
    /// </summary>
    /// <param name="span">The span into which to write the array suffix.</param>
    /// <returns>The number of characters written.</returns>
    public static int ApplyArraySuffix(Span<char> span)
    {
        ArraySuffix.CopyTo(span);
        return ArraySuffix.Length;
    }

    private static void ThrowIdentifierTooLongException(TypeDeclaration typeDeclaration)
    {
        throw new InvalidOperationException(
            $"""
            A class name or other identifier can be no longer than 512 characters (CS0645).
                    
            Consider setting a specific named type on the Options for the schema at {typeDeclaration.LocatedSchema.Location}.
            """);
    }

    private static int FixCasing(Span<char> chars, bool capitalizeFirst, bool lowerCaseFirst)
    {
        int setIndex = 0;
        bool capitalizeNext = capitalizeFirst;
        bool lowercaseNext = lowerCaseFirst;
        bool lastUppercase = false;
        for (int readIndex = 0; readIndex < chars.Length; readIndex++)
        {
            if (char.IsLetter(chars[readIndex]))
            {
                if (capitalizeNext)
                {
                    chars[setIndex] = char.ToUpperInvariant(chars[readIndex]);
                    lastUppercase = true;
                }
                else if (lowercaseNext)
                {
                    chars[setIndex] = char.ToLowerInvariant(chars[readIndex]);
                    lowercaseNext = false;
                    lastUppercase = false;
                }
                else
                {
                    if (char.ToUpperInvariant(chars[readIndex]) == chars[readIndex])
                    {
                        if (lastUppercase &&
                            (readIndex == chars.Length - 1 || // We are at the end of the string
                            !char.IsLetter(chars[readIndex + 1]) || // The next character *isn't* a letter or the next character is also uppercase
                            chars[readIndex + 1] == char.ToUpperInvariant(chars[readIndex + 1])))
                        {
                            lastUppercase = true;
                            chars[setIndex] = char.ToLowerInvariant(chars[readIndex]);
                            setIndex++;
                            continue;
                        }

                        lastUppercase = true;
                    }
                    else
                    {
                        lastUppercase = false;
                    }

                    chars[setIndex] = chars[readIndex];
                }

                capitalizeNext = false;
                setIndex++;
            }
            else if (char.IsDigit(chars[readIndex]))
            {
                chars[setIndex] = chars[readIndex];

                // We do not capitalize the next character we find after a run of digits
                capitalizeNext = false;
                setIndex++;
            }
            else
            {
                // Don't increment the set index; we just skip this non-letter-or-digit character
                // but start a new word, if we have already written a character.
                // (If we haven't then capitalizeNext will remain set according to our capitalizeFirst request.)
                if (setIndex > 0)
                {
                    capitalizeNext = true;
                }
            }
        }

        return setIndex;
    }
}