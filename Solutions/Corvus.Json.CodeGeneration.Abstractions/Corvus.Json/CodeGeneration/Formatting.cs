// <copyright file="Formatting.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Formatting utilities.
/// </summary>
public static class Formatting
{
    private static readonly ReadOnlyMemory<char> TypePrefix = "Type".AsMemory();

    private static readonly ReadOnlyMemory<char>[] Keywords = new ReadOnlyMemory<char>[]
    {
        "abstract".AsMemory(), "as".AsMemory(), "base".AsMemory(), "bool".AsMemory(),
        "break".AsMemory(), "byte".AsMemory(), "case".AsMemory(), "catch".AsMemory(),
        "char".AsMemory(), "checked".AsMemory(), "class".AsMemory(), "const".AsMemory(),
        "continue".AsMemory(), "decimal".AsMemory(), "default".AsMemory(), "delegate".AsMemory(),
        "do".AsMemory(), "double".AsMemory(), "else".AsMemory(), "enum".AsMemory(),
        "event".AsMemory(), "explicit".AsMemory(), "extern".AsMemory(), "false".AsMemory(),
        "finally".AsMemory(), "fixed".AsMemory(), "float".AsMemory(), "for".AsMemory(),
        "foreach".AsMemory(), "goto".AsMemory(), "if".AsMemory(), "implicit".AsMemory(),
        "in".AsMemory(), "int".AsMemory(), "interface".AsMemory(), "internal".AsMemory(),
        "is".AsMemory(),  "item".AsMemory(), "lock".AsMemory(), "long".AsMemory(), "namespace".AsMemory(),
        "new".AsMemory(), "null".AsMemory(), "object".AsMemory(), "operator".AsMemory(),
        "out".AsMemory(), "override".AsMemory(), "params".AsMemory(), "private".AsMemory(),
        "protected".AsMemory(), "public".AsMemory(), "readonly".AsMemory(), "ref".AsMemory(),
        "return".AsMemory(), "sbyte".AsMemory(), "sealed".AsMemory(), "short".AsMemory(),
        "sizeof".AsMemory(), "stackalloc".AsMemory(), "static".AsMemory(), "string".AsMemory(),
        "struct".AsMemory(), "switch".AsMemory(), "this".AsMemory(), "throw".AsMemory(),
        "true".AsMemory(), "try".AsMemory(), "typeof".AsMemory(), "uint".AsMemory(),
        "ulong".AsMemory(), "unchecked".AsMemory(), "unsafe".AsMemory(), "ushort".AsMemory(),
        "using".AsMemory(), "virtual".AsMemory(), "void".AsMemory(), "volatile".AsMemory(),
        "while".AsMemory(),
    };

    /// <summary>
    /// Escapes a value for embeddeding into a quoted C# string.
    /// </summary>
    /// <param name="value">The value to escape.</param>
    /// <param name="quote">Whether to quote the string.</param>
    /// <returns>The escaped value. This can be inserted into a regular quoted C# string.</returns>
    [return: NotNullIfNotNull("value")]
    public static string? FormatLiteralOrNull(string? value, bool quote)
    {
        return value is null ? null : SymbolDisplay.FormatLiteral(value, quote);
    }

    /// <summary>
    /// Convert the given name to <c>camelCase</c>.
    /// </summary>
    /// <param name="name">The name to convert.</param>
    /// <returns>The name in <c>camelCase</c>.</returns>
    public static ReadOnlySpan<char> ToCamelCaseWithReservedWords(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        // We could possibly do a better job here by using the "in place" access to the underlying
        // string data; however, we'd still end up with a single buffer copy of at least the length
        // of the string data, so I think this is a "reasonable" approach.
        ReadOnlySpan<char> result = FixCasing(name.ToCharArray(), false, true);
        return SubstituteReservedWords(result);
    }

    /// <summary>
    /// Convert the given name to <c>PascalCase</c>.
    /// </summary>
    /// <param name="name">The name to convert.</param>
    /// <returns>The name in <c>PascalCase</c>.</returns>
    public static ReadOnlySpan<char> ToPascalCaseWithReservedWords(string name)
    {
        if (name.Length == 0)
        {
            return name;
        }

        // We could possibly do a better job here by using the "in place" access to the underlying
        // string data; however, we'd still end up with a single buffer copy of at least the length
        // of the string data, so I think this is a "reasonable" approach.
        ReadOnlySpan<char> result = FixCasing(name.ToCharArray(), true, false);
        return SubstituteReservedWords(result);
    }

    /// <summary>
    /// Removes the given prefix.
    /// </summary>
    /// <param name="dotnetTypeName">The type name from which to remove the prefix.</param>
    /// <param name="prefix">The prefix to remove.</param>
    /// <returns>The type name without the prefix.</returns>
    public static ReadOnlySpan<char> RemovePrefix(ReadOnlySpan<char> dotnetTypeName, ReadOnlySpan<char> prefix)
    {
        if (dotnetTypeName.StartsWith(prefix))
        {
            return dotnetTypeName[prefix.Length..];
        }

        return dotnetTypeName;
    }

    private static ReadOnlySpan<char> SubstituteReservedWords(ReadOnlySpan<char> v)
    {
        foreach (ReadOnlyMemory<char> k in Keywords)
        {
            if (k.Span.SequenceEqual(v))
            {
                Span<char> buffer = new char[v.Length + 1];
                buffer[0] = '@';
                v.CopyTo(buffer[1..]);
                return buffer;
            }
        }

        if (v.Length == 0)
        {
            return v;
        }

        if (char.IsDigit(v[0]))
        {
            Span<char> buffer2 = new char[v.Length + TypePrefix.Length];
            TypePrefix.Span.CopyTo(buffer2);
            v.CopyTo(buffer2[TypePrefix.Length..]);
            return buffer2;
        }

        return v;
    }

    private static ReadOnlySpan<char> FixCasing(Span<char> chars, bool capitalizeFirst, bool lowerCaseFirst)
    {
        int setIndex = 0;
        bool capitalizeNext = capitalizeFirst;
        bool lowercaseNext = lowerCaseFirst;
        int uppercasedCount = 0;

        for (int readIndex = 0; readIndex < chars.Length; readIndex++)
        {
            if (char.IsLetter(chars[readIndex]))
            {
                if (capitalizeNext)
                {
                    chars[setIndex] = char.ToUpperInvariant(chars[readIndex]);
                    uppercasedCount++;
                }
                else if (lowercaseNext)
                {
                    chars[setIndex] = char.ToLowerInvariant(chars[readIndex]);
                    uppercasedCount = 0;
                    lowercaseNext = false;
                }
                else
                {
                    if (char.ToUpperInvariant(chars[readIndex]) == chars[readIndex])
                    {
                        uppercasedCount++;
                        if (uppercasedCount > 2)
                        {
                            chars[setIndex] = char.ToLowerInvariant(chars[readIndex]);
                            uppercasedCount = 0;
                            continue;
                        }
                    }
                    else
                    {
                        uppercasedCount = 0;
                    }

                    chars[setIndex] = chars[readIndex];
                }

                capitalizeNext = false;
                setIndex++;
            }
            else if (char.IsDigit(chars[readIndex]))
            {
                chars[setIndex] = chars[readIndex];

                // We capitalize the next character we find after a run of digits
                capitalizeNext = true;
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

        return chars[..setIndex];
    }
}