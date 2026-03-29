using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace JsonSchemaTestSuite.Draft6.Optional.Format.Uri;

[Trait("JsonSchemaTestSuite", "Draft6")]
public class SuiteValidationOfUrIs : IClassFixture<SuiteValidationOfUrIs.Fixture>
{
    private readonly Fixture _fixture;
    public SuiteValidationOfUrIs(Fixture fixture)
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
    public void TestAValidUrlWithAnchorTag()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"http://foo.bar/?baz=qux#quux\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidUrlWithAnchorTagAndParentheses()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"http://foo.com/blah_(wikipedia)_blah#cite-1\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidUrlWithUrlEncodedStuff()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"http://foo.bar/?q=Test%20URL-encoded%20stuff\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidPunyCodedUrl()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"http://xn--nw2a.xn--j6w193g/\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidUrlWithManySpecialCharacters()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"http://-.~_!$&'()*+,;=:%40:80%2f::::::@example.com\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidUrlBasedOnIPv4()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"http://223.255.255.254\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidUrlWithFtpScheme()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"ftp://ftp.is.co.za/rfc/rfc1808.txt\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidUrlForASimpleTextFile()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"http://www.ietf.org/rfc/rfc2396.txt\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidUrl()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"ldap://[2001:db8::7]/c=GB?objectClass?one\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidMailtoUri()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"mailto:John.Doe@example.com\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidNewsgroupUri()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"news:comp.infosystems.www.servers.unix\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidTelUri()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"tel:+1-816-555-1212\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAValidUrn()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"urn:oasis:names:specification:docbook:dtd:xml:4.1.2\"");
        Assert.True(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidProtocolRelativeUriReference()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"//foo.bar/?baz=qux#quux\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidRelativeUriReference()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"/abc\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidUri()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"\\\\\\\\WINDOWS\\\\fileshare\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidUriThoughValidUriReference()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"abc\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidUriWithSpaces()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"http:// shouldfail.com\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidUriWithSpacesAndMissingScheme()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\":// should fail\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestAnInvalidUriWithCommaInScheme()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"bar,baz:foo\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidUserinfo()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"https://[@example.org/test.txt\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestUnescapedNonUsAsciiCharacters()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"https://example.org/foobar®.txt\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidBackslashCharacter()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"https://example.org/foobar\\\\.txt\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidCharacter()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"https://example.org/foobar\\\".txt\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidCharacters()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"https://example.org/foobar<>.txt\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidCharacters1()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"https://example.org/foobar{}.txt\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidCharacter1()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"https://example.org/foobar^.txt\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidCharacter2()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"https://example.org/foobar`.txt\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidSpaceCharacter()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"https://example.org/foo bar.txt\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    [Fact]
    public void TestInvalidCharacter3()
    {
        var dynamicInstance = _fixture.DynamicJsonType.ParseInstance("\"https://example.org/foobar|.txt\"");
        Assert.False(dynamicInstance.EvaluateSchema());
    }

    public class Fixture : IAsyncLifetime
    {
        public DynamicJsonType DynamicJsonType { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.DynamicJsonType = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
                "tests\\draft6\\optional\\format\\uri.json",
                "{ \"format\": \"uri\" }",
                "JsonSchemaTestSuite.Draft6.Optional.Format.Uri",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "http://json-schema.org/draft-06/schema#",
                validateFormat: true,
                optionalAsNullable: false,
                useImplicitOperatorString: false,
                addExplicitUsings: false,
                Assembly.GetExecutingAssembly());
        }
    }
}
