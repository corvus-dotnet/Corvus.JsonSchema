// <copyright file="AuditChainSetVerification.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>What verifying the chains of a sink together found (ADR 0069).</summary>
/// <param name="Chains">Each chain, in the order given, with where it stands.</param>
/// <param name="UnmatchedAnchors">The anchors that name a chain not among those given. An anchor is published outside the sink, so a chain it names and the sink no longer holds was removed whole, or was not supplied.</param>
public readonly record struct AuditChainSetVerification(IReadOnlyList<AuditChainSetEntry> Chains, IReadOnlyList<AuditHead> UnmatchedAnchors)
{
    /// <summary>Gets a value indicating whether every chain stands and every anchor was matched to a chain that holds it.</summary>
    public bool IsIntact
    {
        get
        {
            if (this.UnmatchedAnchors.Count > 0)
            {
                return false;
            }

            foreach (AuditChainSetEntry chain in this.Chains)
            {
                if (chain.Standing is not (AuditChainStanding.Verified or AuditChainStanding.AbandonedAndContinued or AuditChainStanding.Empty))
                {
                    return false;
                }
            }

            return true;
        }
    }
}