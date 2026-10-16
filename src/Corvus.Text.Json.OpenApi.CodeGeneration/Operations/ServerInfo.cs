// <copyright file="ServerInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Operations;

/// <summary>
/// A server entry: a URL template and the variables it substitutes.
/// </summary>
/// <param name="UrlTemplate">The server URL, with <c>{name}</c> placeholders for its variables.</param>
/// <param name="Variables">The server variables, in document order; empty when the URL has none.</param>
public readonly record struct ServerInfo(
    string UrlTemplate,
    ServerVariableInfo[] Variables);