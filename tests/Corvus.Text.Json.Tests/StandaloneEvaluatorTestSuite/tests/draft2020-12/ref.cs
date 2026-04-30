using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft202012.Ref;

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteRootPointerRef : IClassFixture<SuiteRootPointerRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRootPointerRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": false}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestRecursiveMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": {\"foo\": false}}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMismatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"bar\": false}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestRecursiveMismatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": {\"bar\": false}}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"foo\": {\"$ref\": \"#\"}\r\n            },\r\n            \"additionalProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteRelativePointerRefToObject : IClassFixture<SuiteRelativePointerRefToObject.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRelativePointerRefToObject(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"bar\": 3}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMismatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"bar\": true}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"foo\": {\"type\": \"integer\"},\r\n                \"bar\": {\"$ref\": \"#/properties/foo\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteRelativePointerRefToArray : IClassFixture<SuiteRelativePointerRefToArray.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRelativePointerRefToArray(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatchArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMismatchArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, \"foo\"]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"prefixItems\": [\r\n                {\"type\": \"integer\"},\r\n                {\"$ref\": \"#/prefixItems/0\"}\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteEscapedPointerRef : IClassFixture<SuiteEscapedPointerRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEscapedPointerRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestSlashInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"slash\": \"aoeu\"}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTildeInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"tilde\": \"aoeu\"}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestPercentInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"percent\": \"aoeu\"}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSlashValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"slash\": 123}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTildeValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"tilde\": 123}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestPercentValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"percent\": 123}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$defs\": {\r\n                \"tilde~field\": {\"type\": \"integer\"},\r\n                \"slash/field\": {\"type\": \"integer\"},\r\n                \"percent%field\": {\"type\": \"integer\"}\r\n            },\r\n            \"properties\": {\r\n                \"tilde\": {\"$ref\": \"#/$defs/tilde~0field\"},\r\n                \"slash\": {\"$ref\": \"#/$defs/slash~1field\"},\r\n                \"percent\": {\"$ref\": \"#/$defs/percent%25field\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteNestedRefs : IClassFixture<SuiteNestedRefs.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteNestedRefs(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNestedRefValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("5");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNestedRefInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$defs\": {\r\n                \"a\": {\"type\": \"integer\"},\r\n                \"b\": {\"$ref\": \"#/$defs/a\"},\r\n                \"c\": {\"$ref\": \"#/$defs/b\"}\r\n            },\r\n            \"$ref\": \"#/$defs/c\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteRefAppliesAlongsideSiblingKeywords : IClassFixture<SuiteRefAppliesAlongsideSiblingKeywords.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefAppliesAlongsideSiblingKeywords(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestRefValidMaxItemsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": [] }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestRefValidMaxItemsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": [1, 2, 3] }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestRefInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": \"string\" }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$defs\": {\r\n                \"reffed\": {\r\n                    \"type\": \"array\"\r\n                }\r\n            },\r\n            \"properties\": {\r\n                \"foo\": {\r\n                    \"$ref\": \"#/$defs/reffed\",\r\n                    \"maxItems\": 2\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteRemoteRefContainingRefsItself : IClassFixture<SuiteRemoteRefContainingRefsItself.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRemoteRefContainingRefsItself(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestRemoteRefValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"minLength\": 1}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestRemoteRefInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"minLength\": -1}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$ref\": \"https://json-schema.org/draft/2020-12/schema\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuitePropertyNamedRefThatIsNotAReference : IClassFixture<SuitePropertyNamedRefThatIsNotAReference.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePropertyNamedRefThatIsNotAReference(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestPropertyNamedRefValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"$ref\": \"a\"}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestPropertyNamedRefInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"$ref\": 2}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"$ref\": {\"type\": \"string\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuitePropertyNamedRefContainingAnActualRef : IClassFixture<SuitePropertyNamedRefContainingAnActualRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePropertyNamedRefContainingAnActualRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestPropertyNamedRefValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"$ref\": \"a\"}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestPropertyNamedRefInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"$ref\": 2}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"$ref\": {\"$ref\": \"#/$defs/is-string\"}\r\n            },\r\n            \"$defs\": {\r\n                \"is-string\": {\r\n                    \"type\": \"string\"\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteRefToBooleanSchemaTrue : IClassFixture<SuiteRefToBooleanSchemaTrue.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefToBooleanSchemaTrue(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnyValueIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$ref\": \"#/$defs/bool\",\r\n            \"$defs\": {\r\n                \"bool\": true\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteRefToBooleanSchemaFalse : IClassFixture<SuiteRefToBooleanSchemaFalse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefToBooleanSchemaFalse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAnyValueIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$ref\": \"#/$defs/bool\",\r\n            \"$defs\": {\r\n                \"bool\": false\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteRecursiveReferencesBetweenSchemas : IClassFixture<SuiteRecursiveReferencesBetweenSchemas.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRecursiveReferencesBetweenSchemas(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidTree()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"meta\": \"root\",\r\n                    \"nodes\": [\r\n                        {\r\n                            \"value\": 1,\r\n                            \"subtree\": {\r\n                                \"meta\": \"child\",\r\n                                \"nodes\": [\r\n                                    {\"value\": 1.1},\r\n                                    {\"value\": 1.2}\r\n                                ]\r\n                            }\r\n                        },\r\n                        {\r\n                            \"value\": 2,\r\n                            \"subtree\": {\r\n                                \"meta\": \"child\",\r\n                                \"nodes\": [\r\n                                    {\"value\": 2.1},\r\n                                    {\"value\": 2.2}\r\n                                ]\r\n                            }\r\n                        }\r\n                    ]\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidTree()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"meta\": \"root\",\r\n                    \"nodes\": [\r\n                        {\r\n                            \"value\": 1,\r\n                            \"subtree\": {\r\n                                \"meta\": \"child\",\r\n                                \"nodes\": [\r\n                                    {\"value\": \"string is invalid\"},\r\n                                    {\"value\": 1.2}\r\n                                ]\r\n                            }\r\n                        },\r\n                        {\r\n                            \"value\": 2,\r\n                            \"subtree\": {\r\n                                \"meta\": \"child\",\r\n                                \"nodes\": [\r\n                                    {\"value\": 2.1},\r\n                                    {\"value\": 2.2}\r\n                                ]\r\n                            }\r\n                        }\r\n                    ]\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"http://localhost:1234/draft2020-12/tree\",\r\n            \"description\": \"tree of nodes\",\r\n            \"type\": \"object\",\r\n            \"properties\": {\r\n                \"meta\": {\"type\": \"string\"},\r\n                \"nodes\": {\r\n                    \"type\": \"array\",\r\n                    \"items\": {\"$ref\": \"node\"}\r\n                }\r\n            },\r\n            \"required\": [\"meta\", \"nodes\"],\r\n            \"$defs\": {\r\n                \"node\": {\r\n                    \"$id\": \"http://localhost:1234/draft2020-12/node\",\r\n                    \"description\": \"node\",\r\n                    \"type\": \"object\",\r\n                    \"properties\": {\r\n                        \"value\": {\"type\": \"number\"},\r\n                        \"subtree\": {\"$ref\": \"tree\"}\r\n                    },\r\n                    \"required\": [\"value\"]\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteRefsWithQuote : IClassFixture<SuiteRefsWithQuote.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefsWithQuote(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestObjectWithNumbersIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\\\"bar\": 1\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestObjectWithStringsIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\\\"bar\": \"1\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"foo\\\"bar\": {\"$ref\": \"#/$defs/foo%22bar\"}\r\n            },\r\n            \"$defs\": {\r\n                \"foo\\\"bar\": {\"type\": \"number\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteRefCreatesNewScopeWhenAdjacentToKeywords : IClassFixture<SuiteRefCreatesNewScopeWhenAdjacentToKeywords.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefCreatesNewScopeWhenAdjacentToKeywords(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestReferencedSubschemaDoesnTSeeAnnotationsFromProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"prop1\": \"match\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$defs\": {\r\n                \"A\": {\r\n                    \"unevaluatedProperties\": false\r\n                }\r\n            },\r\n            \"properties\": {\r\n                \"prop1\": {\r\n                    \"type\": \"string\"\r\n                }\r\n            },\r\n            \"$ref\": \"#/$defs/A\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteNaiveReplacementOfRefWithItsDestinationIsNotCorrect : IClassFixture<SuiteNaiveReplacementOfRefWithItsDestinationIsNotCorrect.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteNaiveReplacementOfRefWithItsDestinationIsNotCorrect(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestDoNotEvaluateTheRefInsideTheEnumMatchingAnyString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"this is a string\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestDoNotEvaluateTheRefInsideTheEnumDefinitionExactMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"type\": \"string\" }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMatchTheEnumExactly()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"$ref\": \"#/$defs/a_string\" }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$defs\": {\r\n                \"a_string\": { \"type\": \"string\" }\r\n            },\r\n            \"enum\": [\r\n                { \"$ref\": \"#/$defs/a_string\" }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteRefsWithRelativeUrisAndDefs : IClassFixture<SuiteRefsWithRelativeUrisAndDefs.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefsWithRelativeUrisAndDefs(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestInvalidOnInnerField()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": {\r\n                        \"bar\": 1\r\n                    },\r\n                    \"bar\": \"a\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidOnOuterField()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": {\r\n                        \"bar\": \"a\"\r\n                    },\r\n                    \"bar\": 1\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidOnBothFields()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": {\r\n                        \"bar\": \"a\"\r\n                    },\r\n                    \"bar\": \"a\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"http://example.com/schema-relative-uri-defs1.json\",\r\n            \"properties\": {\r\n                \"foo\": {\r\n                    \"$id\": \"schema-relative-uri-defs2.json\",\r\n                    \"$defs\": {\r\n                        \"inner\": {\r\n                            \"properties\": {\r\n                                \"bar\": { \"type\": \"string\" }\r\n                            }\r\n                        }\r\n                    },\r\n                    \"$ref\": \"#/$defs/inner\"\r\n                }\r\n            },\r\n            \"$ref\": \"schema-relative-uri-defs2.json\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteRelativeRefsWithAbsoluteUrisAndDefs : IClassFixture<SuiteRelativeRefsWithAbsoluteUrisAndDefs.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRelativeRefsWithAbsoluteUrisAndDefs(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestInvalidOnInnerField()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": {\r\n                        \"bar\": 1\r\n                    },\r\n                    \"bar\": \"a\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidOnOuterField()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": {\r\n                        \"bar\": \"a\"\r\n                    },\r\n                    \"bar\": 1\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidOnBothFields()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": {\r\n                        \"bar\": \"a\"\r\n                    },\r\n                    \"bar\": \"a\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"http://example.com/schema-refs-absolute-uris-defs1.json\",\r\n            \"properties\": {\r\n                \"foo\": {\r\n                    \"$id\": \"http://example.com/schema-refs-absolute-uris-defs2.json\",\r\n                    \"$defs\": {\r\n                        \"inner\": {\r\n                            \"properties\": {\r\n                                \"bar\": { \"type\": \"string\" }\r\n                            }\r\n                        }\r\n                    },\r\n                    \"$ref\": \"#/$defs/inner\"\r\n                }\r\n            },\r\n            \"$ref\": \"schema-refs-absolute-uris-defs2.json\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteIdMustBeResolvedAgainstNearestParentNotJustImmediateParent : IClassFixture<SuiteIdMustBeResolvedAgainstNearestParentNotJustImmediateParent.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteIdMustBeResolvedAgainstNearestParentNotJustImmediateParent(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNumberIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonNumberIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"http://example.com/a.json\",\r\n            \"$defs\": {\r\n                \"x\": {\r\n                    \"$id\": \"http://example.com/b/c.json\",\r\n                    \"not\": {\r\n                        \"$defs\": {\r\n                            \"y\": {\r\n                                \"$id\": \"d.json\",\r\n                                \"type\": \"number\"\r\n                            }\r\n                        }\r\n                    }\r\n                }\r\n            },\r\n            \"allOf\": [\r\n                {\r\n                    \"$ref\": \"http://example.com/b/d.json\"\r\n                }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteOrderOfEvaluationIdAndRef : IClassFixture<SuiteOrderOfEvaluationIdAndRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteOrderOfEvaluationIdAndRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestDataIsValidAgainstFirstDefinition()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("5");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestDataIsInvalidAgainstFirstDefinition()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("50");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$comment\": \"$id must be evaluated before $ref to get the proper $ref destination\",\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"https://example.com/draft2020-12/ref-and-id1/base.json\",\r\n            \"$ref\": \"int.json\",\r\n            \"$defs\": {\r\n                \"bigint\": {\r\n                    \"$comment\": \"canonical uri: https://example.com/ref-and-id1/int.json\",\r\n                    \"$id\": \"int.json\",\r\n                    \"maximum\": 10\r\n                },\r\n                \"smallint\": {\r\n                    \"$comment\": \"canonical uri: https://example.com/ref-and-id1-int.json\",\r\n                    \"$id\": \"/draft2020-12/ref-and-id1-int.json\",\r\n                    \"maximum\": 2\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteOrderOfEvaluationIdAndAnchorAndRef : IClassFixture<SuiteOrderOfEvaluationIdAndAnchorAndRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteOrderOfEvaluationIdAndAnchorAndRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestDataIsValidAgainstFirstDefinition()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("5");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestDataIsInvalidAgainstFirstDefinition()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("50");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$comment\": \"$id must be evaluated before $ref to get the proper $ref destination\",\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"https://example.com/draft2020-12/ref-and-id2/base.json\",\r\n            \"$ref\": \"#bigint\",\r\n            \"$defs\": {\r\n                \"bigint\": {\r\n                    \"$comment\": \"canonical uri: /ref-and-id2/base.json#/$defs/bigint; another valid uri for this location: /ref-and-id2/base.json#bigint\",\r\n                    \"$anchor\": \"bigint\",\r\n                    \"maximum\": 10\r\n                },\r\n                \"smallint\": {\r\n                    \"$comment\": \"canonical uri: https://example.com/ref-and-id2#/$defs/smallint; another valid uri for this location: https://example.com/ref-and-id2/#bigint\",\r\n                    \"$id\": \"https://example.com/draft2020-12/ref-and-id2/\",\r\n                    \"$anchor\": \"bigint\",\r\n                    \"maximum\": 2\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteOrderOfEvaluationIdAndRefOnNestedSchema : IClassFixture<SuiteOrderOfEvaluationIdAndRefOnNestedSchema.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteOrderOfEvaluationIdAndRefOnNestedSchema(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestDataIsValidAgainstNestedSibling()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("5");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestDataIsInvalidAgainstNestedSibling()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$comment\": \"$id must be evaluated before $ref to get the proper $ref destination\",\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"https://example.com/draft2020-12/ref-and-id3/base.json\",\r\n            \"$ref\": \"nested/foo.json\",\r\n            \"$defs\": {\r\n                \"foo\": {\r\n                    \"$comment\": \"canonical uri: https://example.com/draft2020-12/ref-and-id3/nested/foo.json\",\r\n                    \"$id\": \"nested/foo.json\",\r\n                    \"$ref\": \"./bar.json\"\r\n                },\r\n                \"bar\": {\r\n                    \"$comment\": \"canonical uri: https://example.com/draft2020-12/ref-and-id3/nested/bar.json\",\r\n                    \"$id\": \"nested/bar.json\",\r\n                    \"type\": \"number\"\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteSimpleUrnBaseUriWithRefViaTheUrn : IClassFixture<SuiteSimpleUrnBaseUriWithRefViaTheUrn.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteSimpleUrnBaseUriWithRefViaTheUrn(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidUnderTheUrnIDedSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 37}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidUnderTheUrnIDedSchema()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 12}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$comment\": \"URIs do not have to have HTTP(s) schemes\",\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"urn:uuid:deadbeef-1234-ffff-ffff-4321feebdaed\",\r\n            \"minimum\": 30,\r\n            \"properties\": {\r\n                \"foo\": {\"$ref\": \"urn:uuid:deadbeef-1234-ffff-ffff-4321feebdaed\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteSimpleUrnBaseUriWithJsonPointer : IClassFixture<SuiteSimpleUrnBaseUriWithJsonPointer.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteSimpleUrnBaseUriWithJsonPointer(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAStringIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": \"bar\"}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestANonStringIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 12}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$comment\": \"URIs do not have to have HTTP(s) schemes\",\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"urn:uuid:deadbeef-1234-00ff-ff00-4321feebdaed\",\r\n            \"properties\": {\r\n                \"foo\": {\"$ref\": \"#/$defs/bar\"}\r\n            },\r\n            \"$defs\": {\r\n                \"bar\": {\"type\": \"string\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUrnBaseUriWithNss : IClassFixture<SuiteUrnBaseUriWithNss.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUrnBaseUriWithNss(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAStringIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": \"bar\"}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestANonStringIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 12}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$comment\": \"RFC 8141 §2.2\",\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"urn:example:1/406/47452/2\",\r\n            \"properties\": {\r\n                \"foo\": {\"$ref\": \"#/$defs/bar\"}\r\n            },\r\n            \"$defs\": {\r\n                \"bar\": {\"type\": \"string\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUrnBaseUriWithRComponent : IClassFixture<SuiteUrnBaseUriWithRComponent.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUrnBaseUriWithRComponent(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAStringIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": \"bar\"}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestANonStringIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 12}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$comment\": \"RFC 8141 §2.3.1\",\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"urn:example:foo-bar-baz-qux?+CCResolve:cc=uk\",\r\n            \"properties\": {\r\n                \"foo\": {\"$ref\": \"#/$defs/bar\"}\r\n            },\r\n            \"$defs\": {\r\n                \"bar\": {\"type\": \"string\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUrnBaseUriWithQComponent : IClassFixture<SuiteUrnBaseUriWithQComponent.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUrnBaseUriWithQComponent(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAStringIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": \"bar\"}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestANonStringIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 12}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$comment\": \"RFC 8141 §2.3.2\",\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"urn:example:weather?=op=map&lat=39.56&lon=-104.85&datetime=1969-07-21T02:56:15Z\",\r\n            \"properties\": {\r\n                \"foo\": {\"$ref\": \"#/$defs/bar\"}\r\n            },\r\n            \"$defs\": {\r\n                \"bar\": {\"type\": \"string\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUrnBaseUriWithUrnAndJsonPointerRef : IClassFixture<SuiteUrnBaseUriWithUrnAndJsonPointerRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUrnBaseUriWithUrnAndJsonPointerRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAStringIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": \"bar\"}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestANonStringIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 12}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"urn:uuid:deadbeef-1234-0000-0000-4321feebdaed\",\r\n            \"properties\": {\r\n                \"foo\": {\"$ref\": \"urn:uuid:deadbeef-1234-0000-0000-4321feebdaed#/$defs/bar\"}\r\n            },\r\n            \"$defs\": {\r\n                \"bar\": {\"type\": \"string\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUrnBaseUriWithUrnAndAnchorRef : IClassFixture<SuiteUrnBaseUriWithUrnAndAnchorRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUrnBaseUriWithUrnAndAnchorRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAStringIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": \"bar\"}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestANonStringIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": 12}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"urn:uuid:deadbeef-1234-ff00-00ff-4321feebdaed\",\r\n            \"properties\": {\r\n                \"foo\": {\"$ref\": \"urn:uuid:deadbeef-1234-ff00-00ff-4321feebdaed#something\"}\r\n            },\r\n            \"$defs\": {\r\n                \"bar\": {\r\n                    \"$anchor\": \"something\",\r\n                    \"type\": \"string\"\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteUrnRefWithNestedPointerRef : IClassFixture<SuiteUrnRefWithNestedPointerRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUrnRefWithNestedPointerRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAStringIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"bar\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestANonStringIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("12");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$ref\": \"urn:uuid:deadbeef-4321-ffff-ffff-1234feebdaed\",\r\n            \"$defs\": {\r\n                \"foo\": {\r\n                    \"$id\": \"urn:uuid:deadbeef-4321-ffff-ffff-1234feebdaed\",\r\n                    \"$defs\": {\"bar\": {\"type\": \"string\"}},\r\n                    \"$ref\": \"#/$defs/bar\"\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteRefToIf : IClassFixture<SuiteRefToIf.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefToIf(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestANonIntegerIsInvalidDueToTheRef()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnIntegerIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("12");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$ref\": \"http://example.com/ref/if\",\r\n            \"if\": {\r\n                \"$id\": \"http://example.com/ref/if\",\r\n                \"type\": \"integer\"\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteRefToThen : IClassFixture<SuiteRefToThen.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefToThen(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestANonIntegerIsInvalidDueToTheRef()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnIntegerIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("12");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$ref\": \"http://example.com/ref/then\",\r\n            \"then\": {\r\n                \"$id\": \"http://example.com/ref/then\",\r\n                \"type\": \"integer\"\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteRefToElse : IClassFixture<SuiteRefToElse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefToElse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestANonIntegerIsInvalidDueToTheRef()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnIntegerIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("12");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$ref\": \"http://example.com/ref/else\",\r\n            \"else\": {\r\n                \"$id\": \"http://example.com/ref/else\",\r\n                \"type\": \"integer\"\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteRefWithAbsolutePathReference : IClassFixture<SuiteRefWithAbsolutePathReference.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefWithAbsolutePathReference(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAStringIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnIntegerIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("12");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"http://example.com/ref/absref.json\",\r\n            \"$defs\": {\r\n                \"a\": {\r\n                    \"$id\": \"http://example.com/ref/absref/foobar.json\",\r\n                    \"type\": \"number\"\r\n                },\r\n                \"b\": {\r\n                    \"$id\": \"http://example.com/absref/foobar.json\",\r\n                    \"type\": \"string\"\r\n                }\r\n            },\r\n            \"$ref\": \"/absref/foobar.json\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteIdWithFileUriStillResolvesPointersNix : IClassFixture<SuiteIdWithFileUriStillResolvesPointersNix.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteIdWithFileUriStillResolvesPointersNix(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNumberIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonNumberIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"file:///folder/file.json\",\r\n            \"$defs\": {\r\n                \"foo\": {\r\n                    \"type\": \"number\"\r\n                }\r\n            },\r\n            \"$ref\": \"#/$defs/foo\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteIdWithFileUriStillResolvesPointersWindows : IClassFixture<SuiteIdWithFileUriStillResolvesPointersWindows.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteIdWithFileUriStillResolvesPointersWindows(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNumberIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonNumberIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"file:///c:/folder/file.json\",\r\n            \"$defs\": {\r\n                \"foo\": {\r\n                    \"type\": \"number\"\r\n                }\r\n            },\r\n            \"$ref\": \"#/$defs/foo\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteEmptyTokensInRefJsonPointer : IClassFixture<SuiteEmptyTokensInRefJsonPointer.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEmptyTokensInRefJsonPointer(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNumberIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNonNumberIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\ref.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$defs\": {\r\n                \"\": {\r\n                    \"$defs\": {\r\n                        \"\": { \"type\": \"number\" }\r\n                    }\r\n                } \r\n            },\r\n            \"allOf\": [\r\n                {\r\n                    \"$ref\": \"#/$defs//$defs/\"\r\n                }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
