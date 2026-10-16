// <copyright file="IOpenApiClientEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.CodeGeneration;
using Corvus.Text.Json.OpenApi.CodeGeneration.Operations;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Emits a client for one target language from a prepared <see cref="OpenApiClientModel"/>. One implementation per
/// language; the CLI selects it by <see cref="LanguageId"/>.
/// </summary>
public interface IOpenApiClientEmitter
{
    /// <summary>
    /// Gets the language identifier the CLI selects this emitter by (see <see cref="LanguageIds"/>).
    /// </summary>
    string LanguageId { get; }

    /// <summary>
    /// Emits the client for the operations under <c>paths</c>.
    /// </summary>
    /// <param name="model">The prepared operations, in the <see cref="OperationScope.Paths"/> scope.</param>
    /// <param name="types">The generated model types the client refers to, by schema pointer.</param>
    /// <param name="options">The emission options.</param>
    /// <returns>The generated files, with names relative to the output directory.</returns>
    IReadOnlyList<GeneratedFile> EmitClient(in OpenApiClientModel model, IModelTypeMap types, ClientEmitOptions options);

    /// <summary>
    /// Emits the client that invokes the document's webhooks and callbacks.
    /// </summary>
    /// <param name="model">The prepared operations, in the <see cref="OperationScope.WebhooksAndCallbacks"/> scope.</param>
    /// <param name="types">The generated model types the client refers to, by schema pointer.</param>
    /// <param name="options">The emission options.</param>
    /// <returns>The generated files, with names relative to the output directory.</returns>
    IReadOnlyList<GeneratedFile> EmitCallbackClient(in OpenApiClientModel model, IModelTypeMap types, ClientEmitOptions options);
}