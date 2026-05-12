// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public class JsonSchemaMatchingStringTests
{
    [TestMethod]
    [DataRow("2023-06-01", true)]
    [DataRow("not-a-date", false)]
    public void MatchDate_ValidatesDate(string value, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchDate(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow("2023-06-01T12:34:56+00:00", true)]
    [DataRow("not-a-datetime", false)]
    public void MatchDateTime_ValidatesDateTime(string value, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchDateTime(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow("P1D", true)] // ISO 8601 duration
    [DataRow("not-a-duration", false)]
    public void MatchDuration_ValidatesDuration(string value, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchDuration(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow("user@example.com", true)]
    [DataRow("(allows leading comment)user@example.com", true)]
    [DataRow("user(allows trailing comment)@example.com", true)]
    [DataRow("(allows leading comment)user(and allows trailing comment)@example.com", true)]
    [DataRow("(user@example.com", false)]
    [DataRow("user(akdjsd@example.com", false)]
    [DataRow("u:ser@example.com", false)]
    [DataRow("invalid-email", false)]
    [DataRow("用户@例子.广告", false)]
    public void MatchEmail_ValidatesEmail(string value, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchEmail(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow("example.com", true)]
    [DataRow("sub.domain.example.com", true)]
    [DataRow("xn--4gbwdl.xn--wgbh1c", true)] // Punycode
    [DataRow("xn--X", false)] // invalid punycode
    [DataRow("-a-host-name-that-starts-with--", false)]
    [DataRow("not_a_valid_host_name", false)]
    [DataRow("a-vvvvvvvvvvvvvvvveeeeeeeeeeeeeeeerrrrrrrrrrrrrrrryyyyyyyyyyyyyyyy-long-host-name-component", false)]
    [DataRow("-hostname", false)]
    [DataRow("hostname-", false)]
    [DataRow("_hostname", false)]
    [DataRow("hostname_", false)]
    [DataRow("host_name", false)]
    [DataRow("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.com", true)]
    [DataRow("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijkl.com", false)]
    [DataRow("hostname", true)]
    [DataRow("host-name", true)]
    [DataRow("h0stn4me", true)]
    [DataRow("1host", true)]
    [DataRow("hostnam3", true)]
    [DataRow("실례.테스트", false)]
    public void MatchHostname_ValidatesHostname(string value, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchHostname(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow("用户@例子.广告", true)]
    [DataRow("(allows leading comment)用户@例子.广告", true)]
    [DataRow("用户(allows trailing comment)@例子.广告", true)]
    [DataRow("(allows leading comment)用户(and allows trailing comment)@例子.广告", true)]
    [DataRow("(用户@例子.广告", false)]
    [DataRow("用户(sdk@例子.广告", false)]
    [DataRow("ಬೆಂಬಲ@ಡೇಟಾಮೇಲ್.ಭಾರತ", true)]
    [DataRow("अजय@डाटा.भारत", true)]
    [DataRow("квіточка@пошта.укр", true)]
    [DataRow("χρήστης@παράδειγμα.ελ", true)]
    [DataRow("Dörte@Sörensen.example.com", true)]
    [DataRow("مثال@موقع.عر", true)]
    [DataRow("jo@\u0640\u07fa", false)]
    public void MatchIdnEmail_ValidatesIdnEmail(string value, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchIdnEmail(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow("실례.테스트", true)]
    [DataRow("〮실례.테스트", false)]
    [DataRow("실〮례.테스트", false)]
    [DataRow("실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실례례테스트례례례례례례례례례례례례례례례례례테스트례례례례례례례례례례례례례례례례례례례테스트례례례례례례례례례례례례테스트례례실례.테스트", false)]
    [DataRow("-> $1.00 <--", false)]
    [DataRow("xn--ihqwcrb4cv8a8dqg056pqjye", true)]
    [DataRow("xn--X", false)]
    [DataRow("XN--aa---o47jg78q", false)]
    [DataRow("-hello", false)]
    [DataRow("hello-", false)]
    [DataRow("-hello-", false)]
    [DataRow("\u0903hello", false)]
    [DataRow("\u0300hello", false)]
    [DataRow("\u0488hello", false)]
    [DataRow("\u00df\u03c2\u0f0b\u3007", true)]
    [DataRow("\u06fd\u06fe", true)]
    [DataRow("\u0640\u07fa", false)]
    [DataRow("\u3031\u3032\u3033\u3034\u3035\u302e\u302f\u303b", false)]
    [DataRow("a\u00b7l", false)]
    [DataRow("\u00b7l", false)]
    [DataRow("l\u00b7a", false)]
    [DataRow("l\u00b7", false)]
    [DataRow("l\u00b7l", true)]
    [DataRow("\u03b1\u0375S", false)]
    [DataRow("\u03b1\u0375", false)]
    [DataRow("\u03b1\u0375\u03b2", true)]
    [DataRow("A\u05f3\u05d1", false)]
    [DataRow("\u05f3\u05d1", false)]
    [DataRow("\u05d0\u05f3\u05d1", true)]
    [DataRow("A\u05f4\u05d1", false)]
    [DataRow("\u05f4\u05d1", false)]
    [DataRow("\u05d0\u05f4\u05d1", true)]
    [DataRow("def\u30fbabc", false)]
    [DataRow("\u30fb", false)]
    [DataRow("\u30fb\u3041", true)]
    [DataRow("\u30fb\u30a1", true)]
    [DataRow("\u30fb\u4e08", true)]
    [DataRow("\u0628\u0660\u06f0", false)]
    [DataRow("\u0628\u0660\u0628", true)]
    [DataRow("\u06f00", true)]
    [DataRow("\u0915\u200d\u0937", false)]
    [DataRow("\u200d\u0937", false)]
    [DataRow("\u0915\u094d\u200d\u0937", true)]
    [DataRow("\u0915\u094d\u200c\u0937", true)]
    [DataRow("\u0628\u064a\u200c\u0628\u064a", true)]
    [DataRow("hostname", true)]
    [DataRow("host-name", true)]
    [DataRow("h0stn4me", true)]
    [DataRow("1host", true)]
    [DataRow("hostnam3", true)]
    public void MatchIdnHostname_ValidatesIdnHostname(string value, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchIdnHostname(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow("192.168.0.1", true)]
    [DataRow("127.0.0.0.1", false)]
    [DataRow("256.256.256.256", false)]
    [DataRow("127.0", false)]
    [DataRow("0x7f000001", false)]
    [DataRow("2130706433", false)]
    [DataRow("087.10.0.1", false)]
    [DataRow("87.10.0.1", true)]
    [DataRow("1২7.0.0.1", false)]
    [DataRow("192.168.1.0/24", false)]
    public void MatchIPV4_ValidatesIPV4(string value, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchIPV4(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow("::1", true)]
    [DataRow("12345::", false)]
    [DataRow("::abef", true)]
    [DataRow(":abcef", false)]
    [DataRow("1:1:1:1:1:1:1:1:1:1:1:1:1:1:1:1", false)]
    [DataRow(":laptop", false)]
    [DataRow("::", true)]
    [DataRow("::42:ff:1", true)]
    [DataRow("d6::", true)]
    [DataRow(":2:3:4:5:6:7:8", false)]
    [DataRow("1:2:3:4:5:6:7:", false)]
    [DataRow(":2:3:4::8", false)]
    [DataRow("1:d6::42", true)]
    [DataRow("1::d6::42", false)]
    [DataRow("1::d6:192.168.0.1", true)]
    [DataRow("1:2::192.168.0.1", true)]
    [DataRow("1::2:192.168.256.1", false)]
    [DataRow("1::2:192.168.ff.1", false)]
    [DataRow("::ffff:192.168.0.1", true)]
    [DataRow("1:2:3:4:5:::8", false)]
    [DataRow("1:2:3:4:5:6:7:8", true)]
    [DataRow("1:2:3:4:5:6:7", false)]
    [DataRow("1", false)]
    [DataRow("127.0.0.1", false)]
    [DataRow("1:2:3:4:1.2.3", false)]
    [DataRow("  ::1", false)]
    [DataRow("::1  ", false)]
    [DataRow("fe80::/64", false)]
    [DataRow("fe80::a%eth1", false)]
    [DataRow("1000:1000:1000:1000:1000:1000:255.255.255.255", true)]
    [DataRow("100:100:100:100:100:100:255.255.255.255.255", false)]
    [DataRow("100:100:100:100:100:100:100:255.255.255.255", false)]
    [DataRow("1:2:3:4:5:6:7:৪", false)]
    [DataRow("1:2::192.16৪.0.1", false)]
    public void MatchIPV6_ValidatesIPV6(string value, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchIPV6(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow("http://ƒøø.ßår/?∂éœ=πîx#πîüx", true)]
    [DataRow("http://ƒøø.com/blah_(wîkïpédiå)_blah#ßité-1", true)]
    [DataRow("http://ƒøø.ßår/?q=Test%20URL-encoded%20stuff", true)]
    [DataRow("http://-.~_!$&'()*+,;=:%40:80%2f::::::@example.com", true)]
    [DataRow("http://[2001:0db8:85a3:0000:0000:8a2e:0370:7334]", true)]
    [DataRow("http://2001:0db8:85a3:0000:0000:8a2e:0370:7334", false)]
    [DataRow("/abc", false)]
    [DataRow("\\\\WINDOWS\\filëßåré", false)]
    [DataRow("âππ", false)]
    // Unicode IRI test cases for optimization
    [DataRow("http://example.com/ℌ𝔢𝔩𝔩𝔬", true)]
    [DataRow("http://例え.テスト/パス", true)]
    [DataRow("http://пример.испытание/путь", true)]
    [DataRow("http://مثال.آزمایشی/مسیر", true)]
    [DataRow("https://тест.рф/файл?параметр=значение", true)]
    [DataRow("ftp://用戶:密碼@主機.網站/路徑/文件", true)]
    [DataRow("http://🌍.example/🚀", true)]
    [DataRow("http://café.com/naïve", true)]
    // Mixed ASCII/Unicode
    [DataRow("http://café.example.com/résumé", true)]
    [DataRow("http://test.café/résumé?naïve=true", true)]
    public void MatchIri_ValidatesIri(string value, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchIri(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow("http://ƒøø.ßår/?∂éœ=πîx#πîüx", true)]
    [DataRow("//ƒøø.ßår/?∂éœ=πîx#πîüx", true)]
    [DataRow("/âππ", true)]
    [DataRow("\\\\WINDOWS\\filëßåré", false)]
    [DataRow("âππ", true)]
    [DataRow("#ƒrägmênt", true)]
    [DataRow("#ƒräg\\mênt", false)]
    // Unicode IRI Reference test cases for optimization
    [DataRow("//café.example.com/résumé", true)]
    [DataRow("/naïve/påth", true)]
    [DataRow("?naïve=café", true)]
    [DataRow("#ƒrägmênt-ℌ𝔢𝔩𝔩𝔬", true)]
    [DataRow("relative/påth/to/résource", true)]
    [DataRow("../père/mère", true)]
    [DataRow("./current/dîrectory", true)]
    [DataRow("スキーム://ホスト.ドメイン/パス", true)]
    [DataRow("протокол://хост.домен/путь", true)]
    public void MatchIriReference_ValidatesIriReference(string value, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchIriReference(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow("/foo/bar~0/baz~1/%a", true)]
    [DataRow("/foo/bar~", false)]
    [DataRow("/foo//bar", true)]
    [DataRow("/foo/bar/", true)]
    [DataRow("", true)]
    [DataRow("/foo", true)]
    [DataRow("/foo/0", true)]
    [DataRow("/", true)]
    [DataRow("/a~1b", true)]
    [DataRow("/c%d", true)]
    [DataRow("/e^f", true)]
    [DataRow("/g|h", true)]
    [DataRow("/i\\j", true)]
    [DataRow("/k\"l", true)]
    [DataRow("/ ", true)]
    [DataRow("/m~0n", true)]
    [DataRow("/foo/-", true)]
    [DataRow("/foo/-/bar", true)]
    [DataRow("/~1~0~0~1~1", true)]
    [DataRow("/~1.1", true)]
    [DataRow("/~0.1", true)]
    [DataRow("#", false)]
    [DataRow("#/", false)]
    [DataRow("#a", false)]
    [DataRow("/~0~", false)]
    [DataRow("/~0/~", false)]
    [DataRow("/~2", false)]
    [DataRow("/~-1", false)]
    [DataRow("/~~", false)]
    [DataRow("a", false)]
    [DataRow("0", false)]
    [DataRow("a/a", false)]
    public void MatchJsonPointer_ValidatesJsonPointer(string value, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchJsonPointer(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow("([abc])+\\s+$", true)]
    [DataRow("^(abc]", false)]
    public void MatchRegex_ValidatesRegex(string value, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchRegex(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow("1", true)]
    [DataRow("0/foo/bar", true)]
    [DataRow("2/0/baz/1/zip", true)]
    [DataRow("0#", true)]
    [DataRow("/foo/bar", false)]
    [DataRow("-1/foo/bar", false)]
    [DataRow("+1/foo/bar", false)]
    [DataRow("0##", false)]
    [DataRow("01/a", false)]
    [DataRow("01#", false)]
    [DataRow("", false)]
    [DataRow("120/foo/bar", true)]
    public void MatchRelativeJsonPointer_ValidatesRelativeJsonPointer(string value, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchRelativeJsonPointer(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow("12:34:56+00:00", true)]
    [DataRow("not-a-time", false)]
    public void MatchTime_ValidatesTime(string value, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchTime(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow(JsonTokenType.String, true)]
    [DataRow(JsonTokenType.Number, false)]
    public void MatchTypeString_ValidatesTokenType(JsonTokenType tokenType, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchTypeString(tokenType, "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow("http://foo.bar/?baz=qux#quux", true)]
    [DataRow("http://foo.bar/?baz=qux#qu\\ux", false)] // Non-canonical fragment
    [DataRow("http://foo.com/blah_(wikipedia)_blah#cite-1", true)]
    [DataRow("http://foo.bar/?q=Test%20URL-encoded%20stuff", true)]
    [DataRow("http://xn--nw2a.xn--j6w193g/", true)]
    [DataRow("http://-.~_!$&'()*+,;=:%40:80%2f::::::@example.com", true)]
    [DataRow("http://223.255.255.254", true)]
    [DataRow("ftp://ftp.is.co.za/rfc/rfc1808.txt", true)]
    [DataRow("http://www.ietf.org/rfc/rfc2396.txt", true)]
    [DataRow("ldap://[2001:db8::7]/c=GB?objectClass?one", true)]
    [DataRow("mailto:John.Doe@example.com", true)]
    [DataRow("news:comp.infosystems.www.servers.unix", true)]
    [DataRow("tel:+1-816-555-1212", true)]
    [DataRow("urn:oasis:names:specification:docbook:dtd:xml:4.1.2", true)]
    [DataRow("//foo.bar/?baz=qux#quux", false)]
    [DataRow("/abc", false)]
    [DataRow("\\\\WINDOWS\\fileshare", false)]
    [DataRow("abc", false)]
    [DataRow("http:// shouldfail.com", false)]
    [DataRow(":// should fail", false)]
    [DataRow("bar,baz:foo", false)]
    [DataRow("http://ƒøø.ßår/?∂éœ=πîx#πîüx", false)]
    // Fast-path scheme optimization tests
    [DataRow("http://example.com", true)]
    [DataRow("https://example.com", true)]
    [DataRow("ftp://example.com", true)]
    [DataRow("file:///C:/path", true)]
    [DataRow("h://example.com", true)]
    [DataRow("httpsx://example.com", true)] // Extended scheme
    // Authority parsing edge cases
    [DataRow("http://[::1]:8080/path", true)]
    [DataRow("http://[2001:db8::1]/", true)]
    [DataRow("http://user:pass@host:80/", true)]
    [DataRow("http://user@host/", true)]
    [DataRow("http://:pass@host/", true)]
    [DataRow("http://host:8080/", true)]
    [DataRow("http://192.168.1.1:3000/", true)]
    // Percent-encoding stress tests
    [DataRow("http://example.com/%20%21%22%23%24%25%26%27%28%29", true)]
    [DataRow("http://example.com/a%2Fb%2Fc", true)]
    [DataRow("http://example.com/%C2%A9", false)] // UTF-8 encoded may not be valid in strict URI mode
    [DataRow("http://example.com/%", false)] // Incomplete percent-encoding
    [DataRow("http://example.com/%2", false)] // Incomplete percent-encoding
    [DataRow("http://example.com/%ZZ", false)] // Invalid hex
    // Long URI performance tests
    [DataRow("http://example.com/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)]
    [DataRow("http://example.com/a/b/c/d/e/f/g/h/i/j/k/l/m/n/o/p/q/r/s/t/u/v/w/x/y/z/a/b/c/d/e/f/g/h/i/j/k/l/m/n/o/p/q/r/s/t/u/v/w/x/y/z", true)]
    [DataRow("http://xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx.com/", true)] // Max label length
    [DataRow("http://xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx.com/", true)] // we are permissive about label length
    // Edge case schemes and invalid schemes
    [DataRow("", false)]
    [DataRow(":", false)]
    [DataRow("://", false)]
    [DataRow("1http://example.com", false)] // Scheme cannot start with digit
    [DataRow("ht+tp://example.com", true)] // Valid scheme chars
    [DataRow("ht_tp://example.com", false)] // Invalid scheme char
    [DataRow("ht-tp://example.com", true)] // Valid scheme chars
    [DataRow("ht.tp://example.com", true)] // Valid scheme chars
    public void MatchUri_ValidatesUri(string value, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchUri(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow("http://foo.bar/?baz=qux#quux", true)]
    [DataRow("//foo.bar/?baz=qux#quux", true)]
    [DataRow("/abc", true)]
    [DataRow("\\\\WINDOWS\\fileshare", false)]
    [DataRow("abc", true)]
    [DataRow("#fragment", true)]
    [DataRow("#frag\\ment", false)]
    [DataRow("#ƒrägmênt", false)]
    // Performance optimization test cases - common schemes (fast-path validation)
    [DataRow("https://example.com", true)]
    [DataRow("HTTPS://EXAMPLE.COM", true)] // Case insensitive scheme
    [DataRow("http://api.service.com/v1/endpoint", true)]
    [DataRow("ftp://files.example.org/path/file.txt", true)]
    [DataRow("ws://websocket.example.com", true)]
    [DataRow("wss://secure.websocket.example.com", true)]
    // Edge cases for vectorized parsing optimization
    [DataRow("", true)] // Empty string (relative reference)
    [DataRow("?query=value", true)] // Query-only reference
    [DataRow("?", true)] // Empty query
    [DataRow("#", true)] // Empty fragment
    [DataRow("?query=a&param=b&other=c#section", true)] // Complex query + fragment
    // Percent-encoding stress tests (escape sequence optimization)
    [DataRow("/path%20with%20spaces", true)]
    [DataRow("/path%2Fwith%2Fencoded%2Fslashes", true)]
    [DataRow("/path%20%21%22%23%24%25%26", true)] // Multiple percent sequences
    [DataRow("path%GG", false)] // Invalid percent encoding
    [DataRow("path%2", false)] // Incomplete percent encoding
    // Long URI stress tests (memory allocation optimization)
    [DataRow("http://very-long-subdomain-name-that-exceeds-normal-length.example.com/very/long/path/with/many/segments/that/tests/buffer/management/and/memory/allocation/patterns/in/the/optimized/parser", true)]
    [DataRow("/very/long/relative/path/with/many/segments/that/tests/the/span-based/parsing/optimizations/and/ensures/no/performance/degradation/with/longer/content/that/might/stress/the/vectorized/operations", true)]
    // Unicode and IRI test cases (IRI parsing optimization)
    [DataRow("/пуṫḩ/ẅἰṫḩ/υηἰċøժε", false)] // Unicode paths fail in strict URI reference mode
    [DataRow("//хост.example.com/пуṫḩ", false)]
    // IPv6 address test cases (UNC paths not permitted)
    [DataRow("//[2001:db8::1]/path", false)]
    [DataRow("//[::1]:8080/path", false)]
    [DataRow("//[2001:db8::1", false)]
    // Authority parsing edge cases
    [DataRow("//user:pass@host.com:8080/path", true)] // These are valid relative reference
    [DataRow("//user@host.com/path", true)] // These are valid relative reference
    [DataRow("//host.com:8080/path", true)] // These are valid relative reference
    [DataRow("//[v1.1]:8080/", false)] // But this one isn't
    // Scheme validation edge cases
    [DataRow("custom-scheme://example.com", true)]
    [DataRow("x-custom+scheme://example.com", true)]
    [DataRow("123://example.com", true)] // Parser allows scheme starting with digit in URI reference
    [DataRow("-scheme://example.com", true)] // Parser allows scheme starting with hyphen in URI reference
    public void MatchUriReference_ValidatesUriReference(string value, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchUriReference(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow("http://example.com/dictionary/{term:1}/{term}", true)]
    [DataRow("http://example.com/dictionary/{term:1}/{term", false)]
    [DataRow("http://example.com/dictionary", true)]
    [DataRow("dictionary/{term:1}/{term}", true)]
    [DataRow("map?{x,y}", true)]
    [DataRow("{x,hello,y}", true)]
    [DataRow("{+x,hello,y}", true)]
    [DataRow("{+path,x}/here", true)]
    [DataRow("{#x,hello,y}", true)]
    [DataRow("{#path,x}/here", true)]
    [DataRow("X{.var}", true)]
    [DataRow("X{.x,y}", true)]
    [DataRow("{/var}", true)]
    [DataRow("{/var,x}/here", true)]
    [DataRow("{;x,y}", true)]
    [DataRow("{;x,y,empty}", true)]
    [DataRow("{?x,y}", true)]
    [DataRow("{?x,y,empty}", true)]
    [DataRow("?fixed=yes{&x}", true)]
    [DataRow("{&x,y,empty}", true)]
    [DataRow("{var:3}", true)]
    [DataRow("{var:30}", true)]
    [DataRow("{list}", true)]
    [DataRow("{list*}", true)]
    [DataRow("{keys}", true)]
    [DataRow("{keys*}", true)]
    [DataRow("{+path:6}/here", true)]
    [DataRow("{+list}", true)]
    [DataRow("{+list*}", true)]
    [DataRow("{+keys}", true)]
    [DataRow("{+keys*}", true)]
    [DataRow("{#path:6}/here", true)]
    [DataRow("{#list}", true)]
    [DataRow("{#list*}", true)]
    [DataRow("{#keys}", true)]
    [DataRow("X{.var:3}", true)]
    [DataRow("X{.list}", true)]
    [DataRow("X{.list*}", true)]
    [DataRow("X{.keys}", true)]
    [DataRow("{/var:1,var}", true)]
    [DataRow("{/list}", true)]
    [DataRow("{/list*}", true)]
    [DataRow("{/list*,path:4}", true)]
    [DataRow("{/keys}", true)]
    [DataRow("{/keys*}", true)]
    [DataRow("{;hello:5}", true)]
    [DataRow("{;list}", true)]
    [DataRow("{;list*}", true)]
    [DataRow("{;keys}", true)]
    [DataRow("{;keys*}", true)]
    [DataRow("{?var:3}", true)]
    [DataRow("{?list}", true)]
    [DataRow("{?list*}", true)]
    [DataRow("{?keys}", true)]
    [DataRow("{?keys*}", true)]
    [DataRow("{&var:3}", true)]
    [DataRow("{&list}", true)]
    [DataRow("{&list*}", true)]
    [DataRow("{&keys}", true)]
    [DataRow("{&keys*}", true)]
    [DataRow("{+var}", true)]
    [DataRow("{+hello}", true)]
    [DataRow("{+path}/here", true)]
    [DataRow("here?ref={+path}", true)]
    [DataRow("{var}", true)]
    [DataRow("'{var}'", false)]
    [DataRow("{hello}", true)]
    [DataRow("http://ƒøø.ßår/?∂éœ={var}#πîüx", true)]
    [DataRow("#ƒräg\\mênt/{var}", false)]
    public void MatchUriTemplate_ValidatesUriTemplate(string value, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchUriTemplate(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    // Base64String
    [TestMethod]
    [DataRow("SGVsbG8gV29ybGQ=", true)]    // "Hello World"
    [DataRow("dGVzdA==", true)]             // "test"
    [DataRow("", true)]                      // Empty is valid Base64
    [DataRow("Invalid!@#$", false)]          // Not Base64
    [DataRow("SGVsbG8=====", false)]         // Excessive padding
    public void MatchBase64String_ValidatesBase64(string value, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchBase64String(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    // JsonContent
    [TestMethod]
    [DataRow("{\"key\":\"value\"}", true)]   // Valid JSON object
    [DataRow("[1,2,3]", true)]               // Valid JSON array
    [DataRow("\"hello\"", true)]             // Valid JSON string
    [DataRow("42", true)]                    // Valid JSON number
    [DataRow("true", true)]                  // Valid JSON boolean
    [DataRow("null", true)]                  // Valid JSON null
    [DataRow("{invalid json}", false)]        // Invalid JSON
    [DataRow("", false)]                      // Empty string
    [DataRow("{\"key\":", false)]             // Incomplete JSON
    public void MatchJsonContent_ValidatesJson(string value, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchJsonContent(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    // Base64Content (Base64-encoded JSON)
    [TestMethod]
    [DataRow("eyJ0ZXN0Ijp0cnVlfQ==", true)]         // {"test":true}
    [DataRow("WzEsMiwzXQ==", true)]                   // [1,2,3]
    [DataRow("IjQyIg==", true)]                       // "42" (valid JSON string)
    [DataRow("", true)]                                // Empty is valid
    [DataRow("Invalid!@#$", false)]                    // Not valid Base64
    [DataRow("e2ludmFsaWR9", false)]                   // Base64 of {invalid} — valid Base64, invalid JSON
    public void MatchBase64Content_ValidatesBase64EncodedJson(string value, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchBase64Content(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    // String constant matching
    [TestMethod]
    [DataRow("hello", "hello", true)]
    [DataRow("hello", "world", false)]
    [DataRow("", "", true)]
    [DataRow("hello", "", false)]
    public void MatchStringConstantValue_ValidatesEquality(string actual, string expected, bool expectedResult)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchStringConstantValue(
            Encoding.UTF8.GetBytes(actual),
            Encoding.UTF8.GetBytes(expected),
            expected,
            "dummy"u8,
            ref context);
        Assert.AreEqual(expectedResult, result);
        collector.AssertState();
        context.Dispose();
    }

    // Length equals
    [TestMethod]
    [DataRow(5, 5, true)]
    [DataRow(5, 3, false)]
    [DataRow(0, 0, true)]
    public void MatchLengthEquals_ValidatesLength(int expected, int actual, bool expectedResult)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchLengthEquals(expected, actual, "dummy"u8, ref context);
        Assert.AreEqual(expectedResult, result);
        collector.AssertState();
        context.Dispose();
    }

    // Length not equals
    [TestMethod]
    [DataRow(5, 5, false)]
    [DataRow(5, 3, true)]
    public void MatchLengthNotEquals_ValidatesLength(int expected, int actual, bool expectedResult)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchLengthNotEquals(expected, actual, "dummy"u8, ref context);
        Assert.AreEqual(expectedResult, result);
        collector.AssertState();
        context.Dispose();
    }

    // Length greater than
    [TestMethod]
    [DataRow(5, 6, true)]
    [DataRow(5, 5, false)]
    [DataRow(5, 4, false)]
    public void MatchLengthGreaterThan_ValidatesLength(int expected, int actual, bool expectedResult)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchLengthGreaterThan(expected, actual, "dummy"u8, ref context);
        Assert.AreEqual(expectedResult, result);
        collector.AssertState();
        context.Dispose();
    }

    // Length greater than or equals
    [TestMethod]
    [DataRow(5, 6, true)]
    [DataRow(5, 5, true)]
    [DataRow(5, 4, false)]
    public void MatchLengthGreaterThanOrEquals_ValidatesLength(int expected, int actual, bool expectedResult)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchLengthGreaterThanOrEquals(expected, actual, "dummy"u8, ref context);
        Assert.AreEqual(expectedResult, result);
        collector.AssertState();
        context.Dispose();
    }

    // Length less than
    [TestMethod]
    [DataRow(5, 4, true)]
    [DataRow(5, 5, false)]
    [DataRow(5, 6, false)]
    public void MatchLengthLessThan_ValidatesLength(int expected, int actual, bool expectedResult)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchLengthLessThan(expected, actual, "dummy"u8, ref context);
        Assert.AreEqual(expectedResult, result);
        collector.AssertState();
        context.Dispose();
    }

    // Length less than or equals
    [TestMethod]
    [DataRow(5, 4, true)]
    [DataRow(5, 5, true)]
    [DataRow(5, 6, false)]
    public void MatchLengthLessThanOrEquals_ValidatesLength(int expected, int actual, bool expectedResult)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchLengthLessThanOrEquals(expected, actual, "dummy"u8, ref context);
        Assert.AreEqual(expectedResult, result);
        collector.AssertState();
        context.Dispose();
    }

    // Regex with context (matching and non-matching)
    [TestMethod]
    [DataRow("abc123", "^[a-z]+\\d+$", true)]
    [DataRow("ABC", "^[a-z]+$", false)]
    [DataRow("", "^.+$", false)]
    public void MatchRegularExpression_WithContext_ValidatesPattern(string value, string pattern, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        var regex = new System.Text.RegularExpressions.Regex(pattern);
        bool result = JsonSchemaEvaluation.MatchRegularExpression(Encoding.UTF8.GetBytes(value), regex, pattern, "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    // NonEmpty regex optimization
    [TestMethod]
    [DataRow("hello", true)]
    [DataRow("", false)]
    public void MatchNonEmptyRegularExpression_ValidatesNonEmpty(string value, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchNonEmptyRegularExpression(Encoding.UTF8.GetBytes(value), ".+", "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    // Prefix regex optimization
    [TestMethod]
    [DataRow("prefix_value", "prefix", true)]
    [DataRow("other_value", "prefix", false)]
    public void MatchPrefixRegularExpression_ValidatesPrefix(string value, string prefix, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchPrefixRegularExpression(Encoding.UTF8.GetBytes(value), Encoding.UTF8.GetBytes(prefix), "^prefix", "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    // Range regex optimization
    [TestMethod]
    [DataRow("abc", 2, 5, true)]     // 3 chars, in range
    [DataRow("a", 2, 5, false)]       // 1 char, below range
    [DataRow("abcdef", 2, 5, false)]  // 6 chars, above range
    [DataRow("ab", 2, 5, true)]       // boundary: exactly min
    [DataRow("abcde", 2, 5, true)]    // boundary: exactly max
    public void MatchRangeRegularExpression_WithContext_ValidatesRange(string value, int min, int max, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchRangeRegularExpression(Encoding.UTF8.GetBytes(value), min, max, "^.{2,5}$", "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    // Range regex without context
    [TestMethod]
    [DataRow("abc", 2, 5, true)]
    [DataRow("a", 2, 5, false)]
    [DataRow("abcdef", 2, 5, false)]
    public void MatchRangeRegularExpression_NoContext_ValidatesRange(string value, int min, int max, bool expected)
    {
        bool result = JsonSchemaEvaluation.MatchRangeRegularExpression(Encoding.UTF8.GetBytes(value), min, max);
        Assert.AreEqual(expected, result);
    }

    // MatchTypeString with PropertyName (existing test only covers String and Number)
    [TestMethod]
    [DataRow(JsonTokenType.PropertyName, true)]   // PropertyName is also valid for string type
    [DataRow(JsonTokenType.True, false)]
    [DataRow(JsonTokenType.False, false)]
    [DataRow(JsonTokenType.Null, false)]
    public void MatchTypeString_AdditionalTokenTypes(JsonTokenType tokenType, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchTypeString(tokenType, "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    // Noop regex records success
    [TestMethod]
    public void MatchNoopRegularExpression_RecordsSuccess()
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        JsonSchemaEvaluation.MatchNoopRegularExpression(".*", "dummy"u8, ref context);
        collector.AssertState();
        context.Dispose();
    }

    // Email with quoted local part
    [TestMethod]
    [DataRow("\"user name\"@example.com", true)]           // Quoted local part with space
    [DataRow("\"user.name\"@example.com", true)]           // Quoted local part with dot
    [DataRow("\"user\x01name\"@example.com", false)]       // Control char not allowed in quoted string
    public void MatchEmail_QuotedLocalPart(string value, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchEmail(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    [TestMethod]
    [DataRow("2EB8AA08-AA98-11EA-B4AA-73B441D16380", true)]
    [DataRow("2eb8aa08-aa98-11ea-b4aa-73b441d16380", true)]
    [DataRow("2eb8aa08-AA98-11ea-B4Aa-73B441D16380", true)]
    [DataRow("00000000-0000-0000-0000-000000000000", true)]
    [DataRow("2eb8aa08-aa98-11ea-b4aa-73b441d1638", false)]
    [DataRow("2eb8aa08-aa98-11ea-73b441d16380", false)]
    [DataRow("2eb8aa08-aa98-11ea-b4ga-73b441d16380", false)]
    [DataRow("2eb8aa08aa9811eab4aa73b441d16380", false)]
    [DataRow("2eb8aa08aa98-11ea-b4aa73b441d16380", false)]
    [DataRow("2eb8-aa08-aa98-11ea-b4aa73b44-1d16380", false)]
    [DataRow("2eb8aa08aa9811eab4aa73b441d16380----", false)]
    [DataRow("98d80576-482e-427f-8434-7f86890ab222", true)]
    [DataRow("99c17cbb-656f-564a-940f-1a4568f03487", true)]
    [DataRow("99c17cbb-656f-664a-940f-1a4568f03487", true)]
    [DataRow("99c17cbb-656f-f64a-940f-1a4568f03487", true)]
    public void MatchUuid_ValidatesUuid(string value, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);
        bool result = JsonSchemaEvaluation.MatchUuid(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);
        Assert.AreEqual(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    private JsonSchemaContext CreateContext(DummyResultsCollector collector, JsonTokenType tokenType)
    {
        return JsonSchemaContext.BeginContext(new DummyDocument(tokenType), 0, false, false, collector);
    }
}
