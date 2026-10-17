// <copyright file="EcmaRegexValidityTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.CodeGeneration;
using Corvus.Text.Json.Tests.EcmaPatterns;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// The translator accepts the patterns V8 accepts and refuses the ones it refuses: with the grammar of the <c>u</c>
/// flag alone through <see cref="EcmaRegexTranslator.Translate"/>, and with either grammar through
/// <see cref="EcmaRegexTranslator.TryTranslate(string, out string, out bool, out string)"/>.
/// </summary>
[TestClass]
public class EcmaRegexValidityTests
{
    [TestMethod]
    public void EitherGrammar_AcceptsWhatV8AcceptsWithOrWithoutTheUFlag()
    {
        var failures = new List<string>();
        foreach ((string pattern, bool validWithU, bool validWithoutU) in EcmaPatternCases.Validity)
        {
            bool valid = EcmaRegexTranslator.TryTranslate(pattern, out string translated, out bool unicode, out string error);
            if (valid != (validWithU || validWithoutU))
            {
                failures.Add($"{pattern}: {(valid ? "accepted as " + translated : "refused: " + error)}");
            }
            else if (valid && unicode != validWithU)
            {
                failures.Add($"{pattern}: read {(unicode ? "with" : "without")} the u flag");
            }
            else if (valid ? error is not null || translated is null : string.IsNullOrEmpty(error) || translated is not null)
            {
                failures.Add($"{pattern}: translation {translated ?? "null"}, error {error ?? "null"}");
            }
        }

        Assert.AreEqual(0, failures.Count, string.Join("\n", failures.Take(25)));
    }

    [TestMethod]
    public void TheUFlagGrammar_AcceptsWhatV8AcceptsWithTheUFlag()
    {
        var failures = new List<string>();
        foreach ((string pattern, bool validWithU, _) in EcmaPatternCases.Validity)
        {
            bool valid;
            try
            {
                EcmaRegexTranslator.Translate(pattern);
                valid = true;
            }
            catch (ArgumentException)
            {
                valid = false;
            }

            if (valid != validWithU)
            {
                failures.Add($"{pattern}: {(valid ? "accepted" : "refused")}");
            }
        }

        Assert.AreEqual(0, failures.Count, string.Join("\n", failures.Take(25)));
    }

    /// <summary>
    /// Every translation of a valid pattern is a pattern .NET accepts.
    /// </summary>
    [TestMethod]
    public void EveryTranslationIsAPatternDotNetAccepts()
    {
        var failures = new List<string>();
        foreach ((string pattern, _, _) in EcmaPatternCases.Validity)
        {
            if (EcmaRegexTranslator.TryTranslate(pattern, out string translated, out _))
            {
                try
                {
                    _ = new System.Text.RegularExpressions.Regex(translated);
                }
                catch (ArgumentException ex)
                {
                    failures.Add($"{pattern}: {translated}: {ex.Message}");
                }
            }
        }

        Assert.AreEqual(0, failures.Count, string.Join("\n", failures.Take(25)));
    }

    [TestMethod]
    [DataRow("a{2,1}", "Numbers out of order in {} quantifier at offset 6.")]
    [DataRow("(", "Unterminated group at offset 1.")]
    [DataRow(")", "Unmatched ')' at offset 0.")]
    [DataRow("[a", "Unterminated character class at offset 2.")]
    [DataRow("[z-a]", "Range out of order in character class at offset 2.")]
    [DataRow(@"x\b+", "Nothing to repeat at offset 1.")]
    [DataRow("^*", "Nothing to repeat at offset 0.")]
    [DataRow("a**", "Nothing to repeat at offset 2.")]
    [DataRow("(?<=a)+", "Nothing to repeat at offset 0.")]
    [DataRow("(?<n>a)(?<n>b)", "Duplicate group name 'n' at offset 12.")]
    [DataRow("(?i-i:a)", "Repeated group modifier at offset 4.")]
    [DataRow("(?<1a>x)", "Invalid group name at offset 4.")]
    [DataRow(@"a\", @"\ at end of pattern at offset 2.")]
    public void AnInvalidPattern_IsRefusedWithWhatIsWrongAndWhere(string pattern, string expectedError)
    {
        Assert.IsFalse(EcmaRegexTranslator.TryTranslate(pattern, out string translated, out string error));
        Assert.IsNull(translated);
        Assert.AreEqual(expectedError, error);
    }

    /// <summary>
    /// When the two grammars refuse a pattern for different reasons the error gives both: what the grammar of JSON
    /// Schema's <c>pattern</c> objects to, and what is still wrong when the pattern is read without the u flag.
    /// </summary>
    [TestMethod]
    public void AnInvalidPattern_ThatTheTwoGrammarsRefuseDifferently_HasBothReasons()
    {
        Assert.IsFalse(EcmaRegexTranslator.TryTranslate(@"\p{Nope}(", out _, out string error));
        Assert.AreEqual("Invalid property name at offset 3 (read without the u flag: Unterminated group at offset 9).", error);
    }

    [TestMethod]
    [DataRow(@"\-", true, false)]
    [DataRow(@"a\-b", true, false)]
    [DataRow("a{", true, false)]
    [DataRow("a}", true, false)]
    [DataRow("]", true, false)]
    [DataRow(@"\1", true, false)]
    [DataRow(@"\p{Nope}", true, false)]
    [DataRow(@"\u{1F600}", true, true)]
    [DataRow(@"\p{L}", true, true)]
    [DataRow("a{2}", true, true)]
    [DataRow("a{2,1}", false, false)]
    public void TryTranslate_SaysWhichGrammarAcceptedThePattern(string pattern, bool valid, bool unicode)
    {
        Assert.AreEqual(valid, EcmaRegexTranslator.TryTranslate(pattern, out _, out bool readWithU, out _));
        Assert.AreEqual(unicode, readWithU);
    }
}