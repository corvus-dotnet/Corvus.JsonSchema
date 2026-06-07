using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties;

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesTrue
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWithUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"unevaluatedProperties\": true\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesSchema
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWithValidUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWithInvalidUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"fo\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"unevaluatedProperties\": {\n                \"type\": \"string\",\n                \"minLength\": 3\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesFalse
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWithUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"unevaluatedProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesWithAdjacentProperties
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWithUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\",\n                    \"bar\": \"bar\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"properties\": {\n                \"foo\": { \"type\": \"string\" }\n            },\n            \"unevaluatedProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesWithAdjacentPatternProperties
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWithUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\",\n                    \"bar\": \"bar\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"patternProperties\": {\n                \"^foo\": { \"type\": \"string\" }\n            },\n            \"unevaluatedProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesWithAdjacentBoolAdditionalProperties
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoAdditionalProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWithAdditionalProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\",\n                    \"bar\": \"bar\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"properties\": {\n                \"foo\": { \"type\": \"string\" }\n            },\n            \"additionalProperties\": true,\n            \"unevaluatedProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesWithAdjacentNonBoolAdditionalProperties
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoAdditionalProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWithAdditionalProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\",\n                    \"bar\": \"bar\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"properties\": {\n                \"foo\": { \"type\": \"string\" }\n            },\n            \"additionalProperties\": {\"type\": \"string\"},\n            \"unevaluatedProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesWithNestedProperties
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoAdditionalProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\",\n                    \"bar\": \"bar\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWithAdditionalProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\",\n                    \"bar\": \"bar\",\n                    \"baz\": \"baz\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"properties\": {\n                \"foo\": { \"type\": \"string\" }\n            },\n            \"allOf\": [\n                {\n                    \"properties\": {\n                        \"bar\": { \"type\": \"string\" }\n                    }\n                }\n            ],\n            \"unevaluatedProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesWithNestedPatternProperties
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoAdditionalProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\",\n                    \"bar\": \"bar\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWithAdditionalProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\",\n                    \"bar\": \"bar\",\n                    \"baz\": \"baz\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"properties\": {\n                \"foo\": { \"type\": \"string\" }\n            },\n            \"allOf\": [\n              {\n                  \"patternProperties\": {\n                      \"^bar\": { \"type\": \"string\" }\n                  }\n              }\n            ],\n            \"unevaluatedProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesWithNestedAdditionalProperties
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoAdditionalProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWithAdditionalProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\",\n                    \"bar\": \"bar\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"properties\": {\n                \"foo\": { \"type\": \"string\" }\n            },\n            \"allOf\": [\n                {\n                    \"additionalProperties\": true\n                }\n            ],\n            \"unevaluatedProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesWithNestedUnevaluatedProperties
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoNestedUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWithNestedUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\",\n                    \"bar\": \"bar\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"properties\": {\n                \"foo\": { \"type\": \"string\" }\n            },\n            \"allOf\": [\n                {\n                    \"unevaluatedProperties\": true\n                }\n            ],\n            \"unevaluatedProperties\": {\n                \"type\": \"string\",\n                \"maxLength\": 2\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesWithAnyOf
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWhenOneMatchesAndHasNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\",\n                    \"bar\": \"bar\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWhenOneMatchesAndHasUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\",\n                    \"bar\": \"bar\",\n                    \"baz\": \"not-baz\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWhenTwoMatchAndHasNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\",\n                    \"bar\": \"bar\",\n                    \"baz\": \"baz\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWhenTwoMatchAndHasUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\",\n                    \"bar\": \"bar\",\n                    \"baz\": \"baz\",\n                    \"quux\": \"not-quux\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"properties\": {\n                \"foo\": { \"type\": \"string\" }\n            },\n            \"anyOf\": [\n                {\n                    \"properties\": {\n                        \"bar\": { \"const\": \"bar\" }\n                    },\n                    \"required\": [\"bar\"]\n                },\n                {\n                    \"properties\": {\n                        \"baz\": { \"const\": \"baz\" }\n                    },\n                    \"required\": [\"baz\"]\n                },\n                {\n                    \"properties\": {\n                        \"quux\": { \"const\": \"quux\" }\n                    },\n                    \"required\": [\"quux\"]\n                }\n            ],\n            \"unevaluatedProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesWithOneOf
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\",\n                    \"bar\": \"bar\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWithUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\",\n                    \"bar\": \"bar\",\n                    \"quux\": \"quux\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"properties\": {\n                \"foo\": { \"type\": \"string\" }\n            },\n            \"oneOf\": [\n                {\n                    \"properties\": {\n                        \"bar\": { \"const\": \"bar\" }\n                    },\n                    \"required\": [\"bar\"]\n                },\n                {\n                    \"properties\": {\n                        \"baz\": { \"const\": \"baz\" }\n                    },\n                    \"required\": [\"baz\"]\n                }\n            ],\n            \"unevaluatedProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesWithNot
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\",\n                    \"bar\": \"bar\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"properties\": {\n                \"foo\": { \"type\": \"string\" }\n            },\n            \"not\": {\n                \"not\": {\n                    \"properties\": {\n                        \"bar\": { \"const\": \"bar\" }\n                    },\n                    \"required\": [\"bar\"]\n                }\n            },\n            \"unevaluatedProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesWithIfThenElse
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWhenIfIsTrueAndHasNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"then\",\n                    \"bar\": \"bar\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWhenIfIsTrueAndHasUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"then\",\n                    \"bar\": \"bar\",\n                    \"baz\": \"baz\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWhenIfIsFalseAndHasNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"baz\": \"baz\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWhenIfIsFalseAndHasUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"else\",\n                    \"baz\": \"baz\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"if\": {\n                \"properties\": {\n                    \"foo\": { \"const\": \"then\" }\n                },\n                \"required\": [\"foo\"]\n            },\n            \"then\": {\n                \"properties\": {\n                    \"bar\": { \"type\": \"string\" }\n                },\n                \"required\": [\"bar\"]\n            },\n            \"else\": {\n                \"properties\": {\n                    \"baz\": { \"type\": \"string\" }\n                },\n                \"required\": [\"baz\"]\n            },\n            \"unevaluatedProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesWithIfThenElseThenNotDefined
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWhenIfIsTrueAndHasNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"then\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWhenIfIsTrueAndHasUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"then\",\n                    \"bar\": \"bar\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWhenIfIsFalseAndHasNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"baz\": \"baz\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWhenIfIsFalseAndHasUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"else\",\n                    \"baz\": \"baz\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"if\": {\n                \"properties\": {\n                    \"foo\": { \"const\": \"then\" }\n                },\n                \"required\": [\"foo\"]\n            },\n            \"else\": {\n                \"properties\": {\n                    \"baz\": { \"type\": \"string\" }\n                },\n                \"required\": [\"baz\"]\n            },\n            \"unevaluatedProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesWithIfThenElseElseNotDefined
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWhenIfIsTrueAndHasNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"then\",\n                    \"bar\": \"bar\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWhenIfIsTrueAndHasUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"then\",\n                    \"bar\": \"bar\",\n                    \"baz\": \"baz\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWhenIfIsFalseAndHasNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"baz\": \"baz\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWhenIfIsFalseAndHasUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"else\",\n                    \"baz\": \"baz\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"if\": {\n                \"properties\": {\n                    \"foo\": { \"const\": \"then\" }\n                },\n                \"required\": [\"foo\"]\n            },\n            \"then\": {\n                \"properties\": {\n                    \"bar\": { \"type\": \"string\" }\n                },\n                \"required\": [\"bar\"]\n            },\n            \"unevaluatedProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesWithDependentSchemas
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\",\n                    \"bar\": \"bar\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWithUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"bar\": \"bar\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"properties\": {\n                \"foo\": { \"type\": \"string\" }\n            },\n            \"dependentSchemas\": {\n                \"foo\": {\n                    \"properties\": {\n                        \"bar\": { \"const\": \"bar\" }\n                    },\n                    \"required\": [\"bar\"]\n                }\n            },\n            \"unevaluatedProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesWithBooleanSchemas
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWithUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"bar\": \"bar\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"properties\": {\n                \"foo\": { \"type\": \"string\" }\n            },\n            \"allOf\": [true],\n            \"unevaluatedProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesWithRef
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\",\n                    \"bar\": \"bar\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWithUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\",\n                    \"bar\": \"bar\",\n                    \"baz\": \"baz\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"$ref\": \"#/$defs/bar\",\n            \"properties\": {\n                \"foo\": { \"type\": \"string\" }\n            },\n            \"unevaluatedProperties\": false,\n            \"$defs\": {\n                \"bar\": {\n                    \"properties\": {\n                        \"bar\": { \"type\": \"string\" }\n                    }\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesBeforeRef
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\",\n                    \"bar\": \"bar\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWithUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\",\n                    \"bar\": \"bar\",\n                    \"baz\": \"baz\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"unevaluatedProperties\": false,\n            \"properties\": {\n                \"foo\": { \"type\": \"string\" }\n            },\n            \"$ref\": \"#/$defs/bar\",\n            \"$defs\": {\n                \"bar\": {\n                    \"properties\": {\n                        \"bar\": { \"type\": \"string\" }\n                    }\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesWithRecursiveRef
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"name\": \"a\",\n                    \"node\": 1,\n                    \"branches\": {\n                      \"name\": \"b\",\n                      \"node\": 2\n                    }\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWithUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"name\": \"a\",\n                    \"node\": 1,\n                    \"branches\": {\n                      \"foo\": \"b\",\n                      \"node\": 2\n                    }\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$id\": \"https://example.com/unevaluated-properties-with-recursive-ref/extended-tree\",\n\n            \"$recursiveAnchor\": true,\n\n            \"$ref\": \"./tree\",\n            \"properties\": {\n                \"name\": { \"type\": \"string\" }\n            },\n\n            \"$defs\": {\n                \"tree\": {\n                    \"$id\": \"./tree\",\n                    \"$recursiveAnchor\": true,\n\n                    \"type\": \"object\",\n                    \"properties\": {\n                        \"node\": true,\n                        \"branches\": {\n                            \"$comment\": \"unevaluatedProperties comes first so it's more likely to bugs errors with implementations that are sensitive to keyword ordering\",\n                            \"unevaluatedProperties\": false,\n                            \"$recursiveRef\": \"#\"\n                        }\n                    },\n                    \"required\": [\"node\"]\n                }\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesCanTSeeInsideCousins
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestAlwaysFails()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": 1\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"allOf\": [\n                {\n                    \"properties\": {\n                        \"foo\": true\n                    }\n                },\n                {\n                    \"unevaluatedProperties\": false\n                }\n            ]\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesCanTSeeInsideCousinsReverseOrder
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestAlwaysFails()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": 1\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"allOf\": [\n                {\n                    \"unevaluatedProperties\": false\n                },\n                {\n                    \"properties\": {\n                        \"foo\": true\n                    }\n                }\n            ]\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteNestedUnevaluatedPropertiesOuterFalseInnerTruePropertiesOutside
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoNestedUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWithNestedUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\",\n                    \"bar\": \"bar\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"properties\": {\n                \"foo\": { \"type\": \"string\" }\n            },\n            \"allOf\": [\n                {\n                    \"unevaluatedProperties\": true\n                }\n            ],\n            \"unevaluatedProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteNestedUnevaluatedPropertiesOuterFalseInnerTruePropertiesInside
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoNestedUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWithNestedUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\",\n                    \"bar\": \"bar\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"allOf\": [\n                {\n                    \"properties\": {\n                        \"foo\": { \"type\": \"string\" }\n                    },\n                    \"unevaluatedProperties\": true\n                }\n            ],\n            \"unevaluatedProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteNestedUnevaluatedPropertiesOuterTrueInnerFalsePropertiesOutside
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoNestedUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWithNestedUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\",\n                    \"bar\": \"bar\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"properties\": {\n                \"foo\": { \"type\": \"string\" }\n            },\n            \"allOf\": [\n                {\n                    \"unevaluatedProperties\": false\n                }\n            ],\n            \"unevaluatedProperties\": true\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteNestedUnevaluatedPropertiesOuterTrueInnerFalsePropertiesInside
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoNestedUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWithNestedUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\",\n                    \"bar\": \"bar\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"allOf\": [\n                {\n                    \"properties\": {\n                        \"foo\": { \"type\": \"string\" }\n                    },\n                    \"unevaluatedProperties\": false\n                }\n            ],\n            \"unevaluatedProperties\": true\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteCousinUnevaluatedPropertiesTrueAndFalseTrueWithProperties
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoNestedUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWithNestedUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\",\n                    \"bar\": \"bar\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"allOf\": [\n                {\n                    \"properties\": {\n                        \"foo\": { \"type\": \"string\" }\n                    },\n                    \"unevaluatedProperties\": true\n                },\n                {\n                    \"unevaluatedProperties\": false\n                }\n            ]\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteCousinUnevaluatedPropertiesTrueAndFalseFalseWithProperties
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoNestedUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWithNestedUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"foo\",\n                    \"bar\": \"bar\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"allOf\": [\n                {\n                    \"unevaluatedProperties\": true\n                },\n                {\n                    \"properties\": {\n                        \"foo\": { \"type\": \"string\" }\n                    },\n                    \"unevaluatedProperties\": false\n                }\n            ]\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuitePropertyIsEvaluatedInAnUncleSchemaToUnevaluatedProperties
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestNoExtraProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": {\n                        \"bar\": \"test\"\n                    }\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUncleKeywordEvaluationIsNotSignificant()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": {\n                        \"bar\": \"test\",\n                        \"faz\": \"test\"\n                    }\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"properties\": {\n                \"foo\": {\n                    \"type\": \"object\",\n                    \"properties\": {\n                        \"bar\": {\n                            \"type\": \"string\"\n                        }\n                    },\n                    \"unevaluatedProperties\": false\n                  }\n            },\n            \"anyOf\": [\n                {\n                    \"properties\": {\n                        \"foo\": {\n                            \"properties\": {\n                                \"faz\": {\n                                    \"type\": \"string\"\n                                }\n                            }\n                        }\n                    }\n                }\n            ]\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteInPlaceApplicatorSiblingsAllOfHasUnevaluated
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestBaseCaseBothPropertiesPresent()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": 1,\n                    \"bar\": 1\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInPlaceApplicatorSiblingsBarIsMissing()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": 1\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInPlaceApplicatorSiblingsFooIsMissing()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"bar\": 1\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"allOf\": [\n                {\n                    \"properties\": {\n                        \"foo\": true\n                    },\n                    \"unevaluatedProperties\": false\n                }\n            ],\n            \"anyOf\": [\n                {\n                    \"properties\": {\n                        \"bar\": true\n                    }\n                }\n            ]\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteInPlaceApplicatorSiblingsAnyOfHasUnevaluated
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestBaseCaseBothPropertiesPresent()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": 1,\n                    \"bar\": 1\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInPlaceApplicatorSiblingsBarIsMissing()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": 1\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInPlaceApplicatorSiblingsFooIsMissing()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"bar\": 1\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"allOf\": [\n                {\n                    \"properties\": {\n                        \"foo\": true\n                    }\n                }\n            ],\n            \"anyOf\": [\n                {\n                    \"properties\": {\n                        \"bar\": true\n                    },\n                    \"unevaluatedProperties\": false\n                }\n            ]\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesSingleCyclicRef
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestEmptyIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestSingleIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"x\": {} }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUnevaluatedOn1stLevelIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"x\": {}, \"y\": {} }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestNestedIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"x\": { \"x\": {} } }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUnevaluatedOn2ndLevelIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"x\": { \"x\": {}, \"y\": {} } }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestDeepNestedIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"x\": { \"x\": { \"x\": {} } } }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUnevaluatedOn3rdLevelIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"x\": { \"x\": { \"x\": {}, \"y\": {} } } }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"type\": \"object\",\n            \"properties\": {\n                \"x\": { \"$ref\": \"#\" }\n            },\n            \"unevaluatedProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesRefInsideAllOfOneOf
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestEmptyIsInvalidNoXOrY()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAAndBAreInvalidNoXOrY()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"a\": 1, \"b\": 1 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestXAndYAreInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"x\": 1, \"y\": 1 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAAndXAreValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"a\": 1, \"x\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAAndYAreValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"a\": 1, \"y\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAAndBAndXAreValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"a\": 1, \"b\": 1, \"x\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAAndBAndYAreValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"a\": 1, \"b\": 1, \"y\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAAndBAndXAndYAreInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"a\": 1, \"b\": 1, \"x\": 1, \"y\": 1 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$defs\": {\n                \"one\": {\n                    \"properties\": { \"a\": true }\n                },\n                \"two\": {\n                    \"required\": [\"x\"],\n                    \"properties\": { \"x\": true }\n                }\n            },\n            \"allOf\": [\n                { \"$ref\": \"#/$defs/one\" },\n                { \"properties\": { \"b\": true } },\n                {\n                    \"oneOf\": [\n                        { \"$ref\": \"#/$defs/two\" },\n                        {\n                            \"required\": [\"y\"],\n                            \"properties\": { \"y\": true }\n                        }\n                    ]\n                }\n            ],\n            \"unevaluatedProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteDynamicEvalationInsideNestedRefs
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestEmptyIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"a\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestBIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"b\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestCIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"c\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestDIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"d\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestABIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"a\": 1, \"b\": 1 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestACIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"a\": 1, \"c\": 1 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestADIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"a\": 1, \"d\": 1 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestBCIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"b\": 1, \"c\": 1 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestBDIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"b\": 1, \"d\": 1 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestCDIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"c\": 1, \"d\": 1 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestXxIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"xx\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestXxFooxIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"xx\": 1, \"foox\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestXxFooIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"xx\": 1, \"foo\": 1 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestXxAIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"xx\": 1, \"a\": 1 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestXxBIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"xx\": 1, \"b\": 1 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestXxCIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"xx\": 1, \"c\": 1 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestXxDIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"xx\": 1, \"d\": 1 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAllIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"all\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAllFooIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"all\": 1, \"foo\": 1 }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestAllAIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"all\": 1, \"a\": 1 }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"$defs\": {\n                \"one\": {\n                    \"oneOf\": [\n                        { \"$ref\": \"#/$defs/two\" },\n                        { \"required\": [\"b\"], \"properties\": { \"b\": true } },\n                        { \"required\": [\"xx\"], \"patternProperties\": { \"x\": true } },\n                        { \"required\": [\"all\"], \"unevaluatedProperties\": true }\n                    ]\n                },\n                \"two\": {\n                    \"oneOf\": [\n                        { \"required\": [\"c\"], \"properties\": { \"c\": true } },\n                        { \"required\": [\"d\"], \"properties\": { \"d\": true } }\n                    ]\n                }\n            },\n            \"oneOf\": [\n                { \"$ref\": \"#/$defs/one\" },\n                { \"required\": [\"a\"], \"properties\": { \"a\": true } }\n            ],\n            \"unevaluatedProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteNonObjectInstancesAreValid
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestIgnoresBooleans()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("true");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIgnoresIntegers()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("123");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIgnoresFloats()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1.0");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIgnoresArrays()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIgnoresStrings()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestIgnoresNull()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"unevaluatedProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesWithNullValuedInstanceProperties
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestAllowsNullValuedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": null}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"unevaluatedProperties\": {\n                \"type\": \"null\"\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesNotAffectedByPropertyNames
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestAllowsOnlyNumberProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\": 1}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestStringPropertyIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\": \"b\"}");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"propertyNames\": {\"maxLength\": 1},\n            \"unevaluatedProperties\": {\n                \"type\": \"number\"\n            }\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteUnevaluatedPropertiesCanSeeAnnotationsFromIfWithoutThenAndElse
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestValidInCaseIfIsEvaluated()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": \"a\"\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestInvalidInCaseIfIsEvaluated()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"bar\": \"a\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"if\": {\n                \"patternProperties\": {\n                    \"foo\": {\n                        \"type\": \"string\"\n                    }\n                }\n            },\n            \"unevaluatedProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteDependentSchemasWithUnevaluatedProperties
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestUnevaluatedPropertiesDoesnTConsiderDependentSchemas()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": \"\"}");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUnevaluatedPropertiesDoesnTSeeBarWhenFoo2IsAbsent()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"bar\": \"\"}");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestUnevaluatedPropertiesSeesBarWhenFoo2IsPresent()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo2\": \"\", \"bar\": \"\"}");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"properties\": {\"foo2\": {}},\n            \"dependentSchemas\": {\n                \"foo\" : {},\n                \"foo2\": {\n                    \"properties\": {\n                        \"bar\":{}\n                    }\n                }\n            },\n            \"unevaluatedProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteEvaluatedPropertiesCollectionNeedsToConsiderInstanceLocation
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithAnUnevaluatedPropertyThatExistsAtAnotherLocation()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": { \"bar\": \"foo\" },\n                    \"bar\": \"bar\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"properties\": {\n                \"foo\": {\n                    \"properties\": {\n                        \"bar\": { \"type\": \"string\" }\n                    }\n                }\n            },\n            \"unevaluatedProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteEvaluatedPropertiesCollectionNeedsToConsiderInstanceLocationWithPatternProperties
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithOnlyTheNestedEvaluatedProperty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": { \"bar\": \"foo\" }\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWithAnUnevaluatedPropertyThatExistsAtAnotherLocation()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": { \"bar\": \"foo\" },\n                    \"bar\": \"bar\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"properties\": {\n                \"foo\": {\n                    \"patternProperties\": {\n                        \"^bar$\": { \"type\": \"string\" }\n                    }\n                }\n            },\n            \"unevaluatedProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[TestCategory("Draft201909")]
[TestClass]
public class SuiteEvaluatedPropertiesCollectionNeedsToConsiderInstanceLocationWithAdditionalProperties
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
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithOnlyTheNestedEvaluatedProperty()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": { \"bar\": \"foo\" }\n                }");
        Assert.IsTrue(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    [TestMethod]
    public void TestWithAnUnevaluatedPropertyThatExistsAtAnotherLocation()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\n                    \"foo\": { \"bar\": \"foo\" },\n                    \"bar\": \"bar\"\n                }");
        Assert.IsFalse(s_fixture!.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests/draft2019-09/unevaluatedProperties.json",
                "{\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\n            \"properties\": {\n                \"foo\": {\n                    \"additionalProperties\": { \"type\": \"string\" }\n                }\n            },\n            \"unevaluatedProperties\": false\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
