// <copyright file="PatternValidityTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.RuntimeEvaluator;
using Corvus.Text.Json.Tests.EcmaPatterns;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// A schema whose <c>pattern</c> or <c>patternProperties</c> pattern is not a valid ECMA-262 regular expression is
/// refused when it is compiled, with <see cref="JsonSchemaCompilationException"/>. A pattern is valid when V8 accepts
/// it with the <c>u</c> flag or without it.
/// </summary>
[TestClass]
public class PatternValidityTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ASchemaIsRefusedWhenV8RefusesItsPattern(bool generateCode)
    {
        JsonSchemaEvaluatorOptions options = Options(generateCode);
        var failures = new List<string>();
        foreach ((string pattern, bool validWithU, bool validWithoutU) in EcmaPatternCases.Validity)
        {
            if (validWithU || validWithoutU)
            {
                continue;
            }

            foreach (string schema in new[]
            {
                "{\"pattern\": " + Json(pattern) + "}",
                "{\"patternProperties\": {" + Json(pattern) + ": true}}",
            })
            {
                try
                {
                    using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema, options);
                    failures.Add($"{schema}: accepted");
                }
                catch (JsonSchemaCompilationException)
                {
                }
                catch (Exception ex)
                {
                    failures.Add($"{schema}: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        Assert.AreEqual(0, failures.Count, string.Join("\n", failures.Take(25)));
    }

    [TestMethod]
    public void TheExceptionNamesThePatternAndSaysWhatIsWrongAndWhere()
    {
        JsonSchemaCompilationException thrown = Assert.ThrowsExactly<JsonSchemaCompilationException>(
            () => JsonSchemaEvaluator.Compile("""{"type": "string", "pattern": "^[a-z]{3,1}$"}"""));

        Assert.AreEqual(
            "The pattern '^[a-z]{3,1}$' is not a valid ECMA-262 regular expression. Numbers out of order in {} quantifier at offset 11.",
            thrown.Message);
    }

    /// <summary>
    /// The matchers that take a pattern without a regular expression do not take an invalid one: each of these has
    /// the shape of a pattern one of them would match (a length range, a class sequence, a separated list, literals).
    /// </summary>
    [TestMethod]
    [DataRow("^.{5,2}$")]
    [DataRow("^[a-z]{3,1}$")]
    [DataRow("^[z-a]+$")]
    [DataRow("^a{2,1}(,a)*$")]
    [DataRow("^(?:on|off)**$")]
    [DataRow("^x-++")]
    public void APatternWithTheShapeOfAFastMatcher_IsRefusedWhenItIsNotValid(string pattern)
    {
        foreach (bool generateCode in new[] { false, true })
        {
            Assert.ThrowsExactly<JsonSchemaCompilationException>(
                () => JsonSchemaEvaluator.Compile("{\"pattern\": " + Json(pattern) + "}", Options(generateCode)));
        }
    }

    /// <summary>
    /// An evaluator for an entry point compiles the subschema it reaches, and refuses an invalid pattern there. The
    /// root schema here does not reach the subschema, so it compiles.
    /// </summary>
    [TestMethod]
    public void AnEntryPointThatReachesAnInvalidPattern_IsRefused()
    {
        using JsonSchemaEvaluator root = JsonSchemaEvaluator.Compile("""{"$defs": {"bad": {"pattern": "a{2,1}"}}, "type": "object"}""");
        Assert.IsTrue(root.Evaluate("{}"));
        Assert.ThrowsExactly<JsonSchemaCompilationException>(() => root.ForEntryPoint("#/$defs/bad"));
    }

    /// <summary>Gets a value indicating whether a schema can be compiled to IL here.</summary>
    internal static bool GeneratedCodeIsSupported =>
#if NET
        Corvus.Text.Json.RuntimeEvaluator.CodeGeneration.SchemaLowering.IsSupported;
#else
        false;
#endif

    /// <summary>A string as JSON text in ASCII, so that no character depends on an encoding.</summary>
    internal static string Json(string value)
    {
        var json = new StringBuilder("\"");
        foreach (char c in value)
        {
            json.Append("\\u").Append(((int)c).ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
        }

        return json.Append('"').ToString();
    }

    private static JsonSchemaEvaluatorOptions Options(bool generateCode)
    {
        return new JsonSchemaEvaluatorOptions
        {
            CodeGeneration = generateCode && GeneratedCodeIsSupported
                ? JsonSchemaCodeGeneration.Eager
                : JsonSchemaCodeGeneration.Disabled,
        };
    }
}