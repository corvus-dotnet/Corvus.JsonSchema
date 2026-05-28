// <copyright file="CodeGenRegressionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.Specs.Tests.Infrastructure;
using Drivers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.HandWritten;

/// <summary>
/// Tests for code generation regression scenarios that only need to verify
/// code generation succeeds without throwing.
/// </summary>
[TestClass]
public class CodeGenRegressionTests
{
    [TestMethod]
    public async Task UnableToFindProperty_CodeGenDoesNotThrow()
    {
        // Repro: A large schema with nested $refs and additionalProperties
        // that previously caused "unable to find property" exceptions.
        using var driver = DriverFactory.CreateDraft4Driver();
        IReadOnlyCollection<GeneratedCodeFile> code = await driver.GenerateCodeForVirtualFile(
            """
            {
              "$comment": "https://docs.codeclimate.com/docs/advanced-configuration",
              "$schema": "http://json-schema.org/draft-04/schema#",
              "definitions": {
                "enabled": {
                  "type": "object",
                  "properties": {
                    "enabled": { "title": "Enabled", "type": "boolean", "default": true }
                  }
                },
                "config": { "title": "Config", "type": "object" },
                "threshold": { "title": "Threshold", "type": ["integer", "null"] }
              },
              "description": "Configuration file as an alternative for configuring your repository in the settings page.",
              "id": "https://json.schemastore.org/codeclimate.json",
              "properties": {
                "version": { "title": "Version", "description": "Required to adjust maintainability checks.", "type": "string", "default": "2" },
                "prepare": {
                  "title": "Prepare", "type": "array",
                  "items": { "type": "object", "properties": { "url": { "title": "URL", "type": "string", "format": "uri" }, "path": { "title": "Path", "type": "string" } } }
                },
                "checks": {
                  "title": "Checks", "type": "object",
                  "properties": {
                    "argument-count": { "title": "Argument Count", "$ref": "#/definitions/enabled", "properties": { "config": { "$ref": "#/definitions/config", "properties": { "threshold": { "$ref": "#/definitions/threshold", "default": 4 } } } } },
                    "complex-logic": { "title": "Complex Logic", "$ref": "#/definitions/enabled", "properties": { "config": { "$ref": "#/definitions/config", "properties": { "threshold": { "$ref": "#/definitions/threshold", "default": 4 } } } } },
                    "file-lines": { "title": "File Lines", "$ref": "#/definitions/enabled", "properties": { "config": { "$ref": "#/definitions/config", "properties": { "threshold": { "$ref": "#/definitions/threshold", "default": 250 } } } } },
                    "method-complexity": { "title": "Method Complexity", "$ref": "#/definitions/enabled", "properties": { "config": { "$ref": "#/definitions/config", "properties": { "threshold": { "$ref": "#/definitions/threshold", "default": 5 } } } } },
                    "method-count": { "title": "Method Count", "$ref": "#/definitions/enabled", "properties": { "config": { "$ref": "#/definitions/config", "properties": { "threshold": { "$ref": "#/definitions/threshold", "default": 20 } } } } },
                    "method-lines": { "title": "Method Lines", "$ref": "#/definitions/enabled", "properties": { "config": { "$ref": "#/definitions/config", "properties": { "threshold": { "$ref": "#/definitions/threshold", "default": 25 } } } } },
                    "nested-control-flow": { "title": "Nested Control Flow", "$ref": "#/definitions/enabled", "properties": { "config": { "$ref": "#/definitions/config", "properties": { "threshold": { "$ref": "#/definitions/threshold", "default": 4 } } } } },
                    "return-statements": { "title": "Return Statements", "$ref": "#/definitions/enabled", "properties": { "config": { "$ref": "#/definitions/config", "properties": { "threshold": { "$ref": "#/definitions/threshold", "default": 4 } } } } },
                    "similar-code": { "title": "Similar Code", "$ref": "#/definitions/enabled", "properties": { "config": { "$ref": "#/definitions/config", "properties": { "threshold": { "$ref": "#/definitions/threshold" } } } } },
                    "identical-code": { "title": "Identical Code", "$ref": "#/definitions/enabled", "properties": { "config": { "$ref": "#/definitions/config", "properties": { "threshold": { "$ref": "#/definitions/threshold" } } } } }
                  }
                },
                "plugins": { "title": "Plugins", "description": "To add a plugin to your analysis.", "type": "object", "additionalProperties": { "$ref": "#/definitions/enabled" } },
                "exclude_patterns": { "title": "Exclude Patterns", "type": "array", "items": { "title": "Exclude Pattern", "type": "string" } }
              },
              "title": "Code Climate Configuration",
              "type": "object"
            }
            """,
            "unableToFindProperty.json",
            "UnableToFindProperty",
            "CodeGenDoesNotThrow",
            validateFormat: false,
            optionalAsNullable: false,
            useImplicitOperatorString: false);

        // We just need to verify that code generation succeeds
        Assert.IsTrue((code).Any());
    }

    [TestMethod]
    public async Task DuplicateDocumentation_CodeGenSucceeds()
    {
        // Repro #440: A schema that produces duplicate documentation comments.
        // This test verifies code generation succeeds and produces the expected
        // remarks for the 'Files' property.
        using var driver = DriverFactory.CreateDraft4Driver();
        string schemaPath = Path.Combine(
            DriverFactory.RepoRoot,
            "tests-v4",
            "Corvus.Json.Specs.Tests",
            "HandWritten",
            "duplicateDocumentation.schema.json");
        string schema = File.ReadAllText(schemaPath);

        IReadOnlyCollection<GeneratedCodeFile> code = await driver.GenerateCodeForVirtualFile(
            schema,
            "duplicateDocumentation.json",
            "DuplicateDocumentation",
            "ASchemaThatProducesDuplicateDocumentation",
            validateFormat: false,
            optionalAsNullable: false,
            useImplicitOperatorString: false);

        Assert.IsTrue((code).Any());

        // Find the file containing the FilesDefinition property
        GeneratedCodeFile? targetFile = code.FirstOrDefault(
            f => f.FileName.Contains("FilesDefinition.Object"));

        Assert.IsNotNull(targetFile);

        // Verify it contains the expected remarks about 'files' and 'include'
        StringAssert.Contains(targetFile!.FileContent, "If no &#39;files&#39; or &#39;include&#39; property is present");
    }

    [TestMethod]
    public async Task DebuggerDisplayAttribute_IsGeneratedOnCoreType()
    {
        using var driver = DriverFactory.CreateDraft4Driver();
        Type generatedType = await driver.GenerateTypeForVirtualFile(
            """
            {
              "$schema": "http://json-schema.org/draft-04/schema#",
              "title": "Debugger Display",
              "type": "object",
              "properties": {
                "name": { "type": "string" }
              }
            }
            """,
            "debuggerDisplay.json",
            "DebuggerDisplay",
            "AttributeIsGenerated",
            validateFormat: false,
            optionalAsNullable: false,
            useImplicitOperatorString: false);

        DebuggerDisplayAttribute? attribute = generatedType.GetCustomAttribute<DebuggerDisplayAttribute>();
        Assert.IsNotNull(attribute);
        Assert.IsTrue(attribute.Value.StartsWith("{", StringComparison.Ordinal));
        Assert.IsTrue(attribute.Value.EndsWith(",nq}", StringComparison.Ordinal));

        string propertyName = attribute.Value[1..^4];
        PropertyInfo? property = generatedType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(property);

        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse("""{"name":"Alice"}""");
        IJsonValue instance = JsonSchemaBuilderDriver.CreateInstance(generatedType, document.RootElement.Clone());
        Assert.AreEqual(
            "DebuggerDisplay: ValueKind = Object : \"{\"name\":\"Alice\"}\"",
            property.GetValue(instance));
    }

    [TestMethod]
    [DataRow(
        """
        {
          "$schema": "http://json-schema.org/draft-07/schema#",
          "title": "All Of Bool",
          "allOf": [
            { "type": "boolean" },
            { "description": "an allOf boolean" }
          ]
        }
        """,
        "public static implicit operator bool(",
        1,
        "public static explicit operator bool(",
        0)]
    [DataRow(
        """
        {
          "$schema": "http://json-schema.org/draft-07/schema#",
          "title": "String Or Is Available",
          "oneOf": [
            { "type": "boolean" },
            { "type": "string" }
          ]
        }
        """,
        "public static implicit operator bool(",
        0,
        "public static explicit operator bool(",
        1)]
    [DataRow(
        """
        {
          "$schema": "http://json-schema.org/draft-07/schema#",
          "title": "Int Or String",
          "oneOf": [
            { "type": "integer", "format": "int32" },
            { "type": "string" }
          ]
        }
        """,
        "public static implicit operator int(",
        0,
        "public static explicit operator int(",
        1)]
    public async Task CoreTypeConversions_ToDotnet_AreExplicitForMultiCoreTypes(
        string schema,
        string implicitOperator,
        int expectedImplicitCount,
        string explicitOperator,
        int expectedExplicitCount)
    {
        using var driver = DriverFactory.CreateDraft7Driver();
        IReadOnlyCollection<GeneratedCodeFile> code = await driver.GenerateCodeForVirtualFile(
            schema,
            "coreTypeConversions.json",
            "CoreTypeConversions",
            "ToDotnet",
            validateFormat: false,
            optionalAsNullable: false,
            useImplicitOperatorString: false);

        string generatedCode = string.Concat(code.Select(f => f.FileContent));

        Assert.AreEqual(expectedImplicitCount, generatedCode.Split([implicitOperator], StringSplitOptions.None).Length - 1);
        Assert.AreEqual(expectedExplicitCount, generatedCode.Split([explicitOperator], StringSplitOptions.None).Length - 1);
    }

    [TestMethod]
    public async Task CoreTypeConversions_MultiCoreBoolean_Compiles()
    {
        using var driver = DriverFactory.CreateDraft7Driver();
        Type generatedType = await driver.GenerateTypeForVirtualFile(
            """
            {
              "$schema": "http://json-schema.org/draft-07/schema#",
              "title": "Boolean Or String",
              "type": [ "boolean", "string" ]
            }
            """,
            "multiCoreBoolean.json",
            "CoreTypeConversions",
            "MultiCoreBoolean",
            validateFormat: false,
            optionalAsNullable: false,
            useImplicitOperatorString: false);

        Assert.AreEqual("MultiCoreBoolean", generatedType.Name);
    }

    [TestMethod]
    public async Task PatternProperties_GenerateVisitorDispatch()
    {
        using var driver = DriverFactory.CreateDraft7Driver();
        Type generatedType = await driver.GenerateTypeForVirtualFile(
            """
            {
              "$schema": "http://json-schema.org/draft-07/schema#",
              "title": "Pattern Dispatch",
              "type": "object",
              "patternProperties": {
                "^S_": { "type": "string" },
                "^I_": { "type": "integer" }
              }
            }
            """,
            "patternDispatch.json",
            "PatternDispatch",
            "GeneratesVisitorDispatch",
            validateFormat: false,
            optionalAsNullable: false,
            useImplicitOperatorString: false);

        Type visitorInterface = generatedType.GetNestedTypes().Single(t => t.Name.StartsWith("IPatternPropertyVisitor`", StringComparison.Ordinal));
        System.Reflection.MethodInfo matchMethod = generatedType.GetMethods().Single(m => m.Name == "MatchPatternProperties");

        Assert.AreEqual(3, visitorInterface.GetMethods().Length);
        Assert.IsNotNull(visitorInterface.GetMethods().SingleOrDefault(m => m.Name == "VisitUnmatched"));
        Assert.IsTrue(matchMethod.IsGenericMethodDefinition);
        Assert.AreEqual(3, matchMethod.GetParameters().Length);
    }

    [TestMethod]
    [DataRow("allOf")]
    [DataRow("anyOf")]
    [DataRow("oneOf")]
    public async Task WithApplied_CompositionType_MergesPropertiesWithoutMutatingSource(string compositionKeyword)
    {
        using var driver = DriverFactory.CreateDraft7Driver();
        Type generatedType = await driver.GenerateTypeForVirtualFile(
            $$"""
            {
              "$schema": "http://json-schema.org/draft-07/schema#",
              "title": "Apply Composition",
              "type": "object",
              "properties": {
                "name": { "type": "string" },
                "age": { "type": "integer" }
              },
              "{{compositionKeyword}}": [
                {
                  "title": "Applied Properties",
                  "type": "object",
                  "properties": {
                    "age": { "type": "integer" },
                    "city": { "type": "string" }
                  }
                }
              ]
            }
            """,
            $"withApplied-{compositionKeyword}.json",
            "WithApplied",
            compositionKeyword,
            validateFormat: false,
            optionalAsNullable: false,
            useImplicitOperatorString: false);

        System.Reflection.MethodInfo method = generatedType.GetMethods().Single(m => m.Name == "WithApplied");
        Type composedType = method.GetParameters()[0].ParameterType.GetElementType()!;

        using System.Text.Json.JsonDocument baseDocument = System.Text.Json.JsonDocument.Parse("""{"name":"Alice","age":30}""");
        using System.Text.Json.JsonDocument appliedDocument = System.Text.Json.JsonDocument.Parse("""{"age":42,"city":"Paris"}""");

        IJsonValue baseInstance = JsonSchemaBuilderDriver.CreateInstance(generatedType, baseDocument.RootElement.Clone());
        IJsonValue appliedInstance = JsonSchemaBuilderDriver.CreateInstance(composedType, appliedDocument.RootElement.Clone());

        object? result = method.Invoke(baseInstance, [appliedInstance]);

        Assert.IsNotNull(result);
        Assert.AreEqual("""{"name":"Alice","age":42,"city":"Paris"}""", result.ToString());
        Assert.AreEqual("""{"name":"Alice","age":30}""", baseInstance.ToString());
    }

    [TestMethod]
    public void JsonPropertyNameIsMatch_WithEscapedJsonPropertyBacking_MatchesUnescapedName()
    {
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse("""{"S_\u006Eame":1}""");
        System.Text.Json.JsonProperty property = document.RootElement.EnumerateObject().First();
        JsonPropertyName name = new(property);

        Assert.IsTrue(name.IsMatch(new Regex("^S_name$")));
        Assert.IsFalse(name.IsMatch(new Regex("^S_\\\\u006Eame$")));
    }

    [TestMethod]
    public async Task GeneratedNumericType_ImplementsFormattingInterfaces()
    {
        using var driver = DriverFactory.CreateDraft7Driver();
        Type generatedType = await driver.GenerateTypeForVirtualFile(
            """
            {
              "$schema": "http://json-schema.org/draft-07/schema#",
              "title": "Formattable Number",
              "type": "integer",
              "format": "int32",
              "minimum": 0
            }
            """,
            "formattable-number.json",
            "Formattable",
            "Number",
            validateFormat: false,
            optionalAsNullable: false,
            useImplicitOperatorString: false);

        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse("1234");
        IJsonValue instance = JsonSchemaBuilderDriver.CreateInstance(generatedType, document.RootElement.Clone());

        Assert.IsInstanceOfType<IFormattable>(instance);
#if NET8_0_OR_GREATER
        Assert.IsInstanceOfType<ISpanFormattable>(instance);
        Assert.IsInstanceOfType<IUtf8SpanFormattable>(instance);
#endif
        Assert.AreEqual("1,234.00", ((IFormattable)instance).ToString("N2", CultureInfo.InvariantCulture));
        Assert.AreEqual("1234", ((IFormattable)instance).ToString(null, CultureInfo.InvariantCulture));

#if NET8_0_OR_GREATER
        Span<char> chars = stackalloc char[16];
        Assert.IsTrue(((ISpanFormattable)instance).TryFormat(chars, out int charsWritten, "N2", CultureInfo.InvariantCulture));
        Assert.AreEqual("1,234.00", chars[..charsWritten].ToString());

        Span<byte> bytes = stackalloc byte[16];
        Assert.IsTrue(((IUtf8SpanFormattable)instance).TryFormat(bytes, out int bytesWritten, "N2", CultureInfo.InvariantCulture));
        Assert.AreEqual("1,234.00", Encoding.UTF8.GetString(bytes[..bytesWritten]));
#endif
    }

    [TestMethod]
    public async Task GeneratedObjectType_FormatsCanonicalJsonToSpans()
    {
        using var driver = DriverFactory.CreateDraft7Driver();
        Type generatedType = await driver.GenerateTypeForVirtualFile(
            """
            {
              "$schema": "http://json-schema.org/draft-07/schema#",
              "title": "Formattable Object",
              "type": "object",
              "properties": {
                "name": { "type": "string" },
                "age": { "type": "integer" }
              }
            }
            """,
            "formattable-object.json",
            "Formattable",
            "Object",
            validateFormat: false,
            optionalAsNullable: false,
            useImplicitOperatorString: false);

        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse("""{"name":"Alice","age":42}""");
        IJsonValue instance = JsonSchemaBuilderDriver.CreateInstance(generatedType, document.RootElement.Clone());

        Assert.IsInstanceOfType<IFormattable>(instance);
        Assert.AreEqual("""{"name":"Alice","age":42}""", ((IFormattable)instance).ToString(null, CultureInfo.InvariantCulture));

#if NET8_0_OR_GREATER
        Span<char> chars = stackalloc char[64];
        Assert.IsTrue(((ISpanFormattable)instance).TryFormat(chars, out int charsWritten, default, CultureInfo.InvariantCulture));
        Assert.AreEqual("""{"name":"Alice","age":42}""", chars[..charsWritten].ToString());

        Span<char> shortChars = stackalloc char[4];
        Assert.IsFalse(((ISpanFormattable)instance).TryFormat(shortChars, out charsWritten, default, CultureInfo.InvariantCulture));
        Assert.AreEqual(0, charsWritten);

        Span<byte> bytes = stackalloc byte[64];
        Assert.IsTrue(((IUtf8SpanFormattable)instance).TryFormat(bytes, out int bytesWritten, default, CultureInfo.InvariantCulture));
        Assert.AreEqual("""{"name":"Alice","age":42}""", Encoding.UTF8.GetString(bytes[..bytesWritten]));

        Span<byte> shortBytes = stackalloc byte[4];
        Assert.IsFalse(((IUtf8SpanFormattable)instance).TryFormat(shortBytes, out bytesWritten, default, CultureInfo.InvariantCulture));
        Assert.AreEqual(0, bytesWritten);
#endif
    }

    [TestMethod]
    public async Task GeneratedUuidType_UsesFormatAwareToString()
    {
        using var driver = DriverFactory.CreateDraft7Driver();
        Type generatedType = await driver.GenerateTypeForVirtualFile(
            """
            {
              "$schema": "http://json-schema.org/draft-07/schema#",
              "title": "Formattable Uuid",
              "type": "string",
              "format": "uuid",
              "minLength": 36,
              "maxLength": 36
            }
            """,
            "formattable-uuid.json",
            "Formattable",
            "Uuid",
            validateFormat: false,
            optionalAsNullable: false,
            useImplicitOperatorString: false);

        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse("\"d2719e59-7eb0-4d20-871b-ef843f1fcbb4\"");
        IJsonValue instance = JsonSchemaBuilderDriver.CreateInstance(generatedType, document.RootElement.Clone());

        Assert.AreEqual(
            "d2719e597eb04d20871bef843f1fcbb4",
            ((IFormattable)instance).ToString("N", CultureInfo.InvariantCulture));
        Assert.AreEqual(
            "\"d2719e59-7eb0-4d20-871b-ef843f1fcbb4\"",
            ((IFormattable)instance).ToString(null, CultureInfo.InvariantCulture));
    }
}