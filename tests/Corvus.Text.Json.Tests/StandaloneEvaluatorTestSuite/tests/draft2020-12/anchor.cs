using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft202012.Anchor;

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
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
                "tests\\draft2020-12\\anchor.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$ref\": \"#foo\",\r\n            \"$defs\": {\r\n                \"A\": {\r\n                    \"$anchor\": \"foo\",\r\n                    \"type\": \"integer\"\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Anchor",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteLocationIndependentIdentifierWithAbsoluteUri : IClassFixture<SuiteLocationIndependentIdentifierWithAbsoluteUri.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteLocationIndependentIdentifierWithAbsoluteUri(Fixture fixture)
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
                "tests\\draft2020-12\\anchor.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$ref\": \"http://localhost:1234/draft2020-12/bar#foo\",\r\n            \"$defs\": {\r\n                \"A\": {\r\n                    \"$id\": \"http://localhost:1234/draft2020-12/bar\",\r\n                    \"$anchor\": \"foo\",\r\n                    \"type\": \"integer\"\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Anchor",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
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
                "tests\\draft2020-12\\anchor.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"http://localhost:1234/draft2020-12/root\",\r\n            \"$ref\": \"http://localhost:1234/draft2020-12/nested.json#foo\",\r\n            \"$defs\": {\r\n                \"A\": {\r\n                    \"$id\": \"nested.json\",\r\n                    \"$defs\": {\r\n                        \"B\": {\r\n                            \"$anchor\": \"foo\",\r\n                            \"type\": \"integer\"\r\n                        }\r\n                    }\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Anchor",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteSameAnchorWithDifferentBaseUri : IClassFixture<SuiteSameAnchorWithDifferentBaseUri.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteSameAnchorWithDifferentBaseUri(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestRefResolvesToDefsAAllOf1()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestRefDoesNotResolveToDefsAAllOf0()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\anchor.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"http://localhost:1234/draft2020-12/foobar\",\r\n            \"$defs\": {\r\n                \"A\": {\r\n                    \"$id\": \"child1\",\r\n                    \"allOf\": [\r\n                        {\r\n                            \"$id\": \"child2\",\r\n                            \"$anchor\": \"my_anchor\",\r\n                            \"type\": \"number\"\r\n                        },\r\n                        {\r\n                            \"$anchor\": \"my_anchor\",\r\n                            \"type\": \"string\"\r\n                        }\r\n                    ]\r\n                }\r\n            },\r\n            \"$ref\": \"child1#my_anchor\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Anchor",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
