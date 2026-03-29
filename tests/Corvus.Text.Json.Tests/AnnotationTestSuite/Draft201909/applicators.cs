using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace AnnotationTestSuite.Draft201909.Applicators;

[Trait("AnnotationTestSuite", "Draft201909")]
public class SuitePropertiesPatternPropertiesAndAdditionalProperties : IClassFixture<SuitePropertiesPatternPropertiesAndAdditionalProperties.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePropertiesPatternPropertiesAndAdditionalProperties(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0TitleFooAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{}",
            "/foo",
            "title",
            "{}");
    }

    [Fact]
    public void Test0TitleAppleAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{}",
            "/apple",
            "title",
            "{}");
    }

    [Fact]
    public void Test0TitleBarAssertion2()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{}",
            "/bar",
            "title",
            "{}");
    }

    [Fact]
    public void Test1TitleFooAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{\r\n            \"foo\": {},\r\n            \"apple\": {},\r\n            \"baz\": {}\r\n          }",
            "/foo",
            "title",
            "{\r\n                \"#/properties/foo\": \"Foo\"\r\n              }");
    }

    [Fact]
    public void Test1TitleAppleAssertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{\r\n            \"foo\": {},\r\n            \"apple\": {},\r\n            \"baz\": {}\r\n          }",
            "/apple",
            "title",
            "{\r\n                \"#/patternProperties/%5Ea\": \"Bar\"\r\n              }");
    }

    [Fact]
    public void Test1TitleBazAssertion2()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{\r\n            \"foo\": {},\r\n            \"apple\": {},\r\n            \"baz\": {}\r\n          }",
            "/baz",
            "title",
            "{\r\n                \"#/additionalProperties\": \"Baz\"\r\n              }");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/applicators.json",
                "{\r\n        \"properties\": {\r\n          \"foo\": {\r\n            \"title\": \"Foo\"\r\n          }\r\n        },\r\n        \"patternProperties\": {\r\n          \"^a\": {\r\n            \"title\": \"Bar\"\r\n          }\r\n        },\r\n        \"additionalProperties\": {\r\n          \"title\": \"Baz\"\r\n        }\r\n      }",
                "AnnotationTestSuite.Draft201909.Applicators",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft201909")]
public class SuitePropertyNamesDoesnTAnnotatePropertyValues : IClassFixture<SuitePropertyNamesDoesnTAnnotatePropertyValues.Fixture>
{
    private readonly Fixture _fixture;
    public SuitePropertyNamesDoesnTAnnotatePropertyValues(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0TitleFooAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{\r\n            \"foo\": 42\r\n          }",
            "/foo",
            "title",
            "{}");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/applicators.json",
                "{\r\n        \"propertyNames\": {\r\n          \"const\": \"foo\",\r\n          \"title\": \"Foo\"\r\n        }\r\n      }",
                "AnnotationTestSuite.Draft201909.Applicators",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft201909")]
public class SuiteContains : IClassFixture<SuiteContains.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteContains(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0Title0Assertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "[\r\n            \"foo\",\r\n            42,\r\n            true\r\n          ]",
            "/0",
            "title",
            "{}");
    }

    [Fact]
    public void Test0Title1Assertion1()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "[\r\n            \"foo\",\r\n            42,\r\n            true\r\n          ]",
            "/1",
            "title",
            "{\r\n                \"#/contains\": \"Foo\"\r\n              }");
    }

    [Fact]
    public void Test0Title2Assertion2()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "[\r\n            \"foo\",\r\n            42,\r\n            true\r\n          ]",
            "/2",
            "title",
            "{}");
    }

    [Fact]
    public void Test0Title3Assertion3()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "[\r\n            \"foo\",\r\n            42,\r\n            true\r\n          ]",
            "/3",
            "title",
            "{}");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/applicators.json",
                "{\r\n        \"contains\": {\r\n          \"type\": \"number\",\r\n          \"title\": \"Foo\"\r\n        }\r\n      }",
                "AnnotationTestSuite.Draft201909.Applicators",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft201909")]
public class SuiteAllOf : IClassFixture<SuiteAllOf.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAllOf(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0TitleRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "\"foo\"",
            "",
            "title",
            "{\r\n                \"#/allOf/1\": \"Bar\",\r\n                \"#/allOf/0\": \"Foo\"\r\n              }");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/applicators.json",
                "{\r\n        \"allOf\": [\r\n          {\r\n            \"title\": \"Foo\"\r\n          },\r\n          {\r\n            \"title\": \"Bar\"\r\n          }\r\n        ]\r\n      }",
                "AnnotationTestSuite.Draft201909.Applicators",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft201909")]
public class SuiteAnyOf : IClassFixture<SuiteAnyOf.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAnyOf(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0TitleRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "42",
            "",
            "title",
            "{\r\n                \"#/anyOf/1\": \"Bar\",\r\n                \"#/anyOf/0\": \"Foo\"\r\n              }");
    }

    [Fact]
    public void Test1TitleRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "4.2",
            "",
            "title",
            "{\r\n                \"#/anyOf/1\": \"Bar\"\r\n              }");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/applicators.json",
                "{\r\n        \"anyOf\": [\r\n          {\r\n            \"type\": \"integer\",\r\n            \"title\": \"Foo\"\r\n          },\r\n          {\r\n            \"type\": \"number\",\r\n            \"title\": \"Bar\"\r\n          }\r\n        ]\r\n      }",
                "AnnotationTestSuite.Draft201909.Applicators",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft201909")]
public class SuiteOneOf : IClassFixture<SuiteOneOf.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteOneOf(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0TitleRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "\"foo\"",
            "",
            "title",
            "{\r\n                \"#/oneOf/0\": \"Foo\"\r\n              }");
    }

    [Fact]
    public void Test1TitleRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "42",
            "",
            "title",
            "{\r\n                \"#/oneOf/1\": \"Bar\"\r\n              }");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/applicators.json",
                "{\r\n        \"oneOf\": [\r\n          {\r\n            \"type\": \"string\",\r\n            \"title\": \"Foo\"\r\n          },\r\n          {\r\n            \"type\": \"number\",\r\n            \"title\": \"Bar\"\r\n          }\r\n        ]\r\n      }",
                "AnnotationTestSuite.Draft201909.Applicators",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft201909")]
public class SuiteNot : IClassFixture<SuiteNot.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteNot(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0TitleRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{}",
            "",
            "title",
            "{\r\n                \"#\": \"Foo\"\r\n              }");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/applicators.json",
                "{\r\n        \"title\": \"Foo\",\r\n        \"not\": {\r\n          \"not\": {\r\n            \"title\": \"Bar\"\r\n          }\r\n        }\r\n      }",
                "AnnotationTestSuite.Draft201909.Applicators",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft201909")]
public class SuiteDependentSchemas : IClassFixture<SuiteDependentSchemas.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteDependentSchemas(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0TitleRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{\r\n            \"foo\": 42\r\n          }",
            "",
            "title",
            "{\r\n                \"#/dependentSchemas/foo\": \"Foo\"\r\n              }");
    }

    [Fact]
    public void Test1TitleFooAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{\r\n            \"foo\": 42\r\n          }",
            "/foo",
            "title",
            "{}");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/applicators.json",
                "{\r\n        \"dependentSchemas\": {\r\n          \"foo\": {\r\n            \"title\": \"Foo\"\r\n          }\r\n        }\r\n      }",
                "AnnotationTestSuite.Draft201909.Applicators",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft201909")]
public class SuiteIfThenAndElse : IClassFixture<SuiteIfThenAndElse.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteIfThenAndElse(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0TitleRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "\"foo\"",
            "",
            "title",
            "{\r\n                \"#/then\": \"Then\",\r\n                \"#/if\": \"If\"\r\n              }");
    }

    [Fact]
    public void Test1TitleRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "42",
            "",
            "title",
            "{\r\n                \"#/else\": \"Else\"\r\n              }");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/applicators.json",
                "{\r\n        \"if\": {\r\n          \"title\": \"If\",\r\n          \"type\": \"string\"\r\n        },\r\n        \"then\": {\r\n          \"title\": \"Then\"\r\n        },\r\n        \"else\": {\r\n          \"title\": \"Else\"\r\n        }\r\n      }",
                "AnnotationTestSuite.Draft201909.Applicators",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
