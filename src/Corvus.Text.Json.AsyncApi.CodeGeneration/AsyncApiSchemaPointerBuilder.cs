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
    /// Builds a pointer to a message payload schema in a 3.x channel.
    /// </summary>
    /// <param name="channelName">The channel name.</param>
    /// <param name="messageName">The message name within the channel.</param>
    /// <returns>The JSON Pointer string.</returns>
    public static string ChannelMessagePayload(string channelName, string messageName)
    {
        return $"#/channels/{Escape(channelName)}/messages/{Escape(messageName)}/payload";
    }

    /// <summary>
    /// Builds a pointer to a message headers schema in a 3.x channel.
    /// </summary>
    /// <param name="channelName">The channel name.</param>
    /// <param name="messageName">The message name within the channel.</param>
    /// <returns>The JSON Pointer string.</returns>
    public static string ChannelMessageHeaders(string channelName, string messageName)
    {
        return $"#/channels/{Escape(channelName)}/messages/{Escape(messageName)}/headers";
    }

    /// <summary>
    /// Builds a pointer to a component schema.
    /// </summary>
    /// <param name="schemaName">The schema name within components.</param>
    /// <returns>The JSON Pointer string.</returns>
    public static string ComponentSchema(string schemaName)
    {
        return $"#/components/schemas/{Escape(schemaName)}";
    }

    /// <summary>
    /// Builds a pointer to a component message payload.
    /// </summary>
    /// <param name="messageName">The message name within components.</param>
    /// <returns>The JSON Pointer string.</returns>
    public static string ComponentMessagePayload(string messageName)
    {
        return $"#/components/messages/{Escape(messageName)}/payload";
    }

    /// <summary>
    /// Builds a pointer to a component message headers.
    /// </summary>
    /// <param name="messageName">The message name within components.</param>
    /// <returns>The JSON Pointer string.</returns>
    public static string ComponentMessageHeaders(string messageName)
    {
        return $"#/components/messages/{Escape(messageName)}/headers";
    }

    /// <summary>
    /// Builds a pointer to a 2.x channel publish message payload.
    /// </summary>
    /// <param name="channelName">The channel address/name.</param>
    /// <returns>The JSON Pointer string.</returns>
    public static string ChannelPublishPayload(string channelName)
    {
        return $"#/channels/{Escape(channelName)}/publish/message/payload";
    }

    /// <summary>
    /// Builds a pointer to a 2.x channel subscribe message payload.
    /// </summary>
    /// <param name="channelName">The channel address/name.</param>
    /// <returns>The JSON Pointer string.</returns>
    public static string ChannelSubscribePayload(string channelName)
    {
        return $"#/channels/{Escape(channelName)}/subscribe/message/payload";
    }

    /// <summary>
    /// Escapes a JSON Pointer reference token (RFC 6901).
    /// </summary>
    private static string Escape(string token)
    {
        return token.Replace("~", "~0").Replace("/", "~1");
    }
}