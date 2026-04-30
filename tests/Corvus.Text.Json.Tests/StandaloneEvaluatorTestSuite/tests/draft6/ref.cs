using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft6.Ref;

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
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
                "tests\\draft6\\ref.json",
                "{\r\n            \"properties\": {\r\n                \"foo\": {\"$ref\": \"#\"}\r\n            },\r\n            \"additionalProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
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
                "tests\\draft6\\ref.json",
                "{\r\n            \"properties\": {\r\n                \"foo\": {\"type\": \"integer\"},\r\n                \"bar\": {\"$ref\": \"#/properties/foo\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
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
                "tests\\draft6\\ref.json",
                "{\r\n            \"items\": [\r\n                {\"type\": \"integer\"},\r\n                {\"$ref\": \"#/items/0\"}\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
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
                "tests\\draft6\\ref.json",
                "{\r\n            \"definitions\": {\r\n                \"tilde~field\": {\"type\": \"integer\"},\r\n                \"slash/field\": {\"type\": \"integer\"},\r\n                \"percent%field\": {\"type\": \"integer\"}\r\n            },\r\n            \"properties\": {\r\n                \"tilde\": {\"$ref\": \"#/definitions/tilde~0field\"},\r\n                \"slash\": {\"$ref\": \"#/definitions/slash~1field\"},\r\n                \"percent\": {\"$ref\": \"#/definitions/percent%25field\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
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
                "tests\\draft6\\ref.json",
                "{\r\n            \"definitions\": {\r\n                \"a\": {\"type\": \"integer\"},\r\n                \"b\": {\"$ref\": \"#/definitions/a\"},\r\n                \"c\": {\"$ref\": \"#/definitions/b\"}\r\n            },\r\n            \"allOf\": [{ \"$ref\": \"#/definitions/c\" }]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteRefOverridesAnySiblingKeywords : IClassFixture<SuiteRefOverridesAnySiblingKeywords.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefOverridesAnySiblingKeywords(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestRefValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": [] }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestRefValidMaxItemsIgnored()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo\": [ 1, 2, 3] }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
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
                "tests\\draft6\\ref.json",
                "{\r\n            \"definitions\": {\r\n                \"reffed\": {\r\n                    \"type\": \"array\"\r\n                }\r\n            },\r\n            \"properties\": {\r\n                \"foo\": {\r\n                    \"$ref\": \"#/definitions/reffed\",\r\n                    \"maxItems\": 2\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteRefPreventsASiblingIdFromChangingTheBaseUri : IClassFixture<SuiteRefPreventsASiblingIdFromChangingTheBaseUri.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefPreventsASiblingIdFromChangingTheBaseUri(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestRefResolvesToDefinitionsBaseFooDataDoesNotValidate()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestRefResolvesToDefinitionsBaseFooDataValidates()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\ref.json",
                "{\r\n            \"$id\": \"http://localhost:1234/sibling_id/base/\",\r\n            \"definitions\": {\r\n                \"foo\": {\r\n                    \"$id\": \"http://localhost:1234/sibling_id/foo.json\",\r\n                    \"type\": \"string\"\r\n                },\r\n                \"base_foo\": {\r\n                    \"$comment\": \"this canonical uri is http://localhost:1234/sibling_id/base/foo.json\",\r\n                    \"$id\": \"foo.json\",\r\n                    \"type\": \"number\"\r\n                }\r\n            },\r\n            \"allOf\": [\r\n                {\r\n                    \"$comment\": \"$ref resolves to http://localhost:1234/sibling_id/base/foo.json, not http://localhost:1234/sibling_id/foo.json\",\r\n                    \"$id\": \"http://localhost:1234/sibling_id/\",\r\n                    \"$ref\": \"foo.json\"\r\n                }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
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
                "tests\\draft6\\ref.json",
                "{\"$ref\": \"http://json-schema.org/draft-06/schema#\"}",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
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
                "tests\\draft6\\ref.json",
                "{\r\n            \"properties\": {\r\n                \"$ref\": {\"type\": \"string\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
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
                "tests\\draft6\\ref.json",
                "{\r\n            \"properties\": {\r\n                \"$ref\": {\"$ref\": \"#/definitions/is-string\"}\r\n            },\r\n            \"definitions\": {\r\n                \"is-string\": {\r\n                    \"type\": \"string\"\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
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
                "tests\\draft6\\ref.json",
                "{\r\n            \"allOf\": [{ \"$ref\": \"#/definitions/bool\" }],\r\n            \"definitions\": {\r\n                \"bool\": true\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
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
                "tests\\draft6\\ref.json",
                "{\r\n            \"allOf\": [{ \"$ref\": \"#/definitions/bool\" }],\r\n            \"definitions\": {\r\n                \"bool\": false\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \r\n                    \"meta\": \"root\",\r\n                    \"nodes\": [\r\n                        {\r\n                            \"value\": 1,\r\n                            \"subtree\": {\r\n                                \"meta\": \"child\",\r\n                                \"nodes\": [\r\n                                    {\"value\": 1.1},\r\n                                    {\"value\": 1.2}\r\n                                ]\r\n                            }\r\n                        },\r\n                        {\r\n                            \"value\": 2,\r\n                            \"subtree\": {\r\n                                \"meta\": \"child\",\r\n                                \"nodes\": [\r\n                                    {\"value\": 2.1},\r\n                                    {\"value\": 2.2}\r\n                                ]\r\n                            }\r\n                        }\r\n                    ]\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidTree()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \r\n                    \"meta\": \"root\",\r\n                    \"nodes\": [\r\n                        {\r\n                            \"value\": 1,\r\n                            \"subtree\": {\r\n                                \"meta\": \"child\",\r\n                                \"nodes\": [\r\n                                    {\"value\": \"string is invalid\"},\r\n                                    {\"value\": 1.2}\r\n                                ]\r\n                            }\r\n                        },\r\n                        {\r\n                            \"value\": 2,\r\n                            \"subtree\": {\r\n                                \"meta\": \"child\",\r\n                                \"nodes\": [\r\n                                    {\"value\": 2.1},\r\n                                    {\"value\": 2.2}\r\n                                ]\r\n                            }\r\n                        }\r\n                    ]\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\ref.json",
                "{\r\n            \"$id\": \"http://localhost:1234/tree\",\r\n            \"description\": \"tree of nodes\",\r\n            \"type\": \"object\",\r\n            \"properties\": {\r\n                \"meta\": {\"type\": \"string\"},\r\n                \"nodes\": {\r\n                    \"type\": \"array\",\r\n                    \"items\": {\"$ref\": \"node\"}\r\n                }\r\n            },\r\n            \"required\": [\"meta\", \"nodes\"],\r\n            \"definitions\": {\r\n                \"node\": {\r\n                    \"$id\": \"http://localhost:1234/node\",\r\n                    \"description\": \"node\",\r\n                    \"type\": \"object\",\r\n                    \"properties\": {\r\n                        \"value\": {\"type\": \"number\"},\r\n                        \"subtree\": {\"$ref\": \"tree\"}\r\n                    },\r\n                    \"required\": [\"value\"]\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
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
                "tests\\draft6\\ref.json",
                "{\r\n            \"properties\": {\r\n                \"foo\\\"bar\": {\"$ref\": \"#/definitions/foo%22bar\"}\r\n            },\r\n            \"definitions\": {\r\n                \"foo\\\"bar\": {\"type\": \"number\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteLocationIndependentIdentifier : IClassFixture<SuiteLocationIndependentIdentifier.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteLocationIndependentIdentifier(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMismatch()
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
                "tests\\draft6\\ref.json",
                "{\r\n            \"allOf\": [{\r\n                \"$ref\": \"#foo\"\r\n            }],\r\n            \"definitions\": {\r\n                \"A\": {\r\n                    \"$id\": \"#foo\",\r\n                    \"type\": \"integer\"\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteReferenceAnAnchorWithANonRelativeUri : IClassFixture<SuiteReferenceAnAnchorWithANonRelativeUri.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteReferenceAnAnchorWithANonRelativeUri(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMismatch()
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
                "tests\\draft6\\ref.json",
                "{\r\n            \"$id\": \"https://example.com/schema-with-anchor\",\r\n            \"allOf\": [{\r\n                \"$ref\": \"https://example.com/schema-with-anchor#foo\"\r\n            }],\r\n            \"definitions\": {\r\n                \"A\": {\r\n                    \"$id\": \"#foo\",\r\n                    \"type\": \"integer\"\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
public class SuiteLocationIndependentIdentifierWithBaseUriChangeInSubschema : IClassFixture<SuiteLocationIndependentIdentifierWithBaseUriChangeInSubschema.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteLocationIndependentIdentifierWithBaseUriChangeInSubschema(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMismatch()
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
                "tests\\draft6\\ref.json",
                "{\r\n            \"$id\": \"http://localhost:1234/root\",\r\n            \"allOf\": [{\r\n                \"$ref\": \"http://localhost:1234/nested.json#foo\"\r\n            }],\r\n            \"definitions\": {\r\n                \"A\": {\r\n                    \"$id\": \"nested.json\",\r\n                    \"definitions\": {\r\n                        \"B\": {\r\n                            \"$id\": \"#foo\",\r\n                            \"type\": \"integer\"\r\n                        }\r\n                    }\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
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
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"$ref\": \"#/definitions/a_string\" }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft6\\ref.json",
                "{\r\n            \"definitions\": {\r\n                \"a_string\": { \"type\": \"string\" }\r\n            },\r\n            \"enum\": [\r\n                { \"$ref\": \"#/definitions/a_string\" }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
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
                "tests\\draft6\\ref.json",
                "{\r\n            \"$id\": \"http://example.com/schema-relative-uri-defs1.json\",\r\n            \"properties\": {\r\n                \"foo\": {\r\n                    \"$id\": \"schema-relative-uri-defs2.json\",\r\n                    \"definitions\": {\r\n                        \"inner\": {\r\n                            \"properties\": {\r\n                                \"bar\": { \"type\": \"string\" }\r\n                            }\r\n                        }\r\n                    },\r\n                    \"allOf\": [ { \"$ref\": \"#/definitions/inner\" } ]\r\n                }\r\n            },\r\n            \"allOf\": [ { \"$ref\": \"schema-relative-uri-defs2.json\" } ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
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
                "tests\\draft6\\ref.json",
                "{\r\n            \"$id\": \"http://example.com/schema-refs-absolute-uris-defs1.json\",\r\n            \"properties\": {\r\n                \"foo\": {\r\n                    \"$id\": \"http://example.com/schema-refs-absolute-uris-defs2.json\",\r\n                    \"definitions\": {\r\n                        \"inner\": {\r\n                            \"properties\": {\r\n                                \"bar\": { \"type\": \"string\" }\r\n                            }\r\n                        }\r\n                    },\r\n                    \"allOf\": [ { \"$ref\": \"#/definitions/inner\" } ]\r\n                }\r\n            },\r\n            \"allOf\": [ { \"$ref\": \"schema-refs-absolute-uris-defs2.json\" } ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
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
                "tests\\draft6\\ref.json",
                "{\r\n            \"$comment\": \"URIs do not have to have HTTP(s) schemes\",\r\n            \"$id\": \"urn:uuid:deadbeef-1234-ffff-ffff-4321feebdaed\",\r\n            \"minimum\": 30,\r\n            \"properties\": {\r\n                \"foo\": {\"$ref\": \"urn:uuid:deadbeef-1234-ffff-ffff-4321feebdaed\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
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
                "tests\\draft6\\ref.json",
                "{\r\n            \"$comment\": \"URIs do not have to have HTTP(s) schemes\",\r\n            \"$id\": \"urn:uuid:deadbeef-1234-00ff-ff00-4321feebdaed\",\r\n            \"properties\": {\r\n                \"foo\": {\"$ref\": \"#/definitions/bar\"}\r\n            },\r\n            \"definitions\": {\r\n                \"bar\": {\"type\": \"string\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
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
                "tests\\draft6\\ref.json",
                "{\r\n            \"$comment\": \"RFC 8141 §2.2\",\r\n            \"$id\": \"urn:example:1/406/47452/2\",\r\n            \"properties\": {\r\n                \"foo\": {\"$ref\": \"#/definitions/bar\"}\r\n            },\r\n            \"definitions\": {\r\n                \"bar\": {\"type\": \"string\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
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
                "tests\\draft6\\ref.json",
                "{\r\n            \"$comment\": \"RFC 8141 §2.3.1\",\r\n            \"$id\": \"urn:example:foo-bar-baz-qux?+CCResolve:cc=uk\",\r\n            \"properties\": {\r\n                \"foo\": {\"$ref\": \"#/definitions/bar\"}\r\n            },\r\n            \"definitions\": {\r\n                \"bar\": {\"type\": \"string\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
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
                "tests\\draft6\\ref.json",
                "{\r\n            \"$comment\": \"RFC 8141 §2.3.2\",\r\n            \"$id\": \"urn:example:weather?=op=map&lat=39.56&lon=-104.85&datetime=1969-07-21T02:56:15Z\",\r\n            \"properties\": {\r\n                \"foo\": {\"$ref\": \"#/definitions/bar\"}\r\n            },\r\n            \"definitions\": {\r\n                \"bar\": {\"type\": \"string\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
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
                "tests\\draft6\\ref.json",
                "{\r\n            \"$id\": \"urn:uuid:deadbeef-1234-0000-0000-4321feebdaed\",\r\n            \"properties\": {\r\n                \"foo\": {\"$ref\": \"urn:uuid:deadbeef-1234-0000-0000-4321feebdaed#/definitions/bar\"}\r\n            },\r\n            \"definitions\": {\r\n                \"bar\": {\"type\": \"string\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
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
                "tests\\draft6\\ref.json",
                "{\r\n            \"$id\": \"urn:uuid:deadbeef-1234-ff00-00ff-4321feebdaed\",\r\n            \"properties\": {\r\n                \"foo\": {\"$ref\": \"urn:uuid:deadbeef-1234-ff00-00ff-4321feebdaed#something\"}\r\n            },\r\n            \"definitions\": {\r\n                \"bar\": {\r\n                    \"$id\": \"#something\",\r\n                    \"type\": \"string\"\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
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
                "tests\\draft6\\ref.json",
                "{\r\n             \"$id\": \"http://example.com/ref/absref.json\",\r\n             \"definitions\": {\r\n                 \"a\": {\r\n                     \"$id\": \"http://example.com/ref/absref/foobar.json\",\r\n                     \"type\": \"number\"\r\n                  },\r\n                  \"b\": {\r\n                      \"$id\": \"http://example.com/absref/foobar.json\",\r\n                      \"type\": \"string\"\r\n                  }\r\n             },\r\n             \"allOf\": [\r\n                 { \"$ref\": \"/absref/foobar.json\" }\r\n             ]\r\n         }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
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
                "tests\\draft6\\ref.json",
                "{\r\n             \"$id\": \"file:///folder/file.json\",\r\n             \"definitions\": {\r\n                 \"foo\": {\r\n                     \"type\": \"number\"\r\n                 }\r\n             },\r\n             \"allOf\": [\r\n                 {\r\n                     \"$ref\": \"#/definitions/foo\"\r\n                 }\r\n             ]\r\n         }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
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
                "tests\\draft6\\ref.json",
                "{\r\n             \"$id\": \"file:///c:/folder/file.json\",\r\n             \"definitions\": {\r\n                 \"foo\": {\r\n                     \"type\": \"number\"\r\n                 }\r\n             },\r\n             \"allOf\": [\r\n                 {\r\n                     \"$ref\": \"#/definitions/foo\"\r\n                 }\r\n             ]\r\n         }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft6")]
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
                "tests\\draft6\\ref.json",
                "{\r\n             \"definitions\": {\r\n                 \"\": {\r\n                     \"definitions\": {\r\n                         \"\": { \"type\": \"number\" }\r\n                     }\r\n                 } \r\n             },\r\n             \"allOf\": [\r\n                 {\r\n                     \"$ref\": \"#/definitions//definitions/\"\r\n                 }\r\n             ]\r\n         }",
                "StandaloneEvaluatorTestSuite.Draft6.Ref",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
