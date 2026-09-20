// <copyright file="UnpairedSurrogateUtf8StringTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Reading a string as unescaped UTF-8 fails when the string contains an unpaired surrogate escape.
/// It used to return the escaped text as though it were the value.
/// </summary>
[TestClass]
public class UnpairedSurrogateUtf8StringTests
{
    [TestMethod]
    [DataRow("\"\\ud83d\"", DisplayName = "Lone high surrogate")]
    [DataRow("\"\\ude00\"", DisplayName = "Lone low surrogate")]
    [DataRow("\"\\ude00\\ud83d\"", DisplayName = "Reversed surrogate pair")]
    public void ParsedDocumentThrows(string json)
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            using UnescapedUtf8JsonString value = root.GetUtf8String();
        });
    }

    [TestMethod]
    [DataRow("\"\\ud83d\"", DisplayName = "Lone high surrogate")]
    [DataRow("\"\\ude00\"", DisplayName = "Lone low surrogate")]
    [DataRow("\"\\ude00\\ud83d\"", DisplayName = "Reversed surrogate pair")]
    public void DocumentBuilderThrows(string json)
    {
        using ParsedJsonDocument<JsonElement> source = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = source.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = doc.RootElement;

        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            using UnescapedUtf8JsonString value = root.GetUtf8String();
        });
    }

    [TestMethod]
    [DataRow("\"\\ud83d\"", DisplayName = "Lone high surrogate")]
    [DataRow("\"\\ude00\"", DisplayName = "Lone low surrogate")]
    [DataRow("\"\\ude00\\ud83d\"", DisplayName = "Reversed surrogate pair")]
    public void FixedStringDocumentThrows(string json)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        using var doc = (IDisposable)FixedStringJsonDocument<JsonElement>.Parse(bytes, requiresUnescaping: true);
        IJsonDocument jsonDoc = (IJsonDocument)doc;

        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            using UnescapedUtf8JsonString value = jsonDoc.GetUtf8JsonString(0, JsonTokenType.String);
        });
    }

    [TestMethod]
    public void PairedSurrogatesAreUnescaped()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("\"\\ud83d\\ude00\"");
        using UnescapedUtf8JsonString value = doc.RootElement.GetUtf8String();

        Assert.IsTrue(value.Span.SequenceEqual("\U0001F600"u8));
    }
}