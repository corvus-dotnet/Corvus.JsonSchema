// <copyright file="AuditChainVerificationOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Execution;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>What a chain is verified against, beyond its own links (ADR 0069).</summary>
public sealed class AuditChainVerificationOptions
{
    /// <summary>
    /// Gets the trust store the heads' signatures are checked against: the audit's public keys, by key id. Without one
    /// the heads are not checked, and the result says so.
    /// </summary>
    public IExecutorPackageVerifier? TrustStore { get; init; }

    /// <summary>
    /// Gets a record hash to look for among the verified records (64 lowercase hex digits). It is how a caller checks
    /// the hash a later chain says it continues from.
    /// </summary>
    public ReadOnlyMemory<byte> ExpectedHash { get; init; }

    /// <summary>
    /// Gets an anchor the chain must hold: a signed head published outside the sink, through the span and the log. The
    /// chain holds it where the record at the anchor's sequence is a head carrying the anchor's previous-hash. A chain
    /// that was rewritten after the anchor was published cannot, which is what the anchor is for.
    /// </summary>
    public AuditHead? Anchor { get; init; }
}