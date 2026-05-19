// <copyright file="JsonStreamReader.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Reads a stream of newline-delimited JSON (NDJSON) or Server-Sent Events (SSE)
/// and yields strongly-typed items parsed from each JSON payload.
/// </summary>
public static class JsonStreamReader
{
    /// <summary>
    /// Asynchronously reads items from a stream containing newline-delimited JSON or SSE data.
    /// </summary>
    /// <typeparam name="T">The element type to parse each JSON payload into.</typeparam>
    /// <param name="stream">The source stream (NDJSON or SSE).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An async enumerable of parsed items.</returns>
    /// <remarks>
    /// <para>Supported formats:</para>
    /// <list type="bullet">
    ///   <item><description>NDJSON: each line is a complete JSON value.</description></item>
    ///   <item><description>SSE: lines prefixed with <c>data: </c> contain JSON payloads;
    ///   comment lines (starting with <c>:</c>) and metadata lines (<c>event:</c>, <c>id:</c>,
    ///   <c>retry:</c>) are skipped.</description></item>
    /// </list>
    /// <para>Empty lines are skipped. Each yielded item is a standalone value with no
    /// shared disposal requirements.</para>
    /// </remarks>
    public static async IAsyncEnumerable<T> ReadItemsAsync<T>(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
        where T : struct, IJsonElement<T>
    {
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);

        while (!cancellationToken.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                yield break;
            }

            ReadOnlySpan<char> span = line.AsSpan();

            // Skip empty lines.
            if (span.IsEmpty)
            {
                continue;
            }

            // SSE comment lines start with ':'.
            if (span[0] == ':')
            {
                continue;
            }

            // SSE data lines: strip "data:" or "data: " prefix.
            if (span.StartsWith("data:".AsSpan()))
            {
                span = span[5..];
                if (span.Length > 0 && span[0] == ' ')
                {
                    span = span[1..];
                }

                // Empty data field (e.g. heartbeat) — skip.
                if (span.IsEmpty)
                {
                    continue;
                }
            }
            else if (!IsJsonStart(span))
            {
                // Skip SSE metadata lines (event:, id:, retry:, etc.)
                continue;
            }

            // Parse the JSON payload into the target type.
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(span.ToString());
            yield return JsonElementHelpers.ParseValue<T>(utf8Bytes);
        }
    }

    private static bool IsJsonStart(ReadOnlySpan<char> span)
    {
        return span[0] is '{' or '[' or '"' or '-' or 't' or 'f' or 'n'
            || char.IsDigit(span[0]);
    }
}