// <copyright file="JsonStringTryGetValueSteps.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text;
using Corvus.Json;
using NUnit.Framework;
using TechTalk.SpecFlow;

namespace Steps;

[Binding]
public class JsonStringTryGetValueSteps
{
    /// <summary>
    /// The key for a parse result.
    /// </summary>
    internal const string TryParseResult = "TryParseResult";

    private readonly ScenarioContext scenarioContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonValueCastSteps"/> class.
    /// </summary>
    /// <param name="scenarioContext">The scenario context.</param>
    public JsonStringTryGetValueSteps(ScenarioContext scenarioContext)
    {
        this.scenarioContext = scenarioContext;
    }

    [When("you try get an integer from the json value using a char parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonValueUsingACharParser(int multiplier)
    {
        JsonString subjectUnderTest = this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest);

        bool success = subjectUnderTest.TryGetValue(TryGetIntegerUsingChar, multiplier, out int? result);

        this.scenarioContext.Set(new ParseResult(success, result), TryParseResult);

        static bool TryGetIntegerUsingChar(ReadOnlySpan<char> span, in int state, [NotNullWhen(true)] out int? value)
        {
#if NET8_0_OR_GREATER
            if (int.TryParse(span, out int baseValue))
#else
            if (int.TryParse(span.ToString(), out int baseValue))
#endif
            {
                value = baseValue * state;
                return true;
            }

            value = default;
            return false;
        }
    }

    [When("you try get an integer from the json value using a utf8 parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonValueUsingAUtf8Parser(int multiplier)
    {
        JsonString subjectUnderTest = this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest);

        bool success = subjectUnderTest.TryGetValue(TryGetIntegerUsingUtf8, multiplier, out int? result);

        this.scenarioContext.Set(new ParseResult(success, result), TryParseResult);

        static bool TryGetIntegerUsingUtf8(ReadOnlySpan<byte> span, in int state, [NotNullWhen(true)] out int? value)
        {
#if NET8_0_OR_GREATER
            if (int.TryParse(Encoding.UTF8.GetString(span), out int baseValue))
#else
            if (int.TryParse(Encoding.UTF8.GetString(span.ToArray()), out int baseValue))
#endif
            {
                value = baseValue * state;
                return true;
            }

            value = default;
            return false;
        }
    }

    [Then("the parse result should be true")]
    public void ThenTheParseResultShouldBeTrue()
    {
        ParseResult result = this.scenarioContext.Get<ParseResult>(TryParseResult);
        Assert.IsTrue(result.Success);
    }

    [Then("the parse result should be false")]
    public void ThenTheParseResultShouldBeFalse()
    {
        ParseResult result = this.scenarioContext.Get<ParseResult>(TryParseResult);
        Assert.IsFalse(result.Success);
    }

    [Then("the parsed value should be equal to the number (.*)")]
    public void ThenTheParsedValueShouldBeEqualToTheNumber(int expected)
    {
        ParseResult result = this.scenarioContext.Get<ParseResult>(TryParseResult);
        Assert.AreEqual(expected, result.Value);
    }

    [Then("the parsed value should be null")]
    public void ThenTheParsedValueShouldBeNull()
    {
        ParseResult result = this.scenarioContext.Get<ParseResult>(TryParseResult);
        Assert.IsNull(result.Value);
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// The result of a TryParse() operation.
    /// </summary>
    /// <param name="Success">Captures the return value of TryParse().</param>
    /// <param name="Value">Captures the value produced by TryParse().</param>
    internal readonly record struct ParseResult(bool Success, int? Value);
#else
    /// <summary>
    /// The result of a TryParse() operation.
    /// </summary>
    internal readonly struct ParseResult
    {
        /// <summary>
        /// Create and instance of a <see cref="ParseResult"/>.
        /// </summary>
        /// <param name="success">Captures the return value of TryParse().</param>
        /// <param name="value">Captures the value produced by TryParse().</param>
        public ParseResult(bool success, int? value)
        {
            this.Success = success;
            this.Value = value;
        }

        public bool Success { get; }

        public int? Value { get; }
    }
#endif
}