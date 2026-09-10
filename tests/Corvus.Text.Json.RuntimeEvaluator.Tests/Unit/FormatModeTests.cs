// <copyright file="FormatModeTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

[TestClass]
public class FormatModeTests
{
    private const string DateTimeSchema = """{"type": "string", "format": "date-time"}""";

    [TestMethod]
    public void AssertFormatFailsNonConformingValue()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(DateTimeSchema, new JsonSchemaEvaluatorOptions { AssertFormat = true });
        Assert.IsFalse(evaluator.Evaluate("\"nope\""));
        Assert.IsTrue(evaluator.Evaluate("\"2020-01-01T00:00:00Z\""));
    }

    [TestMethod]
    public void DisableOverrideAnnotatesOnly()
    {
        var options = new JsonSchemaEvaluatorOptions
        {
            AssertFormat = true,
            FormatModes = new Dictionary<string, JsonSchemaFormatMode> { ["date-time"] = JsonSchemaFormatMode.Disable },
        };
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(DateTimeSchema, options);
        Assert.IsTrue(evaluator.Evaluate("\"nope\""));
    }

    [TestMethod]
    public void WarningOverridePassesButReportsWarning()
    {
        var options = new JsonSchemaEvaluatorOptions
        {
            AssertFormat = true,
            FormatModes = new Dictionary<string, JsonSchemaFormatMode> { ["date-time"] = JsonSchemaFormatMode.Warning },
        };
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(DateTimeSchema, options);
        Assert.IsTrue(evaluator.Evaluate("\"nope\""));

        using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        Assert.IsTrue(evaluator.Evaluate("\"nope\"", collector));
        bool sawWarning = false;
        foreach (JsonSchemaResultsCollector.Result result in collector.EnumerateResults())
        {
            if (result.IsMatch && result.GetMessageText().StartsWith("WARNING: ", StringComparison.Ordinal))
            {
                sawWarning = true;
            }
        }

        Assert.IsTrue(sawWarning, "A non-conforming value in warning mode must report a warning result.");
    }

    [TestMethod]
    public void WildcardIsOutrankedByPerFormatOverride()
    {
        const string schema = """{"type": "object", "properties": {"d": {"type": "string", "format": "date-time"}, "u": {"type": "string", "format": "uuid"}}}""";
        var options = new JsonSchemaEvaluatorOptions
        {
            AssertFormat = true,
            FormatModes = new Dictionary<string, JsonSchemaFormatMode> { ["*"] = JsonSchemaFormatMode.Disable, ["uuid"] = JsonSchemaFormatMode.Assert },
        };
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema, options);
        Assert.IsTrue(evaluator.Evaluate("""{"d": "nope"}"""));
        Assert.IsFalse(evaluator.Evaluate("""{"u": "nope"}"""));
    }

    [TestMethod]
    public void AssertOverrideAppliesWhenDialectAnnotates()
    {
        // 2020-12 annotates by default; an explicit Assert override still asserts.
        var options = new JsonSchemaEvaluatorOptions
        {
            FormatModes = new Dictionary<string, JsonSchemaFormatMode> { ["date-time"] = JsonSchemaFormatMode.Assert },
        };
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(DateTimeSchema, options);
        Assert.IsFalse(evaluator.Evaluate("\"nope\""));
    }

    [TestMethod]
    [DataRow("int32", "2147483647", "2147483648")]
    [DataRow("int32", "-2147483648", "-2147483649")]
    [DataRow("int32", "1", "1.5")]
    [DataRow("int64", "9223372036854775807", "9223372036854775808")]
    [DataRow("byte", "255", "256")]
    [DataRow("byte", "0", "-1")]
    [DataRow("uint16", "65535", "65536")]
    [DataRow("int16", "32767", "32768")]
    [DataRow("sbyte", "-128", "-129")]
    [DataRow("uint32", "4294967295", "4294967296")]
    [DataRow("uint64", "18446744073709551615", "18446744073709551616")]
    [DataRow("single", "1.5", "1e40")]
    [DataRow("double", "1.5", "1e400")]
    public void NumericFormatsAreAsserted(string format, string valid, string invalid)
    {
        string schema = $$$"""{"type": "number", "format": "{{{format}}}"}""";
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema, new JsonSchemaEvaluatorOptions { AssertFormat = true });
        Assert.IsTrue(evaluator.Evaluate(valid), $"{valid} should be a valid {format}");
        Assert.IsFalse(evaluator.Evaluate(invalid), $"{invalid} should not be a valid {format}");
    }

    [TestMethod]
    public void NumericFormatIgnoresStringsAndStringFormatIgnoresNumbers()
    {
        using JsonSchemaEvaluator numeric = JsonSchemaEvaluator.Compile("""{"format": "int32"}""", new JsonSchemaEvaluatorOptions { AssertFormat = true });
        Assert.IsTrue(numeric.Evaluate("\"not a number\""));
        Assert.IsFalse(numeric.Evaluate("3000000000"));

        using JsonSchemaEvaluator text = JsonSchemaEvaluator.Compile("""{"format": "date-time"}""", new JsonSchemaEvaluatorOptions { AssertFormat = true });
        Assert.IsTrue(text.Evaluate("42"));
        Assert.IsFalse(text.Evaluate("\"nope\""));
    }

    [TestMethod]
    public void NumericFormatWarningFallsBackToAssert()
    {
        var options = new JsonSchemaEvaluatorOptions
        {
            AssertFormat = true,
            FormatModes = new Dictionary<string, JsonSchemaFormatMode> { ["int32"] = JsonSchemaFormatMode.Warning },
        };
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile("""{"format": "int32"}""", options);
        Assert.IsFalse(evaluator.Evaluate("3000000000"));
    }
}