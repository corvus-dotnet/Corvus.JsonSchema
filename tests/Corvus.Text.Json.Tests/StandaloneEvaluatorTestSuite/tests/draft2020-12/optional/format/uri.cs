using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json;
using TestUtilities;
using Xunit;

namespace StandaloneEvaluatorTestSuite.Draft202012.Optional.Format.Uri;

[Trait("StandaloneEvaluatorTestSuite", "Draft202012")]
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
    public void TestAValidUrlWithAnchorTag()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://foo.bar/?baz=qux#quux\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidUrlWithAnchorTagAndParentheses()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://foo.com/blah_(wikipedia)_blah#cite-1\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidUrlWithUrlEncodedStuff()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://foo.bar/?q=Test%20URL-encoded%20stuff\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidPunyCodedUrl()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://xn--nw2a.xn--j6w193g/\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidUrlWithManySpecialCharacters()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://-.~_!$&'()*+,;=:%40:80%2f::::::@example.com\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidUrlBasedOnIPv4()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://223.255.255.254\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidUrlWithFtpScheme()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"ftp://ftp.is.co.za/rfc/rfc1808.txt\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidUrlForASimpleTextFile()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http://www.ietf.org/rfc/rfc2396.txt\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidUrl()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"ldap://[2001:db8::7]/c=GB?objectClass?one\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidMailtoUri()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"mailto:John.Doe@example.com\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidNewsgroupUri()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"news:comp.infosystems.www.servers.unix\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidTelUri()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"tel:+1-816-555-1212\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAValidUrn()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"urn:oasis:names:specification:docbook:dtd:xml:4.1.2\"");
        Assert.True(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidProtocolRelativeUriReference()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"//foo.bar/?baz=qux#quux\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidRelativeUriReference()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"/abc\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidUri()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"\\\\\\\\WINDOWS\\\\fileshare\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidUriThoughValidUriReference()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"abc\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidUriWithSpaces()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"http:// shouldfail.com\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidUriWithSpacesAndMissingScheme()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\":// should fail\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestAnInvalidUriWithCommaInScheme()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"bar,baz:foo\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidUserinfo()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"https://[@example.org/test.txt\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestUnescapedNonUsAsciiCharacters()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"https://example.org/foobar®.txt\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidBackslashCharacter()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"https://example.org/foobar\\\\.txt\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidCharacter()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"https://example.org/foobar\\\".txt\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidCharacters()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"https://example.org/foobar<>.txt\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidCharacters1()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"https://example.org/foobar{}.txt\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidCharacter1()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"https://example.org/foobar^.txt\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidCharacter2()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"https://example.org/foobar`.txt\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidSpaceCharacter()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"https://example.org/foo bar.txt\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    [Fact]
    public void TestInvalidCharacter3()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"https://example.org/foobar|.txt\"");
        Assert.False(_fixture.Evaluator.Evaluate(doc.RootElement));
    }

    public class Fixture : IAsyncLifetime
    {
        public CompiledEvaluator Evaluator { get; private set; }

        public Task DisposeAsync() => Task.CompletedTask;

        public async Task InitializeAsync()
        {
            this.Evaluator = await TestEvaluatorHelper.GenerateEvaluatorForVirtualFileAsync(
                "tests\\draft2020-12\\optional\\format\\uri.json",
                "{\r\n            \"$schema\": \"https://json-schema.org/draft/2020-12/schema\",\r\n            \"format\": \"uri\"\r\n        }",
                "StandaloneEvaluatorTestSuite.Draft202012.Optional.Format.Uri",
                "../../../../../JSON-Schema-Test-Suite/remotes",
                "https://json-schema.org/draft/2020-12/schema",
                validateFormat: true,
                Assembly.GetExecutingAssembly());
        }
    }
}
