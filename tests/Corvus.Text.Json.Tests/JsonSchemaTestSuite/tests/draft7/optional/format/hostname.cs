using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft7.Optional.Format.Hostname;

[Trait("JsonSchemaTestSuite", "Draft7")]
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
    public void TestAValidHostName()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"www.example.com\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestSingleLabel()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"hostname\"");
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

    [Fact]
    public void TestSingleDot()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\".\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestLeadingDot()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\".example\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestTrailingDot()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"example.\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestIdnLabelSeparator()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"example\\uff0ecom\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestSingleLabelWithHyphen()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"host-name\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestStartsWithHyphen()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"-hostname\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestEndsWithHyphen()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"hostname-\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestContainsInThe3rdAnd4thPosition()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"XN--aa---o47jg78q\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestContainsUnderscore()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"host_name\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestExceedsMaximumOverallLength256()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.com\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMaximumLabelLength63()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.com\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestExceedsMaximumLabelLength63()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijkl.com\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\optional\\format\\hostname.json",
                "{ \"format\": \"hostname\" }",
                "JsonSchemaTestSuite.Draft7.Optional.Format.Hostname",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: true,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}

[Trait("JsonSchemaTestSuite", "Draft7")]
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
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--X\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidHostNameExampleTestInHangul()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--9n2bp8q.xn--9t4b11yi5a\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestContainsIllegalCharU302eHangulSingleDotToneMark()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--07jt112bpxg.xn--9t4b11yi5a\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestBeginsWithASpacingCombiningMark()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--hello-txk\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestBeginsWithANonspacingMark()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--hello-zed\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestBeginsWithAnEnclosingMark()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--hello-6bf\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestExceptionsThatArePvalidLeftToRightChars()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--zca29lwxobi7a\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestExceptionsThatArePvalidRightToLeftChars()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--qmbc\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestExceptionsThatAreDisallowedRightToLeftChars()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--chb89f\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestExceptionsThatAreDisallowedLeftToRightChars()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--07jceefgh4c\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMiddleDotWithNoPrecedingL()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--al-0ea\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMiddleDotWithNothingPreceding()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--l-fda\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMiddleDotWithNoFollowingL()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--la-0ea\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMiddleDotWithNothingFollowing()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--l-gda\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestMiddleDotWithSurroundingLS()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--ll-0ea\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestGreekKeraiaNotFollowedByGreek()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--S-jib3p\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestGreekKeraiaNotFollowedByAnything()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--wva3j\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestGreekKeraiaFollowedByGreek()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--wva3je\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestHebrewGereshNotPrecededByHebrew()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--A-2hc5h\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestHebrewGereshNotPrecededByAnything()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--5db1e\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestHebrewGereshPrecededByHebrew()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--4dbc5h\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestHebrewGershayimNotPrecededByHebrew()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--A-2hc8h\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestHebrewGershayimNotPrecededByAnything()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--5db3e\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestHebrewGershayimPrecededByHebrew()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--4dbc8h\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestKatakanaMiddleDotWithNoHiraganaKatakanaOrHan()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--defabc-k64e\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestKatakanaMiddleDotWithNoOtherCharacters()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--vek\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestKatakanaMiddleDotWithHiragana()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--k8j5u\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestKatakanaMiddleDotWithKatakana()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--bck0j\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestKatakanaMiddleDotWithHan()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--vek778f\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestArabicIndicDigitsMixedWithExtendedArabicIndicDigits()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--ngb6iyr\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestArabicIndicDigitsNotMixedWithExtendedArabicIndicDigits()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--ngba1o\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestExtendedArabicIndicDigitsNotMixedWithArabicIndicDigits()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--0-gyc\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestZeroWidthJoinerNotPrecededByVirama()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--11b2er09f\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestZeroWidthJoinerNotPrecededByAnything()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--02b508i\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestZeroWidthJoinerPrecededByVirama()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--11b2ezcw70k\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestZeroWidthNonJoinerPrecededByVirama()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--11b2ezcs70k\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestZeroWidthNonJoinerNotPrecededByViramaButMatchesRegexp()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"xn--ngba5hb2804a\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft7\\optional\\format\\hostname.json",
                "{ \"format\": \"hostname\" }",
                "JsonSchemaTestSuite.Draft7.Optional.Format.Hostname",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-07/schema#",
                validateFormat: true,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
