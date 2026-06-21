// <copyright file="WorkflowPackage.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
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
/// is content-addressable via <c>ComputeContentHash</c>.
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
        => PackPooled(workflowUtf8, ToMemorySources(sources), schemas, executor, executorManifest);

    /// <summary>Assembles a package archive, taking the source documents as <see cref="ReadOnlyMemory{T}"/> (so a pooled
    /// reader can pack without materialising each source to its own array) — a distinct name from <c>Pack</c> so an empty
    /// collection literal does not become an ambiguous overload at the call site.</summary>
    /// <param name="workflowUtf8">The Arazzo workflow document as UTF-8 JSON.</param>
    /// <param name="sources">The referenced source documents, keyed by name.</param>
    /// <param name="schemas">An optional precomputed schema-metadata document.</param>
    /// <param name="executor">An optional compiled executor assembly.</param>
    /// <param name="executorManifest">An optional executor manifest.</param>
    /// <returns>The package archive bytes (a ZIP).</returns>
    internal static byte[] PackPooled(
        ReadOnlyMemory<byte> workflowUtf8,
        IReadOnlyList<KeyValuePair<string, ReadOnlyMemory<byte>>> sources,
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
            foreach (KeyValuePair<string, ReadOnlyMemory<byte>> source in orderedSources)
            {
                WriteEntry(archive, SourcesPrefix + source.Key + ".json", source.Value.Span);
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
        using MemoryStream buffer = AsReadStream(packageZip);
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
    /// Opens a package's workflow + source documents into <strong>pooled</strong> buffers — the borrow-only counterpart
    /// of <see cref="Open"/> for the catalog-add hot path: the documents are read into <see cref="ArrayPool{T}"/>-rented
    /// arrays and exposed as <see cref="ReadOnlyMemory{T}"/>, returned to the pool on <see cref="IDisposable.Dispose"/>.
    /// The caller must <c>using</c> the result and must not retain any of its <see cref="ReadOnlyMemory{T}"/> past the
    /// dispose (the buffers are recycled). Schemas/executor are not read — they are produced by the providers at add time.
    /// </summary>
    /// <param name="packageZip">The package archive bytes.</param>
    /// <returns>The pooled, disposable contents.</returns>
    /// <exception cref="ArgumentException">The archive is not a valid package (no workflow document).</exception>
    internal static PooledPackageContents OpenPooled(ReadOnlyMemory<byte> packageZip)
    {
        using MemoryStream buffer = AsReadStream(packageZip);
        using var archive = new ZipArchive(buffer, ZipArchiveMode.Read);

        ZipArchiveEntry workflowEntry = archive.GetEntry(WorkflowEntryName)
            ?? throw new ArgumentException("The package archive has no 'workflow.json' document.", nameof(packageZip));

        var rented = new List<byte[]>();
        try
        {
            byte[] workflowBuffer = RentEntry(workflowEntry, out int workflowLength);
            rented.Add(workflowBuffer);

            var sources = new List<KeyValuePair<string, ReadOnlyMemory<byte>>>();
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (entry.FullName.StartsWith(SourcesPrefix, StringComparison.Ordinal) && entry.FullName.EndsWith(".json", StringComparison.Ordinal))
                {
                    string name = entry.FullName[SourcesPrefix.Length..^".json".Length];
                    byte[] sourceBuffer = RentEntry(entry, out int sourceLength);
                    rented.Add(sourceBuffer);
                    sources.Add(new KeyValuePair<string, ReadOnlyMemory<byte>>(name, sourceBuffer.AsMemory(0, sourceLength)));
                }
            }

            sources.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return new PooledPackageContents(workflowBuffer.AsMemory(0, workflowLength), sources, rented);
        }
        catch
        {
            foreach (byte[] b in rented)
            {
                ArrayPool<byte>.Shared.Return(b);
            }

            throw;
        }
    }

    // Reads a ZIP entry into a right-sized, pooled buffer using its known uncompressed length; the caller owns the
    // returned array's pool lifetime (it is tracked by the PooledPackageContents and returned on dispose).
    private static byte[] RentEntry(ZipArchiveEntry entry, out int length)
    {
        length = (int)entry.Length;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
        using Stream stream = entry.Open();
        stream.ReadExactly(buffer.AsSpan(0, length));
        return buffer;
    }

    // Wraps the package's backing array directly (no full-package copy) when it is array-backed — the common case, since
    // stores hand the persisted bytes back as a byte[]; falls back to a copy only for non-array-backed memory.
    private static MemoryStream AsReadStream(ReadOnlyMemory<byte> packageZip)
        => System.Runtime.InteropServices.MemoryMarshal.TryGetArray(packageZip, out ArraySegment<byte> segment) && segment.Array is not null
            ? new MemoryStream(segment.Array, segment.Offset, segment.Count, writable: false)
            : new MemoryStream(packageZip.ToArray(), writable: false);

    /// <summary>
    /// Computes the SHA-256 (lower-hex) of the RFC 8785 canonical form of the logical <c>{ workflow, sources }</c>
    /// content — independent of the ZIP container — so it is stable across repacks.
    /// </summary>
    /// <param name="workflowUtf8">The Arazzo workflow document as UTF-8 JSON.</param>
    /// <param name="sources">The referenced source documents.</param>
    /// <returns>The hex-encoded content hash.</returns>
    public static string ComputeContentHash(ReadOnlyMemory<byte> workflowUtf8, IReadOnlyList<KeyValuePair<string, byte[]>> sources)
        => ComputeContentHash(workflowUtf8, ToMemorySources(sources));

    /// <summary>Computes the content hash taking the sources as <see cref="ReadOnlyMemory{T}"/> — the form a pooled
    /// package reader supplies (so the hash runs without materialising each source to its own array).</summary>
    /// <param name="workflowUtf8">The Arazzo workflow document as UTF-8 JSON.</param>
    /// <param name="sources">The referenced source documents.</param>
    /// <returns>The hex-encoded content hash.</returns>
    public static string ComputeContentHash(ReadOnlyMemory<byte> workflowUtf8, IReadOnlyList<KeyValuePair<string, ReadOnlyMemory<byte>>> sources)
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
                foreach (KeyValuePair<string, ReadOnlyMemory<byte>> source in sources.OrderBy(s => s.Key, StringComparer.Ordinal))
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

            // Canonicalize the assembled content over a pooled document of the written span, hashing straight from a
            // pooled canonical buffer — no per-call canonical byte[] and no SHA256 instance; the only heap output is the
            // hex string (the genuine result). The same assembled bytes are canonicalized as before, so the hash is unchanged.
            using ParsedJsonDocument<JsonElement> assembled = PersistedJson.ToPooledDocument<JsonElement>(buffer.WrittenSpan);
            return HashCanonical(assembled.RootElement, buffer.WrittenSpan.Length);
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    // Canonicalizes the element into an ArrayPool-rented buffer (growing on overflow) and returns the lower-hex SHA-256
    // of the canonical bytes — the canonical form is hashed in place, never materialized to its own array. The size hint
    // (the assembled content length) right-sizes the first rent so the common case fits without a grow.
    private static string HashCanonical(in JsonElement element, int sizeHint)
    {
        int bufferSize = Math.Max(DefaultBufferSize, sizeHint);
        while (true)
        {
            byte[] rented = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                if (JsonCanonicalizer.TryCanonicalize(element, rented, out int written))
                {
                    Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
                    SHA256.HashData(rented.AsSpan(0, written), digest);
                    return Convert.ToHexStringLower(digest);
                }

                bufferSize = checked(bufferSize * 2);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    // Converts a byte[]-keyed source list to the ReadOnlyMemory form the canonical Pack/hash take (cold callers — Build,
    // tests — pay this small wrapper list; the warm publish path supplies pooled ReadOnlyMemory directly).
    private static IReadOnlyList<KeyValuePair<string, ReadOnlyMemory<byte>>> ToMemorySources(IReadOnlyList<KeyValuePair<string, byte[]>> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var list = new List<KeyValuePair<string, ReadOnlyMemory<byte>>>(sources.Count);
        foreach (KeyValuePair<string, byte[]> source in sources)
        {
            list.Add(new KeyValuePair<string, ReadOnlyMemory<byte>>(source.Key, source.Value));
        }

        return list;
    }

    private static byte[] BuildManifest(IReadOnlyList<KeyValuePair<string, ReadOnlyMemory<byte>>> orderedSources)
        => PersistedJson.ToArray(
            orderedSources,
            static (Utf8JsonWriter writer, in IReadOnlyList<KeyValuePair<string, ReadOnlyMemory<byte>>> sources) =>
            {
                writer.WriteStartObject();
                writer.WriteNumber("formatVersion"u8, FormatVersion);
                writer.WriteString("workflow"u8, WorkflowEntryName);
                writer.WriteStartArray("sources"u8);
                foreach (KeyValuePair<string, ReadOnlyMemory<byte>> source in sources)
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

/// <summary>
/// The pooled, borrow-only documents of a package opened via <see cref="WorkflowPackage.OpenPooled"/>: the workflow and
/// source documents as <see cref="ReadOnlyMemory{T}"/> views over <see cref="ArrayPool{T}"/>-rented buffers, returned to
/// the pool on <see cref="Dispose"/>. Consume synchronously and do not retain any view past the dispose (buffers recycle).
/// </summary>
internal sealed class PooledPackageContents : IDisposable
{
    private readonly List<byte[]> rentedBuffers;
    private bool disposed;

    internal PooledPackageContents(ReadOnlyMemory<byte> workflow, IReadOnlyList<KeyValuePair<string, ReadOnlyMemory<byte>>> sources, List<byte[]> rentedBuffers)
    {
        this.Workflow = workflow;
        this.Sources = sources;
        this.rentedBuffers = rentedBuffers;
    }

    /// <summary>Gets the Arazzo workflow document bytes (a view over a pooled buffer).</summary>
    public ReadOnlyMemory<byte> Workflow { get; }

    /// <summary>Gets the referenced source documents (name → bytes), ordered by name (views over pooled buffers).</summary>
    public IReadOnlyList<KeyValuePair<string, ReadOnlyMemory<byte>>> Sources { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        foreach (byte[] buffer in this.rentedBuffers)
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}