// <copyright file="EscapedStringConstructorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Tests;

using System.Text;
using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A computed string whose text carries JSON escapes keeps its value when it is placed in an object or array
/// constructor (https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/1045).
/// </summary>
[TestClass]
public class EscapedStringConstructorTests
{
    [TestMethod]
    [DataRow("""{"x": a & "!"}""", """{"a":"say \"hi\""}""", "say \"hi\"!")]
    [DataRow("""{"x": a & "!"}""", """{"a":"C:\\temp"}""", "C:\\temp!")]
    [DataRow("""{"x": a & "!"}""", """{"a":"line1\nline2"}""", "line1\nline2!")]
    [DataRow("""{"x": a & "!"}""", """{"a":"\u00F8"}""", "\u00F8!")]
    [DataRow("""{"x": $uppercase(a)}""", """{"a":"say \"hi\""}""", "SAY \"HI\"")]
    [DataRow("""{"x": $uppercase(a)}""", """{"a":"line1\nline2"}""", "LINE1\nLINE2")]
    [DataRow("""{"x": $join([a, "!"])}""", """{"a":"say \"hi\""}""", "say \"hi\"!")]
    [DataRow("""{"x": $string(a & "!")}""", """{"a":"say \"hi\""}""", "say \"hi\"!")]
    [DataRow("""{"x": "a\"b" & "!"}""", """{}""", "a\"b!")]
    [DataRow("""{"x": a, "y": a & "!"}""", """{"a":"say \"hi\""}""", "say \"hi\"")]
    public void RT_ObjectConstructor_KeepsTheValueOfAnEscapedString(string expression, string input, string expected)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(input));
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = new JsonataEvaluator().Evaluate(expression, doc.RootElement, workspace);

        Assert.IsTrue(result.TryGetProperty("x", out JsonElement x), result.GetRawText());
        Assert.AreEqual(expected, x.GetString(), result.GetRawText());
        Assert.AreEqual(expected, System.Text.Json.JsonDocument.Parse(result.GetRawText()).RootElement.GetProperty("x").GetString(), result.GetRawText());
    }

    [TestMethod]
    [DataRow("""[a & "!"]""", """{"a":"say \"hi\""}""", "say \"hi\"!")]
    [DataRow("""[a & "!", 1]""", """{"a":"line1\nline2"}""", "line1\nline2!")]
    [DataRow("""[$uppercase(a)]""", """{"a":"\u00F8"}""", "\u00D8")]
    public void RT_ArrayConstructor_KeepsTheValueOfAnEscapedString(string expression, string input, string expected)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(input));
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = new JsonataEvaluator().Evaluate(expression, doc.RootElement, workspace);

        Assert.AreEqual(expected, result[0].GetString(), result.GetRawText());
        Assert.AreEqual(expected, System.Text.Json.JsonDocument.Parse(result.GetRawText()).RootElement[0].GetString(), result.GetRawText());
    }
}
