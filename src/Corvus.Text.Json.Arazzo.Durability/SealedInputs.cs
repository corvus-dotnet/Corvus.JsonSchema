// <copyright file="SealedInputs.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Anchoring;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A run's start inputs as an initiator sealed and signed them (ADR 0065 decision 9): the seal key generation, the
/// seal's encapsulated key and ciphertext, and the initiator's signature over the binding and the seal. This is what
/// the genesis row of a sealed start carries, byte for byte, and what the runner opens at first claim; nothing that
/// holds one can read the inputs.
/// </summary>
/// <param name="KeyId">The seal key generation the inputs are wrapped to.</param>
/// <param name="Enc">The encapsulated key (<see cref="InputSeal.EncLength"/> bytes).</param>
/// <param name="Ciphertext">The sealed inputs: ciphertext and tag.</param>
/// <param name="Signature">The initiator's signature (<see cref="SealedStartSignature.SignatureLength"/> bytes).</param>
public readonly record struct SealedInputs(string KeyId, ReadOnlyMemory<byte> Enc, ReadOnlyMemory<byte> Ciphertext, ReadOnlyMemory<byte> Signature)
{
    /// <summary>Whether the parts have the shapes a seal produces: a key generation, a P-256 point, a ciphertext at least a tag long, and a P1363 signature.</summary>
    public bool IsWellFormed
        => !string.IsNullOrEmpty(this.KeyId)
            && this.Enc.Length == InputSeal.EncLength
            && this.Ciphertext.Length >= InputSeal.TagLength
            && this.Signature.Length == SealedStartSignature.SignatureLength;
}