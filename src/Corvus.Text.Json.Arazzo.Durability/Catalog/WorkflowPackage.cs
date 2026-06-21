// <copyright file="WorkflowPackage.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Canonicalization;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The on-disk/on-the-wire <strong>workflow document package</strong>: a self-contained binary artifact bundling an
/// Arazzo workflow document with the OpenAPI/AsyncAPI source documents it references, plus optional precomputed
/// schema metadata and a compiled executor. It is an opaque blob — moved as a file (multipart upload / streamed
/// download) and stored verbatim — whose internal framing is documented here and whose logical content is
/// content-addressable via <c>ComputeContentHash</c>.
/// </summary>
/// <remarks>
/// <para>
/// The container is a small, deterministic <strong>length-prefixed (TLV) framing</strong> — not a ZIP — so it reads
/// and writes with spans and a single output buffer (no per-entry object graph, no compression streams). Layout
/// (all multi-byte integers little-endian):
/// </para>
/// <list type="bullet">
/// <item><description>Header: magic <c>"AWP"</c> (3 bytes) + format version (1 byte) + entry count (<c>uint32</c>).</description></item>
/// <item><description>Entry (repeated, sorted by name ordinal): name length (<c>uint16</c>) + name (UTF-8) +
/// encoding (<c>byte</c>: <c>0</c> = stored; other values reserved for a future per-entry compression) +
/// data length (<c>uint32</c>) + data (opaque bytes — UTF-8 JSON or binary).</description></item>
/// </list>
/// <para>Logical entries by name:</para>
/// <list type="bullet">
/// <item><description><c>workflow.json</c> — the Arazzo workflow document.</description></item>
/// <item><description><c>sources/&lt;name&gt;.json</c> — each referenced source document.</description></item>
/// <item><description><c>metadata/schemas.json</c> — optional precomputed schema metadata.</description></item>
/// <item><description><c>metadata/executor.dll</c> — optional compiled workflow executor assembly (binary).</description></item>
/// <item><description><c>metadata/executor-manifest.json</c> — optional executor manifest.</description></item>
/// </list>
/// <para>
/// Entries are written in name order, so identical content yields identical bytes. The content hash is computed over
/// the RFC 8785 canonical form of the logical <c>{ workflow, sources }</c> content — independent of the container
/// framing — so it is stable across repacks and container revisions.
/// </para>
/// </remarks>
public static class WorkflowPackage
{
    private const int DefaultBufferSize = 1024;

    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    /// <summary>The package format version (the 4th byte of the container magic).</summary>
    public const int FormatVersion = 1;

    /// <summary>The Arazzo workflow document entry name.</summary>
    public const string WorkflowEntryName = "workflow.json";

    /// <summary>The path prefix under which source documents are stored.</summary>
    public const string SourcesPrefix = "sources/";

    /// <summary>The reserved document name addressing the package's Arazzo workflow document.</summary>
    public const string WorkflowDocumentName = "$workflow";

    /// <summary>The reserved document name addressing the package's precomputed schema-metadata document.</summary>
    public const string SchemasDocumentName = "$schemas";

    /// <summary>The entry name of the precomputed schema-metadata document.</summary>
    public const string SchemasEntryName = "metadata/schemas.json";

    /// <summary>The reserved document name addressing the package's compiled workflow executor assembly.</summary>
    public const string ExecutorDocumentName = "$executor";

    /// <summary>The reserved document name addressing the package's executor manifest.</summary>
    public const string ExecutorManifestDocumentName = "$executorManifest";

    /// <summary>The entry name of the compiled workflow executor assembly (a .NET DLL — binary).</summary>
    public const string ExecutorEntryName = "metadata/executor.dll";

    /// <summary>The entry name of the executor manifest (target framework, integrity binding, entry type).</summary>
    public const string ExecutorManifestEntryName = "metadata/executor-manifest.json";

    // Container framing constants.
    private const int MagicSize = 4;        // "AWP" + version byte
    private const int HeaderSize = 8;       // magic (4) + entry count uint32 (4)
    private const int EntryHeaderSize = 7;  // name length uint16 (2) + encoding byte (1) + data length uint32 (4)
    private const byte StoredEncoding = 0;  // payload stored verbatim (no compression)

    // The 4-byte container magic: ASCII 'A','W','P' + the format version. All-constant, so this materializes as a
    // static data span (no allocation).
    private static ReadOnlySpan<byte> Magic => [0x41, 0x57, 0x50, (byte)FormatVersion];

    private static readonly Comparison<PackEntry> ByEntryName = static (a, b) => string.CompareOrdinal(a.Name, b.Name);
    private static readonly Comparison<KeyValuePair<string, byte[]>> BySourceKey = static (a, b) => string.CompareOrdinal(a.Key, b.Key);
    private static readonly Comparison<KeyValuePair<string, ReadOnlyMemory<byte>>> BySourceKeyMemory = static (a, b) => string.CompareOrdinal(a.Key, b.Key);

    /// <summary>Packs an Arazzo workflow document and its referenced source documents into a deterministic package.</summary>
    /// <param name="workflowUtf8">The Arazzo workflow document as UTF-8 JSON.</param>
    /// <param name="sources">The referenced source documents, keyed by their <c>sourceDescriptions</c> name.</param>
    /// <param name="schemas">An optional precomputed schema-metadata document written to
    /// <see cref="SchemasEntryName"/>; omitted when empty.</param>
    /// <param name="executor">An optional compiled workflow executor assembly written to
    /// <see cref="ExecutorEntryName"/>; omitted when empty.</param>
    /// <param name="executorManifest">An optional executor manifest written to
    /// <see cref="ExecutorManifestEntryName"/>; omitted when empty.</param>
    /// <returns>The package bytes.</returns>
    public static byte[] Pack(
        ReadOnlyMemory<byte> workflowUtf8,
        IReadOnlyList<KeyValuePair<string, byte[]>> sources,
        ReadOnlyMemory<byte> schemas = default,
        ReadOnlyMemory<byte> executor = default,
        ReadOnlyMemory<byte> executorManifest = default)
        => PackPooled(workflowUtf8, ToMemorySources(sources), schemas, executor, executorManifest);

    /// <summary>Assembles a package, taking the source documents as <see cref="ReadOnlyMemory{T}"/> (so a pooled reader
    /// can pack without materialising each source to its own array) — a distinct name from <c>Pack</c> so an empty
    /// collection literal does not become an ambiguous overload at the call site.</summary>
    /// <param name="workflowUtf8">The Arazzo workflow document as UTF-8 JSON.</param>
    /// <param name="sources">The referenced source documents, keyed by name.</param>
    /// <param name="schemas">An optional precomputed schema-metadata document.</param>
    /// <param name="executor">An optional compiled executor assembly.</param>
    /// <param name="executorManifest">An optional executor manifest.</param>
    /// <returns>The package bytes.</returns>
    internal static byte[] PackPooled(
        ReadOnlyMemory<byte> workflowUtf8,
        IReadOnlyList<KeyValuePair<string, ReadOnlyMemory<byte>>> sources,
        ReadOnlyMemory<byte> schemas = default,
        ReadOnlyMemory<byte> executor = default,
        ReadOnlyMemory<byte> executorManifest = default)
    {
        ArgumentNullException.ThrowIfNull(sources);

        // Build the entry table (logical name -> payload), then sort by name for a deterministic container.
        var entries = new List<PackEntry>(sources.Count + 4) { new(WorkflowEntryName, workflowUtf8) };
        foreach (KeyValuePair<string, ReadOnlyMemory<byte>> source in sources)
        {
            entries.Add(new(SourcesPrefix + source.Key + ".json", source.Value));
        }

        if (!schemas.IsEmpty)
        {
            entries.Add(new(SchemasEntryName, schemas));
        }

        if (!executor.IsEmpty)
        {
            entries.Add(new(ExecutorEntryName, executor));
        }

        if (!executorManifest.IsEmpty)
        {
            entries.Add(new(ExecutorManifestEntryName, executorManifest));
        }

        entries.Sort(ByEntryName);

        // Exact output size: header + per entry (entry header + UTF-8 name + payload). One allocation — the package
        // itself, the genuine leaf — written in place with spans; no ZIP object graph and no growing stream.
        int total = HeaderSize;
        foreach (PackEntry entry in entries)
        {
            int nameLength = Encoding.UTF8.GetByteCount(entry.Name);
            if (nameLength > ushort.MaxValue)
            {
                throw new ArgumentException($"Package entry name '{entry.Name}' is too long.", nameof(sources));
            }

            total = checked(total + EntryHeaderSize + nameLength + entry.Data.Length);
        }

        byte[] result = new byte[total];
        Span<byte> span = result;
        Magic.CopyTo(span);
        BinaryPrimitives.WriteUInt32LittleEndian(span[MagicSize..], (uint)entries.Count);

        int pos = HeaderSize;
        foreach (PackEntry entry in entries)
        {
            int nameLength = Encoding.UTF8.GetBytes(entry.Name, span[(pos + 2)..]);
            BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], (ushort)nameLength);
            pos += 2 + nameLength;
            span[pos++] = StoredEncoding;
            BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], (uint)entry.Data.Length);
            pos += 4;
            entry.Data.Span.CopyTo(span[pos..]);
            pos += entry.Data.Length;
        }

        return result;
    }

    /// <summary>Opens a package, materializing its workflow and source documents.</summary>
    /// <param name="package">The package bytes.</param>
    /// <returns>The package contents.</returns>
    /// <exception cref="ArgumentException">The bytes are not a valid package (bad magic or no workflow document).</exception>
    /// <exception cref="InvalidDataException">The package is truncated or uses an unsupported entry encoding.</exception>
    public static WorkflowPackageContents Open(ReadOnlyMemory<byte> package)
    {
        ReadOnlySpan<byte> span = package.Span;
        var reader = new PackageReader(span);

        byte[]? workflow = null;
        byte[]? schemas = null;
        byte[]? executor = null;
        byte[]? executorManifest = null;
        var sources = new List<KeyValuePair<string, byte[]>>();

        while (reader.TryRead(out ReadOnlySpan<byte> name, out int dataOffset, out int dataLength))
        {
            // u8 literals below mirror the WorkflowEntryName / SchemasEntryName / ExecutorEntryName /
            // ExecutorManifestEntryName string constants (zero-alloc span matching) — keep them in sync.
            ReadOnlySpan<byte> data = span.Slice(dataOffset, dataLength);
            if (name.SequenceEqual("workflow.json"u8))
            {
                workflow = data.ToArray();
            }
            else if (TryExtractSourceName(name, out string sourceName))
            {
                sources.Add(new KeyValuePair<string, byte[]>(sourceName, data.ToArray()));
            }
            else if (name.SequenceEqual("metadata/schemas.json"u8))
            {
                schemas = data.ToArray();
            }
            else if (name.SequenceEqual("metadata/executor.dll"u8))
            {
                executor = data.ToArray();
            }
            else if (name.SequenceEqual("metadata/executor-manifest.json"u8))
            {
                executorManifest = data.ToArray();
            }

            // Unknown entries are ignored (forward compatibility).
        }

        if (workflow is null)
        {
            throw new ArgumentException("The package has no 'workflow.json' document.", nameof(package));
        }

        sources.Sort(BySourceKey);
        return new WorkflowPackageContents(workflow, sources)
        {
            Schemas = schemas,
            Executor = executor,
            ExecutorManifest = executorManifest,
        };
    }

    /// <summary>
    /// Opens a package's workflow + source documents as <strong>borrow-only views</strong> over the package buffer —
    /// the zero-copy counterpart of <see cref="Open"/> for the catalog-add hot path. Because payloads are stored
    /// verbatim and contiguously, each document is a <see cref="ReadOnlyMemory{T}"/> slice of <paramref name="package"/>
    /// with no per-document copy. The caller must <c>using</c> the result and keep <paramref name="package"/> alive for
    /// its lifetime, and must not retain any view past the dispose. Schemas/executor are not read — they are produced by
    /// the providers at add time.
    /// </summary>
    /// <param name="package">The package bytes.</param>
    /// <returns>The borrow-only, disposable contents.</returns>
    /// <exception cref="ArgumentException">The bytes are not a valid package (bad magic or no workflow document).</exception>
    /// <exception cref="InvalidDataException">The package is truncated or uses an unsupported entry encoding.</exception>
    internal static PooledPackageContents OpenPooled(ReadOnlyMemory<byte> package)
    {
        ReadOnlySpan<byte> span = package.Span;
        var reader = new PackageReader(span);

        ReadOnlyMemory<byte> workflow = default;
        bool hasWorkflow = false;
        var sources = new List<KeyValuePair<string, ReadOnlyMemory<byte>>>();

        while (reader.TryRead(out ReadOnlySpan<byte> name, out int dataOffset, out int dataLength))
        {
            // "workflow.json"u8 mirrors the WorkflowEntryName constant.
            if (name.SequenceEqual("workflow.json"u8))
            {
                workflow = package.Slice(dataOffset, dataLength);
                hasWorkflow = true;
            }
            else if (TryExtractSourceName(name, out string sourceName))
            {
                sources.Add(new KeyValuePair<string, ReadOnlyMemory<byte>>(sourceName, package.Slice(dataOffset, dataLength)));
            }

            // schemas/executor/manifest are produced by the providers at projection time, not read here.
        }

        if (!hasWorkflow)
        {
            throw new ArgumentException("The package has no 'workflow.json' document.", nameof(package));
        }

        sources.Sort(BySourceKeyMemory);
        return new PooledPackageContents(workflow, sources);
    }

    /// <summary>
    /// Computes the SHA-256 (lower-hex) of the RFC 8785 canonical form of the logical <c>{ workflow, sources }</c>
    /// content — independent of the container framing — so it is stable across repacks.
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

    // Recognises a "sources/<name>.json" entry and extracts <name>; the name string is unavoidable (it is the
    // dictionary key the catalog projects), but matching the prefix/suffix is span-only.
    private static bool TryExtractSourceName(ReadOnlySpan<byte> entryName, out string sourceName)
    {
        // "sources/"u8 / ".json"u8 mirror the SourcesPrefix constant and the source-entry suffix.
        ReadOnlySpan<byte> prefix = "sources/"u8;
        ReadOnlySpan<byte> suffix = ".json"u8;
        if (entryName.Length > prefix.Length + suffix.Length
            && entryName.StartsWith(prefix)
            && entryName.EndsWith(suffix))
        {
            ReadOnlySpan<byte> middle = entryName[prefix.Length..^suffix.Length];
            sourceName = Encoding.UTF8.GetString(middle);
            return true;
        }

        sourceName = string.Empty;
        return false;
    }

    // A single (name, payload) entry being packed.
    private readonly record struct PackEntry(string Name, ReadOnlyMemory<byte> Data);

    // Forward-only reader over a package's bytes: validates the magic + entry count up front, then yields each entry's
    // name span and the absolute [offset, length) of its payload (so the caller takes either a span or a memory view).
    private ref struct PackageReader
    {
        private readonly ReadOnlySpan<byte> span;
        private int pos;
        private int remaining;

        public PackageReader(ReadOnlySpan<byte> span)
        {
            if (span.Length < HeaderSize || !span[..MagicSize].SequenceEqual(Magic))
            {
                throw new ArgumentException("The bytes are not a valid workflow package (bad magic).", nameof(span));
            }

            this.span = span;
            this.remaining = (int)BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(MagicSize, 4));
            this.pos = HeaderSize;
        }

        public bool TryRead(out ReadOnlySpan<byte> name, out int dataOffset, out int dataLength)
        {
            name = default;
            dataOffset = 0;
            dataLength = 0;
            if (this.remaining <= 0)
            {
                return false;
            }

            this.Require(2);
            int nameLength = BinaryPrimitives.ReadUInt16LittleEndian(this.span.Slice(this.pos, 2));
            this.pos += 2;

            this.Require(nameLength + 1 + 4);
            name = this.span.Slice(this.pos, nameLength);
            this.pos += nameLength;

            byte encoding = this.span[this.pos++];
            if (encoding != StoredEncoding)
            {
                throw new InvalidDataException($"Unsupported workflow-package entry encoding {encoding}.");
            }

            dataLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(this.span.Slice(this.pos, 4));
            this.pos += 4;

            this.Require(dataLength);
            dataOffset = this.pos;
            this.pos += dataLength;
            this.remaining--;
            return true;
        }

        // Asserts that `count` more bytes are available from the current position; converts a truncated/overlong field
        // (including a negative length from a >2 GiB uint32) into a clean InvalidDataException.
        private readonly void Require(int count)
        {
            if (count < 0 || this.pos + count > this.span.Length)
            {
                throw new InvalidDataException("The workflow package is truncated or malformed.");
            }
        }
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
/// The borrow-only documents of a package opened via <see cref="WorkflowPackage.OpenPooled"/>: the workflow and source
/// documents as <see cref="ReadOnlyMemory{T}"/> <strong>views over the package buffer</strong> (stored payloads need no
/// copy). Consume synchronously, keep the underlying package alive for this object's lifetime, and do not retain any
/// view past the dispose. <see cref="Dispose"/> returns any buffers a future compressed encoding had to rent to
/// decompress into (none for the stored encoding).
/// </summary>
internal sealed class PooledPackageContents : IDisposable
{
    private readonly List<byte[]>? rentedBuffers;
    private bool disposed;

    internal PooledPackageContents(ReadOnlyMemory<byte> workflow, IReadOnlyList<KeyValuePair<string, ReadOnlyMemory<byte>>> sources, List<byte[]>? rentedBuffers = null)
    {
        this.Workflow = workflow;
        this.Sources = sources;
        this.rentedBuffers = rentedBuffers;
    }

    /// <summary>Gets the Arazzo workflow document bytes (a view over the package buffer).</summary>
    public ReadOnlyMemory<byte> Workflow { get; }

    /// <summary>Gets the referenced source documents (name → bytes), ordered by name (views over the package buffer).</summary>
    public IReadOnlyList<KeyValuePair<string, ReadOnlyMemory<byte>>> Sources { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        if (this.rentedBuffers is null)
        {
            return;
        }

        foreach (byte[] buffer in this.rentedBuffers)
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}