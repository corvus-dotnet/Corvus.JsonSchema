// <copyright file="GeneratedMalformedTextTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// A string that is not well-formed text (an escape of half of a surrogate pair, or bytes that are not UTF-8) is
/// accepted when the document is parsed. A generated type reports it not valid against a keyword that reads its
/// characters, and comparing it with other text gives an answer, never an exception
/// (https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/1021).
/// </summary>
[TestClass]
public class GeneratedMalformedTextTests
{
    [TestMethod]
    [DataRow("""{"code": "\ud800"}""")]
    [DataRow("""{"code": "ab\udc00"}""")]
    [DataRow("""{"kind": "\ud83d"}""")]
    [DataRow("""{"\ud800": 1}""")]
    [DataRow("""{"a\udc00": 1}""")]
    public void AGeneratedTypeReportsAMalformedStringNotValid(string json)
    {
        using var doc = ParsedJsonDocument<MalformedTextModel>.Parse(json);
        Assert.IsFalse(doc.RootElement.EvaluateSchema());

        using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        Assert.IsFalse(doc.RootElement.EvaluateSchema(collector));
    }

    [TestMethod]
    public void AGeneratedTypeReportsBytesThatAreNotUtf8NotValid()
    {
        byte[] json = [.. "{\"code\": \"a"u8.ToArray(), 0xED, 0xA0, 0x80, .. "\"}"u8.ToArray()];
        using var doc = ParsedJsonDocument<MalformedTextModel>.Parse(json);
        Assert.IsFalse(doc.RootElement.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("""{"code": "été", "kind": "a", "é": 1}""")]
    [DataRow("""{"code": "été"}""")]
    public void AGeneratedTypeReadsWellFormedTextAsItAlwaysHas(string json)
    {
        using var doc = ParsedJsonDocument<MalformedTextModel>.Parse(json);
        Assert.IsTrue(doc.RootElement.EvaluateSchema());
    }

    [TestMethod]
    public void ValueEquals_AMalformedStringEqualsNoWellFormedText()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""["a\udc00", "\ud800", "é"]""");
        JsonElement lowAfterA = doc.RootElement[0];
        Assert.IsFalse(lowAfterA.ValueEquals("a"));
        Assert.IsFalse(lowAfterA.ValueEquals("a"u8));
        Assert.IsFalse(doc.RootElement[1].ValueEquals("x"u8));
        Assert.IsTrue(doc.RootElement[2].ValueEquals("é"));
    }

    [TestMethod]
    public void DeepEquals_AMalformedStringEqualsTheSameTextAndNothingElse()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[{"k": "\ud800"}, {"k": "\ud800"}, {"k": "\udc00"}, {"\ud800": 1}, {"\ud800": 1}]""");
        JsonElement root = doc.RootElement;
        Assert.IsTrue(JsonElementHelpers.DeepEquals(root[0], root[1]));
        Assert.IsFalse(JsonElementHelpers.DeepEquals(root[0], root[2]));
        Assert.IsTrue(JsonElementHelpers.DeepEquals(root[3], root[4]));

        // The escape and the bytes it is read as are the same text.
        byte[] raw = [.. "{\"k\": \""u8.ToArray(), 0xED, 0xA0, 0x80, .. "\"}"u8.ToArray()];
        using var rawDoc = ParsedJsonDocument<JsonElement>.Parse(raw);
        Assert.IsTrue(JsonElementHelpers.DeepEquals(root[0], rawDoc.RootElement));
    }
}