// <copyright file="RegexCriterionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.RegularExpressions;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

/// <summary>
/// Tests for the <see cref="RegexCriterion"/> runtime surface that generated executors call when
/// inlining a <c>regex</c> criterion.
/// </summary>
[TestClass]
public class RegexCriterionTests
{
    private static readonly Regex Word = new("^Fido$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    [TestMethod]
    public void Matches_a_json_string_value()
    {
        RegexCriterion.IsMatch(Word, Json("\"Fido\"")).ShouldBeTrue();
        RegexCriterion.IsMatch(Word, Json("\"Rex\"")).ShouldBeFalse();
    }

    [TestMethod]
    public void A_non_string_json_value_never_matches()
    {
        RegexCriterion.IsMatch(Word, Json("42")).ShouldBeFalse();
        RegexCriterion.IsMatch(Word, Json("true")).ShouldBeFalse();
        RegexCriterion.IsMatch(Word, Json("null")).ShouldBeFalse();
        RegexCriterion.IsMatch(Word, default(JsonElement)).ShouldBeFalse();
    }

    [TestMethod]
    public void Matches_a_json_string_with_non_ascii_content()
    {
        var emoji = new Regex("^\U0001F600$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        RegexCriterion.IsMatch(emoji, Json("\"\U0001F600\"")).ShouldBeTrue();
    }

    [TestMethod]
    public void Matches_a_status_code()
    {
        var clientError = new Regex("^4", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        RegexCriterion.IsMatch(clientError, 404).ShouldBeTrue();
        RegexCriterion.IsMatch(clientError, 200).ShouldBeFalse();
    }

    [TestMethod]
    public void Matches_a_char_span()
    {
        RegexCriterion.IsMatch(Word, "Fido".AsSpan()).ShouldBeTrue();
        RegexCriterion.IsMatch(Word, "nope".AsSpan()).ShouldBeFalse();
    }

    [TestMethod]
    public void Matches_a_managed_string_and_treats_null_as_no_match()
    {
        RegexCriterion.IsMatch(Word, "Fido").ShouldBeTrue();
        RegexCriterion.IsMatch(Word, "nope").ShouldBeFalse();
        RegexCriterion.IsMatch(Word, (string?)null).ShouldBeFalse();
    }

    [TestMethod]
    public void Matches_a_long_json_string_via_the_pooled_transcode_path()
    {
        // An input longer than the stack threshold (256) takes the rented-buffer transcode path.
        var manyAs = new Regex("^a+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        string longValue = new('a', 400);
        RegexCriterion.IsMatch(manyAs, Json($"\"{longValue}\"")).ShouldBeTrue();
    }

    [TestMethod]
    public void A_match_timeout_yields_false()
    {
        // Catastrophic backtracking against a 1-tick timeout must be caught and reported as no match.
        var pathological = new Regex("^(a+)+$", RegexOptions.CultureInvariant, TimeSpan.FromTicks(1));
        string input = new string('a', 64) + "!";
        RegexCriterion.IsMatch(pathological, input.AsSpan()).ShouldBeFalse();
    }

    private static JsonElement Json(string json)
        => ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(json)).RootElement;
}
