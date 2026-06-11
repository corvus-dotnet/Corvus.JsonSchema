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
/// workflow, the typed shape of its <c>inputs</c> and of every step's resolved <c>outputs</c> (requests and
/// responses follow) — by resolving the workflow's expressions against its referenced OpenAPI/AsyncAPI source
/// documents and normalising the resulting JSON Schema nodes into a small recursive type descriptor.
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
/// <c>properties</c>+<c>required</c> (objects) / <c>items</c> (arrays). Anything that cannot be resolved degrades
/// to <c>{ "type": "unknown" }</c> rather than failing, so the document is always producible.</para>
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

        writer.WritePropertyName(stepId);
        writer.WriteStartObject();
        writer.WritePropertyName("outputs");
        writer.WriteStartObject();

        if (step.TryGetProperty("outputs", out JsonElement outputs) && outputs.ValueKind == JsonValueKind.Object)
        {
            bool haveResponse = TryResolveResponseBodySchema(step, sources, out JsonElement responseSchema, out JsonElement responseRoot);

            foreach (var output in outputs.EnumerateObject())
            {
                writer.WritePropertyName(output.Name);
                string expression = output.Value.ValueKind == JsonValueKind.String ? output.Value.GetString() ?? string.Empty : string.Empty;
                WriteOutputDescriptor(writer, expression, workflow, workflowRoot, haveResponse, responseSchema, responseRoot);
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
        bool haveResponse,
        JsonElement responseSchema,
        JsonElement responseRoot)
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
                if (TryNavigatePointer(responseSchema, responseRoot, expr.JsonPointer, out JsonElement resolvedBody))
                {
                    WriteTypeDescriptor(writer, resolvedBody, responseRoot, 0);
                    return;
                }

                break;

            case ArazzoExprSource.Literal:
                WriteScalar(writer, "string");
                return;
        }

        // $steps.*.outputs.*, unresolved $response.body / $inputs, $message.payload, etc. — not yet resolved.
        WriteUnknown(writer);
    }

    /// <summary>
    /// Resolves a step's success response body schema (the operation's lowest 2xx, else <c>default</c>) from
    /// the referenced OpenAPI source documents. Best-effort; returns <see langword="false"/> when the step has
    /// no resolvable operation (AsyncAPI channel / sub-workflow / unresolved operation).
    /// </summary>
    private static bool TryResolveResponseBodySchema(JsonElement step, IReadOnlyDictionary<string, JsonElement> sources, out JsonElement schema, out JsonElement documentRoot)
    {
        schema = default;
        documentRoot = default;

        if (!TryResolveOperation(step, sources, out JsonElement operation, out documentRoot))
        {
            return false;
        }

        if (!operation.TryGetProperty("responses", out JsonElement responses) || responses.ValueKind != JsonValueKind.Object)
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

        if (!found || !chosen.TryGetProperty("content", out JsonElement content) || content.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var mediaType in content.EnumerateObject())
        {
            if (mediaType.Value.TryGetProperty("schema", out JsonElement bodySchema))
            {
                schema = bodySchema;
                return true;
            }
        }

        return false;
    }

    /// <summary>Resolves a step's <c>operationId</c> (or <c>operationPath</c>) to the operation object + its source document.</summary>
    private static bool TryResolveOperation(JsonElement step, IReadOnlyDictionary<string, JsonElement> sources, out JsonElement operation, out JsonElement documentRoot)
    {
        operation = default;
        documentRoot = default;

        if (TryGetString(step, "operationId") is { } operationId)
        {
            foreach (JsonElement source in sources.Values)
            {
                if (TryFindOperationById(source, operationId, out operation))
                {
                    documentRoot = source;
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
                        && source.Value.TryResolvePointer(pointer, out operation))
                    {
                        documentRoot = source.Value;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool TryFindOperationById(JsonElement openApi, string operationId, out JsonElement operation)
    {
        operation = default;
        if (!openApi.TryGetProperty("paths", out JsonElement paths) || paths.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var path in paths.EnumerateObject())
        {
            if (path.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var method in path.Value.EnumerateObject())
            {
                if (method.Value.ValueKind == JsonValueKind.Object
                    && TryGetString(method.Value, "operationId") == operationId)
                {
                    operation = method.Value;
                    return true;
                }
            }
        }

        return false;
    }

    // ---- type descriptor ------------------------------------------------------------------------

    /// <summary>Writes the normalised recursive type descriptor for a JSON Schema node.</summary>
    private static void WriteTypeDescriptor(Utf8JsonWriter writer, JsonElement schema, JsonElement documentRoot, int depth)
    {
        schema = SchemaClassifier.ResolveRef(schema, documentRoot);

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
            else if (schema.TryGetProperty("items", out _))
            {
                type = "array";
            }
        }

        if (type is not null)
        {
            writer.WriteString("type", type);
        }

        if (nullable)
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

        if (type == "array" && schema.TryGetProperty("items", out JsonElement items) && items.ValueKind == JsonValueKind.Object)
        {
            writer.WritePropertyName("items");
            WriteTypeDescriptor(writer, items, documentRoot, depth + 1);
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