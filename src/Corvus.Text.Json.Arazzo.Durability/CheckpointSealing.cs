// <copyright file="CheckpointSealing.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The one rule every keyless checkpoint surface applies to a submission for a sealed environment (ADR 0065 decision
/// 10): the runner API and the serverless checkpoint surface hold no key, so they cannot verify a MAC or open a
/// payload, and they require both instead. A rule stated once, because two surfaces that each restated it would be
/// two surfaces that could disagree, and the one that admitted a clear row would be the way round the other.
/// </summary>
public static class CheckpointSealing
{
    /// <summary>
    /// Whether a submission is sealed under one of an environment's active key generations: its payload is
    /// encrypted, it carries a MAC, and the generation it names is active.
    /// </summary>
    /// <param name="submission">What the submission claims.</param>
    /// <param name="activeGenerations">The environment's active key generations.</param>
    /// <returns><see langword="true"/> when the submission may be written into the sealed environment.</returns>
    public static bool IsSealedUnder(in CheckpointSubmission submission, IReadOnlySet<string> activeGenerations)
    {
        ArgumentNullException.ThrowIfNull(activeGenerations);
        return submission.Algorithm == CheckpointAlgorithm.Aes256Gcm
            && submission.HasMac
            && submission.KeyId is { } keyId
            && activeGenerations.Contains(keyId);
    }
}