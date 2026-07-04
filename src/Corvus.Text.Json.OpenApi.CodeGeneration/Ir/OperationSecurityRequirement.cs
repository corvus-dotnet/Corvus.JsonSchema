// <copyright file="OperationSecurityRequirement.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// A single named security scheme reference within an
/// <see cref="OperationSecurityRequirementSet"/>.
/// </summary>
/// <param name="SchemeName">The name of the referenced security scheme.</param>
/// <param name="Scopes">The required scopes for the scheme.</param>
/// <param name="SchemeType">The optional scheme type.</param>
public readonly record struct OperationSecurityRequirement(
    string SchemeName,
    string[] Scopes,
    string? SchemeType = null);