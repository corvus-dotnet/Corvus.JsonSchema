// <copyright file="ExternalReferenceResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Resolves OpenAPI <c>$ref</c> URI-references, supporting local (fragment-only),
/// relative, and absolute URI references per OpenAPI 3.1.2 §4.6.
/// </summary>
/// <remarks>
/// <para>
/// Documents can be supplied in two ways:
/// </para>
/// <list type="number">
/// <item><description>
/// <b>Pre-registered</b> via <see cref="AddDocument(Uri, JsonElement)"/> — the caller
/// provides the document root together with its canonical URI. This enables bundling
/// files from any source (file system, HTTP, embedded resource, test fixture) while
/// preserving their original URI relationships. Pre-registered documents are <b>not</b>
/// owned by the resolver; the caller manages their lifetime.
/// </description></item>
/// <item><description>
/// <b>Lazily loaded</b> — if no pre-registered document matches, the resolver falls
/// back to loading from the local file system. Lazily loaded documents are owned by the
/// resolver and disposed when <see cref="Dispose"/> is called.
/// </description></item>
/// </list>
/// <para>
/// The three kinds of <c>$ref</c> URI-references are handled as follows:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Fragment-only</b> (e.g. <c>#/components/parameters/PetId</c>) — resolved within
/// the entry document.
/// </description></item>
/// <item><description>
/// <b>Relative</b> (e.g. <c>./common.json#/components/schemas/Pet</c>) — resolved
/// against the entry document's base URI, then looked up in the registry or loaded
/// from disk.
/// </description></item>
/// <item><description>
/// <b>Absolute</b> (e.g. <c>https://example.com/api.json#/components/schemas/Pet</c>)
/// — looked up directly in the registry. If not pre-registered and the URI is a
/// <c>file://</c> URI, falls back to file-system loading.
/// </description></item>
/// </list>
/// </remarks>
public sealed class ExternalReferenceResolver : IOpenApiReferenceResolver, IDisposable
{
    private readonly JsonElement entryDocumentRoot;
    private readonly Uri baseUri;

    // Optional hook for loading an external document (of any URI scheme) from a virtualized source —
    // e.g. an in-memory registry, an HTTP client, or a build-artifact store. Consulted before the
    // file-system fallback, so documents that "came from anywhere" resolve without touching disk.
    private readonly Func<Uri, byte[]?>? externalDocumentLoader;

    // Pre-registered documents keyed by canonical URI string.
    // These are NOT owned by the resolver — the caller manages their lifetime.
    private readonly Dictionary<string, JsonElement> registeredDocuments = new(StringComparer.Ordinal);

    // Lazily loaded documents (owned by the resolver — disposed on Dispose).
    private readonly Dictionary<string, ParsedJsonDocument<JsonElement>> loadedDocuments = new(StringComparer.Ordinal);

    private readonly Stack<(string Key, JsonElement Root)> baseStack = new();

    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalReferenceResolver"/> class.
    /// </summary>
    /// <param name="entryDocumentRoot">The root element of the entry (main) OpenAPI document.</param>
    /// <param name="entryDocumentPath">
    /// The file-system path of the entry document. Used as the base URI for resolving
    /// relative references. Must be an absolute path.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="entryDocumentPath"/> is not an absolute path.</exception>
    public ExternalReferenceResolver(JsonElement entryDocumentRoot, string entryDocumentPath)
        : this(entryDocumentRoot, ToFileBaseUri(entryDocumentPath), null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalReferenceResolver"/> class with an explicit
    /// base URI (which need not be a file path) and an optional loader for virtualized external documents.
    /// </summary>
    /// <param name="entryDocumentRoot">The root element of the entry (main) OpenAPI document.</param>
    /// <param name="baseUri">
    /// The absolute base URI the entry document was retrieved from — relative <c>$ref</c>s resolve against
    /// it (RFC 3986 §5). May be a <c>file:</c>, <c>http(s):</c>, or any other absolute URI, so the entry
    /// document need not live on disk.
    /// </param>
    /// <param name="externalDocumentLoader">
    /// An optional callback that loads an external document's raw UTF-8 JSON bytes by its resolved
    /// absolute URI, or returns <see langword="null"/> when it cannot. When supplied it is consulted
    /// before the file-system fallback, letting external <c>$ref</c>s resolve from any source (an
    /// in-memory registry, HTTP, an embedded resource). Documents it returns are owned (and disposed) by
    /// this resolver.
    /// </param>
    public ExternalReferenceResolver(JsonElement entryDocumentRoot, Uri baseUri, Func<Uri, byte[]?>? externalDocumentLoader = null)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        this.entryDocumentRoot = entryDocumentRoot;
        this.baseUri = baseUri;
        this.externalDocumentLoader = externalDocumentLoader;
    }

    private static Uri ToFileBaseUri(string entryDocumentPath)
    {
        if (!Path.IsPathFullyQualified(entryDocumentPath))
        {
            throw new ArgumentException(
                SR.Format(SR.EntryDocumentPathMustBeFullyQualified, entryDocumentPath),
                nameof(entryDocumentPath));
        }

        return new Uri(entryDocumentPath);
    }

    private Uri CurrentBaseUri => this.baseStack.Count > 0
        ? new Uri(this.baseStack.Peek().Key)
        : this.baseUri;

    private JsonElement CurrentBaseDocument => this.baseStack.Count > 0
        ? this.baseStack.Peek().Root
        : this.entryDocumentRoot;

    /// <summary>
    /// Pre-registers an external document with its canonical URI.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This allows documents from any source (file system, HTTP, embedded resources,
    /// test fixtures) to be bundled together while preserving their original URI
    /// relationships. When a <c>$ref</c> resolves to a URI that matches
    /// <paramref name="canonicalUri"/>, the resolver uses the supplied
    /// <paramref name="documentRoot"/> instead of attempting file-system loading.
    /// </para>
    /// <para>
    /// The caller is responsible for keeping the backing <see cref="ParsedJsonDocument{T}"/>
    /// alive for the lifetime of the resolver. The resolver does <b>not</b> take ownership
    /// of pre-registered documents.
    /// </para>
    /// </remarks>
    /// <param name="canonicalUri">
    /// The canonical URI for this document. When a <c>$ref</c> resolves to this URI
    /// (after base URI resolution), the registered document is used. For example,
    /// <c>new Uri("https://example.com/schemas/Pet.json")</c> or
    /// <c>new Uri("file:///D:/specs/schemas/Pet.json")</c>.
    /// </param>
    /// <param name="documentRoot">The root element of the document.</param>
    public void AddDocument(Uri canonicalUri, JsonElement documentRoot)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        this.registeredDocuments[canonicalUri.AbsoluteUri] = documentRoot;
    }

    /// <summary>
    /// Pre-registers an external document using a path relative to the entry document.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a convenience overload that resolves <paramref name="relativePath"/>
    /// against the entry document's base URI before registering. It is equivalent to
    /// calling <see cref="AddDocument(Uri, JsonElement)"/> with
    /// <c>new Uri(baseUri, relativePath)</c>.
    /// </para>
    /// </remarks>
    /// <param name="relativePath">
    /// A relative path from the entry document (e.g. <c>"./schemas/Pet.json"</c>
    /// or <c>"../common/Error.json"</c>).
    /// </param>
    /// <param name="documentRoot">The root element of the document.</param>
    public void AddDocument(string relativePath, JsonElement documentRoot)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        Uri resolved = new(this.baseUri, relativePath);
        this.registeredDocuments[resolved.AbsoluteUri] = documentRoot;
    }

    /// <summary>
    /// Pre-registers an external document from raw JSON bytes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The bytes are parsed into a <see cref="ParsedJsonDocument{T}"/> which the resolver
    /// takes ownership of (disposed on <see cref="Dispose"/>). This overload is the
    /// recommended entry point when loading documents from any source (file, HTTP, embedded
    /// resource) because the same raw bytes can be independently fed to the V4
    /// <c>PrepopulatedDocumentResolver</c> (via <c>System.Text.Json.JsonDocument.Parse</c>)
    /// for the JSON Schema type-generation pipeline.
    /// </para>
    /// </remarks>
    /// <param name="canonicalUri">
    /// The canonical URI for this document (see <see cref="AddDocument(Uri, JsonElement)"/>).
    /// </param>
    /// <param name="rawJsonBytes">The UTF-8 JSON bytes of the document.</param>
    public void AddDocument(Uri canonicalUri, byte[] rawJsonBytes)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        string key = canonicalUri.AbsoluteUri;
        ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(rawJsonBytes);
        this.loadedDocuments[key] = doc;
    }

    /// <summary>
    /// Pre-registers an external document from raw JSON bytes, using a path relative to the
    /// entry document.
    /// </summary>
    /// <param name="relativePath">
    /// A relative path from the entry document (e.g. <c>"./schemas/Pet.json"</c>).
    /// </param>
    /// <param name="rawJsonBytes">The UTF-8 JSON bytes of the document.</param>
    public void AddDocument(string relativePath, byte[] rawJsonBytes)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        Uri resolved = new(this.baseUri, relativePath);
        ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(rawJsonBytes);
        this.loadedDocuments[resolved.AbsoluteUri] = doc;
    }

    /// <inheritdoc/>
    public bool TryResolve(ReadOnlySpan<byte> refValue, out JsonElement result)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);

        if (refValue.Length == 0)
        {
            result = default;
            return false;
        }

        // Fragment-only reference — resolve within CURRENT base document (RFC 3986 §5)
        if (refValue[0] == (byte)'#')
        {
            ReadOnlySpan<byte> pointer = refValue.Slice(1);
            return this.CurrentBaseDocument.TryResolvePointer(pointer, out result);
        }

        // External reference — convert to string for URI parsing
        string refString = System.Text.Encoding.UTF8.GetString(refValue);
        return this.TryResolveExternal(refString, out result);
    }

    /// <inheritdoc/>
    public bool TryResolve(string refValue, out JsonElement result)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);

        if (string.IsNullOrEmpty(refValue))
        {
            result = default;
            return false;
        }

        // Fragment-only reference — resolve within CURRENT base document (RFC 3986 §5)
        if (refValue[0] == '#')
        {
            ReadOnlySpan<char> pointer = refValue.AsSpan(1);
            return this.CurrentBaseDocument.TryResolvePointer(pointer, out result);
        }

        return this.TryResolveExternal(refValue, out result);
    }

    /// <inheritdoc/>
    public bool TryResolve<TTarget>(string refValue, out TTarget result)
        where TTarget : struct, IJsonElement<TTarget>
    {
        if (this.TryResolve(refValue, out JsonElement element))
        {
            IJsonElement ie = element;
            TTarget candidate = TTarget.CreateInstance(ie.ParentDocument, ie.ParentDocumentIndex);
            if (candidate.EvaluateSchema())
            {
                result = candidate;
                return true;
            }
        }

        result = default;
        return false;
    }

    /// <inheritdoc/>
    public bool TryResolve<TTarget>(ReadOnlySpan<byte> refValue, out TTarget result)
        where TTarget : struct, IJsonElement<TTarget>
    {
        if (this.TryResolve(refValue, out JsonElement element))
        {
            IJsonElement ie = element;
            TTarget candidate = TTarget.CreateInstance(ie.ParentDocument, ie.ParentDocumentIndex);
            if (candidate.EvaluateSchema())
            {
                result = candidate;
                return true;
            }
        }

        result = default;
        return false;
    }

    /// <inheritdoc/>
    public IDisposable PushResolvedBase(string refValue)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);

        if (string.IsNullOrEmpty(refValue) || refValue[0] == '#')
        {
            return EmptyScope.Instance;
        }

        // Extract the document URI (before the fragment)
        int hashIndex = refValue.IndexOf('#');
        string docPart = hashIndex >= 0 ? refValue.Substring(0, hashIndex) : refValue;

        Uri resolved = new(this.CurrentBaseUri, docPart);
        string key = resolved.AbsoluteUri;

        // Look up the document
        if (this.registeredDocuments.TryGetValue(key, out JsonElement _))
        {
            this.baseStack.Push((key, this.registeredDocuments[key]));
            return new BaseScope(this);
        }

        if (this.loadedDocuments.TryGetValue(key, out ParsedJsonDocument<JsonElement>? loaded))
        {
            this.baseStack.Push((key, loaded.RootElement));
            return new BaseScope(this);
        }

        return EmptyScope.Instance;
    }

    /// <inheritdoc/>
    public string ResolveToAbsolute(string refValue)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);

        // Fragment-only — resolves against entry doc, return as-is
        if (string.IsNullOrEmpty(refValue) || refValue[0] == '#')
        {
            return refValue;
        }

        // Separate doc part from fragment
        int hashIndex = refValue.IndexOf('#');
        string docPart = hashIndex >= 0 ? refValue[..hashIndex] : refValue;
        string fragment = hashIndex >= 0 ? refValue[hashIndex..] : string.Empty;

        // Resolve doc part against current base URI (RFC 3986 §5)
        Uri resolved = new(this.CurrentBaseUri, docPart);

        // For file:// URIs, return the local path; for other schemes, return the absolute URI
        string absoluteDoc = resolved.IsFile ? resolved.LocalPath : resolved.AbsoluteUri;

        return string.Concat(absoluteDoc, fragment);
    }

    /// <summary>
    /// Releases all lazily loaded external documents.
    /// </summary>
    /// <remarks>
    /// Pre-registered documents (added via <see cref="AddDocument(Uri, JsonElement)"/>)
    /// are <b>not</b> disposed — their lifetime is managed by the caller.
    /// </remarks>
    public void Dispose()
    {
        if (!this.disposed)
        {
            foreach (ParsedJsonDocument<JsonElement> doc in this.loadedDocuments.Values)
            {
                doc.Dispose();
            }

            this.loadedDocuments.Clear();
            this.registeredDocuments.Clear();
            this.disposed = true;
        }
    }

    /// <summary>
    /// Tries to resolve an external (non-fragment-only) URI-reference.
    /// </summary>
    private bool TryResolveExternal(string refValue, out JsonElement result)
    {
        // Split into URI and fragment parts
        SplitReference(refValue, out string uriPart, out string? fragment);

        // Resolve the URI against the current base URI (RFC 3986 §5)
        Uri resolvedUri = new(this.CurrentBaseUri, uriPart);
        string key = resolvedUri.AbsoluteUri;

        // 1. Check pre-registered documents (caller-managed lifetime)
        if (this.registeredDocuments.TryGetValue(key, out JsonElement registeredRoot))
        {
            return NavigateFragment(registeredRoot, fragment, out result);
        }

        // 2. Check owned documents (byte-loaded or previously lazily loaded)
        if (this.loadedDocuments.TryGetValue(key, out ParsedJsonDocument<JsonElement>? loaded))
        {
            return NavigateFragment(loaded.RootElement, fragment, out result);
        }

        // 3. Try the injected loader (a virtualized document source — in-memory, HTTP, …) for any scheme.
        if (this.externalDocumentLoader is { } loader && loader(resolvedUri) is { } bytes)
        {
            ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
            this.loadedDocuments[key] = doc;
            return NavigateFragment(doc.RootElement, fragment, out result);
        }

        // 4. Fall back to file-system loading for file:// URIs
        if (resolvedUri.IsFile)
        {
            string filePath = resolvedUri.LocalPath;
            if (this.TryLoadDocumentFromDisk(filePath, key, out JsonElement docRoot))
            {
                return NavigateFragment(docRoot, fragment, out result);
            }
        }

        // URI scheme not supported or document not found
        result = default;
        return false;
    }

    /// <summary>
    /// Navigates a JSON Pointer fragment within a document root.
    /// </summary>
    private static bool NavigateFragment(JsonElement docRoot, string? fragment, out JsonElement result)
    {
        if (fragment is not null)
        {
            return docRoot.TryResolvePointer(fragment.AsSpan(), out result);
        }

        result = docRoot;
        return true;
    }

    /// <summary>
    /// Loads a document from disk and caches it.
    /// </summary>
    private bool TryLoadDocumentFromDisk(string filePath, string cacheKey, out JsonElement documentRoot)
    {
        string normalizedPath = Path.GetFullPath(filePath);
        if (!File.Exists(normalizedPath))
        {
            documentRoot = default;
            return false;
        }

        byte[] bytes = File.ReadAllBytes(normalizedPath);
        ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        this.loadedDocuments[cacheKey] = doc;
        documentRoot = doc.RootElement;
        return true;
    }

    /// <summary>
    /// Splits a URI-reference into its URI and fragment components.
    /// </summary>
    /// <param name="refValue">The full <c>$ref</c> value.</param>
    /// <param name="uriPart">The URI part (before the <c>#</c>).</param>
    /// <param name="fragment">
    /// The fragment part (after the <c>#</c>), or <see langword="null"/> if there is no fragment.
    /// </param>
    private static void SplitReference(string refValue, out string uriPart, out string? fragment)
    {
        int hashIndex = refValue.IndexOf('#');
        if (hashIndex < 0)
        {
            uriPart = refValue;
            fragment = null;
        }
        else
        {
            uriPart = refValue[..hashIndex];
            fragment = refValue[(hashIndex + 1)..];
        }
    }

    private sealed class BaseScope(ExternalReferenceResolver resolver) : IDisposable
    {
        public void Dispose()
        {
            if (resolver.baseStack.Count > 0)
            {
                resolver.baseStack.Pop();
            }
        }
    }
}