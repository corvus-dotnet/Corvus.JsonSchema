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

    [When("you try get an integer from the JsonString using a char parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonStringUsingACharParser(int multiplier)
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

    [When("you try get an integer from the JsonString using a utf8 parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonStringUsingAUtf8Parser(int multiplier)
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

    [When("you try get an integer from the JsonDate using a char parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonDateUsingACharParser(int multiplier)
    {
        JsonDate subjectUnderTest = this.scenarioContext.Get<JsonDate>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonDate using a utf8 parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonDateUsingAUtf8Parser(int multiplier)
    {
        JsonDate subjectUnderTest = this.scenarioContext.Get<JsonDate>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonDateTime using a char parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonDateTimeUsingACharParser(int multiplier)
    {
        JsonDateTime subjectUnderTest = this.scenarioContext.Get<JsonDateTime>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonDateTime using a utf8 parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonDateTimeUsingAUtf8Parser(int multiplier)
    {
        JsonDateTime subjectUnderTest = this.scenarioContext.Get<JsonDateTime>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonDuration using a char parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonDurationUsingACharParser(int multiplier)
    {
        JsonDuration subjectUnderTest = this.scenarioContext.Get<JsonDuration>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonDuration using a utf8 parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonDurationUsingAUtf8Parser(int multiplier)
    {
        JsonDuration subjectUnderTest = this.scenarioContext.Get<JsonDuration>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonEmail using a char parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonEmailUsingACharParser(int multiplier)
    {
        JsonEmail subjectUnderTest = this.scenarioContext.Get<JsonEmail>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonEmail using a utf8 parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonEmailUsingAUtf8Parser(int multiplier)
    {
        JsonEmail subjectUnderTest = this.scenarioContext.Get<JsonEmail>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonHostname using a char parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonHostnameUsingACharParser(int multiplier)
    {
        JsonHostname subjectUnderTest = this.scenarioContext.Get<JsonHostname>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonHostname using a utf8 parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonHostnameUsingAUtf8Parser(int multiplier)
    {
        JsonHostname subjectUnderTest = this.scenarioContext.Get<JsonHostname>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonIdnEmail using a char parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonIdnEmailUsingACharParser(int multiplier)
    {
        JsonIdnEmail subjectUnderTest = this.scenarioContext.Get<JsonIdnEmail>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonIdnEmail using a utf8 parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonIdnEmailUsingAUtf8Parser(int multiplier)
    {
        JsonIdnEmail subjectUnderTest = this.scenarioContext.Get<JsonIdnEmail>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonIdnHostname using a char parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonIdnHostnameUsingACharParser(int multiplier)
    {
        JsonIdnHostname subjectUnderTest = this.scenarioContext.Get<JsonIdnHostname>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonIdnHostname using a utf8 parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonIdnHostnameUsingAUtf8Parser(int multiplier)
    {
        JsonIdnHostname subjectUnderTest = this.scenarioContext.Get<JsonIdnHostname>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonIpV4 using a char parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonIpV4UsingACharParser(int multiplier)
    {
        JsonIpV4 subjectUnderTest = this.scenarioContext.Get<JsonIpV4>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonIpV4 using a utf8 parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonIpV4UsingAUtf8Parser(int multiplier)
    {
        JsonIpV4 subjectUnderTest = this.scenarioContext.Get<JsonIpV4>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonIpV6 using a char parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonIpV6UsingACharParser(int multiplier)
    {
        JsonIpV6 subjectUnderTest = this.scenarioContext.Get<JsonIpV6>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonIpV6 using a utf8 parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonIpV6UsingAUtf8Parser(int multiplier)
    {
        JsonIpV6 subjectUnderTest = this.scenarioContext.Get<JsonIpV6>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonIri using a char parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonIriUsingACharParser(int multiplier)
    {
        JsonIri subjectUnderTest = this.scenarioContext.Get<JsonIri>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonIri using a utf8 parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonIriUsingAUtf8Parser(int multiplier)
    {
        JsonIri subjectUnderTest = this.scenarioContext.Get<JsonIri>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonIriReference using a char parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonIriReferenceUsingACharParser(int multiplier)
    {
        JsonIriReference subjectUnderTest = this.scenarioContext.Get<JsonIriReference>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonIriReference using a utf8 parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonIriReferenceUsingAUtf8Parser(int multiplier)
    {
        JsonIriReference subjectUnderTest = this.scenarioContext.Get<JsonIriReference>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonPointer using a char parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonPointerUsingACharParser(int multiplier)
    {
        JsonPointer subjectUnderTest = this.scenarioContext.Get<JsonPointer>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonPointer using a utf8 parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonPointerUsingAUtf8Parser(int multiplier)
    {
        JsonPointer subjectUnderTest = this.scenarioContext.Get<JsonPointer>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonRegex using a char parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonRegexUsingACharParser(int multiplier)
    {
        JsonRegex subjectUnderTest = this.scenarioContext.Get<JsonRegex>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonRegex using a utf8 parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonRegexUsingAUtf8Parser(int multiplier)
    {
        JsonRegex subjectUnderTest = this.scenarioContext.Get<JsonRegex>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonRelativePointer using a char parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonRelativePointerUsingACharParser(int multiplier)
    {
        JsonRelativePointer subjectUnderTest = this.scenarioContext.Get<JsonRelativePointer>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonRelativePointer using a utf8 parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonRelativePointerUsingAUtf8Parser(int multiplier)
    {
        JsonRelativePointer subjectUnderTest = this.scenarioContext.Get<JsonRelativePointer>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonTime using a char parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonTimeUsingACharParser(int multiplier)
    {
        JsonTime subjectUnderTest = this.scenarioContext.Get<JsonTime>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonTime using a utf8 parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonTimeUsingAUtf8Parser(int multiplier)
    {
        JsonTime subjectUnderTest = this.scenarioContext.Get<JsonTime>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonUri using a char parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonUriUsingACharParser(int multiplier)
    {
        JsonUri subjectUnderTest = this.scenarioContext.Get<JsonUri>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonUri using a utf8 parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonUriUsingAUtf8Parser(int multiplier)
    {
        JsonUri subjectUnderTest = this.scenarioContext.Get<JsonUri>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonUriReference using a char parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonUriReferenceUsingACharParser(int multiplier)
    {
        JsonUriReference subjectUnderTest = this.scenarioContext.Get<JsonUriReference>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonUriReference using a utf8 parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonUriReferenceUsingAUtf8Parser(int multiplier)
    {
        JsonUriReference subjectUnderTest = this.scenarioContext.Get<JsonUriReference>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonUuid using a char parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonUuidUsingACharParser(int multiplier)
    {
        JsonUuid subjectUnderTest = this.scenarioContext.Get<JsonUuid>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonUuid using a utf8 parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonUuidUsingAUtf8Parser(int multiplier)
    {
        JsonUuid subjectUnderTest = this.scenarioContext.Get<JsonUuid>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonContent using a char parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonContentUsingACharParser(int multiplier)
    {
        JsonContent subjectUnderTest = this.scenarioContext.Get<JsonContent>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonContent using a utf8 parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonContentUsingAUtf8Parser(int multiplier)
    {
        JsonContent subjectUnderTest = this.scenarioContext.Get<JsonContent>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonBase64Content using a char parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonBase64ContentUsingACharParser(int multiplier)
    {
        JsonBase64Content subjectUnderTest = this.scenarioContext.Get<JsonBase64Content>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonBase64Content using a utf8 parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonBase64ContentUsingAUtf8Parser(int multiplier)
    {
        JsonBase64Content subjectUnderTest = this.scenarioContext.Get<JsonBase64Content>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonBase64ContentPre201909 using a char parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonBase64ContentPre201909UsingACharParser(int multiplier)
    {
        JsonBase64ContentPre201909 subjectUnderTest = this.scenarioContext.Get<JsonBase64ContentPre201909>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonBase64ContentPre201909 using a utf8 parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonBase64ContentPre201909UsingAUtf8Parser(int multiplier)
    {
        JsonBase64ContentPre201909 subjectUnderTest = this.scenarioContext.Get<JsonBase64ContentPre201909>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonBase64String using a char parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonBase64StringUsingACharParser(int multiplier)
    {
        JsonBase64String subjectUnderTest = this.scenarioContext.Get<JsonBase64String>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonBase64String using a utf8 parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonBase64StringUsingAUtf8Parser(int multiplier)
    {
        JsonBase64String subjectUnderTest = this.scenarioContext.Get<JsonBase64String>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonBase64StringPre201909 using a char parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonBase64StringPre201909UsingACharParser(int multiplier)
    {
        JsonBase64StringPre201909 subjectUnderTest = this.scenarioContext.Get<JsonBase64StringPre201909>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonBase64StringPre201909 using a utf8 parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonBase64StringPre201909UsingAUtf8Parser(int multiplier)
    {
        JsonBase64StringPre201909 subjectUnderTest = this.scenarioContext.Get<JsonBase64StringPre201909>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonContentPre201909 using a char parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonContentPre201909UsingACharParser(int multiplier)
    {
        JsonContentPre201909 subjectUnderTest = this.scenarioContext.Get<JsonContentPre201909>(JsonValueSteps.SubjectUnderTest);

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

    [When("you try get an integer from the JsonContentPre201909 using a utf8 parser with the multiplier (.*)")]
    public void WhenYouTryGetAnIntegerFromTheJsonContentPre201909UsingAUtf8Parser(int multiplier)
    {
        JsonContentPre201909 subjectUnderTest = this.scenarioContext.Get<JsonContentPre201909>(JsonValueSteps.SubjectUnderTest);

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