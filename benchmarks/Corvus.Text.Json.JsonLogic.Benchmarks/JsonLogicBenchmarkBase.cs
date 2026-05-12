// <copyright file="JsonLogicBenchmarkBase.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json.Nodes;

using CorvusJsonElement = Corvus.Text.Json.JsonElement;

namespace Corvus.Text.Json.JsonLogic.Benchmarks;

/// <summary>
/// Base class for JsonLogic benchmarks comparing Corvus and JsonEverything implementations.
/// </summary>
public abstract class JsonLogicBenchmarkBase
{
    private JsonNode? jeRule;
    private JsonNode? jeData;
    private CorvusJsonElement corvusData;
    private JsonLogicRule corvusLogicRule;

    /// <summary>
    /// Gets the JsonEverything rule node.
    /// </summary>
    protected JsonNode? JeRule => this.jeRule;

    /// <summary>
    /// Gets the JsonEverything data node.
    /// </summary>
    protected JsonNode? JeData => this.jeData;

    /// <summary>
    /// Gets the Corvus JsonLogic rule.
    /// </summary>
    protected JsonLogicRule CorvusLogicRule => this.corvusLogicRule;

    /// <summary>
    /// Gets the Corvus data element.
    /// </summary>
    protected CorvusJsonElement CorvusData => this.corvusData;

    /// <summary>
    /// Parses rule and data JSON for both libraries, and pre-warms the Corvus compilation cache.
    /// </summary>
    protected void Setup(string ruleJson, string dataJson, int maxDepth = 64)
    {
        // JsonEverything setup
        System.Text.Json.JsonDocumentOptions stjDocOptions = new() { MaxDepth = maxDepth };
        this.jeRule = JsonNode.Parse(ruleJson, nodeOptions: null, documentOptions: stjDocOptions);
        this.jeData = JsonNode.Parse(dataJson, nodeOptions: null, documentOptions: stjDocOptions);

        // Corvus setup
        Corvus.Text.Json.JsonDocumentOptions corvusDocOptions = new() { MaxDepth = maxDepth };
        CorvusJsonElement corvusRule = CorvusJsonElement.ParseValue(Encoding.UTF8.GetBytes(ruleJson), corvusDocOptions);
        this.corvusData = CorvusJsonElement.ParseValue(Encoding.UTF8.GetBytes(dataJson), corvusDocOptions);
        this.corvusLogicRule = new JsonLogicRule(corvusRule);

        // Pre-warm the Corvus compilation cache so benchmarks measure evaluation only
        using JsonWorkspace w = JsonWorkspace.Create();
        JsonLogicEvaluator.Default.Evaluate(this.corvusLogicRule, this.corvusData, w);
    }
}
