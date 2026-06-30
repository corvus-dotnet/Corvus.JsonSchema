// <copyright file="IClientEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Turns the shared OpenAPI intermediate representation (a list of <see cref="OperationInfo"/>)
/// into generated source files for a single target language.
/// </summary>
/// <remarks>
/// <para>
/// An emitter is the language-specific half of the OpenAPI code generator: the
/// <see cref="OpenApiWalkerBase">walk</see> produces the language-neutral intermediate
/// representation, and an emitter consumes it. The grouping and sequencing of the emit (which
/// operations and tags are visited, in what order, and which cross-cutting files are produced)
/// is owned by the version-neutral <see cref="ClientEmitDriver"/>; an emitter only provides the
/// per-item building blocks.
/// </para>
/// <para>
/// The client members (request and response modules, the per-tag client interface and
/// implementation) are required. The server members are optional: an emitter that targets a
/// client-only language returns <see langword="null"/> from them (the default implementations do),
/// and the driver skips any <see langword="null"/> result.
/// </para>
/// </remarks>
public interface IClientEmitter
{
    /// <summary>
    /// Computes the per-generate-call <see cref="ClientEmitContext"/> once, before any per-item
    /// emit. An emitter that needs document-level metadata not carried by the intermediate
    /// representation (the document identity, security schemes) extracts it here.
    /// </summary>
    /// <param name="specRoot">The root element of the parsed spec document.</param>
    /// <param name="referenceResolver">The reference resolver (never <see langword="null"/>).</param>
    /// <param name="rootServer">The effective root server, or <see langword="null"/> if none.</param>
    /// <returns>The context threaded into every subsequent emit for this call.</returns>
    ClientEmitContext PrepareContext(
        JsonElement specRoot,
        IOpenApiReferenceResolver referenceResolver,
        ServerInfo? rootServer);

    /// <summary>
    /// Emits the request module (the per-operation request struct) for an operation.
    /// </summary>
    /// <param name="op">The operation.</param>
    /// <returns>The generated file.</returns>
    GeneratedFile EmitRequestModule(OperationInfo op);

    /// <summary>
    /// Emits the response module (the per-operation response struct) for an operation.
    /// </summary>
    /// <param name="op">The operation.</param>
    /// <param name="allOperations">All operations being emitted in this call.</param>
    /// <returns>The generated file.</returns>
    GeneratedFile EmitResponseModule(OperationInfo op, IReadOnlyList<OperationInfo> allOperations);

    /// <summary>
    /// Emits the client interface for a tag.
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <param name="tagOperations">The operations carrying the tag.</param>
    /// <param name="context">The per-generate-call context.</param>
    /// <returns>The generated file.</returns>
    GeneratedFile EmitClientInterface(
        string tag,
        IReadOnlyList<OperationInfo> tagOperations,
        ClientEmitContext context);

    /// <summary>
    /// Emits the client implementation for a tag.
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <param name="tagOperations">The operations carrying the tag.</param>
    /// <param name="context">The per-generate-call context.</param>
    /// <returns>The generated file.</returns>
    GeneratedFile EmitClientImplementation(
        string tag,
        IReadOnlyList<OperationInfo> tagOperations,
        ClientEmitContext context);

    /// <summary>
    /// Emits the server parameter module for an operation, or <see langword="null"/> if the
    /// target language does not produce server-side code.
    /// </summary>
    /// <param name="op">The operation.</param>
    /// <returns>The generated file, or <see langword="null"/>.</returns>
    GeneratedFile? EmitServerParamsModule(OperationInfo op) => null;

    /// <summary>
    /// Emits the server result module for an operation, or <see langword="null"/> if the target
    /// language does not produce server-side code.
    /// </summary>
    /// <param name="op">The operation.</param>
    /// <returns>The generated file, or <see langword="null"/>.</returns>
    GeneratedFile? EmitServerResultModule(OperationInfo op) => null;

    /// <summary>
    /// Emits the server handler module (interface) for a tag, or <see langword="null"/> if the
    /// target language does not produce server-side code.
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <param name="tagOperations">The operations carrying the tag.</param>
    /// <returns>The generated file, or <see langword="null"/>.</returns>
    GeneratedFile? EmitServerHandlerModule(
        string tag,
        IReadOnlyList<OperationInfo> tagOperations) => null;

    /// <summary>
    /// Emits the cross-cutting server endpoint-registration module, or <see langword="null"/> if
    /// the target language does not produce server-side code.
    /// </summary>
    /// <param name="groups">The operations grouped by tag.</param>
    /// <param name="operations">All operations being emitted in this call.</param>
    /// <param name="isCallbackServer">
    /// <see langword="true"/> when registering webhook/callback endpoints rather than the main
    /// <c>paths</c> endpoints.
    /// </param>
    /// <param name="context">The per-generate-call context.</param>
    /// <returns>The generated file, or <see langword="null"/>.</returns>
    GeneratedFile? EmitServerRegistrationModule(
        IReadOnlyDictionary<string, List<OperationInfo>> groups,
        IReadOnlyList<OperationInfo> operations,
        bool isCallbackServer,
        ClientEmitContext context) => null;
}