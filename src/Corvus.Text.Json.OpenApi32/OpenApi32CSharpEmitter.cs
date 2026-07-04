// <copyright file="OpenApi32CSharpEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;

namespace Corvus.Text.Json.OpenApi32;

/// <summary>
/// The C# <see cref="IClientEmitter"/> for OpenAPI 3.2: a thin subclass of the shared
/// <see cref="OpenApiCSharpEmitterBase"/>.
/// </summary>
/// <remarks>
/// <para>
/// OpenAPI 3.2 is the superset the shared emitter is shaped around, so this subclass adds only the
/// one piece of emit that needs the strongly-typed model: <see cref="PrepareContext"/> dereferences
/// the typed <see cref="OpenApiDocument"/> to extract the document identity (<c>$self</c>) into the
/// <see cref="ClientEmitContext"/>. The security schemes are extracted by the shared, version-agnostic
/// <see cref="OpenApiCSharpEmitterBase.PrepareSecuritySchemes"/> raw pass inherited from the base.
/// Everything else is inherited from <see cref="OpenApiCSharpEmitterBase"/>.
/// </para>
/// </remarks>
internal sealed class OpenApi32CSharpEmitter : OpenApiCSharpEmitterBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApi32CSharpEmitter"/> class.
    /// </summary>
    /// <param name="rootNamespace">The root namespace for generated code.</param>
    /// <param name="clientNamePrefix">Optional prefix for client type names.</param>
    /// <param name="ignoreEmptyFormUrlEncodedBody">
    /// When <see langword="true"/>, form-urlencoded request bodies whose schema defines no
    /// properties are treated as if the body were absent.
    /// </param>
    /// <param name="schemaTypeResolver">The schema-type resolver.</param>
    /// <param name="walker">The walker (used for client-name synthesis).</param>
    public OpenApi32CSharpEmitter(
        string rootNamespace,
        string? clientNamePrefix,
        bool ignoreEmptyFormUrlEncodedBody,
        ISchemaTypeResolver schemaTypeResolver,
        OpenApi32Walker walker)
        : base(rootNamespace, clientNamePrefix, ignoreEmptyFormUrlEncodedBody, schemaTypeResolver, walker)
    {
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The 3.2 implementation additionally dereferences the strongly-typed
    /// <see cref="OpenApiDocument"/> to extract the document identity (<c>$self</c>) that the shared
    /// intermediate representation does not carry. The security schemes are extracted by the shared,
    /// version-agnostic <see cref="OpenApiCSharpEmitterBase.PrepareSecuritySchemes"/> raw pass (the
    /// same one that populates the 3.0/3.1 context), so the emitted security constants are byte-identical
    /// across every version.
    /// </remarks>
    public override ClientEmitContext PrepareContext(
        JsonElement specRoot,
        IOpenApiReferenceResolver referenceResolver,
        ServerInfo? rootServer)
        => new(
            specRoot,
            referenceResolver,
            rootServer,
            GetDocumentSelf(specRoot),
            PrepareSecuritySchemes(specRoot));

    private static string? GetDocumentSelf(JsonElement specRoot)
    {
        OpenApiDocument doc = specRoot;
        return doc.Self.IsNotUndefined() ? doc.Self.GetString() : null;
    }
}