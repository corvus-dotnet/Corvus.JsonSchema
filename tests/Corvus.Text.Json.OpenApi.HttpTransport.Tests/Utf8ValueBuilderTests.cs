// <copyright file="Utf8ValueBuilderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.OpenApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.OpenApi.HttpTransport.Tests;

[TestClass]
public class Utf8ValueBuilderTests
{
    [TestMethod]
    public void Append_WithinInitialBuffer_WritesInPlace()
    {
        using Utf8ValueBuilder builder = new(stackalloc byte[16]);
        builder.Append("abc"u8);
        builder.Append((byte)',');
        builder.Append("def"u8);

        Assert.AreEqual("abc,def", Encoding.UTF8.GetString(builder.WrittenSpan));
        Assert.AreEqual(7, builder.Length);
    }

    [TestMethod]
    public void Append_BeyondInitialBuffer_GrowsAndKeepsEveryByte()
    {
        // A value larger than any fixed stack buffer: 600 items of 3 bytes plus separators.
        StringBuilder expected = new();
        using Utf8ValueBuilder builder = new(stackalloc byte[8]);
        for (int i = 0; i < 600; i++)
        {
            if (i > 0)
            {
                builder.Append((byte)',');
                expected.Append(',');
            }

            string item = (i % 1000).ToString("D3");
            builder.Append(Encoding.UTF8.GetBytes(item));
            expected.Append(item);
        }

        Assert.AreEqual(expected.ToString(), Encoding.UTF8.GetString(builder.WrittenSpan));
    }

    [TestMethod]
    public void Append_SingleSpanLargerThanDoubledBuffer_Grows()
    {
        byte[] big = new byte[5000];
        Array.Fill(big, (byte)'x');
        using Utf8ValueBuilder builder = new(stackalloc byte[4]);
        builder.Append("ab"u8);
        builder.Append(big);

        Assert.AreEqual(5002, builder.Length);
        Assert.IsTrue(builder.WrittenSpan[..2].SequenceEqual("ab"u8));
        Assert.IsTrue(builder.WrittenSpan[2..].SequenceEqual(big));
    }

    [TestMethod]
    public void Append_EmptyBuffer_AppendsThroughThePool()
    {
        using Utf8ValueBuilder builder = new(default);
        builder.Append((byte)'a');
        builder.Append("bc"u8);

        Assert.AreEqual("abc", Encoding.UTF8.GetString(builder.WrittenSpan));
    }

    [TestMethod]
    public void Dispose_ClearsTheBuilder()
    {
        Utf8ValueBuilder builder = new(stackalloc byte[2]);
        builder.Append("abcdef"u8);
        builder.Dispose();

        Assert.AreEqual(0, builder.Length);
        Assert.AreEqual(0, builder.WrittenSpan.Length);
    }
}