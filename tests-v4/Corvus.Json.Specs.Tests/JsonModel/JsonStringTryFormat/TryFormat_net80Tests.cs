// <copyright file="TryFormat_net80Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using Corvus.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.JsonModel.JsonStringTryFormat;

#if NET8_0_OR_GREATER

/// <summary>
/// Tests for TryFormat_net80.
/// </summary>
[TestClass]
public class TryFormat_net80Tests
{
    [TestMethod]
    public void JsonElement_backed_string_JsonString_Foo()
    {
        var sut = JsonString.ParseValue("\"Foo\"".AsSpan());
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void JsonElement_backed_string_JsonDate_Foo()
    {
        var sut = JsonDate.ParseValue("\"Foo\"".AsSpan());
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void JsonElement_backed_string_JsonDateTime_Foo()
    {
        var sut = JsonDateTime.ParseValue("\"Foo\"".AsSpan());
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void JsonElement_backed_string_JsonDuration_Foo()
    {
        var sut = JsonDuration.ParseValue("\"Foo\"".AsSpan());
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void JsonElement_backed_string_JsonEmail_Foo()
    {
        var sut = JsonEmail.ParseValue("\"Foo\"".AsSpan());
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void JsonElement_backed_string_JsonHostname_Foo()
    {
        var sut = JsonHostname.ParseValue("\"Foo\"".AsSpan());
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void JsonElement_backed_string_JsonIdnEmail_Foo()
    {
        var sut = JsonIdnEmail.ParseValue("\"Foo\"".AsSpan());
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void JsonElement_backed_string_JsonIdnHostname_Foo()
    {
        var sut = JsonIdnHostname.ParseValue("\"Foo\"".AsSpan());
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void JsonElement_backed_string_JsonIpV4_Foo()
    {
        var sut = JsonIpV4.ParseValue("\"Foo\"".AsSpan());
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void JsonElement_backed_string_JsonIpV6_Foo()
    {
        var sut = JsonIpV6.ParseValue("\"Foo\"".AsSpan());
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void JsonElement_backed_string_JsonIri_Foo()
    {
        var sut = JsonIri.ParseValue("\"Foo\"".AsSpan());
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void JsonElement_backed_string_JsonIriReference_Foo()
    {
        var sut = JsonIriReference.ParseValue("\"Foo\"".AsSpan());
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void JsonElement_backed_string_JsonPointer_Foo()
    {
        var sut = JsonPointer.ParseValue("\"Foo\"".AsSpan());
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void JsonElement_backed_string_JsonRegex_Foo()
    {
        var sut = JsonRegex.ParseValue("\"Foo\"".AsSpan());
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void JsonElement_backed_string_JsonRelativePointer_Foo()
    {
        var sut = JsonRelativePointer.ParseValue("\"Foo\"".AsSpan());
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void JsonElement_backed_string_JsonTime_Foo()
    {
        var sut = JsonTime.ParseValue("\"Foo\"".AsSpan());
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void JsonElement_backed_string_JsonUri_Foo()
    {
        var sut = JsonUri.ParseValue("\"Foo\"".AsSpan());
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void JsonElement_backed_string_JsonUriReference_Foo()
    {
        var sut = JsonUriReference.ParseValue("\"Foo\"".AsSpan());
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void JsonElement_backed_string_JsonUriTemplate_Foo()
    {
        var sut = JsonUriTemplate.ParseValue("\"Foo\"".AsSpan());
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void JsonElement_backed_string_JsonUuid_Foo()
    {
        var sut = JsonUuid.ParseValue("\"Foo\"".AsSpan());
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void JsonElement_backed_string_JsonContent_Foo()
    {
        var sut = JsonContent.ParseValue("\"Foo\"".AsSpan());
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void JsonElement_backed_string_JsonContentPre201909_Foo()
    {
        var sut = JsonContentPre201909.ParseValue("\"Foo\"".AsSpan());
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void JsonElement_backed_string_JsonBase64Content_Foo()
    {
        var sut = JsonBase64Content.ParseValue("\"Foo\"".AsSpan());
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void JsonElement_backed_string_JsonBase64ContentPre201909_Foo()
    {
        var sut = JsonBase64ContentPre201909.ParseValue("\"Foo\"".AsSpan());
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void JsonElement_backed_string_JsonBase64String_Foo()
    {
        var sut = JsonBase64String.ParseValue("\"Foo\"".AsSpan());
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void JsonElement_backed_string_JsonBase64StringPre201909_Foo()
    {
        var sut = JsonBase64StringPre201909.ParseValue("\"Foo\"".AsSpan());
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void Ddotnet_backed_string_JsonString_Foo()
    {
        var sut = JsonString.Parse("\"Foo\"").AsDotnetBackedValue();
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void Ddotnet_backed_string_JsonDate_Foo()
    {
        var sut = JsonDate.Parse("\"Foo\"").AsDotnetBackedValue();
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void Ddotnet_backed_string_JsonDateTime_Foo()
    {
        var sut = JsonDateTime.Parse("\"Foo\"").AsDotnetBackedValue();
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void Ddotnet_backed_string_JsonDuration_Foo()
    {
        var sut = JsonDuration.Parse("\"Foo\"").AsDotnetBackedValue();
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void Ddotnet_backed_string_JsonEmail_Foo()
    {
        var sut = JsonEmail.Parse("\"Foo\"").AsDotnetBackedValue();
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void Ddotnet_backed_string_JsonHostname_Foo()
    {
        var sut = JsonHostname.Parse("\"Foo\"").AsDotnetBackedValue();
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void Ddotnet_backed_string_JsonIdnEmail_Foo()
    {
        var sut = JsonIdnEmail.Parse("\"Foo\"").AsDotnetBackedValue();
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void Ddotnet_backed_string_JsonIdnHostname_Foo()
    {
        var sut = JsonIdnHostname.Parse("\"Foo\"").AsDotnetBackedValue();
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void Ddotnet_backed_string_JsonIpV4_Foo()
    {
        var sut = JsonIpV4.Parse("\"Foo\"").AsDotnetBackedValue();
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void Ddotnet_backed_string_JsonIpV6_Foo()
    {
        var sut = JsonIpV6.Parse("\"Foo\"").AsDotnetBackedValue();
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void Ddotnet_backed_string_JsonIri_Foo()
    {
        var sut = JsonIri.Parse("\"Foo\"").AsDotnetBackedValue();
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void Ddotnet_backed_string_JsonIriReference_Foo()
    {
        var sut = JsonIriReference.Parse("\"Foo\"").AsDotnetBackedValue();
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void Ddotnet_backed_string_JsonPointer_Foo()
    {
        var sut = JsonPointer.Parse("\"Foo\"").AsDotnetBackedValue();
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void Ddotnet_backed_string_JsonRegex_Foo()
    {
        var sut = JsonRegex.Parse("\"Foo\"").AsDotnetBackedValue();
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void Ddotnet_backed_string_JsonRelativePointer_Foo()
    {
        var sut = JsonRelativePointer.Parse("\"Foo\"").AsDotnetBackedValue();
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void Ddotnet_backed_string_JsonTime_Foo()
    {
        var sut = JsonTime.Parse("\"Foo\"").AsDotnetBackedValue();
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void Ddotnet_backed_string_JsonUri_Foo()
    {
        var sut = JsonUri.Parse("\"Foo\"").AsDotnetBackedValue();
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void Ddotnet_backed_string_JsonUriReference_Foo()
    {
        var sut = JsonUriReference.Parse("\"Foo\"").AsDotnetBackedValue();
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void Ddotnet_backed_string_JsonUriTemplate_Foo()
    {
        var sut = JsonUriTemplate.Parse("\"Foo\"").AsDotnetBackedValue();
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void Ddotnet_backed_string_JsonUuid_Foo()
    {
        var sut = JsonUuid.Parse("\"Foo\"").AsDotnetBackedValue();
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void Ddotnet_backed_string_JsonContent_Foo()
    {
        var sut = JsonContent.Parse("\"Foo\"").AsDotnetBackedValue();
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void Ddotnet_backed_string_JsonContentPre201909_Foo()
    {
        var sut = JsonContentPre201909.Parse("\"Foo\"").AsDotnetBackedValue();
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void Ddotnet_backed_string_JsonBase64Content_Foo()
    {
        var sut = JsonBase64Content.Parse("\"Foo\"").AsDotnetBackedValue();
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void Ddotnet_backed_string_JsonBase64ContentPre201909_Foo()
    {
        var sut = JsonBase64ContentPre201909.Parse("\"Foo\"").AsDotnetBackedValue();
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void Ddotnet_backed_string_JsonBase64String_Foo()
    {
        var sut = JsonBase64String.Parse("\"Foo\"").AsDotnetBackedValue();
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }

    [TestMethod]
    public void Ddotnet_backed_string_JsonBase64StringPre201909_Foo()
    {
        var sut = JsonBase64StringPre201909.Parse("\"Foo\"").AsDotnetBackedValue();
        Span<char> buffer = stackalloc char[256];
        sut.TryFormat(buffer, out int charsWritten, ReadOnlySpan<char>.Empty, null);
        string formattedResult = buffer[..charsWritten].ToString();
        Assert.AreEqual("Foo", formattedResult);
    }
}

#endif