// <copyright file="CodeGenStringHelpers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Shared string escaping, unescaping, and literal formatting helpers
/// for code generation engines. This file is linked into both the CG
/// libraries and their corresponding Roslyn source generators and must
/// therefore have zero external dependencies.
/// </summary>
internal static class CodeGenStringHelpers
{
    /// <summary>
    /// Escapes a .NET string for embedding inside a C# <c>"..."</c> or
    /// <c>"..."u8</c> string literal. Handles all characters that require
    /// escaping, including control characters and the Unicode line
    /// terminators U+2028 / U+2029.
    /// </summary>
    /// <param name="value">The string value to escape.</param>
    /// <returns>The escaped content (without surrounding quotes).</returns>
    internal static string EscapeCSharpStringLiteral(string value)
    {
        StringBuilder sb = new(value.Length + 4);
        foreach (char ch in value)
        {
            switch (ch)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\0': sb.Append("\\0"); break;
                case '\a': sb.Append("\\a"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\v': sb.Append("\\v"); break;
                default:
                    if (ch < 0x20 || ch == '\u2028' || ch == '\u2029')
                    {
                        sb.Append($"\\u{(int)ch:X4}");
                    }
                    else
                    {
                        sb.Append(ch);
                    }

                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Escapes a .NET string for use as content inside a JSON string
    /// (i.e. between the surrounding <c>"</c> delimiters). Use this when
    /// building JSON text that will be parsed at runtime by
    /// <c>JsonElement.ParseValue</c>.
    /// </summary>
    /// <remarks>
    /// This differs from <see cref="EscapeCSharpStringLiteral"/> in that it
    /// only uses escape sequences defined by the JSON specification (RFC 8259).
    /// In particular, <c>\0</c>, <c>\a</c>, and <c>\v</c> are not valid JSON
    /// escapes and are emitted as <c>\uXXXX</c> instead.
    /// </remarks>
    /// <param name="value">The string value to escape.</param>
    /// <returns>The escaped content (without surrounding quotes).</returns>
    internal static string EscapeJsonStringContent(string value)
    {
        StringBuilder sb = new(value.Length);
        foreach (char ch in value)
        {
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    if (ch < ' ')
                    {
                        sb.Append($"\\u{(int)ch:X4}");
                    }
                    else
                    {
                        sb.Append(ch);
                    }

                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Unescapes a raw JSON string literal (including the surrounding double
    /// quotes) to produce the actual string value. Handles all standard JSON
    /// escape sequences: <c>\\</c>, <c>\"</c>, <c>\/</c>, <c>\b</c>,
    /// <c>\f</c>, <c>\n</c>, <c>\r</c>, <c>\t</c>, and <c>\uXXXX</c>.
    /// </summary>
    /// <param name="rawJson">
    /// A JSON string literal including its surrounding <c>"</c> characters,
    /// e.g. <c>"hello\nworld"</c>.
    /// </param>
    /// <returns>The unescaped string value.</returns>
    internal static string UnescapeJsonString(string rawJson)
    {
        // Strip surrounding double quotes
        string content = rawJson.Substring(1, rawJson.Length - 2);

        if (content.IndexOf('\\') < 0)
        {
            return content;
        }

        StringBuilder sb = new(content.Length);
        for (int i = 0; i < content.Length; i++)
        {
            if (content[i] == '\\' && i + 1 < content.Length)
            {
                char next = content[i + 1];
                switch (next)
                {
                    case '"': sb.Append('"'); i++; break;
                    case '\\': sb.Append('\\'); i++; break;
                    case '/': sb.Append('/'); i++; break;
                    case 'b': sb.Append('\b'); i++; break;
                    case 'f': sb.Append('\f'); i++; break;
                    case 'n': sb.Append('\n'); i++; break;
                    case 'r': sb.Append('\r'); i++; break;
                    case 't': sb.Append('\t'); i++; break;
                    case 'u':
                        if (i + 6 <= content.Length)
                        {
#if NETSTANDARD2_0
                            string hex = content.Substring(i + 2, 4);
#else
                            ReadOnlySpan<char> hex = content.AsSpan(i + 2, 4);
#endif
                            if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int codePoint))
                            {
                                sb.Append((char)codePoint);
                                i += 5;
                            }
                            else
                            {
                                sb.Append(content[i]);
                            }
                        }
                        else
                        {
                            sb.Append(content[i]);
                        }

                        break;

                    default:
                        sb.Append(content[i]);
                        break;
                }
            }
            else
            {
                sb.Append(content[i]);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats a byte array as a C# byte-array literal:
    /// <c>new byte[] { 0xAB, 0xCD, ... }</c>.
    /// </summary>
    /// <param name="bytes">The bytes to format.</param>
    /// <returns>A C# byte-array literal string.</returns>
    internal static string FormatUtf8ByteArrayLiteral(byte[] bytes)
    {
        StringBuilder sb = new(bytes.Length * 4 + 4);
        sb.Append("new byte[] { ");
        for (int i = 0; i < bytes.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append("0x");
            sb.Append(bytes[i].ToString("X2"));
        }

        sb.Append(" }");
        return sb.ToString();
    }

    /// <summary>
    /// Formats a string as a C# UTF-8 string literal: <c>"content"u8</c>.
    /// The content is escaped using <see cref="EscapeCSharpStringLiteral"/>.
    /// </summary>
    /// <param name="content">The string content to encode.</param>
    /// <returns>A C# UTF-8 string literal.</returns>
    internal static string FormatUtf8StringLiteral(string content)
    {
        return $"\"{EscapeCSharpStringLiteral(content)}\"u8";
    }
}