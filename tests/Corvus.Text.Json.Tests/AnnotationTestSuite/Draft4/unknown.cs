using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace AnnotationTestSuite.Draft4.Unknown;

[Trait("AnnotationTestSuite", "Draft4")]
public class SuiteUnknownKeywordIsAnAnnotation : IClassFixture<SuiteUnknownKeywordIsAnAnnotation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteUnknownKeywordIsAnAnnotation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0XUnknownKeywordRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "42",
            "",
            "x-unknownKeyword",
            "{\r\n                \"#\": \"Foo\"\r\n              }");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/unknown.json",
                "{\r\n        \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n        \"x-unknownKeyword\": \"Foo\"\r\n      }",
                "AnnotationTestSuite.Draft4.Unknown",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
