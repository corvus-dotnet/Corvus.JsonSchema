// <copyright file="JsonSchemaEvaluatorOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

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
/// Options controlling schema compilation and evaluation.
/// </summary>
public sealed class JsonSchemaEvaluatorOptions
{
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
    /// Gets or sets the match timeout for regular expressions.
    /// </summary>
    public TimeSpan RegexMatchTimeout { get; set; } = Regex.InfiniteMatchTimeout;

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
}
