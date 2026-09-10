// <copyright file="JsonSchemaEvaluator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.RuntimeEvaluator.Compilation;
using Corvus.Text.Json.RuntimeEvaluator.Evaluation;

namespace Corvus.Text.Json.RuntimeEvaluator;

/// <summary>
/// A compiled JSON Schema that can evaluate instances without generating code.
/// </summary>
/// <remarks>
/// Compile once with <see cref="Compile(ReadOnlyMemory{byte}, JsonSchemaEvaluatorOptions?)"/>, then call
/// <see cref="Evaluate{T}(in T, IJsonSchemaResultsCollector?)"/> from any number of threads. Evaluation
/// with a <see langword="null"/> collector is a zero-allocation flag check that fails fast; supplying a
/// <see cref="JsonSchemaResultsCollector"/> produces basic, detailed or verbose results (and annotations
/// via <see cref="JsonSchemaAnnotationProducer"/>).
/// </remarks>
public sealed class JsonSchemaEvaluator : IDisposable
{
    private readonly CompiledSchema program;
    private readonly int rootNode;

    private JsonSchemaEvaluator(CompiledSchema program, int rootNode)
    {
        this.program = program;
        this.rootNode = rootNode;
        program.AddReference();
    }

    /// <summary>
    /// Gets the number of compiled subschemas.
    /// </summary>
    public int NodeCount => this.program.Nodes.Length;

    /// <summary>
    /// Gets the number of schema resources loaded.
    /// </summary>
    public int ResourceCount => this.program.ResourceCount;

    /// <summary>
    /// Gets a value indicating whether the schema uses <c>$dynamicRef</c>/<c>$recursiveRef</c> that require dynamic scope tracking.
    /// </summary>
    public bool UsesDynamicScope => this.program.UsesDynamicScope;

    /// <summary>Gets the compiled program (for diagnostics and tests).</summary>
    internal CompiledSchema Program => this.program;

    /// <summary>Gets the root node index (for diagnostics and tests).</summary>
    internal int RootNode => this.rootNode;

    /// <summary>
    /// Compiles a schema from UTF-8 JSON.
    /// </summary>
    /// <param name="utf8Schema">The schema JSON.</param>
    /// <param name="options">The options, or <see langword="null"/> for defaults.</param>
    /// <returns>The compiled evaluator.</returns>
    public static JsonSchemaEvaluator Compile(ReadOnlyMemory<byte> utf8Schema, JsonSchemaEvaluatorOptions? options = null)
    {
        CompiledSchema program = SchemaCompiler.Compile(utf8Schema, options ?? JsonSchemaEvaluatorOptions.Default);
        return new JsonSchemaEvaluator(program, program.RootNode);
    }

    /// <summary>
    /// Compiles a schema from JSON text.
    /// </summary>
    /// <param name="schema">The schema JSON.</param>
    /// <param name="options">The options, or <see langword="null"/> for defaults.</param>
    /// <returns>The compiled evaluator.</returns>
    public static JsonSchemaEvaluator Compile(string schema, JsonSchemaEvaluatorOptions? options = null)
    {
        return Compile(Encoding.UTF8.GetBytes(schema), options);
    }

    /// <summary>
    /// Compiles a subschema of a document, identified by a URI reference such as <c>#/$defs/PersonArray</c>.
    /// </summary>
    /// <param name="utf8Schema">The schema document JSON.</param>
    /// <param name="entryPoint">The entry point reference, resolved against the document.</param>
    /// <param name="options">The options, or <see langword="null"/> for defaults; <see cref="JsonSchemaEvaluatorOptions.EntryPoint"/> is overridden.</param>
    /// <returns>The compiled evaluator rooted at the subschema.</returns>
    public static JsonSchemaEvaluator Compile(ReadOnlyMemory<byte> utf8Schema, string entryPoint, JsonSchemaEvaluatorOptions? options = null)
    {
        JsonSchemaEvaluatorOptions rooted = Clone(options ?? JsonSchemaEvaluatorOptions.Default);
        rooted.EntryPoint = entryPoint;
        return Compile(utf8Schema, rooted);
    }

    /// <summary>
    /// Compiles the schema document at a URI, retrieved through <see cref="JsonSchemaEvaluatorOptions.DocumentResolver"/>,
    /// the embedded standard metaschemas, or <see cref="JsonSchemaEvaluatorOptions.FallbackDocumentResolver"/>.
    /// </summary>
    /// <param name="uri">The schema URI. A fragment selects the entry point within the document unless
    /// <see cref="JsonSchemaEvaluatorOptions.EntryPoint"/> is set.</param>
    /// <param name="options">The options, or <see langword="null"/> for defaults.</param>
    /// <returns>The compiled evaluator.</returns>
    /// <exception cref="JsonSchemaCompilationException">The document could not be resolved or compiled.</exception>
    public static JsonSchemaEvaluator CompileFromUri(string uri, JsonSchemaEvaluatorOptions? options = null)
    {
        JsonSchemaEvaluatorOptions effective = options ?? JsonSchemaEvaluatorOptions.Default;
        int hash = uri.IndexOf('#');
        if (hash >= 0)
        {
            string fragment = uri.Substring(hash);
            uri = uri.Substring(0, hash);
            if (effective.EntryPoint is null && fragment.Length > 1)
            {
                effective = Clone(effective);
                effective.EntryPoint = fragment;
            }
        }

        CompiledSchema program = SchemaCompiler.CompileFromUri(uri, effective);
        return new JsonSchemaEvaluator(program, program.RootNode);
    }

    /// <summary>
    /// Serialises the compiled program to a binary image that <see cref="FromProgramImage"/> loads without the schema
    /// text, the document resolver or the compiler. Every entry point created so far (the root and any
    /// <see cref="ForEntryPoint"/>) is recorded in the image.
    /// </summary>
    /// <returns>The image bytes.</returns>
    public byte[] ToProgramImage()
    {
        return ProgramImage.Write(this.program);
    }

    /// <summary>
    /// Loads a compiled program from an image produced by <see cref="ToProgramImage"/>. The evaluator returned is
    /// rooted at the image's root entry point; <see cref="ForEntryPoint"/> selects any other recorded entry point.
    /// </summary>
    /// <param name="image">The image bytes.</param>
    /// <param name="options">The evaluation options; only the settings that apply at evaluation time
    /// (<see cref="JsonSchemaEvaluatorOptions.MaxDepth"/>, regular-expression construction) are used, because the
    /// schema was compiled when the image was created.</param>
    /// <returns>The evaluator.</returns>
    public static JsonSchemaEvaluator FromProgramImage(ReadOnlyMemory<byte> image, JsonSchemaEvaluatorOptions? options = null)
    {
        CompiledSchema program = ProgramImage.Read(image, options ?? JsonSchemaEvaluatorOptions.Default);
        return new JsonSchemaEvaluator(program, program.RootNode);
    }

    /// <summary>
    /// Gets an evaluator for another entry point of the same schema document set, sharing the loaded documents
    /// and every already-compiled subschema. Only subschemas newly reachable from the entry point are compiled.
    /// </summary>
    /// <param name="entryPoint">A URI reference resolved against the root document (pointer fragment, anchor, or resource URI).</param>
    /// <returns>An evaluator rooted at the entry point. Dispose each evaluator; the shared documents are released with the last one.</returns>
    /// <remarks>
    /// Create the entry points you need before evaluating concurrently: adding an entry point recompiles graph-wide
    /// metadata (idempotently) and may load further documents.
    /// </remarks>
    public JsonSchemaEvaluator ForEntryPoint(string entryPoint)
    {
        int node = this.program.AddEntryPoint(entryPoint);
        return new JsonSchemaEvaluator(this.program, node);
    }

    /// <summary>
    /// Evaluates an instance.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="instance">The instance.</param>
    /// <param name="resultsCollector">The optional results collector.</param>
    /// <returns><see langword="true"/> if the instance is valid.</returns>
    public bool Evaluate<T>(in T instance, IJsonSchemaResultsCollector? resultsCollector = null)
        where T : struct, IJsonElement<T>
    {
        return Evaluator.Evaluate(this.program, this.rootNode, instance.ParentDocument, instance.ParentDocumentIndex, resultsCollector);
    }

    /// <summary>
    /// Evaluates an instance given its document and index.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The element index within the document.</param>
    /// <param name="resultsCollector">The optional results collector.</param>
    /// <returns><see langword="true"/> if the instance is valid.</returns>
    public bool Evaluate(IJsonDocument document, int index, IJsonSchemaResultsCollector? resultsCollector = null)
    {
        return Evaluator.Evaluate(this.program, this.rootNode, document, index, resultsCollector);
    }

    /// <summary>
    /// Parses and evaluates UTF-8 JSON.
    /// </summary>
    /// <param name="utf8Json">The instance JSON.</param>
    /// <param name="resultsCollector">The optional results collector.</param>
    /// <returns><see langword="true"/> if the instance is valid.</returns>
    public bool Evaluate(ReadOnlyMemory<byte> utf8Json, IJsonSchemaResultsCollector? resultsCollector = null)
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(utf8Json);
        return this.Evaluate(doc.RootElement, resultsCollector);
    }

    /// <summary>
    /// Parses and evaluates JSON text.
    /// </summary>
    /// <param name="json">The instance JSON.</param>
    /// <param name="resultsCollector">The optional results collector.</param>
    /// <returns><see langword="true"/> if the instance is valid.</returns>
    public bool Evaluate(string json, IJsonSchemaResultsCollector? resultsCollector = null)
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        return this.Evaluate(doc.RootElement, resultsCollector);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.program.Dispose();
    }

    private static JsonSchemaEvaluatorOptions Clone(JsonSchemaEvaluatorOptions source)
    {
        return new JsonSchemaEvaluatorOptions
        {
            DefaultDialect = source.DefaultDialect,
            AssertFormat = source.AssertFormat,
            AssertFormatInLegacyDrafts = source.AssertFormatInLegacyDrafts,
            AssertContent = source.AssertContent,
            FormatModes = source.FormatModes,
            CompileRegularExpressions = source.CompileRegularExpressions,
            RegexMatchTimeout = source.RegexMatchTimeout,
            DocumentResolver = source.DocumentResolver,
            FallbackDocumentResolver = source.FallbackDocumentResolver,
            BaseUri = source.BaseUri,
            MaxDepth = source.MaxDepth,
            EntryPoint = source.EntryPoint,
        };
    }
}
