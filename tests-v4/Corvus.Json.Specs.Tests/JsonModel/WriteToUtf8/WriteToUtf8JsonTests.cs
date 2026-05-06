// <copyright file="WriteToUtf8JsonTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using System.Buffers;
using System.Text;
using System.Text.Json;
using Corvus.Json;
using Xunit;

namespace Corvus.Json.Specs.Tests.JsonModel.WriteToUtf8;

/// <summary>
/// Tests for WriteToUtf8Json.
/// </summary>
public class WriteToUtf8JsonTests
{
    [Fact]
    public void Dotnet_backed_element_JsonString_foo()
    {
        var sut = JsonString.Parse("\"foo\"").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonString.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonNumber__3_1()
    {
        var sut = JsonNumber.Parse("3.1").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonNumber.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonInteger__3()
    {
        var sut = JsonInteger.Parse("3").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonInteger.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonBoolean_true()
    {
        var sut = JsonBoolean.Parse("true").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonBoolean.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonBoolean_false()
    {
        var sut = JsonBoolean.Parse("false").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonBoolean.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonArray_()
    {
        var sut = JsonArray.Parse("[]").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonArray.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonArray__1_2_3_4()
    {
        var sut = JsonArray.Parse("[1,2,3,\"4\"]").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonArray.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonObject_()
    {
        var sut = JsonObject.Parse("{}").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonObject.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonObject_foo_bar()
    {
        var sut = JsonObject.Parse("{\"foo\":\"bar\"}").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonObject.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonDate__1963_06_19()
    {
        var sut = JsonDate.Parse("\"1963-06-19\"").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonDate.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonDateTime__1990_12_31T15_59_50_123_08_00()
    {
        var sut = JsonDateTime.Parse("\"1990-12-31T15:59:50.123-08:00\"").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonDateTime.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonDuration_P4DT12H30M5S()
    {
        var sut = JsonDuration.Parse("\"P4DT12H30M5S\"").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonDuration.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonEmail_joe_bloggs_example_com()
    {
        var sut = JsonEmail.Parse("\"joe.bloggs@example.com\"").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonEmail.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonHostname_www_example_com()
    {
        var sut = JsonHostname.Parse("\"www.example.com\"").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonHostname.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonIdnEmail_()
    {
        var sut = JsonIdnEmail.Parse("\"실례@실례.테스트\"").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonIdnEmail.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonIdnHostname_()
    {
        var sut = JsonIdnHostname.Parse("\"실례.테스트\"").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonIdnHostname.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonIpV4__192_168_0_1()
    {
        var sut = JsonIpV4.Parse("\"192.168.0.1\"").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonIpV4.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonIpV6__1()
    {
        var sut = JsonIpV6.Parse("\"::1\"").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonIpV6.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonIri_http_r_x_x()
    {
        var sut = JsonIri.Parse("\"http://ƒøø.ßår/?∂éœ=πîx#πîüx\"").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonIri.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonIriReference_http_r_x_x()
    {
        var sut = JsonIriReference.Parse("\"http://ƒøø.ßår/?∂éœ=πîx#πîüx\"").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonIriReference.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonPointer_foo_bar_0_baz_1_a()
    {
        var sut = JsonPointer.Parse("\"/foo/bar~0/baz~1/%a\"").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonPointer.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonRegex_abc_s()
    {
        var sut = JsonRegex.Parse("\"([abc])+\\\\s+$\"").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonRegex.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonRelativePointer__0_foo_bar()
    {
        var sut = JsonRelativePointer.Parse("\"0/foo/bar\"").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonRelativePointer.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonTime__08_30_06Z()
    {
        var sut = JsonTime.Parse("\"08:30:06Z\"").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonTime.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonUri_http_foo_bar_baz_qux_quux()
    {
        var sut = JsonUri.Parse("\"http://foo.bar/?baz=qux#quux\"").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonUri.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonUriReference_http_foo_bar_baz_qux_quux()
    {
        var sut = JsonUriReference.Parse("\"http://foo.bar/?baz=qux#quux\"").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonUriReference.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonUuid__2EB8AA08_AA98_11EA_B4AA_73B441D16380()
    {
        var sut = JsonUuid.Parse("\"2EB8AA08-AA98-11EA-B4AA-73B441D16380\"").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonUuid.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonContent_foo_bar()
    {
        var sut = JsonContent.Parse("\"{\\\"foo\\\": \\\"bar\\\"}\"").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonContent.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonContentPre201909_foo_bar()
    {
        var sut = JsonContentPre201909.Parse("\"{\\\"foo\\\": \\\"bar\\\"}\"").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonContentPre201909.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonBase64Content_eyJmb28iOiJiYXIifQ()
    {
        var sut = JsonBase64Content.Parse("\"eyJmb28iOiJiYXIifQ==\"").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonBase64Content.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonBase64ContentPre201909_eyJmb28iOiJiYXIifQ()
    {
        var sut = JsonBase64ContentPre201909.Parse("\"eyJmb28iOiJiYXIifQ==\"").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonBase64ContentPre201909.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonBase64String_SGVsbG8gd29ybGQ()
    {
        var sut = JsonBase64String.Parse("\"SGVsbG8gd29ybGQ=\"").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonBase64String.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonBase64StringPre201909_SGVsbG8gd29ybGQ()
    {
        var sut = JsonBase64StringPre201909.Parse("\"SGVsbG8gd29ybGQ=\"").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonBase64StringPre201909.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonUInt128__4()
    {
        var sut = JsonUInt128.Parse("4").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonUInt128.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonInt128__4()
    {
        var sut = JsonInt128.Parse("4").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonInt128.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonHalf__4_1()
    {
        var sut = JsonHalf.Parse("4.1").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonHalf.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonInt64__4()
    {
        var sut = JsonInt64.Parse("4").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonInt64.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonInt32__4()
    {
        var sut = JsonInt32.Parse("4").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonInt32.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonInt16__4()
    {
        var sut = JsonInt16.Parse("4").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonInt16.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonSByte__4()
    {
        var sut = JsonSByte.Parse("4").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonSByte.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonUInt64__4()
    {
        var sut = JsonUInt64.Parse("4").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonUInt64.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonUInt32__4()
    {
        var sut = JsonUInt32.Parse("4").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonUInt32.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonUInt16__4()
    {
        var sut = JsonUInt16.Parse("4").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonUInt16.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonByte__4()
    {
        var sut = JsonByte.Parse("4").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonByte.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonSingle__4_1()
    {
        var sut = JsonSingle.Parse("4.1").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonSingle.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonDouble__4_1()
    {
        var sut = JsonDouble.Parse("4.1").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonDouble.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void Dotnet_backed_element_JsonDecimal__4_1()
    {
        var sut = JsonDecimal.Parse("4.1").AsDotnetBackedValue();
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonDecimal.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonString_foo()
    {
        var sut = JsonString.ParseValue("\"foo\"".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonString.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonNumber__3_1()
    {
        var sut = JsonNumber.ParseValue("3.1".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonNumber.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonInteger__3()
    {
        var sut = JsonInteger.ParseValue("3".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonInteger.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonBoolean_true()
    {
        var sut = JsonBoolean.ParseValue("true".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonBoolean.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonBoolean_false()
    {
        var sut = JsonBoolean.ParseValue("false".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonBoolean.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonArray_()
    {
        var sut = JsonArray.ParseValue("[]".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonArray.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonArray__1_2_3_4()
    {
        var sut = JsonArray.ParseValue("[1,2,3,\"4\"]".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonArray.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonObject_()
    {
        var sut = JsonObject.ParseValue("{}".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonObject.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonObject_foo_bar()
    {
        var sut = JsonObject.ParseValue("{\"foo\":\"bar\"}".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonObject.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonDate__1963_06_19()
    {
        var sut = JsonDate.ParseValue("\"1963-06-19\"".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonDate.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonDateTime__1990_12_31T15_59_50_123_08_00()
    {
        var sut = JsonDateTime.ParseValue("\"1990-12-31T15:59:50.123-08:00\"".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonDateTime.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonDuration_P4DT12H30M5S()
    {
        var sut = JsonDuration.ParseValue("\"P4DT12H30M5S\"".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonDuration.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonEmail_joe_bloggs_example_com()
    {
        var sut = JsonEmail.ParseValue("\"joe.bloggs@example.com\"".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonEmail.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonHostname_www_example_com()
    {
        var sut = JsonHostname.ParseValue("\"www.example.com\"".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonHostname.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonIdnEmail_()
    {
        var sut = JsonIdnEmail.ParseValue("\"실례@실례.테스트\"".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonIdnEmail.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonIdnHostname_()
    {
        var sut = JsonIdnHostname.ParseValue("\"실례.테스트\"".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonIdnHostname.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonIpV4__192_168_0_1()
    {
        var sut = JsonIpV4.ParseValue("\"192.168.0.1\"".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonIpV4.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonIpV6__1()
    {
        var sut = JsonIpV6.ParseValue("\"::1\"".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonIpV6.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonIri_http_r_x_x()
    {
        var sut = JsonIri.ParseValue("\"http://ƒøø.ßår/?∂éœ=πîx#πîüx\"".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonIri.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonIriReference_http_r_x_x()
    {
        var sut = JsonIriReference.ParseValue("\"http://ƒøø.ßår/?∂éœ=πîx#πîüx\"".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonIriReference.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonPointer_foo_bar_0_baz_1_a()
    {
        var sut = JsonPointer.ParseValue("\"/foo/bar~0/baz~1/%a\"".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonPointer.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonRegex_abc_s()
    {
        var sut = JsonRegex.ParseValue("\"([abc])+\\\\s+$\"".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonRegex.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonRelativePointer__0_foo_bar()
    {
        var sut = JsonRelativePointer.ParseValue("\"0/foo/bar\"".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonRelativePointer.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonTime__08_30_06Z()
    {
        var sut = JsonTime.ParseValue("\"08:30:06Z\"".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonTime.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonUri_http_foo_bar_baz_qux_quux()
    {
        var sut = JsonUri.ParseValue("\"http://foo.bar/?baz=qux#quux\"".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonUri.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonUriReference_http_foo_bar_baz_qux_quux()
    {
        var sut = JsonUriReference.ParseValue("\"http://foo.bar/?baz=qux#quux\"".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonUriReference.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonUuid__2EB8AA08_AA98_11EA_B4AA_73B441D16380()
    {
        var sut = JsonUuid.ParseValue("\"2EB8AA08-AA98-11EA-B4AA-73B441D16380\"".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonUuid.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonContent_foo_bar()
    {
        var sut = JsonContent.ParseValue("\"{\\\"foo\\\": \\\"bar\\\"}\"".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonContent.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonContentPre201909_foo_bar()
    {
        var sut = JsonContentPre201909.ParseValue("\"{\\\"foo\\\": \\\"bar\\\"}\"".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonContentPre201909.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonBase64Content_eyJmb28iOiJiYXIifQ()
    {
        var sut = JsonBase64Content.ParseValue("\"eyJmb28iOiJiYXIifQ==\"".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonBase64Content.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonBase64ContentPre201909_eyJmb28iOiJiYXIifQ()
    {
        var sut = JsonBase64ContentPre201909.ParseValue("\"eyJmb28iOiJiYXIifQ==\"".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonBase64ContentPre201909.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonBase64String_SGVsbG8gd29ybGQ()
    {
        var sut = JsonBase64String.ParseValue("\"SGVsbG8gd29ybGQ=\"".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonBase64String.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonBase64StringPre201909_SGVsbG8gd29ybGQ()
    {
        var sut = JsonBase64StringPre201909.ParseValue("\"SGVsbG8gd29ybGQ=\"".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonBase64StringPre201909.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonInt64__4()
    {
        var sut = JsonInt64.ParseValue("4".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonInt64.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonInt32__4()
    {
        var sut = JsonInt32.ParseValue("4".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonInt32.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonInt16__4()
    {
        var sut = JsonInt16.ParseValue("4".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonInt16.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonSByte__4()
    {
        var sut = JsonSByte.ParseValue("4".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonSByte.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonUInt64__4()
    {
        var sut = JsonUInt64.ParseValue("4".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonUInt64.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonUInt32__4()
    {
        var sut = JsonUInt32.ParseValue("4".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonUInt32.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonUInt16__4()
    {
        var sut = JsonUInt16.ParseValue("4".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonUInt16.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonByte__4()
    {
        var sut = JsonByte.ParseValue("4".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonByte.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonInt128__4()
    {
        var sut = JsonInt128.ParseValue("4".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonInt128.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonHalf__4_1()
    {
        var sut = JsonHalf.ParseValue("4.1".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonHalf.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonSingle__4_1()
    {
        var sut = JsonSingle.ParseValue("4.1".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonSingle.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonDouble__4_1()
    {
        var sut = JsonDouble.ParseValue("4.1".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonDouble.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }

    [Fact]
    public void JSON_backed_element_JsonDecimal__4_1()
    {
        var sut = JsonDecimal.ParseValue("4.1".AsSpan());
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        ((IJsonValue)sut).WriteTo(writer);
        writer.Flush();
        string serializedResult = Encoding.UTF8.GetString(abw.WrittenSpan);
        var parsed = JsonDecimal.Parse(serializedResult);
        Assert.True(parsed.IsValid());
        Assert.Equal(parsed, sut);
    }
}