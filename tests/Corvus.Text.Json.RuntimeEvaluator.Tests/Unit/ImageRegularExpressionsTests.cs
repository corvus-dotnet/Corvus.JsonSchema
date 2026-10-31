// <copyright file="ImageRegularExpressionsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Ecma262.RegularExpressions;
using Corvus.Text.Json.RuntimeEvaluator;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// <see cref="JsonSchemaEvaluator.GetImageRegularExpressions"/>: the regular expressions a program image's evaluator
/// matches its patterns with, as a code generator writes compiled matchers for them.
/// </summary>
[TestClass]
public class ImageRegularExpressionsTests
{
    // One pattern the evaluator matches with Corvus.Regex.Ecma262 (four alternatives), one it matches with a matcher
    // of its own (a single sequence), and one that only the grammar with no u flag accepts.
    private const string Schema =
        """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "properties": {
            "stage": { "type": "string", "pattern": "^(alpha|beta|gamma|delta)-[0-9]+$" },
            "name": { "type": "string", "pattern": "^[a-z][a-z0-9]*$" },
            "legacy": { "type": "string", "pattern": "^a\\-(b|c|d)+{$" }
          }
        }
        """;

    [TestMethod]
    public void TheRegularExpressionsAreThoseTheEvaluatorCompiles()
    {
        byte[] image = Image(Schema);
        IReadOnlyList<string> table = JsonSchemaEvaluator.GetImagePatterns(image);
        JsonSchemaImageRegularExpression[] regexes = [.. JsonSchemaEvaluator.GetImageRegularExpressions(image, new JsonSchemaEvaluatorOptions())];

        Assert.AreEqual(2, regexes.Length, string.Join(", ", regexes.Select(r => r.Pattern)));
        JsonSchemaImageRegularExpression stage = regexes.Single(r => r.Pattern.StartsWith("^(alpha", StringComparison.Ordinal));
        Assert.AreEqual(stage.Pattern, table[stage.Index]);
        Assert.AreEqual(RegexFlags.Unicode, stage.Flags);
        Assert.AreEqual(RegexEdition.Es2020, stage.Edition, "Draft 2020-12 reads patterns with the grammar of ECMAScript 2020.");
        Assert.AreEqual(RegexUnicodeVersion.V17, stage.UnicodeVersion, "The default version, resolved.");

        JsonSchemaImageRegularExpression legacy = regexes.Single(r => r.Pattern.StartsWith("^a", StringComparison.Ordinal));
        Assert.AreEqual(RegexFlags.None, legacy.Flags, "A pattern valid only without the u flag is compiled with none.");
        Assert.IsFalse(regexes.Any(r => r.Pattern == "^[a-z][a-z0-9]*$"), "A pattern a matcher of the evaluator's own takes needs no regular expression.");
    }

    [TestMethod]
    public void TheOptionsTheProgramLoadsWithDecideTheEditionAndVersion()
    {
        byte[] image = Image(Schema);
        JsonSchemaImageRegularExpression stage = JsonSchemaEvaluator.GetImageRegularExpressions(
            image,
            new JsonSchemaEvaluatorOptions { RegexEdition = RegexEdition.Current, RegexUnicodeVersion = RegexUnicodeVersion.V13 })
            .Single(r => r.Pattern.StartsWith("^(alpha", StringComparison.Ordinal));
        Assert.AreEqual(RegexEdition.Current, stage.Edition);
        Assert.AreEqual(RegexUnicodeVersion.V13, stage.UnicodeVersion);

        byte[] draft7 = Image(Schema.Replace("https://json-schema.org/draft/2020-12/schema", "http://json-schema.org/draft-07/schema#"));
        Assert.IsTrue(JsonSchemaEvaluator.GetImageRegularExpressions(draft7, new JsonSchemaEvaluatorOptions()).All(r => r.Edition == RegexEdition.Current), "Draft 7 reads patterns with today's grammar.");
    }

    [TestMethod]
    public void AProgramGivenTheRegularExpressionsByAProviderUsesThem()
    {
        byte[] image = Image(Schema);
        JsonSchemaImageRegularExpression[] regexes = [.. JsonSchemaEvaluator.GetImageRegularExpressions(image, new JsonSchemaEvaluatorOptions())];
        var provided = new Dictionary<int, Regex>();
        foreach (JsonSchemaImageRegularExpression r in regexes)
        {
            provided[r.Index] = new Regex(r.Pattern, r.Flags, Regex.InfiniteMatchTimeout, r.Edition, r.UnicodeVersion);
        }

        int asked = 0;
        var options = new JsonSchemaEvaluatorOptions
        {
            EcmaRegexProvider = (index, pattern) =>
            {
                asked++;
                return provided.TryGetValue(index, out Regex? regex) ? regex : null;
            },
        };

        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.FromProgramImage(image, options);
        Assert.AreEqual(regexes.Length, asked, "The provider is asked once for each regular expression the evaluator needs.");
        Assert.IsTrue(evaluator.Evaluate("""{"stage": "beta-12", "name": "a1", "legacy": "a-bc{"}"""));
        Assert.IsFalse(evaluator.Evaluate("""{"stage": "omega-12"}"""));
    }

    private static byte[] Image(string schema)
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema);
        return evaluator.ToProgramImage();
    }
}