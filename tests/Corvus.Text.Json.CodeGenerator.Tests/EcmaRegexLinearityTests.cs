// <copyright file="EcmaRegexLinearityTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Corvus.Text.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// The proof that a pattern matches in time proportional to the text (<see cref="EcmaRegexTranslator.IsLinear"/>),
/// which lets the evaluator and generated code match it with no timeout. A pattern the proof accepts wrongly would
/// let a schema or an instance from outside keep a match running, so the proof is tested by what it accepts and
/// refuses, and by timing every pattern it accepts against texts made to be slow for it.
/// </summary>
[TestClass]
public class EcmaRegexLinearityTests
{
    // A linear match of the longest text below takes well under a millisecond. A quadratic one takes seconds.
    private const int Length = 100_000;
    private static readonly TimeSpan Bound = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Patterns the proof accepts and refuses. Those of the jsonschema-benchmark corpora are among them.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="expected">Whether it is proved linear.</param>
    [TestMethod]
    [DataRow("", true)]
    [DataRow("abc", true)]
    [DataRow("^x-", true)]
    [DataRow("^[12]$", true)]
    [DataRow("^:[0-9]+$", true)]
    [DataRow("^[0-9]+(ns|ms|us|µs|s|m|h)$", true)]
    [DataRow("^[@$_#]", true)]
    [DataRow("default|^[0-9]+$", true)]
    [DataRow("(.*)", true)]
    [DataRow(".+", true)]
    [DataRow("(base64key|awskms|azurekeyvault|gcpkms|hashivault)://(.*)", true)]
    [DataRow(@"^\/[^\*\?\&\%]*(\/\*)?$", true)]
    [DataRow("^#[0-9a-fA-F]{6}$", true)]
    [DataRow(@"^[a-z]+\.[a-z]+$", true)]
    [DataRow("^[a-z]+[0-9]+$", true)]
    [DataRow("^[a-z]*[0-9]*$", true)]
    [DataRow("^[^:]+:[^:]+$", true)]
    [DataRow("^.{1,256}$", true)]
    [DataRow("^[a-z]+?[0-9]+$", true)]
    [DataRow("^(?i:abc)[a-z]+$", true)]
    [DataRow("^[\\u{1F600}-\\u{1F64F}]+x$", true)]
    [DataRow("^[a-z]+[a-z]*", true)]
    [DataRow("^[a-z]+[a-z0-9]+$", false)]
    [DataRow("^[a-z]+[a-z]*$", false)]
    [DataRow("^([a-z]+\\.)*[a-z]+$", false)]
    [DataRow("^(a+)+$", false)]
    [DataRow("^(a|a)*$", false)]
    [DataRow(".*$", false)]
    [DataRow("a*b", false)]
    [DataRow("x[a-z]*y", false)]
    [DataRow("^(a)\\1$", false)]
    [DataRow("^(?=a)a$", false)]
    [DataRow("^a\\b", false)]
    [DataRow("(?m:^a$)", false)]
    [DataRow("a^b", false)]
    [DataRow("(^a|b)", false)]
    [DataRow("^(a+|b)c$", false)]
    [DataRow("^(ab)*$", false)]
    [DataRow("(", false)]
    public void TheProofAcceptsAndRefuses(string pattern, bool expected)
    {
        Assert.AreEqual(expected, EcmaRegexTranslator.IsLinear(pattern), pattern);
    }

    /// <summary>Parts with no repetition that can match in many ways together are refused.</summary>
    [TestMethod]
    public void ManyWaysWithoutARepetitionAreRefused()
    {
        Assert.IsTrue(EcmaRegexTranslator.IsLinear("^" + string.Concat(Enumerable.Repeat("(a|b)", 8)) + "$"));
        Assert.IsFalse(EcmaRegexTranslator.IsLinear("^" + string.Concat(Enumerable.Repeat("(a|b)", 9)) + "$"));
        Assert.IsFalse(EcmaRegexTranslator.IsLinear(string.Join("|", Enumerable.Repeat("a", 257))));
    }

    /// <summary>
    /// Every pattern the proof accepts, of those above and of the corpora, matches texts made to be slow for it in
    /// time that does not grow faster than the text, in .NET's compiled and interpreted engines, as the evaluator
    /// constructs them.
    /// </summary>
    [TestMethod]
    public void EveryPatternProvedLinearMatchesHostileTextsQuickly()
    {
        int timed = 0;
        foreach (string pattern in Patterns().Where(EcmaRegexTranslator.IsLinear))
        {
            string translated = EcmaRegexTranslator.TranslateOrFallback(pattern);
            foreach (RegexOptions options in new[] { RegexOptions.CultureInvariant | RegexOptions.Compiled, RegexOptions.CultureInvariant })
            {
                Regex regex = new(translated, options, TimeSpan.FromSeconds(5));
                foreach (string text in HostileTexts(pattern))
                {
                    TimeSpan took = Time(regex, text);
                    Assert.IsTrue(took < Bound, $"'{pattern}' ({options}) took {took.TotalMilliseconds:F0} ms on a text of {text.Length} characters starting '{Shown(text)}'.");
                    timed++;
                }
            }
        }

        Assert.IsTrue(timed > 1000, timed.ToString());
    }

    /// <summary>
    /// The texts are slow for patterns that are not linear, so the test above would see one the proof accepted
    /// wrongly.
    /// </summary>
    /// <param name="pattern">A pattern whose match takes time quadratic, or exponential, in the text.</param>
    [TestMethod]
    [DataRow("^[a-z]+[a-z0-9]+$")]
    [DataRow(@"^(\w+\s?)*$")]
    public void TheHostileTextsAreSlowForAPatternThatIsNotLinear(string pattern)
    {
        Assert.IsFalse(EcmaRegexTranslator.IsLinear(pattern));
        Regex regex = new(EcmaRegexTranslator.TranslateOrFallback(pattern), RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        bool slow = false;
        foreach (string text in HostileTexts(pattern))
        {
            try
            {
                slow |= Time(regex, text) > Bound;
            }
            catch (RegexMatchTimeoutException)
            {
                slow = true;
            }

            if (slow)
            {
                break;
            }
        }

        Assert.IsTrue(slow, pattern);
    }

    /// <summary>
    /// Every pattern of the ECMA-262 regular expression test data (github.com/corvus-dotnet/ecma262-regex-test-data)
    /// that the proof accepts matches texts made to be slow for it quickly, in .NET's interpreted engine, which is
    /// the slower. The test runs when <c>CORVUS_REGEX_DATASET</c> names the data directory of a clone.
    /// </summary>
    [TestMethod]
    [TestCategory("outerloop")]
    public void EveryPatternOfTheDatasetProvedLinearMatchesHostileTextsQuickly()
    {
        string directory = Environment.GetEnvironmentVariable("CORVUS_REGEX_DATASET");
        if (directory is null)
        {
            Assert.Inconclusive("CORVUS_REGEX_DATASET is not set.");
            return;
        }

        var patterns = new HashSet<string>(StringComparer.Ordinal);
        foreach (string file in Directory.GetFiles(directory, "*.jsonl"))
        {
            foreach (string line in File.ReadLines(file))
            {
                using var document = System.Text.Json.JsonDocument.Parse(line);
                System.Text.Json.JsonElement root = document.RootElement;
                if (root.TryGetProperty("pattern", out System.Text.Json.JsonElement pattern)
                    && (!root.TryGetProperty("flags", out System.Text.Json.JsonElement flags) || flags.GetString() is "" or "u"))
                {
                    patterns.Add(pattern.GetString()!);
                }
            }
        }

        int linear = 0;
        var slow = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.ForEach(patterns.Where(EcmaRegexTranslator.IsLinear), new ParallelOptions { MaxDegreeOfParallelism = 4 }, pattern =>
        {
            Interlocked.Increment(ref linear);
            Regex regex = new(EcmaRegexTranslator.TranslateOrFallback(pattern), RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));
            foreach (string text in HostileTexts(pattern, 20_000, variants: 4))
            {
                TimeSpan took;
                try
                {
                    took = Time(regex, text, TimeSpan.FromMilliseconds(100));
                }
                catch (RegexMatchTimeoutException)
                {
                    took = TimeSpan.FromSeconds(5);
                }

                if (took > TimeSpan.FromMilliseconds(100))
                {
                    slow.Add($"'{pattern}' took {took.TotalMilliseconds:F0} ms on '{Shown(text)}' ({text.Length} characters)");
                    break;
                }
            }
        });

        Console.WriteLine($"{linear} of {patterns.Count} patterns are proved linear.");
        Assert.AreEqual(0, slow.Count, string.Join(Environment.NewLine, slow.Take(20)));
    }

    private static IEnumerable<string> Patterns()
    {
        foreach (object[] row in typeof(EcmaRegexLinearityTests).GetMethod(nameof(TheProofAcceptsAndRefuses))!
            .GetCustomAttributes(typeof(DataRowAttribute), false).Cast<DataRowAttribute>().Select(r => r.Data))
        {
            yield return (string)row[0];
        }

        foreach (string pattern in CorpusPatterns)
        {
            yield return pattern;
        }
    }

    private static TimeSpan Time(Regex regex, string text) => Time(regex, text, Bound);

    private static TimeSpan Time(Regex regex, string text, TimeSpan bound)
    {
        TimeSpan best = TimeSpan.MaxValue;
        for (int i = 0; i < 2; i++)
        {
            long start = Stopwatch.GetTimestamp();
            regex.IsMatch(text);
            TimeSpan took = Stopwatch.GetElapsedTime(start);
            if (took < best)
            {
                best = took;
            }

            if (best < bound)
            {
                break;
            }
        }

        return best;
    }

    private static string Shown(string text) => text.Length <= 20 ? text : text.Substring(0, 20) + "...";

    /// <summary>
    /// Texts made from the tree of the pattern: each repetition pumped to the length of the text with one member
    /// of its set, each other part matched one way, and an ending that fails, in several variants of which member
    /// and which alternative is taken. And texts of one character of the pattern repeated, with each ending.
    /// </summary>
    private static IEnumerable<string> HostileTexts(string pattern) => HostileTexts(pattern, Length, variants: 8);

    private static IEnumerable<string> HostileTexts(string pattern, int length, int variants)
    {
        EcmaRegexNode root;
        bool unicode = true;
        try
        {
            root = new EcmaRegexParser(pattern.AsSpan(), unicode: true).Parse();
        }
        catch (EcmaRegexSyntaxException)
        {
            unicode = false;
            root = new EcmaRegexParser(pattern.AsSpan(), unicode: false).Parse();
        }

        string[] endings = ["", "!", "\n", "\u0001", "é", "\U0001F600"];
        var members = new List<int>();
        Members(root, members);
        for (int variant = 0; variant < variants; variant++)
        {
            var sb = new StringBuilder();
            Pump(root, sb, variant, unicode, length);
            string pumped = sb.ToString();
            foreach (string ending in endings)
            {
                yield return pumped + ending;
            }
        }

        foreach (int member in members.Distinct().Take(12))
        {
            string one = Text(member, unicode);
            string repeated = new StringBuilder(length * one.Length).Insert(0, one, length).ToString();
            foreach (string ending in endings)
            {
                yield return repeated + ending;
            }
        }
    }

    private static void Pump(EcmaRegexNode node, StringBuilder sb, int variant, bool unicode, int length)
    {
        switch (node.Kind)
        {
            case EcmaRegexNodeKind.Char:
                sb.Append(Text(node.CodePoint, unicode));
                break;
            case EcmaRegexNodeKind.Set:
                sb.Append(Text(Member(node.Set, variant), unicode));
                break;
            case EcmaRegexNodeKind.Cat:
                foreach (EcmaRegexNode sub in node.Subs)
                {
                    Pump(sub, sb, variant, unicode, length);
                }

                break;
            case EcmaRegexNodeKind.Alt:
                Pump(node.Subs[variant % node.Subs.Length], sb, variant / node.Subs.Length, unicode, length);
                break;
            case EcmaRegexNodeKind.Group:
            case EcmaRegexNodeKind.Look:
                Pump(node.Subs[0], sb, variant, unicode, length);
                break;
            case EcmaRegexNodeKind.Repeat:
                int count = node.Max is 0 or 1
                    ? ((variant & 1) == 0 ? node.Max : node.Min)
                    : node.Max == EcmaRegexNode.Unbounded ? length : Math.Min(node.Max, length);
                var body = new StringBuilder();
                Pump(node.Subs[0], body, variant, unicode, length);
                string one = body.ToString();
                if (one.Length > 0 && count > 0)
                {
                    sb.Append(new StringBuilder(count * one.Length).Insert(0, one, Math.Max(1, Math.Min(count, length / one.Length))).ToString());
                }

                break;
        }
    }

    private static void Members(EcmaRegexNode node, List<int> members)
    {
        if (node.Kind == EcmaRegexNodeKind.Char)
        {
            members.Add(node.CodePoint);
        }
        else if (node.Kind == EcmaRegexNodeKind.Set)
        {
            for (int k = 0; k < 3; k++)
            {
                members.Add(Member(node.Set, k));
            }
        }

        foreach (EcmaRegexNode sub in node.Subs)
        {
            Members(sub, members);
        }
    }

    /// <summary>A member of a set that is not a surrogate, chosen by the variant.</summary>
    private static int Member(int[] set, int variant)
    {
        int ranges = set.Length / 2;
        for (int k = 0; k < ranges; k++)
        {
            int r = (variant + k) % ranges;
            int first = set[2 * r];
            int last = set[(2 * r) + 1];
            int c = (variant & 1) == 0 ? first : last;
            if (c is < 0xD800 or > 0xDFFF)
            {
                return c;
            }

            if (first < 0xD800)
            {
                return first;
            }

            if (last > 0xDFFF)
            {
                return last;
            }
        }

        return 'a';
    }

    private static string Text(int c, bool unicode) => unicode || c <= 0xFFFF ? char.ConvertFromUtf32(c is >= 0xD800 and <= 0xDFFF ? 'a' : c) : "a";

    /// <summary>The patterns of the jsonschema-benchmark corpora that the shapes of the evaluator do not match.</summary>
    private static readonly string[] CorpusPatterns =
    [
        "^[a-zA-Z0-9_\\-]+$",
        "^[a-zA-Z0-9_\\.]*$",
        "^(\\\\[0-9a-zA-Z_]+\\\\)?[a-zA-Z][a-zA-Z0-9_]*$",
        "^([a-zA-Z_$][a-zA-Z0-9_$]{0,39}\\.)*([a-zA-Z_$][a-zA-Z0-9_$]{0,39})$",
        "^[a-zA-Z0-9\\.]+$",
        "^([d|D][e|E][f|F][a|A][u|U][l|L][t|T])|([p|P][e|E][r|R][s|S][o|O][n|N][a|A][l|L][i|I][z|Z][a|A][t|T][i|I][o|O][n|N])$",
        "^[a-zA-Z0-9_\\.\\-\\|@#]*$",
        "^([a-z][a-z0-9]{0,39})(\\.[a-z][a-z0-9]{0,39})*$",
        "^[\\w\\*]{0,60}$",
        "^[0-9]{1,}.[0-9]{1,}.[0-9]{1,}$",
        "^[A-Z0-9_\\-\\/]+$",
        "^3\\.1\\.\\d+(-.+)?$",
        "^[Ss][Cc][Rr][Ii][Pp][Tt][Hh][Oo][Ss][Tt]$",
        "^[a-z_]+\\.[a-z_]+$",
        "^/.*",
        "^[^:]+:[^:]+$",
    ];
}