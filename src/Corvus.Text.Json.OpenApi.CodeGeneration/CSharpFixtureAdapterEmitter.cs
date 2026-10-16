// <copyright file="CSharpFixtureAdapterEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi.CodeGeneration.Operations;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Emits the test-only fixture adapter for a generated C# client: one entry point that maps a fixture case's JSON
/// inputs onto the typed client method of an operation, and projects the typed outcome back to JSON. The adapter is
/// what lets the language-neutral fixtures in <c>tests/OpenApi-Client-Fixtures</c> drive the strongly typed client.
/// </summary>
/// <remarks>
/// The adapter consumes only the promoted operation model and the type map, so one emitter serves every spec version.
/// Operations whose inputs the adapter cannot yet build (multipart bodies with binary parts, parameters without a
/// generated type) report themselves as unsupported rather than failing to compile.
/// </remarks>
public static class CSharpFixtureAdapterEmitter
{
    /// <summary>
    /// The file name the adapter is written to.
    /// </summary>
    public const string FileName = "FixtureAdapter.cs";

    /// <summary>
    /// Emits the adapter.
    /// </summary>
    /// <param name="model">The prepared operations.</param>
    /// <param name="types">The generated model types, by schema pointer.</param>
    /// <param name="options">The emission options.</param>
    /// <returns>The adapter file.</returns>
    public static GeneratedFile Emit(in OpenApiClientModel model, IModelTypeMap types, ClientEmitOptions options)
    {
        IndentedWriter w = new();
        CodeEmitHelpers.EmitHeader(w);
        w.WriteLine("using System.IO;");
        w.WriteLine("using System.Text;");
        w.WriteLine();
        w.WriteLine($"namespace {options.Namespace};");
        w.WriteLine();
        w.WriteLine("/// <summary>");
        w.WriteLine("/// Test-only: drives the generated client from language-neutral fixture cases.");
        w.WriteLine("/// </summary>");
        w.WriteLine("public static class FixtureAdapter");
        w.OpenBrace();

        EmitInvoke(w, model.Operations);

        foreach (OperationInfo op in model.Operations)
        {
            EmitOperation(w, op, types, options);
        }

        EmitHelpers(w);
        w.CloseBrace();
        return new GeneratedFile(FileName, w.ToString());
    }

    private static string OperationKey(in OperationInfo op) => op.OperationId ?? op.CSharpMethodName();

    private static void EmitInvoke(IndentedWriter w, OperationInfo[] operations)
    {
        w.WriteLine("/// <summary>");
        w.WriteLine("/// Invokes an operation from a fixture case's <c>input</c> and returns the outcome as JSON.");
        w.WriteLine("/// </summary>");
        w.WriteLine("public static async ValueTask<string> InvokeAsync(string operationId, JsonElement input, IApiTransport transport, CancellationToken cancellationToken = default)");
        w.OpenBrace();
        w.WriteLine("try");
        w.OpenBrace();
        w.WriteLine("switch (operationId)");
        w.OpenBrace();
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (OperationInfo op in operations)
        {
            string key = OperationKey(in op);
            if (!seen.Add(key))
            {
                continue;
            }

            w.WriteLine($"case {CodeEmitHelpers.FormatStringLiteral(key)}:");
            w.PushIndent();
            w.WriteLine($"return await {op.CSharpMethodName()}Async(input, transport, cancellationToken).ConfigureAwait(false);");
            w.PopIndent();
        }

        w.WriteLine("default:");
        w.PushIndent();
        w.WriteLine("return Unsupported($\"no operation '{operationId}'\");");
        w.PopIndent();
        w.CloseBrace();
        w.CloseBrace();
        w.WriteLine("catch (Exception ex) when (ex is not OperationCanceledException)");
        w.OpenBrace();
        w.WriteLine("return Error(ex);");
        w.CloseBrace();
        w.CloseBrace();
        w.WriteLine();
    }

    private static void EmitOperation(IndentedWriter w, in OperationInfo op, IModelTypeMap types, ClientEmitOptions options)
    {
        string tag = op.Tags.Length > 0 ? op.Tags[0] : "default";
        string clientType = $"{options.ClientName ?? "Api"}{CodeEmitHelpers.SanitizeIdentifier(tag)}Client";
        string responseType = $"{op.CSharpMethodName()}Response";

        w.WriteLine($"private static async ValueTask<string> {op.CSharpMethodName()}Async(JsonElement input, IApiTransport transport, CancellationToken cancellationToken)");
        w.OpenBrace();

        string? unsupported = Unsupportable(in op, types);
        if (unsupported is not null)
        {
            w.WriteLine($"await Task.CompletedTask.ConfigureAwait(false);");
            w.WriteLine($"return Unsupported({CodeEmitHelpers.FormatStringLiteral(unsupported)});");
            w.CloseBrace();
            w.WriteLine();
            return;
        }

        w.WriteLine($"{clientType} client = new(transport);");
        w.WriteLine("ValidationMode validationMode = ReadValidationMode(input, \"validation\", ValidationMode.Basic);");
        w.WriteLine("ValidationMode responseValidationMode = ReadValidationMode(input, \"responseValidation\", ValidationMode.None);");

        List<string> arguments = [];

        foreach (ParameterInfo param in op.Parameters)
        {
            string identifier = CodeEmitHelpers.EscapeCSharpKeyword(CodeEmitHelpers.SanitizeParameterName(param.Name));
            string local = "p_" + CodeEmitHelpers.SanitizeParameterName(param.Name);
            string typeName = TypeFor(types, param.SchemaPointer);
            string section = param.Location switch
            {
                ParameterLocation.Path => "path",
                ParameterLocation.Query => "query",
                ParameterLocation.Header => "header",
                _ => "cookie",
            };

            if (param.IsRequired)
            {
                w.WriteLine($"if (!TryGetInput(input, \"{section}\", {CodeEmitHelpers.FormatStringLiteral(param.Name)}, out JsonElement {local}Input))");
                w.OpenBrace();
                w.WriteLine($"return Unsupported(\"missing required input {section}.{param.Name}\");");
                w.CloseBrace();
                w.WriteLine();
                w.WriteLine($"{typeName} {local} = {typeName}.ParseValue({local}Input.GetRawText());");
            }
            else
            {
                w.WriteLine($"{typeName}.Source {local} = TryGetInput(input, \"{section}\", {CodeEmitHelpers.FormatStringLiteral(param.Name)}, out JsonElement {local}Input) ? ({typeName}.Source){typeName}.ParseValue({local}Input.GetRawText()) : default;");
            }

            arguments.Add($"{identifier}: {local}");
        }

        if (op.RequestBody is { } body)
        {
            ContentInfo? typed = TypedBodyContent(body);
            if (typed is { } content)
            {
                string typeName = TypeFor(types, content.SchemaPointer);
                if (body.IsRequired)
                {
                    w.WriteLine("if (!TryGetBody(input, out JsonElement bodyInput))");
                    w.OpenBrace();
                    w.WriteLine("return Unsupported(\"missing required input body\");");
                    w.CloseBrace();
                    w.WriteLine();
                    w.WriteLine($"{typeName} p_body = {typeName}.ParseValue(bodyInput.GetRawText());");
                }
                else
                {
                    w.WriteLine($"{typeName}.Source p_body = TryGetBody(input, out JsonElement bodyInput) ? ({typeName}.Source){typeName}.ParseValue(bodyInput.GetRawText()) : default;");
                }
            }
            else
            {
                w.WriteLine("Stream? p_body = ReadRawBody(input);");
                if (body.IsRequired)
                {
                    w.WriteLine("if (p_body is null)");
                    w.OpenBrace();
                    w.WriteLine("return Unsupported(\"missing required input bodyText or bodyBase64\");");
                    w.CloseBrace();
                }
            }

            arguments.Add("body: p_body");
        }

        arguments.Add("cancellationToken: cancellationToken");
        arguments.Add("validationMode: validationMode");
        arguments.Add("responseValidationMode: responseValidationMode");

        w.WriteLine();
        w.WriteLine($"await using {responseType} response = await client.{op.CSharpMethodName()}Async({string.Join(", ", arguments)}).ConfigureAwait(false);");
        w.WriteLine();
        w.WriteLine("string? caseName = null;");
        w.WriteLine("string? bodyJson = null;");
        w.WriteLine("string? bodyText = null;");
        w.WriteLine("string? bodyBase64 = null;");

        EmitResponseProjection(w, in op);
        EmitHeaderProjection(w, in op);

        w.WriteLine("return WriteResult(response.StatusCode, caseName, bodyJson, bodyText, bodyBase64, headers);");
        w.CloseBrace();
        w.WriteLine();
    }

    private static void EmitResponseProjection(IndentedWriter w, in OperationInfo op)
    {
        bool first = true;
        int index = 0;

        // Specific statuses first, then ranges, then default: the client matches in that order too.
        foreach (ResponseInfo resp in op.Responses.OrderBy(r => r.StatusCode == "default" ? 2 : r.StatusCode.Length == 3 && r.StatusCode[1] == 'X' ? 1 : 0))
        {
            string name = CodeEmitHelpers.StatusCodeToName(resp.StatusCode);

            // Streaming content (OpenAPI 3.2 itemSchema) is exposed through Enumerate{Name}Items, not a body accessor; phase C.
            HashSet<ContentCategory> categories = [.. resp.Content.Where(c => c.ItemSchemaPointer is null).Select(c => CodeEmitHelpers.ClassifyMediaType(c.MediaType))];

            if (categories.Contains(ContentCategory.Json))
            {
                EmitBranch(w, ref first, $"response.TryGet{name}(out var body{index})", name, $"bodyJson = JsonElement.From(in body{index}).GetRawText();");
                index++;
            }

            if (categories.Contains(ContentCategory.TextPlain))
            {
                EmitBranch(w, ref first, $"response.TryGet{name}String(out string? text{index})", name, $"bodyText = text{index};");
                index++;
            }

            if (categories.Contains(ContentCategory.OctetStream))
            {
                EmitBranch(w, ref first, $"response.TryGet{name}Stream(out Stream? stream{index})", name, $"bodyBase64 = await ReadBase64Async(stream{index}, cancellationToken).ConfigureAwait(false);");
                index++;
            }

            string statusCondition = resp.StatusCode switch
            {
                "default" => "true",
                _ when resp.StatusCode.Length == 3 && resp.StatusCode[1] == 'X' => $"response.StatusCode / 100 == {resp.StatusCode[0]}",
                _ => $"response.StatusCode == {resp.StatusCode}",
            };

            EmitBranch(w, ref first, statusCondition, name, null);
        }

        w.WriteLine();
    }

    private static void EmitBranch(IndentedWriter w, ref bool first, string condition, string caseName, string? assignment)
    {
        w.WriteLine($"{(first ? "if" : "else if")} ({condition})");
        w.OpenBrace();
        w.WriteLine($"caseName = {CodeEmitHelpers.FormatStringLiteral(caseName)};");
        if (assignment is not null)
        {
            w.WriteLine(assignment);
        }

        w.CloseBrace();
        first = false;
    }

    private static void EmitHeaderProjection(IndentedWriter w, in OperationInfo op)
    {
        HashSet<string> emitted = new(StringComparer.Ordinal);
        List<string> entries = [];

        foreach (ResponseInfo resp in op.Responses)
        {
            foreach (HeaderInfo header in resp.Headers)
            {
                string property = CodeEmitHelpers.HeaderNameToPropertyName(header.HeaderName) + "Header";
                if (!emitted.Add(property))
                {
                    continue;
                }

                string name = CodeEmitHelpers.FormatStringLiteral(header.HeaderName.ToLowerInvariant());
                entries.Add(header.SchemaPointer is not null
                    ? $"({name}, response.{property}.IsNotUndefined() ? JsonElement.From(response.{property}).GetRawText() : null, true)"
                    : $"({name}, response.{property}, false)");
            }
        }

        w.WriteLine("(string Name, string? Value, bool IsJson)[] headers =");
        w.WriteLine("[");
        w.PushIndent();
        foreach (string entry in entries)
        {
            w.WriteLine(entry + ",");
        }

        w.PopIndent();
        w.WriteLine("];");
        w.WriteLine();
    }

    private static string? Unsupportable(in OperationInfo op, IModelTypeMap types)
    {
        foreach (ParameterInfo param in op.Parameters)
        {
            if (param.SchemaPointer is null || !types.TryGetTypeReference(param.SchemaPointer, out _))
            {
                return $"parameter '{param.Name}' has no generated type";
            }
        }

        if (op.RequestBody is { } body)
        {
            if (body.BinaryProperties.Length > 0 || body.PrefixParts is not null || body.ItemPart is not null)
            {
                return "multipart bodies with binary parts are not supported by the adapter yet";
            }

            if (TypedBodyContent(body) is { } content && (content.SchemaPointer is null || !types.TryGetTypeReference(content.SchemaPointer, out _)))
            {
                return "the request body has no generated type";
            }
        }

        return null;
    }

    private static ContentInfo? TypedBodyContent(in RequestBodyInfo body)
    {
        foreach (ContentInfo content in body.Content)
        {
            if (CodeEmitHelpers.IsJsonMediaType(content.MediaType)
                || CodeEmitHelpers.IsFormUrlEncodedMediaType(content.MediaType)
                || CodeEmitHelpers.IsMultipartMediaType(content.MediaType))
            {
                return content;
            }
        }

        return null;
    }

    private static string TypeFor(IModelTypeMap types, string? pointer)
        => pointer is not null && types.TryGetTypeReference(pointer, out string? name) ? name : types.FallbackTypeReference;

    private static void EmitHelpers(IndentedWriter w)
    {
        w.WriteLine("private static bool TryGetInput(JsonElement input, string section, string name, out JsonElement value)");
        w.OpenBrace();
        w.WriteLine("value = default;");
        w.WriteLine("return input.ValueKind == JsonValueKind.Object");
        w.PushIndent();
        w.WriteLine("&& input.TryGetProperty(section, out JsonElement sectionValue)");
        w.WriteLine("&& sectionValue.ValueKind == JsonValueKind.Object");
        w.WriteLine("&& sectionValue.TryGetProperty(name, out value);");
        w.PopIndent();
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("private static bool TryGetBody(JsonElement input, out JsonElement value)");
        w.OpenBrace();
        w.WriteLine("value = default;");
        w.WriteLine("return input.ValueKind == JsonValueKind.Object && input.TryGetProperty(\"body\", out value);");
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("private static Stream? ReadRawBody(JsonElement input)");
        w.OpenBrace();
        w.WriteLine("if (input.ValueKind != JsonValueKind.Object)");
        w.OpenBrace();
        w.WriteLine("return null;");
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("if (input.TryGetProperty(\"bodyText\", out JsonElement text) && text.ValueKind == JsonValueKind.String)");
        w.OpenBrace();
        w.WriteLine("return new MemoryStream(Encoding.UTF8.GetBytes(text.GetString()!));");
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("if (input.TryGetProperty(\"bodyBase64\", out JsonElement base64) && base64.ValueKind == JsonValueKind.String)");
        w.OpenBrace();
        w.WriteLine("return new MemoryStream(Convert.FromBase64String(base64.GetString()!));");
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("return null;");
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("private static ValidationMode ReadValidationMode(JsonElement input, string name, ValidationMode fallback)");
        w.OpenBrace();
        w.WriteLine("if (input.ValueKind == JsonValueKind.Object && input.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String)");
        w.OpenBrace();
        w.WriteLine("return value.GetString() switch");
        w.OpenBrace();
        w.WriteLine("\"none\" => ValidationMode.None,");
        w.WriteLine("\"basic\" => ValidationMode.Basic,");
        w.WriteLine("\"detailed\" => ValidationMode.Detailed,");
        w.WriteLine("_ => fallback,");
        w.CloseBraceWithSemicolon();
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("return fallback;");
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("private static async ValueTask<string> ReadBase64Async(Stream stream, CancellationToken cancellationToken)");
        w.OpenBrace();
        w.WriteLine("using MemoryStream buffer = new();");
        w.WriteLine("await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);");
        w.WriteLine("return Convert.ToBase64String(buffer.ToArray());");
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("private static string WriteResult(int status, string? caseName, string? bodyJson, string? bodyText, string? bodyBase64, (string Name, string? Value, bool IsJson)[] headers)");
        w.OpenBrace();
        w.WriteLine("ArrayBufferWriter<byte> buffer = new();");
        w.WriteLine("using (Utf8JsonWriter writer = new(buffer))");
        w.OpenBrace();
        w.WriteLine("writer.WriteStartObject();");
        w.WriteLine("writer.WriteString(\"kind\", \"ok\");");
        w.WriteLine("writer.WriteNumber(\"status\", status);");
        w.WriteLine("if (caseName is not null)");
        w.OpenBrace();
        w.WriteLine("writer.WriteString(\"case\", caseName);");
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("if (bodyJson is not null)");
        w.OpenBrace();
        w.WriteLine("writer.WritePropertyName(\"body\");");
        w.WriteLine("writer.WriteRawValue(bodyJson);");
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("if (bodyText is not null)");
        w.OpenBrace();
        w.WriteLine("writer.WriteString(\"bodyText\", bodyText);");
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("if (bodyBase64 is not null)");
        w.OpenBrace();
        w.WriteLine("writer.WriteString(\"bodyBase64\", bodyBase64);");
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("writer.WriteStartObject(\"headers\");");
        w.WriteLine("foreach ((string name, string? value, bool isJson) in headers)");
        w.OpenBrace();
        w.WriteLine("if (value is null)");
        w.OpenBrace();
        w.WriteLine("writer.WriteNull(name);");
        w.CloseBrace();
        w.WriteLine("else if (isJson)");
        w.OpenBrace();
        w.WriteLine("writer.WritePropertyName(name);");
        w.WriteLine("writer.WriteRawValue(value);");
        w.CloseBrace();
        w.WriteLine("else");
        w.OpenBrace();
        w.WriteLine("writer.WriteString(name, value);");
        w.CloseBrace();
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("writer.WriteEndObject();");
        w.WriteLine("writer.WriteEndObject();");
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("return Encoding.UTF8.GetString(buffer.WrittenSpan);");
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("private static string Unsupported(string reason) => \"{\\\"kind\\\":\\\"unsupported\\\",\\\"reason\\\":\" + Quote(reason) + \"}\";");
        w.WriteLine();
        w.WriteLine("private static string Error(Exception ex) => \"{\\\"kind\\\":\\\"error\\\",\\\"type\\\":\" + Quote(ex.GetType().Name) + \",\\\"error\\\":\" + Quote(ex.Message) + \"}\";");
        w.WriteLine();
        w.WriteLine("private static string Quote(string value)");
        w.OpenBrace();
        w.WriteLine("ArrayBufferWriter<byte> buffer = new();");
        w.WriteLine("using (Utf8JsonWriter writer = new(buffer))");
        w.OpenBrace();
        w.WriteLine("writer.WriteStringValue(value);");
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("return Encoding.UTF8.GetString(buffer.WrittenSpan);");
        w.CloseBrace();
    }
}