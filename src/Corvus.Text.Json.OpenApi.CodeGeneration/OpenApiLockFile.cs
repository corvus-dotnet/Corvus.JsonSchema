// <copyright file="OpenApiLockFile.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Reflection;
using System.Security.Cryptography;
using Corvus.Text.Json;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Provides creation, loading, saving, and comparison logic for the
/// <c>corvusjson-openapi.lock</c> file using the generated
/// <see cref="OpenApiLockFileModel"/> type.
/// </summary>
public static class OpenApiLockFile
{
    private const string LockFileName = "corvusjson-openapi.lock";

    /// <summary>
    /// Creates a new lock file model from the given generation parameters.
    /// </summary>
    /// <param name="specBytes">The raw spec file content.</param>
    /// <param name="specVersion">The spec version ("3.0" or "3.1").</param>
    /// <param name="rootNamespace">The root namespace.</param>
    /// <param name="clientName">The client name prefix, or <see langword="null"/>.</param>
    /// <param name="filter">The operation filter, or <see langword="null"/>.</param>
    /// <param name="generatedFiles">The list of generated file names.</param>
    /// <returns>A new <see cref="OpenApiLockFileModel"/>.</returns>
    public static OpenApiLockFileModel Create(
        byte[] specBytes,
        string specVersion,
        string rootNamespace,
        string? clientName,
        OperationFilter? filter,
        IReadOnlyList<string> generatedFiles)
    {
        string[] includePaths = filter?.IncludePaths is { Count: > 0 } inc ? [.. inc] : [];
        string[] excludePaths = filter?.ExcludePaths is { Count: > 0 } exc ? [.. exc] : [];
        string[] files = [.. generatedFiles];

        using JsonWorkspace workspace = JsonWorkspace.Create();

        var context = (excludePaths, files, includePaths);

        using JsonDocumentBuilder<OpenApiLockFileModel.Mutable> builder =
            OpenApiLockFileModel.CreateBuilder(
                workspace,
                context,
                excludePaths: OpenApiLockFileModel.JsonStringArray.Build(
                    context,
                    static (in (string[] excludePaths, string[] files, string[] includePaths) ctx, ref OpenApiLockFileModel.JsonStringArray.Builder b) =>
                    {
                        foreach (string p in ctx.excludePaths)
                        {
                            b.AddItem(p);
                        }
                    }),
                generatedAt: DateTimeOffset.UtcNow.ToString("O"),
                generatedFiles: OpenApiLockFileModel.GeneratedFArray.Build(
                    context,
                    static (in (string[] excludePaths, string[] files, string[] includePaths) ctx, ref OpenApiLockFileModel.GeneratedFArray.Builder b) =>
                    {
                        foreach (string file in ctx.files)
                        {
                            b.AddItem(file);
                        }
                    }),
                generatorVersion: GetGeneratorVersion(),
                includePaths: OpenApiLockFileModel.IncludePatArray.Build(
                    context,
                    static (in (string[] excludePaths, string[] files, string[] includePaths) ctx, ref OpenApiLockFileModel.IncludePatArray.Builder b) =>
                    {
                        foreach (string p in ctx.includePaths)
                        {
                            b.AddItem(p);
                        }
                    }),
                rootNamespace: rootNamespace,
                specFileHash: ComputeHash(specBytes),
                specVersion: specVersion,
                clientName: clientName is not null ? (JsonString.Source)clientName : default);

        return builder.RootElement.Clone();
    }

    /// <summary>
    /// Tries to load a lock file from the output directory.
    /// </summary>
    /// <param name="outputPath">The output directory.</param>
    /// <param name="result">When this method returns <see langword="true"/>, the loaded model.</param>
    /// <returns><see langword="true"/> if the lock file was loaded successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryLoad(string outputPath, out OpenApiLockFileModel result)
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
            using ParsedJsonDocument<OpenApiLockFileModel> doc = ParsedJsonDocument<OpenApiLockFileModel>.Parse(bytes);

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
    /// <param name="specBytes">The current spec file content.</param>
    /// <param name="specVersion">The spec version.</param>
    /// <param name="rootNamespace">The root namespace.</param>
    /// <param name="clientName">The client name prefix.</param>
    /// <param name="filter">The operation filter.</param>
    /// <returns><see langword="true"/> if the lock file matches and generation can be skipped.</returns>
    public static bool IsUpToDate(
        in OpenApiLockFileModel lockFile,
        byte[] specBytes,
        string specVersion,
        string rootNamespace,
        string? clientName,
        OperationFilter? filter)
    {
        if (lockFile.GeneratorVersion.GetString() != GetGeneratorVersion())
        {
            return false;
        }

        if (lockFile.SpecFileHash.GetString() != ComputeHash(specBytes))
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

        string? existingClientName = lockFile.ClientName.IsNotUndefined()
            ? lockFile.ClientName.GetString()
            : null;

        if (!string.Equals(existingClientName, clientName, StringComparison.Ordinal))
        {
            return false;
        }

        string[] currentIncludes = filter?.IncludePaths is { Count: > 0 } inc ? [.. inc] : [];
        string[] currentExcludes = filter?.ExcludePaths is { Count: > 0 } exc ? [.. exc] : [];

        if (!StringArrayEquals(lockFile.IncludePaths, currentIncludes))
        {
            return false;
        }

        if (!StringArrayEquals(lockFile.ExcludePaths, currentExcludes))
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
    public static void Save(in OpenApiLockFileModel lockFile, string outputPath)
    {
        Directory.CreateDirectory(outputPath);
        string filePath = Path.Combine(outputPath, LockFileName);

        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true });
        lockFile.WriteTo(writer);
        writer.Flush();

        File.WriteAllBytes(filePath, buffer.WrittenSpan.ToArray());
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

    private static string ComputeHash(byte[] content)
    {
        byte[] hash = SHA256.HashData(content);
        return Convert.ToHexStringLower(hash);
    }

    private static string GetGeneratorVersion()
    {
        return Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";
    }
}