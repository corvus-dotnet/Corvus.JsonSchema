// <copyright file="SchemaLoader.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
#if !STJ
using Corvus.Text.Json.Internal;
#endif

namespace Corvus.Text.Json.RuntimeEvaluator.Compilation;

/// <summary>
/// A parsed schema document.
/// </summary>
internal sealed class SchemaDocument
{
    public SchemaDocument(int id, ParsedJsonDocument<JsonElement> document, string retrievalUri)
    {
        this.Id = id;
        this.Document = document;
        this.RetrievalUri = retrievalUri;
    }

    public int Id { get; }

    public ParsedJsonDocument<JsonElement> Document { get; }

    public string RetrievalUri { get; }

    public JsonElement GetElement(int index) => Elements.Create<JsonElement>(this.Document, index);
}

/// <summary>
/// A schema resource: a schema identified by a URI (a document root or an embedded <c>$id</c>).
/// </summary>
internal sealed class SchemaResource
{
    public SchemaResource(int id, SchemaDocument document, int rootIndex, string uri, JsonSchemaDialect dialect, JsonSchemaVocabularies vocabularies)
    {
        this.Id = id;
        this.Document = document;
        this.RootIndex = rootIndex;
        this.Uri = uri;
        this.Dialect = dialect;
        this.Vocabularies = vocabularies;
    }

    public int Id { get; }

    public SchemaDocument Document { get; }

    public int RootIndex { get; }

    public string Uri { get; set; }

    public JsonSchemaDialect Dialect { get; }

    public JsonSchemaVocabularies Vocabularies { get; }

    public bool RecursiveAnchor { get; set; }

    public Dictionary<string, int>? Anchors { get; set; }

    public Dictionary<string, int>? DynamicAnchors { get; set; }

    public JsonElement Root => this.Document.GetElement(this.RootIndex);
}

/// <summary>
/// The target of a reference: an element in a document, and the resource it belongs to.
/// </summary>
internal readonly record struct SchemaTarget(SchemaDocument Document, int Index, SchemaResource Resource)
{
    public JsonElement Element => this.Document.GetElement(this.Index);
}

/// <summary>
/// Loads schema documents, identifies resources and anchors, and resolves references.
/// </summary>
internal sealed class SchemaLoader
{
    private const string DefaultRootUri = "https://corvus-oss.org/runtime-evaluator/root.json";

    private readonly JsonSchemaEvaluatorOptions options;
    private readonly List<SchemaDocument> documents = [];
    private readonly List<SchemaResource> resources = [];
    private readonly Dictionary<string, SchemaResource> resourcesByUri = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SchemaDocument> documentsByUri = new(StringComparer.Ordinal);
    private readonly Dictionary<(int Doc, int Index), SchemaResource> resourceOfElement = [];
    private readonly Dictionary<string, (JsonSchemaDialect Dialect, JsonSchemaVocabularies Vocabularies)> metaschemaInfo = new(StringComparer.Ordinal);
    private readonly HashSet<string> metaschemaLoading = new(StringComparer.Ordinal);

    public SchemaLoader(JsonSchemaEvaluatorOptions options)
    {
        this.options = options;
    }

    public IReadOnlyList<SchemaDocument> Documents => this.documents;

    public IReadOnlyList<SchemaResource> Resources => this.resources;

    /// <summary>
    /// Loads the root schema and returns its resource.
    /// </summary>
    public SchemaResource LoadRoot(ReadOnlyMemory<byte> utf8Json, string? baseUri)
    {
        string uri = baseUri is null ? DefaultRootUri : UriUtilities.Normalize(baseUri);
        SchemaDocument doc = this.AddDocument(uri, utf8Json);
        return this.resourceOfElement[(doc.Id, Elements.Index(doc.Document.RootElement))];
    }

    /// <summary>
    /// Loads the root schema from a URI through the configured resolvers and returns its resource.
    /// </summary>
    public SchemaResource LoadRoot(string uri)
    {
        string normalized = UriUtilities.Normalize(uri);
        if (!this.TryLoadDocument(normalized) || !this.documentsByUri.TryGetValue(normalized, out SchemaDocument? doc))
        {
            throw new JsonSchemaCompilationException($"Unable to resolve the schema document '{uri}'.");
        }

        return this.resourceOfElement[(doc.Id, Elements.Index(doc.Document.RootElement))];
    }

    /// <summary>
    /// Gets the resource that an element belongs to, if it was visited during identification.
    /// </summary>
    public bool TryGetResourceOf(SchemaDocument document, int index, [NotNullWhen(true)] out SchemaResource? resource)
    {
        return this.resourceOfElement.TryGetValue((document.Id, index), out resource);
    }

    /// <summary>
    /// Resolves a reference relative to the given resource.
    /// </summary>
    public bool TryResolveReference(SchemaResource from, string reference, out SchemaTarget target)
    {
        UriUtilities.Split(reference, out string uriPart, out string fragment);
        string absolute = UriUtilities.Resolve(from.Uri, uriPart);

        if (!this.resourcesByUri.TryGetValue(absolute, out SchemaResource? resource))
        {
            if (!this.TryLoadDocument(absolute))
            {
                target = default;
                return false;
            }

            if (!this.resourcesByUri.TryGetValue(absolute, out resource))
            {
                target = default;
                return false;
            }
        }

        return this.TryResolveFragment(resource, UriUtilities.DecodeFragment(fragment), out target);
    }

    /// <summary>
    /// Resolves a fragment within a resource.
    /// </summary>
    public bool TryResolveFragment(SchemaResource resource, string fragment, out SchemaTarget target)
    {
        if (fragment.Length == 0)
        {
            target = new SchemaTarget(resource.Document, resource.RootIndex, resource);
            return true;
        }

        if (fragment[0] == '/')
        {
            JsonElement root = resource.Root;
            if (!root.TryResolvePointer(fragment.AsSpan(), out JsonElement resolved))
            {
                target = default;
                return false;
            }

            int index = Elements.Index(resolved);
            SchemaResource owner = this.resourceOfElement.TryGetValue((resource.Document.Id, index), out SchemaResource? r) ? r : resource;
            target = new SchemaTarget(resource.Document, index, owner);
            return true;
        }

        if (resource.Anchors is not null && resource.Anchors.TryGetValue(fragment, out int anchorIndex))
        {
            target = new SchemaTarget(resource.Document, anchorIndex, resource);
            return true;
        }

        target = default;
        return false;
    }

    /// <summary>
    /// Gets the dialect and vocabularies associated with a <c>$schema</c> URI.
    /// </summary>
    public (JsonSchemaDialect Dialect, JsonSchemaVocabularies Vocabularies) GetDialectInfo(string schemaUri)
    {
        string normalized = UriUtilities.Normalize(schemaUri);
        if (Metaschemas.TryGetDialect(normalized, out JsonSchemaDialect known))
        {
            return (known, JsonSchemaVocabularies.AllAnnotatingFormat);
        }

        if (this.metaschemaInfo.TryGetValue(normalized, out var info))
        {
            return info;
        }

        if (!this.metaschemaLoading.Add(normalized))
        {
            return (this.options.DefaultDialect, JsonSchemaVocabularies.AllAnnotatingFormat);
        }

        try
        {
            if (!this.resourcesByUri.TryGetValue(normalized, out SchemaResource? metaResource))
            {
                if (!this.TryLoadDocument(normalized) || !this.resourcesByUri.TryGetValue(normalized, out metaResource))
                {
                    info = (this.options.DefaultDialect, JsonSchemaVocabularies.AllAnnotatingFormat);
                    this.metaschemaInfo[normalized] = info;
                    return info;
                }
            }

            JsonElement meta = metaResource.Root;
            JsonSchemaDialect dialect = metaResource.Dialect;
            JsonSchemaVocabularies vocabularies = JsonSchemaVocabularies.AllAnnotatingFormat;

            if (meta.ValueKind == JsonValueKind.Object && meta.TryGetProperty("$vocabulary"u8, out JsonElement vocab) && vocab.ValueKind == JsonValueKind.Object)
            {
                vocabularies = JsonSchemaVocabularies.None;
                foreach (JsonProperty<JsonElement> p in vocab.EnumerateObject())
                {
                    string name = p.Name;
                    bool required = p.Value.ValueKind == JsonValueKind.True;
                    JsonSchemaVocabularies flag = name switch
                    {
                        "https://json-schema.org/draft/2020-12/vocab/core" or "https://json-schema.org/draft/2019-09/vocab/core" => JsonSchemaVocabularies.Core,
                        "https://json-schema.org/draft/2020-12/vocab/applicator" or "https://json-schema.org/draft/2019-09/vocab/applicator" => JsonSchemaVocabularies.Applicator,
                        "https://json-schema.org/draft/2020-12/vocab/validation" or "https://json-schema.org/draft/2019-09/vocab/validation" => JsonSchemaVocabularies.Validation,
                        "https://json-schema.org/draft/2020-12/vocab/meta-data" or "https://json-schema.org/draft/2019-09/vocab/meta-data" => JsonSchemaVocabularies.MetaData,
                        "https://json-schema.org/draft/2020-12/vocab/format-annotation" or "https://json-schema.org/draft/2019-09/vocab/format" => JsonSchemaVocabularies.FormatAnnotation,
                        "https://json-schema.org/draft/2020-12/vocab/format-assertion" => JsonSchemaVocabularies.FormatAssertion,
                        "https://json-schema.org/draft/2020-12/vocab/content" or "https://json-schema.org/draft/2019-09/vocab/content" => JsonSchemaVocabularies.Content,
                        "https://json-schema.org/draft/2020-12/vocab/unevaluated" => JsonSchemaVocabularies.Unevaluated,
                        _ => JsonSchemaVocabularies.None,
                    };

                    // A vocabulary listed as optional (false) that we understand is still applied.
                    _ = required;
                    vocabularies |= flag;
                }

                // Core is always in effect.
                vocabularies |= JsonSchemaVocabularies.Core;
            }

            info = (dialect, vocabularies);
            this.metaschemaInfo[normalized] = info;
            return info;
        }
        finally
        {
            this.metaschemaLoading.Remove(normalized);
        }
    }

    private bool TryLoadDocument(string absoluteUri)
    {
        if (this.documentsByUri.ContainsKey(absoluteUri))
        {
            return true;
        }

        ReadOnlyMemory<byte> utf8;
        if (this.options.DocumentResolver is JsonSchemaDocumentResolver resolver && resolver(absoluteUri, out utf8))
        {
            this.AddDocument(absoluteUri, utf8);
            return true;
        }

        if (Metaschemas.TryGet(absoluteUri, out utf8))
        {
            this.AddDocument(absoluteUri, utf8);
            return true;
        }

        if (this.options.FallbackDocumentResolver is JsonSchemaDocumentResolver fallback && fallback(absoluteUri, out utf8))
        {
            this.AddDocument(absoluteUri, utf8);
            return true;
        }

        return false;
    }

    private SchemaDocument AddDocument(string uri, ReadOnlyMemory<byte> utf8Json)
    {
        // Copy the JSON so that the document is independent of the caller's buffer lifetime.
        byte[] copy = utf8Json.ToArray();
        ParsedJsonDocument<JsonElement> parsed = ParsedJsonDocument<JsonElement>.Parse(copy);
        var doc = new SchemaDocument(this.documents.Count, parsed, uri);
        this.documents.Add(doc);
        this.documentsByUri[uri] = doc;

        JsonElement root = parsed.RootElement;
        (JsonSchemaDialect dialect, JsonSchemaVocabularies vocabularies) = this.GetRootDialect(root);
        SchemaResource resource = this.CreateResource(doc, Elements.Index(root), uri, dialect, vocabularies);
        this.Walk(doc, root, resource, isResourceRoot: true);
        return doc;
    }

    private (JsonSchemaDialect, JsonSchemaVocabularies) GetRootDialect(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("$schema"u8, out JsonElement schema) && schema.ValueKind == JsonValueKind.String)
        {
            return this.GetDialectInfo(schema.GetString()!);
        }

        return (this.options.DefaultDialect, JsonSchemaVocabularies.AllAnnotatingFormat);
    }

    private SchemaResource CreateResource(SchemaDocument doc, int rootIndex, string uri, JsonSchemaDialect dialect, JsonSchemaVocabularies vocabularies)
    {
        var resource = new SchemaResource(this.resources.Count, doc, rootIndex, uri, dialect, vocabularies);
        this.resources.Add(resource);
        this.resourcesByUri.TryAdd(uri, resource);
        return resource;
    }

    private void Walk(SchemaDocument doc, JsonElement element, SchemaResource resource, bool isResourceRoot)
    {
        int index = Elements.Index(element);

        if (element.ValueKind != JsonValueKind.Object)
        {
            this.resourceOfElement[(doc.Id, index)] = resource;
            return;
        }

        JsonSchemaDialect dialect = resource.Dialect;
        JsonSchemaVocabularies vocabularies = resource.Vocabularies;

        if (!isResourceRoot && element.TryGetProperty("$schema"u8, out JsonElement schemaKeyword) && schemaKeyword.ValueKind == JsonValueKind.String)
        {
            // $schema in a subschema only takes effect alongside an $id (embedded resource). We honour it
            // when the element declares an identifier below; otherwise it is ignored.
            (dialect, vocabularies) = this.GetDialectInfo(schemaKeyword.GetString()!);
        }

        bool legacyRefOverridesSiblings = dialect <= JsonSchemaDialect.Draft7 && element.TryGetProperty("$ref"u8, out JsonElement legacyRef) && legacyRef.ValueKind == JsonValueKind.String;

        if (!legacyRefOverridesSiblings)
        {
            ReadOnlySpan<byte> idKeyword = dialect == JsonSchemaDialect.Draft4 ? "id"u8 : "$id"u8;
            if (element.TryGetProperty(idKeyword, out JsonElement idElement) && idElement.ValueKind == JsonValueKind.String)
            {
                string idValue = idElement.GetString()!;
                UriUtilities.Split(idValue, out string uriPart, out string fragment);
                if (uriPart.Length == 0)
                {
                    if (fragment.Length > 0 && dialect <= JsonSchemaDialect.Draft7)
                    {
                        (resource.Anchors ??= new(StringComparer.Ordinal)).TryAdd(fragment, index);
                    }
                }
                else
                {
                    string absolute = UriUtilities.Resolve(resource.Uri, uriPart);
                    if (!isResourceRoot || !string.Equals(absolute, resource.Uri, StringComparison.Ordinal))
                    {
                        if (isResourceRoot)
                        {
                            // The document root declares an $id that differs from its retrieval URI. The $id
                            // becomes the base URI; the resource stays reachable under both.
                            this.resourcesByUri.TryAdd(absolute, resource);
                            resource.Uri = absolute;
                        }
                        else
                        {
                            resource = this.CreateResource(doc, index, absolute, dialect, vocabularies);
                            isResourceRoot = true;
                        }
                    }

                    if (fragment.Length > 0 && dialect <= JsonSchemaDialect.Draft7)
                    {
                        (resource.Anchors ??= new(StringComparer.Ordinal)).TryAdd(fragment, index);
                    }
                }
            }

            if (dialect >= JsonSchemaDialect.Draft201909 && element.TryGetProperty("$anchor"u8, out JsonElement anchor) && anchor.ValueKind == JsonValueKind.String)
            {
                (resource.Anchors ??= new(StringComparer.Ordinal)).TryAdd(anchor.GetString()!, index);
            }

            if (dialect >= JsonSchemaDialect.Draft202012 && element.TryGetProperty("$dynamicAnchor"u8, out JsonElement dynamicAnchor) && dynamicAnchor.ValueKind == JsonValueKind.String)
            {
                string name = dynamicAnchor.GetString()!;
                (resource.DynamicAnchors ??= new(StringComparer.Ordinal)).TryAdd(name, index);
                (resource.Anchors ??= new(StringComparer.Ordinal)).TryAdd(name, index);
            }

            if (dialect == JsonSchemaDialect.Draft201909 && isResourceRoot && element.TryGetProperty("$recursiveAnchor"u8, out JsonElement recursiveAnchor) && recursiveAnchor.ValueKind == JsonValueKind.True)
            {
                resource.RecursiveAnchor = true;
            }
        }

        this.resourceOfElement[(doc.Id, index)] = resource;

        foreach (JsonProperty<JsonElement> property in element.EnumerateObject())
        {
            JsonElement value = property.Value;
            SubschemaKeywordKind kind = SchemaKeywords.GetSubschemaKind(property.Utf8NameSpan.Span, dialect, legacyRefOverridesSiblings);
            switch (kind)
            {
                case SubschemaKeywordKind.None:
                    break;
                case SubschemaKeywordKind.Single:
                    if (value.ValueKind is JsonValueKind.Object or JsonValueKind.True or JsonValueKind.False)
                    {
                        this.Walk(doc, value, resource, isResourceRoot: false);
                    }

                    break;
                case SubschemaKeywordKind.SingleOrArray:
                    if (value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement item in value.EnumerateArray())
                        {
                            this.Walk(doc, item, resource, isResourceRoot: false);
                        }
                    }
                    else if (value.ValueKind is JsonValueKind.Object or JsonValueKind.True or JsonValueKind.False)
                    {
                        this.Walk(doc, value, resource, isResourceRoot: false);
                    }

                    break;
                case SubschemaKeywordKind.Array:
                    if (value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement item in value.EnumerateArray())
                        {
                            this.Walk(doc, item, resource, isResourceRoot: false);
                        }
                    }

                    break;
                case SubschemaKeywordKind.Map:
                    if (value.ValueKind == JsonValueKind.Object)
                    {
                        foreach (JsonProperty<JsonElement> entry in value.EnumerateObject())
                        {
                            if (entry.Value.ValueKind is JsonValueKind.Object or JsonValueKind.True or JsonValueKind.False)
                            {
                                this.Walk(doc, entry.Value, resource, isResourceRoot: false);
                            }
                        }
                    }

                    break;
            }
        }
    }
}