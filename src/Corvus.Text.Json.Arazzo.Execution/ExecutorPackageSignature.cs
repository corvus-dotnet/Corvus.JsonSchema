// <copyright file="ExecutorPackageSignature.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;

namespace Corvus.Text.Json.Arazzo.Execution;

/// <summary>
/// A detached cryptographic signature over an executor manifest's exact UTF-8 bytes (design §3.3, §12): the control
/// plane signs the manifest with a private key held in its own vault, and a runner verifies the signature against a
/// trusted <em>public</em> key it holds locally — the private key never reaches the runner, so a compromised runner
/// cannot forge a package. Carried in the package as <c>metadata/executor-manifest.sig</c>, alongside the manifest it
/// signs (the signature is not part of the manifest, so the manifest stays byte-reproducible and the signed bytes are
/// exactly the manifest a runner reads).
/// </summary>
/// <param name="Algorithm">The signature algorithm (one of <see cref="ExecutorSignatureAlgorithms"/>), telling a verifier how to check it.</param>
/// <param name="KeyId">The signing key's identifier — the verifier selects the trusted public key to check against by this id (so a deployment can rotate keys by trusting more than one).</param>
/// <param name="Value">The raw signature bytes.</param>
public readonly record struct ExecutorPackageSignature(string Algorithm, string KeyId, ReadOnlyMemory<byte> Value)
{
    /// <summary>Parses a detached signature from its UTF-8 JSON form (<c>{ "algorithm", "keyId", "value" }</c>, <c>value</c> base64).</summary>
    /// <param name="signatureUtf8">The signature document as UTF-8 JSON.</param>
    /// <returns>The parsed signature.</returns>
    /// <exception cref="FormatException">A required field is missing or malformed.</exception>
    public static ExecutorPackageSignature Parse(ReadOnlyMemory<byte> signatureUtf8)
    {
        using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(signatureUtf8);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("The executor signature is not a JSON object.");
        }

        string algorithm = ReadString(root, "algorithm"u8);
        string keyId = ReadString(root, "keyId"u8);
        string valueBase64 = ReadString(root, "value"u8);

        byte[] value = new byte[Base64.GetMaxDecodedFromUtf8Length(valueBase64.Length)];
        if (!Convert.TryFromBase64String(valueBase64, value, out int written))
        {
            throw new FormatException("The executor signature's 'value' is not valid base64.");
        }

        return new ExecutorPackageSignature(algorithm, keyId, value.AsMemory(0, written));
    }

    /// <summary>Serializes this signature to its UTF-8 JSON form for the <c>metadata/executor-manifest.sig</c> package entry.</summary>
    /// <returns>The signature as UTF-8 JSON.</returns>
    public byte[] ToUtf8()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false, SkipValidation = true }))
        {
            // Keys in a stable (alphabetical) order, matching the manifest's own reproducible layout.
            writer.WriteStartObject();
            writer.WriteString("algorithm"u8, this.Algorithm);
            writer.WriteString("keyId"u8, this.KeyId);
            writer.WriteString("value"u8, Convert.ToBase64String(this.Value.Span));
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static string ReadString(JsonElement root, ReadOnlySpan<byte> name)
        => root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } s
            ? s
            : throw new FormatException($"The executor signature is missing the required string property '{System.Text.Encoding.UTF8.GetString(name)}'.");
}

/// <summary>The signature algorithms an executor-package signer may use and a runner's verifier understands.</summary>
public static class ExecutorSignatureAlgorithms
{
    /// <summary>ECDSA over the NIST P-256 curve with SHA-256, IEEE P1363 fixed-size signature encoding (the ES256 shape).</summary>
    public const string EcdsaP256Sha256 = "ecdsa-p256-sha256";

    /// <summary>ECDSA over the NIST P-384 curve with SHA-384, IEEE P1363 fixed-size signature encoding.</summary>
    public const string EcdsaP384Sha384 = "ecdsa-p384-sha384";

    /// <summary>RSASSA-PSS with SHA-256 (the shape AWS KMS <c>RSASSA_PSS_SHA_256</c> and Azure Key Vault <c>PS256</c> produce).</summary>
    public const string RsaPssSha256 = "rsa-pss-sha256";
}