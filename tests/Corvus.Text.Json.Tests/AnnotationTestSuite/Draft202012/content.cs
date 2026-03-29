using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace AnnotationTestSuite.Draft202012.Content;

[Trait("AnnotationTestSuite", "Draft202012")]
public class SuiteContentMediaTypeIsAnAnnotationForStringInstances : IClassFixture<SuiteContentMediaTypeIsAnAnnotationForStringInstances.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteContentMediaTypeIsAnAnnotationForStringInstances(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0ContentMediaTypeRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "\"{ \\\"foo\\\": \\\"bar\\\" }\"",
            "",
            "contentMediaType",
            "{\r\n                \"#\": \"application/json\"\r\n              }");
    }

    [Fact]
    public void Test1ContentMediaTypeRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "42",
            "",
            "contentMediaType",
            "{}");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/content.json",
                "{\r\n        \"contentMediaType\": \"application/json\"\r\n      }",
                "AnnotationTestSuite.Draft202012.Content",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft202012")]
public class SuiteContentEncodingIsAnAnnotationForStringInstances : IClassFixture<SuiteContentEncodingIsAnAnnotationForStringInstances.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteContentEncodingIsAnAnnotationForStringInstances(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0ContentEncodingRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "\"SGVsbG8gZnJvbSBKU09OIFNjaGVtYQ==\"",
            "",
            "contentEncoding",
            "{\r\n                \"#\": \"base64\"\r\n              }");
    }

    [Fact]
    public void Test1ContentEncodingRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "42",
            "",
            "contentEncoding",
            "{}");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/content.json",
                "{\r\n        \"contentEncoding\": \"base64\"\r\n      }",
                "AnnotationTestSuite.Draft202012.Content",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft202012")]
public class SuiteContentSchemaIsAnAnnotationForStringInstances : IClassFixture<SuiteContentSchemaIsAnAnnotationForStringInstances.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteContentSchemaIsAnAnnotationForStringInstances(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0ContentSchemaRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "\"42\"",
            "",
            "contentSchema",
            "{\r\n                \"#\": { \"type\": \"number\" }\r\n              }");
    }

    [Fact]
    public void Test1ContentSchemaRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "42",
            "",
            "contentSchema",
            "{}");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/content.json",
                "{\r\n        \"contentMediaType\": \"application/json\",\r\n        \"contentSchema\": { \"type\": \"number\" }\r\n      }",
                "AnnotationTestSuite.Draft202012.Content",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft202012")]
public class SuiteContentSchemaRequiresContentMediaType : IClassFixture<SuiteContentSchemaRequiresContentMediaType.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteContentSchemaRequiresContentMediaType(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0ContentSchemaRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "\"42\"",
            "",
            "contentSchema",
            "{}");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/content.json",
                "{\r\n        \"contentSchema\": { \"type\": \"number\" }\r\n      }",
                "AnnotationTestSuite.Draft202012.Content",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
