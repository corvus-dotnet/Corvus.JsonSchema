// <copyright file="JsonataBenchmarkBase.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;

namespace Corvus.Text.Json.Jsonata.Benchmarks;

/// <summary>
/// Base class for JSONata benchmarks. Pre-parses input data and pre-compiles
/// the expression so benchmarks measure evaluation cost only. A separate
/// benchmark method can be used to measure parse + compile + evaluate.
/// </summary>
public abstract class JsonataBenchmarkBase
{
    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? parsedDocument;
    private JsonWorkspace workspace = null!;

    /// <summary>
    /// Gets the pre-parsed input data element.
    /// </summary>
    protected JsonElement Data { get; private set; }

    /// <summary>
    /// Gets the expression string.
    /// </summary>
    protected string Expression { get; private set; } = null!;

    /// <summary>
    /// Gets the pre-warmed evaluator with the expression already cached.
    /// </summary>
    protected JsonataEvaluator Evaluator => this.evaluator;

    /// <summary>
    /// Initializes the benchmark with a JSONata expression and JSON data string.
    /// Parses data, pre-compiles the expression, and runs one warm-up evaluation.
    /// </summary>
    protected void Setup(string expression, string dataJson)
    {
        this.Expression = expression;
        this.parsedDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(dataJson));
        this.Data = this.parsedDocument.RootElement;

        this.evaluator = new JsonataEvaluator();
        this.workspace = JsonWorkspace.Create();

        // Pre-warm: compiles and caches the expression
        this.evaluator.Evaluate(expression, this.Data);
    }

    /// <summary>
    /// Initializes the benchmark with a JSONata expression and JSON data loaded from a file.
    /// </summary>
    protected void SetupFromFile(string expression, string filePath)
    {
        this.Setup(expression, File.ReadAllText(filePath));
    }

    /// <summary>
    /// Evaluates the given expression using the caller-provided workspace,
    /// avoiding the Clone() overhead at the evaluation boundary.
    /// The workspace is reset between iterations to reclaim document memory.
    /// </summary>
    protected JsonElement EvaluateWithWorkspace(string expression, JsonElement data)
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(expression, data, this.workspace);
    }

    /// <summary>
    /// Cleans up the parsed document and workspace.
    /// </summary>
    protected void Cleanup()
    {
        this.workspace.Dispose();
        this.parsedDocument?.Dispose();
        this.parsedDocument = null;
    }
}
