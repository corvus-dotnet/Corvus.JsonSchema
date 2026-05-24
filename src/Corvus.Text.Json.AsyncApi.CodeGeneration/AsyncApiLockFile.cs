// <copyright file="AsyncApiLockFile.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Reflection;
using System.Security.Cryptography;
using Corvus.Text.Json;
using Corvus.Text.Json.Canonicalization;

namespace Corvus.Text.Json.AsyncApi.CodeGeneration;

/// <summary>
/// Provides creation, loading, saving, and comparison logic for the
/// <c>corvusjson-asyncapi.lock</c> file using the generated
/// <see cref="AsyncApiLockFileModel"/> type.
/// </summary>
public static class AsyncApiLockFile
{
    private const string LockFileName = "corvusjson-asyncapi.lock";
    private const string BackupSuffix = ".bak";

    /// <summary>
    /// Creates a new lock file model from the given generation parameters.
    /// </summary>
    /// <param name="specRoot">The parsed spec root element. The hash is computed from
    /// the RFC 8785 canonical form, making it whitespace- and format-insensitive.</param>
    /// <param name="specVersion">The spec version ("3.0" or "2.6").</param>
    /// <param name="rootNamespace">The root namespace.</param>
    /// <param name="mode">The generation mode (producer, consumer, or both).</param>
    /// <param name="filter">The operation filter, or <see langword="null"/>.</param>
    /// <param name="generatedFiles">The list of generated file names.</param>
    /// <param name="descriptionLocation">The original URL of the API description, or <see langword="null"/>.</param>
    /// <returns>A new <see cref="AsyncApiLockFileModel"/>.</returns>
    public static AsyncApiLockFileModel Create(
        in JsonElement specRoot,
        string specVersion,
        string rootNamespace,
        string mode,
        OperationFilter? filter,
        IReadOnlyList<string> generatedFiles,
        string? descriptionLocation = null)
    {
        string[] includeChannels = filter?.IncludePaths is { Count: > 0 } inc ? [.. inc] : [];
        string[] excludeChannels = filter?.ExcludePaths is { Count: > 0 } exc ? [.. exc] : [];
        string[] tags = filter?.Tags is { Count: > 0 } t ? [.. t] : [];
        string[] files = [.. generatedFiles];

        using JsonWorkspace workspace = JsonWorkspace.Create();

        var context = (excludeChannels, files, includeChannels, tags);

        using JsonDocumentBuilder<AsyncApiLockFileModel.Mutable> builder =
            AsyncApiLockFileModel.CreateBuilder(
                workspace,
                context,
                excludeChannels: AsyncApiLockFileModel.JsonStringArray.Build(
                    context,
                    static (in (string[] excludeChannels, string[] files, string[] includeChannels, string[] tags) ctx, ref AsyncApiLockFileModel.JsonStringArray.Builder b) =>
                    {
                        foreach (string p in ctx.excludeChannels)
                        {
                            b.AddItem(p);
                        }
                    }),
                generatedAt: DateTimeOffset.UtcNow.ToString("O"),
                generatedFiles: AsyncApiLockFileModel.GeneratedFArray.Build(
                    context,
                    static (in (string[] excludeChannels, string[] files, string[] includeChannels, string[] tags) ctx, ref AsyncApiLockFileModel.GeneratedFArray.Builder b) =>
                    {
                        foreach (string file in ctx.files)
                        {
                            b.AddItem(file);
                        }
                    }),
                generatorVersion: GetGeneratorVersion(),
                includeChannels: AsyncApiLockFileModel.IncludeChaArray.Build(
                    context,
                    static (in (string[] excludeChannels, string[] files, string[] includeChannels, string[] tags) ctx, ref AsyncApiLockFileModel.IncludeChaArray.Builder b) =>
                    {
                        foreach (string p in ctx.includeChannels)
                        {
                            b.AddItem(p);
                        }
                    }),
                mode: mode,
                rootNamespace: rootNamespace,
                specFileHash: ComputeCanonicalHash(in specRoot),
                specVersion: specVersion,
                tags: AsyncApiLockFileModel.TagsJsonStArray.Build(
                    context,
                    static (in (string[] excludeChannels, string[] files, string[] includeChannels, string[] tags) ctx, ref AsyncApiLockFileModel.TagsJsonStArray.Builder b) =>
                    {
                        foreach (string tag in ctx.tags)
                        {
                            b.AddItem(tag);
                        }
                    }),
                descriptionLocation: descriptionLocation is not null ? (JsonUri.Source)descriptionLocation : default);

        return builder.RootElement.Clone();
    }

    /// <summary>
    /// Tries to load a lock file from the output directory.
    /// </summary>
    /// <param name="outputPath">The output directory.</param>
    /// <param name="result">When this method returns <see langword="true"/>, the loaded model.</param>
    /// <returns><see langword="true"/> if the lock file was loaded successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryLoad(string outputPath, out AsyncApiLockFileModel result)
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
            using ParsedJsonDocument<AsyncApiLockFileModel> doc = ParsedJsonDocument<AsyncApiLockFileModel>.Parse(bytes);

            // Clone to detach from the parsed document lifetime.
            result = doc.RootElement.Clone();
            return result.EvaluateSchema();
        }
        catch
        {
            result = default;
            return false;
        }
    }

    /// <summary>
    /// Determines whether the lock file is up to date with the given parameters.
    /// </summary>
    /// <param name="lockFile">The existing lock file model.</param>
    /// <param name="specRoot">The parsed spec root element.</param>
    /// <param name="specVersion">The spec version.</param>
    /// <param name="rootNamespace">The root namespace.</param>
    /// <param name="mode">The generation mode.</param>
    /// <param name="filter">The operation filter.</param>
    /// <returns><see langword="true"/> if the lock file matches and generation can be skipped.</returns>
    public static bool IsUpToDate(
        in AsyncApiLockFileModel lockFile,
        in JsonElement specRoot,
        string specVersion,
        string rootNamespace,
        string mode,
        OperationFilter? filter)
    {
        if (lockFile.GeneratorVersion.GetString() != GetGeneratorVersion())
        {
            return false;
        }

        if (lockFile.SpecFileHash.GetString() != ComputeCanonicalHash(in specRoot))
        {
            return false;
        }

        if (lockFile.SpecVersion.GetString() != specVersion)
        {
            return false;
        }

        if (lockFile.RootNamespace.GetString() != rootNamespace)
        {
            return false;
        }

        if (lockFile.Mode.GetString() != mode)
        {
            return false;
        }

        string[] currentIncludes = filter?.IncludePaths is { Count: > 0 } inc ? [.. inc] : [];
        string[] currentExcludes = filter?.ExcludePaths is { Count: > 0 } exc ? [.. exc] : [];
        string[] currentTags = filter?.Tags is { Count: > 0 } t ? [.. t] : [];

        if (!StringArrayEquals(lockFile.IncludeChannels, currentIncludes))
        {
            return false;
        }

        if (!StringArrayEquals(lockFile.ExcludeChannels, currentExcludes))
        {
            return false;
        }

        if (!StringArrayEquals(lockFile.Tags, currentTags))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Saves the lock file model to the output directory.
    /// </summary>
    /// <param name="lockFile">The lock file model to save.</param>
    /// <param name="outputPath">The output directory.</param>
    public static void Save(in AsyncApiLockFileModel lockFile, string outputPath)
    {
        Directory.CreateDirectory(outputPath);
        string filePath = Path.Combine(outputPath, LockFileName);

        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true });
        lockFile.WriteTo(writer);
        writer.Flush();

        File.WriteAllBytes(filePath, buffer.WrittenSpan.ToArray());
    }

    /// <summary>
    /// Creates a backup of the existing lock file, if one exists.
    /// Call this before generation so the lock file can be restored on failure.
    /// </summary>
    /// <param name="outputPath">The output directory.</param>
    /// <returns><see langword="true"/> if a backup was created; <see langword="false"/>
    /// if no lock file existed.</returns>
    public static bool BackupLockFile(string outputPath)
    {
        string filePath = Path.Combine(outputPath, LockFileName);
        if (!File.Exists(filePath))
        {
            return false;
        }

        string backupPath = filePath + BackupSuffix;
        File.Copy(filePath, backupPath, overwrite: true);
        return true;
    }

    /// <summary>
    /// Restores the lock file from a previously created backup.
    /// Call this when generation fails to leave the lock file in its pre-generation state.
    /// </summary>
    /// <param name="outputPath">The output directory.</param>
    /// <returns><see langword="true"/> if the backup was restored; <see langword="false"/>
    /// if no backup existed.</returns>
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

    /// <summary>
    /// Deletes the lock file backup, if one exists.
    /// Call this after generation succeeds and the new lock file has been written.
    /// </summary>
    /// <param name="outputPath">The output directory.</param>
    public static void DeleteBackup(string outputPath)
    {
        string backupPath = Path.Combine(outputPath, LockFileName + BackupSuffix);
        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }
    }

    /// <summary>
    /// Computes a SHA-256 hash of the RFC 8785 canonical form of the given JSON element.
    /// This makes the hash whitespace- and format-insensitive.
    /// </summary>
    private static string ComputeCanonicalHash(in JsonElement element)
    {
        byte[] canonicalBytes = JsonCanonicalizer.Canonicalize(in element);
        byte[] hash = SHA256.HashData(canonicalBytes);
        return Convert.ToHexStringLower(hash);
    }

    private static bool StringArrayEquals(JsonElement jsonArray, string[] expected)
    {
        int i = 0;
        foreach (JsonElement item in jsonArray.EnumerateArray())
        {
            if (i >= expected.Length || item.GetString() != expected[i])
            {
                return false;
            }

            i++;
        }

        return i == expected.Length;
    }

    private static string GetGeneratorVersion()
    {
        return Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";
    }
}