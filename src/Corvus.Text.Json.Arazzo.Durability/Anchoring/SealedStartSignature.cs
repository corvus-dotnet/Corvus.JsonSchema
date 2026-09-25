// <copyright file="SealedStartSignature.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability.Anchoring;

/// <summary>
/// The binding and the signature of a sealed start (ADR 0065 decision 9). The binding is the seal's associated data,
/// <c>len‖environmentId ‖ len‖baseWorkflowId ‖ uint32(versionNumber) ‖ len‖sealKeyId ‖ len‖runId</c>: a sealed
/// value opens only at the run, workflow version, environment and key generation it was sealed for, so the control
/// plane cannot move an initiator's inputs to a run of its own choosing. The signature is ES256 (ECDSA over P-256
/// with SHA-256, the registration's algorithm) over <c>len‖"arazzo-sealed-start" ‖ len‖binding ‖ len‖enc ‖
/// len‖sealedInputs</c>, verified by the runner against an initiator public key pinned in its own configuration: the
/// seal key is public and the binding holds no secret, so without the signature anyone, the control plane included,
/// could seal inputs of their choosing into the tenant's boundary.
/// </summary>
public static class SealedStartSignature
{
    /// <summary>The signature's length: two P-256 field elements, IEEE P1363.</summary>
    public const int SignatureLength = 64;

    /// <summary>The application info every input seal is made under.</summary>
    public static ReadOnlySpan<byte> SealInfo => "arazzo-run-start-inputs"u8;

    private const int MaxIdentifierLength = 256;
    private const int LengthPrefix = sizeof(int);

    private static ReadOnlySpan<byte> SignatureLabel => "arazzo-sealed-start"u8;

    /// <summary>The binding's length for these identifiers, which is what <see cref="WriteBinding"/> writes.</summary>
    /// <param name="environmentId">The environment.</param>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="sealKeyId">The seal key generation.</param>
    /// <param name="runId">The run id the initiator chose.</param>
    /// <returns>The length in bytes.</returns>
    public static int BindingLength(string environmentId, string baseWorkflowId, string sealKeyId, string runId)
    {
        CheckIdentifiers(environmentId, baseWorkflowId, sealKeyId, runId);
        return (4 * LengthPrefix) + sizeof(uint)
            + Encoding.UTF8.GetByteCount(environmentId)
            + Encoding.UTF8.GetByteCount(baseWorkflowId)
            + Encoding.UTF8.GetByteCount(sealKeyId)
            + Encoding.UTF8.GetByteCount(runId);
    }

    /// <summary>Writes the binding: the seal's associated data.</summary>
    /// <param name="environmentId">The environment.</param>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The workflow version.</param>
    /// <param name="sealKeyId">The seal key generation.</param>
    /// <param name="runId">The run id the initiator chose.</param>
    /// <param name="destination">Receives the binding; <see cref="BindingLength"/> bytes.</param>
    /// <returns>The bytes written.</returns>
    public static int WriteBinding(string environmentId, string baseWorkflowId, int versionNumber, string sealKeyId, string runId, Span<byte> destination)
    {
        CheckIdentifiers(environmentId, baseWorkflowId, sealKeyId, runId);
        ArgumentOutOfRangeException.ThrowIfNegative(versionNumber);
        int written = WriteField(destination, environmentId);
        written += WriteField(destination[written..], baseWorkflowId);
        BinaryPrimitives.WriteUInt32BigEndian(destination[written..], (uint)versionNumber);
        written += sizeof(uint);
        written += WriteField(destination[written..], sealKeyId);
        written += WriteField(destination[written..], runId);
        return written;
    }

    /// <summary>Signs a seal as its initiator.</summary>
    /// <param name="initiatorKey">The initiator's P-256 signing key.</param>
    /// <param name="binding">The binding the seal was made under.</param>
    /// <param name="enc">The seal's encapsulated key.</param>
    /// <param name="sealedInputs">The seal's ciphertext and tag.</param>
    /// <param name="signature">Receives the signature (<see cref="SignatureLength"/> bytes).</param>
    public static void Sign(ECDsa initiatorKey, ReadOnlySpan<byte> binding, ReadOnlySpan<byte> enc, ReadOnlySpan<byte> sealedInputs, Span<byte> signature)
    {
        ArgumentNullException.ThrowIfNull(initiatorKey);
        RequireP256(initiatorKey);
        ArgumentOutOfRangeException.ThrowIfNotEqual(signature.Length, SignatureLength, nameof(signature));
        byte[] message = Message(binding, enc, sealedInputs);
        if (!initiatorKey.TrySignData(message, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation, out int written) || written != SignatureLength)
        {
            throw new CryptographicException("The initiator key did not produce an ES256 signature.");
        }
    }

    /// <summary>Verifies an initiator's signature over a seal against one pinned public key.</summary>
    /// <param name="initiatorPublicKey">A pinned initiator public key, as SubjectPublicKeyInfo.</param>
    /// <param name="binding">The binding re-derived by the verifier, never read from the sealed start.</param>
    /// <param name="enc">The seal's encapsulated key.</param>
    /// <param name="sealedInputs">The seal's ciphertext and tag.</param>
    /// <param name="signature">The presented signature.</param>
    /// <returns><see langword="true"/> when the signature verifies under the key.</returns>
    public static bool Verify(ReadOnlySpan<byte> initiatorPublicKey, ReadOnlySpan<byte> binding, ReadOnlySpan<byte> enc, ReadOnlySpan<byte> sealedInputs, ReadOnlySpan<byte> signature)
    {
        if (signature.Length != SignatureLength)
        {
            return false;
        }

        using var key = ECDsa.Create();
        try
        {
            key.ImportSubjectPublicKeyInfo(initiatorPublicKey, out _);
        }
        catch (CryptographicException)
        {
            return false;
        }

        if (key.KeySize != 256)
        {
            return false;
        }

        return key.VerifyData(Message(binding, enc, sealedInputs), signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    /// <summary>Whether a public key is a P-256 key this platform can pin as an initiator.</summary>
    /// <param name="publicKey">The key, as SubjectPublicKeyInfo.</param>
    /// <returns><see langword="true"/> when it imports as a P-256 ECDSA key.</returns>
    public static bool IsP256PublicKey(ReadOnlySpan<byte> publicKey)
    {
        using var key = ECDsa.Create();
        try
        {
            key.ImportSubjectPublicKeyInfo(publicKey, out _);
            return key.KeySize == 256;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    // message = len‖label ‖ len‖binding ‖ len‖enc ‖ len‖sealedInputs
    private static byte[] Message(ReadOnlySpan<byte> binding, ReadOnlySpan<byte> enc, ReadOnlySpan<byte> sealedInputs)
    {
        byte[] message = new byte[(4 * LengthPrefix) + SignatureLabel.Length + binding.Length + enc.Length + sealedInputs.Length];
        int written = WriteField(message, SignatureLabel);
        written += WriteField(message.AsSpan(written), binding);
        written += WriteField(message.AsSpan(written), enc);
        WriteField(message.AsSpan(written), sealedInputs);
        return message;
    }

    private static void RequireP256(ECDsa key)
    {
        if (key.KeySize != 256)
        {
            throw new CryptographicException("The initiator key is not a P-256 key.");
        }
    }

    private static void CheckIdentifiers(string environmentId, string baseWorkflowId, string sealKeyId, string runId)
    {
        ArgumentException.ThrowIfNullOrEmpty(environmentId);
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(sealKeyId);
        ArgumentException.ThrowIfNullOrEmpty(runId);
        if (environmentId.Length > MaxIdentifierLength || baseWorkflowId.Length > MaxIdentifierLength || sealKeyId.Length > MaxIdentifierLength || runId.Length > MaxIdentifierLength)
        {
            throw new ArgumentException($"Binding identifiers are limited to {MaxIdentifierLength} characters.");
        }
    }

    private static int WriteField(Span<byte> destination, ReadOnlySpan<byte> value)
    {
        BinaryPrimitives.WriteInt32BigEndian(destination, value.Length);
        value.CopyTo(destination[LengthPrefix..]);
        return LengthPrefix + value.Length;
    }

    private static int WriteField(Span<byte> destination, string value)
    {
        int length = Encoding.UTF8.GetBytes(value, destination[LengthPrefix..]);
        BinaryPrimitives.WriteInt32BigEndian(destination, length);
        return LengthPrefix + length;
    }
}