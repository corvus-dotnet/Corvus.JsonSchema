// <copyright file="RunStartInitiator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;

namespace Corvus.Text.Json.Arazzo.Durability.Anchoring;

/// <summary>
/// The initiator's side of a sealed start (ADR 0065 decision 9): seals a run's inputs to the environment's published
/// seal key under the binding for the run it names, and signs the seal with the initiator's own key. This is what the
/// CLI and a tenant-hosted trigger host do; the control plane never can, since it holds no initiator key the runner
/// pins. An initiator pins the seal key's fingerprint before it seals to it: a control plane that published a key of
/// its own would otherwise read every input.
/// </summary>
public static class RunStartInitiator
{
    /// <summary>Seals and signs a run's inputs.</summary>
    /// <param name="sealPublicKey">The environment's seal public key for <paramref name="keyId"/>, as SubjectPublicKeyInfo.</param>
    /// <param name="keyId">The seal key generation.</param>
    /// <param name="environmentId">The environment the run is pinned to.</param>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The workflow version.</param>
    /// <param name="runId">The run id the initiator chose (the run-id grammar).</param>
    /// <param name="inputs">The inputs document, as UTF-8 JSON.</param>
    /// <param name="initiatorKey">The initiator's P-256 signing key, whose public half the runner pins.</param>
    /// <returns>The sealed inputs.</returns>
    /// <exception cref="ArgumentException">The run id is outside the grammar.</exception>
    /// <exception cref="CryptographicException">A key is not a P-256 key.</exception>
    public static SealedInputs Seal(ReadOnlySpan<byte> sealPublicKey, string keyId, string environmentId, string baseWorkflowId, int versionNumber, string runId, ReadOnlySpan<byte> inputs, ECDsa initiatorKey)
    {
        ArgumentNullException.ThrowIfNull(initiatorKey);
        ArgumentException.ThrowIfNullOrEmpty(runId);
        if (!WorkflowRunId.IsWellFormed(runId))
        {
            throw new ArgumentException("The run id is outside the run-id grammar: 32 lowercase hex characters.", nameof(runId));
        }

        byte[] binding = new byte[SealedStartSignature.BindingLength(environmentId, baseWorkflowId, keyId, runId)];
        SealedStartSignature.WriteBinding(environmentId, baseWorkflowId, versionNumber, keyId, runId, binding);
        byte[] enc = new byte[InputSeal.EncLength];
        byte[] ciphertext = new byte[InputSeal.SealedLength(inputs.Length)];
        InputSeal.Seal(sealPublicKey, SealedStartSignature.SealInfo, binding, inputs, enc, ciphertext);
        byte[] signature = new byte[SealedStartSignature.SignatureLength];
        SealedStartSignature.Sign(initiatorKey, binding, enc, ciphertext, signature);
        return new SealedInputs(keyId, enc, ciphertext, signature);
    }

    /// <summary>The fingerprint an initiator pins a seal key by: the base64 SHA-256 of its SubjectPublicKeyInfo.</summary>
    /// <param name="sealPublicKey">The seal public key, as SubjectPublicKeyInfo.</param>
    /// <returns>The fingerprint.</returns>
    public static string SealKeyFingerprint(ReadOnlySpan<byte> sealPublicKey)
        => Convert.ToBase64String(SHA256.HashData(sealPublicKey));

    /// <summary>A fresh run id in the run-id grammar, for an initiator that names the run at random.</summary>
    /// <returns>32 lowercase hex characters of fresh entropy.</returns>
    public static string NewRunId()
        => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
}