using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JsonSchemaTestSuite.Draft202012.UnevaluatedProperties;

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"unevaluatedProperties\": true\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithValidUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithInvalidUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"fo\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"unevaluatedProperties\": {\r\n                \"type\": \"string\",\r\n                \"minLength\": 3\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"unevaluatedProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"unevaluatedProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"patternProperties\": {\r\n                \"^foo\": { \"type\": \"string\" }\r\n            },\r\n            \"unevaluatedProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoAdditionalProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithAdditionalProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"object\",\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"additionalProperties\": true,\r\n            \"unevaluatedProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoAdditionalProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithAdditionalProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"type\": \"object\",\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"additionalProperties\": {\"type\": \"string\"},\r\n            \"unevaluatedProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoAdditionalProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithAdditionalProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\",\r\n                    \"baz\": \"baz\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"allOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"bar\": { \"type\": \"string\" }\r\n                    }\r\n                }\r\n            ],\r\n            \"unevaluatedProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoAdditionalProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithAdditionalProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\",\r\n                    \"baz\": \"baz\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"allOf\": [\r\n              {\r\n                  \"patternProperties\": {\r\n                      \"^bar\": { \"type\": \"string\" }\r\n                  }\r\n              }\r\n            ],\r\n            \"unevaluatedProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoAdditionalProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithAdditionalProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"allOf\": [\r\n                {\r\n                    \"additionalProperties\": true\r\n                }\r\n            ],\r\n            \"unevaluatedProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoNestedUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithNestedUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"allOf\": [\r\n                {\r\n                    \"unevaluatedProperties\": true\r\n                }\r\n            ],\r\n            \"unevaluatedProperties\": {\r\n                \"type\": \"string\",\r\n                \"maxLength\": 2\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestWhenOneMatchesAndHasNoUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWhenOneMatchesAndHasUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\",\r\n                    \"baz\": \"not-baz\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWhenTwoMatchAndHasNoUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\",\r\n                    \"baz\": \"baz\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWhenTwoMatchAndHasUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\",\r\n                    \"baz\": \"baz\",\r\n                    \"quux\": \"not-quux\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"anyOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"bar\": { \"const\": \"bar\" }\r\n                    },\r\n                    \"required\": [\"bar\"]\r\n                },\r\n                {\r\n                    \"properties\": {\r\n                        \"baz\": { \"const\": \"baz\" }\r\n                    },\r\n                    \"required\": [\"baz\"]\r\n                },\r\n                {\r\n                    \"properties\": {\r\n                        \"quux\": { \"const\": \"quux\" }\r\n                    },\r\n                    \"required\": [\"quux\"]\r\n                }\r\n            ],\r\n            \"unevaluatedProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\",\r\n                    \"quux\": \"quux\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"oneOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"bar\": { \"const\": \"bar\" }\r\n                    },\r\n                    \"required\": [\"bar\"]\r\n                },\r\n                {\r\n                    \"properties\": {\r\n                        \"baz\": { \"const\": \"baz\" }\r\n                    },\r\n                    \"required\": [\"baz\"]\r\n                }\r\n            ],\r\n            \"unevaluatedProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"not\": {\r\n                \"not\": {\r\n                    \"properties\": {\r\n                        \"bar\": { \"const\": \"bar\" }\r\n                    },\r\n                    \"required\": [\"bar\"]\r\n                }\r\n            },\r\n            \"unevaluatedProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestWhenIfIsTrueAndHasNoUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"then\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWhenIfIsTrueAndHasUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"then\",\r\n                    \"bar\": \"bar\",\r\n                    \"baz\": \"baz\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWhenIfIsFalseAndHasNoUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"baz\": \"baz\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWhenIfIsFalseAndHasUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"else\",\r\n                    \"baz\": \"baz\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"if\": {\r\n                \"properties\": {\r\n                    \"foo\": { \"const\": \"then\" }\r\n                },\r\n                \"required\": [\"foo\"]\r\n            },\r\n            \"then\": {\r\n                \"properties\": {\r\n                    \"bar\": { \"type\": \"string\" }\r\n                },\r\n                \"required\": [\"bar\"]\r\n            },\r\n            \"else\": {\r\n                \"properties\": {\r\n                    \"baz\": { \"type\": \"string\" }\r\n                },\r\n                \"required\": [\"baz\"]\r\n            },\r\n            \"unevaluatedProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestWhenIfIsTrueAndHasNoUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"then\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWhenIfIsTrueAndHasUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"then\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWhenIfIsFalseAndHasNoUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"baz\": \"baz\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWhenIfIsFalseAndHasUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"else\",\r\n                    \"baz\": \"baz\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"if\": {\r\n                \"properties\": {\r\n                    \"foo\": { \"const\": \"then\" }\r\n                },\r\n                \"required\": [\"foo\"]\r\n            },\r\n            \"else\": {\r\n                \"properties\": {\r\n                    \"baz\": { \"type\": \"string\" }\r\n                },\r\n                \"required\": [\"baz\"]\r\n            },\r\n            \"unevaluatedProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestWhenIfIsTrueAndHasNoUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"then\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWhenIfIsTrueAndHasUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"then\",\r\n                    \"bar\": \"bar\",\r\n                    \"baz\": \"baz\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWhenIfIsFalseAndHasNoUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"baz\": \"baz\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWhenIfIsFalseAndHasUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"else\",\r\n                    \"baz\": \"baz\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"if\": {\r\n                \"properties\": {\r\n                    \"foo\": { \"const\": \"then\" }\r\n                },\r\n                \"required\": [\"foo\"]\r\n            },\r\n            \"then\": {\r\n                \"properties\": {\r\n                    \"bar\": { \"type\": \"string\" }\r\n                },\r\n                \"required\": [\"bar\"]\r\n            },\r\n            \"unevaluatedProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"dependentSchemas\": {\r\n                \"foo\": {\r\n                    \"properties\": {\r\n                        \"bar\": { \"const\": \"bar\" }\r\n                    },\r\n                    \"required\": [\"bar\"]\r\n                }\r\n            },\r\n            \"unevaluatedProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"allOf\": [true],\r\n            \"unevaluatedProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\",\r\n                    \"baz\": \"baz\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$ref\": \"#/$defs/bar\",\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"unevaluatedProperties\": false,\r\n            \"$defs\": {\r\n                \"bar\": {\r\n                    \"properties\": {\r\n                        \"bar\": { \"type\": \"string\" }\r\n                    }\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\",\r\n                    \"baz\": \"baz\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"unevaluatedProperties\": false,\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"$ref\": \"#/$defs/bar\",\r\n            \"$defs\": {\r\n                \"bar\": {\r\n                    \"properties\": {\r\n                        \"bar\": { \"type\": \"string\" }\r\n                    }\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
[TestClass]
public class SuiteUnevaluatedPropertiesWithDynamicRef
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
    public void TestWithNoUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\",\r\n                    \"baz\": \"baz\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"https://example.com/unevaluated-properties-with-dynamic-ref/derived\",\r\n\r\n            \"$ref\": \"./baseSchema\",\r\n\r\n            \"$defs\": {\r\n                \"derived\": {\r\n                    \"$dynamicAnchor\": \"addons\",\r\n                    \"properties\": {\r\n                        \"bar\": { \"type\": \"string\" }\r\n                    }\r\n                },\r\n                \"baseSchema\": {\r\n                    \"$id\": \"./baseSchema\",\r\n\r\n                    \"$comment\": \"unevaluatedProperties comes first so it's more likely to catch bugs with implementations that are sensitive to keyword ordering\",\r\n                    \"unevaluatedProperties\": false,\r\n                    \"properties\": {\r\n                        \"foo\": { \"type\": \"string\" }\r\n                    },\r\n                    \"$dynamicRef\": \"#addons\",\r\n\r\n                    \"$defs\": {\r\n                        \"defaultAddons\": {\r\n                            \"$comment\": \"Needed to satisfy the bookending requirement\",\r\n                            \"$dynamicAnchor\": \"addons\"\r\n                        }\r\n                    }\r\n                }\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestAlwaysFails()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": 1\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"allOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"foo\": true\r\n                    }\r\n                },\r\n                {\r\n                    \"unevaluatedProperties\": false\r\n                }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestAlwaysFails()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": 1\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"allOf\": [\r\n                {\r\n                    \"unevaluatedProperties\": false\r\n                },\r\n                {\r\n                    \"properties\": {\r\n                        \"foo\": true\r\n                    }\r\n                }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoNestedUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithNestedUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"allOf\": [\r\n                {\r\n                    \"unevaluatedProperties\": true\r\n                }\r\n            ],\r\n            \"unevaluatedProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoNestedUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithNestedUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"allOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"foo\": { \"type\": \"string\" }\r\n                    },\r\n                    \"unevaluatedProperties\": true\r\n                }\r\n            ],\r\n            \"unevaluatedProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoNestedUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithNestedUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"allOf\": [\r\n                {\r\n                    \"unevaluatedProperties\": false\r\n                }\r\n            ],\r\n            \"unevaluatedProperties\": true\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoNestedUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithNestedUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"allOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"foo\": { \"type\": \"string\" }\r\n                    },\r\n                    \"unevaluatedProperties\": false\r\n                }\r\n            ],\r\n            \"unevaluatedProperties\": true\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoNestedUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithNestedUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"allOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"foo\": { \"type\": \"string\" }\r\n                    },\r\n                    \"unevaluatedProperties\": true\r\n                },\r\n                {\r\n                    \"unevaluatedProperties\": false\r\n                }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithNoNestedUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestWithNestedUnevaluatedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"allOf\": [\r\n                {\r\n                    \"unevaluatedProperties\": true\r\n                },\r\n                {\r\n                    \"properties\": {\r\n                        \"foo\": { \"type\": \"string\" }\r\n                    },\r\n                    \"unevaluatedProperties\": false\r\n                }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestNoExtraProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": {\r\n                        \"bar\": \"test\"\r\n                    }\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUncleKeywordEvaluationIsNotSignificant()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": {\r\n                        \"bar\": \"test\",\r\n                        \"faz\": \"test\"\r\n                    }\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"foo\": {\r\n                    \"properties\": {\r\n                        \"bar\": {\r\n                            \"type\": \"string\"\r\n                        }\r\n                    },\r\n                    \"unevaluatedProperties\": false\r\n                  }\r\n            },\r\n            \"anyOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"foo\": {\r\n                            \"properties\": {\r\n                                \"faz\": {\r\n                                    \"type\": \"string\"\r\n                                }\r\n                            }\r\n                        }\r\n                    }\r\n                }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestBaseCaseBothPropertiesPresent()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": 1,\r\n                    \"bar\": 1\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInPlaceApplicatorSiblingsBarIsMissing()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": 1\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInPlaceApplicatorSiblingsFooIsMissing()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"bar\": 1\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"allOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"foo\": true\r\n                    },\r\n                    \"unevaluatedProperties\": false\r\n                }\r\n            ],\r\n            \"anyOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"bar\": true\r\n                    }\r\n                }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestBaseCaseBothPropertiesPresent()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": 1,\r\n                    \"bar\": 1\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInPlaceApplicatorSiblingsBarIsMissing()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": 1\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInPlaceApplicatorSiblingsFooIsMissing()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"bar\": 1\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"allOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"foo\": true\r\n                    }\r\n                }\r\n            ],\r\n            \"anyOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"bar\": true\r\n                    },\r\n                    \"unevaluatedProperties\": false\r\n                }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestEmptyIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestSingleIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"x\": {} }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUnevaluatedOn1stLevelIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"x\": {}, \"y\": {} }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestNestedIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"x\": { \"x\": {} } }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUnevaluatedOn2ndLevelIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"x\": { \"x\": {}, \"y\": {} } }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestDeepNestedIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"x\": { \"x\": { \"x\": {} } } }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUnevaluatedOn3rdLevelIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"x\": { \"x\": { \"x\": {}, \"y\": {} } } }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"x\": { \"$ref\": \"#\" }\r\n            },\r\n            \"unevaluatedProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestEmptyIsInvalidNoXOrY()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAAndBAreInvalidNoXOrY()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"a\": 1, \"b\": 1 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestXAndYAreInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"x\": 1, \"y\": 1 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAAndXAreValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"a\": 1, \"x\": 1 }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAAndYAreValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"a\": 1, \"y\": 1 }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAAndBAndXAreValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"a\": 1, \"b\": 1, \"x\": 1 }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAAndBAndYAreValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"a\": 1, \"b\": 1, \"y\": 1 }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAAndBAndXAndYAreInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"a\": 1, \"b\": 1, \"x\": 1, \"y\": 1 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$defs\": {\r\n                \"one\": {\r\n                    \"properties\": { \"a\": true }\r\n                },\r\n                \"two\": {\r\n                    \"required\": [\"x\"],\r\n                    \"properties\": { \"x\": true }\r\n                }\r\n            },\r\n            \"allOf\": [\r\n                { \"$ref\": \"#/$defs/one\" },\r\n                { \"properties\": { \"b\": true } },\r\n                {\r\n                    \"oneOf\": [\r\n                        { \"$ref\": \"#/$defs/two\" },\r\n                        {\r\n                            \"required\": [\"y\"],\r\n                            \"properties\": { \"y\": true }\r\n                        }\r\n                    ]\r\n                }\r\n            ],\r\n            \"unevaluatedProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestEmptyIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"a\": 1 }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestBIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"b\": 1 }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestCIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"c\": 1 }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestDIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"d\": 1 }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestABIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"a\": 1, \"b\": 1 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestACIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"a\": 1, \"c\": 1 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestADIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"a\": 1, \"d\": 1 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestBCIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"b\": 1, \"c\": 1 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestBDIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"b\": 1, \"d\": 1 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestCDIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"c\": 1, \"d\": 1 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestXxIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"xx\": 1 }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestXxFooxIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"xx\": 1, \"foox\": 1 }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestXxFooIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"xx\": 1, \"foo\": 1 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestXxAIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"xx\": 1, \"a\": 1 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestXxBIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"xx\": 1, \"b\": 1 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestXxCIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"xx\": 1, \"c\": 1 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestXxDIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"xx\": 1, \"d\": 1 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAllIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"all\": 1 }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAllFooIsValid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"all\": 1, \"foo\": 1 }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestAllAIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"all\": 1, \"a\": 1 }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$defs\": {\r\n                \"one\": {\r\n                    \"oneOf\": [\r\n                        { \"$ref\": \"#/$defs/two\" },\r\n                        { \"required\": [\"b\"], \"properties\": { \"b\": true } },\r\n                        { \"required\": [\"xx\"], \"patternProperties\": { \"x\": true } },\r\n                        { \"required\": [\"all\"], \"unevaluatedProperties\": true }\r\n                    ]\r\n                },\r\n                \"two\": {\r\n                    \"oneOf\": [\r\n                        { \"required\": [\"c\"], \"properties\": { \"c\": true } },\r\n                        { \"required\": [\"d\"], \"properties\": { \"d\": true } }\r\n                    ]\r\n                }\r\n            },\r\n            \"oneOf\": [\r\n                { \"$ref\": \"#/$defs/one\" },\r\n                { \"required\": [\"a\"], \"properties\": { \"a\": true } }\r\n            ],\r\n            \"unevaluatedProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestIgnoresBooleans()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("true");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIgnoresIntegers()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("123");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIgnoresFloats()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("1.0");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIgnoresArrays()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("[]");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIgnoresStrings()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("\"foo\"");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestIgnoresNull()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("null");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"unevaluatedProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestAllowsNullValuedProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": null}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"unevaluatedProperties\": {\r\n                \"type\": \"null\"\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestAllowsOnlyNumberProperties()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"a\": 1}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestStringPropertyIsInvalid()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"a\": \"b\"}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"propertyNames\": {\"maxLength\": 1},\r\n            \"unevaluatedProperties\": {\r\n                \"type\": \"number\"\r\n            }\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestValidInCaseIfIsEvaluated()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": \"a\"\r\n                }");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestInvalidInCaseIfIsEvaluated()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"bar\": \"a\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"if\": {\r\n                \"patternProperties\": {\r\n                    \"foo\": {\r\n                        \"type\": \"string\"\r\n                    }\r\n                }\r\n            },\r\n            \"unevaluatedProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestUnevaluatedPropertiesDoesnTConsiderDependentSchemas()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"foo\": \"\"}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUnevaluatedPropertiesDoesnTSeeBarWhenFoo2IsAbsent()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\"bar\": \"\"}");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    [TestMethod]
    public void TestUnevaluatedPropertiesSeesBarWhenFoo2IsPresent()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{ \"foo2\": \"\", \"bar\": \"\"}");
        Assert.IsTrue(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\"foo2\": {}},\r\n            \"dependentSchemas\": {\r\n                \"foo\" : {},\r\n                \"foo2\": {\r\n                    \"properties\": {\r\n                        \"bar\":{}\r\n                    }\r\n                }\r\n            },\r\n            \"unevaluatedProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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

[TestCategory("Draft202012")]
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
        (s_fixture as IDisposable)?.Dispose();
        s_fixture = null;
    }

    [TestMethod]
    public void TestWithAnUnevaluatedPropertyThatExistsAtAnotherLocation()
    {
        var dynamicInstance = s_fixture!.DynamicJsonType.ParseInstance("{\r\n                    \"foo\": { \"bar\": \"foo\" },\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.IsFalse(dynamicInstance.EvaluateSchema());
    }

    public class Fixture
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2020-12\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"foo\": {\r\n                    \"properties\": {\r\n                        \"bar\": { \"type\": \"string\" }\r\n                    }\r\n                }\r\n            },\r\n            \"unevaluatedProperties\": false\r\n        }",
                "JsonSchemaTestSuite.Draft202012.UnevaluatedProperties",
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
