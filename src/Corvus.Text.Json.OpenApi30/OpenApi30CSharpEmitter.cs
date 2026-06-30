// <copyright file="OpenApi30CSharpEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi.CodeGeneration;

namespace Corvus.Text.Json.OpenApi30;

/// <summary>
/// The C# <see cref="IClientEmitter"/> for OpenAPI 3.0: a thin subclass of the shared
/// <see cref="OpenApiCSharpEmitterBase"/>.
/// </summary>
/// <remarks>
/// <para>
/// OpenAPI 3.0 carries no document-identity or security-scheme metadata into the generated client,
/// so this subclass adds nothing to the shared emitter beyond a strongly-typed constructor: it uses
/// the base's default <see cref="OpenApiCSharpEmitterBase.PrepareContext"/> (no extraction from the
/// typed model). The 3.2-only emit in the base is gated on intermediate-representation fields that
/// the 3.0 walker leaves at their defaults, so it is inert here.
/// </para>
/// </remarks>
internal sealed class OpenApi30CSharpEmitter : OpenApiCSharpEmitterBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApi30CSharpEmitter"/> class.
    /// </summary>
    /// <param name="rootNamespace">The root namespace for generated code.</param>
    /// <param name="clientNamePrefix">Optional prefix for client type names.</param>
    /// <param name="ignoreEmptyFormUrlEncodedBody">
    /// When <see langword="true"/>, form-urlencoded request bodies whose schema defines no
    /// properties are treated as if the body were absent.
    /// </param>
    /// <param name="schemaTypeResolver">The schema-type resolver.</param>
    /// <param name="walker">The walker (used for client-name synthesis).</param>
    public OpenApi30CSharpEmitter(
        string rootNamespace,
        string? clientNamePrefix,
        bool ignoreEmptyFormUrlEncodedBody,
        ISchemaTypeResolver schemaTypeResolver,
        OpenApi30Walker walker)
        : base(rootNamespace, clientNamePrefix, ignoreEmptyFormUrlEncodedBody, schemaTypeResolver, walker)
    {
    }
}