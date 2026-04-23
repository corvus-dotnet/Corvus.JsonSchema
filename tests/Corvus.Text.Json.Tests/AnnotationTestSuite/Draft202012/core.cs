using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace AnnotationTestSuite.Draft202012.Core;

[Trait("AnnotationTestSuite", "Draft202012")]
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
                "AnnotationTestSuite.Draft202012.Core",
                "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft202012")]
public class SuiteDynamicRefResolvesToDynamicAnchor : IClassFixture<SuiteDynamicRefResolvesToDynamicAnchor.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteDynamicRefResolvesToDynamicAnchor(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0TitleRootAssertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "\"bar\"",
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
                "{\r\n        \"$dynamicRef\": \"#foo\",\r\n        \"$defs\": {\r\n          \"foo\": {\r\n            \"$dynamicAnchor\": \"foo\",\r\n            \"title\": \"Foo\"\r\n          }\r\n        }\r\n      }",
                "AnnotationTestSuite.Draft202012.Core",
                "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("AnnotationTestSuite", "Draft202012")]
public class SuiteDynamicRefResolvesToDifferentDynamicAnchorSDependingOnDynamicPath : IClassFixture<SuiteDynamicRefResolvesToDifferentDynamicAnchorSDependingOnDynamicPath.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteDynamicRefResolvesToDifferentDynamicAnchorSDependingOnDynamicPath(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test0TitleList0Assertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{ \"kindOfList\": \"numbers\", \"list\": [1] }",
            "/list/0",
            "title",
            "{\r\n                \"#/$defs/numberList/$defs/itemType\": \"Number Item\"\r\n              }");
    }

    [Fact]
    public void Test1TitleList0Assertion0()
    {
        AnnotationTestHelper.AssertAnnotations(
            _fixture.Evaluator,
            "{ \"kindOfList\": \"strings\", \"list\": [\"foo\"] }",
            "/list/0",
            "title",
            "{\r\n                \"#/$defs/stringList/$defs/itemType\": \"String Item\"\r\n              }");
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "annotations/core.json",
                "{\r\n        \"$id\": \"https://test.json-schema.org/dynamic-ref-annotation/main\",\r\n        \"if\": {\r\n          \"properties\": { \"kindOfList\": { \"const\": \"numbers\" } },\r\n          \"required\": [\"kindOfList\"]\r\n        },\r\n        \"then\": { \"$ref\": \"numberList\" },\r\n        \"else\": { \"$ref\": \"stringList\" },\r\n        \"$defs\": {\r\n          \"genericList\": {\r\n            \"$id\": \"genericList\",\r\n            \"properties\": {\r\n              \"list\": {\r\n                \"items\": { \"$dynamicRef\": \"#itemType\" }\r\n              }\r\n            },\r\n            \"$defs\": {\r\n              \"defaultItemType\": {\r\n                \"$dynamicAnchor\": \"itemType\"\r\n              }\r\n            }\r\n          },\r\n          \"numberList\": {\r\n            \"$id\": \"numberList\",\r\n            \"$defs\": {\r\n              \"itemType\": {\r\n                \"$dynamicAnchor\": \"itemType\",\r\n                \"title\": \"Number Item\"\r\n              }\r\n            },\r\n            \"$ref\": \"genericList\"\r\n          },\r\n          \"stringList\": {\r\n            \"$id\": \"stringList\",\r\n            \"$defs\": {\r\n              \"itemType\": {\r\n                \"$dynamicAnchor\": \"itemType\",\r\n                \"title\": \"String Item\"\r\n              }\r\n            },\r\n            \"$ref\": \"genericList\"\r\n          }\r\n        }\r\n      }",
                "AnnotationTestSuite.Draft202012.Core",
                "D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\JSON-Schema-Test-Suite\\remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
