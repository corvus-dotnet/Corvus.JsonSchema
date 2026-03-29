using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft202012.Optional.RefOfUnknownKeyword;

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteReferenceOfARootArbitraryKeyword : IClassFixture<SuiteReferenceOfARootArbitraryKeyword.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteReferenceOfARootArbitraryKeyword(Fixture fixture)
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
                "tests\\draft2020-12\\optional\\refOfUnknownKeyword.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"unknown-keyword\": {\"type\": \"integer\"},\r\n            \"properties\": {\r\n                \"bar\": {\"$ref\": \"#/unknown-keyword\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.RefOfUnknownKeyword",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteReferenceOfARootArbitraryKeywordWithEncodedRef : IClassFixture<SuiteReferenceOfARootArbitraryKeywordWithEncodedRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteReferenceOfARootArbitraryKeywordWithEncodedRef(Fixture fixture)
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
                "tests\\draft2020-12\\optional\\refOfUnknownKeyword.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"unknown/keyword\": {\"type\": \"integer\"},\r\n            \"properties\": {\r\n                \"bar\": {\"$ref\": \"#/unknown~1keyword\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.RefOfUnknownKeyword",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteReferenceOfAnArbitraryKeywordOfASubSchema : IClassFixture<SuiteReferenceOfAnArbitraryKeywordOfASubSchema.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteReferenceOfAnArbitraryKeywordOfASubSchema(Fixture fixture)
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
                "tests\\draft2020-12\\optional\\refOfUnknownKeyword.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"foo\": {\"unknown-keyword\": {\"type\": \"integer\"}},\r\n                \"bar\": {\"$ref\": \"#/properties/foo/unknown-keyword\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.RefOfUnknownKeyword",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteReferenceInternalsOfKnownNonApplicator : IClassFixture<SuiteReferenceInternalsOfKnownNonApplicator.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteReferenceInternalsOfKnownNonApplicator(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestMatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a string\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMismatch()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("42");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\optional\\refOfUnknownKeyword.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"$id\": \"/base\",\r\n            \"examples\": [\r\n              { \"type\": \"string\" }\r\n            ],\r\n            \"$ref\": \"#/examples/0\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.RefOfUnknownKeyword",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteReferenceOfAnArbitraryKeywordOfASubSchemaWithEncodedRef : IClassFixture<SuiteReferenceOfAnArbitraryKeywordOfASubSchemaWithEncodedRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteReferenceOfAnArbitraryKeywordOfASubSchemaWithEncodedRef(Fixture fixture)
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
                "tests\\draft2020-12\\optional\\refOfUnknownKeyword.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"properties\": {\r\n                \"foo\": {\"unknown/keyword\": {\"type\": \"integer\"}},\r\n                \"bar\": {\"$ref\": \"#/properties/foo/unknown~1keyword\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.RefOfUnknownKeyword",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
