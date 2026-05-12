// <copyright file="JsonataSourceGeneratorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Jsonata.SourceGenerator.Tests;

/// <summary>
/// Integration tests for the JSONata source generator.
/// Each test exercises a source-generated evaluator by providing data and asserting on the result.
/// </summary>
[TestClass]
public class JsonataSourceGeneratorTests
{
    private const string TestData = """
        {
            "Account": {
                "Account Name": "Firefly",
                "Order": [
                    {
                        "OrderID": "order103",
                        "Product": [
                            {"Product Name": "Bowler Hat", "Price": 34.45, "Quantity": 2},
                            {"Product Name": "Trilby hat", "Price": 21.67, "Quantity": 1}
                        ]
                    },
                    {
                        "OrderID": "order104",
                        "Product": [
                            {"Product Name": "Bowler Hat", "Price": 34.45, "Quantity": 4},
                            {"Product Name": "Cloak", "Price": 107.99, "Quantity": 1}
                        ]
                    }
                ]
            },
            "FirstName": "Fred",
            "Surname": "Smith",
            "Contact": {
                "Phone": [
                    {"type": "home", "number": "0203 544 1234"},
                    {"type": "mobile", "number": "07700 900 333"}
                ]
            }
        }
        """;

    [TestMethod]
    public void PropertyPath_EvaluatesCorrectly()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(TestData);
        JsonElement result = PropertyPathExpr.Evaluate(doc.RootElement, workspace);

        // Account.Order.Product.Price should return [34.45, 21.67, 34.45, 107.99]
        Assert.AreEqual(JsonValueKind.Array, result.ValueKind);
        Assert.AreEqual(4, result.GetArrayLength());
    }

    [TestMethod]
    public void Arithmetic_EvaluatesCorrectly()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        JsonElement result = ArithmeticExpr.Evaluate(doc.RootElement, workspace);

        // 1 + 2 * 3 = 7 (JSONata follows standard operator precedence)
        Assert.AreEqual(JsonValueKind.Number, result.ValueKind);
        Assert.AreEqual("7", result.GetRawText());
    }

    [TestMethod]
    public void StringConcat_EvaluatesCorrectly()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(TestData);
        JsonElement result = StringConcatExpr.Evaluate(doc.RootElement, workspace);

        // FirstName & ' ' & Surname = "Fred Smith"
        Assert.AreEqual(JsonValueKind.String, result.ValueKind);
        Assert.AreEqual("Fred Smith", result.GetString());
    }

    [TestMethod]
    public void SumProduct_EvaluatesCorrectly()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(TestData);
        JsonElement result = SumProductExpr.Evaluate(doc.RootElement, workspace);

        // $sum(Account.Order.Product.(Price * Quantity))
        // = 34.45*2 + 21.67*1 + 34.45*4 + 107.99*1
        // = 68.90 + 21.67 + 137.80 + 107.99
        // = 336.36
        Assert.AreEqual(JsonValueKind.Number, result.ValueKind);
        Assert.AreEqual("336.36", result.GetRawText());
    }

    [TestMethod]
    public void PredicateFilter_EvaluatesCorrectly()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(TestData);
        JsonElement result = PredicateFilterExpr.Evaluate(doc.RootElement, workspace);

        // Contact.Phone[type = 'mobile'].number = "07700 900 333"
        Assert.AreEqual(JsonValueKind.String, result.ValueKind);
        Assert.AreEqual("07700 900 333", result.GetString());
    }

    [TestMethod]
    public void Map_EvaluatesCorrectly()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(TestData);
        JsonElement result = MapExpr.Evaluate(doc.RootElement, workspace);

        // $map(Account.Order, function($o) { $o.OrderID }) = ["order103", "order104"]
        Assert.AreEqual(JsonValueKind.Array, result.ValueKind);
        Assert.AreEqual(2, result.GetArrayLength());
        Assert.AreEqual("order103", result[0].GetString());
        Assert.AreEqual("order104", result[1].GetString());
    }
}