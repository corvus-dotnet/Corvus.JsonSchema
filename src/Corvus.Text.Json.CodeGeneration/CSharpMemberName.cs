// <copyright file="CSharpMemberName.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// The name of a C# member.
/// </summary>
public class CSharpMemberName(
    string fullyQualifiedScope,
    string baseName,
    Casing casing,
    string? prefix = null,
    string? suffix = null)
    : MemberName(fullyQualifiedScope, baseName, casing, prefix, suffix)
{
    private static ReadOnlySpan<char> CamelPrefix => "value".AsSpan();

    private static ReadOnlySpan<char> FallbackName => "Value".AsSpan();

    private static ReadOnlySpan<char> PascalPrefix => "Value".AsSpan();

    /// <inheritdoc/>
    public override string BuildName()
    {
        string baseName = string.IsNullOrWhiteSpace(BaseName) ? "Value" : BaseName;

        if (baseName.Length == 1 && !char.IsLetter(baseName[0]))
        {
            baseName = TranslateNonLetterToWord(baseName[0]) ?? "Value";
        }

        string prefix = string.IsNullOrWhiteSpace(Prefix) ? string.Empty : Prefix;
        string suffix = string.IsNullOrWhiteSpace(Suffix) ? string.Empty : Suffix;

        ReadOnlySpan<char> leadingDigitPrefix = Casing == Casing.PascalCase ? PascalPrefix : CamelPrefix;

        int bufferLength = Formatting.GetBufferLength(baseName.Length + prefix.Length + suffix.Length, leadingDigitPrefix, FallbackName);

        Span<char> buffer = stackalloc char[bufferLength];

        int totalLength = 0;

        // Copy the components into the buffer
        if (Casing == Casing.Unmodified)
        {
            totalLength = prefix.Length + baseName.Length + suffix.Length;
            prefix.AsSpan().CopyTo(buffer);
            baseName.AsSpan().CopyTo(buffer[prefix.Length..]);
            suffix.AsSpan().CopyTo(buffer[(prefix.Length + baseName.Length)..]);
        }
        else
        {
            if (Casing == Casing.PascalCase)
            {
                int written = 0;
                if (prefix.Length > 0)
                {
                    prefix.AsSpan().CopyTo(buffer);
                    written = Formatting.ToPascalCase(buffer[..prefix.Length]);
                    totalLength += written;
                }

                baseName.AsSpan().CopyTo(buffer[totalLength..]);
                written = Formatting.ToPascalCase(buffer.Slice(totalLength, baseName.Length));
                totalLength += written;
            }
            else if (Casing == Casing.CamelCase)
            {
                int written = 0;
                if (prefix.Length > 0)
                {
                    prefix.AsSpan().CopyTo(buffer);
                    written = Formatting.ToCamelCase(buffer[..prefix.Length]);
                    totalLength += written;

                    baseName.AsSpan().CopyTo(buffer[totalLength..]);
                    written = Formatting.ToPascalCase(buffer.Slice(totalLength, baseName.Length));
                    totalLength += written;
                }
                else
                {
                    baseName.AsSpan().CopyTo(buffer[totalLength..]);
                    written = Formatting.ToCamelCase(buffer.Slice(totalLength, baseName.Length));
                    totalLength += written;
                }
            }

            if (suffix.Length > 0)
            {
                suffix.AsSpan().CopyTo(buffer[totalLength..]);
                int written = Formatting.ToPascalCase(buffer.Slice(totalLength, suffix.Length));
                totalLength += written;
            }

            totalLength = Formatting.FixReservedWords(buffer, totalLength, leadingDigitPrefix, FallbackName);

            if (totalLength == 0)
            {
                FallbackName.CopyTo(buffer);
                totalLength = FallbackName.Length;
                if (Casing == Casing.CamelCase)
                {
                    // We are already in PascalCase, so no need to translate for that.
                    totalLength = Formatting.ToCamelCase(buffer[..totalLength]);
                }
            }
        }

        return buffer[..totalLength].ToString();
    }

    private static string? TranslateNonLetterToWord(char v)
    {
        // NOTE: This method is only called when baseName.Length == 1 && !char.IsLetter(baseName[0]).
        // Characters where char.IsLetter() returns true can never reach this switch and have
        // been intentionally excluded (e.g. all of \u00C0-\u00FF except \u00D7 and \u00F7).
        // The space character is also excluded because string.IsNullOrWhiteSpace catches it first.
        return v switch
        {
            '!' => "excl",
            '"' => "quot",
            '#' => "num",
            '$' => "dollar",
            '%' => "percent",
            '&' => "amp",
            '\'' => "apos",
            '(' => "lpar",
            ')' => "rpar",
            '*' => "ast",
            '+' => "plus",
            ',' => "comma",
            '-' => "minus",
            '.' => "period",
            '/' => "sol",
            '0' => "zero",
            '1' => "one",
            '2' => "two",
            '3' => "three",
            '4' => "four",
            '5' => "five",
            '6' => "six",
            '7' => "seven",
            '8' => "eight",
            '9' => "nine",
            ':' => "colon",
            ';' => "semi",
            '<' => "lt",
            '=' => "equals",
            '>' => "gt",
            '?' => "quest",
            '@' => "commat",
            '[' => "lsqb",
            '\\' => "bsol",
            ']' => "rsqb",
            '^' => "caret",
            '_' => "lowbar",
            '`' => "grave",
            '{' => "lcub",
            '|' => "verbar",
            '}' => "rcub",
            '~' => "tilde",
            '€' => "euro",
            '‚' => "sbquo",
            '„' => "bdquo",
            '…' => "hellip",
            '†' => "dagger",
            '‡' => "Dagger",
            '‰' => "permil",
            '‹' => "lsaquo",
            '‘' => "lsquo",
            '’' => "rsquo",
            '“' => "ldquo",
            '”' => "rdquo",
            '•' => "bull",
            '–' => "ndash",
            '—' => "mdash",
            '˜' => "tilde",
            '™' => "trade",
            '›' => "rsaquo",
            '¡' => "iexcl",
            '¢' => "cent",
            '£' => "pound",
            '¤' => "curren",
            '¥' => "yen",
            '¦' => "brvbar",
            '§' => "sect",
            '¨' => "uml",
            '©' => "copy",
            '«' => "laquo",
            '¬' => "not",
            '®' => "reg",
            '¯' => "macr",
            '°' => "deg",
            '±' => "plusmn",
            '²' => "sup2",
            '³' => "sup3",
            '´' => "acute",
            '¶' => "para",
            '·' => "middot",
            '¸' => "cedil",
            '¹' => "sup1",
            '»' => "raquo",
            '¼' => "frac14",
            '½' => "frac12",
            '¾' => "frac34",
            '¿' => "iquest",
            '×' => "times",
            '÷' => "divide",
            _ => null,
        };
    }
}