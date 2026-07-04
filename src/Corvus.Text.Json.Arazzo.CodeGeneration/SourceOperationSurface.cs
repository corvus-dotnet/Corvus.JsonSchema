// <copyright file="SourceOperationSurface.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Writes an OpenAPI or AsyncAPI source document's operation surface — the designer's
/// <c>listSourceOperations</c> (workflow-designer design §4.1) — straight into a
/// <see cref="Utf8JsonWriter"/> as a JSON array of descriptors: per operation the binding identity
/// (<c>operationId</c>/path+method, or channel+action), the summary, the parameters, the request
/// content type and schema, and the documented response codes each with its content schema. Every
/// schema is emitted as <strong>raw JSON Schema</strong> (local <c>$ref</c>s inlined to a bounded
/// depth) — the shapes the designer's operation templates and payload editors consume directly.
/// </summary>
/// <remarks>
/// <para>The projection is a <strong>single write-through pass</strong>: source-document tokens
/// (ids, summaries, schemas) copy bytes-to-bytes into the destination writer — no descriptor
/// records, no per-schema buffers, no managed strings for values the document already holds.
/// Generated-type projections (<see cref="OperationResolver"/>'s descriptors) serve codegen, not
/// this browse surface.</para>
/// <para>The walk is tolerant like <see cref="WorkflowDocumentAnalyzer"/>: it writes whatever
/// well-formed shape it finds and never throws on a malformed document (an empty array simply
/// comes out). The document kind is detected from its own top-level field (<c>openapi</c> /
/// <c>asyncapi</c>); AsyncAPI 2.x <c>publish</c>/<c>subscribe</c> map to receive/send exactly as
/// the AsyncAPI code generator maps them.</para>
/// </remarks>
public static class SourceOperationSurface
{
    private const int MaxRefDepth = 8;

    // (lower-case document property, upper-case wire value) — fixed literals, no per-op case conversion.
    private static readonly (byte[] Property, byte[] Method)[] HttpMethods =
    [
        ("get"u8.ToArray(), "GET"u8.ToArray()),
        ("put"u8.ToArray(), "PUT"u8.ToArray()),
        ("post"u8.ToArray(), "POST"u8.ToArray()),
        ("delete"u8.ToArray(), "DELETE"u8.ToArray()),
        ("options"u8.ToArray(), "OPTIONS"u8.ToArray()),
        ("head"u8.ToArray(), "HEAD"u8.ToArray()),
        ("patch"u8.ToArray(), "PATCH"u8.ToArray()),
        ("trace"u8.ToArray(), "TRACE"u8.ToArray()),
        ("query"u8.ToArray(), "QUERY"u8.ToArray()),
    ];

    /// <summary>Writes the document's operation surface as a JSON array value (empty when the document
    /// declares neither kind or is malformed).</summary>
    /// <param name="writer">The destination writer (the caller owns the surrounding envelope).</param>
    /// <param name="document">The source document's root JSON value.</param>
    public static void WriteOperations(Utf8JsonWriter writer, in JsonElement document)
    {
        writer.WriteStartArray();
        if (document.ValueKind == JsonValueKind.Object)
        {
            if (document.TryGetProperty("openapi"u8, out JsonElement o) && o.ValueKind == JsonValueKind.String)
            {
                WriteOpenApi(writer, document);
            }
            else if (document.TryGetProperty("asyncapi"u8, out JsonElement a) && a.ValueKind == JsonValueKind.String)
            {
                if (a.GetString() is { } version && version.StartsWith("3", StringComparison.Ordinal))
                {
                    WriteAsyncApi30(writer, document);
                }
                else
                {
                    WriteAsyncApi2(writer, document);
                }
            }
        }

        writer.WriteEndArray();
    }

    // ── OpenAPI (3.x) ─────────────────────────────────────────────────────────────────────────────
    private static void WriteOpenApi(Utf8JsonWriter writer, in JsonElement document)
    {
        if (!document.TryGetProperty("paths"u8, out JsonElement paths) || paths.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (JsonProperty<JsonElement> pathEntry in paths.EnumerateObject())
        {
            JsonElement pathItem = Deref(document, pathEntry.Value, 0);
            if (pathItem.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            // Path-level parameters apply to every operation beneath, before the operation's own.
            JsonElement pathParameters = pathItem.TryGetProperty("parameters"u8, out JsonElement pp) ? pp : default;

            foreach ((byte[] property, byte[] method) in HttpMethods)
            {
                if (!pathItem.TryGetProperty(property, out JsonElement operation) || operation.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                writer.WriteStartObject();
                writer.WriteString("kind"u8, "openapi"u8);
                WriteValueAs(writer, "operationId"u8, operation, "operationId"u8);
                using (UnescapedUtf8JsonString path = pathEntry.Utf8NameSpan)
                {
                    writer.WriteString("path"u8, path.Span);
                }

                writer.WriteString("method"u8, method);
                WriteSummary(writer, operation);
                if (operation.TryGetProperty("deprecated"u8, out JsonElement d) && d.ValueKind == JsonValueKind.True)
                {
                    writer.WriteBoolean("deprecated"u8, true);
                }

                WriteOpenApiRequest(writer, document, operation);
                WriteOpenApiParameters(writer, document, pathParameters, operation);
                WriteOpenApiResponses(writer, document, operation);
                writer.WriteEndObject();
            }
        }
    }

    private static void WriteOpenApiRequest(Utf8JsonWriter writer, in JsonElement document, in JsonElement operation)
    {
        if (!operation.TryGetProperty("requestBody"u8, out JsonElement requestBodyRef))
        {
            return;
        }

        JsonElement requestBody = Deref(document, requestBodyRef, 0);
        if (requestBody.ValueKind != JsonValueKind.Object || !requestBody.TryGetProperty("content"u8, out JsonElement content))
        {
            return;
        }

        if (content.ValueKind != JsonValueKind.Object || !TryPickContent(content, out JsonProperty<JsonElement> chosen))
        {
            return;
        }

        writer.WriteStartObject("request"u8);
        using (UnescapedUtf8JsonString contentType = chosen.Utf8NameSpan)
        {
            writer.WriteString("contentType"u8, contentType.Span);
        }

        if (chosen.Value.ValueKind == JsonValueKind.Object && chosen.Value.TryGetProperty("schema"u8, out JsonElement schema))
        {
            writer.WritePropertyName("schema"u8);
            WriteResolved(writer, document, schema, 0);
        }

        writer.WriteEndObject();
    }

    // Picks the JSON content type when offered (application/json, else */json or +json), otherwise the first entry.
    private static bool TryPickContent(in JsonElement content, out JsonProperty<JsonElement> chosen)
    {
        chosen = default;
        bool found = false;
        bool foundJson = false;
        foreach (JsonProperty<JsonElement> entry in content.EnumerateObject())
        {
            if (entry.NameEquals("application/json"u8))
            {
                chosen = entry;
                return true;
            }

            bool isJson;
            using (UnescapedUtf8JsonString name = entry.Utf8NameSpan)
            {
                isJson = name.Span.EndsWith("+json"u8) || name.Span.EndsWith("/json"u8);
            }

            if (!found || (isJson && !foundJson))
            {
                chosen = entry;
                found = true;
                foundJson = isJson;
            }
        }

        return found;
    }

    private static void WriteOpenApiParameters(Utf8JsonWriter writer, in JsonElement document, in JsonElement pathParameters, in JsonElement operation)
    {
        JsonElement operationParameters = operation.TryGetProperty("parameters"u8, out JsonElement op) ? op : default;
        bool any = (pathParameters.ValueKind == JsonValueKind.Array && pathParameters.GetArrayLength() > 0)
            || (operationParameters.ValueKind == JsonValueKind.Array && operationParameters.GetArrayLength() > 0);
        if (!any)
        {
            return;
        }

        writer.WriteStartArray("parameters"u8);

        // Path-level first (skipping any the operation overrides by (name, in)), then the operation's own.
        if (pathParameters.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement entry in pathParameters.EnumerateArray())
            {
                JsonElement parameter = Deref(document, entry, 0);
                if (IsNamedParameter(parameter) && !IsOverridden(document, operationParameters, parameter))
                {
                    WriteParameter(writer, document, parameter);
                }
            }
        }

        if (operationParameters.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement entry in operationParameters.EnumerateArray())
            {
                JsonElement parameter = Deref(document, entry, 0);
                if (IsNamedParameter(parameter))
                {
                    WriteParameter(writer, document, parameter);
                }
            }
        }

        writer.WriteEndArray();
    }

    private static bool IsNamedParameter(in JsonElement parameter)
        => parameter.ValueKind == JsonValueKind.Object
            && parameter.TryGetProperty("name"u8, out JsonElement n) && n.ValueKind == JsonValueKind.String
            && parameter.TryGetProperty("in"u8, out JsonElement i) && i.ValueKind == JsonValueKind.String;

    // An operation-level parameter overrides a path-level one with the same (name, in) — compared bytes-to-bytes.
    private static bool IsOverridden(in JsonElement document, in JsonElement operationParameters, in JsonElement pathParameter)
    {
        if (operationParameters.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        pathParameter.TryGetProperty("name"u8, out JsonElement pathName);
        pathParameter.TryGetProperty("in"u8, out JsonElement pathIn);
        foreach (JsonElement entry in operationParameters.EnumerateArray())
        {
            JsonElement candidate = Deref(document, entry, 0);
            if (!IsNamedParameter(candidate))
            {
                continue;
            }

            candidate.TryGetProperty("name"u8, out JsonElement candidateName);
            candidate.TryGetProperty("in"u8, out JsonElement candidateIn);
            bool sameName;
            using (UnescapedUtf8JsonString name = candidateName.GetUtf8String())
            {
                sameName = pathName.ValueEquals(name.Span);
            }

            if (!sameName)
            {
                continue;
            }

            using UnescapedUtf8JsonString location = candidateIn.GetUtf8String();
            if (pathIn.ValueEquals(location.Span))
            {
                return true;
            }
        }

        return false;
    }

    private static void WriteParameter(Utf8JsonWriter writer, in JsonElement document, in JsonElement parameter)
    {
        writer.WriteStartObject();
        WriteValueAs(writer, "name"u8, parameter, "name"u8);
        WriteValueAs(writer, "in"u8, parameter, "in"u8);
        if (parameter.TryGetProperty("required"u8, out JsonElement r) && r.ValueKind == JsonValueKind.True)
        {
            writer.WriteBoolean("required"u8, true);
        }

        if (parameter.TryGetProperty("schema"u8, out JsonElement schema))
        {
            writer.WritePropertyName("schema"u8);
            WriteResolved(writer, document, schema, 0);
        }

        writer.WriteEndObject();
    }

    private static void WriteOpenApiResponses(Utf8JsonWriter writer, in JsonElement document, in JsonElement operation)
    {
        if (!operation.TryGetProperty("responses"u8, out JsonElement responses) || responses.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        writer.WriteStartObject("responses"u8);
        foreach (JsonProperty<JsonElement> response in responses.EnumerateObject())
        {
            using (UnescapedUtf8JsonString code = response.Utf8NameSpan)
            {
                writer.WriteStartObject(code.Span);
            }

            JsonElement resolved = Deref(document, response.Value, 0);
            if (resolved.ValueKind == JsonValueKind.Object
                && resolved.TryGetProperty("content"u8, out JsonElement content)
                && content.ValueKind == JsonValueKind.Object
                && TryPickContent(content, out JsonProperty<JsonElement> chosen)
                && chosen.Value.ValueKind == JsonValueKind.Object
                && chosen.Value.TryGetProperty("schema"u8, out JsonElement schema))
            {
                writer.WritePropertyName("schema"u8);
                WriteResolved(writer, document, schema, 0);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    // ── AsyncAPI 3.0 (top-level operations over channels) ────────────────────────────────────────
    private static void WriteAsyncApi30(Utf8JsonWriter writer, in JsonElement document)
    {
        if (!document.TryGetProperty("operations"u8, out JsonElement ops) || ops.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (JsonProperty<JsonElement> entry in ops.EnumerateObject())
        {
            JsonElement operation = Deref(document, entry.Value, 0);
            if (operation.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            writer.WriteStartObject();
            writer.WriteString("kind"u8, "asyncapi"u8);
            using (UnescapedUtf8JsonString id = entry.Utf8NameSpan)
            {
                writer.WriteString("operationId"u8, id.Span);
            }

            JsonElement channelRef = operation.TryGetProperty("channel"u8, out JsonElement cr) ? cr : default;
            JsonElement channel = Deref(document, channelRef, 0);
            if (channel.ValueKind == JsonValueKind.Object && channel.TryGetProperty("address"u8, out JsonElement address) && address.ValueKind == JsonValueKind.String)
            {
                writer.WritePropertyName("channelPath"u8);
                address.WriteTo(writer);
            }
            else
            {
                WriteChannelPathFromRef(writer, channelRef);
            }

            WriteValueAs(writer, "action"u8, operation, "action"u8);
            WriteSummary(writer, operation);

            // The message payload: the operation's first message, else the channel's first message.
            JsonElement message = FirstMessage(document, operation.TryGetProperty("messages"u8, out JsonElement m) ? m : default);
            if (message.ValueKind != JsonValueKind.Object && channel.ValueKind == JsonValueKind.Object && channel.TryGetProperty("messages"u8, out JsonElement channelMessages))
            {
                message = FirstMessage(document, channelMessages);
            }

            WriteMessagePayloadRequest(writer, document, message);
            writer.WriteEndObject();
        }
    }

    // The first message in an AsyncAPI messages collection (array of refs in 3.0 operations, map in channels).
    private static JsonElement FirstMessage(in JsonElement document, in JsonElement messages)
    {
        JsonElement first = default;
        if (messages.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement entry in messages.EnumerateArray())
            {
                first = entry;
                break;
            }
        }
        else if (messages.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty<JsonElement> entry in messages.EnumerateObject())
            {
                first = entry.Value;
                break;
            }
        }

        return Deref(document, first, 0);
    }

    private static void WriteMessagePayloadRequest(Utf8JsonWriter writer, in JsonElement document, in JsonElement message)
    {
        if (message.ValueKind == JsonValueKind.Object && message.TryGetProperty("payload"u8, out JsonElement payload))
        {
            writer.WriteStartObject("request"u8);
            writer.WritePropertyName("schema"u8);
            WriteResolved(writer, document, payload, 0);
            writer.WriteEndObject();
        }
    }

    // A 3.0 channel ref ('#/channels/<key>') names the channel when the channel object has no
    // address — the key writes straight from the pointer's UTF-8 (decoded only when RFC 6901-escaped).
    private static void WriteChannelPathFromRef(Utf8JsonWriter writer, in JsonElement channelRef)
    {
        if (channelRef.ValueKind != JsonValueKind.Object
            || !channelRef.TryGetProperty("$ref"u8, out JsonElement r)
            || r.ValueKind != JsonValueKind.String)
        {
            return;
        }

        using UnescapedUtf8JsonString pointer = r.GetUtf8String();
        if (!pointer.Span.StartsWith("#/channels/"u8))
        {
            return;
        }

        ReadOnlySpan<byte> key = pointer.Span["#/channels/"u8.Length..];
        if (key.IndexOf((byte)'~') < 0)
        {
            writer.WriteString("channelPath"u8, key);
        }
        else
        {
            writer.WriteString("channelPath"u8, System.Text.Encoding.UTF8.GetString(key).Replace("~1", "/").Replace("~0", "~"));
        }
    }

    // ── AsyncAPI 2.x (publish/subscribe under channels) ──────────────────────────────────────────
    private static void WriteAsyncApi2(Utf8JsonWriter writer, in JsonElement document)
    {
        if (!document.TryGetProperty("channels"u8, out JsonElement channels) || channels.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (JsonProperty<JsonElement> channelEntry in channels.EnumerateObject())
        {
            JsonElement channel = Deref(document, channelEntry.Value, 0);
            if (channel.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            // 2.x semantics, mapped exactly as the AsyncAPI code generator maps them: `publish` =
            // clients publish TO the channel (the application RECEIVES); `subscribe` = the
            // application SENDS.
            WriteAsyncApi2Operation(writer, document, channelEntry, channel, "publish"u8, "receive"u8);
            WriteAsyncApi2Operation(writer, document, channelEntry, channel, "subscribe"u8, "send"u8);
        }
    }

    private static void WriteAsyncApi2Operation(Utf8JsonWriter writer, in JsonElement document, in JsonProperty<JsonElement> channelEntry, in JsonElement channel, ReadOnlySpan<byte> property, ReadOnlySpan<byte> action)
    {
        if (!channel.TryGetProperty(property, out JsonElement operation) || operation.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("kind"u8, "asyncapi"u8);
        WriteValueAs(writer, "operationId"u8, operation, "operationId"u8);
        using (UnescapedUtf8JsonString channelPath = channelEntry.Utf8NameSpan)
        {
            writer.WriteString("channelPath"u8, channelPath.Span);
        }

        writer.WriteString("action"u8, action);
        WriteSummary(writer, operation);

        if (operation.TryGetProperty("message"u8, out JsonElement messageRef))
        {
            JsonElement message = Deref(document, messageRef, 0);

            // A oneOf message set projects its first alternative (the browse surface, not a validator).
            if (message.ValueKind == JsonValueKind.Object && message.TryGetProperty("oneOf"u8, out JsonElement oneOf) && oneOf.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement alternative in oneOf.EnumerateArray())
                {
                    message = Deref(document, alternative, 0);
                    break;
                }
            }

            WriteMessagePayloadRequest(writer, document, message);
        }

        writer.WriteEndObject();
    }

    // ── shared write helpers ──────────────────────────────────────────────────────────────────────

    // The operation's summary (falling back to its description), copied bytes-to-bytes under 'summary'.
    private static void WriteSummary(Utf8JsonWriter writer, in JsonElement operation)
    {
        if (operation.TryGetProperty("summary"u8, out JsonElement summary) && summary.ValueKind == JsonValueKind.String)
        {
            writer.WritePropertyName("summary"u8);
            summary.WriteTo(writer);
        }
        else if (operation.TryGetProperty("description"u8, out JsonElement description) && description.ValueKind == JsonValueKind.String)
        {
            writer.WritePropertyName("summary"u8);
            description.WriteTo(writer);
        }
    }

    // Copies a string-valued source property to the destination under the given name, bytes-to-bytes.
    private static void WriteValueAs(Utf8JsonWriter writer, ReadOnlySpan<byte> destinationName, in JsonElement owner, ReadOnlySpan<byte> sourceProperty)
    {
        if (owner.TryGetProperty(sourceProperty, out JsonElement value) && value.ValueKind == JsonValueKind.String)
        {
            writer.WritePropertyName(destinationName);
            value.WriteTo(writer);
        }
    }

    // Writes a schema subtree, inlining local $refs to a bounded depth (cycles and over-deep chains
    // flatten to {}) — the designer's skeleton/typed-form builders consume plain schemas and do not
    // chase references.
    private static void WriteResolved(Utf8JsonWriter writer, in JsonElement document, in JsonElement node, int depth)
    {
        if (depth > MaxRefDepth)
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
            return;
        }

        if (node.ValueKind == JsonValueKind.Object)
        {
            if (node.TryGetProperty("$ref"u8, out JsonElement r) && r.ValueKind == JsonValueKind.String)
            {
                WriteResolved(writer, document, Deref(document, node, depth), depth + 1);
                return;
            }

            writer.WriteStartObject();
            foreach (JsonProperty<JsonElement> property in node.EnumerateObject())
            {
                using (UnescapedUtf8JsonString name = property.Utf8NameSpan)
                {
                    writer.WritePropertyName(name.Span);
                }

                WriteResolved(writer, document, property.Value, depth + 1);
            }

            writer.WriteEndObject();
            return;
        }

        if (node.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (JsonElement item in node.EnumerateArray())
            {
                WriteResolved(writer, document, item, depth + 1);
            }

            writer.WriteEndArray();
            return;
        }

        node.WriteTo(writer);
    }

    // Follows a local $ref ('#/…') into the same document (chains bounded); non-local or unresolvable
    // references (and non-ref nodes) come back as-is / undefined. The pointer walks as UTF-8 spans —
    // no per-hop string or segment split; an RFC 6901-escaped segment ('~0'/'~1', rare) falls back to
    // a decoded string for that segment only.
    private static JsonElement Deref(in JsonElement document, in JsonElement node, int depth)
    {
        if (depth > MaxRefDepth
            || node.ValueKind != JsonValueKind.Object
            || !node.TryGetProperty("$ref"u8, out JsonElement r)
            || r.ValueKind != JsonValueKind.String)
        {
            return node;
        }

        JsonElement current;
        using (UnescapedUtf8JsonString pointer = r.GetUtf8String())
        {
            ReadOnlySpan<byte> remaining = pointer.Span;
            if (!remaining.StartsWith("#/"u8))
            {
                return node;
            }

            remaining = remaining[2..];
            current = document;
            while (true)
            {
                int slash = remaining.IndexOf((byte)'/');
                ReadOnlySpan<byte> segment = slash < 0 ? remaining : remaining[..slash];
                if (!TryStepSegment(current, segment, out JsonElement next))
                {
                    return default;
                }

                current = next;

                if (slash < 0)
                {
                    break;
                }

                remaining = remaining[(slash + 1)..];
            }
        }

        return Deref(document, current, depth + 1);
    }

    // One pointer-segment step: the common unescaped segment matches as raw UTF-8; a segment carrying
    // '~' decodes per RFC 6901 first (the rare path).
    private static bool TryStepSegment(in JsonElement owner, ReadOnlySpan<byte> segment, out JsonElement value)
    {
        value = default;
        if (owner.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (segment.IndexOf((byte)'~') < 0)
        {
            return owner.TryGetProperty(segment, out value);
        }

        string decoded = System.Text.Encoding.UTF8.GetString(segment).Replace("~1", "/").Replace("~0", "~");
        return owner.TryGetProperty(decoded, out value);
    }
}