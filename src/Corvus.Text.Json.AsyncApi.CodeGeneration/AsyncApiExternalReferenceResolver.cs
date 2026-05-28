// <copyright file="AsyncApiExternalReferenceResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.AsyncApi.CodeGeneration;

/// <summary>
/// Resolves AsyncAPI <c>$ref</c> URI-references, supporting local (fragment-only),
/// relative, and absolute URI references.
/// </summary>
/// <remarks>
/// <para>
/// Documents can be supplied in two ways:
/// </para>
/// <list type="number">
/// <item><description>
/// <b>Pre-registered</b> via <see cref="AddDocument(Uri, JsonElement)"/> — the caller
/// provides the document root together with its canonical URI.
/// </description></item>
/// <item><description>
/// <b>Lazily loaded</b> — if no pre-registered document matches, the resolver falls
/// back to loading from the local file system.
/// </description></item>
/// </list>
/// </remarks>
public sealed class AsyncApiExternalReferenceResolver : IAsyncApiReferenceResolver, IDisposable
{
    private readonly JsonElement entryDocumentRoot;
    private readonly Uri baseUri;

    private readonly Dictionary<string, JsonElement> registeredDocuments = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ParsedJsonDocument<JsonElement>> loadedDocuments = new(StringComparer.Ordinal);
    private readonly Stack<(string Key, JsonElement Root)> baseStack = new();

    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncApiExternalReferenceResolver"/> class.
    /// </summary>
    /// <param name="entryDocumentRoot">The root element of the entry (main) AsyncAPI document.</param>
    /// <param name="entryDocumentPath">
    /// The file-system path of the entry document. Used as the base URI for resolving
    /// relative references. Must be an absolute path.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="entryDocumentPath"/> is not an absolute path.</exception>
    public AsyncApiExternalReferenceResolver(JsonElement entryDocumentRoot, string entryDocumentPath)
    {
        if (!Path.IsPathFullyQualified(entryDocumentPath))
        {
            throw new ArgumentException(
                SR.Format(SR.EntryDocumentPathMustBeFullyQualified, entryDocumentPath),
                nameof(entryDocumentPath));
        }

        this.entryDocumentRoot = entryDocumentRoot;
        this.baseUri = new Uri(entryDocumentPath);
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
    /// <param name="canonicalUri">
    /// The canonical URI for this document. When a <c>$ref</c> resolves to this URI
    /// (after base URI resolution), the registered document is used.
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
    /// <param name="relativePath">
    /// A relative path from the entry document (e.g. <c>"./schemas/Event.json"</c>).
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
    /// The bytes are parsed into a <see cref="ParsedJsonDocument{T}"/> which the resolver
    /// takes ownership of (disposed on <see cref="Dispose"/>).
    /// </remarks>
    /// <param name="canonicalUri">The canonical URI for this document.</param>
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
    /// A relative path from the entry document (e.g. <c>"./schemas/Event.json"</c>).
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
    public bool TryResolve(ReadOnlySpan<byte> refValue, out JsonElement result)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);

        if (refValue.IsEmpty)
        {
            result = default;
            return false;
        }

        // Fragment-only reference — resolve within CURRENT base document (RFC 3986 §5)
        if (refValue[0] == (byte)'#')
        {
            ReadOnlySpan<byte> pointer = refValue[1..];
            if (Utf8JsonPointer.TryCreateJsonPointer(pointer, out Utf8JsonPointer ptr) &&
                ptr.TryResolve<JsonElement, JsonElement>(this.CurrentBaseDocument, out JsonElement resolved))
            {
                result = resolved;
                return true;
            }

            result = default;
            return false;
        }

        // External reference — transcode to string for URI resolution
        string refStr = System.Text.Encoding.UTF8.GetString(refValue);
        return this.TryResolveExternal(refStr, out result);
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
        string docPart = hashIndex >= 0 ? refValue[..hashIndex] : refValue;

        Uri resolved = new(this.CurrentBaseUri, docPart);
        string key = resolved.AbsoluteUri;

        // Look up the document
        if (this.registeredDocuments.TryGetValue(key, out JsonElement registeredRoot))
        {
            this.baseStack.Push((key, registeredRoot));
            return new BaseScope(this);
        }

        if (this.loadedDocuments.TryGetValue(key, out ParsedJsonDocument<JsonElement>? loaded))
        {
            this.baseStack.Push((key, loaded.RootElement));
            return new BaseScope(this);
        }

        // Try file-system loading so push works even if TryResolve hasn't been called yet
        if (resolved.IsFile)
        {
            string filePath = resolved.LocalPath;
            if (this.TryLoadDocumentFromDisk(filePath, key, out JsonElement docRoot))
            {
                this.baseStack.Push((key, docRoot));
                return new BaseScope(this);
            }
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

    /// <inheritdoc/>
    public string ResolveToAbsolute(ReadOnlySpan<byte> refValue)
    {
        // Transcode once here — URI resolution requires strings
        return this.ResolveToAbsolute(System.Text.Encoding.UTF8.GetString(refValue));
    }

    /// <inheritdoc/>
    public int ResolveToAbsolute(ReadOnlySpan<byte> refValue, Span<byte> destination)
    {
        // URI resolution requires strings internally, so transcode, resolve, then
        // write the result back as UTF-8 into the destination (single allocation for the
        // intermediate string; the caller avoids a second allocation for concatenation).
        string resolved = this.ResolveToAbsolute(System.Text.Encoding.UTF8.GetString(refValue));
        return System.Text.Encoding.UTF8.GetBytes(resolved.AsSpan(), destination);
    }

    /// <summary>
    /// Releases all lazily loaded external documents.
    /// </summary>
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

    private bool TryResolveExternal(string refValue, out JsonElement result)
    {
        SplitReference(refValue, out string uriPart, out string? fragment);

        Uri resolvedUri = new(this.CurrentBaseUri, uriPart);
        string key = resolvedUri.AbsoluteUri;

        // 1. Check pre-registered documents
        if (this.registeredDocuments.TryGetValue(key, out JsonElement registeredRoot))
        {
            return NavigateFragment(registeredRoot, fragment, out result);
        }

        // 2. Check owned documents (byte-loaded or previously lazily loaded)
        if (this.loadedDocuments.TryGetValue(key, out ParsedJsonDocument<JsonElement>? loaded))
        {
            return NavigateFragment(loaded.RootElement, fragment, out result);
        }

        // 3. Fall back to file-system loading for file:// URIs
        if (resolvedUri.IsFile)
        {
            string filePath = resolvedUri.LocalPath;
            if (this.TryLoadDocumentFromDisk(filePath, key, out JsonElement docRoot))
            {
                return NavigateFragment(docRoot, fragment, out result);
            }
        }

        result = default;
        return false;
    }

    private static bool NavigateFragment(JsonElement docRoot, string? fragment, out JsonElement result)
    {
        if (fragment is not null)
        {
            return docRoot.TryResolvePointer(fragment.AsSpan(), out result);
        }

        result = docRoot;
        return true;
    }

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

    private sealed class BaseScope(AsyncApiExternalReferenceResolver resolver) : IDisposable
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