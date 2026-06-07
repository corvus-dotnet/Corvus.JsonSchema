// <copyright file="SourceDescriptionClient.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Binds an Arazzo source description to the generated client the workflow planner resolves its
/// operations against (plan §3.1). The generated type names are already fully qualified in the
/// resolver's descriptors, so no namespace is carried here.
/// </summary>
/// <param name="Name">The source description <c>name</c>.</param>
/// <param name="Resolver">The resolver for the source's generated operations.</param>
public readonly record struct SourceDescriptionClient(
    string Name,
    OperationResolver Resolver);