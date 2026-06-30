// <copyright file="ClientEmitDriver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Owns the version-neutral grouping and sequencing of the OpenAPI emit phase and drives an
/// <see cref="IClientEmitter"/> over the shared intermediate representation.
/// </summary>
/// <remarks>
/// <para>
/// This is the half of the generator that does not depend on the target language: it groups the
/// operations by tag and decides the order in which the emitter's per-operation, per-tag and
/// cross-cutting building blocks are invoked. The same driver serves every emitter (C#,
/// TypeScript, …) so the emit order is defined once.
/// </para>
/// </remarks>
public sealed class ClientEmitDriver
{
    /// <summary>
    /// Drives the client emit: per-operation request and response modules, then the per-tag
    /// client interface and implementation.
    /// </summary>
    /// <param name="emitter">The target-language emitter.</param>
    /// <param name="operations">The operations to emit (the shared intermediate representation).</param>
    /// <param name="specRoot">The root element of the parsed spec document.</param>
    /// <param name="referenceResolver">The reference resolver (never <see langword="null"/>).</param>
    /// <param name="rootServer">The effective root server, or <see langword="null"/> if none.</param>
    /// <returns>The generated source files.</returns>
    public IReadOnlyList<GeneratedFile> EmitClient(
        IClientEmitter emitter,
        IReadOnlyList<OperationInfo> operations,
        JsonElement specRoot,
        IOpenApiReferenceResolver referenceResolver,
        ServerInfo? rootServer)
    {
        ClientEmitContext context = emitter.PrepareContext(specRoot, referenceResolver, rootServer);

        List<GeneratedFile> files = [];
        Dictionary<string, List<OperationInfo>> groups = OpenApiWalkerBase.GroupOperationsByTag([.. operations]);

        foreach (OperationInfo op in operations)
        {
            files.Add(emitter.EmitRequestModule(op));
            files.Add(emitter.EmitResponseModule(op, operations));
        }

        foreach ((string tag, List<OperationInfo> tagOps) in groups)
        {
            files.Add(emitter.EmitClientInterface(tag, tagOps, context));
            files.Add(emitter.EmitClientImplementation(tag, tagOps, context));
        }

        return files;
    }

    /// <summary>
    /// Drives the server emit: per-operation parameter and result types, then the per-tag handler
    /// interface, then the cross-cutting endpoint registration.
    /// </summary>
    /// <param name="emitter">The target-language emitter.</param>
    /// <param name="operations">The operations to emit (the shared intermediate representation).</param>
    /// <param name="specRoot">The root element of the parsed spec document.</param>
    /// <param name="referenceResolver">The reference resolver (never <see langword="null"/>).</param>
    /// <param name="rootServer">The effective root server, or <see langword="null"/> if none.</param>
    /// <param name="isCallbackServer">
    /// <see langword="true"/> when emitting webhook/callback endpoints rather than the main
    /// <c>paths</c> endpoints.
    /// </param>
    /// <returns>The generated source files.</returns>
    public IReadOnlyList<GeneratedFile> EmitServer(
        IClientEmitter emitter,
        IReadOnlyList<OperationInfo> operations,
        JsonElement specRoot,
        IOpenApiReferenceResolver referenceResolver,
        ServerInfo? rootServer,
        bool isCallbackServer)
    {
        ClientEmitContext context = emitter.PrepareContext(specRoot, referenceResolver, rootServer);

        List<GeneratedFile> files = [];
        Dictionary<string, List<OperationInfo>> groups = OpenApiWalkerBase.GroupOperationsByTag([.. operations]);

        foreach (OperationInfo op in operations)
        {
            if (emitter.EmitServerParamsModule(op) is { } paramsFile)
            {
                files.Add(paramsFile);
            }

            if (emitter.EmitServerResultModule(op) is { } resultFile)
            {
                files.Add(resultFile);
            }
        }

        foreach ((string tag, List<OperationInfo> tagOps) in groups)
        {
            if (emitter.EmitServerHandlerModule(tag, tagOps) is { } handlerFile)
            {
                files.Add(handlerFile);
            }
        }

        if (emitter.EmitServerRegistrationModule(groups, operations, isCallbackServer, context) is { } registrationFile)
        {
            files.Add(registrationFile);
        }

        return files;
    }
}