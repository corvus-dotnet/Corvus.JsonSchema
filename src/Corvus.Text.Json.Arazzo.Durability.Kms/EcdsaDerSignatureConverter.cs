// <copyright file="EcdsaDerSignatureConverter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography;

namespace Corvus.Text.Json.Arazzo.Durability.Kms;

/// <summary>
/// Converts an ECDSA signature from the ASN.1 DER <c>SEQUENCE { r, s }</c> form AWS KMS returns into the fixed-size
/// IEEE P1363 <c>r || s</c> concatenation the shared <c>TrustStoreExecutorPackageVerifier</c> expects. KMS is the one
/// backend that returns DER for ECDSA (Azure Key Vault returns P1363 natively, and Vault Transit is asked for the JWS
/// marshaling which is P1363), so this normalisation lives with the KMS signer.
/// </summary>
internal static class EcdsaDerSignatureConverter
{
    /// <summary>Converts a DER-encoded ECDSA signature to the IEEE P1363 fixed-size encoding.</summary>
    /// <param name="derSignature">The signature as an ASN.1 DER <c>SEQUENCE { INTEGER r, INTEGER s }</c>.</param>
    /// <param name="fieldSizeBytes">The curve's field size in bytes (32 for P-256, 48 for P-384): the fixed width of each of <c>r</c> and <c>s</c>.</param>
    /// <returns>The signature as <c>r || s</c>, each component big-endian and left-padded to <paramref name="fieldSizeBytes"/> (total length <c>2 × fieldSizeBytes</c>).</returns>
    /// <exception cref="CryptographicException">The DER is malformed, or a component does not fit the field size.</exception>
    public static byte[] ToP1363(ReadOnlySpan<byte> derSignature, int fieldSizeBytes)
    {
        BigInteger r;
        BigInteger s;
        try
        {
            var reader = new AsnReader(derSignature.ToArray(), AsnEncodingRules.DER);
            AsnReader sequence = reader.ReadSequence();
            r = sequence.ReadInteger();
            s = sequence.ReadInteger();
            sequence.ThrowIfNotEmpty();
            reader.ThrowIfNotEmpty();
        }
        catch (AsnContentException ex)
        {
            throw new CryptographicException("The ECDSA signature is not a valid ASN.1 DER SEQUENCE { r, s }.", ex);
        }

        var result = new byte[fieldSizeBytes * 2];
        WriteUnsignedBigEndian(r, result.AsSpan(0, fieldSizeBytes));
        WriteUnsignedBigEndian(s, result.AsSpan(fieldSizeBytes, fieldSizeBytes));
        return result;
    }

    // Writes a non-negative integer big-endian, right-aligned into a fixed-size destination (zero-padded on the left).
    private static void WriteUnsignedBigEndian(BigInteger value, Span<byte> destination)
    {
        if (value.Sign < 0)
        {
            throw new CryptographicException("An ECDSA signature component is negative.");
        }

        int byteCount = value.GetByteCount(isUnsigned: true);
        if (byteCount > destination.Length)
        {
            throw new CryptographicException("An ECDSA signature component is larger than the curve's field size.");
        }

        destination.Clear();
        if (!value.TryWriteBytes(destination[(destination.Length - byteCount)..], out _, isUnsigned: true, isBigEndian: true))
        {
            throw new CryptographicException("Failed to encode an ECDSA signature component.");
        }
    }
}