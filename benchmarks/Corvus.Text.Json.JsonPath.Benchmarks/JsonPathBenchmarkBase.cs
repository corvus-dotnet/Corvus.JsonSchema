// <copyright file="JsonPathBenchmarkBase.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using JsonCons.JsonPath;

using CorvusJsonElement = Corvus.Text.Json.JsonElement;
using JEJsonPath = Json.Path.JsonPath;

namespace Corvus.Text.Json.JsonPath.Benchmarks;

/// <summary>
/// Base class for JSONPath benchmarks comparing Corvus, JsonCons, JsonEverything, and System.Text.Json.
/// </summary>
public abstract class JsonPathBenchmarkBase
{
    /// <summary>
    /// Goessner's bookstore JSON, used across all benchmarks.
    /// </summary>
    protected const string BookstoreJson = """
        {
          "store": {
            "book": [
              {
                "category": "reference",
                "author": "Nigel Rees",
                "title": "Sayings of the Century",
                "price": 8.95
              },
              {
                "category": "fiction",
                "author": "Evelyn Waugh",
                "title": "Sword of Honour",
                "price": 12.99
              },
              {
                "category": "fiction",
                "author": "Herman Melville",
                "title": "Moby Dick",
                "isbn": "0-553-21311-3",
                "price": 8.99
              },
              {
                "category": "fiction",
                "author": "J. R. R. Tolkien",
                "title": "The Lord of the Rings",
                "isbn": "0-395-19395-8",
                "price": 22.99
              }
            ],
            "bicycle": {
              "color": "red",
              "price": 19.95
            }
          }
        }
        """;

    private JsonDocument? jsonConsDocument;
    private JsonNode? jsonEverythingNode;
    private string dataJson = string.Empty;
    private ParsedJsonDocument<CorvusJsonElement>? corvusDocument;
    private CorvusJsonElement corvusData;
    private string expression = string.Empty;
    private JsonSelector? jsonConsSelector;
    private JEJsonPath? jsonEverythingPath;

    /// <summary>
    /// Gets the raw JSON string.
    /// </summary>
    protected string DataJsonString => this.dataJson;

    /// <summary>
    /// Gets the JSONPath expression.
    /// </summary>
    protected string Expression => this.expression;

    /// <summary>
    /// Gets the Corvus data element.
    /// </summary>
    protected CorvusJsonElement CorvusData => this.corvusData;

    /// <summary>
    /// Gets the JsonCons document.
    /// </summary>
    protected JsonDocument JsonConsDocument => this.jsonConsDocument!;

    /// <summary>
    /// Gets the pre-compiled JsonCons selector.
    /// </summary>
    protected JsonSelector JsonConsSelector => this.jsonConsSelector!;

    /// <summary>
    /// Gets the JsonEverything (JsonPath.Net) parsed node.
    /// </summary>
    protected JsonNode JsonEverythingNode => this.jsonEverythingNode!;

    /// <summary>
    /// Gets the pre-parsed JsonEverything path.
    /// </summary>
    protected JEJsonPath JsonEverythingPath => this.jsonEverythingPath!;

    /// <summary>
    /// Sets up all libraries and pre-warms caches.
    /// </summary>
    protected void Setup(string expressionText, string dataJsonText, string? jsonConsExpression = null)
    {
        this.expression = expressionText;
        this.dataJson = dataJsonText;

        // Corvus setup — use a disposable ParsedJsonDocument so property maps
        // can be built, matching JE's parse-time dictionary construction.
        this.corvusDocument = ParsedJsonDocument<CorvusJsonElement>.Parse(dataJsonText);
        this.corvusData = this.corvusDocument.RootElement;

        // Pre-warm the Corvus compilation cache
        JsonPathEvaluator.Default.Query(this.expression, this.corvusData);

        // JsonCons setup
        this.jsonConsDocument = JsonDocument.Parse(dataJsonText);
        this.jsonConsSelector = JsonSelector.Parse(jsonConsExpression ?? expressionText);

        // JsonEverything (JsonPath.Net) setup — uses JsonNode
        this.jsonEverythingNode = JsonNode.Parse(dataJsonText);
        this.jsonEverythingPath = JEJsonPath.Parse(expressionText);
    }
}
