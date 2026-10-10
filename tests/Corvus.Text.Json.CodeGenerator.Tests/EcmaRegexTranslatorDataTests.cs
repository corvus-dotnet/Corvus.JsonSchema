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