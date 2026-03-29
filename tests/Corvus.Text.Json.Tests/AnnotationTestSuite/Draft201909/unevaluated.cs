using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace AnnotationTestSuite.Draft201909.Unevaluated;

[Trait("AnnotationTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesAlone : IClassFixture<SuiteUnevaluatedPropertiesAlone.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesAlone(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0TitleFooAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/foo",
            "title",
            "{\r\n                \"#/unevaluatedProperties\": \"Unevaluated\"\r\n              }");
    }

    [Fact]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/bar",
            "title",
            "{\r\n                \"#/unevaluatedProperties\": \"Unevaluated\"\r\n              }");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\r\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\r\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesWithProperties : IClassFixture<SuiteUnevaluatedPropertiesWithProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesWithProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0TitleFooAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/foo",
            "title",
            "{\r\n                \"#/properties/foo\": \"Evaluated\"\r\n              }");
    }

    [Fact]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/bar",
            "title",
            "{\r\n                \"#/unevaluatedProperties\": \"Unevaluated\"\r\n              }");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\r\n        \"properties\": {\r\n          \"foo\": { \"title\": \"Evaluated\" }\r\n        },\r\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\r\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesWithPatternProperties : IClassFixture<SuiteUnevaluatedPropertiesWithPatternProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesWithPatternProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0TitleAppleAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{ \"apple\": 42, \"bar\": 24 }",
            "/apple",
            "title",
            "{\r\n                \"#/patternProperties/%5Ea\": \"Evaluated\"\r\n              }");
    }

    [Fact]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{ \"apple\": 42, \"bar\": 24 }",
            "/bar",
            "title",
            "{\r\n                \"#/unevaluatedProperties\": \"Unevaluated\"\r\n              }");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\r\n        \"patternProperties\": {\r\n          \"^a\": { \"title\": \"Evaluated\" }\r\n        },\r\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\r\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesWithAdditionalProperties : IClassFixture<SuiteUnevaluatedPropertiesWithAdditionalProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesWithAdditionalProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0TitleFooAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/foo",
            "title",
            "{\r\n                \"#/additionalProperties\": \"Evaluated\"\r\n              }");
    }

    [Fact]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/bar",
            "title",
            "{\r\n                \"#/additionalProperties\": \"Evaluated\"\r\n              }");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\r\n        \"additionalProperties\": { \"title\": \"Evaluated\" },\r\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\r\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesWithDependentSchemas : IClassFixture<SuiteUnevaluatedPropertiesWithDependentSchemas.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesWithDependentSchemas(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0TitleFooAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/foo",
            "title",
            "{\r\n                \"#/unevaluatedProperties\": \"Unevaluated\"\r\n              }");
    }

    [Fact]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/bar",
            "title",
            "{\r\n                \"#/dependentSchemas/foo/properties/bar\": \"Evaluated\"\r\n              }");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\r\n        \"dependentSchemas\": {\r\n          \"foo\": {\r\n            \"properties\": {\r\n              \"bar\": { \"title\": \"Evaluated\" }\r\n            }\r\n          }\r\n        },\r\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\r\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesWithIfThenAndElse : IClassFixture<SuiteUnevaluatedPropertiesWithIfThenAndElse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesWithIfThenAndElse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0TitleFooAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{ \"foo\": \"\", \"bar\": 42 }",
            "/foo",
            "title",
            "{\r\n                \"#/then/properties/foo\": \"Then\",\r\n                \"#/if/properties/foo\": \"If\"\r\n              }");
    }

    [Fact]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{ \"foo\": \"\", \"bar\": 42 }",
            "/bar",
            "title",
            "{\r\n                \"#/unevaluatedProperties\": \"Unevaluated\"\r\n              }");
    }

    [Fact]
    public void Test1TitleFooAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{ \"foo\": 42, \"bar\": \"\" }",
            "/foo",
            "title",
            "{\r\n                \"#/else/properties/foo\": \"Else\"\r\n              }");
    }

    [Fact]
    public void Test1TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{ \"foo\": 42, \"bar\": \"\" }",
            "/bar",
            "title",
            "{\r\n                \"#/unevaluatedProperties\": \"Unevaluated\"\r\n              }");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\r\n        \"if\": {\r\n          \"properties\": {\r\n            \"foo\": {\r\n              \"type\": \"string\",\r\n              \"title\": \"If\"\r\n            }\r\n          }\r\n        },\r\n        \"then\": {\r\n          \"properties\": {\r\n            \"foo\": { \"title\": \"Then\" }\r\n          }\r\n        },\r\n        \"else\": {\r\n          \"properties\": {\r\n            \"foo\": { \"title\": \"Else\" }\r\n          }\r\n        },\r\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\r\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesWithAllOf : IClassFixture<SuiteUnevaluatedPropertiesWithAllOf.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesWithAllOf(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0TitleFooAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/foo",
            "title",
            "{\r\n                \"#/allOf/0/properties/foo\": \"Evaluated\"\r\n              }");
    }

    [Fact]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/bar",
            "title",
            "{\r\n                \"#/unevaluatedProperties\": \"Unevaluated\"\r\n              }");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\r\n        \"allOf\": [\r\n          {\r\n            \"properties\": {\r\n              \"foo\": { \"title\": \"Evaluated\" }\r\n            }\r\n          }\r\n        ],\r\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\r\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesWithAnyOf : IClassFixture<SuiteUnevaluatedPropertiesWithAnyOf.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesWithAnyOf(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0TitleFooAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/foo",
            "title",
            "{\r\n                \"#/anyOf/0/properties/foo\": \"Evaluated\"\r\n              }");
    }

    [Fact]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/bar",
            "title",
            "{\r\n                \"#/unevaluatedProperties\": \"Unevaluated\"\r\n              }");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\r\n        \"anyOf\": [\r\n          {\r\n            \"properties\": {\r\n              \"foo\": { \"title\": \"Evaluated\" }\r\n            }\r\n          }\r\n        ],\r\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\r\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesWithOneOf : IClassFixture<SuiteUnevaluatedPropertiesWithOneOf.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesWithOneOf(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0TitleFooAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/foo",
            "title",
            "{\r\n                \"#/oneOf/0/properties/foo\": \"Evaluated\"\r\n              }");
    }

    [Fact]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/bar",
            "title",
            "{\r\n                \"#/unevaluatedProperties\": \"Unevaluated\"\r\n              }");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\r\n        \"oneOf\": [\r\n          {\r\n            \"properties\": {\r\n              \"foo\": { \"title\": \"Evaluated\" }\r\n            }\r\n          }\r\n        ],\r\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\r\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft201909")]
public class SuiteUnevaluatedPropertiesWithNot : IClassFixture<SuiteUnevaluatedPropertiesWithNot.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedPropertiesWithNot(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0TitleFooAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/foo",
            "title",
            "{\r\n                \"#/unevaluatedProperties\": \"Unevaluated\"\r\n              }");
    }

    [Fact]
    public void Test0TitleBarAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{ \"foo\": 42, \"bar\": 24 }",
            "/bar",
            "title",
            "{\r\n                \"#/unevaluatedProperties\": \"Unevaluated\"\r\n              }");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\r\n        \"not\": {\r\n          \"not\": {\r\n            \"properties\": {\r\n              \"foo\": { \"title\": \"Evaluated\" }\r\n            }\r\n          }\r\n        },\r\n        \"unevaluatedProperties\": { \"title\": \"Unevaluated\" }\r\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft201909")]
public class SuiteUnevaluatedItemsAlone : IClassFixture<SuiteUnevaluatedItemsAlone.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnevaluatedItemsAlone(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0Title0Assertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "[42, 24]",
            "/0",
            "title",
            "{\r\n                \"#/unevaluatedItems\": \"Unevaluated\"\r\n              }");
    }

    [Fact]
    public void Test0Title1Assertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "[42, 24]",
            "/1",
            "title",
            "{\r\n                \"#/unevaluatedItems\": \"Unevaluated\"\r\n              }");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unevaluated.json",
                "{\r\n        \"unevaluatedItems\": { \"title\": \"Unevaluated\" }\r\n      }",
                "AnnotationTestSuite.Draft201909.Unevaluated",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
