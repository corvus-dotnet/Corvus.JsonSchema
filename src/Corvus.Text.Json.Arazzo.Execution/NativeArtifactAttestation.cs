// <copyright file="NativeArtifactAttestation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Security.Cryptography;

namespace Corvus.Text.Json.Arazzo.Execution;

/// <summary>
/// The manifest the control plane signs to attest a native executor binary (ADR 0055), so a deploy verifies the native
/// binary's provenance the same way a runner verifies the executor via its manifest signature. It binds the binary to
/// the version it was built from (<see cref="PackageHash"/>), the runtime target it is for (<see cref="RuntimeIdentifier"/>),
/// the engine version it links against (<see cref="EngineVersion"/>), and its own bytes (<see cref="NativeDigest"/>), so a
/// validly-signed native binary for one version or target cannot be replayed as another. The detached signature over it is
/// an <see cref="ExecutorPackageSignature"/>, produced by the same signer and checked by the same verifier as the executor
/// manifest signature.
/// </summary>
/// <param name="FormatVersion">The attestation format version.</param>
/// <param name="PackageHash">The catalog version's content hash the native binary was built from.</param>
/// <param name="RuntimeIdentifier">The .NET runtime identifier the native binary targets.</param>
/// <param name="EngineVersion">The Corvus runtime version the native binary links against (the executor's engine version).</param>
/// <param name="NativeDigest">The digest of the native binary bytes (<c>sha256:&lt;hex&gt;</c>), binding the binary to this attestation.</param>
public readonly record struct NativeArtifactAttestation(
    int FormatVersion,
    string PackageHash,
    string RuntimeIdentifier,
    string EngineVersion,
    string NativeDigest)
{
    /// <summary>The current attestation format version.</summary>
    public const int CurrentFormatVersion = 1;

    /// <summary>Computes the <c>sha256:&lt;hex&gt;</c> digest of a native binary, the form <see cref="NativeDigest"/> carries.</summary>
    /// <param name="nativeBinary">The native binary bytes.</param>
    /// <returns>The digest string.</returns>
    public static string ComputeDigest(ReadOnlySpan<byte> nativeBinary)
        => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(nativeBinary));

    /// <summary>Parses an attestation from its UTF-8 JSON form.</summary>
    /// <param name="attestationUtf8">The attestation as UTF-8 JSON.</param>
    /// <returns>The parsed attestation.</returns>
    /// <exception cref="FormatException">A required field is missing or malformed.</exception>
    public static NativeArtifactAttestation Parse(ReadOnlyMemory<byte> attestationUtf8)
    {
        using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(attestationUtf8);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            ThrowHelper.ThrowNativeAttestationNotJsonObject();
        }

        return new NativeArtifactAttestation(
            FormatVersion: ReadInt(root, "formatVersion"u8),
            PackageHash: ReadString(root, "packageHash"u8),
            RuntimeIdentifier: ReadString(root, "rid"u8),
            EngineVersion: ReadString(root, "engineVersion"u8),
            NativeDigest: ReadString(root, "nativeDigest"u8));
    }

    /// <summary>Serializes this attestation to its UTF-8 JSON form (keys alphabetical, for a reproducible signed document).</summary>
    /// <returns>The attestation as UTF-8 JSON.</returns>
    public byte[] ToUtf8()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false, SkipValidation = true }))
        {
            // Keys alphabetical, matching the executor manifest's reproducible layout.
            writer.WriteStartObject();
            writer.WriteString("engineVersion"u8, this.EngineVersion);
            writer.WriteNumber("formatVersion"u8, this.FormatVersion);
            writer.WriteString("nativeDigest"u8, this.NativeDigest);
            writer.WriteString("packageHash"u8, this.PackageHash);
            writer.WriteString("rid"u8, this.RuntimeIdentifier);
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static string ReadString(JsonElement root, ReadOnlySpan<byte> name)
        => root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } s
            ? s
            : throw ThrowHelper.GetNativeAttestationMissingStringPropertyException(System.Text.Encoding.UTF8.GetString(name));

    private static int ReadInt(JsonElement root, ReadOnlySpan<byte> name)
        => root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int i)
            ? i
            : throw ThrowHelper.GetNativeAttestationMissingIntegerPropertyException(System.Text.Encoding.UTF8.GetString(name));
}