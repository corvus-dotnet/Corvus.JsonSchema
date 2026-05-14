// <copyright file="ClientOperation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Represents a single API operation in the client model.
/// </summary>
public readonly struct ClientOperation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClientOperation"/> struct.
    /// </summary>
    /// <param name="pathTemplate">The path template (e.g. <c>/pets/{petId}</c>).</param>
    /// <param name="method">The HTTP method or messaging action.</param>
    /// <param name="operationId">The operation ID, or <see langword="null"/>.</param>
    /// <param name="summary">The operation summary, or <see langword="null"/>.</param>
    /// <param name="description">The operation description, or <see langword="null"/>.</param>
    /// <param name="tags">Tags declared on the operation.</param>
    /// <param name="parameters">The parameters for this operation.</param>
    /// <param name="requestBody">The request body, if any.</param>
    /// <param name="responses">The declared responses.</param>
    public ClientOperation(
        string pathTemplate,
        OperationMethod method,
        string? operationId,
        string? summary,
        string? description,
        string[] tags,
        ClientParameter[] parameters,
        ClientRequestBody? requestBody,
        ClientResponse[] responses)
    {
        this.PathTemplate = pathTemplate;
        this.Method = method;
        this.OperationId = operationId;
        this.Summary = summary;
        this.Description = description;
        this.Tags = tags;
        this.Parameters = parameters;
        this.RequestBody = requestBody;
        this.Responses = responses;
    }

    /// <summary>
    /// Gets the path template (e.g. <c>/pets/{petId}</c>).
    /// </summary>
    public string PathTemplate { get; }

    /// <summary>
    /// Gets the HTTP method or messaging action.
    /// </summary>
    public OperationMethod Method { get; }

    /// <summary>
    /// Gets the operation ID, or <see langword="null"/> if not declared.
    /// </summary>
    public string? OperationId { get; }

    /// <summary>
    /// Gets the operation summary, or <see langword="null"/> if not declared.
    /// </summary>
    public string? Summary { get; }

    /// <summary>
    /// Gets the operation description, or <see langword="null"/> if not declared.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets the tags declared on this operation.
    /// </summary>
    public string[] Tags { get; }

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
    /// Gets a suitable method name for this operation. Uses the operation ID
    /// if available, otherwise synthesizes from the method and path template.
    /// </summary>
    /// <returns>A PascalCase method name.</returns>
    public string GetMethodName()
    {
        if (this.OperationId is not null)
        {
            return ToPascalCase(this.OperationId);
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

        string pathPart = this.PathTemplate
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