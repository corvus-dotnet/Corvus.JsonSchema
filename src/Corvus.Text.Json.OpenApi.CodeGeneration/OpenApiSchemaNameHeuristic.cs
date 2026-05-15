// <copyright file="OpenApiSchemaNameHeuristic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.CodeGeneration;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// A naming heuristic that derives contextual type names from OpenAPI JSON Pointer fragments.
/// </summary>
/// <remarks>
/// <para>
/// When inline schemas are used in OpenAPI specs (rather than <c>$ref</c>), the default
/// <see cref="SubschemaNameHeuristic"/> produces the same name ("Schema") for every
/// response/request body schema because the last path segment is always <c>/schema</c>.
/// </para>
/// <para>
/// This heuristic recognises the OpenAPI JSON Pointer structure and derives names from the
/// HTTP method, URL path, and position (response status code, request body, or parameter).
/// For example:
/// <list type="bullet">
/// <item><c>/paths/~1items/get/responses/200/.../schema</c> → <c>GetItemsOk</c></item>
/// <item><c>/paths/~1items/post/responses/201/.../schema</c> → <c>PostItemsCreated</c></item>
/// <item><c>/paths/~1items/post/requestBody/.../schema</c> → <c>PostItemsBody</c></item>
/// <item><c>/paths/~1items~1{itemId}/get/responses/404/.../schema</c> → <c>GetItemsByItemIdNotFound</c></item>
/// </list>
/// </para>
/// <para>
/// Register this heuristic on the <see cref="CSharpLanguageProvider"/> before calling
/// <c>GenerateCodeUsing</c>. Its priority (500) must be lower than
/// <see cref="BaseSchemaNameHeuristic"/> (1000) to take precedence.
/// </para>
/// <para>
/// <b>Maintenance note:</b> This heuristic currently handles the five schema locations
/// extracted by <c>ClientModelBuilder</c>: <c>responses</c>, <c>requestBody</c>,
/// <c>parameters</c> (operation-level and path-level), and <c>response headers</c>.
/// When the walker is extended to extract schemas from additional OpenAPI locations
/// (callbacks, link schemas), corresponding branches must be added to
/// <see cref="TryParseOpenApiFragment"/>.
/// </para>
/// </remarks>
public sealed class OpenApiSchemaNameHeuristic : INameHeuristicBeforeSubschema
{
    private OpenApiSchemaNameHeuristic()
    {
    }

    /// <summary>
    /// Gets a singleton instance of the <see cref="OpenApiSchemaNameHeuristic"/>.
    /// </summary>
    public static OpenApiSchemaNameHeuristic Instance { get; } = new();

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

        // Must start with /paths/ to be an OpenAPI inline schema.
        if (!fragment.StartsWith("/paths/".AsSpan()))
        {
            return false;
        }

        // Parse: /paths/<url>/<method>/responses/<code>/content/<media>/schema[/...]
        //    or: /paths/<url>/<method>/requestBody/content/<media>/schema[/...]
        //    or: /paths/<url>/<method>/parameters/<idx>/schema[/...]
        // We only handle the root schema level (the immediate /schema).
        // Child schemas (e.g. /schema/properties/id) are handled by SubschemaNameHeuristic.
        if (!TryParseOpenApiFragment(fragment, out OpenApiSchemaContext ctx))
        {
            return false;
        }

        // Build the name: {Method}{Path}{Suffix}
        // e.g. "Get" + "ItemsByItemId" + "Ok"
        Span<char> workBuffer = stackalloc char[Formatting.MaxIdentifierLength];
        int pos = 0;

        // Method component
        pos += FormatMethod(ctx.Method, workBuffer[pos..]);

        // Path component — decode ~1 to / then PascalCase the segments
        pos += FormatUrlPath(ctx.UrlPath, workBuffer[pos..]);

        // Suffix component — response code name, "Body", or parameter name
        pos += FormatSuffix(ctx, workBuffer[pos..]);

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

    private static bool TryParseOpenApiFragment(
        ReadOnlySpan<char> fragment,
        out OpenApiSchemaContext ctx)
    {
        ctx = default;

        // Skip "/paths/"
        ReadOnlySpan<char> remainder = fragment["/paths/".Length..];

        // The URL path is the next segment (encoded with ~1 for /).
        // It ends at the next unescaped / that is followed by an HTTP method
        // or by "parameters" (for path-level parameters).
        int methodStart = FindMethodSegment(remainder);
        if (methodStart >= 0)
        {
            ctx.UrlPath = remainder[..methodStart];

            remainder = remainder[(methodStart + 1)..];

            // Extract the HTTP method
            int nextSlash = remainder.IndexOf('/');
            if (nextSlash < 0)
            {
                return false;
            }

            ctx.Method = remainder[..nextSlash];
            remainder = remainder[(nextSlash + 1)..];
        }
        else
        {
            // No HTTP method found — check for path-level parameters.
            // Format: /paths/<url>/parameters/<idx>/schema
            int paramStart = FindSegment(remainder, "parameters");
            if (paramStart < 0)
            {
                return false;
            }

            ctx.UrlPath = remainder[..paramStart];
            ctx.Method = [];
            remainder = remainder[(paramStart + 1)..];
        }

        // Now determine the position: responses, requestBody, or parameters
        if (remainder.StartsWith("responses/".AsSpan()))
        {
            remainder = remainder["responses/".Length..];

            // Extract status code (e.g. "200", "default")
            int nextSlash = remainder.IndexOf('/');
            if (nextSlash < 0)
            {
                // No content — just a response code with no schema (e.g. 204)
                return false;
            }

            ctx.StatusCode = remainder[..nextSlash];
            ReadOnlySpan<char> afterStatusCode = remainder[(nextSlash + 1)..];

            // Check for response header: headers/<name>/schema
            if (afterStatusCode.StartsWith("headers/".AsSpan()))
            {
                ReadOnlySpan<char> afterHeaders = afterStatusCode["headers/".Length..];
                int headerNameEnd = afterHeaders.IndexOf('/');
                if (headerNameEnd < 0)
                {
                    return false;
                }

                ctx.HeaderName = afterHeaders[..headerNameEnd];
                ctx.Position = SchemaPosition.ResponseHeader;

                // Must be exactly "schema" after the header name
                return afterHeaders[(headerNameEnd + 1)..].SequenceEqual("schema".AsSpan());
            }

            ctx.Position = SchemaPosition.Response;

            // Verify this is a root schema (must end with /schema or /schema followed by nothing)
            return IsRootSchema(afterStatusCode);
        }
        else if (remainder.StartsWith("requestBody/".AsSpan()))
        {
            ctx.Position = SchemaPosition.RequestBody;
            return IsRootSchema(remainder["requestBody/".Length..]);
        }
        else if (remainder.StartsWith("parameters/".AsSpan()))
        {
            remainder = remainder["parameters/".Length..];
            int nextSlash = remainder.IndexOf('/');
            if (nextSlash < 0)
            {
                return false;
            }

            ctx.ParameterIndex = remainder[..nextSlash];
            ctx.Position = SchemaPosition.Parameter;
            return IsRootSchema(remainder[(nextSlash + 1)..]);
        }

        return false;
    }

    private static bool IsRootSchema(ReadOnlySpan<char> remainder)
    {
        // We match the root schema: content/<media-type>/schema
        // or just "schema" for parameters.
        // We do NOT match child schemas like content/.../schema/properties/id.

        // For content paths: content/<media>/schema
        if (remainder.StartsWith("content/".AsSpan()))
        {
            remainder = remainder["content/".Length..];
            int slash = remainder.IndexOf('/');
            if (slash < 0)
            {
                return false;
            }

            remainder = remainder[(slash + 1)..];
        }

        // Must be exactly "schema" with nothing after it
        return remainder.SequenceEqual("schema".AsSpan());
    }

    private static int FindMethodSegment(ReadOnlySpan<char> pathAndRest)
    {
        return FindSegment(pathAndRest, null);
    }

    private static int FindSegment(ReadOnlySpan<char> pathAndRest, string? targetSegment)
    {
        // The URL path segment is encoded: / → ~1, ~ → ~0.
        // We need to find where the URL path ends and the next segment begins.
        // If targetSegment is null, we look for an HTTP method.
        // Otherwise, we look for the exact target segment name.
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

            if (targetSegment is null)
            {
                if (IsHttpMethod(segment))
                {
                    return slash;
                }
            }
            else
            {
                if (segment.SequenceEqual(targetSegment.AsSpan()))
                {
                    return slash;
                }
            }

            searchStart = slash + 1;
        }

        return -1;
    }

    private static bool IsHttpMethod(ReadOnlySpan<char> segment)
    {
        return segment.SequenceEqual("get".AsSpan()) ||
               segment.SequenceEqual("post".AsSpan()) ||
               segment.SequenceEqual("put".AsSpan()) ||
               segment.SequenceEqual("delete".AsSpan()) ||
               segment.SequenceEqual("patch".AsSpan()) ||
               segment.SequenceEqual("options".AsSpan()) ||
               segment.SequenceEqual("head".AsSpan()) ||
               segment.SequenceEqual("trace".AsSpan());
    }

    private static int FormatMethod(ReadOnlySpan<char> method, Span<char> buffer)
    {
        if (method.Length == 0)
        {
            return 0;
        }

        // PascalCase the method: "get" → "Get", "post" → "Post"
        buffer[0] = char.ToUpperInvariant(method[0]);
        int written = 1;

        for (int i = 1; i < method.Length && written < buffer.Length; i++)
        {
            buffer[written++] = char.ToLowerInvariant(method[i]);
        }

        return written;
    }

    private static int FormatUrlPath(ReadOnlySpan<char> encodedPath, Span<char> buffer)
    {
        // Decode ~1 → /, ~0 → ~ then extract meaningful segments.
        // /items/{itemId} → "ItemsByItemId"
        // /items/{itemId}/details → "ItemsByItemIdDetails"
        int written = 0;

        // First, decode JSON Pointer escaping into a temp buffer
        Span<char> decoded = stackalloc char[encodedPath.Length];
        int decodedLen = 0;

        for (int i = 0; i < encodedPath.Length; i++)
        {
            if (encodedPath[i] == '~' && i + 1 < encodedPath.Length)
            {
                if (encodedPath[i + 1] == '1')
                {
                    decoded[decodedLen++] = '/';
                    i++;
                    continue;
                }
                else if (encodedPath[i + 1] == '0')
                {
                    decoded[decodedLen++] = '~';
                    i++;
                    continue;
                }
            }

            decoded[decodedLen++] = encodedPath[i];
        }

        // Now split by / and format each segment
        ReadOnlySpan<char> path = decoded[..decodedLen];

        while (path.Length > 0)
        {
            // Skip leading /
            if (path[0] == '/')
            {
                path = path[1..];
                continue;
            }

            int nextSlash = path.IndexOf('/');
            ReadOnlySpan<char> segment = nextSlash >= 0 ? path[..nextSlash] : path;
            path = nextSlash >= 0 ? path[(nextSlash + 1)..] : [];

            if (segment.Length == 0)
            {
                continue;
            }

            // Path parameters like {itemId} → "ByItemId"
            if (segment[0] == '{' && segment[^1] == '}')
            {
                ReadOnlySpan<char> paramName = segment[1..^1];
                if (written + 2 + paramName.Length <= buffer.Length)
                {
                    buffer[written++] = 'B';
                    buffer[written++] = 'y';
                    written += PascalCaseSegment(paramName, buffer[written..]);
                }
            }
            else
            {
                written += PascalCaseSegment(segment, buffer[written..]);
            }
        }

        return written;
    }

    private static int FormatSuffix(in OpenApiSchemaContext ctx, Span<char> buffer)
    {
        return ctx.Position switch
        {
            SchemaPosition.Response => FormatStatusCode(ctx.StatusCode, buffer),
            SchemaPosition.RequestBody => WriteSpan("Body".AsSpan(), buffer),
            SchemaPosition.Parameter => FormatParameterSuffix(ctx.ParameterIndex, buffer),
            SchemaPosition.ResponseHeader => FormatResponseHeaderSuffix(ctx.StatusCode, ctx.HeaderName, buffer),
            _ => 0,
        };
    }

    private static int FormatStatusCode(ReadOnlySpan<char> code, Span<char> buffer)
    {
        // Map common status codes to readable names
        if (code.SequenceEqual("200".AsSpan()))
        {
            return WriteSpan("Ok".AsSpan(), buffer);
        }

        if (code.SequenceEqual("201".AsSpan()))
        {
            return WriteSpan("Created".AsSpan(), buffer);
        }

        if (code.SequenceEqual("202".AsSpan()))
        {
            return WriteSpan("Accepted".AsSpan(), buffer);
        }

        if (code.SequenceEqual("204".AsSpan()))
        {
            return WriteSpan("NoContent".AsSpan(), buffer);
        }

        if (code.SequenceEqual("301".AsSpan()))
        {
            return WriteSpan("MovedPermanently".AsSpan(), buffer);
        }

        if (code.SequenceEqual("304".AsSpan()))
        {
            return WriteSpan("NotModified".AsSpan(), buffer);
        }

        if (code.SequenceEqual("400".AsSpan()))
        {
            return WriteSpan("BadRequest".AsSpan(), buffer);
        }

        if (code.SequenceEqual("401".AsSpan()))
        {
            return WriteSpan("Unauthorized".AsSpan(), buffer);
        }

        if (code.SequenceEqual("403".AsSpan()))
        {
            return WriteSpan("Forbidden".AsSpan(), buffer);
        }

        if (code.SequenceEqual("404".AsSpan()))
        {
            return WriteSpan("NotFound".AsSpan(), buffer);
        }

        if (code.SequenceEqual("409".AsSpan()))
        {
            return WriteSpan("Conflict".AsSpan(), buffer);
        }

        if (code.SequenceEqual("422".AsSpan()))
        {
            return WriteSpan("UnprocessableEntity".AsSpan(), buffer);
        }

        if (code.SequenceEqual("429".AsSpan()))
        {
            return WriteSpan("TooManyRequests".AsSpan(), buffer);
        }

        if (code.SequenceEqual("500".AsSpan()))
        {
            return WriteSpan("InternalServerError".AsSpan(), buffer);
        }

        if (code.SequenceEqual("502".AsSpan()))
        {
            return WriteSpan("BadGateway".AsSpan(), buffer);
        }

        if (code.SequenceEqual("503".AsSpan()))
        {
            return WriteSpan("ServiceUnavailable".AsSpan(), buffer);
        }

        if (code.SequenceEqual("default".AsSpan()))
        {
            return WriteSpan("Default".AsSpan(), buffer);
        }

        // Unknown code — use "StatusNNN"
        int written = WriteSpan("Status".AsSpan(), buffer);
        written += WriteSpan(code, buffer[written..]);
        return written;
    }

    private static int FormatParameterSuffix(ReadOnlySpan<char> index, Span<char> buffer)
    {
        int written = WriteSpan("Param".AsSpan(), buffer);
        written += WriteSpan(index, buffer[written..]);
        return written;
    }

    private static int FormatResponseHeaderSuffix(
        ReadOnlySpan<char> statusCode,
        ReadOnlySpan<char> headerName,
        Span<char> buffer)
    {
        // e.g. "OkXRateLimit" for 200 + X-Rate-Limit
        int written = FormatStatusCode(statusCode, buffer);
        written += PascalCaseSegment(headerName, buffer[written..]);
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
        Response,
        RequestBody,
        Parameter,
        ResponseHeader,
    }

    private ref struct OpenApiSchemaContext
    {
        public ReadOnlySpan<char> Method;
        public ReadOnlySpan<char> UrlPath;
        public ReadOnlySpan<char> StatusCode;
        public ReadOnlySpan<char> ParameterIndex;
        public ReadOnlySpan<char> HeaderName;
        public SchemaPosition Position;
    }
}