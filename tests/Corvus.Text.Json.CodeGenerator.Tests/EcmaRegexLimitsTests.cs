// <copyright file="EcmaRegexLimitsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text.RegularExpressions;
using Corvus.Text.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// The limits on the size of one pattern (<see cref="EcmaRegexLimits"/>). A pattern may come from outside, so no
/// pattern is to make the translator, or .NET when it constructs a regular expression from the translation, run for
/// long or hold much memory. A pattern over a limit is refused with <see cref="EcmaRegexPatternTooLargeException"/>,
/// whose message says that the pattern is too large and names the limit. It is never refused as a pattern that is not
/// valid.
/// </summary>
[TestClass]
public class EcmaRegexLimitsTests
{
    // No refusal here takes a hundredth of this on a machine with nothing else to do. The bound is for one that is busy.
    private static readonly TimeSpan Generous = TimeSpan.FromSeconds(20);

    [TestMethod]
    public void APatternLongerThanTheLimitIsTooLarge()
    {
        string pattern = new('a', EcmaRegexLimits.MaxLength + 1);

        EcmaRegexPatternTooLargeException thrown = AssertTooLarge(pattern, read: false, "It is longer than 1,048,576 characters.");
        Assert.AreEqual(
            "The pattern '" + new string('a', 120) + "...' (1,048,577 characters) is too large to read as an ECMA-262 regular expression. It is longer than 1,048,576 characters.",
            thrown.Message);
    }

    [TestMethod]
    public void GroupsNestedDeeperThanTheLimitAreTooLarge()
    {
        // The pattern itself is one level, so 199 groups one inside another are read and 200 are not.
        Assert.IsTrue(EcmaRegexTranslator.TryTranslate(Nested("(", "a", ")", 199), out _, out _));
        AssertTooLarge(Nested("(", "a", ")", 200), read: false, "Its groups nest more than 200 deep.");
        AssertTooLarge(Nested("(?:", "a", ")*", 100_000), read: false, "Its groups nest more than 200 deep.");
    }

    /// <summary>
    /// A pattern that nests too deep is refused before it is known to be valid, and what is wrong with the rest of
    /// it is not what it is refused for.
    /// </summary>
    [TestMethod]
    public void APatternThatNestsTooDeepAndIsNotValidIsTooLargeAndNotInvalid()
    {
        EcmaRegexPatternTooLargeException thrown = AssertTooLarge(new string('(', 10_000), read: false, "Its groups nest more than 200 deep.");
        StringAssert.Contains(thrown.Message, "is too large to read as an ECMA-262 regular expression.");
    }

    [TestMethod]
    public void ATranslationLongerThanTheLimitIsTooLarge()
    {
        // A backreference that ignores case, to a group that can hold any character, is written as a comparison for
        // every class of case variants: some 157,000 characters. Six are under the limit and seven are over it.
        Assert.IsTrue(EcmaRegexTranslator.TryTranslate(@"(?i:^(.)\1\1\1\1\1\1$)", out string six, out _));
        Assert.IsTrue(six.Length > 900_000 && six.Length <= EcmaRegexLimits.MaxLength, six.Length.ToString());

        EcmaRegexPatternTooLargeException thrown = AssertTooLarge(@"(?i:^(.)\1\1\1\1\1\1\1$)", read: true, "What is written for .NET from it is longer than 1,048,576 characters.");
        Assert.AreEqual(
            @"The pattern '(?i:^(.)\1\1\1\1\1\1\1$)' is a valid ECMA-262 regular expression, and is too large. What is written for .NET from it is longer than 1,048,576 characters.",
            thrown.Message);
    }

    /// <summary>
    /// A repetition whose iterations that need not run are made to fail when they match nothing, by the check that
    /// reads the rest of the input, is written as its body twice, so one inside another doubles what is written.
    /// Before the limit, twenty of them were written as a hundred million characters and seventy as more than the
    /// machine has memory for. The check is written inside a lookaround whose captures a backreference reads, and
    /// where the body is too large to be written as one that cannot match the empty string.
    /// </summary>
    [TestMethod]
    [DataRow(true, 20)]
    [DataRow(true, 70)]
    [DataRow(false, 70)]
    [DataRow(false, 190)]
    public void RepetitionsWrittenTwiceOneInAnotherAreTooLargeAndNotWritten(bool inALookaround, int depth)
    {
        string nested = Nested("(?:", "(a)", "|){1,2}", depth);
        string pattern = inALookaround ? "^(?=(" + nested + @"))\1\2$" : "^" + nested + @"\1$";

        long before = GC.GetTotalAllocatedBytes(precise: true);
        AssertTooLarge(pattern, read: true, "What is written for .NET from it is longer than 1,048,576 characters.");
        long allocated = GC.GetTotalAllocatedBytes(precise: true) - before;
        Assert.IsTrue(allocated < 256L * 1024 * 1024, $"Allocated {allocated:N0} bytes.");
    }

    /// <summary>
    /// Outside such a lookaround the iterations that need not run are written as a body that cannot match the empty
    /// string, which holds the repetitions inside it once. Twenty one inside another are then a few thousand
    /// characters, where each used to double what the one inside it had written.
    /// </summary>
    [TestMethod]
    public void RepetitionsOfABodyThatCannotMatchNothingOneInAnotherAreNotDoubled()
    {
        Assert.IsTrue(EcmaRegexTranslator.TryTranslate("^" + Nested("(?:", "(a)", "|){1,2}", 20) + @"\1$", out string translated, out _));
        Assert.IsTrue(translated.Length < 10_000, translated.Length.ToString());
    }

    [TestMethod]
    public void ALazyRepetitionWrittenAsOptionalGroupsOneInAnotherIsTooLarge()
    {
        // In a lookahead whose capture is read, a lazy repetition of up to eight is written as eight copies.
        string pattern = "^(?=(" + Nested("(?:", "a", "){0,8}?", 95) + @"))\1$";
        AssertTooLarge(pattern, read: true, "What is written for .NET from it is longer than 1,048,576 characters.");
    }

    /// <summary>
    /// .NET writes out a repetition with a least count of 2, 3 or 4 to find the text a match starts with, one inside
    /// another too, when it constructs a regular expression: twenty-four counts of 2 took it 220 ms and 100 MB, and
    /// thirty did not fit in memory.
    /// </summary>
    [TestMethod]
    public void SmallCountsOneInAnotherAreTooLarge()
    {
        const string Limit = "Its repetitions with a least count of 2, 3 or 4, one inside another, come to more than 65,536 copies of what they repeat, beyond four times its size.";

        Assert.IsTrue(EcmaRegexTranslator.TryTranslate(Nested("(?:", "a", "){2}", 12), out _, out _));
        Assert.IsTrue(EcmaRegexTranslator.TryTranslate(Nested("(?:", "a", "){4}", 6), out _, out _));
        AssertTooLarge(Nested("(?:", "a", "){2}", 24), read: true, Limit);
        AssertTooLarge(Nested("(?:", "ab", "){4}", 9), read: true, Limit);
        AssertTooLarge(Nested("(?:", "a", "){3,}", 140), read: true, Limit);

        // A count of 5 or more is not written out, and neither is a count of none or one.
        Assert.IsTrue(EcmaRegexTranslator.TryTranslate(Nested("(?:", "a", "){5}", 140), out _, out _));
        Assert.IsTrue(EcmaRegexTranslator.TryTranslate(Nested("(?:", "a", "){1,2}", 110), out _, out _));
        Assert.IsTrue(EcmaRegexTranslator.TryTranslate("(?:(?:a{1000}){1000}){1000}", out _, out _));

        // Many such repetitions side by side are in proportion to the pattern.
        Assert.IsTrue(EcmaRegexTranslator.TryTranslate(string.Concat(Enumerable.Repeat(@"(?:\d{4}-\d{2}-\d{2}|[a-f]{3}){2}x", 3000)), out _, out _));
    }

    [TestMethod]
    public void APatternThatTakesTooManyStepsIsTooLarge()
    {
        // Each group with a name that another has is compared with every one before it.
        string names = string.Concat(Enumerable.Repeat("(?<n>a)|", 4000)) + "b";
        AssertTooLarge(names, read: false, "It takes more than 2,000,000 steps to translate.");

        // Each class is gone through for the characters equivalent to its members.
        string classes = "(?i:" + string.Concat(Enumerable.Repeat(@"[\p{L}]", 20_000)) + ")";
        AssertTooLarge(classes, read: false, "It takes more than 2,000,000 steps to translate.");

        // Each group here holds what the next one can, so what each can hold is worked out again for every other.
        string chain = "(?i:" + string.Concat(Enumerable.Range(0, 300).Select(k => "([a-h]" + @"\" + (k + 2).ToString() + ")")) + ")";
        AssertTooLarge(chain, read: true, "It takes more than 2,000,000 steps to translate.");
    }

    [TestMethod]
    public void AnOrdinaryPatternTakesFewSteps()
    {
        Assert.IsTrue(EcmaRegexTranslator.StepsToTranslate(@"^[a-z]+(?:-[a-z0-9]+)*$") < 500);
        Assert.IsTrue(EcmaRegexTranslator.StepsToTranslate(@"^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})$") < 2_000);
        Assert.IsTrue(EcmaRegexTranslator.StepsToTranslate(@"^[\p{L}\p{N}_.-]{1,64}@[\p{L}\p{N}.-]+$") < 60_000);

        // The most that a pattern of the ECMA-262 test data takes is some 740,000, for six case-insensitive
        // backreferences to groups that hold any character.
        long one = EcmaRegexTranslator.StepsToTranslate(@"(?i:^(.)\1$)");
        Assert.IsTrue(one > 10_000 && one < 400_000, one.ToString());
    }

    /// <summary>
    /// .NET joins the pieces of literal text of a sequence one at a time, and copies what it has joined each time.
    /// 64,000 escaped characters in a row took it 1.1 seconds, and 128,000 pieces took it 18.
    /// </summary>
    [TestMethod]
    public void ASequenceOfVeryManyPiecesOfTextIsTooLarge()
    {
        const string Limit = "Its sequences and alternations are longer than .NET constructs a regular expression from in bounded time: by the count of the translator, a cost of more than 100,000,000.";

        Assert.IsTrue(EcmaRegexTranslator.TryTranslate(string.Concat(Enumerable.Repeat(@"\.", 5_000)), out _, out _));
        AssertTooLarge(string.Concat(Enumerable.Repeat(@"\.", 12_000)), read: true, Limit);
        AssertTooLarge(new string('\u00E9', 12_000), read: true, Limit);
        AssertTooLarge(string.Concat(Enumerable.Repeat("a{2}b{3}", 8_000)), read: true, Limit);
        AssertTooLarge(string.Concat(Enumerable.Repeat("(ab)(cd)", 8_000)), read: true, Limit);
        AssertTooLarge(string.Concat(Enumerable.Repeat("\U0001F600", 8_000)), read: true, Limit);

        // Text written as itself is one piece however long it is, and a class between two pieces keeps them apart.
        Assert.IsTrue(EcmaRegexTranslator.TryTranslate(new string('a', 200_000), out _, out _));
        Assert.IsTrue(EcmaRegexTranslator.TryTranslate(string.Concat(Enumerable.Repeat(@"\.[0-9]", 40_000)), out _, out _));
    }

    /// <summary>
    /// .NET goes through the alternatives of an alternation once for each of them: 100,000 took it 1.3 seconds.
    /// </summary>
    [TestMethod]
    public void AnAlternationOfVeryManyAlternativesIsTooLarge()
    {
        const string Limit = "Its sequences and alternations are longer than .NET constructs a regular expression from in bounded time: by the count of the translator, a cost of more than 100,000,000.";

        Assert.IsTrue(EcmaRegexTranslator.TryTranslate(string.Join("|", Enumerable.Range(0, 15_000).Select(k => "w" + k)), out _, out _));
        AssertTooLarge(string.Join("|", Enumerable.Range(0, 25_000).Select(k => "w" + k)), read: true, Limit);
    }

    /// <summary>The count of what a translation costs .NET to construct, on patterns as the translator writes them.</summary>
    [TestMethod]
    [DataRow("abc", 3L)] // one piece of three
    [DataRow(@"a\.b", 9L)] // three pieces of three
    [DataRow(@"\u00E9\u00E9\u00E9\u00E9", 16L)]
    [DataRow("a{3}b", 8L)] // two pieces of four
    [DataRow("a{65}b", 1L)] // .NET does not write out a count over 64
    [DataRow("a+b", 1L)]
    [DataRow("[ab]x[cd]", 1L)]
    [DataRow("[a]x", 4L)] // a class of one character is that character
    [DataRow(@"ab[^\n\r]cd", 4L)] // two pieces of two, which the class keeps apart
    [DataRow("(?:ab)(?:cd)", 16L)] // two pieces of four, and two plain groups among four terms
    [DataRow("(?:ab)+c", 3L)] // a group with a quantifier is not part of the sequence
    [DataRow("(?<1>ab)c", 3L)]
    [DataRow("(?=ab)c", 3L)]
    [DataRow("a|b|c", 5L)] // three sequences of one, and a quarter of nine
    [DataRow("(?:a|b)c", 4L)]
    [DataRow(@"(?(1)\1)a", 1L)]
    [DataRow(@"(?(1)\1|x)a", 3L)]
    [DataRow(@"(?#interpreted)ab", 2L)]
    [DataRow(@"^(?:(?(1)(?<-1>))(?=(?<e1>[\s\S]*))(?:(?<1>a)|b|)(?!\k<e1>\z))*(?(1)\1)\z", 4L)] // a, b, and a quarter of nine
    public void TheCostOfConstruction(string dotNetPattern, long expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.ConstructionCost(dotNetPattern));
    }

    /// <summary>
    /// What the limits are for: .NET constructs a regular expression from a translation at a limit in well under a
    /// second. The bound is generous, for a machine that is busy.
    /// </summary>
    [TestMethod]
    public void DotNetConstructsWhatIsUnderTheLimitsInBoundedTime()
    {
        string[] patterns =
        [
            Nested("(?:", "a", "){2}", 12),
            Nested("(?:", "ab", "){4}", 6),
            string.Concat(Enumerable.Repeat(@"\.", 7_000)),
            string.Concat(Enumerable.Repeat("a{2}b{3}", 2_500)),
            string.Join("|", Enumerable.Range(0, 15_000).Select(k => "w" + k)),
            @"(?i:^(.)\1\1\1\1\1\1$)",
            new string('a', 200_000),
        ];

        foreach (string pattern in patterns)
        {
            Assert.IsTrue(EcmaRegexTranslator.TryTranslate(pattern, out string translated, out _));
            var elapsed = Stopwatch.StartNew();
            Regex regex = new(translated, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            Assert.IsTrue(elapsed.Elapsed < Generous, $"{elapsed.Elapsed} to construct the translation of {Shown(pattern)}");
            Assert.IsFalse(regex.IsMatch("-"));
        }
    }

    /// <summary>Every entry point of the translator refuses a pattern over a limit in the same way.</summary>
    [TestMethod]
    public void EveryEntryPointRefusesAPatternOverALimit()
    {
        string pattern = Nested("(?:", "ab", "){4}", 9);

        Assert.ThrowsExactly<EcmaRegexPatternTooLargeException>(() => EcmaRegexTranslator.TryTranslate(pattern, out _, out _));
        Assert.ThrowsExactly<EcmaRegexPatternTooLargeException>(() => EcmaRegexTranslator.TryTranslate(pattern, out _, out _, out _));
        Assert.ThrowsExactly<EcmaRegexPatternTooLargeException>(() => EcmaRegexTranslator.TranslateOrFallback(pattern));
        Assert.ThrowsExactly<EcmaRegexPatternTooLargeException>(() => EcmaRegexTranslator.Translate(pattern.AsSpan()));
        Assert.ThrowsExactly<EcmaRegexPatternTooLargeException>(() => EcmaRegexTranslator.GetMaxTranslatedLength(pattern.AsSpan()));
        Assert.ThrowsExactly<EcmaRegexPatternTooLargeException>(() => EcmaRegexTranslator.TryTranslate(pattern.AsSpan(), new char[16], out _));

        // The exception is not the one of a pattern that is not valid, which Translate throws as ArgumentException.
        Assert.IsFalse(typeof(ArgumentException).IsAssignableFrom(typeof(EcmaRegexPatternTooLargeException)));
        Assert.IsTrue(typeof(InvalidOperationException).IsAssignableFrom(typeof(EcmaRegexPatternTooLargeException)));
    }

    /// <summary>
    /// The count of steps is of the pattern alone: what an earlier translation on the same thread did, or a caller
    /// of the tables of Unicode, is not counted against the next pattern.
    /// </summary>
    [TestMethod]
    public void TheStepsOfOnePatternAreNotCountedAgainstTheNext()
    {
        long alone = EcmaRegexTranslator.StepsToTranslate(@"^(?i:[a-z]+)$");
        for (int k = 0; k < 2_000; k++)
        {
            EcmaRegexUnicode.FoldClosure([0, EcmaRegexUnicode.Max], unicode: true);
        }

        Assert.AreEqual(alone, EcmaRegexTranslator.StepsToTranslate(@"^(?i:[a-z]+)$"));
    }

    private static string Nested(string open, string inner, string close, int depth)
    {
        return string.Concat(Enumerable.Repeat(open, depth)) + inner + string.Concat(Enumerable.Repeat(close, depth));
    }

    private static string Shown(string pattern) => pattern.Length > 60 ? pattern.Substring(0, 60) + "..." : pattern;

    private static EcmaRegexPatternTooLargeException AssertTooLarge(string pattern, bool read, string limit)
    {
        var elapsed = Stopwatch.StartNew();
        EcmaRegexPatternTooLargeException thrown = Assert.ThrowsExactly<EcmaRegexPatternTooLargeException>(
            () => EcmaRegexTranslator.TryTranslate(pattern, out _, out _),
            Shown(pattern));
        Assert.IsTrue(elapsed.Elapsed < Generous, $"{elapsed.Elapsed} to refuse {Shown(pattern)}");
        Assert.AreEqual(limit, thrown.Limit, Shown(pattern));
        Assert.AreEqual(read, thrown.Read, Shown(pattern));
        StringAssert.EndsWith(thrown.Message, limit);
        StringAssert.Contains(
            thrown.Message,
            read ? " is a valid ECMA-262 regular expression, and is too large. " : " is too large to read as an ECMA-262 regular expression. ");
        Assert.IsFalse(thrown.Message.Contains("not a valid", StringComparison.Ordinal), thrown.Message);
        return thrown;
    }
}