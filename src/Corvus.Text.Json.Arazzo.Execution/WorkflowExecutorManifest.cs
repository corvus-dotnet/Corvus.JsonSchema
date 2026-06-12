// <copyright file="WorkflowExecutorManifest.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Execution;

/// <summary>
/// The parsed executor manifest baked alongside a compiled workflow assembly: the binding a runner verifies
/// (<see cref="AssemblyDigest"/> + <see cref="PackageHash"/>) and the metadata it needs to load the assembly
/// (<see cref="TargetFramework"/>, <see cref="EntryType"/>).
/// </summary>
/// <param name="FormatVersion">The manifest format version.</param>
/// <param name="TargetFramework">The target framework moniker the assembly was compiled for (e.g. <c>net10.0</c>).</param>
/// <param name="PackageHash">The catalog version's content hash this assembly was built from.</param>
/// <param name="AssemblyDigest">The digest of the assembly bytes (<c>sha256:&lt;hex&gt;</c>), binding the DLL to this manifest.</param>
/// <param name="EntryType">The fully-qualified name of the <c>IHostedWorkflow</c> adapter type to activate.</param>
/// <param name="WorkflowId">The versioned workflow id the assembly runs.</param>
/// <param name="Durable">Whether the executor was built in durable (checkpoint &amp; resume) mode.</param>
public readonly record struct WorkflowExecutorManifest(
    int FormatVersion,
    string TargetFramework,
    string PackageHash,
    string AssemblyDigest,
    string EntryType,
    string WorkflowId,
    bool Durable)
{
    /// <summary>Parses an executor manifest from its UTF-8 JSON form.</summary>
    /// <param name="manifestUtf8">The manifest as UTF-8 JSON.</param>
    /// <returns>The parsed manifest.</returns>
    /// <exception cref="FormatException">A required field is missing or malformed.</exception>
    public static WorkflowExecutorManifest Parse(ReadOnlyMemory<byte> manifestUtf8)
    {
        using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(manifestUtf8);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("The executor manifest is not a JSON object.");
        }

        return new WorkflowExecutorManifest(
            FormatVersion: ReadInt(root, "formatVersion"u8),
            TargetFramework: ReadString(root, "targetFramework"u8),
            PackageHash: ReadString(root, "packageHash"u8),
            AssemblyDigest: ReadString(root, "assemblyDigest"u8),
            EntryType: ReadString(root, "entryType"u8),
            WorkflowId: ReadString(root, "workflowId"u8),
            Durable: root.TryGetProperty("durable"u8, out JsonElement durable) && durable.ValueKind == JsonValueKind.True);
    }

    private static string ReadString(JsonElement root, ReadOnlySpan<byte> name)
        => root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } s
            ? s
            : throw new FormatException($"The executor manifest is missing the required string property '{System.Text.Encoding.UTF8.GetString(name)}'.");

    private static int ReadInt(JsonElement root, ReadOnlySpan<byte> name)
        => root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int i)
            ? i
            : throw new FormatException($"The executor manifest is missing the required integer property '{System.Text.Encoding.UTF8.GetString(name)}'.");
}