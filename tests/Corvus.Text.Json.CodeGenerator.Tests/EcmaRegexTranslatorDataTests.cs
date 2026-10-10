// <copyright file="EcmaRegexTranslatorDataTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.RegularExpressions;
using Corvus.Text.Json.CodeGeneration;
using Corvus.Text.Json.Tests.EcmaPatterns;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// The translation of each pattern of <see cref="EcmaPatternCases"/> answers as V8 does in .NET's compiled engine
/// and in its interpreted one.
/// </summary>
[TestClass]
public class EcmaRegexTranslatorDataTests
{
    [TestMethod]
    [DataRow(RegexOptions.Compiled)]
    [DataRow(RegexOptions.None)]
    public void PatternsWrittenFromTheTreeAnswerAsV8(RegexOptions engine)
    {
        AssertCases(EcmaPatternCases.WrittenFromTheTree, engine);
    }

    [TestMethod]
    [DataRow(RegexOptions.Compiled)]
    [DataRow(RegexOptions.None)]
    public void PatternsWithBackreferencesAnswerAsV8(RegexOptions engine)
    {
        AssertCases(EcmaPatternCases.Backreferences, engine);
    }

    [TestMethod]
    [DataRow(RegexOptions.Compiled)]
    [DataRow(RegexOptions.None)]
    public void PatternsWithEmptyIterationsAnswerAsV8(RegexOptions engine)
    {
        AssertCases(EcmaPatternCases.EmptyIterations, engine);
    }

    [TestMethod]
    [DataRow(RegexOptions.Compiled)]
    [DataRow(RegexOptions.None)]
    public void PatternsThatFaultedTheEngineAnswerAsV8(RegexOptions engine)
    {
        AssertCases(EcmaPatternCases.EngineFaults, engine);
    }

    [TestMethod]
    [DataRow(RegexOptions.Compiled)]
    [DataRow(RegexOptions.None)]
    public void PatternsWithACaseInsensitiveBackreferenceAnswerAsV8(RegexOptions engine)
    {
        AssertCases(EcmaPatternCases.CaseInsensitiveBackreferences, engine);
    }

    /// <summary>
    /// A backreference that ignores case, to a group that captures any one character, matches a second character
    /// exactly when ECMA-262's Canonicalize gives one character for both. Every character with a case variant is
    /// tried with every member of its class, with the characters next to it and with one from another class, in
    /// both grammars. .NET's own comparison differs from Canonicalize for hundreds of these pairs.
    /// </summary>
    [TestMethod]
    [DataRow(true, RegexOptions.Compiled)]
    [DataRow(true, RegexOptions.None)]
    [DataRow(false, RegexOptions.Compiled)]
    [DataRow(false, RegexOptions.None)]
    public void ACaseInsensitiveBackreferenceComparesByCanonicalize(bool unicode, RegexOptions engine)
    {
        // A lone "{" is valid only without the u flag.
        string pattern = unicode ? @"^(?i:([^])\1)$" : @"^(?i:([^])\1)(?:{){0}$";
        Assert.IsTrue(EcmaRegexTranslator.TryTranslate(pattern, out string translated, out bool wasUnicode, out _));
        Assert.AreEqual(unicode, wasUnicode);
        var regex = new Regex(translated, engine | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(30));
        var failures = new List<string>();
        int pairs = 0;
        foreach (int[] members in EcmaRegexUnicode.FoldClasses([0, EcmaRegexUnicode.Max], unicode))
        {
            foreach (int captured in members)
            {
                var others = new List<int>(members) { captured + 1, captured - 1, 'a', 0x3C3, 0x10428 };
                foreach (int other in others)
                {
                    bool expected = Array.IndexOf(members, other) >= 0;
                    string text = char.ConvertFromUtf32(captured) + (other is >= 0xD800 and <= 0xDFFF ? "?" : char.ConvertFromUtf32(other));
                    pairs++;
                    if (regex.IsMatch(text) != expected)
                    {
                        failures.Add($"U+{captured:X4} then U+{other:X4}: expected {expected}");
                    }
                }
            }
        }

        Assert.IsTrue(pairs > 10000, $"{pairs} pairs");
        if (failures.Count > 0)
        {
            Assert.Fail($"{failures.Count} of {pairs} pairs are wrong. The first of them:\n{string.Join("\n", failures.Take(25))}");
        }
    }

    /// <summary>
    /// A lazy repetition with more than eight iterations left to choice, inside a lookaround whose captures a
    /// backreference reads, is written as a repetition with no highest count whose iterations a group counts. The
    /// count holds, also when the repetition is entered again and in a lookbehind. The answers are V8's.
    /// </summary>
    [TestMethod]
    [DataRow(RegexOptions.Compiled)]
    [DataRow(RegexOptions.None)]
    public void ALazyRepetitionCountedByAGroupKeepsItsHighestCount(RegexOptions engine)
    {
        (string Pattern, string[] Matches, string[] Fails)[] cases =
        [
        ("^(?=((?:ab?){0,9}?)c)\\1c$", ["aaaaaaaaac", "c", "aaac", "abababababababababc"], ["aaaaaaaaaac", "aaaaaaaaaaac", "aaaaaaaaacaaaaaaaaac", "aaaaaaaaacaaaaaaaaaac", "aaaaacaaaaaaaaac", "aacaaaaaaaaaac", "aaaaaaaaaacaac", "cc", "aaaaaaaaaaaacaaac", "ababababababababababc", "abababababcabababababababababc"]),
        ("^(?:(?=((?:ab?){0,9}?)c)\\1c){2}$", ["aaaaaaaaacaaaaaaaaac", "aaaaacaaaaaaaaac", "cc", "abababababcabababababababababc"], ["aaaaaaaaac", "aaaaaaaaaac", "aaaaaaaaaaac", "aaaaaaaaacaaaaaaaaaac", "aacaaaaaaaaaac", "aaaaaaaaaacaac", "c", "aaac", "aaaaaaaaaaaacaaac", "abababababababababc", "ababababababababababc"]),
        ("^(?:(?=((?:ab?){0,9}?)c)\\1c)+$", ["aaaaaaaaac", "aaaaaaaaacaaaaaaaaac", "aaaaacaaaaaaaaac", "c", "cc", "aaac", "abababababababababc", "abababababcabababababababababc"], ["aaaaaaaaaac", "aaaaaaaaaaac", "aaaaaaaaacaaaaaaaaaac", "aacaaaaaaaaaac", "aaaaaaaaaacaac", "aaaaaaaaaaaacaaac", "ababababababababababc"]),
        ("^(?:(?=((?:ab?){2,11}?)c)\\1c)+$", ["aaaaaaaaac", "aaaaaaaaaac", "aaaaaaaaaaac", "aaaaaaaaacaaaaaaaaac", "aaaaaaaaacaaaaaaaaaac", "aaaaacaaaaaaaaac", "aacaaaaaaaaaac", "aaaaaaaaaacaac", "aaac", "abababababababababc", "ababababababababababc", "abababababcabababababababababc"], ["c", "cc", "aaaaaaaaaaaacaaac"]),
        ("(?:(?<=^((?:ab?){0,9}?))\\1c)", ["aaaaaaaaaac", "aacaaaaaaaaaac", "aaaaaaaaaacaac", "c", "cc", "aaaaaaaaaaaacaaac", "ababababababababababc"], ["aaaaaaaaac", "aaaaaaaaaaac", "aaaaaaaaacaaaaaaaaac", "aaaaaaaaacaaaaaaaaaac", "aaaaacaaaaaaaaac", "aaac", "abababababababababc", "abababababcabababababababababc"]),
        ("^(?:(?=((?:(a)|b){1,10}?)c)\\1c\\2?)+$", ["aaaaaaaaac", "aaaaaaaaaac", "aaaaaaaaacaaaaaaaaac", "aaaaaaaaacaaaaaaaaaac", "aaaaacaaaaaaaaac", "aacaaaaaaaaaac", "aaaaaaaaaacaac", "aaac"], ["aaaaaaaaaaac", "c", "cc", "aaaaaaaaaaaacaaac", "abababababababababc", "ababababababababababc", "abababababcabababababababababc"]),
        ("^(?:(?=((?:(a|)b?){0,9}?)c)\\1c)+$", ["aaaaaaaaac", "aaaaaaaaacaaaaaaaaac", "aaaaacaaaaaaaaac", "c", "cc", "aaac", "abababababababababc", "abababababcabababababababababc"], ["aaaaaaaaaac", "aaaaaaaaaaac", "aaaaaaaaacaaaaaaaaaac", "aacaaaaaaaaaac", "aaaaaaaaaacaac", "aaaaaaaaaaaacaaac", "ababababababababababc"]),
        ("^(?:(?=((?:a|ab){0,9}?)c)\\1c)+$", ["aaaaaaaaac", "aaaaaaaaacaaaaaaaaac", "aaaaacaaaaaaaaac", "c", "cc", "aaac", "abababababababababc", "abababababcabababababababababc"], ["aaaaaaaaaac", "aaaaaaaaaaac", "aaaaaaaaacaaaaaaaaaac", "aacaaaaaaaaaac", "aaaaaaaaaacaac", "aaaaaaaaaaaacaaac", "ababababababababababc"]),
        ("^(?:(?=((?:a|ab){0,9}?)(c))\\1\\2)+$", ["aaaaaaaaac", "aaaaaaaaacaaaaaaaaac", "aaaaacaaaaaaaaac", "c", "cc", "aaac", "abababababababababc", "abababababcabababababababababc"], ["aaaaaaaaaac", "aaaaaaaaaaac", "aaaaaaaaacaaaaaaaaaac", "aacaaaaaaaaaac", "aaaaaaaaaacaac", "aaaaaaaaaaaacaaac", "ababababababababababc"]),
        ];
        foreach ((string pattern, _, _) in cases)
        {
            StringAssert.Contains(EcmaRegexTranslator.Translate(pattern), "(?<c", pattern);
        }

        AssertCases(cases, engine);
    }

    private static void AssertCases((string Pattern, string[] Matches, string[] Fails)[] cases, RegexOptions engine)
    {
        var failures = new List<string>();
        int wrongPatterns = 0;
        int counted = 0;
        foreach ((string pattern, string[] matches, string[] fails) in cases)
        {
            wrongPatterns += failures.Count > counted ? 1 : 0;
            counted = failures.Count;
            if (!EcmaRegexTranslator.TryTranslate(pattern, out string translated, out string error))
            {
                failures.Add($"{pattern} was refused: {error}");
                continue;
            }

            Regex regex;
            try
            {
                // A match that does not return is a failure here and not a test run that never ends.
                regex = new Regex(translated, engine | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));
            }
            catch (ArgumentException ex)
            {
                failures.Add($"{pattern} was translated to {translated}, which .NET refuses: {ex.Message}");
                continue;
            }

            foreach ((string[] texts, bool expected) in new[] { (matches, true), (fails, false) })
            {
                foreach (string text in texts)
                {
                    try
                    {
                        if (regex.IsMatch(text) != expected)
                        {
                            failures.Add($"{pattern} on {Escape(text)}: expected {expected} from {translated}");
                        }
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"{pattern} on {Escape(text)}: {ex.GetType().Name} from {translated}");
                    }
                }
            }
        }

        wrongPatterns += failures.Count > counted ? 1 : 0;
        if (failures.Count > 0)
        {
            Assert.Fail($"{failures.Count} wrong answers in {wrongPatterns} of {cases.Length} patterns. The first of them:\n{string.Join("\n", failures.Take(25))}");
        }
    }

    private static string Escape(string text)
    {
        return string.Concat(text.Select(c => c is >= ' ' and <= '~' ? c.ToString() : "\\u" + ((int)c).ToString("X4", System.Globalization.CultureInfo.InvariantCulture)));
    }
}