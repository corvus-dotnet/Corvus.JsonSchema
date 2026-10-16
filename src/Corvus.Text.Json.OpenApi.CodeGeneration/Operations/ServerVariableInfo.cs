// <copyright file="ServerVariableInfo.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Operations;

/// <summary>
/// A server URL variable.
/// </summary>
/// <param name="Name">The variable name as it appears in the URL template.</param>
/// <param name="DefaultValue">The variable's default value.</param>
/// <param name="AllowedValues">The variable's <c>enum</c> values, or <see langword="null"/> when any value is allowed.</param>
public readonly record struct ServerVariableInfo(
    string Name,
    string DefaultValue,
    string[]? AllowedValues);