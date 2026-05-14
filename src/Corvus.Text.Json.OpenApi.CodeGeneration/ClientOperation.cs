// <copyright file="ClientOperation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Represents a single API operation in the client model.
/// </summary>
/// <remarks>
/// <para>
/// Stores <see cref="JsonElement"/> references into the parsed document.
/// No strings are allocated at construction time; use the <c>Get*</c> methods
/// to extract string values at the code-emission boundary.
/// </para>
/// </remarks>
public readonly struct ClientOperation
{
    private static readonly ReadOnlyMemory<byte> OperationIdUtf8 = "operationId"u8.ToArray();
    private static readonly ReadOnlyMemory<byte> SummaryUtf8 = "summary"u8.ToArray();
    private static readonly ReadOnlyMemory<byte> DescriptionUtf8 = "description"u8.ToArray();
    private static readonly ReadOnlyMemory<byte> TagsUtf8 = "tags"u8.ToArray();

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientOperation"/> struct.
    /// </summary>
    /// <param name="pathProperty">The path property (name = path template, value = path item).</param>
    /// <param name="operation">The operation element from the parsed document.</param>
    /// <param name="method">The HTTP method or messaging action.</param>
    /// <param name="parameters">The parameters for this operation.</param>
    /// <param name="requestBody">The request body, if any.</param>
    /// <param name="responses">The declared responses.</param>
    public ClientOperation(
        JsonProperty<JsonElement> pathProperty,
        JsonElement operation,
        OperationMethod method,
        ClientParameter[] parameters,
        ClientRequestBody? requestBody,
        ClientResponse[] responses)
    {
        this.PathProperty = pathProperty;
        this.Operation = operation;
        this.Method = method;
        this.Parameters = parameters;
        this.RequestBody = requestBody;
        this.Responses = responses;
    }

    /// <summary>
    /// Gets the path property from the paths map.
    /// The property name is the path template (e.g. <c>/pets/{petId}</c>).
    /// </summary>
    public JsonProperty<JsonElement> PathProperty { get; }

    /// <summary>
    /// Gets the operation element from the parsed document.
    /// </summary>
    public JsonElement Operation { get; }

    /// <summary>
    /// Gets the HTTP method or messaging action.
    /// </summary>
    public OperationMethod Method { get; }

    /// <summary>
    /// Gets the parameters for this operation.
    /// </summary>
    public ClientParameter[] Parameters { get; }

    /// <summary>
    /// Gets the request body, if any.
    /// </summary>
    public ClientRequestBody? RequestBody { get; }

    /// <summary>
    /// Gets the declared responses.
    /// </summary>
    public ClientResponse[] Responses { get; }

    /// <summary>
    /// Gets the path template string. Allocates on each call.
    /// </summary>
    /// <returns>The path template (e.g. <c>/pets/{petId}</c>).</returns>
    /// <remarks>This allocates a string. Call only at the code-emission boundary.</remarks>
    public string GetPathTemplate() => this.PathProperty.Name;

    /// <summary>
    /// Gets the operation ID, or <see langword="null"/> if not declared.
    /// </summary>
    /// <returns>The operation ID string, or <see langword="null"/>.</returns>
    /// <remarks>This allocates a string. Call only at the code-emission boundary.</remarks>
    public string? GetOperationId() => GetOptionalString(this.Operation, OperationIdUtf8.Span);

    /// <summary>
    /// Gets the operation summary, or <see langword="null"/> if not declared.
    /// </summary>
    /// <returns>The summary string, or <see langword="null"/>.</returns>
    /// <remarks>This allocates a string. Call only at the code-emission boundary.</remarks>
    public string? GetSummary() => GetOptionalString(this.Operation, SummaryUtf8.Span);

    /// <summary>
    /// Gets the operation description, or <see langword="null"/> if not declared.
    /// </summary>
    /// <returns>The description string, or <see langword="null"/>.</returns>
    /// <remarks>This allocates a string. Call only at the code-emission boundary.</remarks>
    public string? GetDescription() => GetOptionalString(this.Operation, DescriptionUtf8.Span);

    /// <summary>
    /// Gets the tags declared on this operation.
    /// </summary>
    /// <returns>The tag strings, or an empty array.</returns>
    /// <remarks>This allocates strings. Call only at the code-emission boundary.</remarks>
    public string[] GetTags()
    {
        if (!this.Operation.TryGetProperty(TagsUtf8.Span, out JsonElement tags)
            || tags.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        List<string> result = [];

        foreach (JsonElement tag in tags.EnumerateArray())
        {
            if (tag.ValueKind == JsonValueKind.String)
            {
                result.Add(tag.GetString()!);
            }
        }

        return [.. result];
    }

    /// <summary>
    /// Gets a suitable method name for this operation. Uses the operation ID
    /// if available, otherwise synthesizes from the method and path template.
    /// </summary>
    /// <returns>A PascalCase method name.</returns>
    /// <remarks>This allocates a string. Call only at the code-emission boundary.</remarks>
    public string GetMethodName()
    {
        string? operationId = this.GetOperationId();

        if (operationId is not null)
        {
            return ToPascalCase(operationId);
        }

        // Synthesize from method + path: GET /pets/{petId} → GetPetsPetId
        string methodPrefix = this.Method switch
        {
            OperationMethod.Get => "get",
            OperationMethod.Put => "put",
            OperationMethod.Post => "post",
            OperationMethod.Delete => "delete",
            OperationMethod.Options => "options",
            OperationMethod.Head => "head",
            OperationMethod.Patch => "patch",
            OperationMethod.Trace => "trace",
            _ => this.Method.ToString().ToLowerInvariant(),
        };

        string pathPart = this.GetPathTemplate()
            .Replace("/", " ", StringComparison.Ordinal)
            .Replace("{", string.Empty, StringComparison.Ordinal)
            .Replace("}", string.Empty, StringComparison.Ordinal)
            .Trim();

        return ToPascalCase(methodPrefix + " " + pathPart);
    }

    private static string? GetOptionalString(JsonElement element, ReadOnlySpan<byte> propertyName)
    {
        if (element.TryGetProperty(propertyName, out JsonElement value)
            && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }

    private static string ToPascalCase(string input)
    {
        var sb = new System.Text.StringBuilder(input.Length);
        bool capitalizeNext = true;

        foreach (char c in input)
        {
            if (c is ' ' or '_' or '-')
            {
                capitalizeNext = true;
                continue;
            }

            sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
            capitalizeNext = false;
        }

        return sb.ToString();
    }
}