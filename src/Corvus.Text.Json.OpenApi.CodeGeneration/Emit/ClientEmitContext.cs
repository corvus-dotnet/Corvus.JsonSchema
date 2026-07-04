// <copyright file="ClientEmitContext.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// The version-neutral, per-generate-call context threaded from the
/// <see cref="ClientEmitDriver"/> into an <see cref="IClientEmitter"/>.
/// </summary>
/// <remarks>
/// <para>
/// A driver computes this once per generate call (via <see cref="IClientEmitter.PrepareContext"/>)
/// and passes the same instance to every per-tag and cross-cutting emit. It carries the raw
/// spec root and resolver (for emitters that still read version-specific metadata not present in
/// the shared intermediate representation), the effective root server, and any document-level
/// metadata an emitter extracted up front (the document identity and security schemes).
/// </para>
/// </remarks>
/// <param name="SpecRoot">The root element of the parsed spec document.</param>
/// <param name="ReferenceResolver">The reference resolver (never <see langword="null"/>).</param>
/// <param name="RootServer">The effective root server, or <see langword="null"/> if none.</param>
/// <param name="DocumentSelf">
/// The document identity URI (<c>$self</c>), or <see langword="null"/> if absent or not extracted
/// by this emitter's version.
/// </param>
/// <param name="SecuritySchemes">
/// The document security schemes, or <see langword="null"/> if not extracted by this emitter's
/// version.
/// </param>
public readonly record struct ClientEmitContext(
    JsonElement SpecRoot,
    IOpenApiReferenceResolver ReferenceResolver,
    ServerInfo? RootServer,
    string? DocumentSelf = null,
    SecuritySchemeInfo[]? SecuritySchemes = null);