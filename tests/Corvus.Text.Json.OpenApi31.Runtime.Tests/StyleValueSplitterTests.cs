// <copyright file="StyleValueSplitterTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi31.Runtime.Tests;

[TestClass]
public class StyleValueSplitterTests
{
    [TestMethod]
    public void NextSeparator_SimpleValues_FindsComma()
    {
        Assert.AreEqual(1, StyleValueSplitter.NextSeparator("a,b,c"));
    }

    [TestMethod]
    public void NextSeparator_NoComma_ReturnsMinusOne()
    {
        Assert.AreEqual(-1, StyleValueSplitter.NextSeparator("hello"));
    }

    [TestMethod]
    public void NextSeparator_Empty_ReturnsMinusOne()
    {
        Assert.AreEqual(-1, StyleValueSplitter.NextSeparator(ReadOnlySpan<char>.Empty));
    }

    [TestMethod]
    public void NextSeparator_SingleComma_ReturnsZero()
    {
        Assert.AreEqual(0, StyleValueSplitter.NextSeparator(",rest"));
    }

    [TestMethod]
    public void NextSeparator_SkipsCommaInsideBraces()
    {
        ReadOnlySpan<char> input = "{\"a\":1,\"b\":2},next";
        int idx = StyleValueSplitter.NextSeparator(input);
        Assert.AreEqual(13, idx);
        Assert.AreEqual("next", input[(idx + 1)..].ToString());
    }

    [TestMethod]
    public void NextSeparator_SkipsCommaInsideBrackets()
    {
        ReadOnlySpan<char> input = "[1,2,3],next";
        int idx = StyleValueSplitter.NextSeparator(input);
        Assert.AreEqual(7, idx);
        Assert.AreEqual("next", input[(idx + 1)..].ToString());
    }

    [TestMethod]
    public void NextSeparator_SkipsNestedBracesAndBrackets()
    {
        ReadOnlySpan<char> input = "{\"arr\":[1,2],\"obj\":{\"x\":3}},next";
        int idx = StyleValueSplitter.NextSeparator(input);
        Assert.AreEqual(27, idx);
        Assert.AreEqual("next", input[(idx + 1)..].ToString());
    }

    [TestMethod]
    public void NextSeparator_SkipsCommaInsideQuotedString()
    {
        ReadOnlySpan<char> input = "\"hello, world\",next";
        int idx = StyleValueSplitter.NextSeparator(input);
        Assert.AreEqual(14, idx);
        Assert.AreEqual("next", input[(idx + 1)..].ToString());
    }

    [TestMethod]
    public void NextSeparator_HandlesEscapedQuoteInString()
    {
        ReadOnlySpan<char> input = "\"say \\\"hi\\\", ok\",next";
        int idx = StyleValueSplitter.NextSeparator(input);
        Assert.AreEqual(input.Length - 5, idx);
        Assert.AreEqual("next", input[(idx + 1)..].ToString());
    }

    [TestMethod]
    public void NextSeparator_NoCommaOutsideNestedJson()
    {
        ReadOnlySpan<char> input = "{\"a\":1,\"b\":2}";
        Assert.AreEqual(-1, StyleValueSplitter.NextSeparator(input));
    }

    [TestMethod]
    public void NextSeparator_ArrayWithJsonEncodedElements()
    {
        ReadOnlySpan<char> input = "simple,{\"nested\":\"value, with comma\"},another";
        int idx1 = StyleValueSplitter.NextSeparator(input);
        Assert.AreEqual(6, idx1);
        Assert.AreEqual("simple", input[..idx1].ToString());

        ReadOnlySpan<char> rest1 = input[(idx1 + 1)..];
        int idx2 = StyleValueSplitter.NextSeparator(rest1);
        Assert.AreEqual("{\"nested\":\"value, with comma\"}", rest1[..idx2].ToString());

        ReadOnlySpan<char> rest2 = rest1[(idx2 + 1)..];
        Assert.AreEqual(-1, StyleValueSplitter.NextSeparator(rest2));
        Assert.AreEqual("another", rest2.ToString());
    }

    [TestMethod]
    public void NextSeparator_ObjectWithJsonEncodedValues()
    {
        ReadOnlySpan<char> input = "key,{\"a\":1,\"b\":2},key2,simple";

        int idx1 = StyleValueSplitter.NextSeparator(input);
        Assert.AreEqual(3, idx1);
        Assert.AreEqual("key", input[..idx1].ToString());

        ReadOnlySpan<char> rest1 = input[(idx1 + 1)..];
        int idx2 = StyleValueSplitter.NextSeparator(rest1);
        Assert.AreEqual("{\"a\":1,\"b\":2}", rest1[..idx2].ToString());

        ReadOnlySpan<char> rest2 = rest1[(idx2 + 1)..];
        int idx3 = StyleValueSplitter.NextSeparator(rest2);
        Assert.AreEqual("key2", rest2[..idx3].ToString());

        ReadOnlySpan<char> rest3 = rest2[(idx3 + 1)..];
        Assert.AreEqual(-1, StyleValueSplitter.NextSeparator(rest3));
        Assert.AreEqual("simple", rest3.ToString());
    }
}