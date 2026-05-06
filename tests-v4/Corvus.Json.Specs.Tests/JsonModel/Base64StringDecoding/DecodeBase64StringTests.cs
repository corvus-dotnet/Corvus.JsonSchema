// <copyright file="DecodeBase64StringTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using System.Text;
using Corvus.Json;
using Xunit;

namespace Corvus.Json.Specs.Tests.JsonModel.Base64StringDecoding;

/// <summary>
/// Tests for DecodeBase64String.
/// </summary>
public class DecodeBase64StringTests
{
    [Fact]
    public void Decode_a_valid_string_JsonElement()
    {
        var sut = JsonBase64String.ParseValue("\"SSBoYXZlIGVuY29kZWQgdGhpcyBzdHJpbmc=\"".AsSpan());
        byte[] decodeBuffer = new byte[1024];
        bool decodeSuccess = sut.TryGetDecodedBase64Bytes(decodeBuffer.AsSpan(), out int decodedByteCount);
        Assert.True(decodeSuccess);
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes("I have encoded this string"), decodeBuffer[..decodedByteCount]);
    }

    [Fact]
    public void Decode_a_valid_string_dotnet()
    {
        var sut = JsonBase64String.Parse("\"SSBoYXZlIGVuY29kZWQgdGhpcyBzdHJpbmc=\"").AsDotnetBackedValue();
        byte[] decodeBuffer = new byte[1024];
        bool decodeSuccess = sut.TryGetDecodedBase64Bytes(decodeBuffer.AsSpan(), out int decodedByteCount);
        Assert.True(decodeSuccess);
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes("I have encoded this string"), decodeBuffer[..decodedByteCount]);
    }

    [Fact]
    public void Decode_a_valid_string_with_a_buffer_that_is_too_short_JsonElement()
    {
        var sut = JsonBase64String.ParseValue("\"SSBoYXZlIGVuY29kZWQgdGhpcyBzdHJpbmc=\"".AsSpan());
        byte[] decodeBuffer = new byte[10];
        bool decodeSuccess = sut.TryGetDecodedBase64Bytes(decodeBuffer.AsSpan(), out int decodedByteCount);
        Assert.True(decodedByteCount >= 26);
    }

    [Fact]
    public void Decode_a_valid_string_with_a_buffer_that_is_too_short_dotnet()
    {
        var sut = JsonBase64String.Parse("\"SSBoYXZlIGVuY29kZWQgdGhpcyBzdHJpbmc=\"").AsDotnetBackedValue();
        byte[] decodeBuffer = new byte[10];
        bool decodeSuccess = sut.TryGetDecodedBase64Bytes(decodeBuffer.AsSpan(), out int decodedByteCount);
        Assert.True(decodedByteCount >= 26);
    }

    [Fact]
    public void Decode_an_invalid_base64_string_JsonElement()
    {
        var sut = JsonBase64String.ParseValue("\"SSBoY%%XZlIGVuY29kZWQgdGhpcyBzdHJpbmc=\"".AsSpan());
        byte[] decodeBuffer = new byte[1024];
        bool decodeSuccess = sut.TryGetDecodedBase64Bytes(decodeBuffer.AsSpan(), out int decodedByteCount);
        Assert.False(decodeSuccess);
        Assert.Equal(0, decodedByteCount);
    }

    [Fact]
    public void Decode_an_invalid_base64_string_dotnet()
    {
        var sut = JsonBase64String.Parse("\"SSBoY%%XZlIGVuY29kZWQgdGhpcyBzdHJpbmc=\"").AsDotnetBackedValue();
        byte[] decodeBuffer = new byte[1024];
        bool decodeSuccess = sut.TryGetDecodedBase64Bytes(decodeBuffer.AsSpan(), out int decodedByteCount);
        Assert.False(decodeSuccess);
        Assert.Equal(0, decodedByteCount);
    }

    [Fact]
    public void Test_validity_of_a_valid_base64_string_JsonElement()
    {
        var sut = JsonBase64String.ParseValue("\"SSBoYXZlIGVuY29kZWQgdGhpcyBzdHJpbmc=\"".AsSpan());
        Assert.True(sut.HasBase64Bytes());
    }

    [Fact]
    public void Test_validity_of_a_valid_base64_string_dotnet()
    {
        var sut = JsonBase64String.Parse("\"SSBoYXZlIGVuY29kZWQgdGhpcyBzdHJpbmc=\"").AsDotnetBackedValue();
        Assert.True(sut.HasBase64Bytes());
    }

    [Fact]
    public void Test_validity_of_an_invalid_base64_string_JsonElement()
    {
        var sut = JsonBase64String.ParseValue("\"SSBoY%%XZlIGVuY29kZWQgdGhpcyBzdHJpbmc=\"".AsSpan());
        Assert.False(sut.HasBase64Bytes());
    }

    [Fact]
    public void Test_validity_of_an_invalid_base64_string_dotnet()
    {
        var sut = JsonBase64String.Parse("\"SSBoY%%XZlIGVuY29kZWQgdGhpcyBzdHJpbmc=\"").AsDotnetBackedValue();
        Assert.False(sut.HasBase64Bytes());
    }
}