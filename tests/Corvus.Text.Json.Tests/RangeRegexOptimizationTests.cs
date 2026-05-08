// <copyright file="RangeRegexOptimizationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests.RegexOptimization;

/// <summary>
/// Tests for range regex pattern optimization with the <c>pattern</c> keyword.
/// The pattern <c>^.{1,256}$</c> matches strings between 1 and 256 characters long.
/// </summary>
[TestCategory("RegexRange")]
[TestClass]
public class RangePatternKeyword
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "string",
            "pattern": "^.{1,10}$"
        }
        """;

    private static Fixture? s_fixture;
    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        s_fixture = new Fixture();
        await s_fixture.InitializeAsync();
    }

    [ClassCleanup]
    public static void ClassCleanupMethod()
    {
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    [DataRow("\"a\"")]
    [DataRow("\"hello\"")]
    [DataRow("\"1234567890\"")]
    public void StringWithinRangeIsAccepted(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    public void EmptyStringIsRejected()
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance("\"\"");
        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void StringExceedingMaxIsRejected()
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance("\"12345678901\"");
        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("42")]
    [DataRow("null")]
    public void NonStringValueIsRejected(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsFalse(instance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\custom\\range-pattern-1-10.json",
                Schema,
                "Corvus.Text.Json.Tests.RegexOptimization",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

/// <summary>
/// Tests for range regex with larger bounds matching the Vercel benchmark pattern.
/// </summary>
[TestCategory("RegexRange")]
[TestClass]
public class RangePatternKeywordLarge
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "string",
            "pattern": "^.{1,256}$"
        }
        """;

    private static Fixture? s_fixture;
    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        s_fixture = new Fixture();
        await s_fixture.InitializeAsync();
    }

    [ClassCleanup]
    public static void ClassCleanupMethod()
    {
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    [DataRow("\"a\"")]
    [DataRow("\"hello world\"")]
    public void StringWithinRangeIsAccepted(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    public void EmptyStringIsRejected()
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance("\"\"");
        Assert.IsFalse(instance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\custom\\range-pattern-1-256.json",
                Schema,
                "Corvus.Text.Json.Tests.RegexOptimization",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

/// <summary>
/// Tests for range regex with Unicode codepoints (ensures rune counting, not byte counting).
/// </summary>
[TestCategory("RegexRange")]
[TestClass]
public class RangePatternKeywordUnicode
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "string",
            "pattern": "^.{1,3}$"
        }
        """;

    private static Fixture? s_fixture;
    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        s_fixture = new Fixture();
        await s_fixture.InitializeAsync();
    }

    [ClassCleanup]
    public static void ClassCleanupMethod()
    {
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    [DataRow("\"\\u00e9\"")]
    [DataRow("\"ab\"")]
    [DataRow("\"\\u00e9\\u00e8\\u00ea\"")]
    public void UnicodeStringsWithinRuneRangeAreAccepted(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("\"abcd\"")]
    [DataRow("\"\\u00e9\\u00e8\\u00ea\\u00eb\"")]
    public void StringsExceedingRuneRangeAreRejected(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    public void EmptyStringIsRejected()
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance("\"\"");
        Assert.IsFalse(instance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\custom\\range-pattern-1-3-unicode.json",
                Schema,
                "Corvus.Text.Json.Tests.RegexOptimization",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

/// <summary>
/// Tests for range regex optimization with the <c>patternProperties</c> keyword.
/// </summary>
[TestCategory("RegexRange")]
[TestClass]
public class RangePatternProperties
{
    private const string Schema = """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "patternProperties": {
                "^.{1,5}$": { "type": "string" }
            },
            "additionalProperties": false
        }
        """;

    private static Fixture? s_fixture;
    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        s_fixture = new Fixture();
        await s_fixture.InitializeAsync();
    }

    [ClassCleanup]
    public static void ClassCleanupMethod()
    {
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    [DataRow("""{"a": "v"}""")]
    [DataRow("""{"abc": "v", "de": "w"}""")]
    [DataRow("""{}""")]
    public void ObjectWithShortKeyNamesIsAccepted(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("""{"a": "v", "toolong": "w"}""")]
    public void ObjectWithTooLongKeyNameIsRejected(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsFalse(instance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\custom\\range-patternproperties-1-5.json",
                Schema,
                "Corvus.Text.Json.Tests.RegexOptimization",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
