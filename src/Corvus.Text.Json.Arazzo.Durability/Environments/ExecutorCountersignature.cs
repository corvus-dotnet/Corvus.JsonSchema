// <copyright file="ExecutorCountersignature.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability.Environments;

/// <summary>Why an executor countersignature did or did not verify.</summary>
public enum ExecutorCountersignatureResult
{
    /// <summary>The signature verifies under the signer's key over the framed tuple.</summary>
    Verified,

    /// <summary>The signer's public key is not a P-256 SubjectPublicKeyInfo.</summary>
    SignerKeyUnreadable,

    /// <summary>The signature does not verify under the signer's key over the framed tuple.</summary>
    SignatureInvalid,

    /// <summary>An identifier in the tuple exceeds the bound the tuple's stack allocation is sized for.</summary>
    IdentifierTooLong,
}

/// <summary>
/// The tenant operator's countersignature over a version's executor for one environment (ADR 0065 phase C). The
/// platform generates, compiles and signs the executor, and its signature proves only that the platform produced it;
/// the tenant countersigns the executor's assembly digest with a key of its own, whose public half a runner pins, and
/// the runner executes nothing in the environment the tenant did not countersign. The signed tuple is framed, every
/// field length-prefixed: <c>("executor-countersignature", environment, baseWorkflowId, versionNumber, packageHash,
/// assemblyDigest)</c>. The environment is inside it, so a countersignature for one environment admits nothing in
/// another; the package hash and the assembly digest are both inside, so neither a repack that keeps the content hash
/// nor a recompile that keeps the package is covered by an earlier signature.
/// </summary>
public static class ExecutorCountersignature
{
    private const int MaxIdentifierLength = 256;

    /// <summary>Verifies a countersignature under the signer's public key.</summary>
    /// <param name="environment">The environment the executor is countersigned for.</param>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="packageHash">The version's content hash the executor manifest records.</param>
    /// <param name="assemblyDigest">The executor assembly's digest the executor manifest records.</param>
    /// <param name="signerPublicKey">The tenant's executor-signing public key (SubjectPublicKeyInfo, P-256).</param>
    /// <param name="signature">The ES256 signature (IEEE P1363) to verify.</param>
    /// <returns>Whether the signature verifies, and if not why.</returns>
    public static ExecutorCountersignatureResult Verify(
        string environment,
        string baseWorkflowId,
        int versionNumber,
        string packageHash,
        string assemblyDigest,
        ReadOnlySpan<byte> signerPublicKey,
        ReadOnlySpan<byte> signature)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(baseWorkflowId);
        ArgumentNullException.ThrowIfNull(packageHash);
        ArgumentNullException.ThrowIfNull(assemblyDigest);
        if (environment.Length > MaxIdentifierLength || baseWorkflowId.Length > MaxIdentifierLength || packageHash.Length > MaxIdentifierLength || assemblyDigest.Length > MaxIdentifierLength)
        {
            return ExecutorCountersignatureResult.IdentifierTooLong;
        }

        using var ecdsa = ECDsa.Create();
        try
        {
            ecdsa.ImportSubjectPublicKeyInfo(signerPublicKey, out _);
        }
        catch (CryptographicException)
        {
            return ExecutorCountersignatureResult.SignerKeyUnreadable;
        }

        if (ecdsa.KeySize != 256)
        {
            return ExecutorCountersignatureResult.SignerKeyUnreadable;
        }

        Span<byte> tuple = stackalloc byte[MaxTupleLength(environment, baseWorkflowId, packageHash, assemblyDigest)];
        int written = WriteSignedTuple(tuple, environment, baseWorkflowId, versionNumber, packageHash, assemblyDigest);
        return ecdsa.VerifyData(tuple[..written], signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)
            ? ExecutorCountersignatureResult.Verified
            : ExecutorCountersignatureResult.SignatureInvalid;
    }

    /// <summary>Writes the framed tuple the countersignature is made over.</summary>
    /// <param name="destination">The buffer, at least <see cref="MaxTupleLength"/> long.</param>
    /// <param name="environment">The environment.</param>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="packageHash">The version's content hash.</param>
    /// <param name="assemblyDigest">The executor assembly's digest.</param>
    /// <returns>The number of bytes written.</returns>
    public static int WriteSignedTuple(Span<byte> destination, string environment, string baseWorkflowId, int versionNumber, string packageHash, string assemblyDigest)
    {
        int written = WriteField(destination, "executor-countersignature"u8);
        written += WriteField(destination[written..], environment);
        written += WriteField(destination[written..], baseWorkflowId);
        BinaryPrimitives.WriteInt32BigEndian(destination[written..], sizeof(uint));
        written += sizeof(int);
        BinaryPrimitives.WriteUInt32BigEndian(destination[written..], checked((uint)versionNumber));
        written += sizeof(uint);
        written += WriteField(destination[written..], packageHash);
        written += WriteField(destination[written..], assemblyDigest);
        return written;
    }

    /// <summary>The most bytes the framed tuple can take for these identifiers.</summary>
    /// <param name="environment">The environment.</param>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="packageHash">The version's content hash.</param>
    /// <param name="assemblyDigest">The executor assembly's digest.</param>
    /// <returns>An upper bound on the tuple length.</returns>
    public static int MaxTupleLength(string environment, string baseWorkflowId, string packageHash, string assemblyDigest)
        => (6 * (sizeof(int) + 32))
        + Encoding.UTF8.GetMaxByteCount(environment.Length + baseWorkflowId.Length + packageHash.Length + assemblyDigest.Length);

    /// <summary>Signs the framed tuple with the tenant's executor-signing private key (ES256, IEEE P1363).</summary>
    /// <param name="signer">The tenant's executor-signing P-256 private key.</param>
    /// <param name="environment">The environment the executor is countersigned for.</param>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="packageHash">The version's content hash the executor manifest records.</param>
    /// <param name="assemblyDigest">The executor assembly's digest the executor manifest records.</param>
    /// <returns>The signature bytes.</returns>
    public static byte[] Sign(ECDsa signer, string environment, string baseWorkflowId, int versionNumber, string packageHash, string assemblyDigest)
    {
        ArgumentNullException.ThrowIfNull(signer);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(baseWorkflowId);
        ArgumentNullException.ThrowIfNull(packageHash);
        ArgumentNullException.ThrowIfNull(assemblyDigest);
        Span<byte> tuple = stackalloc byte[MaxTupleLength(environment, baseWorkflowId, packageHash, assemblyDigest)];
        int written = WriteSignedTuple(tuple, environment, baseWorkflowId, versionNumber, packageHash, assemblyDigest);
        return signer.SignData(tuple[..written], HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    private static int WriteField(Span<byte> destination, ReadOnlySpan<byte> value)
    {
        BinaryPrimitives.WriteInt32BigEndian(destination, value.Length);
        value.CopyTo(destination[sizeof(int)..]);
        return sizeof(int) + value.Length;
    }

    private static int WriteField(Span<byte> destination, string value)
    {
        int length = Encoding.UTF8.GetBytes(value, destination[sizeof(int)..]);
        BinaryPrimitives.WriteInt32BigEndian(destination, length);
        return sizeof(int) + length;
    }
}