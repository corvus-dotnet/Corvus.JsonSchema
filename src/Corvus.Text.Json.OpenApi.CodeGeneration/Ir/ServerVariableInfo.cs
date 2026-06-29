// <copyright file="ServerVariableInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// The emit-boundary description of an OpenAPI server variable.
/// </summary>
/// <param name="Name">The variable name.</param>
/// <param name="DefaultValue">The default value.</param>
/// <param name="AllowedValues">The optional enumerated allowed values.</param>
public readonly record struct ServerVariableInfo(
    string Name,
    string DefaultValue,
    string[]? AllowedValues);