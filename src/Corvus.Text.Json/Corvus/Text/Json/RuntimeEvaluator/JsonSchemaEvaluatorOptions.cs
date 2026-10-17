// <copyright file="JsonSchemaEvaluatorOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Corvus.Text.Json.RuntimeEvaluator;

/// <summary>
/// Resolves a schema document (as UTF-8 JSON) from an absolute URI.
/// </summary>
/// <param name="uri">The absolute URI of the document, without a fragment.</param>
/// <param name="utf8Json">The UTF-8 JSON text of the document, if found.</param>
/// <returns><see langword="true"/> if the document was found.</returns>
public delegate bool JsonSchemaDocumentResolver(string uri, out ReadOnlyMemory<byte> utf8Json);

/// <summary>
/// Supplies a ready-made <see cref="Regex"/> for a schema pattern, so that a program emitted ahead of time can wire
/// in <c>[GeneratedRegex]</c> instances instead of constructing regular expressions at load.
/// </summary>
/// <param name="index">The pattern's position in the program image's pattern table (see
/// <see cref="JsonSchemaEvaluator.GetImagePatterns"/>), or -1 when the schema is being compiled from text.</param>
/// <param name="ecmaPattern">The pattern as written in the schema (ECMA-262 syntax).</param>
/// <returns>The regular expression to use, or <see langword="null"/> to let the evaluator construct one.</returns>
public delegate Regex? JsonSchemaRegexProvider(int index, string ecmaPattern);

/// <summary>
/// Options controlling schema compilation and evaluation.
/// </summary>
public sealed class JsonSchemaEvaluatorOptions
{
    /// <summary>
    /// The default of <see cref="RegexMatchTimeout"/>: 1 second.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <c>pattern</c> or <c>patternProperties</c> keyword is matched by .NET's regular expression engine when the
    /// evaluator has no matcher of its own for the pattern. That engine backtracks. There are patterns on which a
    /// match never finishes, in the interpreted engine, the compiled engine or both, and patterns that take time
    /// exponential in the length of the text. Without a timeout an evaluation of such a pattern does not return.
    /// </para>
    /// <para>
    /// A match that runs for longer than the timeout throws <see cref="RegexMatchTimeoutException"/>. The exception
    /// leaves the evaluation and reaches the caller. It is not reported as an instance that is not valid, because
    /// the evaluator does not know whether the instance matches.
    /// </para>
    /// <para>
    /// Set <see cref="RegexMatchTimeout"/> to <see cref="Regex.InfiniteMatchTimeout"/> to match with no timeout, as
    /// the evaluator did before this default.
    /// </para>
    /// <para>
    /// A pattern the evaluator proves to match in time proportional to the length of the text has no timeout,
    /// whatever <see cref="RegexMatchTimeout"/> is. A timeout makes .NET's engines check the clock as they match,
    /// which costs such a pattern up to half its time again in the interpreted engine, and a fifth in the compiled
    /// one. The proof accepts a pattern with no backreference, lookaround or word boundary, whose repetitions each
    /// repeat one character or set, and in which no repetition can give characters back to another that could take
    /// them: <c>^[a-z]+\.[a-z]+$</c>, <c>^#[0-9a-fA-F]{6}$</c> and <c>(.*)</c> are proved linear, and
    /// <c>^([a-z]+\.)*[a-z]+$</c> and <c>^[a-z]+[a-z0-9]+$</c> are not. Code generated for a schema does the same.
    /// </para>
    /// <para>
    /// The timeout bounds the time of a match and not its memory. A match that backtracks keeps what it may have to
    /// come back to, and it can keep more for as long as it runs, so the timeout bounds that only as far as it
    /// bounds the time. Set a shorter timeout, or a limit on the memory of the process, where neither the schema
    /// nor the instance is trusted.
    /// </para>
    /// </remarks>
    public static readonly TimeSpan DefaultRegexMatchTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets the default options.
    /// </summary>
    public static JsonSchemaEvaluatorOptions Default { get; } = new();

    /// <summary>
    /// Gets or sets the dialect used when a schema resource does not declare <c>$schema</c>.
    /// </summary>
    public JsonSchemaDialect DefaultDialect { get; set; } = JsonSchemaDialect.Draft202012;

    /// <summary>
    /// Gets or sets a value indicating whether <c>format</c> is treated as an assertion.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> (the default) follows the specification: <c>format</c> is an annotation
    /// unless the resource's metaschema enables the format-assertion vocabulary.
    /// <see langword="true"/> forces assertion; <see langword="false"/> forces annotation.
    /// </remarks>
    public bool? AssertFormat { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether, when <see cref="AssertFormat"/> is <see langword="null"/>, resources
    /// written against draft 4, 6 or 7 treat <c>format</c> as an assertion. Those drafts left the choice to the
    /// implementation; generated models have always asserted, so the programs they emit set this.
    /// </summary>
    /// <remarks>
    /// Those drafts also let an implementation add formats of its own, and generated models have always known every
    /// format. So with this set, a resource written against draft 4, 6 or 7 has the formats that later drafts
    /// defined too (<c>date</c>, <c>time</c>, <c>uuid</c>, <c>duration</c> and the rest); without it, each draft
    /// has only the formats it defines, and any other is an annotation.
    /// </remarks>
    public bool AssertFormatInLegacyDrafts { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether <c>contentEncoding</c>/<c>contentMediaType</c> are asserted
    /// in dialects where the specification leaves this to the implementation (Draft 7 and earlier).
    /// </summary>
    public bool AssertContent { get; set; } = true;

    /// <summary>
    /// Gets or sets per-format overrides of the assertion decision, keyed by format name; the key <c>*</c> applies
    /// to every format that has no entry of its own. An entry overrides both <see cref="AssertFormat"/> and the
    /// dialect default for that format.
    /// </summary>
    public IReadOnlyDictionary<string, JsonSchemaFormatMode>? FormatModes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether regular expressions are compiled to IL. Defaults to <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// Compiled regular expressions match faster at the cost of a few milliseconds per pattern at compile time.
    /// Set to <see langword="false"/> to favour cold start over evaluation throughput.
    /// </remarks>
    public bool CompileRegularExpressions { get; set; } = true;

    /// <summary>
    /// Gets or sets the match timeout for the regular expressions the evaluator constructs. Defaults to
    /// <see cref="DefaultRegexMatchTimeout"/> (1 second).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A single match of a <c>pattern</c> or <c>patternProperties</c> keyword that runs for longer than this throws
    /// <see cref="RegexMatchTimeoutException"/> out of the evaluation. See <see cref="DefaultRegexMatchTimeout"/> for
    /// why there is a default. Set <see cref="Regex.InfiniteMatchTimeout"/> to match with no timeout.
    /// </para>
    /// <para>
    /// The timeout applies to the interpreter and to generated code alike (see <see cref="CodeGeneration"/>), and to
    /// an evaluator loaded from a program image. A <see cref="Regex"/> supplied by <see cref="RegexProvider"/> keeps
    /// the timeout it was constructed with.
    /// </para>
    /// </remarks>
    public TimeSpan RegexMatchTimeout { get; set; } = DefaultRegexMatchTimeout;

    /// <summary>
    /// Gets or sets the provider consulted before a regular expression is constructed. Only patterns that need a
    /// <see cref="Regex"/> are offered (the evaluator matches trivial patterns such as prefixes and length ranges
    /// without one); a <see langword="null"/> result falls back to construction with
    /// <see cref="CompileRegularExpressions"/> and <see cref="RegexMatchTimeout"/>.
    /// </summary>
    public JsonSchemaRegexProvider? RegexProvider { get; set; }

    /// <summary>
    /// Gets or sets the resolver used to load documents referenced by <c>$ref</c> that are not already known.
    /// </summary>
    public JsonSchemaDocumentResolver? DocumentResolver { get; set; }

    /// <summary>
    /// Gets or sets the resolver consulted for documents that neither <see cref="DocumentResolver"/> nor the
    /// embedded standard metaschemas provide; intended for file system and network retrieval.
    /// </summary>
    public JsonSchemaDocumentResolver? FallbackDocumentResolver { get; set; }

    /// <summary>
    /// Gets or sets the base URI applied to the root schema when it has no <c>$id</c>.
    /// </summary>
    public string? BaseUri { get; set; }

    /// <summary>
    /// Gets or sets the schema to evaluate against, as a URI reference resolved against the root document:
    /// a JSON pointer fragment such as <c>#/$defs/PersonArray</c>, a plain-name anchor such as <c>#person</c>,
    /// or the URI of another resource (loaded through <see cref="DocumentResolver"/> if necessary).
    /// <see langword="null"/> (the default) evaluates against the document root, matching the generated
    /// model's ability to be rooted at any subschema.
    /// </summary>
    public string? EntryPoint { get; set; }

    /// <summary>
    /// Gets or sets the maximum nesting of in-place applicators (<c>$ref</c>, <c>allOf</c>, <c>anyOf</c>, <c>oneOf</c>,
    /// <c>not</c>, <c>if</c>/<c>then</c>/<c>else</c>, dependent schemas) before evaluation fails with
    /// <see cref="JsonSchemaEvaluationException"/>. Only nodes that the compiler finds on a cycle of in-place
    /// applicators are counted, so an acyclic chain of any length is never limited. Instance depth is bounded
    /// separately by the parser.
    /// </summary>
    public int MaxDepth { get; set; } = 128;

    /// <summary>
    /// Gets or sets whether, and when, the evaluator compiles the schema to code at run time. The default is
    /// <see cref="JsonSchemaCodeGeneration.Disabled"/>: the interpreter evaluates every instance.
    /// </summary>
    /// <remarks>
    /// Generated code gives the same results as the interpreter. It is used by evaluations that collect no results,
    /// and only where the runtime can compile code at run time (not under native AOT): see
    /// <see cref="JsonSchemaCodeGeneration"/>.
    /// </remarks>
    public JsonSchemaCodeGeneration CodeGeneration { get; set; }
}