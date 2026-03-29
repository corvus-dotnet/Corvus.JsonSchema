using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace AnnotationTestSuite.Draft202012.MetaData;

[Trait("AnnotationTestSuite", "Draft202012")]
public class SuiteTitleIsAnAnnotation : IClassFixture<SuiteTitleIsAnAnnotation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteTitleIsAnAnnotation(Fixture fixture)
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
            "{\r\n                \"#\": \"Foo\"\r\n              }");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/meta-data.json",
                "{\r\n        \"title\": \"Foo\"\r\n      }",
                "AnnotationTestSuite.Draft202012.MetaData",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft202012")]
public class SuiteDescriptionIsAnAnnotation : IClassFixture<SuiteDescriptionIsAnAnnotation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteDescriptionIsAnAnnotation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0DescriptionRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "42",
            "",
            "description",
            "{\r\n                \"#\": \"Foo\"\r\n              }");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/meta-data.json",
                "{\r\n        \"description\": \"Foo\"\r\n      }",
                "AnnotationTestSuite.Draft202012.MetaData",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft202012")]
public class SuiteDefaultIsAnAnnotation : IClassFixture<SuiteDefaultIsAnAnnotation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteDefaultIsAnAnnotation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0DefaultRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "42",
            "",
            "default",
            "{\r\n                \"#\": \"Foo\"\r\n              }");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/meta-data.json",
                "{\r\n        \"default\": \"Foo\"\r\n      }",
                "AnnotationTestSuite.Draft202012.MetaData",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft202012")]
public class SuiteDeprecatedIsAnAnnotation : IClassFixture<SuiteDeprecatedIsAnAnnotation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteDeprecatedIsAnAnnotation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0DeprecatedRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "42",
            "",
            "deprecated",
            "{\r\n                \"#\": true\r\n              }");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/meta-data.json",
                "{\r\n        \"deprecated\": true\r\n      }",
                "AnnotationTestSuite.Draft202012.MetaData",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft202012")]
public class SuiteReadOnlyIsAnAnnotation : IClassFixture<SuiteReadOnlyIsAnAnnotation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteReadOnlyIsAnAnnotation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0ReadOnlyRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "42",
            "",
            "readOnly",
            "{\r\n                \"#\": true\r\n              }");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/meta-data.json",
                "{\r\n        \"readOnly\": true\r\n      }",
                "AnnotationTestSuite.Draft202012.MetaData",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft202012")]
public class SuiteWriteOnlyIsAnAnnotation : IClassFixture<SuiteWriteOnlyIsAnAnnotation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteWriteOnlyIsAnAnnotation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0WriteOnlyRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "42",
            "",
            "writeOnly",
            "{\r\n                \"#\": true\r\n              }");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/meta-data.json",
                "{\r\n        \"writeOnly\": true\r\n      }",
                "AnnotationTestSuite.Draft202012.MetaData",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft202012")]
public class SuiteExamplesIsAnAnnotation : IClassFixture<SuiteExamplesIsAnAnnotation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteExamplesIsAnAnnotation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0ExamplesRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "\"Foo\"",
            "",
            "examples",
            "{\r\n                \"#\": [\"Foo\", \"Bar\"]\r\n              }");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/meta-data.json",
                "{\r\n        \"examples\": [\"Foo\", \"Bar\"]\r\n      }",
                "AnnotationTestSuite.Draft202012.MetaData",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
