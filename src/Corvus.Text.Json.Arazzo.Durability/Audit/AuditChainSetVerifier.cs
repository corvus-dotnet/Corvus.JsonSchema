// <copyright file="AuditChainSetVerifier.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.Execution;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Verifies the chains of an audit sink together (ADR 0069). One chain shows what happened to its own records; the set
/// shows what happened to chains. A writer that abandons or fills a chain says in the next chain where it continues
/// from, so a chain removed whole is named by its successor, a chain cut short no longer holds the hash its successor
/// names, and a torn last line is told apart from a truncation by whether a successor continues from the record before
/// it. Anchors, which are published outside the sink, name chains too.
/// </summary>
public static class AuditChainSetVerifier
{
    /// <summary>Verifies the chains together.</summary>
    /// <param name="chains">The chains of the sink.</param>
    /// <param name="trustStore">The audit's public keys, or <see langword="null"/> to leave the heads' signatures unchecked.</param>
    /// <param name="anchors">The anchors held outside the sink, each of which the chain it names must hold.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>What was found.</returns>
    public static async ValueTask<AuditChainSetVerification> VerifyAsync(IReadOnlyList<AuditChainSource> chains, IExecutorPackageVerifier? trustStore = null, IReadOnlyList<AuditHead>? anchors = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chains);
        anchors ??= [];

        // Each chain on its own first: that is what tells which chain is which, since a chain's id is in its records.
        var verifications = new AuditChainVerification[chains.Count];
        for (int i = 0; i < chains.Count; i++)
        {
            verifications[i] = await VerifyOneAsync(chains[i], new AuditChainVerificationOptions { TrustStore = trustStore }, cancellationToken).ConfigureAwait(false);
        }

        // Then each anchor against the chain it names. An anchor is a property of a whole chain, so it is checked only of
        // a chain with no break of its own, a torn tail aside, since a chain that tears after the anchor still holds it.
        // Any other break is the more specific finding.
        var unmatched = new List<AuditHead>();
        foreach (AuditHead anchor in anchors)
        {
            int index = IndexOf(verifications, anchor.ChainId);
            if (index < 0)
            {
                unmatched.Add(anchor);
            }
            else if (verifications[index].Break is AuditChainBreak.None or AuditChainBreak.TornTail)
            {
                AuditChainVerification anchored = await VerifyOneAsync(chains[index], new AuditChainVerificationOptions { TrustStore = trustStore, Anchor = anchor }, cancellationToken).ConfigureAwait(false);
                if (!anchored.HoldsAnchor)
                {
                    verifications[index] = anchored with { Break = AuditChainBreak.AnchorNotFound, BreakLine = 0 };
                }
            }
        }

        var entries = new AuditChainSetEntry[chains.Count];
        for (int i = 0; i < chains.Count; i++)
        {
            entries[i] = new AuditChainSetEntry(
                chains[i].Name,
                verifications[i],
                await StandingAsync(i, chains, verifications, trustStore, cancellationToken).ConfigureAwait(false),
                ContinuedFromItsLastRecordBy(verifications[i], verifications));
        }

        return new AuditChainSetVerification(entries, unmatched);
    }

    private static async ValueTask<AuditChainStanding> StandingAsync(int index, IReadOnlyList<AuditChainSource> chains, AuditChainVerification[] verifications, IExecutorPackageVerifier? trustStore, CancellationToken cancellationToken)
    {
        AuditChainVerification verification = verifications[index];
        if (verification.ChainId is null)
        {
            return verification.Break is AuditChainBreak.None or AuditChainBreak.TornTail ? AuditChainStanding.Empty : AuditChainStanding.Broken;
        }

        if (verification.Break == AuditChainBreak.TornTail)
        {
            if (ContinuedFromItsLastRecordBy(verification, verifications) is null)
            {
                return AuditChainStanding.Broken;
            }
        }
        else if (verification.Break != AuditChainBreak.None)
        {
            return AuditChainStanding.Broken;
        }

        if (verification.ContinuesChain is { } predecessorId)
        {
            int predecessor = IndexOf(verifications, predecessorId);
            if (predecessor < 0)
            {
                return AuditChainStanding.PredecessorMissing;
            }

            AuditChainVerification named = await VerifyOneAsync(
                chains[predecessor],
                new AuditChainVerificationOptions { TrustStore = trustStore, ExpectedHash = Encoding.UTF8.GetBytes(verification.ContinuesHash!) },
                cancellationToken).ConfigureAwait(false);
            if (!named.ContainsExpectedHash)
            {
                return AuditChainStanding.ContinuationNotFound;
            }
        }

        return verification.Break == AuditChainBreak.TornTail ? AuditChainStanding.AbandonedAndContinued : AuditChainStanding.Verified;
    }

    private static string? ContinuedFromItsLastRecordBy(in AuditChainVerification chain, AuditChainVerification[] verifications)
    {
        if (chain.ChainId is null)
        {
            return null;
        }

        foreach (AuditChainVerification other in verifications)
        {
            if (other.ContinuesChain == chain.ChainId && other.ContinuesHash == chain.LastHash)
            {
                return other.ChainId;
            }
        }

        return null;
    }

    private static int IndexOf(AuditChainVerification[] verifications, string chainId)
    {
        for (int i = 0; i < verifications.Length; i++)
        {
            if (verifications[i].ChainId == chainId)
            {
                return i;
            }
        }

        return -1;
    }

    private static async ValueTask<AuditChainVerification> VerifyOneAsync(AuditChainSource chain, AuditChainVerificationOptions options, CancellationToken cancellationToken)
    {
        Stream stream = chain.Open();
        await using (stream.ConfigureAwait(false))
        {
            return await AuditChainVerifier.VerifyAsync(stream, options, cancellationToken).ConfigureAwait(false);
        }
    }
}