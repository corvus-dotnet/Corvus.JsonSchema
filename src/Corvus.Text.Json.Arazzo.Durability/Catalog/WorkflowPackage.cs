// <copyright file="WorkflowPackage.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.IO.Compression;
using System.Security.Cryptography;
using Corvus.Text.Json;
using Corvus.Text.Json.Canonicalization;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The on-disk/on-the-wire <strong>workflow document package</strong>: a self-contained, nupkg-style ZIP
/// archive bundling an Arazzo workflow document with the OpenAPI/AsyncAPI source documents it references, plus a
/// small manifest describing the contents. It is an opaque binary artifact — moved as a file (multipart upload /
/// streamed download) and stored verbatim — whose internal format is documented here and whose logical content
/// is content-addressable via <see cref="ComputeContentHash"/>.
/// </summary>
/// <remarks>
/// <para>Archive layout (all entries UTF-8 JSON):</para>
/// <list type="bullet">
/// <item><description><c>manifest.json</c> — <c>{ "formatVersion": 1, "workflow": "workflow.json", "sources": [ { "name", "path" } ] }</c>.</description></item>
/// <item><description><c>workflow.json</c> — the Arazzo workflow document.</description></item>
/// <item><description><c>sources/&lt;name&gt;.json</c> — each referenced source document.</description></item>
/// </list>
/// <para>
/// The archive is written deterministically (fixed entry order, fixed timestamps) so identical content yields
/// identical bytes. The content hash is computed over the RFC 8785 canonical form of the logical
/// <c>{ workflow, sources }</c> content — independent of ZIP framing — so it is stable across repacks and zip
/// implementations.
/// </para>
/// </remarks>
public static class WorkflowPackage
{
    /// <summary>The package format version written into the manifest.</summary>
    public const int FormatVersion = 1;

    /// <summary>The manifest entry name.</summary>
    public const string ManifestEntryName = "manifest.json";

    /// <summary>The Arazzo workflow document entry name.</summary>
    public const string WorkflowEntryName = "workflow.json";

    /// <summary>The path prefix under which source documents are stored.</summary>
    public const string SourcesPrefix = "sources/";

    /// <summary>The reserved document name addressing the package's Arazzo workflow document.</summary>
    public const string WorkflowDocumentName = "$workflow";

    // The ZIP epoch (1980-01-01) — the earliest timestamp the format can represent — used for every entry so
    // archives are byte-reproducible.
    private static readonly DateTimeOffset DeterministicTimestamp = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Packs an Arazzo workflow document and its referenced source documents into a deterministic package archive.</summary>
    /// <param name="workflowUtf8">The Arazzo workflow document as UTF-8 JSON.</param>
    /// <param name="sources">The referenced source documents, keyed by their <c>sourceDescriptions</c> name.</param>
    /// <returns>The package archive bytes (a ZIP).</returns>
    public static byte[] Pack(ReadOnlyMemory<byte> workflowUtf8, IReadOnlyList<KeyValuePair<string, byte[]>> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        // Order sources by name for a deterministic archive.
        var orderedSources = sources.OrderBy(s => s.Key, StringComparer.Ordinal).ToList();
        byte[] manifest = BuildManifest(orderedSources);

        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, ManifestEntryName, manifest);
            WriteEntry(archive, WorkflowEntryName, workflowUtf8.Span);
            foreach (KeyValuePair<string, byte[]> source in orderedSources)
            {
                WriteEntry(archive, SourcesPrefix + source.Key + ".json", source.Value);
            }
        }

        return buffer.ToArray();
    }

    /// <summary>Opens a package archive, materializing its workflow and source documents.</summary>
    /// <param name="packageZip">The package archive bytes.</param>
    /// <returns>The package contents.</returns>
    /// <exception cref="ArgumentException">The archive is not a valid package (no workflow document).</exception>
    public static WorkflowPackageContents Open(ReadOnlyMemory<byte> packageZip)
    {
        using var buffer = new MemoryStream(packageZip.ToArray(), writable: false);
        using var archive = new ZipArchive(buffer, ZipArchiveMode.Read);

        byte[]? workflow = ReadEntry(archive, WorkflowEntryName);
        if (workflow is null)
        {
            throw new ArgumentException("The package archive has no 'workflow.json' document.", nameof(packageZip));
        }

        var sources = new List<KeyValuePair<string, byte[]>>();
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (entry.FullName.StartsWith(SourcesPrefix, StringComparison.Ordinal) && entry.FullName.EndsWith(".json", StringComparison.Ordinal))
            {
                string name = entry.FullName[SourcesPrefix.Length..^".json".Length];
                sources.Add(new KeyValuePair<string, byte[]>(name, ReadEntryStream(entry)));
            }
        }

        sources.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        return new WorkflowPackageContents(workflow, sources);
    }

    /// <summary>
    /// Computes the SHA-256 (lower-hex) of the RFC 8785 canonical form of the logical <c>{ workflow, sources }</c>
    /// content — independent of the ZIP container — so it is stable across repacks.
    /// </summary>
    /// <param name="workflowUtf8">The Arazzo workflow document as UTF-8 JSON.</param>
    /// <param name="sources">The referenced source documents.</param>
    /// <returns>The hex-encoded content hash.</returns>
    public static string ComputeContentHash(ReadOnlyMemory<byte> workflowUtf8, IReadOnlyList<KeyValuePair<string, byte[]>> sources)
    {
        return Convert.ToHexStringLower(SHA256.HashData(CanonicalContent(workflowUtf8, sources)));
    }

    /// <summary>Builds the RFC 8785 canonical bytes of the logical <c>{ workflow, sources }</c> content.</summary>
    /// <param name="workflowUtf8">The Arazzo workflow document as UTF-8 JSON.</param>
    /// <param name="sources">The referenced source documents.</param>
    /// <returns>The canonical content bytes.</returns>
    internal static byte[] CanonicalContent(ReadOnlyMemory<byte> workflowUtf8, IReadOnlyList<KeyValuePair<string, byte[]>> sources)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false, SkipValidation = true }))
        using (ParsedJsonDocument<JsonElement> workflow = ParsedJsonDocument<JsonElement>.Parse(workflowUtf8))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("sources"u8);
            writer.WriteStartObject();
            foreach (KeyValuePair<string, byte[]> source in sources.OrderBy(s => s.Key, StringComparer.Ordinal))
            {
                using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(source.Value);
                writer.WritePropertyName(source.Key);
                document.RootElement.WriteTo(writer);
            }

            writer.WriteEndObject();
            writer.WritePropertyName("workflow"u8);
            workflow.RootElement.WriteTo(writer);
            writer.WriteEndObject();
        }

        using ParsedJsonDocument<JsonElement> assembled = ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenMemory);
        return JsonCanonicalizer.Canonicalize(assembled.RootElement);
    }

    private static byte[] BuildManifest(IReadOnlyList<KeyValuePair<string, byte[]>> orderedSources)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false, SkipValidation = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("formatVersion"u8, FormatVersion);
            writer.WriteString("workflow"u8, WorkflowEntryName);
            writer.WriteStartArray("sources"u8);
            foreach (KeyValuePair<string, byte[]> source in orderedSources)
            {
                writer.WriteStartObject();
                writer.WriteString("name"u8, source.Key);
                writer.WriteString("path"u8, SourcesPrefix + source.Key + ".json");
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string name, ReadOnlySpan<byte> content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        entry.LastWriteTime = DeterministicTimestamp;
        using Stream stream = entry.Open();
        stream.Write(content);
    }

    private static byte[]? ReadEntry(ZipArchive archive, string name)
    {
        ZipArchiveEntry? entry = archive.GetEntry(name);
        return entry is null ? null : ReadEntryStream(entry);
    }

    private static byte[] ReadEntryStream(ZipArchiveEntry entry)
    {
        using Stream stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}

/// <summary>The materialized documents of an opened <see cref="WorkflowPackage"/>.</summary>
/// <param name="Workflow">The Arazzo workflow document bytes.</param>
/// <param name="Sources">The referenced source documents (name → bytes), ordered by name.</param>
public readonly record struct WorkflowPackageContents(byte[] Workflow, IReadOnlyList<KeyValuePair<string, byte[]>> Sources)
{
    /// <summary>Gets a single document by name: <see cref="WorkflowPackage.WorkflowDocumentName"/> for the workflow, else a source name.</summary>
    /// <param name="documentName">The document name.</param>
    /// <returns>The document bytes, or <see langword="null"/> if not present.</returns>
    public byte[]? GetDocument(string documentName)
    {
        if (documentName == WorkflowPackage.WorkflowDocumentName)
        {
            return this.Workflow;
        }

        foreach (KeyValuePair<string, byte[]> source in this.Sources)
        {
            if (source.Key == documentName)
            {
                return source.Value;
            }
        }

        return null;
    }
}