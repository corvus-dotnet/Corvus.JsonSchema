using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft202012.Optional.Format.EcmascriptRegex;

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteAIsNotAnEcma262ControlEscape : IClassFixture<SuiteAIsNotAnEcma262ControlEscape.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteAIsNotAnEcma262ControlEscape(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestWhenUsedAsAPattern()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\\a\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\optional\\format\\ecmascript-regex.json",
                "{\r\n      \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n      \"format\": \"regex\"\r\n    }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.Format.EcmascriptRegex",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
