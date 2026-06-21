// <copyright file="WorkflowPackage.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.IO.Compression;
using System.Security.Cryptography;
using Corvus.Text.Json;
using Corvus.Text.Json.Canonicalization;
using Corvus.Text.Json.Internal;

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
/// <item><description><c>metadata/schemas.json</c> — optional precomputed schema metadata.</description></item>
/// <item><description><c>metadata/executor.dll</c> — optional compiled workflow executor assembly (binary).</description></item>
/// <item><description><c>metadata/executor-manifest.json</c> — optional executor manifest.</description></item>
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
    private const int DefaultBufferSize = 1024;

    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

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

    /// <summary>The reserved document name addressing the package's precomputed schema-metadata document.</summary>
    public const string SchemasDocumentName = "$schemas";

    /// <summary>The archive entry name of the precomputed schema-metadata document.</summary>
    public const string SchemasEntryName = "metadata/schemas.json";

    /// <summary>The reserved document name addressing the package's compiled workflow executor assembly.</summary>
    public const string ExecutorDocumentName = "$executor";

    /// <summary>The reserved document name addressing the package's executor manifest.</summary>
    public const string ExecutorManifestDocumentName = "$executorManifest";

    /// <summary>The archive entry name of the compiled workflow executor assembly (a .NET DLL — binary).</summary>
    public const string ExecutorEntryName = "metadata/executor.dll";

    /// <summary>The archive entry name of the executor manifest (target framework, integrity binding, entry type).</summary>
    public const string ExecutorManifestEntryName = "metadata/executor-manifest.json";

    // The ZIP epoch (1980-01-01) — the earliest timestamp the format can represent — used for every entry so
    // archives are byte-reproducible.
    private static readonly DateTimeOffset DeterministicTimestamp = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Packs an Arazzo workflow document and its referenced source documents into a deterministic package archive.</summary>
    /// <param name="workflowUtf8">The Arazzo workflow document as UTF-8 JSON.</param>
    /// <param name="sources">The referenced source documents, keyed by their <c>sourceDescriptions</c> name.</param>
    /// <param name="schemas">An optional precomputed schema-metadata document written to
    /// <see cref="SchemasEntryName"/>; omitted when empty.</param>
    /// <param name="executor">An optional compiled workflow executor assembly written to
    /// <see cref="ExecutorEntryName"/>; omitted when empty.</param>
    /// <param name="executorManifest">An optional executor manifest written to
    /// <see cref="ExecutorManifestEntryName"/>; omitted when empty.</param>
    /// <returns>The package archive bytes (a ZIP).</returns>
    public static byte[] Pack(
        ReadOnlyMemory<byte> workflowUtf8,
        IReadOnlyList<KeyValuePair<string, byte[]>> sources,
        ReadOnlyMemory<byte> schemas = default,
        ReadOnlyMemory<byte> executor = default,
        ReadOnlyMemory<byte> executorManifest = default)
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

            if (!schemas.IsEmpty)
            {
                WriteEntry(archive, SchemasEntryName, schemas.Span);
            }

            if (!executor.IsEmpty)
            {
                WriteEntry(archive, ExecutorEntryName, executor.Span);
            }

            if (!executorManifest.IsEmpty)
            {
                WriteEntry(archive, ExecutorManifestEntryName, executorManifest.Span);
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
        // Wrap the package's backing array directly (no full-package copy) when it is array-backed — the common case,
        // since stores hand the persisted bytes back as a byte[]; fall back to a copy only for non-array-backed memory.
        using MemoryStream buffer = System.Runtime.InteropServices.MemoryMarshal.TryGetArray(packageZip, out ArraySegment<byte> segment) && segment.Array is not null
            ? new MemoryStream(segment.Array, segment.Offset, segment.Count, writable: false)
            : new MemoryStream(packageZip.ToArray(), writable: false);
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
        return new WorkflowPackageContents(workflow, sources)
        {
            Schemas = ReadEntry(archive, SchemasEntryName),
            Executor = ReadEntry(archive, ExecutorEntryName),
            ExecutorManifest = ReadEntry(archive, ExecutorManifestEntryName),
        };
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
        // Hash into a stack span via the static span-destination overload — no per-call SHA256 instance and no heap
        // digest array; the only allocation is the hex string (the genuine output) and the canonical content it hashes.
        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(CanonicalContent(workflowUtf8, sources), digest);
        return Convert.ToHexStringLower(digest);
    }

    /// <summary>Builds the RFC 8785 canonical bytes of the logical <c>{ workflow, sources }</c> content.</summary>
    /// <param name="workflowUtf8">The Arazzo workflow document as UTF-8 JSON.</param>
    /// <param name="sources">The referenced source documents.</param>
    /// <returns>The canonical content bytes.</returns>
    internal static byte[] CanonicalContent(ReadOnlyMemory<byte> workflowUtf8, IReadOnlyList<KeyValuePair<string, byte[]>> sources)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(WriterOptions, DefaultBufferSize, out IByteBufferWriter buffer);
        try
        {
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

            writer.Flush();

            // Canonicalize the assembled content over a pooled document of the written span. Same written bytes as
            // before, so the content hash is unchanged.
            using ParsedJsonDocument<JsonElement> assembled = PersistedJson.ToPooledDocument<JsonElement>(buffer.WrittenSpan);
            return JsonCanonicalizer.Canonicalize(assembled.RootElement);
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    private static byte[] BuildManifest(IReadOnlyList<KeyValuePair<string, byte[]>> orderedSources)
        => PersistedJson.ToArray(
            orderedSources,
            static (Utf8JsonWriter writer, in IReadOnlyList<KeyValuePair<string, byte[]>> sources) =>
            {
                writer.WriteStartObject();
                writer.WriteNumber("formatVersion"u8, FormatVersion);
                writer.WriteString("workflow"u8, WorkflowEntryName);
                writer.WriteStartArray("sources"u8);
                foreach (KeyValuePair<string, byte[]> source in sources)
                {
                    writer.WriteStartObject();
                    writer.WriteString("name"u8, source.Key);
                    writer.WriteString("path"u8, SourcesPrefix + source.Key + ".json");
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            });

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
        // Read straight into a right-sized array using the entry's known uncompressed length — no growing MemoryStream
        // and no Stream.CopyTo scratch buffer; the returned array is the document's genuine bytes.
        byte[] content = new byte[entry.Length];
        using Stream stream = entry.Open();
        stream.ReadExactly(content);
        return content;
    }
}

/// <summary>The materialized documents of an opened <see cref="WorkflowPackage"/>.</summary>
/// <param name="Workflow">The Arazzo workflow document bytes.</param>
/// <param name="Sources">The referenced source documents (name → bytes), ordered by name.</param>
public readonly record struct WorkflowPackageContents(byte[] Workflow, IReadOnlyList<KeyValuePair<string, byte[]>> Sources)
{
    /// <summary>Gets the precomputed schema-metadata document (<see cref="WorkflowPackage.SchemasEntryName"/>), or <see langword="null"/> if the package carries none.</summary>
    public byte[]? Schemas { get; init; }

    /// <summary>Gets the compiled workflow executor assembly (<see cref="WorkflowPackage.ExecutorEntryName"/>), or <see langword="null"/> if the package carries none.</summary>
    public byte[]? Executor { get; init; }

    /// <summary>Gets the executor manifest (<see cref="WorkflowPackage.ExecutorManifestEntryName"/>), or <see langword="null"/> if the package carries none.</summary>
    public byte[]? ExecutorManifest { get; init; }

    /// <summary>Gets a single document by name: <see cref="WorkflowPackage.WorkflowDocumentName"/> for the workflow, <see cref="WorkflowPackage.SchemasDocumentName"/> for the schema metadata, <see cref="WorkflowPackage.ExecutorDocumentName"/> for the executor assembly, <see cref="WorkflowPackage.ExecutorManifestDocumentName"/> for its manifest, else a source name.</summary>
    /// <param name="documentName">The document name.</param>
    /// <returns>The document bytes, or <see langword="null"/> if not present.</returns>
    public byte[]? GetDocument(string documentName)
    {
        if (documentName == WorkflowPackage.WorkflowDocumentName)
        {
            return this.Workflow;
        }

        if (documentName == WorkflowPackage.SchemasDocumentName)
        {
            return this.Schemas;
        }

        if (documentName == WorkflowPackage.ExecutorDocumentName)
        {
            return this.Executor;
        }

        if (documentName == WorkflowPackage.ExecutorManifestDocumentName)
        {
            return this.ExecutorManifest;
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