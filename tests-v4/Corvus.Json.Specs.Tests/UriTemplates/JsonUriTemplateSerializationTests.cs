// <copyright file="JsonUriTemplateSerializationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text.Json;
using Corvus.Json;
using Xunit;

namespace Corvus.Json.Specs.Tests.UriTemplates;

public class JsonUriTemplateSerializationTests
{
    [Fact]
    public void WriteJsonElementBackedJsonUriTemplateToString()
    {
        JsonUriTemplate sut = JsonUriTemplate.Parse("\"http://example.com/dictionary/{term:1}/{term}\"");

        ArrayBufferWriter<byte> abw = new();
        using Utf8JsonWriter writer = new(abw);
        sut.WriteTo(writer);
        writer.Flush();
        JsonAny roundTripped = JsonAny.ParseValue(abw.WrittenSpan);

        Assert.Equal(JsonValueKind.String, roundTripped.ValueKind);
        Assert.Equal(JsonAny.Parse("\"http://example.com/dictionary/{term:1}/{term}\""), roundTripped);
    }

    [Fact]
    public void WriteDotnetBackedJsonUriTemplateToString()
    {
        JsonUriTemplate sut = JsonAny.Parse("\"http://example.com/dictionary/{term:1}/{term}\"").As<JsonUriTemplate>().AsDotnetBackedValue();

        ArrayBufferWriter<byte> abw = new();
        using Utf8JsonWriter writer = new(abw);
        sut.WriteTo(writer);
        writer.Flush();
        JsonAny roundTripped = JsonAny.ParseValue(abw.WrittenSpan);

        Assert.Equal(JsonValueKind.String, roundTripped.ValueKind);
        Assert.Equal(JsonAny.Parse("\"http://example.com/dictionary/{term:1}/{term}\""), roundTripped);
    }
}