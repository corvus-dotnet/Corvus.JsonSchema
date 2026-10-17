// <copyright file="FormatTimeTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Corvus.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.CoverageGap;

/// <summary>
/// A format check takes time in proportion to the string it reads, and the checks that are regular expressions
/// have the match limit of 1 second that the expressions of a pattern have. Three checks were not linear:
/// <c>idn-email</c> (a search that started again at every at sign, and the conversion of a domain of any length),
/// <c>ipv6</c> (a search for a zone that started again at every percent sign) and <c>regex</c> (the groups of a
/// pattern).
/// </summary>
[TestClass]
public class FormatTimeTests
{
    // Each text of the timed tests took its check more than 20 seconds before the change. The bound is far above
    // what it takes now on a machine with every core busy, and far below that.
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(6);

    private delegate ValidationContext Check(in JsonString value, in ValidationContext context, ValidationLevel level);

    [TestMethod]
    [DataRow("idn-email", "at signs before a line end", false)]
    [DataRow("idn-email", "a domain of 750,000 labels", false)]
    [DataRow("ipv6", "percent signs before a line end", false)]
    [DataRow("regex", "groups", true)]
    [DataRow("regex", "named groups", true)]
    public void ACheckTakesTimeInProportionToTheString(string format, string name, bool expected)
    {
        string text = name switch
        {
            "at signs before a line end" => new string('@', 100_000) + "\nx",
            "a domain of 750,000 labels" => "a@" + Repeat("\u00e9.", 750_000) + "com",
            "percent signs before a line end" => "fe80::1%" + new string('%', 400_000) + "\nx",
            "groups" => Repeat("(a)", 100_000),
            "named groups" => Numbered("(?<n", ">a)", 40_000),
            _ => throw new ArgumentException(name),
        };

        var value = new JsonString(text);
        foreach (Check check in ChecksOf(format))
        {
            check(new JsonString("warm"), ValidationContext.ValidContext, ValidationLevel.Flag);

            var stopwatch = Stopwatch.StartNew();
            bool actual = check(value, ValidationContext.ValidContext, ValidationLevel.Flag).IsValid;
            stopwatch.Stop();

            Assert.AreEqual(expected, actual, $"{format}, {name}");
            Assert.IsTrue(stopwatch.Elapsed < Bound, $"{format}, {name}: {text.Length:N0} characters took {stopwatch.Elapsed.TotalSeconds:F1} s.");
        }
    }

    [TestMethod]
    [DataRow("a@b.c", true)]
    [DataRow("a@b.c\n", true, DisplayName = "one line end after the address is not read")]
    [DataRow("a@\u00e9.com", true)]
    [DataRow("\u00e9@\u00e9.com", true)]
    [DataRow("a@[1.2.3.4]", true)]
    [DataRow("a@[IPv6:::1]", true)]
    [DataRow("a@[IPv6:fe80::1%eth0]", false)]
    [DataRow("a", false, DisplayName = "no at sign")]
    [DataRow("a@", false)]
    [DataRow("a@\n", false)]
    [DataRow("@b.c", false)]
    [DataRow("a@b", false, DisplayName = "a domain of one label")]
    [DataRow("a@b@c.d", false, DisplayName = "two at signs")]
    [DataRow("a@b\n@c.d", false, DisplayName = "two at signs on two lines")]
    [DataRow("a@b\nc.d", false, DisplayName = "a line end in the domain")]
    [DataRow("a b@c.d", false)]
    [DataRow("a@c .d", false)]
    public void IdnEmailAnswersAsItDid(string text, bool expected)
    {
        var value = new JsonString(text);
        foreach (Check check in ChecksOf("idn-email"))
        {
            Assert.AreEqual(expected, check(value, ValidationContext.ValidContext, ValidationLevel.Flag).IsValid, text);
        }
    }

    [TestMethod]
    public void IdnEmailReadsADomainOfAtMost253Characters()
    {
        // Four labels of 61 characters and one of 5, with their dots: 253 characters, the most a host name has.
        string longest = Repeat(new string('a', 61) + ".", 4) + "abcde";
        Assert.AreEqual(253, longest.Length);
        foreach (Check check in ChecksOf("idn-email"))
        {
            Assert.IsTrue(check(new JsonString("a@" + longest), ValidationContext.ValidContext, ValidationLevel.Flag).IsValid);

            // A character more is refused by the conversion to ASCII, as it was.
            Assert.IsFalse(check(new JsonString("a@" + longest + "f"), ValidationContext.ValidContext, ValidationLevel.Flag).IsValid);

            // More than 253 characters of two UTF-16 code units each is refused unread. A domain cannot be longer
            // and be a host name.
            Assert.IsFalse(check(new JsonString("a@" + Repeat("b.", 253) + "c"), ValidationContext.ValidContext, ValidationLevel.Flag).IsValid);
        }
    }

    [TestMethod]
    [DataRow("::1", true)]
    [DataRow("fe80::1", true)]
    [DataRow("fe80::1%eth0", false, DisplayName = "a zone is not part of an address")]
    [DataRow("fe80::1%", false)]
    [DataRow("fe80::1%eth0\n", false)]
    [DataRow("fe80::1%eth0\nx", false, DisplayName = "a zone with a line end in it was not seen, and the address was valid")]
    [DataRow("1.2.3.4", false)]
    public void IpV6HasNoZone(string text, bool expected)
    {
        var value = new JsonString(text);
        foreach (Check check in ChecksOf("ipv6"))
        {
            Assert.AreEqual(expected, check(value, ValidationContext.ValidContext, ValidationLevel.Flag).IsValid, text);
        }
    }

    [TestMethod]
    public void EveryRegularExpressionOfAFormatHasTheMatchLimit()
    {
        FieldInfo[] fields = typeof(Validate).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        int count = 0;
        foreach (FieldInfo field in fields)
        {
            if (field.FieldType == typeof(Regex))
            {
                var regex = (Regex)field.GetValue(null)!;
                Assert.AreEqual(TimeSpan.FromSeconds(1), regex.MatchTimeout, field.Name);
                count++;
            }
        }

        Assert.IsTrue(count >= 10, $"{count} regular expressions found.");
    }

    private static Check[] ChecksOf(string format)
    {
        return format switch
        {
            "idn-email" =>
            [
                static (in JsonString v, in ValidationContext c, ValidationLevel l) => Validate.TypeIdnEmail(v, c, l),
                static (in JsonString v, in ValidationContext c, ValidationLevel l) => ValidateWithoutCoreType.TypeIdnEmail(v, c, l),
            ],
            "ipv6" =>
            [
                static (in JsonString v, in ValidationContext c, ValidationLevel l) => Validate.TypeIpV6(v, c, l),
                static (in JsonString v, in ValidationContext c, ValidationLevel l) => ValidateWithoutCoreType.TypeIpV6(v, c, l),
            ],
            "regex" =>
            [
                static (in JsonString v, in ValidationContext c, ValidationLevel l) => Validate.TypeRegex(v, c, l),
                static (in JsonString v, in ValidationContext c, ValidationLevel l) => ValidateWithoutCoreType.TypeRegex(v, c, l),
            ],
            _ => throw new ArgumentException(format),
        };
    }

    private static string Repeat(string unit, int count)
    {
        var builder = new StringBuilder(unit.Length * count);
        for (int i = 0; i < count; i++)
        {
            builder.Append(unit);
        }

        return builder.ToString();
    }

    private static string Numbered(string before, string after, int count)
    {
        var builder = new StringBuilder();
        for (int i = 1; i <= count; i++)
        {
            builder.Append(before).Append(i).Append(after);
        }

        return builder.ToString();
    }
}