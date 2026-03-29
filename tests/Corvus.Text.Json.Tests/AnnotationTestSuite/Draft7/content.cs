using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace AnnotationTestSuite.Draft7.Content;

[Trait("AnnotationTestSuite", "Draft7")]
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
                "AnnotationTestSuite.Draft7.Content",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft7")]
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
                "AnnotationTestSuite.Draft7.Content",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
