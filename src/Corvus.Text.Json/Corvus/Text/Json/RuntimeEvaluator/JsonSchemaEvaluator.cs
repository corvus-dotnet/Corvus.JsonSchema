// <copyright file="JsonSchemaEvaluator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
#if NET && !STJ
using System.Threading;
using System.Threading.Tasks;
#endif
#if !STJ
using Corvus.Text.Json.Internal;
#endif
#if NET && !STJ
using Corvus.Text.Json.RuntimeEvaluator.CodeGeneration;
#endif
using Corvus.Text.Json.RuntimeEvaluator.Compilation;
#if !STJ
using Corvus.Text.Json.RuntimeEvaluator.Evaluation;
#endif

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
[SkipLocalsInit]
public sealed class JsonSchemaEvaluator : IDisposable
{
    private readonly CompiledSchema program;
    private readonly int rootNode;

    // The entry data of flag-mode evaluation over a parsed document, published as one object so a reader sees a
    // consistent set; rebuilt when the program's node array changes (an entry point added). Null Entry: the program
    // uses a dynamic scope, so every evaluation takes the general entry.
    private FlagModeEntry? flagModeEntry;
#if NET && !STJ

    // The schema's generated code once it is compiled and has an entry method, for Evaluate to read directly.
    private CompiledEntry? compiledEntry;
#endif

    private JsonSchemaEvaluator(CompiledSchema program, int rootNode)
    {
        this.program = program;
        this.rootNode = rootNode;
        program.AddReference();
#if NET && !STJ
        JsonSchemaCodeGeneration codeGeneration = CodeGenerationOverride ?? program.Options.CodeGeneration;
        this.compilesAfterWarmUp = codeGeneration == JsonSchemaCodeGeneration.AfterWarmUp && SchemaLowering.IsSupported;
        this.compilesEagerly = codeGeneration == JsonSchemaCodeGeneration.Eager && SchemaLowering.IsSupported;
#endif
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
    /// Compiles the given entry points into the program in one step, so that <see cref="ToProgramImage"/> records
    /// them and later <see cref="ForEntryPoint"/> calls find them compiled. Adding many entry points this way runs
    /// the compiler's analyses once rather than once per entry point.
    /// </summary>
    /// <param name="entryPoints">The entry point references.</param>
    public void RegisterEntryPoints(IReadOnlyList<string> entryPoints)
    {
        this.program.AddEntryPoints(entryPoints);
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

#if !STJ
    /// <summary>
    /// Loads a compiled program from an image produced by <see cref="ToProgramImage"/>. The evaluator returned is
    /// rooted at the image's root entry point; <see cref="ForEntryPoint"/> selects any other recorded entry point.
    /// </summary>
    /// <param name="image">The image bytes.</param>
    /// <param name="options">The evaluation options; only the settings that apply at evaluation time
    /// (<see cref="JsonSchemaEvaluatorOptions.MaxDepth"/>, <see cref="JsonSchemaEvaluatorOptions.CodeGeneration"/>,
    /// regular-expression construction) are used, because the
    /// schema was compiled when the image was created.</param>
    /// <returns>The evaluator.</returns>
    public static JsonSchemaEvaluator FromProgramImage(ReadOnlyMemory<byte> image, JsonSchemaEvaluatorOptions? options = null)
    {
        CompiledSchema program = ProgramImage.Read(image, options ?? JsonSchemaEvaluatorOptions.Default);
        return new JsonSchemaEvaluator(program, program.RootNode);
    }
#endif

    /// <summary>
    /// Gets the pattern table of a program image: every pattern that needs a regular expression, in the index order
    /// a <see cref="JsonSchemaRegexProvider"/> is asked for them. Patterns are in ECMA-262 syntax as written in the
    /// schema; <see cref="ToDotNetPattern"/> gives the form the evaluator itself would construct.
    /// </summary>
    /// <param name="image">The image bytes.</param>
    /// <returns>The patterns.</returns>
    public static IReadOnlyList<string> GetImagePatterns(ReadOnlyMemory<byte> image)
    {
        return ProgramImage.ReadPatterns(image);
    }

    /// <summary>
    /// Translates a schema pattern (ECMA-262 syntax) to the .NET pattern the evaluator constructs for it, so that a
    /// generated <c>[GeneratedRegex]</c> matches exactly what the evaluator would have used. The evaluator constructs
    /// its own instances with <see cref="System.Text.RegularExpressions.RegexOptions.CultureInvariant"/>.
    /// </summary>
    /// <param name="ecmaPattern">The pattern as written in the schema.</param>
    /// <returns>The .NET pattern.</returns>
    public static string ToDotNetPattern(string ecmaPattern)
    {
        return Corvus.Text.Json.CodeGeneration.EcmaRegexTranslator.TranslateOrFallback(ecmaPattern);
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

#if !STJ
    /// <summary>
    /// Evaluates an instance.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="instance">The instance.</param>
    /// <param name="resultsCollector">The optional results collector.</param>
    /// <returns><see langword="true"/> if the instance is valid.</returns>
    [CLSCompliant(false)]
    public bool Evaluate<T>(in T instance, IJsonSchemaResultsCollector? resultsCollector = null)
        where T : struct, IJsonElement<T>
    {
        IJsonDocument document = instance.ParentDocument;
#if NET && !STJ

        // A schema with generated code, first: one field and one comparison (that the program still has the node
        // array the code was compiled from) before the call. The general path below reaches the same call through
        // the flag-mode entry data, its checks and the tiering's.
        if (resultsCollector is null && this.compiledEntry is CompiledEntry compiled && ReferenceEquals(compiled.SourceNodes, this.program.Nodes)
            && document is JsonDocument compiledDocument)
        {
            unsafe
            {
                return ((delegate*<CompiledEntry, JsonDocument, IJsonDocument, int, bool>)compiled.EntryAddress)(compiled, compiledDocument, document, instance.ParentDocumentIndex);
            }
        }

#endif
        if (resultsCollector is null && document is JsonDocument parsed && this.FlagMode() is { Entry: SchemaNode entry } fast)
        {
            return this.EvaluateFlag(fast, entry, parsed, document, instance.ParentDocumentIndex);
        }

        return Evaluator.Evaluate(this.program, this.rootNode, document, instance.ParentDocumentIndex, resultsCollector);
    }
#endif

#if !STJ
    /// <summary>
    /// Evaluates an instance given its document and index.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The element index within the document.</param>
    /// <param name="resultsCollector">The optional results collector.</param>
    /// <returns><see langword="true"/> if the instance is valid.</returns>
    [CLSCompliant(false)]
    public bool Evaluate(IJsonDocument document, int index, IJsonSchemaResultsCollector? resultsCollector = null)
    {
        if (resultsCollector is null && document is JsonDocument parsed && this.FlagMode() is { Entry: SchemaNode entry } fast)
        {
            return this.EvaluateFlag(fast, entry, parsed, document, index);
        }

        return Evaluator.Evaluate(this.program, this.rootNode, document, index, resultsCollector);
    }
#endif

#if !STJ
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
#endif

#if !STJ
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
#endif

    /// <inheritdoc/>
    public void Dispose()
    {
        this.program.Dispose();
    }

#if !STJ
    /// <summary>
    /// Flag-mode evaluation of a parsed document: through the schema's generated code once it has been compiled,
    /// otherwise through the interpreter, counting evaluations towards compiling it (runtime codegen, when enabled).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool EvaluateFlag(FlagModeEntry fast, SchemaNode entry, JsonDocument parsed, IJsonDocument document, int index)
    {
#if NET && !STJ
        if (fast.Compiled is CompiledEntry compiled)
        {
            unsafe
            {
                return ((delegate*<CompiledEntry, JsonDocument, IJsonDocument, int, bool>)compiled.EntryAddress)(compiled, parsed, document, index);
            }
        }

        if (this.compilesAfterWarmUp && !fast.Tiered && Interlocked.Increment(ref this.evaluations) == WarmUpEvaluations)
        {
            this.StartCompilingFlagMode(fast);
        }
#endif
        return Evaluator.EvaluateFlagRaw(this.program, fast.Nodes, entry, fast.EntryResource, fast.MaxDepth, parsed, document, this.rootNode, index);
    }

#if NET && !STJ
    // The evaluations that collect no results an evaluator makes before it compiles its schema, with
    // JsonSchemaCodeGeneration.AfterWarmUp.
    private const int WarmUpEvaluations = 1000;

    // What the options ask for, where the runtime can compile code (see JsonSchemaCodeGeneration).
    private readonly bool compilesAfterWarmUp;
    private readonly bool compilesEagerly;

    private int evaluations;

    /// <summary>
    /// Gets or sets a value every evaluator made afterwards takes in place of its options'
    /// <see cref="JsonSchemaEvaluatorOptions.CodeGeneration"/>: for a test run that puts a whole suite of schemas
    /// through generated code. <see langword="null"/> (the default) leaves the options to decide.
    /// </summary>
    internal static JsonSchemaCodeGeneration? CodeGenerationOverride { get; set; }

    /// <summary>
    /// Starts compiling the schema's generated code off the evaluating thread (it takes milliseconds), to be published
    /// when done. On the thread pool: the evaluation that starts it pays for starting the pool when nothing has yet
    /// (1.4 to 2.1 ms in a process that has just started, 0.3 to 0.4 ms once the pool is running), and the
    /// evaluations after it are not slowed. A thread of its own costs 0.5 to 0.8 ms every time, and is a thread for
    /// every evaluator that compiles. A method of its own, never inlined: the closure it makes captures its parameter, so the compiler
    /// allocates it on entry, and in the evaluation's entry that was an allocation on every evaluation.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void StartCompilingFlagMode(FlagModeEntry fast)
    {
        Task.Run(() => this.CompileFlagMode(fast));
    }

    /// <summary>
    /// Compiles the schema's generated code now, whatever the options say: for tests and measurements that compare
    /// the two engines in one process.
    /// </summary>
    /// <returns>Whether the schema runs generated code (not where dynamic code is unsupported, nor for a schema with a dynamic scope).</returns>
    internal bool CompileGeneratedCode()
    {
        if (!SchemaLowering.IsSupported || this.FlagMode() is not { Entry: not null } fast)
        {
            return false;
        }

        if (!fast.Tiered)
        {
            this.CompileFlagMode(fast);
        }

        return true;
    }

    /// <summary>Compiles the entry's generated code and publishes it, unless the entry data has been replaced meanwhile.</summary>
    private FlagModeEntry CompileFlagMode(FlagModeEntry fast)
    {
        // Every caller has asked SchemaLowering.IsSupported already. Asked again here, of the runtime directly, so
        // that the code below is seen to run only where code can be compiled.
        if (!RuntimeFeature.IsDynamicCodeSupported)
        {
            return fast;
        }

        // The entry data over the generated node array: its entry's method when the entry is specialised, and
        // otherwise the interpreter from the entry, which reaches the generated methods beneath it.
        NodeValidator? compiled = SchemaLowering.Compile(fast.SourceNodes, fast.Entry!, out SchemaNode[] generatedNodes, out _, out nint entryAddress);
        var withCode = new FlagModeEntry(fast.SourceNodes, generatedNodes, generatedNodes[fast.Entry!.Id], fast.EntryResource, fast.MaxDepth, compiled is null ? null : new CompiledEntry(compiled, entryAddress, this.program, fast.SourceNodes, generatedNodes, fast.EntryResource, fast.MaxDepth, this.rootNode), tiered: true);
        FlagModeEntry? current = Interlocked.CompareExchange(ref this.flagModeEntry, withCode, fast);
        if (ReferenceEquals(current, fast))
        {
            // The public entry reads this first (see Evaluate).
            this.compiledEntry = withCode.Compiled;
            return withCode;
        }

        return current ?? withCode;
    }
#endif
#endif

    /// <summary>
    /// The flag-mode entry data for the program's current node array: the cached object when it is still that
    /// array's, otherwise rebuilt (a rare event: an entry point added to the program).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private FlagModeEntry FlagMode()
    {
        FlagModeEntry? entry = this.flagModeEntry;
        SchemaNode[] nodes = this.program.Nodes;
        return entry is not null && ReferenceEquals(entry.SourceNodes, nodes) ? entry : this.RebuildFlagMode(nodes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private FlagModeEntry RebuildFlagMode(SchemaNode[] nodes)
    {
        SchemaNode root = nodes[this.rootNode];
        var entry = new FlagModeEntry(nodes, nodes, this.program.UsesDynamicScope ? null : nodes[root.FlagEntry], root.ResourceId, this.program.Options.MaxDepth);
        this.flagModeEntry = entry;
#if NET && !STJ
        if (this.compilesEagerly && entry.Entry is not null)
        {
            // JsonSchemaCodeGeneration.Eager: compiled here, before the first evaluation.
            return this.CompileFlagMode(entry);
        }
#endif
        return entry;
    }

    /// <summary>The entry data of flag-mode evaluation for one node array (see <see cref="FlagMode"/>).</summary>
#if NET && !STJ
    private sealed class FlagModeEntry(SchemaNode[] sourceNodes, SchemaNode[] nodes, SchemaNode? entry, int entryResource, int maxDepth, CompiledEntry? compiled = null, bool tiered = false)
#else
    private sealed class FlagModeEntry(SchemaNode[] sourceNodes, SchemaNode[] nodes, SchemaNode? entry, int entryResource, int maxDepth)
#endif
    {
        /// <summary>The program's node array this entry data was made for.</summary>
        public readonly SchemaNode[] SourceNodes = sourceNodes;

        /// <summary>The node array to evaluate with: the program's, or its copy carrying generated methods.</summary>
        public readonly SchemaNode[] Nodes = nodes;
        public readonly SchemaNode? Entry = entry;
        public readonly int EntryResource = entryResource;
        public readonly int MaxDepth = maxDepth;
#if NET && !STJ

        /// <summary>The entry's generated code, once runtime codegen has compiled the schema and its entry is specialised.</summary>
        public readonly CompiledEntry? Compiled = compiled;

        /// <summary>Whether runtime codegen has compiled the schema.</summary>
        public readonly bool Tiered = tiered;
#endif
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
            RegexProvider = source.RegexProvider,
            DocumentResolver = source.DocumentResolver,
            FallbackDocumentResolver = source.FallbackDocumentResolver,
            BaseUri = source.BaseUri,
            MaxDepth = source.MaxDepth,
            CodeGeneration = source.CodeGeneration,
            EntryPoint = source.EntryPoint,
        };
    }
}