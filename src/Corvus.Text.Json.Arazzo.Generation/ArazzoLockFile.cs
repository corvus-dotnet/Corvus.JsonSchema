// <copyright file="ArazzoLockFile.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Reflection;
using System.Security.Cryptography;
using Corvus.Text.Json;
using Corvus.Text.Json.Canonicalization;

namespace Corvus.Text.Json.Arazzo.Generation;

/// <summary>
/// Creation, loading, saving, and comparison logic for the Arazzo code-generation lock file
/// (<c>corvusjson-arazzo.lock</c>), using the generated <see cref="ArazzoLockFileModel"/>. It lets a regeneration be
/// skipped when nothing has changed (incremental) and makes a generation whose sources were fetched from a remote URL
/// reproducible: each resolved source description is pinned by the SHA-256 digest of the exact bytes loaded, so a
/// regeneration re-loads each and compares. Mirrors <c>OpenApiLockFile</c>.
/// </summary>
public static class ArazzoLockFile
{
    private const string LockFileName = "corvusjson-arazzo.lock";
    private const string BackupSuffix = ".bak";

    /// <summary>A source description resolved during generation, pinned by the digest of its loaded bytes.</summary>
    /// <param name="Uri">The absolute URI the source resolved as (a <c>file://</c> path or an <c>http(s)</c> url).</param>
    /// <param name="Digest">The lowercase-hex SHA-256 of the exact bytes loaded for the source.</param>
    public readonly record struct LockedSource(string Uri, string Digest);

    /// <summary>Computes the lowercase-hex SHA-256 digest of a document's exact loaded bytes.</summary>
    /// <param name="bytes">The loaded document bytes.</param>
    /// <returns>The lowercase-hex SHA-256 digest.</returns>
    public static string ComputeDigest(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>Creates a lock file model from the given generation parameters and resolved sources.</summary>
    /// <param name="arazzoRoot">The parsed Arazzo document root. The hash is computed from its RFC 8785 canonical form,
    /// making it whitespace- and format-insensitive.</param>
    /// <param name="rootNamespace">The root namespace.</param>
    /// <param name="clientName">The client name prefix, or <see langword="null"/>.</param>
    /// <param name="durable">Whether durable executors were generated.</param>
    /// <param name="sources">The source descriptions resolved during generation, each pinned by digest.</param>
    /// <param name="generatedFiles">The generated file names, relative to the output directory.</param>
    /// <returns>A new <see cref="ArazzoLockFileModel"/>.</returns>
    public static ArazzoLockFileModel Create(
        in JsonElement arazzoRoot,
        string rootNamespace,
        string? clientName,
        bool durable,
        IReadOnlyList<LockedSource> sources,
        IReadOnlyList<string> generatedFiles)
    {
        // Build the lock JSON directly and parse it into the generated model: an array of source objects is simplest and
        // clearest built through a writer, and the model still validates and round-trips it on load/save.
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new System.Text.Json.Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("arazzoFileHash", ComputeCanonicalHash(in arazzoRoot));
            writer.WriteString("rootNamespace", rootNamespace);
            if (clientName is not null)
            {
                writer.WriteString("clientName", clientName);
            }

            writer.WriteBoolean("durable", durable);
            writer.WriteStartArray("sources");
            foreach (LockedSource source in sources)
            {
                writer.WriteStartObject();
                writer.WriteString("uri", source.Uri);
                writer.WriteString("digest", source.Digest);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("generatedFiles");
            foreach (string file in generatedFiles)
            {
                writer.WriteStringValue(file);
            }

            writer.WriteEndArray();
            writer.WriteString("generatedAt", DateTimeOffset.UtcNow.ToString("O"));
            writer.WriteString("generatorVersion", GetGeneratorVersion());
            writer.WriteEndObject();
        }

        using ParsedJsonDocument<ArazzoLockFileModel> doc = ParsedJsonDocument<ArazzoLockFileModel>.Parse(buffer.WrittenMemory);
        return doc.RootElement.Clone();
    }

    /// <summary>Tries to load a lock file from the output directory.</summary>
    /// <param name="outputPath">The output directory.</param>
    /// <param name="result">When this method returns <see langword="true"/>, the loaded model.</param>
    /// <returns><see langword="true"/> if the lock file was loaded and is schema-valid; otherwise <see langword="false"/>.</returns>
    public static bool TryLoad(string outputPath, out ArazzoLockFileModel result)
    {
        string filePath = Path.Combine(outputPath, LockFileName);
        if (!File.Exists(filePath))
        {
            result = default;
            return false;
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            using ParsedJsonDocument<ArazzoLockFileModel> doc = ParsedJsonDocument<ArazzoLockFileModel>.Parse(bytes);
            result = doc.RootElement.Clone();
            return result.EvaluateSchema();
        }
        catch
        {
            result = default;
            return false;
        }
    }

    /// <summary>Determines whether the lock file is up to date: the generation parameters match and every recorded source
    /// still resolves to the same digest (so a remote source that changed forces a regeneration).</summary>
    /// <param name="lockFile">The existing lock file model.</param>
    /// <param name="arazzoRoot">The parsed Arazzo document root.</param>
    /// <param name="rootNamespace">The root namespace.</param>
    /// <param name="clientName">The client name prefix.</param>
    /// <param name="durable">Whether durable executors are requested.</param>
    /// <param name="loader">The document loader used to re-resolve each recorded source (file/http).</param>
    /// <returns><see langword="true"/> if the lock file matches and generation can be skipped.</returns>
    public static bool IsUpToDate(
        in ArazzoLockFileModel lockFile,
        in JsonElement arazzoRoot,
        string rootNamespace,
        string? clientName,
        bool durable,
        Func<Uri, byte[]?> loader)
    {
        ArgumentNullException.ThrowIfNull(loader);

        if (lockFile.GeneratorVersion.GetString() != GetGeneratorVersion())
        {
            return false;
        }

        if (lockFile.ArazzoFileHash.GetString() != ComputeCanonicalHash(in arazzoRoot))
        {
            return false;
        }

        if (lockFile.RootNamespace.GetString() != rootNamespace)
        {
            return false;
        }

        string? existingClientName = lockFile.ClientName.IsNotUndefined() ? lockFile.ClientName.GetString() : null;
        if (!string.Equals(existingClientName, clientName, StringComparison.Ordinal))
        {
            return false;
        }

        if ((bool)lockFile.Durable != durable)
        {
            return false;
        }

        // Every recorded source must still resolve to the same digest — this is what makes a remote-fetched generation
        // reproducible and detects drift when a source's url now serves different content.
        foreach (ArazzoLockFileModel.ResolvedSource source in lockFile.Sources.EnumerateArray())
        {
            if (!Uri.TryCreate((string)source.UriValue, UriKind.Absolute, out Uri? uri))
            {
                return false;
            }

            byte[]? bytes = loader(uri);
            if (bytes is null || ComputeDigest(bytes) != (string)source.Digest)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Saves the lock file model to the output directory.</summary>
    /// <param name="lockFile">The lock file model to save.</param>
    /// <param name="outputPath">The output directory.</param>
    public static void Save(in ArazzoLockFileModel lockFile, string outputPath)
    {
        Directory.CreateDirectory(outputPath);
        string filePath = Path.Combine(outputPath, LockFileName);

        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true });
        lockFile.WriteTo(writer);
        writer.Flush();

        File.WriteAllBytes(filePath, buffer.WrittenSpan.ToArray());
    }

    /// <summary>Backs up the existing lock file, if one exists, so it can be restored on failure.</summary>
    /// <param name="outputPath">The output directory.</param>
    /// <returns><see langword="true"/> if a backup was created; <see langword="false"/> if no lock file existed.</returns>
    public static bool BackupLockFile(string outputPath)
    {
        string filePath = Path.Combine(outputPath, LockFileName);
        if (!File.Exists(filePath))
        {
            return false;
        }

        File.Copy(filePath, filePath + BackupSuffix, overwrite: true);
        return true;
    }

    /// <summary>Restores the lock file from a previously created backup (leaving it in its pre-generation state).</summary>
    /// <param name="outputPath">The output directory.</param>
    /// <returns><see langword="true"/> if the backup was restored; <see langword="false"/> if no backup existed.</returns>
    public static bool RestoreLockFile(string outputPath)
    {
        string filePath = Path.Combine(outputPath, LockFileName);
        string backupPath = filePath + BackupSuffix;
        if (!File.Exists(backupPath))
        {
            return false;
        }

        File.Copy(backupPath, filePath, overwrite: true);
        File.Delete(backupPath);
        return true;
    }

    /// <summary>Deletes the lock file backup, if one exists.</summary>
    /// <param name="outputPath">The output directory.</param>
    public static void DeleteBackup(string outputPath)
    {
        string backupPath = Path.Combine(outputPath, LockFileName + BackupSuffix);
        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }
    }

    // A SHA-256 of the RFC 8785 canonical form of the Arazzo document, so the hash is whitespace- and format-insensitive.
    private static string ComputeCanonicalHash(in JsonElement element)
    {
        byte[] canonicalBytes = JsonCanonicalizer.Canonicalize(in element);
        return Convert.ToHexStringLower(SHA256.HashData(canonicalBytes));
    }

    private static string GetGeneratorVersion()
        => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
}