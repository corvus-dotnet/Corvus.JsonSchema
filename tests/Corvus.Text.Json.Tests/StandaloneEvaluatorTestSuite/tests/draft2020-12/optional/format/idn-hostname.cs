using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft202012.Optional.Format.IdnHostname;

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteValidationOfInternationalizedHostNames : IClassFixture<SuiteValidationOfInternationalizedHostNames.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfInternationalizedHostNames(Fixture fixture)
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
    public void TestAValidHostNameExampleTestInHangul()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"실례.테스트\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIllegalFirstCharU302eHangulSingleDotToneMark()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"〮실례.테스트\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestContainsIllegalCharU302eHangulSingleDotToneMark()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"실〮례.테스트\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAHostNameWithAComponentTooLong()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실례례테스트례례례례례례례례례례례례례례례례례테스트례례례례례례례례례례례례례례례례례례례테스트례례례례례례례례례례례례테스트례례실례.테스트\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidLabelCorrectPunycode()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"-> $1.00 <--\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestValidChinesePunycode()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--ihqwcrb4cv8a8dqg056pqjye\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidPunycode()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"xn--X\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestULabelContainsInThe3rdAnd4thPosition()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"XN--aa---o47jg78q\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestULabelStartsWithADash()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"-hello\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestULabelEndsWithADash()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hello-\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestULabelStartsAndEndsWithADash()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"-hello-\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestBeginsWithASpacingCombiningMark()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0903hello\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestBeginsWithANonspacingMark()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0300hello\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestBeginsWithAnEnclosingMark()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0488hello\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestExceptionsThatArePvalidLeftToRightChars()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u00df\\u03c2\\u0f0b\\u3007\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestExceptionsThatArePvalidRightToLeftChars()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u06fd\\u06fe\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestExceptionsThatAreDisallowedRightToLeftChars()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0640\\u07fa\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestExceptionsThatAreDisallowedLeftToRightChars()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u3031\\u3032\\u3033\\u3034\\u3035\\u302e\\u302f\\u303b\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMiddleDotWithNoPrecedingL()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\\u00b7l\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMiddleDotWithNothingPreceding()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u00b7l\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMiddleDotWithNoFollowingL()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"l\\u00b7a\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMiddleDotWithNothingFollowing()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"l\\u00b7\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestMiddleDotWithSurroundingLS()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"l\\u00b7l\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestGreekKeraiaNotFollowedByGreek()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u03b1\\u0375S\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestGreekKeraiaNotFollowedByAnything()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u03b1\\u0375\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestGreekKeraiaFollowedByGreek()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u03b1\\u0375\\u03b2\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestHebrewGereshNotPrecededByHebrew()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"A\\u05f3\\u05d1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestHebrewGereshNotPrecededByAnything()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u05f3\\u05d1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestHebrewGereshPrecededByHebrew()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u05d0\\u05f3\\u05d1\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestHebrewGershayimNotPrecededByHebrew()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"A\\u05f4\\u05d1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestHebrewGershayimNotPrecededByAnything()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u05f4\\u05d1\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestHebrewGershayimPrecededByHebrew()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u05d0\\u05f4\\u05d1\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestKatakanaMiddleDotWithNoHiraganaKatakanaOrHan()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"def\\u30fbabc\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestKatakanaMiddleDotWithNoOtherCharacters()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u30fb\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestKatakanaMiddleDotWithHiragana()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u30fb\\u3041\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestKatakanaMiddleDotWithKatakana()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u30fb\\u30a1\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestKatakanaMiddleDotWithHan()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u30fb\\u4e08\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestArabicIndicDigitsMixedWithExtendedArabicIndicDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0628\\u0660\\u06f0\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestArabicIndicDigitsNotMixedWithExtendedArabicIndicDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0628\\u0660\\u0628\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestExtendedArabicIndicDigitsNotMixedWithArabicIndicDigits()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u06f00\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestZeroWidthJoinerNotPrecededByVirama()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0915\\u200d\\u0937\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestZeroWidthJoinerNotPrecededByAnything()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u200d\\u0937\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestZeroWidthJoinerPrecededByVirama()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0915\\u094d\\u200d\\u0937\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestZeroWidthNonJoinerPrecededByVirama()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0915\\u094d\\u200c\\u0937\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestZeroWidthNonJoinerNotPrecededByViramaButMatchesRegexp()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u0628\\u064a\\u200c\\u0628\\u064a\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSingleLabel()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hostname\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSingleLabelWithHyphen()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"host-name\"");
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

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\optional\\format\\idn-hostname.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"format\": \"idn-hostname\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.Format.IdnHostname",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
public class SuiteValidationOfSeparatorsInInternationalizedHostNames : IClassFixture<SuiteValidationOfSeparatorsInInternationalizedHostNames.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfSeparatorsInInternationalizedHostNames(Fixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void TestSingleDot()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\".\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSingleIdeographicFullStop()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u3002\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSingleFullwidthFullStop()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\uff0e\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestSingleHalfwidthIdeographicFullStop()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\uff61\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestDotAsLabelSeparator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a.b\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestIdeographicFullStopAsLabelSeparator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\\u3002b\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestFullwidthFullStopAsLabelSeparator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\\uff0eb\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestHalfwidthIdeographicFullStopAsLabelSeparator()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"a\\uff61b\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestLeadingDot()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\".example\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestLeadingIdeographicFullStop()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\u3002example\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestLeadingFullwidthFullStop()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\uff0eexample\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestLeadingHalfwidthIdeographicFullStop()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\uff61example\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTrailingDot()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"example.\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTrailingIdeographicFullStop()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"example\\u3002\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTrailingFullwidthFullStop()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"example\\uff0e\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestTrailingHalfwidthIdeographicFullStop()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"example\\uff61\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestLabelTooLongIfSeparatorIgnoredFullStop()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"παράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπα.com\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestLabelTooLongIfSeparatorIgnoredIdeographicFullStop()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"παράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπα\\u3002com\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestLabelTooLongIfSeparatorIgnoredFullwidthFullStop()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"παράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπα\\uff0ecom\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestLabelTooLongIfSeparatorIgnoredHalfwidthIdeographicFullStop()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"παράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπα\\uff61com\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\optional\\format\\idn-hostname.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"format\": \"idn-hostname\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.Format.IdnHostname",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
