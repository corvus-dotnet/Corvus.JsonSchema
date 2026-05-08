// <copyright file="Repro679DefaultedPropertyTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Specs.Tests.Infrastructure;
using Drivers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.HandWritten;

/// <summary>
/// Repro 679: Verifies that defaulted properties are correctly applied
/// when constructing an instance from partial data.
/// </summary>
[TestClass]
public class Repro679DefaultedPropertyTests
{
    private static Fixture? s_fixture;

    [ClassInitialize]
    public static async Task ClassInit(TestContext context)
    {
        s_fixture = new Fixture();
        await s_fixture!.InitializeAsync();
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        if (s_fixture is not null)
        {
            await s_fixture!.DisposeAsync();
        }
    }

    [TestMethod]
    public void PropertyWithDefault_HasDefaultValue()
    {
        using var doc = JsonDocument.Parse("""{"regularProperty": "Goodbye"}""");
        IJsonValue instance = JsonSchemaBuilderDriver.CreateInstance(s_fixture!.GeneratedType, doc.RootElement);
        object? value = GetPropertyValue(instance, "PropertyWithDefault");
        Assert.IsNotNull(value);
        Assert.AreEqual("Hello", GetUnquotedValue(value!.ToString()));
    }

    [TestMethod]
    public void RegularProperty_HasProvidedValue()
    {
        using var doc = JsonDocument.Parse("""{"regularProperty": "Goodbye"}""");
        IJsonValue instance = JsonSchemaBuilderDriver.CreateInstance(s_fixture!.GeneratedType, doc.RootElement);
        object? value = GetPropertyValue(instance, "RegularProperty");
        Assert.IsNotNull(value);
        Assert.AreEqual("Goodbye", GetUnquotedValue(value!.ToString()));
    }

    [TestMethod]
    public void UnsetProperty_IsUndefined()
    {
        using var doc = JsonDocument.Parse("""{"regularProperty": "Goodbye"}""");
        IJsonValue instance = JsonSchemaBuilderDriver.CreateInstance(s_fixture!.GeneratedType, doc.RootElement);
        object? value = GetPropertyValue(instance, "UnsetProperty");
        Assert.IsNotNull(value);

        PropertyInfo? valueKindProp = value!.GetType().GetProperty("ValueKind");
        Assert.IsNotNull(valueKindProp);
        Assert.AreEqual(JsonValueKind.Undefined, valueKindProp!.GetValue(value));
    }

    private static object? GetPropertyValue(IJsonValue instance, string propertyName)
    {
        PropertyInfo? prop = instance.GetType().GetProperty(propertyName);
        Assert.IsNotNull(prop);
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

    public class Fixture
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