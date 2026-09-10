// <copyright file="HeaderValueParserTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using CanonTests30.Client;
using CanonTests30.Client.Models;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi30.Runtime.Tests;

[TestClass]
public class HeaderValueParserTests
{
    [TestMethod]
    public void ParseNumber_Integer_ReturnsTypedValue()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonInt32 result = HeaderValueParser.ParseNumber<JsonInt32>("42", workspace);
        Assert.AreEqual(42, (int)result);
    }

    [TestMethod]
    public void ParseNumber_NegativeInteger_ReturnsTypedValue()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonInt64 result = HeaderValueParser.ParseNumber<JsonInt64>("-9999", workspace);
        Assert.AreEqual(-9999L, (long)result);
    }

    [TestMethod]
    public void ParseNumber_Double_ReturnsTypedValue()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonDouble result = HeaderValueParser.ParseNumber<JsonDouble>("3.14", workspace);
        Assert.AreEqual(3.14, (double)result, 0.001);
    }

    [TestMethod]
    public void ParseString_SimpleValue_ReturnsTypedValue()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonString result = HeaderValueParser.ParseString<JsonString>("hello", workspace);
        Assert.AreEqual("hello", (string)result);
    }

    [TestMethod]
    public void ParseString_ValueWithSpecialChars_EscapesCorrectly()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonString result = HeaderValueParser.ParseString<JsonString>("hello \"world\"", workspace);
        Assert.AreEqual("hello \"world\"", (string)result);
    }

    [TestMethod]
    public void ParseString_EmptyValue_ReturnsEmptyString()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonString result = HeaderValueParser.ParseString<JsonString>("", workspace);
        Assert.AreEqual("", (string)result);
    }

    [TestMethod]
    public void ParseNumber_Zero_ReturnsTypedValue()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonInt32 result = HeaderValueParser.ParseNumber<JsonInt32>("0", workspace);
        Assert.AreEqual(0, (int)result);
    }

    [TestMethod]
    public void ParseNumber_LargeInteger_ReturnsTypedValue()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonInt64 result = HeaderValueParser.ParseNumber<JsonInt64>("9223372036854775807", workspace);
        Assert.AreEqual(long.MaxValue, (long)result);
    }

    [TestMethod]
    public void ParseString_WithBackslash_EscapesCorrectly()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonString result = HeaderValueParser.ParseString<JsonString>(@"path\to\file", workspace);
        Assert.AreEqual(@"path\to\file", (string)result);
    }

    [TestMethod]
    public void ParseNumber_NonNumericText_IsBoundAsStringAndFailsTheSchema()
    {
        // Arbitrary text must never become a number token; bound as a string, the integer schema rejects it.
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonInt32 result = HeaderValueParser.ParseNumber<JsonInt32>("abc", workspace);
        Assert.AreEqual(JsonValueKind.String, result.ValueKind);
        Assert.IsFalse(result.EvaluateSchema());
    }

    [TestMethod]
    public void ParseNumber_NumberWithTrailingText_IsBoundAsStringAndFailsTheSchema()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonInt32 result = HeaderValueParser.ParseNumber<JsonInt32>("12abc", workspace);
        Assert.AreEqual(JsonValueKind.String, result.ValueKind);
        Assert.IsFalse(result.EvaluateSchema());
    }

    [TestMethod]
    public void ParseNumber_EmptyText_IsBoundAsString()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonInt32 result = HeaderValueParser.ParseNumber<JsonInt32>(string.Empty, workspace);
        Assert.AreEqual(JsonValueKind.String, result.ValueKind);
    }

    [TestMethod]
    public void ParseBoolean_Literals_AreBooleans()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Assert.AreEqual(JsonValueKind.True, HeaderValueParser.ParseBoolean<JsonString>("true", workspace).ValueKind);
        Assert.AreEqual(JsonValueKind.False, HeaderValueParser.ParseBoolean<JsonString>("false", workspace).ValueKind);
    }

    [TestMethod]
    public void ParseBoolean_NonLiteralText_IsBoundAsString()
    {
        // Only the JSON literals are booleans; "maybe" (or "True") must not become a boolean token.
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Assert.AreEqual(JsonValueKind.String, HeaderValueParser.ParseBoolean<JsonString>("maybe", workspace).ValueKind);
        Assert.AreEqual(JsonValueKind.String, HeaderValueParser.ParseBoolean<JsonString>("True", workspace).ValueKind);
    }
}