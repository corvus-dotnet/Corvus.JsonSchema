// <copyright file="AsyncApiSchemaPointerBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;

namespace Corvus.Text.Json.AsyncApi.CodeGeneration;

/// <summary>
/// Builds JSON Pointer strings for schemas reachable from AsyncAPI operations.
/// </summary>
/// <remarks>
/// <para>
/// All pointers use RFC 6901 escaping (<c>~</c> → <c>~0</c>, <c>/</c> → <c>~1</c>).
/// Pointers are constructed in a UTF-8 <see cref="Utf8ValueStringBuilder"/> for
/// efficiency and transcoded once to <see langword="string"/> at the end.
/// </para>
/// <para>
/// In AsyncAPI 3.x, schemas are found at:
/// <list type="bullet">
/// <item><c>#/channels/{name}/messages/{msg}/payload</c></item>
/// <item><c>#/channels/{name}/messages/{msg}/headers</c></item>
/// <item><c>#/components/schemas/{name}</c></item>
/// <item><c>#/components/messages/{name}/payload</c></item>
/// <item><c>#/components/messages/{name}/headers</c></item>
/// </list>
/// </para>
/// <para>
/// In AsyncAPI 2.x, schemas are found at:
/// <list type="bullet">
/// <item><c>#/channels/{name}/publish/message/payload</c></item>
/// <item><c>#/channels/{name}/subscribe/message/payload</c></item>
/// <item><c>#/components/schemas/{name}</c></item>
/// <item><c>#/components/messages/{name}/payload</c></item>
/// </list>
/// </para>
/// </remarks>
public static class AsyncApiSchemaPointerBuilder
{
    /// <summary>
    /// Builds a pointer to a message payload schema in a 3.x channel:
    /// <c>#/channels/{name}/messages/{msg}/payload</c>.
    /// </summary>
    /// <param name="channelNameUtf8">The UTF-8 channel name from the channels map.</param>
    /// <param name="messageNameUtf8">The UTF-8 message name within the channel.</param>
    /// <returns>The JSON Pointer string.</returns>
    public static string ChannelMessagePayload(ReadOnlySpan<byte> channelNameUtf8, ReadOnlySpan<byte> messageNameUtf8)
    {
        Span<byte> initialBuffer = stackalloc byte[256];
        Utf8ValueStringBuilder sb = new(initialBuffer);

        try
        {
            sb.Append("#/channels/"u8);
            AppendEncodedSegment(ref sb, channelNameUtf8);
            sb.Append("/messages/"u8);
            AppendEncodedSegment(ref sb, messageNameUtf8);
            sb.Append("/payload"u8);

            return Encoding.UTF8.GetString(sb.AsSpan());
        }
        finally
        {
            sb.Dispose();
        }
    }

    /// <summary>
    /// Builds a pointer to a message headers schema in a 3.x channel:
    /// <c>#/channels/{name}/messages/{msg}/headers</c>.
    /// </summary>
    /// <param name="channelNameUtf8">The UTF-8 channel name from the channels map.</param>
    /// <param name="messageNameUtf8">The UTF-8 message name within the channel.</param>
    /// <returns>The JSON Pointer string.</returns>
    public static string ChannelMessageHeaders(ReadOnlySpan<byte> channelNameUtf8, ReadOnlySpan<byte> messageNameUtf8)
    {
        Span<byte> initialBuffer = stackalloc byte[256];
        Utf8ValueStringBuilder sb = new(initialBuffer);

        try
        {
            sb.Append("#/channels/"u8);
            AppendEncodedSegment(ref sb, channelNameUtf8);
            sb.Append("/messages/"u8);
            AppendEncodedSegment(ref sb, messageNameUtf8);
            sb.Append("/headers"u8);

            return Encoding.UTF8.GetString(sb.AsSpan());
        }
        finally
        {
            sb.Dispose();
        }
    }

    /// <summary>
    /// Builds a pointer to a component schema:
    /// <c>#/components/schemas/{name}</c>.
    /// </summary>
    /// <param name="schemaNameUtf8">The UTF-8 schema name within components.</param>
    /// <returns>The JSON Pointer string.</returns>
    public static string ComponentSchema(ReadOnlySpan<byte> schemaNameUtf8)
    {
        Span<byte> initialBuffer = stackalloc byte[128];
        Utf8ValueStringBuilder sb = new(initialBuffer);

        try
        {
            sb.Append("#/components/schemas/"u8);
            AppendEncodedSegment(ref sb, schemaNameUtf8);

            return Encoding.UTF8.GetString(sb.AsSpan());
        }
        finally
        {
            sb.Dispose();
        }
    }

    /// <summary>
    /// Builds a pointer to a component message payload:
    /// <c>#/components/messages/{name}/payload</c>.
    /// </summary>
    /// <param name="messageNameUtf8">The UTF-8 message name within components.</param>
    /// <returns>The JSON Pointer string.</returns>
    public static string ComponentMessagePayload(ReadOnlySpan<byte> messageNameUtf8)
    {
        Span<byte> initialBuffer = stackalloc byte[128];
        Utf8ValueStringBuilder sb = new(initialBuffer);

        try
        {
            sb.Append("#/components/messages/"u8);
            AppendEncodedSegment(ref sb, messageNameUtf8);
            sb.Append("/payload"u8);

            return Encoding.UTF8.GetString(sb.AsSpan());
        }
        finally
        {
            sb.Dispose();
        }
    }

    /// <summary>
    /// Builds a pointer to a component message headers:
    /// <c>#/components/messages/{name}/headers</c>.
    /// </summary>
    /// <param name="messageNameUtf8">The UTF-8 message name within components.</param>
    /// <returns>The JSON Pointer string.</returns>
    public static string ComponentMessageHeaders(ReadOnlySpan<byte> messageNameUtf8)
    {
        Span<byte> initialBuffer = stackalloc byte[128];
        Utf8ValueStringBuilder sb = new(initialBuffer);

        try
        {
            sb.Append("#/components/messages/"u8);
            AppendEncodedSegment(ref sb, messageNameUtf8);
            sb.Append("/headers"u8);

            return Encoding.UTF8.GetString(sb.AsSpan());
        }
        finally
        {
            sb.Dispose();
        }
    }

    /// <summary>
    /// Builds a pointer to a 2.x channel publish message payload:
    /// <c>#/channels/{name}/publish/message/payload</c>.
    /// </summary>
    /// <param name="channelNameUtf8">The UTF-8 channel address/name.</param>
    /// <returns>The JSON Pointer string.</returns>
    public static string ChannelPublishPayload(ReadOnlySpan<byte> channelNameUtf8)
    {
        Span<byte> initialBuffer = stackalloc byte[256];
        Utf8ValueStringBuilder sb = new(initialBuffer);

        try
        {
            sb.Append("#/channels/"u8);
            AppendEncodedSegment(ref sb, channelNameUtf8);
            sb.Append("/publish/message/payload"u8);

            return Encoding.UTF8.GetString(sb.AsSpan());
        }
        finally
        {
            sb.Dispose();
        }
    }

    /// <summary>
    /// Builds a pointer to a 2.x channel subscribe message payload:
    /// <c>#/channels/{name}/subscribe/message/payload</c>.
    /// </summary>
    /// <param name="channelNameUtf8">The UTF-8 channel address/name.</param>
    /// <returns>The JSON Pointer string.</returns>
    public static string ChannelSubscribePayload(ReadOnlySpan<byte> channelNameUtf8)
    {
        Span<byte> initialBuffer = stackalloc byte[256];
        Utf8ValueStringBuilder sb = new(initialBuffer);

        try
        {
            sb.Append("#/channels/"u8);
            AppendEncodedSegment(ref sb, channelNameUtf8);
            sb.Append("/subscribe/message/payload"u8);

            return Encoding.UTF8.GetString(sb.AsSpan());
        }
        finally
        {
            sb.Dispose();
        }
    }

    /// <summary>
    /// Appends a UTF-8 segment with RFC 6901 JSON Pointer escaping (~ → ~0, / → ~1).
    /// </summary>
    /// <param name="sb">The string builder.</param>
    /// <param name="segment">The raw UTF-8 segment to encode.</param>
    internal static void AppendEncodedSegment(
        ref Utf8ValueStringBuilder sb,
        ReadOnlySpan<byte> segment)
    {
        while (segment.Length > 0)
        {
            int specialIndex = segment.IndexOfAny((byte)'~', (byte)'/');
            if (specialIndex < 0)
            {
                sb.Append(segment);
                break;
            }

            if (specialIndex > 0)
            {
                sb.Append(segment[..specialIndex]);
            }

            if (segment[specialIndex] == (byte)'~')
            {
                sb.Append("~0"u8);
            }
            else
            {
                sb.Append("~1"u8);
            }

            segment = segment[(specialIndex + 1)..];
        }
    }
}