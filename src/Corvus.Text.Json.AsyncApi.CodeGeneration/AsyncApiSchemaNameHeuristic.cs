// <copyright file="AsyncApiSchemaNameHeuristic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.CodeGeneration;

namespace Corvus.Text.Json.AsyncApi.CodeGeneration;

/// <summary>
/// A naming heuristic that derives contextual type names from AsyncAPI JSON Pointer fragments.
/// </summary>
/// <remarks>
/// <para>
/// When inline schemas are used in AsyncAPI specs (rather than <c>$ref</c>), the default
/// <see cref="SubschemaNameHeuristic"/> may produce generic names. This heuristic recognises
/// AsyncAPI 3.x JSON Pointer structure and derives names from the channel, message, and
/// schema position.
/// </para>
/// <para>
/// Supported pointer patterns:
/// <list type="bullet">
/// <item><c>#/components/schemas/{name}</c> → <c>{Name}</c></item>
/// <item><c>#/components/messages/{name}/payload</c> → <c>{Name}Payload</c></item>
/// <item><c>#/components/messages/{name}/headers</c> → <c>{Name}Headers</c></item>
/// <item><c>#/channels/{name}/messages/{msg}/payload</c> → <c>{Msg}Payload</c></item>
/// <item><c>#/channels/{name}/messages/{msg}/headers</c> → <c>{Msg}Headers</c></item>
/// </list>
/// </para>
/// <para>
/// Register this heuristic on the <see cref="CSharpLanguageProvider"/> before calling
/// <c>GenerateCodeUsing</c>. Its priority (500) must be lower than
/// <see cref="BaseSchemaNameHeuristic"/> (1000) to take precedence.
/// </para>
/// </remarks>
public sealed class AsyncApiSchemaNameHeuristic : INameHeuristicBeforeSubschema
{
    private AsyncApiSchemaNameHeuristic()
    {
    }

    /// <summary>
    /// Gets a singleton instance of the <see cref="AsyncApiSchemaNameHeuristic"/>.
    /// </summary>
    public static AsyncApiSchemaNameHeuristic Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsOptional => false;

    /// <inheritdoc/>
    public uint Priority => 500;

    /// <inheritdoc/>
    public bool TryGetName(
        ILanguageProvider languageProvider,
        TypeDeclaration typeDeclaration,
        JsonReferenceBuilder reference,
        Span<char> typeNameBuffer,
        out int written)
    {
        written = 0;

        if (!reference.HasFragment)
        {
            return false;
        }

        ReadOnlySpan<char> fragment = reference.Fragment;

        // The fragment is the JSON Pointer (without leading #).
        // AsyncAPI pointers start with /components/ or /channels/.
        if (fragment.Length == 0 || fragment[0] != '/')
        {
            return false;
        }

        if (!TryParseAsyncApiFragment(fragment, out AsyncApiSchemaContext ctx))
        {
            return false;
        }

        // Build the name from the parsed context
        Span<char> workBuffer = stackalloc char[Formatting.MaxIdentifierLength];
        int pos = 0;

        pos += FormatName(ctx, workBuffer[pos..]);

        if (pos == 0)
        {
            return false;
        }

        written = Formatting.FormatTypeNameComponent(typeDeclaration, workBuffer[..pos], typeNameBuffer);

        if (typeDeclaration.CollidesWithParent(typeNameBuffer[..written]))
        {
            written = Formatting.ApplyStandardSuffix(typeDeclaration, typeNameBuffer, typeNameBuffer[..written]);
        }

        return true;
    }

    private static bool TryParseAsyncApiFragment(
        ReadOnlySpan<char> pointer,
        out AsyncApiSchemaContext ctx)
    {
        ctx = default;

        // Pattern: /components/schemas/{name}
        if (pointer.StartsWith("/components/schemas/".AsSpan()))
        {
            ReadOnlySpan<char> name = pointer["/components/schemas/".Length..];
            if (name.Length == 0 || name.Contains('/'))
            {
                return false;
            }

            ctx.Name = name;
            ctx.Position = SchemaPosition.ComponentSchema;
            return true;
        }

        // Pattern: /components/messages/{name}/payload or /headers
        if (pointer.StartsWith("/components/messages/".AsSpan()))
        {
            ReadOnlySpan<char> remainder = pointer["/components/messages/".Length..];
            int nextSlash = remainder.IndexOf('/');
            if (nextSlash < 0)
            {
                return false;
            }

            ctx.Name = remainder[..nextSlash];
            ReadOnlySpan<char> suffix = remainder[(nextSlash + 1)..];

            if (suffix.SequenceEqual("payload".AsSpan()))
            {
                ctx.Position = SchemaPosition.ComponentMessagePayload;
                return true;
            }

            if (suffix.SequenceEqual("headers".AsSpan()))
            {
                ctx.Position = SchemaPosition.ComponentMessageHeaders;
                return true;
            }

            return false;
        }

        // Pattern: /channels/{name}/messages/{msg}/payload or /headers
        if (pointer.StartsWith("/channels/".AsSpan()))
        {
            ReadOnlySpan<char> remainder = pointer["/channels/".Length..];

            // Find the end of the channel name segment (may contain ~1 escapes)
            int messagesStart = FindSegment(remainder, "messages");
            if (messagesStart < 0)
            {
                return false;
            }

            ctx.ChannelName = remainder[..messagesStart];
            remainder = remainder[(messagesStart + 1 + "messages".Length + 1)..]; // skip "messages/"

            // Extract message name
            int nextSlash = remainder.IndexOf('/');
            if (nextSlash < 0)
            {
                return false;
            }

            ctx.Name = remainder[..nextSlash];
            ReadOnlySpan<char> suffix = remainder[(nextSlash + 1)..];

            if (suffix.SequenceEqual("payload".AsSpan()))
            {
                ctx.Position = SchemaPosition.ChannelMessagePayload;
                return true;
            }

            if (suffix.SequenceEqual("headers".AsSpan()))
            {
                ctx.Position = SchemaPosition.ChannelMessageHeaders;
                return true;
            }

            return false;
        }

        return false;
    }

    private static int FindSegment(ReadOnlySpan<char> pathAndRest, string targetSegment)
    {
        // Find the slash preceding the target segment.
        // The segments before it may contain JSON Pointer escaping (~1 = /, ~0 = ~).
        int searchStart = 0;

        while (searchStart < pathAndRest.Length)
        {
            int slash = pathAndRest[searchStart..].IndexOf('/');
            if (slash < 0)
            {
                break;
            }

            slash += searchStart;

            // Check if the segment after this slash matches
            ReadOnlySpan<char> after = pathAndRest[(slash + 1)..];
            int endOfSegment = after.IndexOf('/');
            ReadOnlySpan<char> segment = endOfSegment >= 0 ? after[..endOfSegment] : after;

            if (segment.SequenceEqual(targetSegment.AsSpan()))
            {
                return slash;
            }

            searchStart = slash + 1;
        }

        return -1;
    }

    private static int FormatName(in AsyncApiSchemaContext ctx, Span<char> buffer)
    {
        int written = 0;

        switch (ctx.Position)
        {
            case SchemaPosition.ComponentSchema:
                // Just PascalCase the schema name
                written += PascalCaseSegment(ctx.Name, buffer[written..]);
                break;

            case SchemaPosition.ComponentMessagePayload:
                written += PascalCaseSegment(ctx.Name, buffer[written..]);
                written += WriteSpan("Payload".AsSpan(), buffer[written..]);
                break;

            case SchemaPosition.ComponentMessageHeaders:
                written += PascalCaseSegment(ctx.Name, buffer[written..]);
                written += WriteSpan("Headers".AsSpan(), buffer[written..]);
                break;

            case SchemaPosition.ChannelMessagePayload:
                // Use the message name (not channel) as it's more specific
                written += PascalCaseSegment(ctx.Name, buffer[written..]);
                written += WriteSpan("Payload".AsSpan(), buffer[written..]);
                break;

            case SchemaPosition.ChannelMessageHeaders:
                written += PascalCaseSegment(ctx.Name, buffer[written..]);
                written += WriteSpan("Headers".AsSpan(), buffer[written..]);
                break;
        }

        return written;
    }

    private static int PascalCaseSegment(ReadOnlySpan<char> segment, Span<char> buffer)
    {
        if (segment.Length == 0 || buffer.Length == 0)
        {
            return 0;
        }

        int written = 0;
        bool capitalizeNext = true;

        for (int i = 0; i < segment.Length && written < buffer.Length; i++)
        {
            char c = segment[i];

            // Decode JSON Pointer escaping inline
            if (c == '~' && i + 1 < segment.Length)
            {
                if (segment[i + 1] == '1')
                {
                    // ~1 = / — treat as word boundary
                    capitalizeNext = true;
                    i++;
                    continue;
                }
                else if (segment[i + 1] == '0')
                {
                    // ~0 = ~ — treat as word boundary
                    capitalizeNext = true;
                    i++;
                    continue;
                }
            }

            if (c == '-' || c == '_' || c == '.')
            {
                capitalizeNext = true;
                continue;
            }

            buffer[written++] = capitalizeNext ? char.ToUpperInvariant(c) : c;
            capitalizeNext = false;
        }

        return written;
    }

    private static int WriteSpan(ReadOnlySpan<char> source, Span<char> destination)
    {
        int len = Math.Min(source.Length, destination.Length);
        source[..len].CopyTo(destination);
        return len;
    }

    private enum SchemaPosition
    {
        ComponentSchema,
        ComponentMessagePayload,
        ComponentMessageHeaders,
        ChannelMessagePayload,
        ChannelMessageHeaders,
    }

    private ref struct AsyncApiSchemaContext
    {
        public ReadOnlySpan<char> Name;
        public ReadOnlySpan<char> ChannelName;
        public SchemaPosition Position;
    }
}