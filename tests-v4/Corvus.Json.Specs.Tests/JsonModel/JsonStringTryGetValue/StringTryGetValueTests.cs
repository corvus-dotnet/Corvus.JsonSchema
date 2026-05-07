// <copyright file="StringTryGetValueTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Corvus.Json;
using Xunit;

namespace Corvus.Json.Specs.Tests.JsonModel.JsonStringTryGetValue;

/// <summary>
/// Tests for JsonString TryGetValue with char and utf8 parsers.
/// </summary>
public class StringTryGetValueTests
{
    private static bool TryGetIntegerUsingChar(ReadOnlySpan<char> span, in int state, [NotNullWhen(true)] out int? value)
    {
#if NET8_0_OR_GREATER
        if (int.TryParse(span, out int baseValue))
#else
        if (int.TryParse(span.ToString(), out int baseValue))
#endif
        {
            value = baseValue * state;
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryGetIntegerUsingUtf8(ReadOnlySpan<byte> span, in int state, [NotNullWhen(true)] out int? value)
    {
#if NET8_0_OR_GREATER
        if (int.TryParse(Encoding.UTF8.GetString(span), out int baseValue))
#else
        if (int.TryParse(Encoding.UTF8.GetString(span.ToArray()), out int baseValue))
#endif
        {
            value = baseValue * state;
            return true;
        }

        value = default;
        return false;
    }

    // Dotnet-backed + char parser + valid value
    [Fact]
    public void DotnetBacked_CharParser_JsonString_ValidValue()
    {
        var sut = JsonString.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonDate_ValidValue()
    {
        var sut = JsonDate.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonDateTime_ValidValue()
    {
        var sut = JsonDateTime.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonDuration_ValidValue()
    {
        var sut = JsonDuration.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonEmail_ValidValue()
    {
        var sut = JsonEmail.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonHostname_ValidValue()
    {
        var sut = JsonHostname.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonIdnEmail_ValidValue()
    {
        var sut = JsonIdnEmail.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonIdnHostname_ValidValue()
    {
        var sut = JsonIdnHostname.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonIpV4_ValidValue()
    {
        var sut = JsonIpV4.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonIpV6_ValidValue()
    {
        var sut = JsonIpV6.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonIri_ValidValue()
    {
        var sut = JsonIri.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonIriReference_ValidValue()
    {
        var sut = JsonIriReference.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonPointer_ValidValue()
    {
        var sut = JsonPointer.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonRegex_ValidValue()
    {
        var sut = JsonRegex.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonRelativePointer_ValidValue()
    {
        var sut = JsonRelativePointer.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonTime_ValidValue()
    {
        var sut = JsonTime.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonUri_ValidValue()
    {
        var sut = JsonUri.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonUriReference_ValidValue()
    {
        var sut = JsonUriReference.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonUriTemplate_ValidValue()
    {
        var sut = JsonUriTemplate.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonUuid_ValidValue()
    {
        var sut = JsonUuid.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonContent_ValidValue()
    {
        var sut = JsonContent.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonContentPre201909_ValidValue()
    {
        var sut = JsonContentPre201909.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonBase64Content_ValidValue()
    {
        var sut = JsonBase64Content.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonBase64ContentPre201909_ValidValue()
    {
        var sut = JsonBase64ContentPre201909.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonBase64String_ValidValue()
    {
        var sut = JsonBase64String.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonBase64StringPre201909_ValidValue()
    {
        var sut = JsonBase64StringPre201909.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    // JsonElement-backed + char parser + valid value
    [Fact]
    public void JsonElementBacked_CharParser_JsonString_ValidValue()
    {
        var sut = JsonString.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonDate_ValidValue()
    {
        var sut = JsonDate.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonDateTime_ValidValue()
    {
        var sut = JsonDateTime.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonDuration_ValidValue()
    {
        var sut = JsonDuration.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonEmail_ValidValue()
    {
        var sut = JsonEmail.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonHostname_ValidValue()
    {
        var sut = JsonHostname.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonIdnEmail_ValidValue()
    {
        var sut = JsonIdnEmail.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonIdnHostname_ValidValue()
    {
        var sut = JsonIdnHostname.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonIpV4_ValidValue()
    {
        var sut = JsonIpV4.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonIpV6_ValidValue()
    {
        var sut = JsonIpV6.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonIri_ValidValue()
    {
        var sut = JsonIri.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonIriReference_ValidValue()
    {
        var sut = JsonIriReference.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonPointer_ValidValue()
    {
        var sut = JsonPointer.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonRegex_ValidValue()
    {
        var sut = JsonRegex.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonRelativePointer_ValidValue()
    {
        var sut = JsonRelativePointer.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonTime_ValidValue()
    {
        var sut = JsonTime.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonUri_ValidValue()
    {
        var sut = JsonUri.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonUriReference_ValidValue()
    {
        var sut = JsonUriReference.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonUriTemplate_ValidValue()
    {
        var sut = JsonUriTemplate.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonUuid_ValidValue()
    {
        var sut = JsonUuid.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonContent_ValidValue()
    {
        var sut = JsonContent.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonContentPre201909_ValidValue()
    {
        var sut = JsonContentPre201909.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonBase64Content_ValidValue()
    {
        var sut = JsonBase64Content.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonBase64ContentPre201909_ValidValue()
    {
        var sut = JsonBase64ContentPre201909.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonBase64String_ValidValue()
    {
        var sut = JsonBase64String.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonBase64StringPre201909_ValidValue()
    {
        var sut = JsonBase64StringPre201909.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    // Dotnet-backed + char parser + invalid value
    [Fact]
    public void DotnetBacked_CharParser_JsonString_InvalidValue()
    {
        var sut = JsonString.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonDate_InvalidValue()
    {
        var sut = JsonDate.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonDateTime_InvalidValue()
    {
        var sut = JsonDateTime.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonDuration_InvalidValue()
    {
        var sut = JsonDuration.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonEmail_InvalidValue()
    {
        var sut = JsonEmail.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonHostname_InvalidValue()
    {
        var sut = JsonHostname.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonIdnEmail_InvalidValue()
    {
        var sut = JsonIdnEmail.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonIdnHostname_InvalidValue()
    {
        var sut = JsonIdnHostname.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonIpV4_InvalidValue()
    {
        var sut = JsonIpV4.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonIpV6_InvalidValue()
    {
        var sut = JsonIpV6.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonIri_InvalidValue()
    {
        var sut = JsonIri.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonIriReference_InvalidValue()
    {
        var sut = JsonIriReference.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonPointer_InvalidValue()
    {
        var sut = JsonPointer.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonRegex_InvalidValue()
    {
        var sut = JsonRegex.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonRelativePointer_InvalidValue()
    {
        var sut = JsonRelativePointer.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonTime_InvalidValue()
    {
        var sut = JsonTime.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonUri_InvalidValue()
    {
        var sut = JsonUri.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonUriReference_InvalidValue()
    {
        var sut = JsonUriReference.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonUriTemplate_InvalidValue()
    {
        var sut = JsonUriTemplate.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonUuid_InvalidValue()
    {
        var sut = JsonUuid.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonContent_InvalidValue()
    {
        var sut = JsonContent.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonContentPre201909_InvalidValue()
    {
        var sut = JsonContentPre201909.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonBase64Content_InvalidValue()
    {
        var sut = JsonBase64Content.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonBase64ContentPre201909_InvalidValue()
    {
        var sut = JsonBase64ContentPre201909.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonBase64String_InvalidValue()
    {
        var sut = JsonBase64String.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_CharParser_JsonBase64StringPre201909_InvalidValue()
    {
        var sut = JsonBase64StringPre201909.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    // JsonElement-backed + char parser + invalid value
    [Fact]
    public void JsonElementBacked_CharParser_JsonString_InvalidValue()
    {
        var sut = JsonString.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonDate_InvalidValue()
    {
        var sut = JsonDate.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonDateTime_InvalidValue()
    {
        var sut = JsonDateTime.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonDuration_InvalidValue()
    {
        var sut = JsonDuration.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonEmail_InvalidValue()
    {
        var sut = JsonEmail.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonHostname_InvalidValue()
    {
        var sut = JsonHostname.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonIdnEmail_InvalidValue()
    {
        var sut = JsonIdnEmail.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonIdnHostname_InvalidValue()
    {
        var sut = JsonIdnHostname.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonIpV4_InvalidValue()
    {
        var sut = JsonIpV4.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonIpV6_InvalidValue()
    {
        var sut = JsonIpV6.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonIri_InvalidValue()
    {
        var sut = JsonIri.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonIriReference_InvalidValue()
    {
        var sut = JsonIriReference.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonPointer_InvalidValue()
    {
        var sut = JsonPointer.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonRegex_InvalidValue()
    {
        var sut = JsonRegex.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonRelativePointer_InvalidValue()
    {
        var sut = JsonRelativePointer.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonTime_InvalidValue()
    {
        var sut = JsonTime.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonUri_InvalidValue()
    {
        var sut = JsonUri.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonUriReference_InvalidValue()
    {
        var sut = JsonUriReference.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonUriTemplate_InvalidValue()
    {
        var sut = JsonUriTemplate.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonUuid_InvalidValue()
    {
        var sut = JsonUuid.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonContent_InvalidValue()
    {
        var sut = JsonContent.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonContentPre201909_InvalidValue()
    {
        var sut = JsonContentPre201909.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonBase64Content_InvalidValue()
    {
        var sut = JsonBase64Content.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonBase64ContentPre201909_InvalidValue()
    {
        var sut = JsonBase64ContentPre201909.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonBase64String_InvalidValue()
    {
        var sut = JsonBase64String.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_CharParser_JsonBase64StringPre201909_InvalidValue()
    {
        var sut = JsonBase64StringPre201909.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingChar, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    // Dotnet-backed + utf8 parser + valid value
    [Fact]
    public void DotnetBacked_Utf8Parser_JsonString_ValidValue()
    {
        var sut = JsonString.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonDate_ValidValue()
    {
        var sut = JsonDate.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonDateTime_ValidValue()
    {
        var sut = JsonDateTime.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonDuration_ValidValue()
    {
        var sut = JsonDuration.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonEmail_ValidValue()
    {
        var sut = JsonEmail.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonHostname_ValidValue()
    {
        var sut = JsonHostname.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonIdnEmail_ValidValue()
    {
        var sut = JsonIdnEmail.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonIdnHostname_ValidValue()
    {
        var sut = JsonIdnHostname.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonIpV4_ValidValue()
    {
        var sut = JsonIpV4.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonIpV6_ValidValue()
    {
        var sut = JsonIpV6.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonIri_ValidValue()
    {
        var sut = JsonIri.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonIriReference_ValidValue()
    {
        var sut = JsonIriReference.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonPointer_ValidValue()
    {
        var sut = JsonPointer.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonRegex_ValidValue()
    {
        var sut = JsonRegex.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonRelativePointer_ValidValue()
    {
        var sut = JsonRelativePointer.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonTime_ValidValue()
    {
        var sut = JsonTime.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonUri_ValidValue()
    {
        var sut = JsonUri.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonUriReference_ValidValue()
    {
        var sut = JsonUriReference.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonUriTemplate_ValidValue()
    {
        var sut = JsonUriTemplate.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonUuid_ValidValue()
    {
        var sut = JsonUuid.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonContent_ValidValue()
    {
        var sut = JsonContent.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonContentPre201909_ValidValue()
    {
        var sut = JsonContentPre201909.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonBase64Content_ValidValue()
    {
        var sut = JsonBase64Content.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonBase64ContentPre201909_ValidValue()
    {
        var sut = JsonBase64ContentPre201909.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonBase64String_ValidValue()
    {
        var sut = JsonBase64String.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonBase64StringPre201909_ValidValue()
    {
        var sut = JsonBase64StringPre201909.Parse("\"2\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    // JsonElement-backed + utf8 parser + valid value
    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonString_ValidValue()
    {
        var sut = JsonString.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonDate_ValidValue()
    {
        var sut = JsonDate.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonDateTime_ValidValue()
    {
        var sut = JsonDateTime.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonDuration_ValidValue()
    {
        var sut = JsonDuration.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonEmail_ValidValue()
    {
        var sut = JsonEmail.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonHostname_ValidValue()
    {
        var sut = JsonHostname.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonIdnEmail_ValidValue()
    {
        var sut = JsonIdnEmail.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonIdnHostname_ValidValue()
    {
        var sut = JsonIdnHostname.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonIpV4_ValidValue()
    {
        var sut = JsonIpV4.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonIpV6_ValidValue()
    {
        var sut = JsonIpV6.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonIri_ValidValue()
    {
        var sut = JsonIri.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonIriReference_ValidValue()
    {
        var sut = JsonIriReference.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonPointer_ValidValue()
    {
        var sut = JsonPointer.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonRegex_ValidValue()
    {
        var sut = JsonRegex.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonRelativePointer_ValidValue()
    {
        var sut = JsonRelativePointer.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonTime_ValidValue()
    {
        var sut = JsonTime.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonUri_ValidValue()
    {
        var sut = JsonUri.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonUriReference_ValidValue()
    {
        var sut = JsonUriReference.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonUriTemplate_ValidValue()
    {
        var sut = JsonUriTemplate.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonUuid_ValidValue()
    {
        var sut = JsonUuid.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonContent_ValidValue()
    {
        var sut = JsonContent.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonContentPre201909_ValidValue()
    {
        var sut = JsonContentPre201909.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonBase64Content_ValidValue()
    {
        var sut = JsonBase64Content.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonBase64ContentPre201909_ValidValue()
    {
        var sut = JsonBase64ContentPre201909.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonBase64String_ValidValue()
    {
        var sut = JsonBase64String.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonBase64StringPre201909_ValidValue()
    {
        var sut = JsonBase64StringPre201909.Parse("\"2\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.True(success);
        Assert.Equal(6, result);
    }

    // Dotnet-backed + utf8 parser + invalid value
    [Fact]
    public void DotnetBacked_Utf8Parser_JsonString_InvalidValue()
    {
        var sut = JsonString.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonDate_InvalidValue()
    {
        var sut = JsonDate.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonDateTime_InvalidValue()
    {
        var sut = JsonDateTime.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonDuration_InvalidValue()
    {
        var sut = JsonDuration.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonEmail_InvalidValue()
    {
        var sut = JsonEmail.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonHostname_InvalidValue()
    {
        var sut = JsonHostname.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonIdnEmail_InvalidValue()
    {
        var sut = JsonIdnEmail.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonIdnHostname_InvalidValue()
    {
        var sut = JsonIdnHostname.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonIpV4_InvalidValue()
    {
        var sut = JsonIpV4.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonIpV6_InvalidValue()
    {
        var sut = JsonIpV6.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonIri_InvalidValue()
    {
        var sut = JsonIri.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonIriReference_InvalidValue()
    {
        var sut = JsonIriReference.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonPointer_InvalidValue()
    {
        var sut = JsonPointer.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonRegex_InvalidValue()
    {
        var sut = JsonRegex.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonRelativePointer_InvalidValue()
    {
        var sut = JsonRelativePointer.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonTime_InvalidValue()
    {
        var sut = JsonTime.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonUri_InvalidValue()
    {
        var sut = JsonUri.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonUriReference_InvalidValue()
    {
        var sut = JsonUriReference.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonUriTemplate_InvalidValue()
    {
        var sut = JsonUriTemplate.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonUuid_InvalidValue()
    {
        var sut = JsonUuid.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonContent_InvalidValue()
    {
        var sut = JsonContent.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonContentPre201909_InvalidValue()
    {
        var sut = JsonContentPre201909.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonBase64Content_InvalidValue()
    {
        var sut = JsonBase64Content.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonBase64ContentPre201909_InvalidValue()
    {
        var sut = JsonBase64ContentPre201909.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonBase64String_InvalidValue()
    {
        var sut = JsonBase64String.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void DotnetBacked_Utf8Parser_JsonBase64StringPre201909_InvalidValue()
    {
        var sut = JsonBase64StringPre201909.Parse("\"Hello\"").AsDotnetBackedValue();
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    // JsonElement-backed + utf8 parser + invalid value
    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonString_InvalidValue()
    {
        var sut = JsonString.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonDate_InvalidValue()
    {
        var sut = JsonDate.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonDateTime_InvalidValue()
    {
        var sut = JsonDateTime.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonDuration_InvalidValue()
    {
        var sut = JsonDuration.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonEmail_InvalidValue()
    {
        var sut = JsonEmail.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonHostname_InvalidValue()
    {
        var sut = JsonHostname.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonIdnEmail_InvalidValue()
    {
        var sut = JsonIdnEmail.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonIdnHostname_InvalidValue()
    {
        var sut = JsonIdnHostname.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonIpV4_InvalidValue()
    {
        var sut = JsonIpV4.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonIpV6_InvalidValue()
    {
        var sut = JsonIpV6.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonIri_InvalidValue()
    {
        var sut = JsonIri.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonIriReference_InvalidValue()
    {
        var sut = JsonIriReference.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonPointer_InvalidValue()
    {
        var sut = JsonPointer.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonRegex_InvalidValue()
    {
        var sut = JsonRegex.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonRelativePointer_InvalidValue()
    {
        var sut = JsonRelativePointer.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonTime_InvalidValue()
    {
        var sut = JsonTime.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonUri_InvalidValue()
    {
        var sut = JsonUri.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonUriReference_InvalidValue()
    {
        var sut = JsonUriReference.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonUriTemplate_InvalidValue()
    {
        var sut = JsonUriTemplate.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonUuid_InvalidValue()
    {
        var sut = JsonUuid.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonContent_InvalidValue()
    {
        var sut = JsonContent.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonContentPre201909_InvalidValue()
    {
        var sut = JsonContentPre201909.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonBase64Content_InvalidValue()
    {
        var sut = JsonBase64Content.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonBase64ContentPre201909_InvalidValue()
    {
        var sut = JsonBase64ContentPre201909.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonBase64String_InvalidValue()
    {
        var sut = JsonBase64String.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void JsonElementBacked_Utf8Parser_JsonBase64StringPre201909_InvalidValue()
    {
        var sut = JsonBase64StringPre201909.Parse("\"Hello\"");
        bool success = sut.TryGetValue(TryGetIntegerUsingUtf8, 3, out int? result);
        Assert.False(success);
        Assert.Null(result);
    }
}