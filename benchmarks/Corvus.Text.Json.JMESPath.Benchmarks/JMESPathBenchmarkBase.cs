// <copyright file="JMESPathBenchmarkBase.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using DevLab.JmesPath;

using CorvusJsonElement = Corvus.Text.Json.JsonElement;

namespace Corvus.Text.Json.JMESPath.Benchmarks;

/// <summary>
/// Base class for JMESPath benchmarks comparing Corvus and JmesPath.Net implementations.
/// </summary>
public abstract class JMESPathBenchmarkBase
{
    private JmesPath? jmesPath;
    private string dataJson = string.Empty;
    private CorvusJsonElement corvusData;
    private string expression = string.Empty;

    /// <summary>
    /// Gets the JmesPath.Net instance.
    /// </summary>
    protected JmesPath JmesPath => this.jmesPath!;

    /// <summary>
    /// Gets the data JSON string for JmesPath.Net.
    /// </summary>
    protected string DataJsonString => this.dataJson;

    /// <summary>
    /// Gets the JMESPath expression string.
    /// </summary>
    protected string Expression => this.expression;

    /// <summary>
    /// Gets the Corvus data element.
    /// </summary>
    protected CorvusJsonElement CorvusData => this.corvusData;

    /// <summary>
    /// Sets up both libraries and pre-warms the Corvus compilation cache.
    /// </summary>
    protected void Setup(string expressionText, string dataJsonText)
    {
        this.expression = expressionText;
        this.dataJson = dataJsonText;

        // JmesPath.Net setup
        this.jmesPath = new JmesPath();

        // Corvus setup
        this.corvusData = CorvusJsonElement.ParseValue(Encoding.UTF8.GetBytes(dataJsonText));

        // Pre-warm the Corvus compilation cache so benchmarks measure evaluation only
        using JsonWorkspace w = JsonWorkspace.Create();
        JMESPathEvaluator.Default.Search(this.expression, this.corvusData, w);
    }
}
