// <copyright file="CallbackDocumentResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.DocumentResolvers;

/// <summary>
/// A document resolver that uses the provided callback to resolve documents.
/// </summary>
public sealed class CallbackDocumentResolver : IDocumentResolver
{
    private readonly PrepopulatedDocumentResolver resolver = new();
    private readonly BaseUriResolver resolverCallback;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallbackDocumentResolver"/> class.
    /// </summary>
    /// <param name="resolverCallback">The document resolver callback function.</param>
    public CallbackDocumentResolver(BaseUriResolver resolverCallback)
    {
        this.resolverCallback = resolverCallback;
    }

    /// <inheritdoc/>
    public bool AddDocument(string uri, JsonDocument document)
    {
        return this.resolver.AddDocument(uri, document);
    }

    /// <inheritdoc/>
    public bool AddDocument(IMetaSchema metaSchema)
        => this.AddDocument(metaSchema.Uri, metaSchema.Document);

    /// <inheritdoc/>
    public void Dispose()
    {
        this.resolver.Dispose();
    }

    /// <inheritdoc/>
    public void Reset()
    {
        this.resolver.Reset();
    }

    /// <inheritdoc/>
    public async ValueTask<JsonElement?> TryResolve(JsonReference reference)
    {
        if (await this.resolver.TryResolve(reference) is JsonElement result)
        {
            return result;
        }

        string documentUri = reference.Uri.ToString();
        JsonDocument? document = this.resolverCallback(documentUri);
        if (document is JsonDocument callbackResult)
        {
            this.resolver.AddDocument(reference, callbackResult);
        }

        // Then resolve the reference now the document has been loaded.
        return await this.resolver.TryResolve(reference);
    }
}