// <copyright file="ClientModelBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Builds a <see cref="ClientModel"/> from the output of an <see cref="ISpecWalker"/>.
/// </summary>
/// <remarks>
/// <para>
/// The walkers extract typed metadata (parameters, request bodies, responses) using
/// the strongly-typed OpenAPI schema models. The builder converts these into
/// <see cref="ClientOperation"/> instances and builds JSON pointer references to schemas.
/// </para>
/// </remarks>
public static class ClientModelBuilder
{
    /// <summary>
    /// Builds a <see cref="ClientModel"/> from a specification document.
    /// </summary>
    /// <param name="specRoot">The root element of the parsed spec document.</param>
    /// <param name="walker">The spec walker to use.</param>
    /// <param name="filter">Optional operation filter.</param>
    /// <returns>The built <see cref="ClientModel"/>.</returns>
    /// <remarks>
    /// <para>
    /// Schema pointers are collected from operation parameters, request bodies, and
    /// responses. Component schemas and transitive <c>$ref</c> targets do not need
    /// to be gathered separately — <c>{ "$ref": "..." }</c> is a valid JSON Schema,
    /// so the code generator follows references automatically.
    /// </para>
    /// </remarks>
    public static ClientModel Build(
        JsonElement specRoot,
        ISpecWalker walker,
        OperationFilter? filter = null)
    {
        List<ClientOperation> operations = [];
        List<string> schemaPointers = [];

        foreach (OperationEntry entry in walker.EnumerateOperations(specRoot, filter))
        {
            ClientOperation op = BuildOperation(entry, schemaPointers);
            operations.Add(op);
        }

        return new ClientModel(
            specRoot,
            [.. operations],
            [.. schemaPointers]);
    }

    private static ClientOperation BuildOperation(
        OperationEntry entry,
        List<string> schemaPointers)
    {
        string pathTemplate = entry.Path.Name;

        ClientParameter[] parameters = BuildParameters(entry, schemaPointers);
        ClientRequestBody? requestBody = BuildRequestBody(entry, schemaPointers);
        ClientResponse[] responses = BuildResponses(entry, schemaPointers);

        return new ClientOperation(
            pathTemplate,
            entry.Method,
            entry.OperationId,
            entry.Summary,
            entry.Description,
            entry.Tags,
            parameters,
            requestBody,
            responses);
    }

    private static ClientParameter[] BuildParameters(
        OperationEntry entry,
        List<string> schemaPointers)
    {
        if (entry.Parameters.Length == 0)
        {
            return [];
        }

        string pathTemplate = entry.Path.Name;
        string method = MethodToString(entry.Method);
        ClientParameter[] result = new ClientParameter[entry.Parameters.Length];

        for (int i = 0; i < entry.Parameters.Length; i++)
        {
            WalkedParameter walked = entry.Parameters[i];

            string? schemaPointer = null;
            if (walked.HasSchema)
            {
                schemaPointer = BuildParameterSchemaPointer(pathTemplate, method, i);
                schemaPointers.Add(schemaPointer);
            }

            result[i] = new ClientParameter(
                walked.Name,
                walked.Location,
                walked.IsRequired,
                schemaPointer,
                walked.Style,
                walked.Explode);
        }

        return result;
    }

    private static ClientRequestBody? BuildRequestBody(
        OperationEntry entry,
        List<string> schemaPointers)
    {
        if (entry.RequestBody is not { } walked)
        {
            return null;
        }

        string pathTemplate = entry.Path.Name;
        string method = MethodToString(entry.Method);

        ClientMediaTypeContent[] content = BuildContent(
            walked.Content,
            pathTemplate,
            method,
            "requestBody",
            schemaPointers);

        return new ClientRequestBody(walked.IsRequired, content, walked.Description);
    }

    private static ClientResponse[] BuildResponses(
        OperationEntry entry,
        List<string> schemaPointers)
    {
        if (entry.Responses.Length == 0)
        {
            return [];
        }

        string pathTemplate = entry.Path.Name;
        string method = MethodToString(entry.Method);
        ClientResponse[] result = new ClientResponse[entry.Responses.Length];

        for (int i = 0; i < entry.Responses.Length; i++)
        {
            WalkedResponse walked = entry.Responses[i];

            ClientMediaTypeContent[] content = BuildResponseContent(
                walked.Content,
                pathTemplate,
                method,
                walked.StatusCode,
                schemaPointers);

            result[i] = new ClientResponse(walked.StatusCode, walked.Description, content);
        }

        return result;
    }

    private static ClientMediaTypeContent[] BuildContent(
        WalkedMediaTypeContent[] walkedContent,
        string pathTemplate,
        string method,
        string parentSegment,
        List<string> schemaPointers)
    {
        if (walkedContent.Length == 0)
        {
            return [];
        }

        ClientMediaTypeContent[] result = new ClientMediaTypeContent[walkedContent.Length];

        for (int i = 0; i < walkedContent.Length; i++)
        {
            WalkedMediaTypeContent walked = walkedContent[i];

            string? schemaPointer = null;
            if (walked.HasSchema)
            {
                schemaPointer = BuildContentSchemaPointer(
                    pathTemplate, method, parentSegment, walked.MediaType);
                schemaPointers.Add(schemaPointer);
            }

            result[i] = new ClientMediaTypeContent(walked.MediaType, schemaPointer);
        }

        return result;
    }

    private static ClientMediaTypeContent[] BuildResponseContent(
        WalkedMediaTypeContent[] walkedContent,
        string pathTemplate,
        string method,
        string statusCode,
        List<string> schemaPointers)
    {
        if (walkedContent.Length == 0)
        {
            return [];
        }

        ClientMediaTypeContent[] result = new ClientMediaTypeContent[walkedContent.Length];

        for (int i = 0; i < walkedContent.Length; i++)
        {
            WalkedMediaTypeContent walked = walkedContent[i];

            string? schemaPointer = null;
            if (walked.HasSchema)
            {
                schemaPointer = BuildResponseContentSchemaPointer(
                    pathTemplate, method, statusCode, walked.MediaType);
                schemaPointers.Add(schemaPointer);
            }

            result[i] = new ClientMediaTypeContent(walked.MediaType, schemaPointer);
        }

        return result;
    }

    private static string MethodToString(OperationMethod method) =>
        method switch
        {
            OperationMethod.Get => "get",
            OperationMethod.Put => "put",
            OperationMethod.Post => "post",
            OperationMethod.Delete => "delete",
            OperationMethod.Options => "options",
            OperationMethod.Head => "head",
            OperationMethod.Patch => "patch",
            OperationMethod.Trace => "trace",
            OperationMethod.Publish => "publish",
            OperationMethod.Subscribe => "subscribe",
            _ => throw new ArgumentOutOfRangeException(nameof(method)),
        };

    // Schema pointer builders -----------------------------------------------
    // All build JSON Pointer strings using RFC 6901 escaping:
    //   ~ → ~0, / → ~1
    // The format is: #/paths/<encoded-path>/<method>/<suffix>

    /// <summary>
    /// Encodes a string as a JSON Pointer segment per RFC 6901.
    /// </summary>
    private static string EncodePointerSegment(string segment) =>
        segment.Replace("~", "~0", StringComparison.Ordinal)
               .Replace("/", "~1", StringComparison.Ordinal);

    /// <summary>
    /// Builds: #/paths/&lt;path&gt;/&lt;method&gt;/parameters/&lt;index&gt;/schema.
    /// </summary>
    private static string BuildParameterSchemaPointer(
        string pathTemplate,
        string method,
        int index)
    {
        string encodedPath = EncodePointerSegment(pathTemplate);
        return $"#/paths/{encodedPath}/{method}/parameters/{index}/schema";
    }

    /// <summary>
    /// Builds: #/paths/&lt;path&gt;/&lt;method&gt;/&lt;parentSegment&gt;/content/&lt;mediaType&gt;/schema.
    /// </summary>
    private static string BuildContentSchemaPointer(
        string pathTemplate,
        string method,
        string parentSegment,
        string mediaType)
    {
        string encodedPath = EncodePointerSegment(pathTemplate);
        string encodedMediaType = EncodePointerSegment(mediaType);
        return $"#/paths/{encodedPath}/{method}/{parentSegment}/content/{encodedMediaType}/schema";
    }

    /// <summary>
    /// Builds: #/paths/&lt;path&gt;/&lt;method&gt;/responses/&lt;statusCode&gt;/content/&lt;mediaType&gt;/schema.
    /// </summary>
    private static string BuildResponseContentSchemaPointer(
        string pathTemplate,
        string method,
        string statusCode,
        string mediaType)
    {
        string encodedPath = EncodePointerSegment(pathTemplate);
        string encodedStatus = EncodePointerSegment(statusCode);
        string encodedMediaType = EncodePointerSegment(mediaType);
        return $"#/paths/{encodedPath}/{method}/responses/{encodedStatus}/content/{encodedMediaType}/schema";
    }
}