// <copyright file="DecodeBase64StringTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using System.Text;
using Corvus.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.JsonModel.Base64StringDecoding;

/// <summary>
/// Tests for DecodeBase64String.
/// </summary>
[TestClass]
public class DecodeBase64StringTests
{
    [TestMethod]
    public void Decode_a_valid_string_JsonElement()
    {
        var sut = JsonBase64String.ParseValue("\"SSBoYXZlIGVuY29kZWQgdGhpcyBzdHJpbmc=\"".AsSpan());
        byte[] decodeBuffer = new byte[1024];
        bool decodeSuccess = sut.TryGetDecodedBase64Bytes(decodeBuffer.AsSpan(), out int decodedByteCount);
        Assert.IsTrue(decodeSuccess);
        CollectionAssert.AreEqual(System.Text.Encoding.UTF8.GetBytes("I have encoded this string"), decodeBuffer.AsSpan(0, decodedByteCount).ToArray());
    }

    [TestMethod]
    public void Decode_a_valid_string_dotnet()
    {
        var sut = JsonBase64String.Parse("\"SSBoYXZlIGVuY29kZWQgdGhpcyBzdHJpbmc=\"").AsDotnetBackedValue();
        byte[] decodeBuffer = new byte[1024];
        bool decodeSuccess = sut.TryGetDecodedBase64Bytes(decodeBuffer.AsSpan(), out int decodedByteCount);
        Assert.IsTrue(decodeSuccess);
        CollectionAssert.AreEqual(System.Text.Encoding.UTF8.GetBytes("I have encoded this string"), decodeBuffer.AsSpan(0, decodedByteCount).ToArray());
    }

    [TestMethod]
    public void Decode_a_valid_string_with_a_buffer_that_is_too_short_JsonElement()
    {
        var sut = JsonBase64String.ParseValue("\"SSBoYXZlIGVuY29kZWQgdGhpcyBzdHJpbmc=\"".AsSpan());
        byte[] decodeBuffer = new byte[10];
        bool decodeSuccess = sut.TryGetDecodedBase64Bytes(decodeBuffer.AsSpan(), out int decodedByteCount);
        Assert.IsTrue(decodedByteCount >= 26);
    }

    [TestMethod]
    public void Decode_a_valid_string_with_a_buffer_that_is_too_short_dotnet()
    {
        var sut = JsonBase64String.Parse("\"SSBoYXZlIGVuY29kZWQgdGhpcyBzdHJpbmc=\"").AsDotnetBackedValue();
        byte[] decodeBuffer = new byte[10];
        bool decodeSuccess = sut.TryGetDecodedBase64Bytes(decodeBuffer.AsSpan(), out int decodedByteCount);
        Assert.IsTrue(decodedByteCount >= 26);
    }

    [TestMethod]
    public void Decode_an_invalid_base64_string_JsonElement()
    {
        var sut = JsonBase64String.ParseValue("\"SSBoY%%XZlIGVuY29kZWQgdGhpcyBzdHJpbmc=\"".AsSpan());
        byte[] decodeBuffer = new byte[1024];
        bool decodeSuccess = sut.TryGetDecodedBase64Bytes(decodeBuffer.AsSpan(), out int decodedByteCount);
        Assert.IsFalse(decodeSuccess);
        Assert.AreEqual(0, decodedByteCount);
    }

    [TestMethod]
    public void Decode_an_invalid_base64_string_dotnet()
    {
        var sut = JsonBase64String.Parse("\"SSBoY%%XZlIGVuY29kZWQgdGhpcyBzdHJpbmc=\"").AsDotnetBackedValue();
        byte[] decodeBuffer = new byte[1024];
        bool decodeSuccess = sut.TryGetDecodedBase64Bytes(decodeBuffer.AsSpan(), out int decodedByteCount);
        Assert.IsFalse(decodeSuccess);
        Assert.AreEqual(0, decodedByteCount);
    }

    [TestMethod]
    public void Test_validity_of_a_valid_base64_string_JsonElement()
    {
        var sut = JsonBase64String.ParseValue("\"SSBoYXZlIGVuY29kZWQgdGhpcyBzdHJpbmc=\"".AsSpan());
        Assert.IsTrue(sut.HasBase64Bytes());
    }

    [TestMethod]
    public void Test_validity_of_a_valid_base64_string_dotnet()
    {
        var sut = JsonBase64String.Parse("\"SSBoYXZlIGVuY29kZWQgdGhpcyBzdHJpbmc=\"").AsDotnetBackedValue();
        Assert.IsTrue(sut.HasBase64Bytes());
    }

    [TestMethod]
    public void Test_validity_of_an_invalid_base64_string_JsonElement()
    {
        var sut = JsonBase64String.ParseValue("\"SSBoY%%XZlIGVuY29kZWQgdGhpcyBzdHJpbmc=\"".AsSpan());
        Assert.IsFalse(sut.HasBase64Bytes());
    }

    [TestMethod]
    public void Test_validity_of_an_invalid_base64_string_dotnet()
    {
        var sut = JsonBase64String.Parse("\"SSBoY%%XZlIGVuY29kZWQgdGhpcyBzdHJpbmc=\"").AsDotnetBackedValue();
        Assert.IsFalse(sut.HasBase64Bytes());
    }
}