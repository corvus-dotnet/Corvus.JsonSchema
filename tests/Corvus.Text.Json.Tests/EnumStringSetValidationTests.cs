// <copyright file="EnumStringSetValidationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests.EnumStringSetValidation;

/// <summary>
/// Tests for string enum validation with 4+ values, which triggers the EnumStringSet hash-based
/// code path instead of sequential SequenceEqual comparisons.
/// </summary>
[TestCategory("EnumStringSet")]
[TestClass]
public class StringEnumWith5Values
{
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
    [DataRow("\"alpha\"")]
    [DataRow("\"bravo\"")]
    [DataRow("\"charlie\"")]
    [DataRow("\"delta\"")]
    [DataRow("\"echo\"")]
    public void ValidEnumValueIsAccepted(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("\"foxtrot\"")]
    [DataRow("\"ALPHA\"")]
    [DataRow("\"Alpha\"")]
    [DataRow("\"\"")]
    [DataRow("\"alph\"")]
    [DataRow("\"alphaa\"")]
    public void InvalidStringValueIsRejected(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("42")]
    [DataRow("true")]
    [DataRow("null")]
    [DataRow("[]")]
    [DataRow("{}")]
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
                "tests\\custom\\enum-string-set-5.json",
                "{\"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"string\", \"enum\": [\"alpha\", \"bravo\", \"charlie\", \"delta\", \"echo\"]}",
                "Corvus.Text.Json.Tests.EnumStringSetValidation",
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
/// Tests for string enum validation at the exact threshold boundary (4 values triggers hash set).
/// </summary>
[TestCategory("EnumStringSet")]
[TestClass]
public class StringEnumWith4Values
{
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
    [DataRow("\"red\"")]
    [DataRow("\"green\"")]
    [DataRow("\"blue\"")]
    [DataRow("\"yellow\"")]
    public void ValidEnumValueIsAccepted(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("\"orange\"")]
    [DataRow("\"RED\"")]
    public void InvalidStringValueIsRejected(string json)
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
                "tests\\custom\\enum-string-set-4.json",
                "{\"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"string\", \"enum\": [\"red\", \"green\", \"blue\", \"yellow\"]}",
                "Corvus.Text.Json.Tests.EnumStringSetValidation",
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
/// Tests for heterogeneous enum with 4+ string values plus other types.
/// The string portion should use EnumStringSet, other types use standard matching.
/// </summary>
[TestCategory("EnumStringSet")]
[TestClass]
public class HeterogeneousEnumWith5StringValues
{
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
    [DataRow("\"alpha\"")]
    [DataRow("\"bravo\"")]
    [DataRow("\"charlie\"")]
    [DataRow("\"delta\"")]
    [DataRow("\"echo\"")]
    public void ValidStringEnumValueIsAccepted(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("42")]
    [DataRow("true")]
    [DataRow("null")]
    public void ValidNonStringEnumValueIsAccepted(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("\"foxtrot\"")]
    [DataRow("\"\"")]
    public void InvalidStringValueIsRejected(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("99")]
    [DataRow("false")]
    [DataRow("[]")]
    public void InvalidNonStringValueIsRejected(string json)
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
                "tests\\custom\\enum-heterogeneous-5-strings.json",
                "{\"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"enum\": [\"alpha\", \"bravo\", \"charlie\", \"delta\", \"echo\", 42, true, null]}",
                "Corvus.Text.Json.Tests.EnumStringSetValidation",
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
/// Tests for string enum with values of varying lengths to exercise the hash function's
/// different code paths (short keys ≤7 bytes vs longer keys).
/// </summary>
[TestCategory("EnumStringSet")]
[TestClass]
public class StringEnumWithVaryingLengths
{
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
    [DataRow("\"ab\"")]
    [DataRow("\"abc\"")]
    [DataRow("\"abcd\"")]
    [DataRow("\"abcde\"")]
    [DataRow("\"abcdef\"")]
    [DataRow("\"abcdefg\"")]
    [DataRow("\"abcdefgh\"")]
    [DataRow("\"a-longer-string-value\"")]
    public void ValidEnumValueIsAccepted(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("\"\"")]
    [DataRow("\"x\"")]
    [DataRow("\"abcdefgx\"")]
    [DataRow("\"a-longer-string-valuex\"")]
    public void InvalidStringValueIsRejected(string json)
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
                "tests\\custom\\enum-string-set-varying-lengths.json",
                "{\"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"string\", \"enum\": [\"a\", \"ab\", \"abc\", \"abcd\", \"abcde\", \"abcdef\", \"abcdefg\", \"abcdefgh\", \"a-longer-string-value\"]}",
                "Corvus.Text.Json.Tests.EnumStringSetValidation",
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
/// Tests for integer enum validation with 4+ values, which triggers the switch-based
/// code path instead of sequential AreEqualNormalizedJsonNumbers comparisons.
/// </summary>
[TestCategory("EnumIntegerSwitch")]
[TestClass]
public class IntegerEnumWith5Values
{
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
    [DataRow("1")]
    [DataRow("2")]
    [DataRow("3")]
    [DataRow("4")]
    [DataRow("5")]
    public void ValidIntegerEnumValueIsAccepted(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("1.0")]
    [DataRow("2.0")]
    [DataRow("5.0")]
    public void EquivalentDecimalFormIsAccepted(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("6")]
    [DataRow("-1")]
    [DataRow("1.5")]
    [DataRow("99")]
    public void InvalidNumericValueIsRejected(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsFalse(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("\"1\"")]
    [DataRow("true")]
    [DataRow("null")]
    public void NonNumericValueIsRejected(string json)
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
                "tests\\custom\\enum-integer-switch-5.json",
                "{\"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"integer\", \"enum\": [1, 2, 3, 4, 5]}",
                "Corvus.Text.Json.Tests.EnumStringSetValidation",
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
/// Tests for integer enum with negative values and zero to exercise switch edge cases.
/// </summary>
[TestCategory("EnumIntegerSwitch")]
[TestClass]
public class IntegerEnumWithNegativeValues
{
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
    [DataRow("-100")]
    [DataRow("-1")]
    [DataRow("0")]
    [DataRow("1")]
    [DataRow("100")]
    public void ValidEnumValueIsAccepted(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("-101")]
    [DataRow("-2")]
    [DataRow("2")]
    [DataRow("99")]
    public void InvalidNumericValueIsRejected(string json)
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
                "tests\\custom\\enum-integer-switch-negatives.json",
                "{\"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"enum\": [-100, -1, 0, 1, 100]}",
                "Corvus.Text.Json.Tests.EnumStringSetValidation",
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
/// Tests for heterogeneous enum with both integer and non-integer numeric values.
/// Non-integer values prevent the switch optimization; sequential comparison is used instead.
/// </summary>
[TestCategory("EnumIntegerSwitch")]
[TestClass]
public class MixedNumericEnum
{
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
    [DataRow("1")]
    [DataRow("2")]
    [DataRow("3.14")]
    [DataRow("4")]
    [DataRow("2.718")]
    public void ValidEnumValueIsAccepted(string json)
    {
        var instance = s_fixture!.DynamicJsonType.ParseInstance(json);
        Assert.IsTrue(instance.EvaluateSchema());
    }

    [TestMethod]
    [DataRow("5")]
    [DataRow("3.15")]
    [DataRow("0")]
    public void InvalidNumericValueIsRejected(string json)
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
                "tests\\custom\\enum-mixed-numeric.json",
                "{\"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"type\": \"number\", \"enum\": [1, 2, 3.14, 4, 2.718]}",
                "Corvus.Text.Json.Tests.EnumStringSetValidation",
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
