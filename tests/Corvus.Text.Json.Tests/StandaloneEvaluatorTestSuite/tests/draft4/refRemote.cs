using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft4.RefRemote;

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
public class SuiteRemoteRef : IClassFixture<SuiteRemoteRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRemoteRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestRemoteRefValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestRemoteRefInvalid()
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
                "tests\\draft4\\refRemote.json",
                "{\"$ref\": \"http://localhost:1234/integer.json\"}",
                "StandaloneEvaluatorTestSuite.Draft4.RefRemote",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
public class SuiteFragmentWithinRemoteRef : IClassFixture<SuiteFragmentWithinRemoteRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteFragmentWithinRemoteRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestRemoteFragmentValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestRemoteFragmentInvalid()
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
                "tests\\draft4\\refRemote.json",
                "{\"$ref\": \"http://localhost:1234/draft4/subSchemas.json#/definitions/integer\"}",
                "StandaloneEvaluatorTestSuite.Draft4.RefRemote",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
public class SuiteRefWithinRemoteRef : IClassFixture<SuiteRefWithinRemoteRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefWithinRemoteRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestRefWithinRefValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestRefWithinRefInvalid()
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
                "tests\\draft4\\refRemote.json",
                "{\r\n            \"$ref\": \"http://localhost:1234/draft4/subSchemas.json#/definitions/refToInteger\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.RefRemote",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
public class SuiteBaseUriChange : IClassFixture<SuiteBaseUriChange.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteBaseUriChange(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestBaseUriChangeRefValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[1]]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestBaseUriChangeRefInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[\"a\"]]");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft4\\refRemote.json",
                "{\r\n            \"id\": \"http://localhost:1234/\",\r\n            \"items\": {\r\n                \"id\": \"baseUriChange/\",\r\n                \"items\": {\"$ref\": \"folderInteger.json\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.RefRemote",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
public class SuiteBaseUriChangeChangeFolder : IClassFixture<SuiteBaseUriChangeChangeFolder.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteBaseUriChangeChangeFolder(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNumberIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"list\": [1]}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestStringIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"list\": [\"a\"]}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft4\\refRemote.json",
                "{\r\n            \"id\": \"http://localhost:1234/scope_change_defs1.json\",\r\n            \"type\" : \"object\",\r\n            \"properties\": {\r\n                \"list\": {\"$ref\": \"#/definitions/baz\"}\r\n            },\r\n            \"definitions\": {\r\n                \"baz\": {\r\n                    \"id\": \"baseUriChangeFolder/\",\r\n                    \"type\": \"array\",\r\n                    \"items\": {\"$ref\": \"folderInteger.json\"}\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.RefRemote",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
public class SuiteBaseUriChangeChangeFolderInSubschema : IClassFixture<SuiteBaseUriChangeChangeFolderInSubschema.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteBaseUriChangeChangeFolderInSubschema(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNumberIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"list\": [1]}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestStringIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"list\": [\"a\"]}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft4\\refRemote.json",
                "{\r\n            \"id\": \"http://localhost:1234/scope_change_defs2.json\",\r\n            \"type\" : \"object\",\r\n            \"properties\": {\r\n                \"list\": {\"$ref\": \"#/definitions/baz/definitions/bar\"}\r\n            },\r\n            \"definitions\": {\r\n                \"baz\": {\r\n                    \"id\": \"baseUriChangeFolderInSubschema/\",\r\n                    \"definitions\": {\r\n                        \"bar\": {\r\n                            \"type\": \"array\",\r\n                            \"items\": {\"$ref\": \"folderInteger.json\"}\r\n                        }\r\n                    }\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.RefRemote",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
public class SuiteRootRefInRemoteRef : IClassFixture<SuiteRootRefInRemoteRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRootRefInRemoteRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestStringIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"name\": \"foo\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNullIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"name\": null\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestObjectIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"name\": {\r\n                        \"name\": null\r\n                    }\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft4\\refRemote.json",
                "{\r\n            \"id\": \"http://localhost:1234/object\",\r\n            \"type\": \"object\",\r\n            \"properties\": {\r\n                \"name\": {\"$ref\": \"draft4/name.json#/definitions/orNull\"}\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.RefRemote",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
public class SuiteLocationIndependentIdentifierInRemoteRef : IClassFixture<SuiteLocationIndependentIdentifierInRemoteRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteLocationIndependentIdentifierInRemoteRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestIntegerIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestStringIsInvalid()
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
                "tests\\draft4\\refRemote.json",
                "{\r\n            \"$ref\": \"http://localhost:1234/draft4/locationIndependentIdentifier.json#/definitions/refToInteger\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft4.RefRemote",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
