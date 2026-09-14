// <copyright file="GenerationInputs.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Corvus.Json.CodeGeneration.DocumentResolvers;
using Corvus.Yaml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Corvus.Json.SourceGeneratorTools;

/// <summary>
/// A JSON or YAML additional text as an input to the incremental pipeline, compared by path and content checksum.
/// </summary>
/// <remarks>
/// Roslyn compares <see cref="AdditionalText"/> instances by reference, so an unchanged file read again (a no-op
/// save, or a host that creates a new instance for the same content) would otherwise look like a change and
/// regenerate every type in the project.
/// </remarks>
internal sealed class SchemaFile : IEquatable<SchemaFile>
{
    private SchemaFile(string path, ImmutableArray<byte> checksum, SourceText? text)
    {
        this.Path = path;
        this.Checksum = checksum;
        this.Text = text;
    }

    /// <summary>
    /// Gets the path of the additional text.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the checksum of the content.
    /// </summary>
    public ImmutableArray<byte> Checksum { get; }

    /// <summary>
    /// Gets the content. It is not part of the file's identity, which <see cref="Checksum"/> represents.
    /// </summary>
    public SourceText? Text { get; }

    /// <summary>
    /// Creates the input for an additional text.
    /// </summary>
    /// <param name="additionalText">The additional text.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The schema file.</returns>
    public static SchemaFile Create(AdditionalText additionalText, CancellationToken token)
    {
        SourceText? text = additionalText.GetText(token);
        return new(additionalText.Path, text?.GetChecksum() ?? ImmutableArray<byte>.Empty, text);
    }

    /// <inheritdoc/>
    public bool Equals(SchemaFile? other)
    {
        return other is not null &&
            string.Equals(this.Path, other.Path, StringComparison.Ordinal) &&
            this.Checksum.SequenceEqual(other.Checksum);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is SchemaFile other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        int hash = StringComparer.Ordinal.GetHashCode(this.Path);
        foreach (byte value in this.Checksum)
        {
            hash = unchecked((hash * 31) + value);
        }

        return hash;
    }
}

/// <summary>
/// The documents that the schema files of one generation provide, parsed through a cache that lives as long as
/// the texts, and the fingerprint (path and checksum) of the file that provides each document URI.
/// </summary>
internal sealed class SchemaFileSet
{
    private static readonly ConditionalWeakTable<SourceText, ParsedSchemaFile> ParsedFiles = new();

    private readonly List<ParsedSchemaFile> files;
    private readonly Dictionary<string, string> fingerprintsByUri;

    private SchemaFileSet(List<ParsedSchemaFile> files, Dictionary<string, string> fingerprintsByUri)
    {
        this.files = files;
        this.fingerprintsByUri = fingerprintsByUri;
    }

    /// <summary>
    /// Creates the set for the schema files of a generation.
    /// </summary>
    /// <param name="schemaFiles">The schema files, in pipeline order.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The schema file set.</returns>
    public static SchemaFileSet Create(ImmutableArray<SchemaFile> schemaFiles, CancellationToken token)
    {
        List<ParsedSchemaFile> files = new(schemaFiles.Length);
        Dictionary<string, string> fingerprints = new(StringComparer.Ordinal);
        foreach (SchemaFile schemaFile in schemaFiles)
        {
            if (token.IsCancellationRequested)
            {
                break;
            }

            ParsedSchemaFile parsed = Parse(schemaFile);
            files.Add(parsed);
            if (parsed.Document is null)
            {
                continue;
            }

            // The first file to provide a URI wins, as in the resolver (AddDocument does not replace).
            if (parsed.NormalizedReference is string normalizedReference && !fingerprints.ContainsKey(normalizedReference))
            {
                fingerprints.Add(normalizedReference, parsed.Fingerprint);
            }

            if (parsed.Id is string id && !fingerprints.ContainsKey(id))
            {
                fingerprints.Add(id, parsed.Fingerprint);
            }
        }

        return new(files, fingerprints);
    }

    /// <summary>
    /// Creates the document resolver for a generation, with the documents registered in the same order and under
    /// the same URIs as <see cref="SourceGeneratorHelpers.BuildDocumentResolver(ImmutableArray{AdditionalText}, CancellationToken)"/>.
    /// </summary>
    /// <returns>The document resolver.</returns>
    public IDocumentResolver CreateResolver()
    {
        PrepopulatedDocumentResolver resolver = new();
        foreach (ParsedSchemaFile file in this.files)
        {
            if (file.Document is null)
            {
                continue;
            }

            if (file.NormalizedReference is string normalizedReference)
            {
                resolver.AddDocument(normalizedReference, file.Document);
            }

            if (file.Id is string id)
            {
                resolver.AddDocument(id, file.Document);
            }
        }

        return SourceGeneratorHelpers.ChainMetaschemas(resolver);
    }

    /// <summary>
    /// Gets the fingerprint of the file that provides each of a set of document URIs, or <see langword="null"/> for
    /// a URI that no file provides.
    /// </summary>
    /// <param name="uris">The document URIs.</param>
    /// <returns>The fingerprints by URI.</returns>
    public Dictionary<string, string?> GetFingerprints(IEnumerable<string> uris)
    {
        Dictionary<string, string?> result = new(StringComparer.Ordinal);
        foreach (string uri in uris)
        {
            result[uri] = this.fingerprintsByUri.TryGetValue(uri, out string? fingerprint) ? fingerprint : null;
        }

        return result;
    }

    /// <summary>
    /// Determines whether every document URI in a set of fingerprints is still provided by the same file content
    /// (or still provided by no file).
    /// </summary>
    /// <param name="fingerprints">The fingerprints recorded for an earlier generation.</param>
    /// <returns><see langword="true"/> if nothing that generation read has changed.</returns>
    public bool Matches(IReadOnlyDictionary<string, string?> fingerprints)
    {
        foreach (KeyValuePair<string, string?> dependency in fingerprints)
        {
            string? current = this.fingerprintsByUri.TryGetValue(dependency.Key, out string? fingerprint) ? fingerprint : null;
            if (!string.Equals(current, dependency.Value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static ParsedSchemaFile Parse(SchemaFile schemaFile)
    {
        if (schemaFile.Text is not SourceText text)
        {
            return new ParsedSchemaFile(schemaFile.Path, null, null, null, string.Empty);
        }

        if (ParsedFiles.TryGetValue(text, out ParsedSchemaFile? cached) &&
            string.Equals(cached.Path, schemaFile.Path, StringComparison.Ordinal))
        {
            return cached;
        }

        ParsedSchemaFile parsed = ParseCore(schemaFile.Path, text, schemaFile.Path + "|" + BitConverter.ToString(schemaFile.Checksum.ToArray()));
        if (cached is not null)
        {
            // The same text under another path: use it without caching.
            return parsed;
        }

        ParsedSchemaFile stored = ParsedFiles.GetValue(text, _ => parsed);
        return string.Equals(stored.Path, schemaFile.Path, StringComparison.Ordinal) ? stored : parsed;
    }

    private static ParsedSchemaFile ParseCore(string path, SourceText text, string fingerprint)
    {
        JsonDocument? doc;

        try
        {
            if (path.EndsWith(".yaml") || path.EndsWith(".yml"))
            {
                string? yaml = text.ToString();
                doc = yaml is not null ? YamlDocument.Parse(yaml) : null;
            }
            else
            {
                string? json = text.ToString();
                doc = json is not null ? JsonDocument.Parse(json) : null;
            }
        }
        catch (YamlException)
        {
            doc = null;
        }
        catch (JsonException)
        {
            doc = null;
        }

        string? normalizedReference = null;
        string? id = null;
        if (doc is not null)
        {
            if (SchemaReferenceNormalization.TryNormalizeSchemaReference(path, string.Empty, out string? normalized))
            {
                normalizedReference = normalized;
            }

            if (doc.RootElement.TryGetProperty("$id", out JsonElement idElement) &&
                idElement.ValueKind == JsonValueKind.String)
            {
                id = idElement.GetString()!;
            }
        }

        return new ParsedSchemaFile(path, doc, normalizedReference, id, fingerprint);
    }

    private sealed class ParsedSchemaFile(string path, JsonDocument? document, string? normalizedReference, string? id, string fingerprint)
    {
        public string Path { get; } = path;

        public JsonDocument? Document { get; } = document;

        public string? NormalizedReference { get; } = normalizedReference;

        public string? Id { get; } = id;

        public string Fingerprint { get; } = fingerprint;
    }
}

/// <summary>
/// A document resolver that records the URI of every document a generation asks it for, so that the generation's
/// output can be reused while none of those documents changes.
/// </summary>
/// <param name="inner">The resolver to delegate to.</param>
internal sealed class RecordingDocumentResolver(IDocumentResolver inner) : IDocumentResolver
{
    private readonly HashSet<string> resolvedUris = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the document URIs that have been resolved (successfully or not).
    /// </summary>
    public IEnumerable<string> ResolvedUris => this.resolvedUris;

    /// <inheritdoc/>
    public bool AddDocument(string uri, JsonDocument document) => inner.AddDocument(uri, document);

    /// <inheritdoc/>
    public ValueTask<JsonElement?> TryResolve(JsonReference reference)
    {
        this.resolvedUris.Add(reference.Uri.ToString());
        return inner.TryResolve(reference);
    }

    /// <inheritdoc/>
    public void Reset() => inner.Reset();

    /// <inheritdoc/>
    public void Dispose() => inner.Dispose();
}

/// <summary>
/// The outputs of recent successful generations, keyed by the generation specifications, the options and the
/// fingerprints of every document each generation read.
/// </summary>
/// <remarks>
/// The incremental pipeline has to run the output step whenever any schema file changes, because it cannot know
/// which files a generation reads. A generation whose specifications (in order), options and read documents are
/// all unchanged produces the same sources, so they are added again without building the types. A change in the
/// set or order of generation roots changes the key, so it always runs a full generation.
/// </remarks>
internal sealed class GenerationMemo
{
    private const int Capacity = 4;

    private readonly List<Entry> entries = [];

    /// <summary>
    /// Tries to get the sources of an earlier generation with the same inputs.
    /// </summary>
    /// <param name="specifications">The generation specifications.</param>
    /// <param name="options">The global options.</param>
    /// <param name="files">The current schema files.</param>
    /// <param name="sources">The sources, if found.</param>
    /// <returns><see langword="true"/> if the sources were found.</returns>
    public bool TryGet(ImmutableArray<SourceGeneratorHelpers.GenerationSpecification> specifications, object options, SchemaFileSet files, [NotNullWhen(true)] out IReadOnlyList<(string HintName, SourceText Text)>? sources)
    {
        lock (this.entries)
        {
            for (int i = this.entries.Count - 1; i >= 0; i--)
            {
                Entry entry = this.entries[i];
                if (entry.HasKey(specifications, options) && files.Matches(entry.Fingerprints))
                {
                    this.entries.RemoveAt(i);
                    this.entries.Add(entry);
                    sources = entry.Sources;
                    return true;
                }
            }
        }

        sources = null;
        return false;
    }

    /// <summary>
    /// Stores the sources of a successful generation, replacing any earlier entry for the same specifications and options.
    /// </summary>
    /// <param name="specifications">The generation specifications.</param>
    /// <param name="options">The global options.</param>
    /// <param name="fingerprints">The fingerprints of the documents the generation read.</param>
    /// <param name="sources">The sources.</param>
    public void Store(ImmutableArray<SourceGeneratorHelpers.GenerationSpecification> specifications, object options, IReadOnlyDictionary<string, string?> fingerprints, IReadOnlyList<(string HintName, SourceText Text)> sources)
    {
        lock (this.entries)
        {
            this.entries.RemoveAll(e => e.HasKey(specifications, options));
            if (this.entries.Count >= Capacity)
            {
                this.entries.RemoveAt(0);
            }

            this.entries.Add(new Entry(specifications, options, fingerprints, sources));
        }
    }

    private sealed class Entry(ImmutableArray<SourceGeneratorHelpers.GenerationSpecification> specifications, object options, IReadOnlyDictionary<string, string?> fingerprints, IReadOnlyList<(string HintName, SourceText Text)> sources)
    {
        public IReadOnlyDictionary<string, string?> Fingerprints { get; } = fingerprints;

        public IReadOnlyList<(string HintName, SourceText Text)> Sources { get; } = sources;

        public bool HasKey(ImmutableArray<SourceGeneratorHelpers.GenerationSpecification> otherSpecifications, object otherOptions)
        {
            return specifications.SequenceEqual(otherSpecifications) && Equals(options, otherOptions);
        }
    }
}