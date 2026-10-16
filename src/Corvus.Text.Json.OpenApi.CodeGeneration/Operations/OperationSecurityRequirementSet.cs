// <copyright file="OperationSecurityRequirementSet.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Operations;

/// <summary>
/// One element of an OpenAPI <c>security</c> array (a Security Requirement Object): an alternative that, on its
/// own, satisfies the operation's security. The schemes within it are combined with AND; the alternatives across
/// the array are combined with OR. An empty object (<c>{}</c>) marks anonymous access as an accepted alternative.
/// </summary>
/// <param name="Requirements">The schemes this alternative requires, all of which must be satisfied.</param>
/// <param name="IsOptional">Whether this alternative is the empty object, so the operation may be called anonymously.</param>
public readonly record struct OperationSecurityRequirementSet(
    OperationSecurityRequirement[] Requirements,
    bool IsOptional);