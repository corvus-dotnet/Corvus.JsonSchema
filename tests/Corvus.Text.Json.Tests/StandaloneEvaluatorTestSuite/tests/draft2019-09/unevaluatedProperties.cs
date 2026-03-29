using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties;

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesTrue : IClassFixture<SuiteUnevaluatedPropertiesTrue.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesTrue(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"unevaluatedProperties\": true\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesSchema : IClassFixture<SuiteUnevaluatedPropertiesSchema.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesSchema(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithValidUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithInvalidUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"fo\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"unevaluatedProperties\": {\r\n                \"type\": \"string\",\r\n                \"minLength\": 3\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesFalse : IClassFixture<SuiteUnevaluatedPropertiesFalse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesFalse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"unevaluatedProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesWithAdjacentProperties : IClassFixture<SuiteUnevaluatedPropertiesWithAdjacentProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesWithAdjacentProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"unevaluatedProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesWithAdjacentPatternProperties : IClassFixture<SuiteUnevaluatedPropertiesWithAdjacentPatternProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesWithAdjacentPatternProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"patternProperties\": {\r\n                \"^foo\": { \"type\": \"string\" }\r\n            },\r\n            \"unevaluatedProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesWithAdjacentAdditionalProperties : IClassFixture<SuiteUnevaluatedPropertiesWithAdjacentAdditionalProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesWithAdjacentAdditionalProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoAdditionalProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithAdditionalProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"additionalProperties\": true,\r\n            \"unevaluatedProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesWithNestedProperties : IClassFixture<SuiteUnevaluatedPropertiesWithNestedProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesWithNestedProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoAdditionalProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithAdditionalProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\",\r\n                    \"baz\": \"baz\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"allOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"bar\": { \"type\": \"string\" }\r\n                    }\r\n                }\r\n            ],\r\n            \"unevaluatedProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesWithNestedPatternProperties : IClassFixture<SuiteUnevaluatedPropertiesWithNestedPatternProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesWithNestedPatternProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoAdditionalProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithAdditionalProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\",\r\n                    \"baz\": \"baz\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"allOf\": [\r\n              {\r\n                  \"patternProperties\": {\r\n                      \"^bar\": { \"type\": \"string\" }\r\n                  }\r\n              }\r\n            ],\r\n            \"unevaluatedProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesWithNestedAdditionalProperties : IClassFixture<SuiteUnevaluatedPropertiesWithNestedAdditionalProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesWithNestedAdditionalProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoAdditionalProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithAdditionalProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"allOf\": [\r\n                {\r\n                    \"additionalProperties\": true\r\n                }\r\n            ],\r\n            \"unevaluatedProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesWithNestedUnevaluatedProperties : IClassFixture<SuiteUnevaluatedPropertiesWithNestedUnevaluatedProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesWithNestedUnevaluatedProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoNestedUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithNestedUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"allOf\": [\r\n                {\r\n                    \"unevaluatedProperties\": true\r\n                }\r\n            ],\r\n            \"unevaluatedProperties\": {\r\n                \"type\": \"string\",\r\n                \"maxLength\": 2\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesWithAnyOf : IClassFixture<SuiteUnevaluatedPropertiesWithAnyOf.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesWithAnyOf(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWhenOneMatchesAndHasNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWhenOneMatchesAndHasUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\",\r\n                    \"baz\": \"not-baz\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWhenTwoMatchAndHasNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\",\r\n                    \"baz\": \"baz\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWhenTwoMatchAndHasUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\",\r\n                    \"baz\": \"baz\",\r\n                    \"quux\": \"not-quux\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"anyOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"bar\": { \"const\": \"bar\" }\r\n                    },\r\n                    \"required\": [\"bar\"]\r\n                },\r\n                {\r\n                    \"properties\": {\r\n                        \"baz\": { \"const\": \"baz\" }\r\n                    },\r\n                    \"required\": [\"baz\"]\r\n                },\r\n                {\r\n                    \"properties\": {\r\n                        \"quux\": { \"const\": \"quux\" }\r\n                    },\r\n                    \"required\": [\"quux\"]\r\n                }\r\n            ],\r\n            \"unevaluatedProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesWithOneOf : IClassFixture<SuiteUnevaluatedPropertiesWithOneOf.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesWithOneOf(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\",\r\n                    \"quux\": \"quux\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"oneOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"bar\": { \"const\": \"bar\" }\r\n                    },\r\n                    \"required\": [\"bar\"]\r\n                },\r\n                {\r\n                    \"properties\": {\r\n                        \"baz\": { \"const\": \"baz\" }\r\n                    },\r\n                    \"required\": [\"baz\"]\r\n                }\r\n            ],\r\n            \"unevaluatedProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesWithNot : IClassFixture<SuiteUnevaluatedPropertiesWithNot.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesWithNot(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"not\": {\r\n                \"not\": {\r\n                    \"properties\": {\r\n                        \"bar\": { \"const\": \"bar\" }\r\n                    },\r\n                    \"required\": [\"bar\"]\r\n                }\r\n            },\r\n            \"unevaluatedProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesWithIfThenElse : IClassFixture<SuiteUnevaluatedPropertiesWithIfThenElse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesWithIfThenElse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWhenIfIsTrueAndHasNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"then\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWhenIfIsTrueAndHasUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"then\",\r\n                    \"bar\": \"bar\",\r\n                    \"baz\": \"baz\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWhenIfIsFalseAndHasNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"baz\": \"baz\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWhenIfIsFalseAndHasUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"else\",\r\n                    \"baz\": \"baz\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"if\": {\r\n                \"properties\": {\r\n                    \"foo\": { \"const\": \"then\" }\r\n                },\r\n                \"required\": [\"foo\"]\r\n            },\r\n            \"then\": {\r\n                \"properties\": {\r\n                    \"bar\": { \"type\": \"string\" }\r\n                },\r\n                \"required\": [\"bar\"]\r\n            },\r\n            \"else\": {\r\n                \"properties\": {\r\n                    \"baz\": { \"type\": \"string\" }\r\n                },\r\n                \"required\": [\"baz\"]\r\n            },\r\n            \"unevaluatedProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesWithIfThenElseThenNotDefined : IClassFixture<SuiteUnevaluatedPropertiesWithIfThenElseThenNotDefined.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesWithIfThenElseThenNotDefined(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWhenIfIsTrueAndHasNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"then\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWhenIfIsTrueAndHasUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"then\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWhenIfIsFalseAndHasNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"baz\": \"baz\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWhenIfIsFalseAndHasUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"else\",\r\n                    \"baz\": \"baz\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"if\": {\r\n                \"properties\": {\r\n                    \"foo\": { \"const\": \"then\" }\r\n                },\r\n                \"required\": [\"foo\"]\r\n            },\r\n            \"else\": {\r\n                \"properties\": {\r\n                    \"baz\": { \"type\": \"string\" }\r\n                },\r\n                \"required\": [\"baz\"]\r\n            },\r\n            \"unevaluatedProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesWithIfThenElseElseNotDefined : IClassFixture<SuiteUnevaluatedPropertiesWithIfThenElseElseNotDefined.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesWithIfThenElseElseNotDefined(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWhenIfIsTrueAndHasNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"then\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWhenIfIsTrueAndHasUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"then\",\r\n                    \"bar\": \"bar\",\r\n                    \"baz\": \"baz\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWhenIfIsFalseAndHasNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"baz\": \"baz\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWhenIfIsFalseAndHasUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"else\",\r\n                    \"baz\": \"baz\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"if\": {\r\n                \"properties\": {\r\n                    \"foo\": { \"const\": \"then\" }\r\n                },\r\n                \"required\": [\"foo\"]\r\n            },\r\n            \"then\": {\r\n                \"properties\": {\r\n                    \"bar\": { \"type\": \"string\" }\r\n                },\r\n                \"required\": [\"bar\"]\r\n            },\r\n            \"unevaluatedProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesWithDependentSchemas : IClassFixture<SuiteUnevaluatedPropertiesWithDependentSchemas.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesWithDependentSchemas(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"dependentSchemas\": {\r\n                \"foo\": {\r\n                    \"properties\": {\r\n                        \"bar\": { \"const\": \"bar\" }\r\n                    },\r\n                    \"required\": [\"bar\"]\r\n                }\r\n            },\r\n            \"unevaluatedProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesWithBooleanSchemas : IClassFixture<SuiteUnevaluatedPropertiesWithBooleanSchemas.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesWithBooleanSchemas(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"allOf\": [true],\r\n            \"unevaluatedProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesWithRef : IClassFixture<SuiteUnevaluatedPropertiesWithRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesWithRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\",\r\n                    \"baz\": \"baz\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"$ref\": \"#/$defs/bar\",\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"unevaluatedProperties\": false,\r\n            \"$defs\": {\r\n                \"bar\": {\r\n                    \"properties\": {\r\n                        \"bar\": { \"type\": \"string\" }\r\n                    }\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesBeforeRef : IClassFixture<SuiteUnevaluatedPropertiesBeforeRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesBeforeRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\",\r\n                    \"baz\": \"baz\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"unevaluatedProperties\": false,\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"$ref\": \"#/$defs/bar\",\r\n            \"$defs\": {\r\n                \"bar\": {\r\n                    \"properties\": {\r\n                        \"bar\": { \"type\": \"string\" }\r\n                    }\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesWithRecursiveRef : IClassFixture<SuiteUnevaluatedPropertiesWithRecursiveRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesWithRecursiveRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"name\": \"a\",\r\n                    \"node\": 1,\r\n                    \"branches\": {\r\n                      \"name\": \"b\",\r\n                      \"node\": 2\r\n                    }\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"name\": \"a\",\r\n                    \"node\": 1,\r\n                    \"branches\": {\r\n                      \"foo\": \"b\",\r\n                      \"node\": 2\r\n                    }\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$id\": \"https://example.com/unevaluated-properties-with-recursive-ref/extended-tree\",\r\n\r\n            \"$recursiveAnchor\": true,\r\n\r\n            \"$ref\": \"./tree\",\r\n            \"properties\": {\r\n                \"name\": { \"type\": \"string\" }\r\n            },\r\n\r\n            \"$defs\": {\r\n                \"tree\": {\r\n                    \"$id\": \"./tree\",\r\n                    \"$recursiveAnchor\": true,\r\n\r\n                    \"type\": \"object\",\r\n                    \"properties\": {\r\n                        \"node\": true,\r\n                        \"branches\": {\r\n                            \"$comment\": \"unevaluatedProperties comes first so it's more likely to bugs errors with implementations that are sensitive to keyword ordering\",\r\n                            \"unevaluatedProperties\": false,\r\n                            \"$recursiveRef\": \"#\"\r\n                        }\r\n                    },\r\n                    \"required\": [\"node\"]\r\n                }\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesCanTSeeInsideCousins : IClassFixture<SuiteUnevaluatedPropertiesCanTSeeInsideCousins.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesCanTSeeInsideCousins(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAlwaysFails()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": 1\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"allOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"foo\": true\r\n                    }\r\n                },\r\n                {\r\n                    \"unevaluatedProperties\": false\r\n                }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesCanTSeeInsideCousinsReverseOrder : IClassFixture<SuiteUnevaluatedPropertiesCanTSeeInsideCousinsReverseOrder.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesCanTSeeInsideCousinsReverseOrder(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAlwaysFails()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": 1\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"allOf\": [\r\n                {\r\n                    \"unevaluatedProperties\": false\r\n                },\r\n                {\r\n                    \"properties\": {\r\n                        \"foo\": true\r\n                    }\r\n                }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteNestedUnevaluatedPropertiesOuterFalseInnerTruePropertiesOutside : IClassFixture<SuiteNestedUnevaluatedPropertiesOuterFalseInnerTruePropertiesOutside.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteNestedUnevaluatedPropertiesOuterFalseInnerTruePropertiesOutside(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoNestedUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithNestedUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"allOf\": [\r\n                {\r\n                    \"unevaluatedProperties\": true\r\n                }\r\n            ],\r\n            \"unevaluatedProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteNestedUnevaluatedPropertiesOuterFalseInnerTruePropertiesInside : IClassFixture<SuiteNestedUnevaluatedPropertiesOuterFalseInnerTruePropertiesInside.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteNestedUnevaluatedPropertiesOuterFalseInnerTruePropertiesInside(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoNestedUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithNestedUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"allOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"foo\": { \"type\": \"string\" }\r\n                    },\r\n                    \"unevaluatedProperties\": true\r\n                }\r\n            ],\r\n            \"unevaluatedProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteNestedUnevaluatedPropertiesOuterTrueInnerFalsePropertiesOutside : IClassFixture<SuiteNestedUnevaluatedPropertiesOuterTrueInnerFalsePropertiesOutside.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteNestedUnevaluatedPropertiesOuterTrueInnerFalsePropertiesOutside(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoNestedUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithNestedUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"properties\": {\r\n                \"foo\": { \"type\": \"string\" }\r\n            },\r\n            \"allOf\": [\r\n                {\r\n                    \"unevaluatedProperties\": false\r\n                }\r\n            ],\r\n            \"unevaluatedProperties\": true\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteNestedUnevaluatedPropertiesOuterTrueInnerFalsePropertiesInside : IClassFixture<SuiteNestedUnevaluatedPropertiesOuterTrueInnerFalsePropertiesInside.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteNestedUnevaluatedPropertiesOuterTrueInnerFalsePropertiesInside(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoNestedUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithNestedUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"allOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"foo\": { \"type\": \"string\" }\r\n                    },\r\n                    \"unevaluatedProperties\": false\r\n                }\r\n            ],\r\n            \"unevaluatedProperties\": true\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteCousinUnevaluatedPropertiesTrueAndFalseTrueWithProperties : IClassFixture<SuiteCousinUnevaluatedPropertiesTrueAndFalseTrueWithProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteCousinUnevaluatedPropertiesTrueAndFalseTrueWithProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoNestedUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithNestedUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"allOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"foo\": { \"type\": \"string\" }\r\n                    },\r\n                    \"unevaluatedProperties\": true\r\n                },\r\n                {\r\n                    \"unevaluatedProperties\": false\r\n                }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteCousinUnevaluatedPropertiesTrueAndFalseFalseWithProperties : IClassFixture<SuiteCousinUnevaluatedPropertiesTrueAndFalseFalseWithProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteCousinUnevaluatedPropertiesTrueAndFalseFalseWithProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithNoNestedUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestWithNestedUnevaluatedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"foo\",\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"allOf\": [\r\n                {\r\n                    \"unevaluatedProperties\": true\r\n                },\r\n                {\r\n                    \"properties\": {\r\n                        \"foo\": { \"type\": \"string\" }\r\n                    },\r\n                    \"unevaluatedProperties\": false\r\n                }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuitePropertyIsEvaluatedInAnUncleSchemaToUnevaluatedProperties : IClassFixture<SuitePropertyIsEvaluatedInAnUncleSchemaToUnevaluatedProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePropertyIsEvaluatedInAnUncleSchemaToUnevaluatedProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestNoExtraProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": {\r\n                        \"bar\": \"test\"\r\n                    }\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUncleKeywordEvaluationIsNotSignificant()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": {\r\n                        \"bar\": \"test\",\r\n                        \"faz\": \"test\"\r\n                    }\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"properties\": {\r\n                \"foo\": {\r\n                    \"type\": \"object\",\r\n                    \"properties\": {\r\n                        \"bar\": {\r\n                            \"type\": \"string\"\r\n                        }\r\n                    },\r\n                    \"unevaluatedProperties\": false\r\n                  }\r\n            },\r\n            \"anyOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"foo\": {\r\n                            \"properties\": {\r\n                                \"faz\": {\r\n                                    \"type\": \"string\"\r\n                                }\r\n                            }\r\n                        }\r\n                    }\r\n                }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteInPlaceApplicatorSiblingsAllOfHasUnevaluated : IClassFixture<SuiteInPlaceApplicatorSiblingsAllOfHasUnevaluated.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteInPlaceApplicatorSiblingsAllOfHasUnevaluated(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestBaseCaseBothPropertiesPresent()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": 1,\r\n                    \"bar\": 1\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInPlaceApplicatorSiblingsBarIsMissing()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": 1\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInPlaceApplicatorSiblingsFooIsMissing()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"bar\": 1\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"allOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"foo\": true\r\n                    },\r\n                    \"unevaluatedProperties\": false\r\n                }\r\n            ],\r\n            \"anyOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"bar\": true\r\n                    }\r\n                }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteInPlaceApplicatorSiblingsAnyOfHasUnevaluated : IClassFixture<SuiteInPlaceApplicatorSiblingsAnyOfHasUnevaluated.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteInPlaceApplicatorSiblingsAnyOfHasUnevaluated(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestBaseCaseBothPropertiesPresent()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": 1,\r\n                    \"bar\": 1\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInPlaceApplicatorSiblingsBarIsMissing()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": 1\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInPlaceApplicatorSiblingsFooIsMissing()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"bar\": 1\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"allOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"foo\": true\r\n                    }\r\n                }\r\n            ],\r\n            \"anyOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"bar\": true\r\n                    },\r\n                    \"unevaluatedProperties\": false\r\n                }\r\n            ]\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesSingleCyclicRef : IClassFixture<SuiteUnevaluatedPropertiesSingleCyclicRef.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesSingleCyclicRef(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestEmptyIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSingleIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"x\": {} }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUnevaluatedOn1stLevelIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"x\": {}, \"y\": {} }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNestedIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"x\": { \"x\": {} } }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUnevaluatedOn2ndLevelIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"x\": { \"x\": {}, \"y\": {} } }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestDeepNestedIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"x\": { \"x\": { \"x\": {} } } }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUnevaluatedOn3rdLevelIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"x\": { \"x\": { \"x\": {}, \"y\": {} } } }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"type\": \"object\",\r\n            \"properties\": {\r\n                \"x\": { \"$ref\": \"#\" }\r\n            },\r\n            \"unevaluatedProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesRefInsideAllOfOneOf : IClassFixture<SuiteUnevaluatedPropertiesRefInsideAllOfOneOf.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesRefInsideAllOfOneOf(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestEmptyIsInvalidNoXOrY()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAAndBAreInvalidNoXOrY()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"a\": 1, \"b\": 1 }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestXAndYAreInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"x\": 1, \"y\": 1 }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAAndXAreValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"a\": 1, \"x\": 1 }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAAndYAreValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"a\": 1, \"y\": 1 }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAAndBAndXAreValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"a\": 1, \"b\": 1, \"x\": 1 }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAAndBAndYAreValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"a\": 1, \"b\": 1, \"y\": 1 }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAAndBAndXAndYAreInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"a\": 1, \"b\": 1, \"x\": 1, \"y\": 1 }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$defs\": {\r\n                \"one\": {\r\n                    \"properties\": { \"a\": true }\r\n                },\r\n                \"two\": {\r\n                    \"required\": [\"x\"],\r\n                    \"properties\": { \"x\": true }\r\n                }\r\n            },\r\n            \"allOf\": [\r\n                { \"$ref\": \"#/$defs/one\" },\r\n                { \"properties\": { \"b\": true } },\r\n                {\r\n                    \"oneOf\": [\r\n                        { \"$ref\": \"#/$defs/two\" },\r\n                        {\r\n                            \"required\": [\"y\"],\r\n                            \"properties\": { \"y\": true }\r\n                        }\r\n                    ]\r\n                }\r\n            ],\r\n            \"unevaluatedProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteDynamicEvalationInsideNestedRefs : IClassFixture<SuiteDynamicEvalationInsideNestedRefs.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteDynamicEvalationInsideNestedRefs(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestEmptyIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"a\": 1 }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestBIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"b\": 1 }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestCIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"c\": 1 }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestDIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"d\": 1 }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestABIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"a\": 1, \"b\": 1 }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestACIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"a\": 1, \"c\": 1 }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestADIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"a\": 1, \"d\": 1 }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestBCIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"b\": 1, \"c\": 1 }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestBDIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"b\": 1, \"d\": 1 }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestCDIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"c\": 1, \"d\": 1 }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestXxIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"xx\": 1 }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestXxFooxIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"xx\": 1, \"foox\": 1 }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestXxFooIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"xx\": 1, \"foo\": 1 }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestXxAIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"xx\": 1, \"a\": 1 }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestXxBIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"xx\": 1, \"b\": 1 }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestXxCIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"xx\": 1, \"c\": 1 }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestXxDIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"xx\": 1, \"d\": 1 }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"all\": 1 }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllFooIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"all\": 1, \"foo\": 1 }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllAIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"all\": 1, \"a\": 1 }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"$defs\": {\r\n                \"one\": {\r\n                    \"oneOf\": [\r\n                        { \"$ref\": \"#/$defs/two\" },\r\n                        { \"required\": [\"b\"], \"properties\": { \"b\": true } },\r\n                        { \"required\": [\"xx\"], \"patternProperties\": { \"x\": true } },\r\n                        { \"required\": [\"all\"], \"unevaluatedProperties\": true }\r\n                    ]\r\n                },\r\n                \"two\": {\r\n                    \"oneOf\": [\r\n                        { \"required\": [\"c\"], \"properties\": { \"c\": true } },\r\n                        { \"required\": [\"d\"], \"properties\": { \"d\": true } }\r\n                    ]\r\n                }\r\n            },\r\n            \"oneOf\": [\r\n                { \"$ref\": \"#/$defs/one\" },\r\n                { \"required\": [\"a\"], \"properties\": { \"a\": true } }\r\n            ],\r\n            \"unevaluatedProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteNonObjectInstancesAreValid : IClassFixture<SuiteNonObjectInstancesAreValid.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteNonObjectInstancesAreValid(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestIgnoresBooleans()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("true");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresIntegers()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("123");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresFloats()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("1.0");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresArrays()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresStrings()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"foo\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIgnoresNull()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"unevaluatedProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesWithNullValuedInstanceProperties : IClassFixture<SuiteUnevaluatedPropertiesWithNullValuedInstanceProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesWithNullValuedInstanceProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAllowsNullValuedProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": null}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"unevaluatedProperties\": {\r\n                \"type\": \"null\"\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesNotAffectedByPropertyNames : IClassFixture<SuiteUnevaluatedPropertiesNotAffectedByPropertyNames.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesNotAffectedByPropertyNames(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAllowsOnlyNumberProperties()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\": 1}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestStringPropertyIsInvalid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\": \"b\"}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"propertyNames\": {\"maxLength\": 1},\r\n            \"unevaluatedProperties\": {\r\n                \"type\": \"number\"\r\n            }\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesCanSeeAnnotationsFromIfWithoutThenAndElse : IClassFixture<SuiteUnevaluatedPropertiesCanSeeAnnotationsFromIfWithoutThenAndElse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesCanSeeAnnotationsFromIfWithoutThenAndElse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestValidInCaseIfIsEvaluated()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": \"a\"\r\n                }");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidInCaseIfIsEvaluated()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"bar\": \"a\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"if\": {\r\n                \"patternProperties\": {\r\n                    \"foo\": {\r\n                        \"type\": \"string\"\r\n                    }\r\n                }\r\n            },\r\n            \"unevaluatedProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteDependentSchemasWithUnevaluatedProperties : IClassFixture<SuiteDependentSchemasWithUnevaluatedProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteDependentSchemasWithUnevaluatedProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestUnevaluatedPropertiesDoesnTConsiderDependentSchemas()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\": \"\"}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUnevaluatedPropertiesDoesnTSeeBarWhenFoo2IsAbsent()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"bar\": \"\"}");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUnevaluatedPropertiesSeesBarWhenFoo2IsPresent()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{ \"foo2\": \"\", \"bar\": \"\"}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"properties\": {\"foo2\": {}},\r\n            \"dependentSchemas\": {\r\n                \"foo\" : {},\r\n                \"foo2\": {\r\n                    \"properties\": {\r\n                        \"bar\":{}\r\n                    }\r\n                }\r\n            },\r\n            \"unevaluatedProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteEvaluatedPropertiesCollectionNeedsToConsiderInstanceLocation : IClassFixture<SuiteEvaluatedPropertiesCollectionNeedsToConsiderInstanceLocation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEvaluatedPropertiesCollectionNeedsToConsiderInstanceLocation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWithAnUnevaluatedPropertyThatExistsAtAnotherLocation()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\r\n                    \"foo\": { \"bar\": \"foo\" },\r\n                    \"bar\": \"bar\"\r\n                }");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\unevaluatedProperties.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"properties\": {\r\n                \"foo\": {\r\n                    \"properties\": {\r\n                        \"bar\": { \"type\": \"string\" }\r\n                    }\r\n                }\r\n            },\r\n            \"unevaluatedProperties\": false\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.UnevaluatedProperties",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
