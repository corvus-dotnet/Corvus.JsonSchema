// <copyright file="SourceDescriptionClient.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Binds an Arazzo source description to the generated client the workflow planner resolves its
/// operations against: the source's <see cref="OperationResolver"/> and the .NET namespace its
/// client types were generated into (plan §3.1).
/// </summary>
/// <param name="Name">The source description <c>name</c>.</param>
/// <param name="Resolver">The resolver for the source's OpenAPI document.</param>
/// <param name="ClientNamespace">The root namespace the source's client types were generated into.</param>
public readonly record struct SourceDescriptionClient(
    string Name,
    OperationResolver Resolver,
    string ClientNamespace);