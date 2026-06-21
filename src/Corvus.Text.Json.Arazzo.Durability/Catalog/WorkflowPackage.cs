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
/// <item><description>Entry (repeated, in a deterministic fixed order — see below): name length (<c>uint16</c>) +
/// name (UTF-8) + encoding (<c>byte</c>: <c>0</c> = stored; other values reserved for a future per-entry compression) +
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
/// Entries are written in a deterministic fixed order — the workflow document, then the source documents ordered by
/// name (ordinal), then any metadata entries — so identical content yields identical bytes. The content hash is
/// computed over the RFC 8785 canonical form of the logical <c>{ workflow, sources }</c> content — independent of the
/// container framing (and so of entry order) — so it is stable across repacks and container revisions.
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

    // The fixed (non-source) entry names as UTF-8, written byte-for-byte with no intermediate string — these mirror the
    // WorkflowEntryName / SchemasEntryName / ExecutorEntryName / ExecutorManifestEntryName string constants above.
    private static ReadOnlySpan<byte> WorkflowEntryNameUtf8 => "workflow.json"u8;

    private static ReadOnlySpan<byte> SchemasEntryNameUtf8 => "metadata/schemas.json"u8;

    private static ReadOnlySpan<byte> ExecutorEntryNameUtf8 => "metadata/executor.dll"u8;

    private static ReadOnlySpan<byte> ExecutorManifestEntryNameUtf8 => "metadata/executor-manifest.json"u8;

    private static readonly Comparison<KeyValuePair<string, byte[]>> BySourceKey = static (a, b) => string.CompareOrdinal(a.Key, b.Key);

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

        int sourceCount = sources.Count;

        // Order the sources by key for a deterministic container. We sort the source *keys* (a plain ordinal compare —
        // no per-source "sources/<key>.json" name to build) and then emit entries in a fixed bucket order: the workflow
        // document, the sorted sources, then any metadata entries. The reader locates entries by name, not position, so
        // any deterministic order yields a stable, content-addressable artifact while letting each name be written as
        // UTF-8 directly. The sort scratch array is rented (returned in the finally); a package with no sources skips it.
        KeyValuePair<string, ReadOnlyMemory<byte>>[]? sortedSources = null;
        if (sourceCount > 0)
        {
            sortedSources = ArrayPool<KeyValuePair<string, ReadOnlyMemory<byte>>>.Shared.Rent(sourceCount);
            for (int i = 0; i < sourceCount; i++)
            {
                sortedSources[i] = sources[i];
            }

            Array.Sort(sortedSources, 0, sourceCount, SourceKeyComparer.Instance);
        }

        try
        {
            // Exact output size: header + per entry (entry header + UTF-8 name + payload). Source names are measured (and
            // below written) as "sources/" + UTF-8(key) + ".json" with no intermediate string.
            int entryCount = 1; // workflow.json is always present.
            int total = checked(HeaderSize + EntryHeaderSize + WorkflowEntryNameUtf8.Length + workflowUtf8.Length);

            for (int i = 0; i < sourceCount; i++)
            {
                string key = sortedSources![i].Key;
                int nameLength = "sources/"u8.Length + Encoding.UTF8.GetByteCount(key) + ".json"u8.Length;
                if (nameLength > ushort.MaxValue)
                {
                    throw new ArgumentException($"Package entry name 'sources/{key}.json' is too long.", nameof(sources));
                }

                total = checked(total + EntryHeaderSize + nameLength + sortedSources[i].Value.Length);
                entryCount++;
            }

            if (!schemas.IsEmpty)
            {
                total = checked(total + EntryHeaderSize + SchemasEntryNameUtf8.Length + schemas.Length);
                entryCount++;
            }

            if (!executor.IsEmpty)
            {
                total = checked(total + EntryHeaderSize + ExecutorEntryNameUtf8.Length + executor.Length);
                entryCount++;
            }

            if (!executorManifest.IsEmpty)
            {
                total = checked(total + EntryHeaderSize + ExecutorManifestEntryNameUtf8.Length + executorManifest.Length);
                entryCount++;
            }

            // One allocation — the package itself, the genuine leaf — written in place with spans; no ZIP object graph,
            // no growing stream, and no per-entry name string.
            byte[] result = new byte[total];
            Span<byte> span = result;
            Magic.CopyTo(span);
            BinaryPrimitives.WriteUInt32LittleEndian(span[MagicSize..], (uint)entryCount);

            int pos = HeaderSize;
            pos = WriteEntry(span, pos, WorkflowEntryNameUtf8, workflowUtf8.Span);
            for (int i = 0; i < sourceCount; i++)
            {
                pos = WriteSourceEntry(span, pos, sortedSources![i].Key, sortedSources[i].Value.Span);
            }

            if (!schemas.IsEmpty)
            {
                pos = WriteEntry(span, pos, SchemasEntryNameUtf8, schemas.Span);
            }

            if (!executor.IsEmpty)
            {
                pos = WriteEntry(span, pos, ExecutorEntryNameUtf8, executor.Span);
            }

            if (!executorManifest.IsEmpty)
            {
                _ = WriteEntry(span, pos, ExecutorManifestEntryNameUtf8, executorManifest.Span);
            }

            return result;
        }
        finally
        {
            if (sortedSources is not null)
            {
                ArrayPool<KeyValuePair<string, ReadOnlyMemory<byte>>>.Shared.Return(sortedSources, clearArray: true);
            }
        }

        // Writes one entry (length-prefixed name + stored encoding + length-prefixed data) at pos, returning the new pos.
        static int WriteEntry(Span<byte> span, int pos, ReadOnlySpan<byte> name, ReadOnlySpan<byte> data)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(span[pos..], (ushort)name.Length);
            pos += 2;
            name.CopyTo(span[pos..]);
            pos += name.Length;
            span[pos++] = StoredEncoding;
            BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], (uint)data.Length);
            pos += 4;
            data.CopyTo(span[pos..]);
            return pos + data.Length;
        }

        // Writes a "sources/<key>.json" entry, composing the name straight into the output (prefix, transcoded key,
        // suffix) and back-patching the name-length prefix from the bytes actually written — no intermediate name string.
        static int WriteSourceEntry(Span<byte> span, int pos, string key, ReadOnlySpan<byte> data)
        {
            int lengthPos = pos;
            pos += 2; // reserve the uint16 name length.
            "sources/"u8.CopyTo(span[pos..]);
            pos += "sources/"u8.Length;
            pos += Encoding.UTF8.GetBytes(key, span[pos..]);
            ".json"u8.CopyTo(span[pos..]);
            pos += ".json"u8.Length;
            BinaryPrimitives.WriteUInt16LittleEndian(span[lengthPos..], (ushort)(pos - lengthPos - 2));
            span[pos++] = StoredEncoding;
            BinaryPrimitives.WriteUInt32LittleEndian(span[pos..], (uint)data.Length);
            pos += 4;
            data.CopyTo(span[pos..]);
            return pos + data.Length;
        }
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

        sources.Sort(SourceKeyComparer.Instance);
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
        using ParsedJsonDocument<JsonElement> workflow = ParsedJsonDocument<JsonElement>.Parse(workflowUtf8);
        return ComputeContentHashCore(workflow.RootElement, sources, alreadySorted: false);
    }

    /// <summary>Computes the content hash from an <strong>already-parsed</strong> workflow element and sources the caller
    /// guarantees are already ordered by name (ordinal) — the form the catalog projection supplies (it parses the rewritten
    /// workflow once and <see cref="OpenPooled"/> sorts the sources) — so the hash neither re-parses the workflow nor
    /// re-sorts. The hash value is identical to <see cref="ComputeContentHash(ReadOnlyMemory{byte}, IReadOnlyList{KeyValuePair{string, ReadOnlyMemory{byte}}})"/>
    /// for the same content.</summary>
    /// <param name="workflow">The parsed (already id-rewritten) Arazzo workflow document.</param>
    /// <param name="sortedSources">The referenced source documents, already ordered by name (ordinal).</param>
    /// <returns>The hex-encoded content hash.</returns>
    internal static string ComputeContentHashPreSorted(in JsonElement workflow, IReadOnlyList<KeyValuePair<string, ReadOnlyMemory<byte>>> sortedSources)
        => ComputeContentHashCore(workflow, sortedSources, alreadySorted: true);

    private static string ComputeContentHashCore(in JsonElement workflow, IReadOnlyList<KeyValuePair<string, ReadOnlyMemory<byte>>> sources, bool alreadySorted)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(WriterOptions, DefaultBufferSize, out IByteBufferWriter buffer);
        try
        {
            writer.WriteStartObject();
            writer.WritePropertyName("sources"u8);
            writer.WriteStartObject();
            if (alreadySorted)
            {
                // Caller-sorted: iterate by index so there is no OrderBy (and no interface-enumerator) allocation.
                for (int i = 0; i < sources.Count; i++)
                {
                    WriteSource(writer, sources[i]);
                }
            }
            else
            {
                foreach (KeyValuePair<string, ReadOnlyMemory<byte>> source in sources.OrderBy(s => s.Key, StringComparer.Ordinal))
                {
                    WriteSource(writer, source);
                }
            }

            writer.WriteEndObject();
            writer.WritePropertyName("workflow"u8);
            workflow.WriteTo(writer);
            writer.WriteEndObject();
            writer.Flush();

            // Canonicalize the assembled content by parsing it zero-copy directly over the workspace buffer
            // (ParsedJsonDocument.Parse(ReadOnlyMemory) references the memory; it copies nothing and owns no buffer), then
            // hashing straight from a pooled canonical buffer — no copy of the assembled bytes, no per-call canonical
            // byte[], and no SHA256 instance; the only heap output is the hex string. The assembled doc is read
            // synchronously and disposed before the workspace buffer is returned below, so the reference stays valid.
            using ParsedJsonDocument<JsonElement> assembled = ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenMemory);
            return HashCanonical(assembled.RootElement, buffer.WrittenMemory.Length);
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }

        static void WriteSource(Utf8JsonWriter writer, KeyValuePair<string, ReadOnlyMemory<byte>> source)
        {
            // Write the source's JSON straight into the assembled document — no per-source parse. The assembled object is
            // re-parsed and canonicalized below, so a verbatim raw copy yields the identical canonical bytes (hence the
            // identical hash); WriteRawValue still validates the tokens, more cheaply than a full parse.
            writer.WritePropertyName(source.Key);
            writer.WriteRawValue(source.Value.Span);
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

    // Orders sources by key (ordinal) for a deterministic container — a plain key compare (the "sources/" prefix and
    // ".json" suffix are identical across all source entries, so the full entry name never needs to be materialized to
    // sort). A cached singleton, so the range/list sort takes no per-call comparer allocation.
    private sealed class SourceKeyComparer : IComparer<KeyValuePair<string, ReadOnlyMemory<byte>>>
    {
        public static readonly SourceKeyComparer Instance = new();

        public int Compare(KeyValuePair<string, ReadOnlyMemory<byte>> x, KeyValuePair<string, ReadOnlyMemory<byte>> y)
            => string.CompareOrdinal(x.Key, y.Key);
    }

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