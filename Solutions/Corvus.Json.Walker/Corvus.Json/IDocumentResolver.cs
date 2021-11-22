// <copyright file="IDocumentResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json
{
    using System;
    using System.Text.Json;
    using System.Threading.Tasks;

    /// <summary>
    /// A factory which can resolve a <see cref="JsonElement"/>
    /// from a <see cref="JsonReference"/>.
    /// </summary>
    /// <remarks>It is disposable so that it can manage the lifetime of the cached documents.</remarks>
    public interface IDocumentResolver : IDisposable
    {
        /// <summary>
        /// Gets the element from the document at the given <see cref="JsonReference.Uri"/> in the <paramref name="reference"/>.
        /// </summary>
        /// <param name="reference">The reference containing the document URI.</param>
        /// <returns>A <see cref="Task{TResult}"/> which provides the <see cref="JsonDocument"/>, or <c>null</c> if it could not be retrieved.</returns>
        Task<JsonElement?> TryResolve(JsonReference reference);

        /// <summary>
        /// Add an existing document to the cache.
        /// </summary>
        /// <param name="uri">The URI of the document.</param>
        /// <param name="document">The document to add.</param>
        /// <returns><c>True</c> if the document was added, otherwise false.</returns>
        bool AddDocument(string uri, JsonDocument document);

        /// <summary>
        /// Reset the document resolver.
        /// </summary>
        void Reset();
    }
}