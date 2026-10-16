// <copyright file="OperationSecurityRequirement.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Operations;

/// <summary>
/// One scheme within a security requirement alternative.
/// </summary>
/// <param name="SchemeName">The scheme's key in <c>components.securitySchemes</c>.</param>
/// <param name="Scopes">The scopes required of an <c>oauth2</c> or <c>openIdConnect</c> scheme; empty for other scheme types.</param>
/// <param name="SchemeType">The scheme <c>type</c>, when the scheme was found in the components; otherwise <see langword="null"/>.</param>
public readonly record struct OperationSecurityRequirement(
    string SchemeName,
    string[] Scopes,
    string? SchemeType = null);