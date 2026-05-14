// <copyright file="ClientOperation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Represents a single API operation in the client model.
/// </summary>
public sealed class ClientOperation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClientOperation"/> class.
    /// </summary>
    /// <param name="operationId">The operation ID from the spec, used as the method name basis.</param>
    /// <param name="path">The path template (e.g. <c>/pets/{petId}</c>).</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="summary">Optional summary from the spec.</param>
    /// <param name="description">Optional description from the spec.</param>
    /// <param name="tags">Tags from the spec, used for grouping operations.</param>
    /// <param name="parameters">The parameters for this operation.</param>
    /// <param name="requestBody">The request body, if any.</param>
    /// <param name="responses">The declared responses.</param>
    public ClientOperation(
        string? operationId,
        string path,
        OperationMethod method,
        string? summary,
        string? description,
        IReadOnlyList<string> tags,
        IReadOnlyList<ClientParameter> parameters,
        ClientRequestBody? requestBody,
        IReadOnlyList<ClientResponse> responses)
    {
        this.OperationId = operationId;
        this.Path = path;
        this.Method = method;
        this.Summary = summary;
        this.Description = description;
        this.Tags = tags;
        this.Parameters = parameters;
        this.RequestBody = requestBody;
        this.Responses = responses;
    }

    /// <summary>
    /// Gets the operation ID from the spec. This is used as the basis for the
    /// generated method name. May be <see langword="null"/> if the spec omits it,
    /// in which case a name is synthesized from the path and method.
    /// </summary>
    public string? OperationId { get; }

    /// <summary>
    /// Gets the path template (e.g. <c>/pets/{petId}</c>).
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the HTTP method.
    /// </summary>
    public OperationMethod Method { get; }

    /// <summary>
    /// Gets the summary from the spec.
    /// </summary>
    public string? Summary { get; }

    /// <summary>
    /// Gets the description from the spec.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets the tags from the spec, used for grouping into client classes.
    /// </summary>
    public IReadOnlyList<string> Tags { get; }

    /// <summary>
    /// Gets the parameters for this operation.
    /// </summary>
    public IReadOnlyList<ClientParameter> Parameters { get; }

    /// <summary>
    /// Gets the request body, if any.
    /// </summary>
    public ClientRequestBody? RequestBody { get; }

    /// <summary>
    /// Gets the declared responses.
    /// </summary>
    public IReadOnlyList<ClientResponse> Responses { get; }

    /// <summary>
    /// Gets a suitable method name for this operation. Uses the <see cref="OperationId"/>
    /// if available, otherwise synthesizes from <see cref="Method"/> and <see cref="Path"/>.
    /// </summary>
    /// <returns>A PascalCase method name.</returns>
    public string GetMethodName()
    {
        if (this.OperationId is not null)
        {
            return ToPascalCase(this.OperationId);
        }

        // Synthesize from method + path: GET /pets/{petId} → GetPetsPetId
        string pathPart = this.Path
            .Replace("/", " ", StringComparison.Ordinal)
            .Replace("{", string.Empty, StringComparison.Ordinal)
            .Replace("}", string.Empty, StringComparison.Ordinal)
            .Trim();

        return ToPascalCase(this.Method.ToString().ToLowerInvariant() + " " + pathPart);
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