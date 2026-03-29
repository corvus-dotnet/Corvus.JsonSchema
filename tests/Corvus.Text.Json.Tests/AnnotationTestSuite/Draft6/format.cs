using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace AnnotationTestSuite.Draft6.Format;

[Trait("AnnotationTestSuite", "Draft6")]
public class SuiteFormatIsAnAnnotation : IClassFixture<SuiteFormatIsAnAnnotation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteFormatIsAnAnnotation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0FormatRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "\"foo@bar.com\"",
            "",
            "format",
            "{\r\n                \"#\": \"email\"\r\n              }");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/format.json",
                "{\r\n        \"format\": \"email\"\r\n      }",
                "AnnotationTestSuite.Draft6.Format",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
