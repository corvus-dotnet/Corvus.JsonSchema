// <copyright file="LinkParameterBinding.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// The emit-boundary description of a single parameter binding within a
/// <see cref="LinkInfo"/>.
/// </summary>
/// <param name="ParameterName">The bound parameter name.</param>
/// <param name="Expression">The binding expression.</param>
public readonly record struct LinkParameterBinding(
    string ParameterName,
    string Expression);