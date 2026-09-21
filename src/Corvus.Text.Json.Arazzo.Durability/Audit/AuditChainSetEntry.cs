// <copyright file="AuditChainSetEntry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>One chain of a verified set.</summary>
/// <param name="Name">The name the chain was given.</param>
/// <param name="Verification">What verifying the chain on its own found.</param>
/// <param name="Standing">Where the chain stands beside the others.</param>
public readonly record struct AuditChainSetEntry(string Name, AuditChainVerification Verification, AuditChainStanding Standing);