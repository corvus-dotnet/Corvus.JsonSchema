// <copyright file="StyleValueSplitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Depth-aware comma splitting for OpenAPI style-serialized values.
/// </summary>
/// <remarks>
/// <para>
/// OpenAPI style serialization uses commas to separate array elements and
/// object key-value pairs. When a value is itself JSON-encoded (for deeply
/// nested schemas where style serialization is undefined), the JSON may
/// contain commas inside braces, brackets, or quoted strings that must not
/// be treated as separators.
/// </para>
/// <para>
/// This helper finds the next comma at structural depth zero, correctly
/// skipping commas inside <c>{ }</c>, <c>[ ]</c>, and <c>" "</c>.
/// </para>
/// </remarks>
public static class StyleValueSplitter
{
    /// <summary>
    /// Finds the index of the next comma separator at depth zero,
    /// respecting JSON structural nesting and quoted strings.
    /// </summary>
    /// <param name="value">The remaining value to scan.</param>
    /// <returns>
    /// The index of the next depth-zero comma, or <c>-1</c> if none found.
    /// </returns>
    public static int NextSeparator(ReadOnlySpan<char> value)
    {
        int depth = 0;
        bool inString = false;
        bool escaped = false;

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            switch (c)
            {
                case '{':
                case '[':
                    depth++;
                    break;
                case '}':
                case ']':
                    depth--;
                    break;
                case ',' when depth == 0:
                    return i;
            }
        }

        return -1;
    }
}