// <copyright file="JsonSchema.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using Corvus.Text.Json.RuntimeEvaluator;

namespace Corvus.Text.Json.Validator;

/// <summary>
/// A JSON schema for validation.
/// </summary>
/// <remarks>
/// Schemas are compiled by the <see cref="JsonSchemaEvaluator"/> into an in-memory evaluator: there is no code
/// generation and no runtime compilation, so the first validation against a schema costs milliseconds rather than
/// seconds. Compiled schemas are cached by canonical URI and <see cref="Options.AlwaysAssertFormat"/>.
/// </remarks>
public readonly struct JsonSchema
{
    private static readonly ConcurrentDictionary<string, JsonSchemaEvaluator> CachedSchema = new(StringComparer.Ordinal);
    private static readonly Lazy<HttpClient> SharedHttpClient = new(() => new HttpClient());

    private readonly JsonSchemaEvaluator evaluator;

    private JsonSchema(JsonSchemaEvaluator evaluator)
    {
        this.evaluator = evaluator;
    }

    /// <summary>
    /// Create an instance of a JSON schema from a JSON document string.
    /// </summary>
    /// <param name="text">The text for the document.</param>
    /// <param name="canonicalUri">The canonical URI for the document. If
    /// <see langword="null"/> then an attempt will be made to find the canonical URI in the schema.</param>
    /// <param name="options">Compilation options.</param>
    /// <param name="refreshCache">If <see langword="true"/>, any cached entry for this schema will be replaced.</param>
    /// <returns>The JSON schema instance.</returns>
    /// <exception cref="InvalidOperationException">No canonical URI could be found for the schema document.</exception>
    public static JsonSchema FromText(string text, string? canonicalUri = null, Options? options = null, bool refreshCache = false)
    {
        return FromUtf8(Encoding.UTF8.GetBytes(text), canonicalUri, options, refreshCache);
    }

    /// <summary>
    /// Create an instance of a JSON schema from a stream containing a JSON document.
    /// </summary>
    /// <param name="stream">The stream containing the document.</param>
    /// <param name="canonicalUri">The canonical URI for the document. If
    /// <see langword="null"/> then an attempt will be made to find the canonical URI in the schema.</param>
    /// <param name="options">Compilation options.</param>
    /// <param name="refreshCache">If <see langword="true"/>, any cached entry for this schema will be replaced.</param>
    /// <returns>The JSON schema instance.</returns>
    /// <exception cref="InvalidOperationException">No canonical URI could be found for the schema document.</exception>
    public static JsonSchema FromStream(Stream stream, string? canonicalUri = null, Options? options = null, bool refreshCache = false)
    {
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        return FromUtf8(buffer.ToArray(), canonicalUri, options, refreshCache);
    }

    /// <summary>
    /// Create an instance of a JSON schema from a file.
    /// </summary>
    /// <param name="fileName">The path to the schema file.</param>
    /// <param name="options">Compilation options.</param>
    /// <param name="refreshCache">If <see langword="true"/>, any cached entry for this schema will be replaced.</param>
    /// <returns>The JSON schema instance.</returns>
    /// <remarks>
    /// The file's location is the base URI for relative <c>$ref</c> references unless the schema declares an
    /// absolute <c>$id</c>.
    /// </remarks>
    public static JsonSchema FromFile(string fileName, Options? options = null, bool refreshCache = false)
    {
        options ??= Options.Default;

        string fullPath = NormalizeFilePath(fileName);
        string cacheKey = BuildCacheKey(fullPath.Replace('\\', '/'), options.AlwaysAssertFormat);

        if (TryGetCached(cacheKey, refreshCache, out JsonSchema cached))
        {
            return cached;
        }

        byte[] utf8 = File.ReadAllBytes(fullPath);
        string baseUri = new Uri(fullPath).AbsoluteUri;
        return Compile(cacheKey, baseUri, utf8, options);
    }

    /// <summary>
    /// Create an instance of a JSON schema from a URI.
    /// </summary>
    /// <param name="jsonSchemaUri">The URI of the schema. A fragment selects a subschema within the document.</param>
    /// <param name="options">Compilation options.</param>
    /// <param name="refreshCache">If <see langword="true"/>, any cached entry for this schema will be replaced.</param>
    /// <returns>The JSON schema instance.</returns>
    /// <remarks>
    /// The document is retrieved through <see cref="Options.AdditionalDocumentResolver"/>,
    /// <see cref="Options.AdditionalSchemaFiles"/>, the embedded standard metaschemas, and then (when
    /// <see cref="Options.AllowFileSystemAndHttpResolution"/> is set) the file system or HTTP.
    /// </remarks>
    public static JsonSchema FromUri(string jsonSchemaUri, Options? options = null, bool refreshCache = false)
    {
        options ??= Options.Default;

        string cacheKey = BuildCacheKey(jsonSchemaUri, options.AlwaysAssertFormat);

        if (TryGetCached(cacheKey, refreshCache, out JsonSchema cached))
        {
            return cached;
        }

        JsonSchemaEvaluatorOptions evaluatorOptions = BuildEvaluatorOptions(options, new PrepopulatedDocuments(options));
        JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.CompileFromUri(jsonSchemaUri, evaluatorOptions);
        return Cache(cacheKey, evaluator);
    }

    /// <summary>
    /// Create an instance of a JSON schema from a URI, resolving via all configured resolvers.
    /// </summary>
    /// <param name="jsonSchemaUri">The URI of the schema.</param>
    /// <param name="options">Compilation options.</param>
    /// <param name="refreshCache">If <see langword="true"/>, any cached entry for this schema will be replaced.</param>
    /// <returns>The JSON schema instance.</returns>
    public static JsonSchema From(string jsonSchemaUri, Options? options = null, bool refreshCache = false)
    {
        return FromUri(jsonSchemaUri, options, refreshCache);
    }

    /// <summary>
    /// Validate a JSON string against this schema.
    /// </summary>
    /// <param name="json">The JSON string to validate.</param>
    /// <param name="resultsCollector">An optional results collector for detailed validation results.</param>
    /// <returns><see langword="true"/> if the document is valid; otherwise <see langword="false"/>.</returns>
    public bool Validate(string json, IJsonSchemaResultsCollector? resultsCollector = null)
    {
        return this.evaluator.Evaluate(json, resultsCollector);
    }

    /// <summary>
    /// Validate UTF-8 JSON bytes against this schema.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 encoded JSON bytes to validate.</param>
    /// <param name="resultsCollector">An optional results collector for detailed validation results.</param>
    /// <returns><see langword="true"/> if the document is valid; otherwise <see langword="false"/>.</returns>
    public bool Validate(ReadOnlyMemory<byte> utf8Json, IJsonSchemaResultsCollector? resultsCollector = null)
    {
        return this.evaluator.Evaluate(utf8Json, resultsCollector);
    }

    /// <summary>
    /// Validate JSON characters against this schema.
    /// </summary>
    /// <param name="json">The JSON characters to validate.</param>
    /// <param name="resultsCollector">An optional results collector for detailed validation results.</param>
    /// <returns><see langword="true"/> if the document is valid; otherwise <see langword="false"/>.</returns>
    public bool Validate(ReadOnlyMemory<char> json, IJsonSchemaResultsCollector? resultsCollector = null)
    {
        using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(json);
        return this.evaluator.Evaluate(document.RootElement, resultsCollector);
    }

    /// <summary>
    /// Validate a UTF-8 JSON stream against this schema.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 encoded JSON stream to validate.</param>
    /// <param name="resultsCollector">An optional results collector for detailed validation results.</param>
    /// <returns><see langword="true"/> if the document is valid; otherwise <see langword="false"/>.</returns>
    public bool Validate(Stream utf8Json, IJsonSchemaResultsCollector? resultsCollector = null)
    {
        using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(utf8Json);
        return this.evaluator.Evaluate(document.RootElement, resultsCollector);
    }

    /// <summary>
    /// Validate a UTF-8 JSON byte sequence against this schema.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 encoded JSON byte sequence to validate.</param>
    /// <param name="resultsCollector">An optional results collector for detailed validation results.</param>
    /// <returns><see langword="true"/> if the document is valid; otherwise <see langword="false"/>.</returns>
    public bool Validate(ReadOnlySequence<byte> utf8Json, IJsonSchemaResultsCollector? resultsCollector = null)
    {
        using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(utf8Json);
        return this.evaluator.Evaluate(document.RootElement, resultsCollector);
    }

    /// <summary>
    /// Validate a pre-parsed <see cref="JsonElement"/> against this schema.
    /// </summary>
    /// <param name="element">The JSON element to validate.</param>
    /// <param name="resultsCollector">An optional results collector for detailed validation results.</param>
    /// <returns><see langword="true"/> if the document is valid; otherwise <see langword="false"/>.</returns>
    public bool Validate(in JsonElement element, IJsonSchemaResultsCollector? resultsCollector = null)
    {
        return this.evaluator.Evaluate(in element, resultsCollector);
    }

    private static JsonSchema FromUtf8(byte[] utf8, string? canonicalUri, Options? options, bool refreshCache)
    {
        options ??= Options.Default;

        if (canonicalUri is null && !TryGetCanonicalUri(utf8, out canonicalUri))
        {
            throw new InvalidOperationException(SR.DocumentDoesNotHaveCanonicalUri);
        }

        string cacheKey = BuildCacheKey(canonicalUri!, options.AlwaysAssertFormat);

        if (TryGetCached(cacheKey, refreshCache, out JsonSchema cached))
        {
            return cached;
        }

        return Compile(cacheKey, canonicalUri!, utf8, options);
    }

    private static JsonSchema Compile(string cacheKey, string baseUri, byte[] utf8, Options options)
    {
        PrepopulatedDocuments documents = new(options);
        documents.Add(baseUri, utf8);
        JsonSchemaEvaluatorOptions evaluatorOptions = BuildEvaluatorOptions(options, documents);
        evaluatorOptions.BaseUri = baseUri;
        JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(utf8, evaluatorOptions);
        return Cache(cacheKey, evaluator);
    }

    private static JsonSchema Cache(string cacheKey, JsonSchemaEvaluator evaluator)
    {
        JsonSchemaEvaluator cached = CachedSchema.GetOrAdd(cacheKey, evaluator);
        if (!ReferenceEquals(cached, evaluator))
        {
            evaluator.Dispose();
        }

        return new(cached);
    }

    private static bool TryGetCached(string cacheKey, bool refreshCache, out JsonSchema schema)
    {
        if (refreshCache)
        {
            CachedSchema.TryRemove(cacheKey, out _);
        }
        else if (CachedSchema.TryGetValue(cacheKey, out JsonSchemaEvaluator? cached))
        {
            schema = new(cached);
            return true;
        }

        schema = default;
        return false;
    }

    private static string BuildCacheKey(string uri, bool alwaysAssertFormat)
    {
        return $"{uri}__{alwaysAssertFormat}";
    }

    private static JsonSchemaEvaluatorOptions BuildEvaluatorOptions(Options options, PrepopulatedDocuments documents)
    {
        return new JsonSchemaEvaluatorOptions
        {
            DefaultDialect = options.DefaultDialect,
            AssertFormat = options.AlwaysAssertFormat ? true : null,
            DocumentResolver = documents.Resolve,
            FallbackDocumentResolver = options.AllowFileSystemAndHttpResolution ? ResolveFromFileSystemOrHttp : null,
        };
    }

    private static bool TryGetCanonicalUri(byte[] utf8, out string? canonicalUri)
    {
        using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(utf8);
        JsonElement root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("$id"u8, out JsonElement value) &&
            value.ValueKind == JsonValueKind.String &&
            value.GetString() is string id)
        {
            canonicalUri = id;
            return true;
        }

        canonicalUri = null;
        return false;
    }

    private static string NormalizeFilePath(string fileName)
    {
        if (Uri.TryCreate(fileName, UriKind.Absolute, out Uri? uri) && uri.IsFile)
        {
            fileName = uri.LocalPath;
        }

        return Path.GetFullPath(fileName);
    }

    private static string NormalizeUri(string uri)
    {
        if (Uri.TryCreate(uri, UriKind.Absolute, out Uri? absolute))
        {
            return absolute.GetComponents(UriComponents.AbsoluteUri & ~UriComponents.Fragment, UriFormat.UriEscaped);
        }

        int hash = uri.IndexOf('#');
        return hash < 0 ? uri : uri.Substring(0, hash);
    }

    private static bool ResolveFromFileSystemOrHttp(string uri, out ReadOnlyMemory<byte> utf8Json)
    {
        try
        {
            if (Uri.TryCreate(uri, UriKind.Absolute, out Uri? absolute))
            {
                if (absolute.IsFile)
                {
                    return TryReadFile(absolute.LocalPath, out utf8Json);
                }

                if (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps)
                {
                    utf8Json = SharedHttpClient.Value.GetByteArrayAsync(absolute).GetAwaiter().GetResult();
                    return true;
                }
            }

            return TryReadFile(Path.Combine(Environment.CurrentDirectory, uri), out utf8Json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException or NotSupportedException)
        {
            utf8Json = default;
            return false;
        }
    }

    private static bool TryReadFile(string path, out ReadOnlyMemory<byte> utf8Json)
    {
        if (File.Exists(path))
        {
            utf8Json = File.ReadAllBytes(path);
            return true;
        }

        utf8Json = default;
        return false;
    }

    /// <summary>
    /// Options for validation.
    /// </summary>
    public sealed class Options
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Options"/> class.
        /// </summary>
        /// <param name="additionalSchemaFiles">Additional schema files to preload into the document resolver.</param>
        /// <param name="allowFileSystemAndHttpResolution">If <see langword="true"/> then referenced documents may be retrieved from the file system and over HTTP.</param>
        /// <param name="defaultDialect">The dialect applied to schemas that do not declare <c>$schema</c> (defaults to <see cref="JsonSchemaDialect.Draft202012"/>).</param>
        /// <param name="alwaysAssertFormat">If <see langword="true"/>, <c>format</c> will always be asserted, even for dialects that usually annotate.</param>
        /// <param name="additionalDocumentResolver">An additional document resolver for in-memory schema resolution.</param>
        public Options(
            IReadOnlyList<AdditionalSchemaFile>? additionalSchemaFiles = null,
            bool allowFileSystemAndHttpResolution = true,
            JsonSchemaDialect defaultDialect = JsonSchemaDialect.Draft202012,
            bool alwaysAssertFormat = true,
            JsonSchemaDocumentResolver? additionalDocumentResolver = null)
        {
            this.AdditionalSchemaFiles = additionalSchemaFiles;
            this.AllowFileSystemAndHttpResolution = allowFileSystemAndHttpResolution;
            this.DefaultDialect = defaultDialect;
            this.AlwaysAssertFormat = alwaysAssertFormat;
            this.AdditionalDocumentResolver = additionalDocumentResolver;
        }

        /// <summary>
        /// Gets the default options.
        /// </summary>
        public static Options Default { get; } = new();

        /// <summary>
        /// Gets the additional schema files to preload.
        /// </summary>
        public IReadOnlyList<AdditionalSchemaFile>? AdditionalSchemaFiles { get; }

        /// <summary>
        /// Gets a value indicating whether referenced documents may be retrieved from the file system and over HTTP.
        /// </summary>
        public bool AllowFileSystemAndHttpResolution { get; }

        /// <summary>
        /// Gets the dialect applied to schemas that do not declare <c>$schema</c>.
        /// </summary>
        public JsonSchemaDialect DefaultDialect { get; }

        /// <summary>
        /// Gets a value indicating whether <c>format</c> will always be asserted, even for dialects that usually annotate.
        /// </summary>
        public bool AlwaysAssertFormat { get; }

        /// <summary>
        /// Gets the additional document resolver for in-memory schema resolution.
        /// </summary>
        public JsonSchemaDocumentResolver? AdditionalDocumentResolver { get; }
    }

    /// <summary>
    /// The in-memory documents consulted before any other resolution: the root document, the
    /// <see cref="Options.AdditionalSchemaFiles"/> (registered by canonical URI, <c>$id</c> and file path), and the
    /// <see cref="Options.AdditionalDocumentResolver"/>.
    /// </summary>
    private sealed class PrepopulatedDocuments
    {
        private readonly Dictionary<string, byte[]> documents = new(StringComparer.Ordinal);
        private readonly JsonSchemaDocumentResolver? additionalResolver;

        public PrepopulatedDocuments(Options options)
        {
            this.additionalResolver = options.AdditionalDocumentResolver;

            if (options.AdditionalSchemaFiles is not { Count: > 0 } files)
            {
                return;
            }

            foreach (AdditionalSchemaFile file in files)
            {
                byte[] utf8 = File.ReadAllBytes(file.FilePath);
                this.Add(file.CanonicalUri, utf8);

                if (TryGetCanonicalUri(utf8, out string? id))
                {
                    this.Add(id!, utf8);
                }

                string fullPath = Path.GetFullPath(file.FilePath);
                this.Add(fullPath, utf8);
                this.Add(new Uri(fullPath).AbsoluteUri, utf8);
            }
        }

        public void Add(string uri, byte[] utf8)
        {
            this.documents[uri] = utf8;
            this.documents[NormalizeUri(uri)] = utf8;
        }

        public bool Resolve(string uri, out ReadOnlyMemory<byte> utf8Json)
        {
            if (this.documents.TryGetValue(uri, out byte[]? utf8) || this.documents.TryGetValue(NormalizeUri(uri), out utf8))
            {
                utf8Json = utf8;
                return true;
            }

            if (this.additionalResolver is JsonSchemaDocumentResolver resolver)
            {
                return resolver(uri, out utf8Json);
            }

            utf8Json = default;
            return false;
        }
    }
}