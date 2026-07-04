// <copyright file="ServerInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// The emit-boundary description of an OpenAPI server.
/// </summary>
/// <param name="UrlTemplate">The server URL template.</param>
/// <param name="Variables">The server variables.</param>
public readonly record struct ServerInfo(
    string UrlTemplate,
    ServerVariableInfo[] Variables);