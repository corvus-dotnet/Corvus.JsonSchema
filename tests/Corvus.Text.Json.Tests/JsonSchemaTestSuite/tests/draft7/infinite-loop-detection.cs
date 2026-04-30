using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft7.InfiniteLoopDetection;

[Trait("JsonSchemaTestSuite", "Draft7")]
public class SuiteEvaluatingTheSameSchemaLocationAgainstTheSameDataLocationTwiceIsNotASignOfAnInfiniteLoop : IClassFixture<SuiteEvaluatingTheSameSchemaLocationAgainstTheSameDataLocationTwiceIsNotASignOfAnInfiniteLoop.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteEvaluatingTheSameSchemaLocationAgainstTheSameDataLocationTwiceIsNotASignOfAnInfiniteLoop(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestPassingCase()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo\": 1 }");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestFailingCase()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{ \"foo\": \"a string\" }");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\infinite-loop-detection.json",
                "{\r\n            \"definitions\": {\r\n                \"int\": { \"type\": \"integer\" }\r\n            },\r\n            \"allOf\": [\r\n                {\r\n                    \"properties\": {\r\n                        \"foo\": {\r\n                            \"$ref\": \"#/definitions/int\"\r\n                        }\r\n                    }\r\n                },\r\n                {\r\n                    \"additionalProperties\": {\r\n                        \"$ref\": \"#/definitions/int\"\r\n                    }\r\n                }\r\n            ]\r\n        }",
                "JsonSchemaTestSuite.Draft7.InfiniteLoopDetection",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: false,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
