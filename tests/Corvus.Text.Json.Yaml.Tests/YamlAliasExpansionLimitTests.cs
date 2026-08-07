// <copyright file="YamlAliasExpansionLimitTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using Corvus.Text.Json.Yaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Yaml.Tests;

/// <summary>
/// The alias-expansion limits. An anchor stores the serialized bytes of the node it names, so a later anchor whose node
/// references it captures those bytes too: each level multiplies, and a few hundred bytes of YAML becomes gigabytes of
/// output. <c>YamlReaderOptions</c> declares a depth and a size limit for exactly this, and
/// <c>docs/Yaml.md</c> presents them as a protection this converter has and YamlDotNet does not.
/// </summary>
/// <remarks>
/// <para>
/// The audit's acceptance criterion named <c>InvalidDataException</c>. This converter's convention, and what
/// <c>docs/Yaml.md</c> already promises, is <c>YamlException</c>, so that is what is asserted.
/// </para>
/// <remarks>
/// <para>
/// The tests assert the refusal is <em>prompt</em> as well as correct. Expansion that eventually exhausts memory and
/// expansion that is refused both look like "the parse did not return a document" from the outside, so a test that only
/// checks for a throw cannot tell the control from its absence — which is the whole failure mode here.
/// </para>
/// <para>
/// The default-initialisation path is covered separately. Every call site takes <c>YamlReaderOptions options =
/// default</c>, and <c>default(struct)</c> skips the parameterless constructor, so both limits arrive as zero. Enforcing
/// a limit of zero would refuse the first alias in every document; enforcing "zero means unlimited" would leave the
/// control off wherever nobody named it. Neither is the documented behaviour, so the zero case is asserted directly.
/// </para>
/// </remarks>
[TestClass]
public class YamlAliasExpansionLimitTests
{
    // The classic billion-laughs shape: nine levels, each a sequence of ten references to the level below. Fully
    // expanded that is 10^9 leaf scalars. It must be refused on its structure rather than by running out of anything.
    private const string BillionLaughs = """
        a: &a ["lol","lol","lol","lol","lol","lol","lol","lol","lol","lol"]
        b: &b [*a,*a,*a,*a,*a,*a,*a,*a,*a,*a]
        c: &c [*b,*b,*b,*b,*b,*b,*b,*b,*b,*b]
        d: &d [*c,*c,*c,*c,*c,*c,*c,*c,*c,*c]
        e: &e [*d,*d,*d,*d,*d,*d,*d,*d,*d,*d]
        f: &f [*e,*e,*e,*e,*e,*e,*e,*e,*e,*e]
        g: &g [*f,*f,*f,*f,*f,*f,*f,*f,*f,*f]
        h: &h [*g,*g,*g,*g,*g,*g,*g,*g,*g,*g]
        i: &i [*h,*h,*h,*h,*h,*h,*h,*h,*h,*h]
        """;

    // Deeply nested inside ONE anchor, then referenced. The parse-time nesting guard counts the depth of the text it
    // reads; an alias is written as pre-serialized bytes, so the depth this produces in the OUTPUT is invisible to it.
    private const string DeepThroughAlias = """
        a: &a [[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]
        b: &b [*a]
        c: &c [*b]
        d: &d [*c]
        e: [*d]
        """;

    [TestMethod]
    public void The_billion_laughs_document_is_refused()
    {
        var elapsed = Stopwatch.StartNew();

        Assert.ThrowsExactly<YamlException>(() => YamlDocument.ConvertToJsonString(BillionLaughs));

        // Refused on structure, not by exhaustion. A converter that actually attempts the expansion does not get here
        // in a second, so the bound is what distinguishes the control from its absence.
        Assert.IsLessThan(TimeSpan.FromSeconds(10), elapsed.Elapsed, "the document was not refused promptly, so expansion was attempted.");
    }

    [TestMethod]
    public void An_explicit_size_limit_is_enforced()
    {
        var options = new YamlReaderOptions { MaxAliasExpansionSize = 64 };

        Assert.ThrowsExactly<YamlException>(() => YamlDocument.ConvertToJsonString(BillionLaughs, options));
    }

    [TestMethod]
    public void An_explicit_depth_limit_is_enforced_through_an_alias()
    {
        // The depth arrives entirely through alias expansion, so this is the case the parse-time nesting guard cannot
        // see and the reason the option is not redundant with it.
        var options = new YamlReaderOptions { MaxAliasExpansionDepth = 8 };

        Assert.ThrowsExactly<YamlException>(() => YamlDocument.ConvertToJsonString(DeepThroughAlias, options));
    }

    [TestMethod]
    public void A_default_constructed_options_value_carries_the_documented_limits()
    {
        var options = new YamlReaderOptions();

        Assert.AreEqual(64, options.MaxAliasExpansionDepth);
        Assert.AreEqual(1_000_000, options.MaxAliasExpansionSize);
    }

    [TestMethod]
    public void A_defaulted_options_struct_behaves_as_the_documented_default()
    {
        // default(YamlReaderOptions) skips the constructor, so both limits read as zero — and every call site in the
        // library passes exactly that. Zero must therefore mean "the documented default" at the point of use, or the
        // limits are off precisely where nobody thought about them.
        YamlReaderOptions defaulted = default;

        Assert.AreEqual(0, defaulted.MaxAliasExpansionDepth);
        Assert.AreEqual(0, defaulted.MaxAliasExpansionSize);

        // ...which must not translate into "refuse everything".
        string json = YamlDocument.ConvertToJsonString("a: &a [1,2]\nb: [*a]", defaulted);
        StringAssert.Contains(json, "[1,2]");

        // ...nor into "allow everything".
        Assert.ThrowsExactly<YamlException>(() => YamlDocument.ConvertToJsonString(BillionLaughs, defaulted));
    }

    [TestMethod]
    public void An_ordinary_document_with_aliases_still_converts()
    {
        // The limits must not change what a well-formed document produces.
        string json = YamlDocument.ConvertToJsonString("base: &base { x: 1 }\nuse: *base");

        StringAssert.Contains(json, "\"x\":1");
    }
}