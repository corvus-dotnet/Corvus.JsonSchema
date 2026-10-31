// <copyright file="EcmaRegexFormatTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.CoverageGap;

/// <summary>
/// The <c>regex</c> format is the grammar of an ECMA-262 regular expression with the <c>u</c> flag, as today's
/// ECMAScript reads it, decided by Corvus.Regex.Ecma262, the engine generated code matches patterns with.
/// </summary>
[TestClass]
public class EcmaRegexFormatTests
{
    [TestMethod]
    [DataRow("^[a-z]+$", true)]
    [DataRow("(?<n>a)\\k<n>", true)]
    [DataRow("^\\p{L}+$", true)]
    [DataRow("\\u{1F600}", true)]
    [DataRow("(?<a>x)|(?<a>y)", true)]
    [DataRow("(?i:a)b", true)]
    [DataRow("(?<a>x)(?<a>y)", false)]
    [DataRow("\\a", false)]
    [DataRow("[z-a]", false)]
    [DataRow("a{2,1}", false)]
    [DataRow("(?<1a>x)", false)]
    [DataRow("\\p{RGI_Emoji}", false)]
    [DataRow("(?i-i:a)", false)]
    public void AStringIsTheRegexFormatWhenTheGrammarOfTheUFlagReadsIt(string text, bool expected)
    {
        JsonString value = new(text);
        Assert.AreEqual(expected, Validate.TypeRegex(value, ValidationContext.ValidContext, ValidationLevel.Flag).IsValid, text);
        Assert.AreEqual(expected, ValidateWithoutCoreType.TypeRegex(value, ValidationContext.ValidContext, ValidationLevel.Flag).IsValid, text);
    }

    [TestMethod]
    [DataRow("(?i:a)", true, false)]
    [DataRow("(?<a>x)|(?<a>y)", true, false)]
    [DataRow("^[a-z]+$", true, true)]
    public void TheRegexFormatIsReadInTheEditionGiven(string text, bool current, bool es2020)
    {
        // ECMAScript 2020, which draft 2019-09 and draft 2020-12 cite, has no modifier groups and no duplicate names.
        JsonString value = new(text);
        Assert.AreEqual(current, Validate.TypeRegex(value, ValidationContext.ValidContext, ValidationLevel.Flag, Corvus.Ecma262.RegularExpressions.RegexEdition.Current, Corvus.Ecma262.RegularExpressions.RegexUnicodeVersion.Default).IsValid, text);
        Assert.AreEqual(es2020, Validate.TypeRegex(value, ValidationContext.ValidContext, ValidationLevel.Flag, Corvus.Ecma262.RegularExpressions.RegexEdition.Es2020, Corvus.Ecma262.RegularExpressions.RegexUnicodeVersion.Default).IsValid, text);
        Assert.AreEqual(es2020, ValidateWithoutCoreType.TypeRegex(value, ValidationContext.ValidContext, ValidationLevel.Flag, Corvus.Ecma262.RegularExpressions.RegexEdition.Es2020, Corvus.Ecma262.RegularExpressions.RegexUnicodeVersion.Default).IsValid, text);
    }
}