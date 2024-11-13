// <copyright file="JsonStringTryFormatStepDefinitions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using NUnit.Framework;
using TechTalk.SpecFlow;

namespace Steps;

[Binding]
public class JsonStringTryFormatStepDefinitions
{
    private const string FormattedResultKey = "FormattedResultKey";

    private readonly ScenarioContext scenarioContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonStringTryFormatStepDefinitions"/> class.
    /// </summary>
    /// <param name="scenarioContext">The scenario context.</param>
    public JsonStringTryFormatStepDefinitions(ScenarioContext scenarioContext)
    {
        this.scenarioContext = scenarioContext;
    }

#if NET8_0_OR_GREATER
    [Then(@"the formatted result should equal the string ""([^""]*)""")]
    public void ThenTheFormattedResultShouldEqualTheString(string value)
    {
        Assert.AreEqual(value, this.scenarioContext.Get<string>(FormattedResultKey));
    }

    [When(@"I TryFormat\(\) the (.*)")]
    public void WhenITryFormatTheValue(string targetType)
    {
        Span<char> buffer = stackalloc char[256];
        int charsWritten;

        bool result = targetType switch
        {
            "JsonString" => this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest).TryFormat(buffer, out charsWritten, ReadOnlySpan<char>.Empty, null),
            "JsonDate" => this.scenarioContext.Get<JsonDate>(JsonValueSteps.SubjectUnderTest).TryFormat(buffer, out charsWritten, ReadOnlySpan<char>.Empty, null),
            "JsonDateTime" => this.scenarioContext.Get<JsonDateTime>(JsonValueSteps.SubjectUnderTest).TryFormat(buffer, out charsWritten, ReadOnlySpan<char>.Empty, null),
            "JsonDuration" => this.scenarioContext.Get<JsonDuration>(JsonValueSteps.SubjectUnderTest).TryFormat(buffer, out charsWritten, ReadOnlySpan<char>.Empty, null),
            "JsonEmail" => this.scenarioContext.Get<JsonEmail>(JsonValueSteps.SubjectUnderTest).TryFormat(buffer, out charsWritten, ReadOnlySpan<char>.Empty, null),
            "JsonHostname" => this.scenarioContext.Get<JsonHostname>(JsonValueSteps.SubjectUnderTest).TryFormat(buffer, out charsWritten, ReadOnlySpan<char>.Empty, null),
            "JsonIdnEmail" => this.scenarioContext.Get<JsonIdnEmail>(JsonValueSteps.SubjectUnderTest).TryFormat(buffer, out charsWritten, ReadOnlySpan<char>.Empty, null),
            "JsonIdnHostname" => this.scenarioContext.Get<JsonIdnHostname>(JsonValueSteps.SubjectUnderTest).TryFormat(buffer, out charsWritten, ReadOnlySpan<char>.Empty, null),
            "JsonIpV4" => this.scenarioContext.Get<JsonIpV4>(JsonValueSteps.SubjectUnderTest).TryFormat(buffer, out charsWritten, ReadOnlySpan<char>.Empty, null),
            "JsonIpV6" => this.scenarioContext.Get<JsonIpV6>(JsonValueSteps.SubjectUnderTest).TryFormat(buffer, out charsWritten, ReadOnlySpan<char>.Empty, null),
            "JsonIri" => this.scenarioContext.Get<JsonIri>(JsonValueSteps.SubjectUnderTest).TryFormat(buffer, out charsWritten, ReadOnlySpan<char>.Empty, null),
            "JsonIriReference" => this.scenarioContext.Get<JsonIriReference>(JsonValueSteps.SubjectUnderTest).TryFormat(buffer, out charsWritten, ReadOnlySpan<char>.Empty, null),
            "JsonPointer" => this.scenarioContext.Get<JsonPointer>(JsonValueSteps.SubjectUnderTest).TryFormat(buffer, out charsWritten, ReadOnlySpan<char>.Empty, null),
            "JsonRegex" => this.scenarioContext.Get<JsonRegex>(JsonValueSteps.SubjectUnderTest).TryFormat(buffer, out charsWritten, ReadOnlySpan<char>.Empty, null),
            "JsonRelativePointer" => this.scenarioContext.Get<JsonRelativePointer>(JsonValueSteps.SubjectUnderTest).TryFormat(buffer, out charsWritten, ReadOnlySpan<char>.Empty, null),
            "JsonTime" => this.scenarioContext.Get<JsonTime>(JsonValueSteps.SubjectUnderTest).TryFormat(buffer, out charsWritten, ReadOnlySpan<char>.Empty, null),
            "JsonUri" => this.scenarioContext.Get<JsonUri>(JsonValueSteps.SubjectUnderTest).TryFormat(buffer, out charsWritten, ReadOnlySpan<char>.Empty, null),
            "JsonUriReference" => this.scenarioContext.Get<JsonUriReference>(JsonValueSteps.SubjectUnderTest).TryFormat(buffer, out charsWritten, ReadOnlySpan<char>.Empty, null),
            "JsonUriTemplate" => this.scenarioContext.Get<JsonUriTemplate>(JsonValueSteps.SubjectUnderTest).TryFormat(buffer, out charsWritten, ReadOnlySpan<char>.Empty, null),
            "JsonUuid" => this.scenarioContext.Get<JsonUuid>(JsonValueSteps.SubjectUnderTest).TryFormat(buffer, out charsWritten, ReadOnlySpan<char>.Empty, null),
            "JsonContent" => this.scenarioContext.Get<JsonContent>(JsonValueSteps.SubjectUnderTest).TryFormat(buffer, out charsWritten, ReadOnlySpan<char>.Empty, null),
            "JsonContentPre201909" => this.scenarioContext.Get<JsonContentPre201909>(JsonValueSteps.SubjectUnderTest).TryFormat(buffer, out charsWritten, ReadOnlySpan<char>.Empty, null),
            "JsonBase64Content" => this.scenarioContext.Get<JsonBase64Content>(JsonValueSteps.SubjectUnderTest).TryFormat(buffer, out charsWritten, ReadOnlySpan<char>.Empty, null),
            "JsonBase64ContentPre201909" => this.scenarioContext.Get<JsonBase64ContentPre201909>(JsonValueSteps.SubjectUnderTest).TryFormat(buffer, out charsWritten, ReadOnlySpan<char>.Empty, null),
            "JsonBase64String" => this.scenarioContext.Get<JsonBase64String>(JsonValueSteps.SubjectUnderTest).TryFormat(buffer, out charsWritten, ReadOnlySpan<char>.Empty, null),
            "JsonBase64StringPre201909" => this.scenarioContext.Get<JsonBase64StringPre201909>(JsonValueSteps.SubjectUnderTest).TryFormat(buffer, out charsWritten, ReadOnlySpan<char>.Empty, null),
            _ => throw new InvalidOperationException("Unsupported type"),
        };

        this.scenarioContext.Set(buffer[..charsWritten].ToString(), FormattedResultKey);
    }
#endif
}