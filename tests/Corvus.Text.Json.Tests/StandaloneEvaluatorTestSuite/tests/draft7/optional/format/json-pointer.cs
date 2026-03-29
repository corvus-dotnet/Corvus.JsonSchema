using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft7.Optional.Format.JsonPointer;

[Trait("StandaloneEvaluatorTestSuite", "Draft7")]
public class SuiteValidationOfJsonPointersJsonStringRepresentation : IClassFixture<SuiteValidationOfJsonPointersJsonStringRepresentation.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfJsonPointersJsonStringRepresentation(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestAllStringFormatsIgnoreIntegers()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("12");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllStringFormatsIgnoreFloats()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("13.7");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllStringFormatsIgnoreObjects()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllStringFormatsIgnoreArrays()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllStringFormatsIgnoreBooleans()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("false");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAllStringFormatsIgnoreNulls()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("null");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidJsonPointer()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/foo/bar~0/baz~1/%a\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNotAValidJsonPointerNotEscaped()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/foo/bar~\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidJsonPointerWithEmptySegment()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/foo//bar\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidJsonPointerWithTheLastEmptySegment()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/foo/bar/\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidJsonPointerAsStatedInRfc69011()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidJsonPointerAsStatedInRfc69012()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/foo\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidJsonPointerAsStatedInRfc69013()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/foo/0\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidJsonPointerAsStatedInRfc69014()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidJsonPointerAsStatedInRfc69015()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/a~1b\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidJsonPointerAsStatedInRfc69016()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/c%d\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidJsonPointerAsStatedInRfc69017()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/e^f\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidJsonPointerAsStatedInRfc69018()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/g|h\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidJsonPointerAsStatedInRfc69019()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/i\\\\j\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidJsonPointerAsStatedInRfc690110()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/k\\\"l\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidJsonPointerAsStatedInRfc690111()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/ \"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidJsonPointerAsStatedInRfc690112()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/m~0n\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidJsonPointerUsedAddingToTheLastArrayPosition()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/foo/-\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidJsonPointerUsedAsObjectMemberName()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/foo/-/bar\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidJsonPointerMultipleEscapedCharacters()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/~1~0~0~1~1\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidJsonPointerEscapedWithFractionPart1()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/~1.1\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidJsonPointerEscapedWithFractionPart2()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/~0.1\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNotAValidJsonPointerUriFragmentIdentifier1()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"#\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNotAValidJsonPointerUriFragmentIdentifier2()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"#/\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNotAValidJsonPointerUriFragmentIdentifier3()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"#a\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNotAValidJsonPointerSomeEscapedButNotAll1()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/~0~\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNotAValidJsonPointerSomeEscapedButNotAll2()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/~0/~\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNotAValidJsonPointerWrongEscapeCharacter1()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/~2\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNotAValidJsonPointerWrongEscapeCharacter2()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/~-1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNotAValidJsonPointerMultipleCharactersNotEscaped()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/~~\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNotAValidJsonPointerIsnTEmptyNorStartsWith1()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNotAValidJsonPointerIsnTEmptyNorStartsWith2()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"0\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNotAValidJsonPointerIsnTEmptyNorStartsWith3()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a/a\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft7\\optional\\format\\json-pointer.json",
                "{ \"format\": \"json-pointer\" }",
                "StandaloneEvaluatorTestSuite.Draft7.Optional.Format.JsonPointer",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
