// <copyright file="Repro679DefaultedPropertyTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Specs.Tests.Infrastructure;
using Drivers;
using Xunit;

namespace Corvus.Json.Specs.Tests.HandWritten;

/// <summary>
/// Repro 679: Verifies that defaulted properties are correctly applied
/// when constructing an instance from partial data.
/// </summary>
public class Repro679DefaultedPropertyTests : IClassFixture<Repro679DefaultedPropertyTests.Fixture>
{
    private readonly Fixture _fixture;

    public Repro679DefaultedPropertyTests(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void PropertyWithDefault_HasDefaultValue()
    {
        using var doc = JsonDocument.Parse("""{"regularProperty": "Goodbye"}""");
        IJsonValue instance = JsonSchemaBuilderDriver.CreateInstance(_fixture.GeneratedType, doc.RootElement);
        object? value = GetPropertyValue(instance, "PropertyWithDefault");
        Assert.NotNull(value);
        Assert.Equal("Hello", GetUnquotedValue(value!.ToString()));
    }

    [Fact]
    public void RegularProperty_HasProvidedValue()
    {
        using var doc = JsonDocument.Parse("""{"regularProperty": "Goodbye"}""");
        IJsonValue instance = JsonSchemaBuilderDriver.CreateInstance(_fixture.GeneratedType, doc.RootElement);
        object? value = GetPropertyValue(instance, "RegularProperty");
        Assert.NotNull(value);
        Assert.Equal("Goodbye", GetUnquotedValue(value!.ToString()));
    }

    [Fact]
    public void UnsetProperty_IsUndefined()
    {
        using var doc = JsonDocument.Parse("""{"regularProperty": "Goodbye"}""");
        IJsonValue instance = JsonSchemaBuilderDriver.CreateInstance(_fixture.GeneratedType, doc.RootElement);
        object? value = GetPropertyValue(instance, "UnsetProperty");
        Assert.NotNull(value);

        PropertyInfo? valueKindProp = value!.GetType().GetProperty("ValueKind");
        Assert.NotNull(valueKindProp);
        Assert.Equal(JsonValueKind.Undefined, valueKindProp!.GetValue(value));
    }

    private static object? GetPropertyValue(IJsonValue instance, string propertyName)
    {
        PropertyInfo? prop = instance.GetType().GetProperty(propertyName);
        Assert.NotNull(prop);
        return prop!.GetValue(instance);
    }

    private static string GetUnquotedValue(string? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return value.Trim('"');
    }

    public class Fixture : IAsyncLifetime
    {
        private JsonSchemaBuilderDriver? _driver;

        public Type GeneratedType { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            _driver = DriverFactory.CreateAdditionalDraft202012Driver();
            GeneratedType = await _driver.GenerateTypeForVirtualFile(
                """
                {
                    "type": "object",
                    "properties": {
                        "regularProperty": { "type": "string" },
                        "propertyWithDefault": { "$ref": "#/$defs/defaultedValue" },
                        "unsetProperty": { "type": "string" }
                    },
                    "$defs": {
                        "defaultedValue": {
                            "type": "string",
                            "default": "Hello"
                        }
                    }
                }
                """,
                "Repro679.json",
                "Repro679",
                "DefaultedProperty",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false);
        }

        public Task DisposeAsync()
        {
            _driver?.Dispose();
            return Task.CompletedTask;
        }
    }
}