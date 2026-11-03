// <copyright file="FormatConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.RuntimeEvaluator;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// The <c>format</c> cases the unified JSON-Schema-Test-Suite added that the format checks got wrong. Each row is a
/// test from the suite (the description is the suite's), with <c>format</c> asserted. The checks are shared with the
/// generated code, so these hold for both.
/// </summary>
[TestClass]
public class FormatConformanceTests
{
    [TestMethod]
    [DataRow("date-time", "\"1985-04-12T00:59:59.999999999999999Z\"", true, "a second fraction of fifteen nines is valid")]
    [DataRow("date-time", "\"1985-04-12T23:20:50+01\"", false, "a numeric offset without minutes is invalid")]
    [DataRow("duration", "\"P999999999999999999999999999999999999999999999999999999999999999999999999999999D\"", true, "a component with many digits is valid")]
    [DataRow("email", "\"\\\"\\\\\\\"\\\"@iana.org\"", true, "an escaped double quote in the local part is valid")]
    [DataRow("email", "\"\\\"\\\\\\\\\\\"@iana.org\"", true, "an escaped backslash in the local part is valid")]
    [DataRow("email", "\"\\\"\\\\a\\\"@iana.org\"", true, "a quoted pair in the local part is valid")]
    [DataRow("email", "\"a@[ipv6:::1]\"", true, "a lowercase IPv6 tag in an address literal is valid")]
    [DataRow("email", "\"test(comment)@iana.org\"", false, "a parenthesis in an unquoted local part is not valid")]
    [DataRow("email", "\"test@iana-.com\"", false, "a domain label ending with a hyphen is not valid")]
    [DataRow("email", "\"test@iana..com\"", false, "two subsequent dots inside the domain are not valid")]
    [DataRow("idn-email", "\"\u0085@example.com\"", true, "a C1 control in the local part is valid")]
    [DataRow("idn-email", "\"\u03B4\u03BF\u03BA\u03B9\u03BC\u03AE@example-.com\"", false, "a domain label ending with a hyphen is not valid")]
    [DataRow("idn-email", "\"\uFFFF@example.com\"", true, "a noncharacter in the local part is valid")]
    [DataRow("idn-hostname", "\"a\u200Bb\"", true, "a zero width space is ignored by the mapping")]
    [DataRow("idn-hostname", "\"\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\uFF41\"", true, "a label of 63 fullwidth characters is valid after mapping")]
    [DataRow("idn-hostname", "\"\uFF58\uFF4E--nxasmq6b\"", true, "an ACE prefix produced by the mapping step is decoded as an A-label")]
    [DataRow("ipv6", "\"::ffff:192.168.0.01\"", false, "a leading zero in the last IPv4 octet is invalid")]
    [DataRow("ipv6", "\"[::1]\"", false, "a bracketed address is invalid")]
    [DataRow("iri", "\"http://[::ffff:192.168.0.01]\"", false, "an IPv6 host whose embedded IPv4 has a leading zero is invalid")]
    [DataRow("iri", "\"http://\u0192\u00F8\u00F8.\u00DF\u00E5r/\\n\"", false, "a trailing newline after a valid IRI is invalid")]
    [DataRow("iri-reference", "\"/%\"", false, "lone percent sign is invalid")]
    [DataRow("iri-reference", "\"/%6G\"", false, "invalid percent-encoding with non-hex digits")]
    [DataRow("iri-reference", "\"/%A\"", false, "incomplete percent-encoding triplet")]
    [DataRow("iri-reference", "\"/\u00E2\u03C0\u03C0\\n\"", false, "a trailing newline after a valid IRI Reference is invalid")]
    [DataRow("iri-reference", "\"//[2001:db8::1]/p\"", true, "a valid protocol-relative IRI Reference with a compressed IPv6 host")]
    [DataRow("time", "\"00:59:59.999999999999999Z\"", true, "a valid time string with a 15 digit second fraction")]
    [DataRow("time", "\"08:30:06+01\"", false, "a numeric time-offset requires the minutes component")]
    [DataRow("uri", "\"http:/[::1]\"", false, "square brackets are not allowed in a path segment")]
    [DataRow("uri", "\"http://foo.bar/\\n\"", false, "a trailing newline after a valid URI is invalid")]
    [DataRow("uri-reference", "\"/%\"", false, "lone percent sign is invalid")]
    [DataRow("uri-reference", "\"/%A\"", false, "incomplete percent-encoding triplet")]
    [DataRow("uri-reference", "\"//a@b@example.com/\"", false, "more than one at-sign in the authority")]
    [DataRow("uri-reference", "\"//example.com:abc/p\"", false, "a non-numeric port in a network-path reference")]
    [DataRow("uri-reference", "\"/[::1]\"", false, "square brackets outside an authority")]
    [DataRow("uri-reference", "\"/a\\\"b\"", false, "a double quote in a path")]
    [DataRow("uri-reference", "\"/p\\n\"", false, "a trailing line feed")]
    [DataRow("uri-reference", "\"1:b\"", false, "a colon in the first segment of a relative-path reference")]
    [DataRow("uri-template", "\"a'b\"", true, "an apostrophe in a literal is valid")]
    public void FormatMatchesTheSuite(string format, string instance, bool expected, string description)
    {
        foreach (JsonSchemaCodeGeneration generation in new[] { JsonSchemaCodeGeneration.Disabled, JsonSchemaCodeGeneration.Eager })
        {
            var options = new JsonSchemaEvaluatorOptions { AssertFormat = true, CodeGeneration = generation };
            using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile($$"""{"format": "{{format}}"}""", options);
            Assert.AreEqual(expected, evaluator.Evaluate(instance), $"{format}: {description} ({generation})");
        }
    }

    [TestMethod]
    [DataRow("\"ex\u00ADample.com\"", true, "a soft hyphen is ignored by the mapping")]
    [DataRow("\"example\uFF0Ecom\"", true, "a fullwidth full stop is a label separator")]
    [DataRow("\"EXAMPLE.COM\"", true, "ASCII is not changed by the mapping")]
    [DataRow("\"a\u00A0b\"", false, "a no-break space maps to a space, which a label cannot hold")]
    [DataRow("\"\uFDFA\uFDFA\uFDFA\uFDFA\uFDFA\uFDFA\uFDFA\uFDFA\uFDFA\uFDFA\uFDFA\uFDFA\uFDFA\uFDFA\uFDFA\uFDFA\uFDFA\uFDFA\uFDFA\uFDFA\uFDFA\uFDFA\uFDFA\uFDFA\uFDFA\uFDFA\uFDFA\uFDFA\uFDFA\uFDFA\uFDFA\uFDFA\"", false, "a name the mapping makes longer than any host name")]
    [DataRow("\"a\u200Db\"", false, "a zero width joiner is kept (a deviation) and needs a virama before it")]
    public void IdnHostnameAppliesTheUts46Mapping(string instance, bool expected, string description)
    {
        var options = new JsonSchemaEvaluatorOptions { AssertFormat = true };
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile("""{"format": "idn-hostname"}""", options);
        Assert.AreEqual(expected, evaluator.Evaluate(instance), description);
    }
}