// <copyright file="SchemaLocationEncoding.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Encodes a schema-location path (a JSON Pointer in URI-fragment form) so that it is a valid URI
/// fragment when emitted as a <c>JsonSchemaEvaluation.TryCopyPath</c> literal.
/// </summary>
/// <remarks>
/// <para>
/// Schema locations are built as JSON Pointers (segments escaped with <c>~0</c>/<c>~1</c>) carried in a
/// <c>#</c>-prefixed fragment. A JSON Pointer reference token may legally contain characters that are not
/// valid in a URI fragment — e.g. a <c>patternProperties</c> key such as <c>^x-</c> — so the
/// <c>#/patternProperties/^x-</c> form is a valid pointer but an invalid URI. The runtime path encoder
/// (<c>TryCopyPath</c>) requires a canonical URI, so the generator must percent-encode the URI-unsafe
/// bytes when it materialises the location as the URI literal.
/// </para>
/// <para>
/// This deliberately runs only at the emission boundary, over the final assembled path, so the internal
/// path representations (used for resolution and scope) are left untouched and a value is never
/// double-encoded (JSON-Pointer escaping uses <c>~</c>, never <c>%</c>).
/// </para>
/// </remarks>
public static class SchemaLocationEncoding
{
    private const string HexDigits = "0123456789ABCDEF";

    /// <summary>
    /// Percent-encodes the URI-unsafe bytes of a schema-location path, preserving the structural leading
    /// <c>#</c> and the <c>/</c> separators (and the <c>~0</c>/<c>~1</c> JSON-Pointer escapes).
    /// </summary>
    /// <param name="path">The schema-location path (e.g. <c>#/patternProperties/^x-</c>).</param>
    /// <returns>The path as a valid URI fragment (e.g. <c>#/patternProperties/%5Ex-</c>).</returns>
    public static string EncodeAsUriFragment(string path)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(path);

        bool needsEncoding = false;
        for (int i = 0; i < bytes.Length; i++)
        {
            if (!IsFragmentSafe(bytes[i], i == 0))
            {
                needsEncoding = true;
                break;
            }
        }

        if (!needsEncoding)
        {
            return path;
        }

        StringBuilder builder = new(bytes.Length + 16);
        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            if (IsFragmentSafe(b, i == 0))
            {
                builder.Append((char)b);
            }
            else
            {
                builder.Append('%');
                builder.Append(HexDigits[b >> 4]);
                builder.Append(HexDigits[b & 0xF]);
            }
        }

        return builder.ToString();
    }

    // RFC 3986: fragment = *( pchar / "/" / "?" ); pchar = unreserved / pct-encoded / sub-delims / ":" / "@".
    // The leading '#' is the fragment marker and is kept; '~' (unreserved) is kept so the JSON-Pointer
    // '~0'/'~1' escapes survive.
    private static bool IsFragmentSafe(byte b, bool isLeading)
    {
        if (isLeading && b == (byte)'#')
        {
            return true;
        }

        return b is
            (>= (byte)'A' and <= (byte)'Z') or
            (>= (byte)'a' and <= (byte)'z') or
            (>= (byte)'0' and <= (byte)'9') or
            (byte)'-' or (byte)'.' or (byte)'_' or (byte)'~' or                                          // unreserved
            (byte)'!' or (byte)'$' or (byte)'&' or (byte)'\'' or (byte)'(' or (byte)')' or                // sub-delims
            (byte)'*' or (byte)'+' or (byte)',' or (byte)';' or (byte)'=' or
            (byte)':' or (byte)'@' or (byte)'/' or (byte)'?';                                            // pchar extras + "/" + "?"
    }
}