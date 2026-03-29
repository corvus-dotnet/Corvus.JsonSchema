using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft201909.Optional.Format.Hostname;

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteValidationOfHostNames : IClassFixture<SuiteValidationOfHostNames.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfHostNames(Fixture fixture)
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
    public void TestAValidHostName()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"www.example.com\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSingleLabel()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hostname\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSingleLabelWithDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"h0stn4me\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSingleLabelStartingWithDigit()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"1host\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSingleLabelEndingWithDigit()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hostnam3\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestEmptyString()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSingleDot()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\".\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestLeadingDot()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\".example\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTrailingDot()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"example.\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIdnLabelSeparator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"example\\uff0ecom\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSingleLabelWithHyphen()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"host-name\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestStartsWithHyphen()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"-hostname\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestEndsWithHyphen()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hostname-\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestContainsInThe3rdAnd4thPosition()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"XN--aa---o47jg78q\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestContainsUnderscore()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"host_name\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestExceedsMaximumOverallLength256()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.com\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMaximumLabelLength63()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.com\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestExceedsMaximumLabelLength63()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijkl.com\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\optional\\format\\hostname.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"format\": \"hostname\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.Format.Hostname",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft201909")]
public class SuiteValidationOfALabelPunycodeHostNames : IClassFixture<SuiteValidationOfALabelPunycodeHostNames.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfALabelPunycodeHostNames(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestInvalidPunycode()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--X\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidHostNameExampleTestInHangul()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--9n2bp8q.xn--9t4b11yi5a\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestContainsIllegalCharU302eHangulSingleDotToneMark()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--07jt112bpxg.xn--9t4b11yi5a\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestBeginsWithASpacingCombiningMark()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--hello-txk\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestBeginsWithANonspacingMark()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--hello-zed\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestBeginsWithAnEnclosingMark()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--hello-6bf\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestExceptionsThatArePvalidLeftToRightChars()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--zca29lwxobi7a\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestExceptionsThatArePvalidRightToLeftChars()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--qmbc\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestExceptionsThatAreDisallowedRightToLeftChars()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--chb89f\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestExceptionsThatAreDisallowedLeftToRightChars()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--07jceefgh4c\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMiddleDotWithNoPrecedingL()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--al-0ea\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMiddleDotWithNothingPreceding()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--l-fda\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMiddleDotWithNoFollowingL()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--la-0ea\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMiddleDotWithNothingFollowing()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--l-gda\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMiddleDotWithSurroundingLS()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--ll-0ea\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestGreekKeraiaNotFollowedByGreek()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--S-jib3p\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestGreekKeraiaNotFollowedByAnything()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--wva3j\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestGreekKeraiaFollowedByGreek()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--wva3je\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestHebrewGereshNotPrecededByHebrew()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--A-2hc5h\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestHebrewGereshNotPrecededByAnything()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--5db1e\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestHebrewGereshPrecededByHebrew()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--4dbc5h\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestHebrewGershayimNotPrecededByHebrew()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--A-2hc8h\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestHebrewGershayimNotPrecededByAnything()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--5db3e\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestHebrewGershayimPrecededByHebrew()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--4dbc8h\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestKatakanaMiddleDotWithNoHiraganaKatakanaOrHan()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--defabc-k64e\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestKatakanaMiddleDotWithNoOtherCharacters()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--vek\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestKatakanaMiddleDotWithHiragana()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--k8j5u\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestKatakanaMiddleDotWithKatakana()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--bck0j\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestKatakanaMiddleDotWithHan()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--vek778f\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestArabicIndicDigitsMixedWithExtendedArabicIndicDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--ngb6iyr\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestArabicIndicDigitsNotMixedWithExtendedArabicIndicDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--ngba1o\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestExtendedArabicIndicDigitsNotMixedWithArabicIndicDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--0-gyc\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestZeroWidthJoinerNotPrecededByVirama()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--11b2er09f\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestZeroWidthJoinerNotPrecededByAnything()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--02b508i\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestZeroWidthJoinerPrecededByVirama()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--11b2ezcw70k\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestZeroWidthNonJoinerPrecededByVirama()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--11b2ezcs70k\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestZeroWidthNonJoinerNotPrecededByViramaButMatchesRegexp()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--ngba5hb2804a\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2019-09\\optional\\format\\hostname.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"format\": \"hostname\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft201909.Optional.Format.Hostname",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
