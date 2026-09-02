// <copyright file="NestedRequiredDoubleEvaluationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Corvus.Text.Json.EvaluatorTestSuite.Tests;

/// <summary>
/// Repro tests for https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/949.
/// A property declared <c>required</c> both in a schema and in the schema it
/// references via <c>$ref</c> must validate once per scope against the same
/// instance, and both evaluations must succeed when the property is present.
/// </summary>
[TestClass]
public class NestedRequiredDoubleEvaluationTests
{
    /// <summary>
    /// Minimal shape: the child schema declares <c>required: [type]</c> and also
    /// references a base schema that declares <c>required: [type]</c>.
    /// </summary>
    private const string MinimalOverlappingRequiredSchema =
        """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "$ref": "https://example.com/base",
            "type": "object",
            "required": ["type"],
            "properties": {
                "type": { "const": "IpConnection" }
            },
            "$defs": {
                "base": {
                    "$id": "https://example.com/base",
                    "type": "object",
                    "required": ["type"],
                    "properties": {
                        "type": { "type": "string" }
                    }
                }
            }
        }
        """;

    /// <summary>
    /// The reporter's shape: a wrapper schema whose property references a child
    /// schema, which itself references a base schema; both child and base declare
    /// <c>required</c> for the same property, and the child adds one of its own.
    /// </summary>
    private const string WrappedOverlappingRequiredSchema =
        """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "properties": {
                "connection": { "$ref": "https://example.com/child" }
            },
            "$defs": {
                "child": {
                    "$id": "https://example.com/child",
                    "type": "object",
                    "$ref": "https://example.com/base",
                    "required": ["hostname", "type"],
                    "properties": {
                        "type": { "const": "IpConnection" },
                        "hostname": { "type": "string" },
                        "port": { "type": "integer" }
                    }
                },
                "base": {
                    "$id": "https://example.com/base",
                    "type": "object",
                    "required": ["type"],
                    "properties": {
                        "type": { "type": "string" },
                        "timeout_ms": { "type": "integer", "minimum": 0 },
                        "cooldown_ms": { "type": "integer", "minimum": 0 },
                        "cooldown_id": { "type": "string" }
                    }
                }
            }
        }
        """;

    /// <summary>
    /// The reporter's child schema also constrains unevaluated properties, which switches
    /// the generated object validation onto the evaluated-property tracking path.
    /// </summary>
    private const string UnevaluatedPropertiesOverlappingRequiredSchema =
        """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "properties": {
                "connection": { "$ref": "https://example.com/child" }
            },
            "$defs": {
                "child": {
                    "$id": "https://example.com/child",
                    "type": "object",
                    "$ref": "https://example.com/base",
                    "unevaluatedProperties": false,
                    "required": ["hostname", "type"],
                    "properties": {
                        "type": { "const": "IpConnection" },
                        "hostname": { "type": "string" },
                        "port": { "type": "integer" }
                    }
                },
                "base": {
                    "$id": "https://example.com/base",
                    "type": "object",
                    "required": ["type"],
                    "properties": {
                        "type": { "type": "string" },
                        "timeout_ms": { "type": "integer", "minimum": 0 },
                        "cooldown_ms": { "type": "integer", "minimum": 0 },
                        "cooldown_id": { "type": "string" }
                    }
                }
            }
        }
        """;

    [TestMethod]
    public async Task OverlappingRequired_UnevaluatedPropertiesRefChain_PresentPropertiesAreAccepted()
    {
        CompiledEvaluator evaluator = await GenerateAsync(UnevaluatedPropertiesOverlappingRequiredSchema, "issue949UnevaluatedOverlappingRequired.json");

        AssertEvaluates(
            evaluator,
            /*lang=json*/ """{"connection": {"type": "IpConnection", "hostname": "10.0.0.228", "cooldown_id": "evcc", "port": 7070, "timeout_ms": 2000, "cooldown_ms": 1250}}""",
            expected: true,
            "All required properties are present in both scopes, so it must be valid.");
        AssertEvaluates(
            evaluator,
            /*lang=json*/ """{"connection": {"hostname": "10.0.0.228"}}""",
            expected: false,
            "'type' is absent from the connection object, so it must be invalid.");
    }

    [TestMethod]
    public async Task OverlappingRequired_MinimalRefChain_PresentPropertyIsAccepted()
    {
        CompiledEvaluator evaluator = await GenerateAsync(MinimalOverlappingRequiredSchema, "issue949MinimalOverlappingRequired.json");

        AssertEvaluates(evaluator, /*lang=json*/ """{"type": "IpConnection"}""", expected: true, "'type' is present and satisfies both scopes' constraints, so it must be valid.");
        AssertEvaluates(evaluator, /*lang=json*/ """{}""", expected: false, "'type' is absent, so it must be invalid.");
        AssertEvaluates(evaluator, /*lang=json*/ """{"type": "SerialConnection"}""", expected: false, "'type' does not match the const, so it must be invalid.");
    }

    [TestMethod]
    public async Task OverlappingRequired_WrappedRefChain_PresentPropertiesAreAccepted()
    {
        CompiledEvaluator evaluator = await GenerateAsync(WrappedOverlappingRequiredSchema, "issue949WrappedOverlappingRequired.json");

        AssertEvaluates(
            evaluator,
            /*lang=json*/ """{"connection": {"type": "IpConnection", "hostname": "10.0.0.228", "cooldown_id": "evcc", "port": 7070, "timeout_ms": 2000, "cooldown_ms": 1250}}""",
            expected: true,
            "All required properties are present in both scopes, so it must be valid.");
        AssertEvaluates(
            evaluator,
            /*lang=json*/ """{"connection": {"hostname": "10.0.0.228"}}""",
            expected: false,
            "'type' is absent from the connection object, so it must be invalid.");
        AssertEvaluates(
            evaluator,
            /*lang=json*/ """{"connection": {"type": "IpConnection"}}""",
            expected: false,
            "'hostname' is absent from the connection object, so it must be invalid.");
    }

    /// <summary>
    /// False-acceptance twin: the base declares 'type' with a constraint the instance
    /// violates (maxLength), but is not required there. The hoisted property subschema
    /// must still be applied even though the child also declares 'type'.
    /// </summary>
    private const string OverlappingPropertyStricterBaseSchema =
        """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "properties": {
                "connection": { "$ref": "https://example.com/child" }
            },
            "$defs": {
                "child": {
                    "$id": "https://example.com/child",
                    "type": "object",
                    "$ref": "https://example.com/base",
                    "required": ["hostname", "type"],
                    "properties": {
                        "type": { "const": "IpConnection" },
                        "hostname": { "type": "string" },
                        "port": { "type": "integer" }
                    }
                },
                "base": {
                    "$id": "https://example.com/base",
                    "type": "object",
                    "properties": {
                        "type": { "type": "string", "maxLength": 5 },
                        "timeout_ms": { "type": "integer", "minimum": 0 },
                        "cooldown_ms": { "type": "integer", "minimum": 0 },
                        "cooldown_id": { "type": "string" }
                    }
                }
            }
        }
        """;

    /// <summary>
    /// False-acceptance repro: 'type' is declared in both scopes but required in neither
    /// beyond hostname; whichever duplicate map entry loses the lookup has its property
    /// subschema silently skipped, so the child's const must still be enforced.
    /// </summary>
    private const string OverlappingPropertyConstNotRequiredSchema =
        """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "properties": {
                "connection": { "$ref": "https://example.com/child" }
            },
            "$defs": {
                "child": {
                    "$id": "https://example.com/child",
                    "type": "object",
                    "$ref": "https://example.com/base",
                    "required": ["hostname"],
                    "properties": {
                        "type": { "const": "IpConnection" },
                        "hostname": { "type": "string" },
                        "port": { "type": "integer" }
                    }
                },
                "base": {
                    "$id": "https://example.com/base",
                    "type": "object",
                    "properties": {
                        "type": { "type": "string", "maxLength": 5 },
                        "timeout_ms": { "type": "integer", "minimum": 0 },
                        "cooldown_ms": { "type": "integer", "minimum": 0 },
                        "cooldown_id": { "type": "string" }
                    }
                }
            }
        }
        """;

    [TestMethod]
    public async Task GeneratedTypes_WrappedRefChain_ChildConstOnOverlappingProperty_StillApplies()
    {
        await AssertGeneratedTypeEvaluates(
            OverlappingPropertyConstNotRequiredSchema,
            "issue949OverlappingPropertyConstNotRequired.json",
            /*lang=json*/ """{"connection": {"type": "abc", "hostname": "10.0.0.228", "port": 7070}}""",
            expected: false,
            "'abc' violates the child schema's const for 'type', so it must be invalid.");
    }

    [TestMethod]
    public async Task GeneratedTypes_MinimalRefChain_OverlappingRequired_PresentPropertyIsAccepted()
    {
        await AssertGeneratedTypeEvaluates(
            MinimalOverlappingRequiredSchema,
            "issue949MinimalOverlappingRequired.json",
            /*lang=json*/ """{"type": "IpConnection"}""",
            expected: true,
            "'type' is present and satisfies both scopes' constraints, so it must be valid.");
    }

    [TestMethod]
    public async Task GeneratedTypes_WrappedRefChain_OverlappingRequired_PresentPropertiesAreAccepted()
    {
        await AssertGeneratedTypeEvaluates(
            WrappedOverlappingRequiredSchema,
            "issue949WrappedOverlappingRequired.json",
            /*lang=json*/ """{"connection": {"type": "IpConnection", "hostname": "10.0.0.228", "cooldown_id": "evcc", "port": 7070, "timeout_ms": 2000, "cooldown_ms": 1250}}""",
            expected: true,
            "All required properties are present in both scopes, so it must be valid.");
    }

    [TestMethod]
    public async Task GeneratedTypes_WrappedRefChain_UnevaluatedProperties_PresentPropertiesAreAccepted()
    {
        await AssertGeneratedTypeEvaluates(
            UnevaluatedPropertiesOverlappingRequiredSchema,
            "issue949UnevaluatedOverlappingRequired.json",
            /*lang=json*/ """{"connection": {"type": "IpConnection", "hostname": "10.0.0.228", "cooldown_id": "evcc", "port": 7070, "timeout_ms": 2000, "cooldown_ms": 1250}}""",
            expected: true,
            "All required properties are present in both scopes, so it must be valid.");
    }

    [TestMethod]
    public async Task GeneratedTypes_WrappedRefChain_MissingRequiredProperty_IsRejected()
    {
        await AssertGeneratedTypeEvaluates(
            WrappedOverlappingRequiredSchema,
            "issue949WrappedOverlappingRequired.json",
            /*lang=json*/ """{"connection": {"hostname": "10.0.0.228"}}""",
            expected: false,
            "'type' is absent from the connection object, so it must be invalid.");
    }

    [TestMethod]
    public async Task GeneratedTypes_WrappedRefChain_HoistedPropertySubschemaStillApplies()
    {
        await AssertGeneratedTypeEvaluates(
            OverlappingPropertyStricterBaseSchema,
            "issue949OverlappingPropertyStricterBase.json",
            /*lang=json*/ """{"connection": {"type": "IpConnection", "hostname": "10.0.0.228", "port": 7070}}""",
            expected: false,
            "'IpConnection' violates the base schema's maxLength for 'type', so it must be invalid.");
    }

    /// <summary>
    /// Standalone loop (no local object keywords on the parent): two allOf branches share a
    /// property name, so the shared hoisted map must dispatch that name to both branch bodies.
    /// </summary>
    private const string StandaloneCrossBranchCollisionSchema =
        """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "allOf": [
                { "$ref": "https://example.com/a" },
                { "$ref": "https://example.com/b" }
            ],
            "$defs": {
                "a": {
                    "$id": "https://example.com/a",
                    "type": "object",
                    "required": ["common"],
                    "properties": {
                        "common": { "type": "string" },
                        "p1": { "type": "integer" },
                        "p2": { "type": "integer" }
                    }
                },
                "b": {
                    "$id": "https://example.com/b",
                    "type": "object",
                    "properties": {
                        "common": { "type": "string", "maxLength": 5 },
                        "p3": { "type": "string" }
                    }
                }
            }
        }
        """;

    /// <summary>
    /// Standalone loop with two hoisted keyword groups ($ref and allOf): the shared map's
    /// indices must line up with each group's own switch, or one group dispatches into the
    /// wrong case body.
    /// </summary>
    private const string StandaloneTwoKeywordGroupsSchema =
        """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "$ref": "https://example.com/a",
            "allOf": [
                { "$ref": "https://example.com/b" }
            ],
            "$defs": {
                "a": {
                    "$id": "https://example.com/a",
                    "type": "object",
                    "required": ["common"],
                    "properties": {
                        "common": { "type": "string" },
                        "p1": { "type": "integer" },
                        "p2": { "type": "integer" }
                    }
                },
                "b": {
                    "$id": "https://example.com/b",
                    "type": "object",
                    "properties": {
                        "common": { "type": "string", "maxLength": 5 },
                        "p3": { "type": "string" }
                    }
                }
            }
        }
        """;

    [TestMethod]
    public async Task GeneratedTypes_StandaloneLoop_CrossBranchCollision_BothBranchesApply()
    {
        await AssertGeneratedTypeEvaluates(
            StandaloneCrossBranchCollisionSchema,
            "issue949StandaloneCrossBranchCollision.json",
            /*lang=json*/ """{"common": "ok"}""",
            expected: true,
            "'common' is present and satisfies both branches, so it must be valid.");

        await AssertGeneratedTypeEvaluates(
            StandaloneCrossBranchCollisionSchema,
            "issue949StandaloneCrossBranchCollision.json",
            /*lang=json*/ """{"common": "toolong!!"}""",
            expected: false,
            "'toolong!!' violates the second branch's maxLength for 'common', so it must be invalid.");

        await AssertGeneratedTypeEvaluates(
            StandaloneCrossBranchCollisionSchema,
            "issue949StandaloneCrossBranchCollision.json",
            /*lang=json*/ """{"p1": 1}""",
            expected: false,
            "'common' is absent, so the first branch's required must fail.");
    }

    [TestMethod]
    public async Task GeneratedTypes_StandaloneLoop_TwoKeywordGroups_DispatchTheRightBodies()
    {
        await AssertGeneratedTypeEvaluates(
            StandaloneTwoKeywordGroupsSchema,
            "issue949StandaloneTwoKeywordGroups.json",
            /*lang=json*/ """{"common": "ok", "p1": 42, "p2": 7, "p3": "hi"}""",
            expected: true,
            "Every property satisfies its own subschema in both groups, so it must be valid.");

        await AssertGeneratedTypeEvaluates(
            StandaloneTwoKeywordGroupsSchema,
            "issue949StandaloneTwoKeywordGroups.json",
            /*lang=json*/ """{"common": "ok", "p1": 42, "p3": 12}""",
            expected: false,
            "'p3' violates the allOf branch's string type, so it must be invalid.");
    }

    private static async Task AssertGeneratedTypeEvaluates(string schemaText, string virtualFilename, string instanceJson, bool expected, string message)
    {
        Corvus.Text.Json.Validator.DynamicJsonType type = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
            virtualFilename,
            schemaText,
            "Corvus.Text.Json.EvaluatorTestSuite.Tests.NestedRequiredDoubleEvaluationTypes",
            "./someFakePath",
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            validateFormat: false,
            optionalAsNullable: false,
            useImplicitOperatorString: false,
            addExplicitUsings: true,
            Assembly.GetExecutingAssembly());

        Corvus.Text.Json.Validator.DynamicJsonElement instance = type.ParseInstance(instanceJson);
        Assert.AreEqual(expected, instance.EvaluateSchema(), $"[Generated types] {message}");

        using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.CreateUnrented(JsonSchemaResultsLevel.Verbose);
        Assert.AreEqual(expected, instance.EvaluateSchema(collector), $"[Generated types, verbose collector] {message}");
    }

    private static void AssertEvaluates(CompiledEvaluator evaluator, string instanceJson, bool expected, string message)
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse(instanceJson);
        Assert.AreEqual(expected, evaluator.Evaluate(doc.RootElement), $"{message} Generated code:{System.Environment.NewLine}{evaluator.GeneratedCode}");

        using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.CreateUnrented(JsonSchemaResultsLevel.Verbose);
        Assert.AreEqual(expected, evaluator.Evaluate(doc.RootElement, collector), $"[With verbose collector] {message} Generated code:{System.Environment.NewLine}{evaluator.GeneratedCode}");
    }

    private static ValueTask<CompiledEvaluator> GenerateAsync(string schemaText, string virtualFilename)
    {
        return TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
            virtualFilename,
            schemaText,
            "Corvus.Text.Json.EvaluatorTestSuite.Tests.NestedRequiredDoubleEvaluation",
            "./someFakePath",
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            validateFormat: false,
            Assembly.GetExecutingAssembly());
    }
}
