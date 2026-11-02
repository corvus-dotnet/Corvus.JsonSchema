// <copyright file="MalformedStringTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Corvus.Json;
using Corvus.Json.CodeGeneration.CSharp;
using Corvus.Json.Specs.Tests.Infrastructure;
using Drivers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.HandWritten;

/// <summary>
/// A string that is not well-formed text (an escape of half of a surrogate pair, or bytes that are not UTF-8) is
/// accepted when the document is parsed. Generated code reports it not valid against a keyword that reads its
/// characters, and never throws (https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/1021).
/// </summary>
[TestClass]
public class MalformedStringTests
{
    private static readonly Dictionary<string, byte[]> Malformed = new()
    {
        ["lone high surrogate escape"] = Encoding.UTF8.GetBytes("\"\\ud800\""),
        ["lone high surrogate escape at the end"] = Encoding.UTF8.GetBytes("\"a\\ud83d\""),
        ["lone low surrogate escape"] = Encoding.UTF8.GetBytes("\"\\udc00\""),
        ["low surrogate escape after a character"] = Encoding.UTF8.GetBytes("\"a\\udc00\""),
        ["high surrogate escape before a character"] = Encoding.UTF8.GetBytes("\"\\ud800x\""),
        ["high surrogate escape before another escape"] = Encoding.UTF8.GetBytes("\"\\ud800\\u0061\""),
        ["a surrogate encoded in UTF-8"] = [0x22, 0xED, 0xA0, 0x80, 0x22],
        ["a lead byte without its continuation"] = [0x22, 0xC3, 0x28, 0x22],
        ["a byte that is never UTF-8"] = [0x22, 0xFF, 0x22],
        ["a byte that is never UTF-8 after an escape"] = [0x22, 0x5C, 0x6E, 0xFF, 0x22],
    };

    [TestMethod]
    [DataRow("""{"pattern": "^.$"}""", false, GeneratedRegexEngine.Ecma262)]
    [DataRow("""{"pattern": "^.$"}""", false, GeneratedRegexEngine.DotNet)]
    [DataRow("""{"pattern": "^(.)\\1?$"}""", false, GeneratedRegexEngine.Ecma262)]
    [DataRow("""{"pattern": "^(.)\\1?$"}""", false, GeneratedRegexEngine.DotNet)]
    [DataRow("""{"minLength": 1}""", false, GeneratedRegexEngine.Ecma262)]
    [DataRow("""{"maxLength": 5}""", false, GeneratedRegexEngine.Ecma262)]
    [DataRow("""{"enum": ["a", "b"]}""", false, GeneratedRegexEngine.Ecma262)]
    [DataRow("""{"const": "a"}""", false, GeneratedRegexEngine.Ecma262)]
    [DataRow("""{"format": "email"}""", false, GeneratedRegexEngine.Ecma262)]
    [DataRow("""{"format": "idn-email"}""", false, GeneratedRegexEngine.Ecma262)]
    [DataRow("""{"format": "date"}""", false, GeneratedRegexEngine.Ecma262)]
    [DataRow("""{"format": "uri"}""", false, GeneratedRegexEngine.Ecma262)]
    [DataRow("""{"type": "string"}""", true, GeneratedRegexEngine.Ecma262)]
    public async Task AKeywordThatReadsAMalformedStringReportsItNotValid(string schema, bool expected, GeneratedRegexEngine regexEngine)
    {
        Type type = await GenerateType(schema, regexEngine);
        AssertEveryInstance(type, static json => json, expected);
    }

    [TestMethod]
    [DataRow("""{"propertyNames": {"pattern": "^a"}}""", false)]
    [DataRow("""{"propertyNames": {"maxLength": 3}}""", false)]
    [DataRow("""{"patternProperties": {"^a": {}}, "additionalProperties": false}""", false)]
    [DataRow("""{"properties": {"a": {}}, "additionalProperties": false}""", false)]
    public async Task AKeywordThatReadsAMalformedPropertyNameReportsItNotValid(string schema, bool expected)
    {
        Type type = await GenerateType(schema, GeneratedRegexEngine.Ecma262);
        AssertEveryInstance(type, static json => [.. "{"u8.ToArray(), .. json, .. ": 1}"u8.ToArray()], expected);
    }

    [TestMethod]
    public async Task PropertiesAloneNeverThrowsForAMalformedName()
    {
        // System.Text.Json unescapes a name it compares in a property lookup when the lengths allow a match, which
        // throws for an escape of half of a surrogate pair; V4 then reports the object not valid. Otherwise the name
        // matches no property, and the object is valid. Either way, validation gives a result.
        Type type = await GenerateType("""{"properties": {"a": {}}}""", GeneratedRegexEngine.Ecma262);
        foreach (KeyValuePair<string, byte[]> entry in Malformed)
        {
            string name = entry.Key;
            byte[] json = entry.Value;
            try
            {
                Validate(type, [.. "{"u8.ToArray(), .. json, .. ": 1}"u8.ToArray()]);
            }
            catch (Exception ex)
            {
                Assert.Fail($"{name}: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    [TestMethod]
    public async Task WellFormedTextIsReadAsItAlwaysWas()
    {
        Type type = await GenerateType("""{"pattern": "^.$", "minLength": 1, "maxLength": 1}""", GeneratedRegexEngine.Ecma262);
        foreach (string json in new[] { "\"\u00e9\"", "\"\\u00e9\"", "\"\\ud83d\\ude00\"", "\"\ud83d\ude00\"" })
        {
            Assert.IsTrue(Validate(type, Encoding.UTF8.GetBytes(json)), json);
        }
    }

    private static async Task<Type> GenerateType(string schema, GeneratedRegexEngine regexEngine)
    {
        using JsonSchemaBuilderDriver driver = DriverFactory.CreateDraft202012Driver();
        return await driver.GenerateTypeForVirtualFile(
            schema,
            "malformedString.json",
            "MalformedString",
            "Scenario",
            validateFormat: true,
            optionalAsNullable: false,
            useImplicitOperatorString: false,
            regexEngine: regexEngine);
    }

    private static void AssertEveryInstance(Type type, Func<byte[], byte[]> wrap, bool expected)
    {
        var failures = new List<string>();
        foreach (KeyValuePair<string, byte[]> entry in Malformed)
        {
            string name = entry.Key;
            byte[] json = entry.Value;
            try
            {
                if (Validate(type, wrap(json)) != expected)
                {
                    failures.Add($"{name}: not {expected}");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"{name}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    private static bool Validate(Type type, byte[] json)
    {
        using var doc = JsonDocument.Parse(json);
        IJsonValue instance = JsonSchemaBuilderDriver.CreateInstance(type, doc.RootElement);
        bool flag = instance.Validate(ValidationContext.ValidContext, ValidationLevel.Flag).IsValid;
        bool detailed = instance.Validate(ValidationContext.ValidContext, ValidationLevel.Detailed).IsValid;
        Assert.AreEqual(flag, detailed, "flag and detailed validation agree");
        return flag;
    }
}