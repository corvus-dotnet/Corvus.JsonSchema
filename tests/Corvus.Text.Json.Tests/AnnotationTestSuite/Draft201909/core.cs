using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace AnnotationTestSuite.Draft201909.Core;

[Trait("AnnotationTestSuite", "Draft201909")]
public class SuiteRefAndDefs : IClassFixture<SuiteRefAndDefs.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteRefAndDefs(Fixture fixture)
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
            "{\r\n                \"#/$defs/foo\": \"Foo\"\r\n              }");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/core.json",
                "{\r\n        \"$ref\": \"#/$defs/foo\",\r\n        \"$defs\": {\r\n          \"foo\": { \"title\": \"Foo\" }\r\n        }\r\n      }",
                "AnnotationTestSuite.Draft201909.Core",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
