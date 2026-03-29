using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace AnnotationTestSuite.Draft4.Applicators;

[Trait("AnnotationTestSuite", "Draft4")]
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
                "AnnotationTestSuite.Draft4.Applicators",
                "D:\\source\\mwadams\\Corvus.Text.Json\\JSON-Schema-Test-Suite\\remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
