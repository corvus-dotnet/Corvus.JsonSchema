// <copyright file="ClientOperation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Represents a single API operation in the client model, backed by element
/// references into the parsed spec document.
/// </summary>
/// <remarks>
/// <para>
/// All element references are valid only while the source document is alive.
/// String properties are extracted at the emitter boundary via the <c>Get*</c>
/// methods — no strings are allocated during model construction.
/// </para>
/// </remarks>
public readonly struct ClientOperation
{
    private static ReadOnlySpan<byte> OperationIdUtf8 => "operationId"u8;

    private static ReadOnlySpan<byte> SummaryUtf8 => "summary"u8;

    private static ReadOnlySpan<byte> DescriptionUtf8 => "description"u8;

    private static ReadOnlySpan<byte> TagsUtf8 => "tags"u8;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientOperation"/> struct.
    /// </summary>
    /// <param name="pathProperty">
    /// The path property from the paths map. The property name is the path template.
    /// </param>
    /// <param name="method">The HTTP method or messaging action.</param>
    /// <param name="operation">The operation element from the spec.</param>
    /// <param name="parameters">Pre-navigated parameters for this operation.</param>
    /// <param name="requestBody">The request body, if any.</param>
    /// <param name="responses">Pre-navigated responses for this operation.</param>
    public ClientOperation(
        JsonProperty<JsonElement> pathProperty,
        OperationMethod method,
        JsonElement operation,
        ClientParameter[] parameters,
        ClientRequestBody? requestBody,
        ClientResponse[] responses)
    {
        this.PathProperty = pathProperty;
        this.Method = method;
        this.Operation = operation;
        this.Parameters = parameters;
        this.RequestBody = requestBody;
        this.Responses = responses;
    }

    /// <summary>
    /// Gets the path property from the paths map. The property name is the path
    /// template (e.g. <c>/pets/{petId}</c>).
    /// </summary>
    public JsonProperty<JsonElement> PathProperty { get; }

    /// <summary>
    /// Gets the HTTP method or messaging action.
    /// </summary>
    public OperationMethod Method { get; }

    /// <summary>
    /// Gets the operation element from the spec document.
    /// </summary>
    public JsonElement Operation { get; }

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
    /// Gets the path template string (e.g. <c>/pets/{petId}</c>).
    /// </summary>
    /// <returns>The path template.</returns>
    /// <remarks>This allocates a string. Call only at the code-emission boundary.</remarks>
    public string GetPathTemplate() => this.PathProperty.Name;

    /// <summary>
    /// Gets the operation ID from the spec.
    /// </summary>
    /// <returns>The operation ID, or <see langword="null"/> if not present.</returns>
    /// <remarks>This allocates a string. Call only at the code-emission boundary.</remarks>
    public string? GetOperationId()
    {
        if (this.Operation.TryGetProperty(OperationIdUtf8, out JsonElement id)
            && id.ValueKind == JsonValueKind.String)
        {
            return id.GetString();
        }

        return null;
    }

    /// <summary>
    /// Gets the summary from the spec.
    /// </summary>
    /// <returns>The summary, or <see langword="null"/> if not present.</returns>
    /// <remarks>This allocates a string. Call only at the code-emission boundary.</remarks>
    public string? GetSummary()
    {
        if (this.Operation.TryGetProperty(SummaryUtf8, out JsonElement summary)
            && summary.ValueKind == JsonValueKind.String)
        {
            return summary.GetString();
        }

        return null;
    }

    /// <summary>
    /// Gets the description from the spec.
    /// </summary>
    /// <returns>The description, or <see langword="null"/> if not present.</returns>
    /// <remarks>This allocates a string. Call only at the code-emission boundary.</remarks>
    public string? GetDescription()
    {
        if (this.Operation.TryGetProperty(DescriptionUtf8, out JsonElement desc)
            && desc.ValueKind == JsonValueKind.String)
        {
            return desc.GetString();
        }

        return null;
    }

    /// <summary>
    /// Gets the tags from the spec.
    /// </summary>
    /// <returns>An array of tag strings, or an empty array if no tags are present.</returns>
    /// <remarks>This allocates strings. Call only at the code-emission boundary.</remarks>
    public string[] GetTags()
    {
        if (!this.Operation.TryGetProperty(TagsUtf8, out JsonElement tags)
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

        string pathTemplate = this.GetPathTemplate();
        string pathPart = pathTemplate
            .Replace("/", " ", StringComparison.Ordinal)
            .Replace("{", string.Empty, StringComparison.Ordinal)
            .Replace("}", string.Empty, StringComparison.Ordinal)
            .Trim();

        return ToPascalCase(methodPrefix + " " + pathPart);
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