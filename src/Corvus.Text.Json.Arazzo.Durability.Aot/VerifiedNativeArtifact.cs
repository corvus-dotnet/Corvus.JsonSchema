// <copyright file="VerifiedNativeArtifact.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Execution;

namespace Corvus.Text.Json.Arazzo.Durability.Aot;

/// <summary>
/// A native artifact that <see cref="WorkflowAotBuildService.VerifyNativeArtifact"/> has verified: the parsed attestation
/// together with the exact bytes it was verified from, so a deploy can hand a platform that verifies for itself (the
/// micro-guest sidecar, ADR 0063) the same binary, attestation and signature the runner just checked, and nothing is
/// re-read from the package after the check.
/// </summary>
/// <param name="Attestation">The parsed, verified attestation.</param>
/// <param name="NativeBinary">The native binary bytes whose digest the attestation names.</param>
/// <param name="AttestationUtf8">The attestation's exact UTF-8 bytes, the message the signature is over.</param>
/// <param name="SignatureUtf8">The detached signature document over <paramref name="AttestationUtf8"/>, as UTF-8 JSON.</param>
public readonly record struct VerifiedNativeArtifact(
    NativeArtifactAttestation Attestation,
    ReadOnlyMemory<byte> NativeBinary,
    ReadOnlyMemory<byte> AttestationUtf8,
    ReadOnlyMemory<byte> SignatureUtf8);