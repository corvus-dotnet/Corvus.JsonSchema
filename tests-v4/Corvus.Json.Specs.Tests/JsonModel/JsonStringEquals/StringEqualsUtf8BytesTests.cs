// <copyright file="StringEqualsUtf8BytesTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using System.Text;
using Corvus.Json;
using Xunit;

namespace Corvus.Json.Specs.Tests.JsonModel.JsonStringEquals;

/// <summary>
/// Tests for StringEqualsUtf8BytesTests.
/// </summary>
public class StringEqualsUtf8BytesTests
{
    [Fact]
    public void utf8bytes_JsonElementBacked_JsonString_Hello_True()
    {
        var sut = JsonString.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonDate_Hello_True()
    {
        var sut = JsonDate.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonDateTime_Hello_True()
    {
        var sut = JsonDateTime.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonDuration_Hello_True()
    {
        var sut = JsonDuration.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonEmail_Hello_True()
    {
        var sut = JsonEmail.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonHostname_Hello_True()
    {
        var sut = JsonHostname.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonIdnEmail_Hello_True()
    {
        var sut = JsonIdnEmail.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonIdnHostname_Hello_True()
    {
        var sut = JsonIdnHostname.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonIpV4_Hello_True()
    {
        var sut = JsonIpV4.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonIpV6_Hello_True()
    {
        var sut = JsonIpV6.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonIri_Hello_True()
    {
        var sut = JsonIri.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonIriReference_Hello_True()
    {
        var sut = JsonIriReference.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonPointer_Hello_True()
    {
        var sut = JsonPointer.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonRegex_Hello_True()
    {
        var sut = JsonRegex.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonRelativePointer_Hello_True()
    {
        var sut = JsonRelativePointer.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonTime_Hello_True()
    {
        var sut = JsonTime.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonUri_Hello_True()
    {
        var sut = JsonUri.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonUriReference_Hello_True()
    {
        var sut = JsonUriReference.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonUriTemplate_Hello_True()
    {
        var sut = JsonUriTemplate.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonUuid_Hello_True()
    {
        var sut = JsonUuid.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonContent_Hello_True()
    {
        var sut = JsonContent.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonContentPre201909_Hello_True()
    {
        var sut = JsonContentPre201909.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonBase64Content_Hello_True()
    {
        var sut = JsonBase64Content.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonBase64ContentPre201909_Hello_True()
    {
        var sut = JsonBase64ContentPre201909.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonBase64String_Hello_True()
    {
        var sut = JsonBase64String.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonBase64StringPre201909_Hello_True()
    {
        var sut = JsonBase64StringPre201909.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonString_12345678901234567890_True()
    {
        var sut = JsonString.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonDate_12345678901234567890_True()
    {
        var sut = JsonDate.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonDateTime_12345678901234567890_True()
    {
        var sut = JsonDateTime.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonDuration_12345678901234567890_True()
    {
        var sut = JsonDuration.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonEmail_12345678901234567890_True()
    {
        var sut = JsonEmail.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonHostname_12345678901234567890_True()
    {
        var sut = JsonHostname.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonIdnEmail_12345678901234567890_True()
    {
        var sut = JsonIdnEmail.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonIdnHostname_12345678901234567890_True()
    {
        var sut = JsonIdnHostname.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonIpV4_12345678901234567890_True()
    {
        var sut = JsonIpV4.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonIpV6_12345678901234567890_True()
    {
        var sut = JsonIpV6.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonIri_12345678901234567890_True()
    {
        var sut = JsonIri.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonIriReference_12345678901234567890_True()
    {
        var sut = JsonIriReference.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonPointer_12345678901234567890_True()
    {
        var sut = JsonPointer.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonRegex_12345678901234567890_True()
    {
        var sut = JsonRegex.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonRelativePointer_12345678901234567890_True()
    {
        var sut = JsonRelativePointer.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonTime_12345678901234567890_True()
    {
        var sut = JsonTime.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonUri_12345678901234567890_True()
    {
        var sut = JsonUri.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonUriReference_12345678901234567890_True()
    {
        var sut = JsonUriReference.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonUriTemplate_12345678901234567890_True()
    {
        var sut = JsonUriTemplate.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonUuid_12345678901234567890_True()
    {
        var sut = JsonUuid.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonContent_12345678901234567890_True()
    {
        var sut = JsonContent.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonContentPre201909_12345678901234567890_True()
    {
        var sut = JsonContentPre201909.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonBase64Content_12345678901234567890_True()
    {
        var sut = JsonBase64Content.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonBase64ContentPre201909_12345678901234567890_True()
    {
        var sut = JsonBase64ContentPre201909.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonBase64String_12345678901234567890_True()
    {
        var sut = JsonBase64String.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonBase64StringPre201909_12345678901234567890_True()
    {
        var sut = JsonBase64StringPre201909.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonString_Goodbye_False()
    {
        var sut = JsonString.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonDate_Goodbye_False()
    {
        var sut = JsonDate.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonDateTime_Goodbye_False()
    {
        var sut = JsonDateTime.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonDuration_Goodbye_False()
    {
        var sut = JsonDuration.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonEmail_Goodbye_False()
    {
        var sut = JsonEmail.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonHostname_Goodbye_False()
    {
        var sut = JsonHostname.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonIdnEmail_Goodbye_False()
    {
        var sut = JsonIdnEmail.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonIdnHostname_Goodbye_False()
    {
        var sut = JsonIdnHostname.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonIpV4_Goodbye_False()
    {
        var sut = JsonIpV4.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonIpV6_Goodbye_False()
    {
        var sut = JsonIpV6.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonIri_Goodbye_False()
    {
        var sut = JsonIri.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonIriReference_Goodbye_False()
    {
        var sut = JsonIriReference.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonPointer_Goodbye_False()
    {
        var sut = JsonPointer.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonRegex_Goodbye_False()
    {
        var sut = JsonRegex.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonRelativePointer_Goodbye_False()
    {
        var sut = JsonRelativePointer.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonTime_Goodbye_False()
    {
        var sut = JsonTime.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonUri_Goodbye_False()
    {
        var sut = JsonUri.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonUriReference_Goodbye_False()
    {
        var sut = JsonUriReference.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonUriTemplate_Goodbye_False()
    {
        var sut = JsonUriTemplate.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonUuid_Goodbye_False()
    {
        var sut = JsonUuid.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonContent_Goodbye_False()
    {
        var sut = JsonContent.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonContentPre201909_Goodbye_False()
    {
        var sut = JsonContentPre201909.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonBase64Content_Goodbye_False()
    {
        var sut = JsonBase64Content.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonBase64ContentPre201909_Goodbye_False()
    {
        var sut = JsonBase64ContentPre201909.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonBase64String_Goodbye_False()
    {
        var sut = JsonBase64String.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_JsonElementBacked_JsonBase64StringPre201909_Goodbye_False()
    {
        var sut = JsonBase64StringPre201909.ParseValue("\"Hello\"".AsSpan());
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonString_Hello_True()
    {
        var sut = JsonString.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonDate_Hello_True()
    {
        var sut = JsonDate.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonDateTime_Hello_True()
    {
        var sut = JsonDateTime.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonDuration_Hello_True()
    {
        var sut = JsonDuration.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonEmail_Hello_True()
    {
        var sut = JsonEmail.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonHostname_Hello_True()
    {
        var sut = JsonHostname.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonIdnEmail_Hello_True()
    {
        var sut = JsonIdnEmail.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonIdnHostname_Hello_True()
    {
        var sut = JsonIdnHostname.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonIpV4_Hello_True()
    {
        var sut = JsonIpV4.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonIpV6_Hello_True()
    {
        var sut = JsonIpV6.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonIri_Hello_True()
    {
        var sut = JsonIri.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonIriReference_Hello_True()
    {
        var sut = JsonIriReference.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonPointer_Hello_True()
    {
        var sut = JsonPointer.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonRegex_Hello_True()
    {
        var sut = JsonRegex.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonRelativePointer_Hello_True()
    {
        var sut = JsonRelativePointer.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonTime_Hello_True()
    {
        var sut = JsonTime.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonUri_Hello_True()
    {
        var sut = JsonUri.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonUriReference_Hello_True()
    {
        var sut = JsonUriReference.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonUriTemplate_Hello_True()
    {
        var sut = JsonUriTemplate.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonUuid_Hello_True()
    {
        var sut = JsonUuid.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonContent_Hello_True()
    {
        var sut = JsonContent.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonContentPre201909_Hello_True()
    {
        var sut = JsonContentPre201909.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonBase64Content_Hello_True()
    {
        var sut = JsonBase64Content.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonBase64ContentPre201909_Hello_True()
    {
        var sut = JsonBase64ContentPre201909.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonBase64String_Hello_True()
    {
        var sut = JsonBase64String.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonBase64StringPre201909_Hello_True()
    {
        var sut = JsonBase64StringPre201909.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Hello"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonString_12345678901234567890_True()
    {
        var sut = JsonString.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonDate_12345678901234567890_True()
    {
        var sut = JsonDate.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonDateTime_12345678901234567890_True()
    {
        var sut = JsonDateTime.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonDuration_12345678901234567890_True()
    {
        var sut = JsonDuration.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonEmail_12345678901234567890_True()
    {
        var sut = JsonEmail.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonHostname_12345678901234567890_True()
    {
        var sut = JsonHostname.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonIdnEmail_12345678901234567890_True()
    {
        var sut = JsonIdnEmail.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonIdnHostname_12345678901234567890_True()
    {
        var sut = JsonIdnHostname.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonIpV4_12345678901234567890_True()
    {
        var sut = JsonIpV4.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonIpV6_12345678901234567890_True()
    {
        var sut = JsonIpV6.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonIri_12345678901234567890_True()
    {
        var sut = JsonIri.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonIriReference_12345678901234567890_True()
    {
        var sut = JsonIriReference.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonPointer_12345678901234567890_True()
    {
        var sut = JsonPointer.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonRegex_12345678901234567890_True()
    {
        var sut = JsonRegex.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonRelativePointer_12345678901234567890_True()
    {
        var sut = JsonRelativePointer.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonTime_12345678901234567890_True()
    {
        var sut = JsonTime.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonUri_12345678901234567890_True()
    {
        var sut = JsonUri.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonUriReference_12345678901234567890_True()
    {
        var sut = JsonUriReference.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonUriTemplate_12345678901234567890_True()
    {
        var sut = JsonUriTemplate.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonUuid_12345678901234567890_True()
    {
        var sut = JsonUuid.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonContent_12345678901234567890_True()
    {
        var sut = JsonContent.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonContentPre201909_12345678901234567890_True()
    {
        var sut = JsonContentPre201909.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonBase64Content_12345678901234567890_True()
    {
        var sut = JsonBase64Content.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonBase64ContentPre201909_12345678901234567890_True()
    {
        var sut = JsonBase64ContentPre201909.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonBase64String_12345678901234567890_True()
    {
        var sut = JsonBase64String.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonBase64StringPre201909_12345678901234567890_True()
    {
        var sut = JsonBase64StringPre201909.ParseValue("\"123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"));
        Assert.True(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonString_Goodbye_False()
    {
        var sut = JsonString.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonDate_Goodbye_False()
    {
        var sut = JsonDate.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonDateTime_Goodbye_False()
    {
        var sut = JsonDateTime.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonDuration_Goodbye_False()
    {
        var sut = JsonDuration.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonEmail_Goodbye_False()
    {
        var sut = JsonEmail.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonHostname_Goodbye_False()
    {
        var sut = JsonHostname.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonIdnEmail_Goodbye_False()
    {
        var sut = JsonIdnEmail.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonIdnHostname_Goodbye_False()
    {
        var sut = JsonIdnHostname.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonIpV4_Goodbye_False()
    {
        var sut = JsonIpV4.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonIpV6_Goodbye_False()
    {
        var sut = JsonIpV6.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonIri_Goodbye_False()
    {
        var sut = JsonIri.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonIriReference_Goodbye_False()
    {
        var sut = JsonIriReference.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonPointer_Goodbye_False()
    {
        var sut = JsonPointer.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonRegex_Goodbye_False()
    {
        var sut = JsonRegex.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonRelativePointer_Goodbye_False()
    {
        var sut = JsonRelativePointer.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonTime_Goodbye_False()
    {
        var sut = JsonTime.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonUri_Goodbye_False()
    {
        var sut = JsonUri.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonUriReference_Goodbye_False()
    {
        var sut = JsonUriReference.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonUriTemplate_Goodbye_False()
    {
        var sut = JsonUriTemplate.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonUuid_Goodbye_False()
    {
        var sut = JsonUuid.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonContent_Goodbye_False()
    {
        var sut = JsonContent.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonContentPre201909_Goodbye_False()
    {
        var sut = JsonContentPre201909.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonBase64Content_Goodbye_False()
    {
        var sut = JsonBase64Content.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonBase64ContentPre201909_Goodbye_False()
    {
        var sut = JsonBase64ContentPre201909.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonBase64String_Goodbye_False()
    {
        var sut = JsonBase64String.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }

    [Fact]
    public void utf8bytes_DotnetBacked_JsonBase64StringPre201909_Goodbye_False()
    {
        var sut = JsonBase64StringPre201909.ParseValue("\"Hello\"".AsSpan()).AsDotnetBackedValue();
        bool result = sut.EqualsUtf8Bytes(Encoding.UTF8.GetBytes("Goodbye"));
        Assert.False(result);
    }
}