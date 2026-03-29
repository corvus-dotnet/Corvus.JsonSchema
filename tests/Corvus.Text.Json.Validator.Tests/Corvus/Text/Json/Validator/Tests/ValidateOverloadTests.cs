// <copyright file="ValidateOverloadTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Xunit;

namespace Corvus.Text.Json.Validator.Tests;

/// <summary>
/// Tests for the different Validate overloads on <see cref="JsonSchema"/>.
/// </summary>
public class ValidateOverloadTests
{
    private static readonly JsonSchema Schema = CreateSchema();

    [Fact]
    public void Validate_String_Valid()
    {
        Assert.True(Schema.Validate("""{"name":"Alice","age":30}"""));
    }

    [Fact]
    public void Validate_String_Invalid()
    {
        Assert.False(Schema.Validate("""{"name":"Alice"}"""));
    }

    [Fact]
    public void Validate_ReadOnlyMemoryByte_Valid()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("""{"name":"Bob","age":25}""");

        Assert.True(Schema.Validate(new ReadOnlyMemory<byte>(utf8)));
    }

    [Fact]
    public void Validate_ReadOnlyMemoryByte_Invalid()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("""{"name":"Bob"}""");

        Assert.False(Schema.Validate(new ReadOnlyMemory<byte>(utf8)));
    }

    [Fact]
    public void Validate_ReadOnlyMemoryChar_Valid()
    {
        string json = """{"name":"Charlie","age":40}""";

        Assert.True(Schema.Validate(json.AsMemory()));
    }

    [Fact]
    public void Validate_ReadOnlyMemoryChar_Invalid()
    {
        string json = """{"name":"Charlie"}""";

        Assert.False(Schema.Validate(json.AsMemory()));
    }

    [Fact]
    public void Validate_Stream_Valid()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("""{"name":"Diana","age":35}""");
        using MemoryStream stream = new(utf8);

        Assert.True(Schema.Validate(stream));
    }

    [Fact]
    public void Validate_Stream_Invalid()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("""{"name":"Diana"}""");
        using MemoryStream stream = new(utf8);

        Assert.False(Schema.Validate(stream));
    }

    [Fact]
    public void Validate_ReadOnlySequenceByte_Valid()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("""{"name":"Eve","age":28}""");
        ReadOnlySequence<byte> sequence = new(utf8);

        Assert.True(Schema.Validate(sequence));
    }

    [Fact]
    public void Validate_ReadOnlySequenceByte_Invalid()
    {
        byte[] utf8 = Encoding.UTF8.GetBytes("""{"name":"Eve"}""");
        ReadOnlySequence<byte> sequence = new(utf8);

        Assert.False(Schema.Validate(sequence));
    }

    private static JsonSchema CreateSchema()
    {
        string schemaJson =
            """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$id": "https://example.com/test/validate-overloads",
              "type": "object",
              "required": ["name", "age"],
              "properties": {
                "name": { "type": "string" },
                "age": { "type": "integer", "minimum": 0 }
              },
              "additionalProperties": false
            }
            """;

        return JsonSchema.FromText(schemaJson);
    }
}