using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft201909.Optional.Format.IdnHostname;

[Trait("JsonSchemaTestSuite", "Draft201909")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("12");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAllStringFormatsIgnoreFloats()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("13.7");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAllStringFormatsIgnoreObjects()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("{}");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAllStringFormatsIgnoreArrays()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("[]");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAllStringFormatsIgnoreBooleans()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("false");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAllStringFormatsIgnoreNulls()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("null");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidHostNameExampleTestInHangul()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"실례.테스트\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIllegalFirstCharU302eHangulSingleDotToneMark()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"〮실례.테스트\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestContainsIllegalCharU302eHangulSingleDotToneMark()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"실〮례.테스트\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAHostNameWithAComponentTooLong()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실례례테스트례례례례례례례례례례례례례례례례례테스트례례례례례례례례례례례례례례례례례례례테스트례례례례례례례례례례례례테스트례례실례.테스트\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidLabelCorrectPunycode()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"-> $1.00 <--\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestValidChinesePunycode()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--ihqwcrb4cv8a8dqg056pqjye\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidPunycode()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--X\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestULabelContainsInThe3rdAnd4thPosition()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"XN--aa---o47jg78q\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestULabelStartsWithADash()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"-hello\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestULabelEndsWithADash()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"hello-\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestULabelStartsAndEndsWithADash()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"-hello-\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestBeginsWithASpacingCombiningMark()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u0903hello\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestBeginsWithANonspacingMark()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u0300hello\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestBeginsWithAnEnclosingMark()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u0488hello\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestExceptionsThatArePvalidLeftToRightChars()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u00df\\u03c2\\u0f0b\\u3007\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestExceptionsThatArePvalidRightToLeftChars()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u06fd\\u06fe\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestExceptionsThatAreDisallowedRightToLeftChars()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u0640\\u07fa\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestExceptionsThatAreDisallowedLeftToRightChars()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u3031\\u3032\\u3033\\u3034\\u3035\\u302e\\u302f\\u303b\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMiddleDotWithNoPrecedingL()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"a\\u00b7l\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMiddleDotWithNothingPreceding()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u00b7l\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMiddleDotWithNoFollowingL()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"l\\u00b7a\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMiddleDotWithNothingFollowing()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"l\\u00b7\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMiddleDotWithSurroundingLS()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"l\\u00b7l\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestGreekKeraiaNotFollowedByGreek()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u03b1\\u0375S\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestGreekKeraiaNotFollowedByAnything()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u03b1\\u0375\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestGreekKeraiaFollowedByGreek()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u03b1\\u0375\\u03b2\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestHebrewGereshNotPrecededByHebrew()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"A\\u05f3\\u05d1\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestHebrewGereshNotPrecededByAnything()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u05f3\\u05d1\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestHebrewGereshPrecededByHebrew()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u05d0\\u05f3\\u05d1\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestHebrewGershayimNotPrecededByHebrew()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"A\\u05f4\\u05d1\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestHebrewGershayimNotPrecededByAnything()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u05f4\\u05d1\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestHebrewGershayimPrecededByHebrew()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u05d0\\u05f4\\u05d1\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestKatakanaMiddleDotWithNoHiraganaKatakanaOrHan()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"def\\u30fbabc\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestKatakanaMiddleDotWithNoOtherCharacters()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u30fb\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestKatakanaMiddleDotWithHiragana()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u30fb\\u3041\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestKatakanaMiddleDotWithKatakana()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u30fb\\u30a1\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestKatakanaMiddleDotWithHan()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u30fb\\u4e08\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestArabicIndicDigitsMixedWithExtendedArabicIndicDigits()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u0628\\u0660\\u06f0\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestArabicIndicDigitsNotMixedWithExtendedArabicIndicDigits()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u0628\\u0660\\u0628\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestExtendedArabicIndicDigitsNotMixedWithArabicIndicDigits()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u06f00\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestZeroWidthJoinerNotPrecededByVirama()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u0915\\u200d\\u0937\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestZeroWidthJoinerNotPrecededByAnything()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u200d\\u0937\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestZeroWidthJoinerPrecededByVirama()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u0915\\u094d\\u200d\\u0937\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestZeroWidthNonJoinerPrecededByVirama()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u0915\\u094d\\u200c\\u0937\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestZeroWidthNonJoinerNotPrecededByViramaButMatchesRegexp()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u0628\\u064a\\u200c\\u0628\\u064a\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestSingleLabel()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"hostname\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestSingleLabelWithHyphen()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"host-name\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestSingleLabelWithDigits()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"h0stn4me\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestSingleLabelStartingWithDigit()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"1host\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestSingleLabelEndingWithDigit()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"hostnam3\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestEmptyString()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\optional\\format\\idn-hostname.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"format\": \"idn-hostname\"\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Optional.Format.IdnHostname",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: true,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft201909")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\".\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestSingleIdeographicFullStop()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u3002\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestSingleFullwidthFullStop()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\uff0e\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestSingleHalfwidthIdeographicFullStop()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\uff61\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestDotAsLabelSeparator()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"a.b\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIdeographicFullStopAsLabelSeparator()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"a\\u3002b\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestFullwidthFullStopAsLabelSeparator()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"a\\uff0eb\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestHalfwidthIdeographicFullStopAsLabelSeparator()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"a\\uff61b\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestLeadingDot()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\".example\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestLeadingIdeographicFullStop()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\u3002example\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestLeadingFullwidthFullStop()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\uff0eexample\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestLeadingHalfwidthIdeographicFullStop()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\uff61example\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTrailingDot()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"example.\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTrailingIdeographicFullStop()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"example\\u3002\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTrailingFullwidthFullStop()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"example\\uff0e\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTrailingHalfwidthIdeographicFullStop()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"example\\uff61\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestLabelTooLongIfSeparatorIgnoredFullStop()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"παράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπα.com\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestLabelTooLongIfSeparatorIgnoredIdeographicFullStop()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"παράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπα\\u3002com\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestLabelTooLongIfSeparatorIgnoredFullwidthFullStop()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"παράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπα\\uff0ecom\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestLabelTooLongIfSeparatorIgnoredHalfwidthIdeographicFullStop()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"παράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπαράδειγμαπα\\uff61com\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft2019-09\\optional\\format\\idn-hostname.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2019-09/schema\",\r\n            \"format\": \"idn-hostname\"\r\n        }",
                "JsonSchemaTestSuite.Draft201909.Optional.Format.IdnHostname",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2019-09/schema",
                validateFormat: true,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
