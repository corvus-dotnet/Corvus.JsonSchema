// <copyright file="LinkParameterBinding.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Operations;

/// <summary>
/// One parameter binding of a link.
/// </summary>
/// <param name="ParameterName">The target operation's parameter name.</param>
/// <param name="Expression">The runtime expression, or literal, that supplies the value.</param>
public readonly record struct LinkParameterBinding(
    string ParameterName,
    string Expression);