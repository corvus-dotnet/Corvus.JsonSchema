// <copyright file="WorkflowSchemaMetadataGenerator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using ArazzoExpr = Corvus.Text.Json.Arazzo.ArazzoExpression;
using ArazzoExprSource = Corvus.Text.Json.Arazzo.ArazzoExpressionSource;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Precomputes a compact, read-cheap "schema metadata" document for an Arazzo workflow package — for each
/// workflow, the typed shape of its <c>inputs</c>, and for every step its resolved operation reference, its
/// typed request (parameters + body) and responses (body + headers) for OpenAPI operations or its message
/// (payload + headers) for AsyncAPI operations, and its resolved <c>outputs</c> — by resolving the workflow's
/// expressions against its referenced OpenAPI/AsyncAPI source documents and normalising the resulting JSON
/// Schema nodes into a small recursive type descriptor.
/// </summary>
/// <remarks>
/// <para>The document shape (all type descriptors share one recursive shape):</para>
/// <code>
/// {
///   "formatVersion": 1,
///   "workflows": {
///     "&lt;workflowId&gt;": {
///       "inputs": &lt;TypeDescriptor&gt;,
///       "steps": { "&lt;stepId&gt;": { "outputs": { "&lt;name&gt;": &lt;TypeDescriptor&gt; } } }
///     }
///   }
/// }
/// </code>
/// <para>A <c>TypeDescriptor</c> normalises a JSON Schema node into the keywords a UI needs to render a suitable
/// control: <c>type</c>, <c>format</c> (the recognised numeric/string formats — <c>int32</c>/<c>int64</c>,
/// <c>date-time</c>/<c>date</c>/<c>time</c>/<c>email</c>/<c>uri</c>/<c>uuid</c>/…), <c>enum</c>, <c>nullable</c>,
/// the validation constraints (<c>minimum</c>/<c>maximum</c>/<c>multipleOf</c>/<c>minLength</c>/<c>maxLength</c>/
/// <c>pattern</c>/…), <c>default</c>, <c>const</c>, <c>title</c>/<c>description</c>, and recursively
/// <c>properties</c>+<c>required</c> (objects) / <c>items</c> (arrays). It also recognises a few composite shapes:
/// a simple polymorphic union (<c>oneOf</c>/<c>anyOf</c>) becomes <c>{ "type": "union", "variants": [ … ],
/// "discriminator"?: "&lt;prop&gt;" }</c> (a <c>X | null</c> union collapses to a nullable <c>X</c>); a tuple
/// (<c>prefixItems</c> or the legacy array-form <c>items</c>) adds <c>"prefixItems": [ … ]</c> with an optional
/// trailing <c>items</c> schema; and a free-form map (<c>additionalProperties</c>/<c>unevaluatedProperties</c>)
/// adds <c>"additionalProperties": &lt;TypeDescriptor&gt;</c>. Anything that cannot be resolved degrades to
/// <c>{ "type": "unknown" }</c> rather than failing, so the document is always producible.</para>
/// </remarks>
public static class WorkflowSchemaMetadataGenerator
{
    /// <summary>The format version written into the metadata document.</summary>
    public const int FormatVersion = 1;

    private const int MaxDepth = 12;

    // Validation/annotation keywords copied verbatim so the UI can render a suitable, constrained control.
    private static readonly string[] ConstraintKeywords =
    [
        "minimum", "maximum", "exclusiveMinimum", "exclusiveMaximum", "multipleOf",
        "minLength", "maxLength", "pattern",
        "minItems", "maxItems", "uniqueItems",
        "default", "const", "title", "readOnly", "writeOnly", "examples",
    ];

    /// <summary>
    /// Generates the schema-metadata document for a workflow package.
    /// </summary>
    /// <param name="workflowUtf8">The Arazzo workflow document as UTF-8 JSON.</param>
    /// <param name="sources">The referenced source documents (name → UTF-8 JSON bytes), keyed by their
    /// <c>sourceDescriptions</c> name.</param>
    /// <returns>The metadata document as UTF-8 JSON.</returns>
    public static byte[] Generate(ReadOnlyMemory<byte> workflowUtf8, IReadOnlyList<KeyValuePair<string, byte[]>> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        using ParsedJsonDocument<JsonElement> workflowDoc = ParsedJsonDocument<JsonElement>.Parse(workflowUtf8);
        JsonElement workflow = workflowDoc.RootElement;

        var sourceDocs = new List<ParsedJsonDocument<JsonElement>>();
        var sourcesByName = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        try
        {
            foreach (KeyValuePair<string, byte[]> source in sources)
            {
                ParsedJsonDocument<JsonElement> parsed = ParsedJsonDocument<JsonElement>.Parse(source.Value);
                sourceDocs.Add(parsed);
                sourcesByName[source.Key] = parsed.RootElement;
            }

            var buffer = new ArrayBufferWriter<byte>();
            var writer = new Utf8JsonWriter(buffer);

            writer.WriteStartObject();
            writer.WriteNumber("formatVersion", FormatVersion);
            writer.WritePropertyName("workflows");
            writer.WriteStartObject();

            if (workflow.TryGetProperty("workflows", out JsonElement workflows) && workflows.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement wf in workflows.EnumerateArray())
                {
                    WriteWorkflow(writer, wf, workflow, sourcesByName);
                }
            }

            writer.WriteEndObject(); // workflows
            writer.WriteEndObject(); // root
            writer.Flush();

            return buffer.WrittenSpan.ToArray();
        }
        finally
        {
            foreach (ParsedJsonDocument<JsonElement> doc in sourceDocs)
            {
                doc.Dispose();
            }
        }
    }

    private static void WriteWorkflow(Utf8JsonWriter writer, JsonElement workflow, JsonElement workflowRoot, IReadOnlyDictionary<string, JsonElement> sources)
    {
        string workflowId = TryGetString(workflow, "workflowId") ?? "$workflow";
        writer.WritePropertyName(workflowId);
        writer.WriteStartObject();

        // inputs — the inline JSON Schema at workflow.inputs.
        writer.WritePropertyName("inputs");
        if (workflow.TryGetProperty("inputs", out JsonElement inputs) && inputs.ValueKind == JsonValueKind.Object)
        {
            WriteTypeDescriptor(writer, inputs, workflowRoot, 0);
        }
        else
        {
            WriteUnknown(writer);
        }

        // steps — for each step, the resolved type of every declared output.
        writer.WritePropertyName("steps");
        writer.WriteStartObject();
        if (workflow.TryGetProperty("steps", out JsonElement steps) && steps.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement step in steps.EnumerateArray())
            {
                WriteStep(writer, step, workflow, workflowRoot, sources);
            }
        }

        writer.WriteEndObject(); // steps
        writer.WriteEndObject(); // workflow
    }

    private static void WriteStep(Utf8JsonWriter writer, JsonElement step, JsonElement workflow, JsonElement workflowRoot, IReadOnlyDictionary<string, JsonElement> sources)
    {
        string? stepId = TryGetString(step, "stepId");
        if (stepId is null)
        {
            return;
        }

        bool resolved = TryResolveStepOperation(step, sources, out StepOperation op);

        writer.WritePropertyName(stepId);
        writer.WriteStartObject();

        if (resolved)
        {
            WriteOperationRef(writer, step, op);
            if (op.IsAsyncApi)
            {
                WriteMessage(writer, op);
            }
            else
            {
                WriteRequest(writer, op);
                WriteResponses(writer, op);
            }
        }

        // The schemas an output expression may resolve against (success response body / message payload / request body).
        JsonElement responseSchema = default, payloadSchema = default, requestBodySchema = default;
        bool haveResponse = resolved && !op.IsAsyncApi && TrySuccessResponseBody(op, out responseSchema);
        bool havePayload = resolved && op.IsAsyncApi && TryMessagePayload(op, out payloadSchema);
        bool haveRequestBody = resolved && !op.IsAsyncApi && TryRequestBody(op, out requestBodySchema);
        JsonElement opRoot = resolved ? op.Root : default;

        writer.WritePropertyName("outputs");
        writer.WriteStartObject();
        if (step.TryGetProperty("outputs", out JsonElement outputs) && outputs.ValueKind == JsonValueKind.Object)
        {
            foreach (var output in outputs.EnumerateObject())
            {
                writer.WritePropertyName(output.Name);
                string expression = output.Value.ValueKind == JsonValueKind.String ? output.Value.GetString() ?? string.Empty : string.Empty;
                WriteOutputDescriptor(writer, expression, workflow, workflowRoot, opRoot, haveResponse, responseSchema, havePayload, payloadSchema, haveRequestBody, requestBodySchema);
            }
        }

        writer.WriteEndObject(); // outputs
        writer.WriteEndObject(); // step
    }

    private static void WriteOutputDescriptor(
        Utf8JsonWriter writer,
        string expression,
        JsonElement workflow,
        JsonElement workflowRoot,
        JsonElement opRoot,
        bool haveResponse,
        JsonElement responseSchema,
        bool havePayload,
        JsonElement payloadSchema,
        bool haveRequestBody,
        JsonElement requestBodySchema)
    {
        ArazzoExpr expr = ArazzoExpr.Parse(expression);
        switch (expr.Source)
        {
            case ArazzoExprSource.StatusCode:
                WriteScalar(writer, "integer");
                return;

            case ArazzoExprSource.Url:
            case ArazzoExprSource.Method:
            case ArazzoExprSource.RequestHeader:
            case ArazzoExprSource.ResponseHeader:
            case ArazzoExprSource.RequestPath:
            case ArazzoExprSource.RequestQuery:
            case ArazzoExprSource.MessageHeader:
                WriteScalar(writer, "string");
                return;

            case ArazzoExprSource.Inputs when expr.Name is { } inputName:
                if (workflow.TryGetProperty("inputs", out JsonElement inputs)
                    && TryNavigateProperty(inputs, workflowRoot, inputName, out JsonElement inputSchema)
                    && TryNavigatePointer(inputSchema, workflowRoot, expr.JsonPointer, out JsonElement resolvedInput))
                {
                    WriteTypeDescriptor(writer, resolvedInput, workflowRoot, 0);
                    return;
                }

                break;

            case ArazzoExprSource.ResponseBody when haveResponse:
                if (TryNavigatePointer(responseSchema, opRoot, expr.JsonPointer, out JsonElement resolvedBody))
                {
                    WriteTypeDescriptor(writer, resolvedBody, opRoot, 0);
                    return;
                }

                break;

            case ArazzoExprSource.RequestBody when haveRequestBody:
                if (TryNavigatePointer(requestBodySchema, opRoot, expr.JsonPointer, out JsonElement resolvedRequest))
                {
                    WriteTypeDescriptor(writer, resolvedRequest, opRoot, 0);
                    return;
                }

                break;

            case ArazzoExprSource.MessagePayload when havePayload:
                if (TryNavigatePointer(payloadSchema, opRoot, expr.JsonPointer, out JsonElement resolvedPayload))
                {
                    WriteTypeDescriptor(writer, resolvedPayload, opRoot, 0);
                    return;
                }

                break;

            case ArazzoExprSource.Literal:
                WriteScalar(writer, "string");
                return;
        }

        // $steps.*.outputs.*, $workflows.*, or an unresolvable source/pointer — not statically typed.
        WriteUnknown(writer);
    }

    // ---- step surface sections (editor metadata) ------------------------------------------------

    /// <summary>Writes the resolved operation reference — source, kind, id, and method/path (or action/channel).</summary>
    private static void WriteOperationRef(Utf8JsonWriter writer, JsonElement step, StepOperation op)
    {
        writer.WritePropertyName("operation");
        writer.WriteStartObject();
        writer.WriteString("source", op.Source);
        writer.WriteString("kind", op.IsAsyncApi ? "asyncapi" : "openapi");
        if (TryGetString(step, "operationId") is { } operationId)
        {
            writer.WriteString("operationId", operationId);
        }

        if (op.IsAsyncApi)
        {
            if (op.Verb is not null)
            {
                writer.WriteString("action", op.Verb);
            }

            if (op.PathOrChannel is not null)
            {
                writer.WriteString("channel", op.PathOrChannel);
            }
        }
        else
        {
            if (op.Verb is not null)
            {
                writer.WriteString("method", op.Verb);
            }

            if (op.PathOrChannel is not null)
            {
                writer.WriteString("path", op.PathOrChannel);
            }
        }

        writer.WriteEndObject();
    }

    /// <summary>Writes the OpenAPI request surface — typed parameters (path/query/header/cookie) and the request body.</summary>
    private static void WriteRequest(Utf8JsonWriter writer, StepOperation op)
    {
        writer.WritePropertyName("request");
        writer.WriteStartObject();

        writer.WritePropertyName("parameters");
        writer.WriteStartObject();
        if (op.Operation.TryGetProperty("parameters", out JsonElement parameters) && parameters.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement raw in parameters.EnumerateArray())
            {
                JsonElement parameter = SchemaClassifier.ResolveRef(raw, op.Root);
                if (TryGetString(parameter, "name") is not { } name)
                {
                    continue;
                }

                writer.WritePropertyName(name);
                writer.WriteStartObject();
                if (TryGetString(parameter, "in") is { } location)
                {
                    writer.WriteString("in", location);
                }

                if (parameter.TryGetProperty("required", out JsonElement required) && required.ValueKind == JsonValueKind.True)
                {
                    writer.WriteBoolean("required", true);
                }

                writer.WritePropertyName("schema");
                if (parameter.TryGetProperty("schema", out JsonElement parameterSchema))
                {
                    WriteTypeDescriptor(writer, parameterSchema, op.Root, 0);
                }
                else
                {
                    WriteUnknown(writer);
                }

                writer.WriteEndObject();
            }
        }

        writer.WriteEndObject(); // parameters

        if (TryRequestBody(op, out JsonElement bodySchema))
        {
            writer.WritePropertyName("body");
            WriteTypeDescriptor(writer, bodySchema, op.Root, 0);
        }

        writer.WriteEndObject(); // request
    }

    /// <summary>Writes the OpenAPI response surface — per status code, the typed body and headers.</summary>
    private static void WriteResponses(Utf8JsonWriter writer, StepOperation op)
    {
        writer.WritePropertyName("responses");
        writer.WriteStartObject();
        if (op.Operation.TryGetProperty("responses", out JsonElement responses) && responses.ValueKind == JsonValueKind.Object)
        {
            foreach (var response in responses.EnumerateObject())
            {
                writer.WritePropertyName(response.Name);
                writer.WriteStartObject();
                JsonElement resolved = SchemaClassifier.ResolveRef(response.Value, op.Root);

                if (TryBodySchema(resolved, op.Root, out JsonElement bodySchema))
                {
                    writer.WritePropertyName("body");
                    WriteTypeDescriptor(writer, bodySchema, op.Root, 0);
                }

                if (resolved.TryGetProperty("headers", out JsonElement headers) && headers.ValueKind == JsonValueKind.Object)
                {
                    writer.WritePropertyName("headers");
                    writer.WriteStartObject();
                    foreach (var header in headers.EnumerateObject())
                    {
                        JsonElement headerObject = SchemaClassifier.ResolveRef(header.Value, op.Root);
                        writer.WritePropertyName(header.Name);
                        if (headerObject.TryGetProperty("schema", out JsonElement headerSchema))
                        {
                            WriteTypeDescriptor(writer, headerSchema, op.Root, 0);
                        }
                        else
                        {
                            WriteUnknown(writer);
                        }
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }
        }

        writer.WriteEndObject();
    }

    /// <summary>Writes the AsyncAPI message surface — the first message's typed payload and headers.</summary>
    private static void WriteMessage(Utf8JsonWriter writer, StepOperation op)
    {
        writer.WritePropertyName("message");
        writer.WriteStartObject();
        if (TryResolveMessage(op, out JsonElement message))
        {
            if (message.TryGetProperty("payload", out JsonElement payload))
            {
                writer.WritePropertyName("payload");
                WriteTypeDescriptor(writer, payload, op.Root, 0);
            }

            if (message.TryGetProperty("headers", out JsonElement headers))
            {
                writer.WritePropertyName("headers");
                WriteTypeDescriptor(writer, headers, op.Root, 0);
            }
        }

        writer.WriteEndObject();
    }

    private static bool TryRequestBody(StepOperation op, out JsonElement schema)
    {
        schema = default;
        if (!op.Operation.TryGetProperty("requestBody", out JsonElement requestBody))
        {
            return false;
        }

        return TryBodySchema(SchemaClassifier.ResolveRef(requestBody, op.Root), op.Root, out schema);
    }

    /// <summary>The success (lowest 2xx, else <c>default</c>) response body schema — what <c>$response.body</c> resolves against.</summary>
    private static bool TrySuccessResponseBody(StepOperation op, out JsonElement schema)
    {
        schema = default;
        if (!op.Operation.TryGetProperty("responses", out JsonElement responses) || responses.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        JsonElement chosen = default;
        bool found = false;
        foreach (var response in responses.EnumerateObject())
        {
            if (response.Name.Length == 3 && response.Name[0] == '2')
            {
                chosen = response.Value;
                found = true;
                break;
            }
        }

        if (!found && responses.TryGetProperty("default", out JsonElement defaultResponse))
        {
            chosen = defaultResponse;
            found = true;
        }

        return found && TryBodySchema(SchemaClassifier.ResolveRef(chosen, op.Root), op.Root, out schema);
    }

    private static bool TryMessagePayload(StepOperation op, out JsonElement schema)
    {
        schema = default;
        return TryResolveMessage(op, out JsonElement message) && message.TryGetProperty("payload", out schema);
    }

    private static bool TryResolveMessage(StepOperation op, out JsonElement message)
    {
        message = default;
        if (op.Operation.TryGetProperty("messages", out JsonElement messages) && messages.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement candidate in messages.EnumerateArray())
            {
                message = SchemaClassifier.ResolveRef(candidate, op.Root);
                return true; // the first message describes the step's payload
            }
        }

        return false;
    }

    /// <summary>The first JSON media type's <c>schema</c> from a request-body/response <c>content</c> map.</summary>
    private static bool TryBodySchema(JsonElement carrier, JsonElement documentRoot, out JsonElement schema)
    {
        schema = default;
        _ = documentRoot;
        if (!carrier.TryGetProperty("content", out JsonElement content) || content.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var mediaType in content.EnumerateObject())
        {
            if (mediaType.Value.TryGetProperty("schema", out schema))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves a step's <c>operationId</c> (or <c>operationPath</c>) to the operation object in its source
    /// document, distinguishing OpenAPI operations (found under <c>paths</c>) from AsyncAPI operations (found
    /// under <c>operations</c>).
    /// </summary>
    private static bool TryResolveStepOperation(JsonElement step, IReadOnlyDictionary<string, JsonElement> sources, out StepOperation result)
    {
        result = default;

        if (TryGetString(step, "operationId") is { } operationId)
        {
            foreach (KeyValuePair<string, JsonElement> source in sources)
            {
                JsonElement root = source.Value;
                if (IsAsyncApi(root))
                {
                    if (root.TryGetProperty("operations", out JsonElement operations)
                        && operations.ValueKind == JsonValueKind.Object
                        && operations.TryGetProperty(operationId, out JsonElement asyncOperation))
                    {
                        result = new StepOperation(asyncOperation, root, true, source.Key, TryGetString(asyncOperation, "action"), ChannelRef(asyncOperation));
                        return true;
                    }
                }
                else if (TryFindOpenApiOperation(root, operationId, out JsonElement operation, out string? method, out string? path))
                {
                    result = new StepOperation(operation, root, false, source.Key, method, path);
                    return true;
                }
            }
        }

        if (TryGetString(step, "operationPath") is { } operationPath)
        {
            // {$sourceDescriptions.<name>.url}#/json/pointer — resolve the pointer in the named source document.
            int hash = operationPath.IndexOf('#', StringComparison.Ordinal);
            if (hash >= 0)
            {
                string pointer = operationPath[(hash + 1)..];
                string prefix = operationPath[..hash];
                foreach (KeyValuePair<string, JsonElement> source in sources)
                {
                    if (prefix.Contains(source.Key, StringComparison.Ordinal)
                        && source.Value.TryResolvePointer(pointer, out JsonElement operation))
                    {
                        result = new StepOperation(operation, source.Value, IsAsyncApi(source.Value), source.Key, null, null);
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool TryFindOpenApiOperation(JsonElement openApi, string operationId, out JsonElement operation, out string? method, out string? path)
    {
        operation = default;
        method = null;
        path = null;
        if (!openApi.TryGetProperty("paths", out JsonElement paths) || paths.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var pathItem in paths.EnumerateObject())
        {
            if (pathItem.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var verb in pathItem.Value.EnumerateObject())
            {
                if (verb.Value.ValueKind == JsonValueKind.Object
                    && TryGetString(verb.Value, "operationId") == operationId)
                {
                    operation = verb.Value;
                    method = verb.Name;
                    path = pathItem.Name;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsAsyncApi(JsonElement root)
        => root.ValueKind == JsonValueKind.Object
            && (root.TryGetProperty("asyncapi", out _)
                || (root.TryGetProperty("operations", out _) && root.TryGetProperty("channels", out _) && !root.TryGetProperty("paths", out _)));

    private static string? ChannelRef(JsonElement asyncOperation)
        => asyncOperation.TryGetProperty("channel", out JsonElement channel)
            && channel.TryGetProperty("$ref", out JsonElement reference)
            && reference.ValueKind == JsonValueKind.String
            ? reference.GetString()
            : null;

    /// <summary>A step's resolved operation: the operation node, its source document + name, whether it is AsyncAPI,
    /// and the HTTP method/path (OpenAPI) or action/channel (AsyncAPI).</summary>
    private readonly record struct StepOperation(JsonElement Operation, JsonElement Root, bool IsAsyncApi, string Source, string? Verb, string? PathOrChannel);

    // ---- type descriptor ------------------------------------------------------------------------

    /// <summary>Writes the normalised recursive type descriptor for a JSON Schema node.</summary>
    private static void WriteTypeDescriptor(Utf8JsonWriter writer, JsonElement schema, JsonElement documentRoot, int depth, bool forceNullable = false)
    {
        schema = SchemaClassifier.ResolveRef(schema, documentRoot);

        // Polymorphic union (oneOf/anyOf). "X | null" collapses to X with nullable; a single remaining branch
        // unwraps to that branch; two or more become a "union" descriptor (a typed variant picker in the UI).
        if (depth < MaxDepth && schema.ValueKind == JsonValueKind.Object
            && TryReadUnionVariants(schema, out List<JsonElement> variants, out bool unionHasNull))
        {
            if (variants.Count == 1)
            {
                WriteTypeDescriptor(writer, variants[0], documentRoot, depth, forceNullable || unionHasNull);
                return;
            }

            writer.WriteStartObject();
            if (variants.Count == 0)
            {
                writer.WriteString("type", unionHasNull ? "null" : "unknown");
                writer.WriteEndObject();
                return;
            }

            writer.WriteString("type", "union");
            if (unionHasNull || forceNullable)
            {
                writer.WriteBoolean("nullable", true);
            }

            if (schema.TryGetProperty("description", out JsonElement unionDescription) && unionDescription.ValueKind == JsonValueKind.String)
            {
                writer.WriteString("description", unionDescription.GetString());
            }

            if (TryDiscriminatorProperty(schema, out string discriminator))
            {
                writer.WriteString("discriminator", discriminator);
            }

            writer.WritePropertyName("variants");
            writer.WriteStartArray();
            foreach (JsonElement variant in variants)
            {
                WriteTypeDescriptor(writer, variant, documentRoot, depth + 1);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            return;
        }

        writer.WriteStartObject();
        if (depth >= MaxDepth || schema.ValueKind != JsonValueKind.Object)
        {
            writer.WriteString("type", "unknown");
            writer.WriteEndObject();
            return;
        }

        (string? type, bool nullable) = ReadType(schema);
        if (type is null)
        {
            if (schema.TryGetProperty("properties", out _))
            {
                type = "object";
            }
            else if (schema.TryGetProperty("items", out _) || schema.TryGetProperty("prefixItems", out _))
            {
                type = "array";
            }
        }

        if (type is not null)
        {
            writer.WriteString("type", type);
        }

        if (nullable || forceNullable)
        {
            writer.WriteBoolean("nullable", true);
        }

        // The recognised numeric/string format (int32, int64, date-time, email, uri, uuid, …) drives the control.
        if (schema.TryGetProperty("format", out JsonElement format) && format.ValueKind == JsonValueKind.String)
        {
            writer.WriteString("format", format.GetString());
        }

        if (schema.TryGetProperty("description", out JsonElement description) && description.ValueKind == JsonValueKind.String)
        {
            writer.WriteString("description", description.GetString());
        }

        if (schema.TryGetProperty("enum", out JsonElement enumValues) && enumValues.ValueKind == JsonValueKind.Array)
        {
            writer.WritePropertyName("enum");
            enumValues.WriteTo(writer);
        }

        // Validation/annotation constraints — copied verbatim so the UI can constrain the control.
        foreach (string keyword in ConstraintKeywords)
        {
            if (schema.TryGetProperty(keyword, out JsonElement value))
            {
                writer.WritePropertyName(keyword);
                value.WriteTo(writer);
            }
        }

        if (type == "object" && schema.TryGetProperty("properties", out JsonElement properties) && properties.ValueKind == JsonValueKind.Object)
        {
            writer.WritePropertyName("properties");
            writer.WriteStartObject();
            foreach (var property in properties.EnumerateObject())
            {
                writer.WritePropertyName(property.Name);
                WriteTypeDescriptor(writer, property.Value, documentRoot, depth + 1);
            }

            writer.WriteEndObject();

            if (schema.TryGetProperty("required", out JsonElement required) && required.ValueKind == JsonValueKind.Array)
            {
                writer.WritePropertyName("required");
                required.WriteTo(writer);
            }
        }

        if (type == "object" && TryAdditionalSchema(schema, "additionalProperties", "unevaluatedProperties", out JsonElement valueSchema))
        {
            // A free-form map (open object): arbitrary keys whose values follow this schema.
            writer.WritePropertyName("additionalProperties");
            WriteTypeDescriptor(writer, valueSchema, documentRoot, depth + 1);
        }

        if (type == "array")
        {
            // A tuple: positional `prefixItems` (2020-12) or the legacy array-form `items`.
            JsonElement prefixItems = default;
            bool isTuple = (schema.TryGetProperty("prefixItems", out prefixItems) && prefixItems.ValueKind == JsonValueKind.Array)
                || (schema.TryGetProperty("items", out prefixItems) && prefixItems.ValueKind == JsonValueKind.Array);
            if (isTuple)
            {
                writer.WritePropertyName("prefixItems");
                writer.WriteStartArray();
                foreach (JsonElement element in prefixItems.EnumerateArray())
                {
                    WriteTypeDescriptor(writer, element, documentRoot, depth + 1);
                }

                writer.WriteEndArray();

                // The schema for any items beyond the fixed prefix (2020-12 `items`, or legacy `additionalItems`).
                if (TryAdditionalSchema(schema, "additionalItems", "unevaluatedItems", out JsonElement extraItems)
                    || (schema.TryGetProperty("items", out extraItems) && extraItems.ValueKind == JsonValueKind.Object))
                {
                    writer.WritePropertyName("items");
                    WriteTypeDescriptor(writer, extraItems, documentRoot, depth + 1);
                }
            }
            else if (schema.TryGetProperty("items", out JsonElement items) && items.ValueKind == JsonValueKind.Object)
            {
                writer.WritePropertyName("items");
                WriteTypeDescriptor(writer, items, documentRoot, depth + 1);
            }
        }

        writer.WriteEndObject();
    }

    private static (string? Type, bool Nullable) ReadType(JsonElement schema)
    {
        if (schema.TryGetProperty("type", out JsonElement type))
        {
            if (type.ValueKind == JsonValueKind.String)
            {
                bool nullableFromKeyword = schema.TryGetProperty("nullable", out JsonElement n) && n.ValueKind == JsonValueKind.True;
                return (type.GetString(), nullableFromKeyword);
            }

            if (type.ValueKind == JsonValueKind.Array)
            {
                string? primary = null;
                bool nullable = false;
                foreach (JsonElement member in type.EnumerateArray())
                {
                    if (member.ValueKind == JsonValueKind.String)
                    {
                        string? value = member.GetString();
                        if (value == "null")
                        {
                            nullable = true;
                        }
                        else
                        {
                            primary ??= value;
                        }
                    }
                }

                return (primary, nullable);
            }
        }

        return (null, schema.TryGetProperty("nullable", out JsonElement nb) && nb.ValueKind == JsonValueKind.True);
    }

    /// <summary>
    /// Reads the branches of a simple polymorphic union (<c>oneOf</c>/<c>anyOf</c>), separating out a pure
    /// <c>null</c> branch (so a <c>X | null</c> union collapses to a nullable <c>X</c>).
    /// </summary>
    private static bool TryReadUnionVariants(JsonElement schema, out List<JsonElement> variants, out bool hasNull)
    {
        variants = [];
        hasNull = false;

        JsonElement union;
        if (!((schema.TryGetProperty("oneOf", out union) && union.ValueKind == JsonValueKind.Array)
            || (schema.TryGetProperty("anyOf", out union) && union.ValueKind == JsonValueKind.Array)))
        {
            return false;
        }

        foreach (JsonElement branch in union.EnumerateArray())
        {
            if (IsPureNullSchema(branch))
            {
                hasNull = true;
            }
            else
            {
                variants.Add(branch);
            }
        }

        return true;
    }

    /// <summary>Whether a schema is exactly the null type (<c>{ "type": "null" }</c> or <c>{ "type": ["null"] }</c>).</summary>
    private static bool IsPureNullSchema(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object || !schema.TryGetProperty("type", out JsonElement type))
        {
            return false;
        }

        if (type.ValueKind == JsonValueKind.String)
        {
            return type.GetString() == "null";
        }

        if (type.ValueKind == JsonValueKind.Array)
        {
            bool any = false;
            foreach (JsonElement member in type.EnumerateArray())
            {
                any = true;
                if (member.ValueKind != JsonValueKind.String || member.GetString() != "null")
                {
                    return false;
                }
            }

            return any;
        }

        return false;
    }

    /// <summary>
    /// Reads the <c>discriminator.propertyName</c> — the property whose value selects the variant. This is an
    /// <em>OpenAPI extension</em>, not a JSON Schema keyword (there is no <c>discriminator</c> in JSON Schema
    /// 2020-12 or earlier), so it is read opportunistically when present on an OpenAPI source and simply absent
    /// otherwise; the UI degrades to labelling variants by title/type when there is no discriminator.
    /// </summary>
    private static bool TryDiscriminatorProperty(JsonElement schema, out string propertyName)
    {
        propertyName = string.Empty;
        if (schema.TryGetProperty("discriminator", out JsonElement discriminator)
            && discriminator.ValueKind == JsonValueKind.Object
            && discriminator.TryGetProperty("propertyName", out JsonElement name)
            && name.ValueKind == JsonValueKind.String)
        {
            propertyName = name.GetString() ?? string.Empty;
        }

        return propertyName.Length > 0;
    }

    /// <summary>Reads an open-shape value schema from the first of two keywords whose value is an object schema.</summary>
    private static bool TryAdditionalSchema(JsonElement schema, string primary, string fallback, out JsonElement valueSchema)
    {
        if (schema.TryGetProperty(primary, out valueSchema) && valueSchema.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        if (schema.TryGetProperty(fallback, out valueSchema) && valueSchema.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        valueSchema = default;
        return false;
    }

    // ---- navigation helpers ---------------------------------------------------------------------

    /// <summary>Navigates an object schema to one of its <c>properties</c> by name (resolving <c>$ref</c>).</summary>
    private static bool TryNavigateProperty(JsonElement schema, JsonElement documentRoot, string propertyName, out JsonElement result)
    {
        schema = SchemaClassifier.ResolveRef(schema, documentRoot);
        if (schema.TryGetProperty("properties", out JsonElement properties)
            && properties.ValueKind == JsonValueKind.Object
            && properties.TryGetProperty(propertyName, out result))
        {
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Navigates a JSON Pointer (e.g. <c>/items/name</c>) <em>into a JSON Schema</em> — object segments select a
    /// <c>properties</c> member, array index segments select the <c>items</c> schema — resolving <c>$ref</c> at
    /// each step. (This is schema-semantic navigation, distinct from resolving a pointer against data.)
    /// </summary>
    private static bool TryNavigatePointer(JsonElement schema, JsonElement documentRoot, string? jsonPointer, out JsonElement result)
    {
        result = schema;
        if (string.IsNullOrEmpty(jsonPointer))
        {
            return true;
        }

        foreach (string rawSegment in jsonPointer.TrimStart('/').Split('/'))
        {
            if (rawSegment.Length == 0)
            {
                continue;
            }

            string segment = rawSegment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            JsonElement current = SchemaClassifier.ResolveRef(result, documentRoot);

            if (current.TryGetProperty("properties", out JsonElement properties)
                && properties.ValueKind == JsonValueKind.Object
                && properties.TryGetProperty(segment, out JsonElement property))
            {
                result = property;
            }
            else if (current.TryGetProperty("items", out JsonElement items))
            {
                result = items;
            }
            else
            {
                result = default;
                return false;
            }
        }

        return true;
    }

    private static void WriteScalar(Utf8JsonWriter writer, string type)
    {
        writer.WriteStartObject();
        writer.WriteString("type", type);
        writer.WriteEndObject();
    }

    private static void WriteUnknown(Utf8JsonWriter writer) => WriteScalar(writer, "unknown");

    private static string? TryGetString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}