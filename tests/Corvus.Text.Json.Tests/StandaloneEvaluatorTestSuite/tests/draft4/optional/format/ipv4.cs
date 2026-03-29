using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft4.Optional.Format.Ipv4;

[Trait("StandaloneEvaluatorTestSuite", "Draft4")]
public class SuiteValidationOfIpAddresses : IClassFixture<SuiteValidationOfIpAddresses.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfIpAddresses(Fixture fixture)
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
    public void TestAValidIpAddress()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"192.168.0.1\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnIpAddressWithTooManyComponents()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"127.0.0.0.1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnIpAddressWithOutOfRangeValues()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"256.256.256.256\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnIpAddressWithout4Components()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"127.0\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnIpAddressAsAnInteger()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"0x7f000001\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnIpAddressAsAnIntegerDecimal()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"2130706433\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidLeadingZeroesAsTheyAreTreatedAsOctals()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"087.10.0.1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValueWithoutLeadingZeroIsValid()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"87.10.0.1\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidNonAscii২ABengali2()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1২7.0.0.1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestNetmaskIsNotAPartOfIpv4Address()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"192.168.1.0/24\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft4\\optional\\format\\ipv4.json",
                "{ \"format\": \"ipv4\" }",
                "StandaloneEvaluatorTestSuite.Draft4.Optional.Format.Ipv4",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-04/schema#",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
