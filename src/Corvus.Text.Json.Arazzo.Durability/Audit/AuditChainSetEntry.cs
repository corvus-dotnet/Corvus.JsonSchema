// <copyright file="AuditChainSetEntry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>One chain of a verified set.</summary>
/// <param name="Name">The name the chain was given.</param>
/// <param name="Verification">What verifying the chain on its own found.</param>
/// <param name="Standing">Where the chain stands beside the others.</param>
/// <param name="FrozenBy">The id of the chain whose open record continues this one from its last verified record, or <see langword="null"/>. Where this chain has an unsigned tail, that continuation is what froze it: the tail is still vouched for by no signature of its own, and any change to it since the later chain's first head shows.</param>
public readonly record struct AuditChainSetEntry(string Name, AuditChainVerification Verification, AuditChainStanding Standing, string? FrozenBy);