// <copyright file="OperationSecurityRequirementSet.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// One element of an OpenAPI <c>security</c> array (a "Security Requirement Object"): an
/// alternative that, on its own, satisfies the operation's security.
/// </summary>
/// <remarks>
/// The schemes within a set are AND'd; the alternatives across the array are OR'd. An empty
/// object (<c>{}</c>) marks anonymous access as an accepted alternative.
/// </remarks>
/// <param name="Requirements">The schemes that must all be satisfied for this alternative.</param>
/// <param name="IsOptional">Whether this alternative permits anonymous access.</param>
public readonly record struct OperationSecurityRequirementSet(
    OperationSecurityRequirement[] Requirements,
    bool IsOptional);