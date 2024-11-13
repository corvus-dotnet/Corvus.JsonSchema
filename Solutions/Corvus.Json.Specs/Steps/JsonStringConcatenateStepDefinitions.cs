// <copyright file="JsonStringConcatenateStepDefinitions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using NUnit.Framework;
using TechTalk.SpecFlow;

namespace Steps;

[Binding]
public class JsonStringConcatenateStepDefinitions
{
    /// <summary>
    /// The key for a concatenation result.
    /// </summary>
    internal const string ConcatenateResult = "ConcatenateResult";

    private readonly ScenarioContext scenarioContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonStringConcatenateStepDefinitions"/> class.
    /// </summary>
    /// <param name="scenarioContext">The scenario context.</param>
    public JsonStringConcatenateStepDefinitions(ScenarioContext scenarioContext)
    {
        this.scenarioContext = scenarioContext;
    }

    [When("the values are concatenated to a (.*)")]
    public void WhenTheValuesAreConcatenatedToA(string typeName)
    {
        JsonArray subjectUnderTest = this.scenarioContext.Get<JsonArray>(JsonValueSteps.SubjectUnderTest);
        Span<byte> buffer = stackalloc byte[256];
        IJsonValue result = typeName switch
        {
            "JsonString" => ConcatenateResultsJsonString(subjectUnderTest, buffer),
            "JsonDate" => ConcatenateResultsJsonDate(subjectUnderTest, buffer),
            "JsonDateTime" => ConcatenateResultsJsonDateTime(subjectUnderTest, buffer),
            "JsonDuration" => ConcatenateResultsJsonDuration(subjectUnderTest, buffer),
            "JsonEmail" => ConcatenateResultsJsonEmail(subjectUnderTest, buffer),
            "JsonHostname" => ConcatenateResultsJsonHostname(subjectUnderTest, buffer),
            "JsonIdnEmail" => ConcatenateResultsJsonIdnEmail(subjectUnderTest, buffer),
            "JsonIdnHostname" => ConcatenateResultsJsonIdnHostname(subjectUnderTest, buffer),
            "JsonIpV4" => ConcatenateResultsJsonIpV4(subjectUnderTest, buffer),
            "JsonIpV6" => ConcatenateResultsJsonIpV6(subjectUnderTest, buffer),
            "JsonIri" => ConcatenateResultsJsonIri(subjectUnderTest, buffer),
            "JsonIriReference" => ConcatenateResultsJsonIriReference(subjectUnderTest, buffer),
            "JsonPointer" => ConcatenateResultsJsonPointer(subjectUnderTest, buffer),
            "JsonRegex" => ConcatenateResultsJsonRegex(subjectUnderTest, buffer),
            "JsonRelativePointer" => ConcatenateResultsJsonRelativePointer(subjectUnderTest, buffer),
            "JsonTime" => ConcatenateResultsJsonTime(subjectUnderTest, buffer),
            "JsonUri" => ConcatenateResultsJsonUri(subjectUnderTest, buffer),
            "JsonUriReference" => ConcatenateResultsJsonUriReference(subjectUnderTest, buffer),
            "JsonUriTemplate" => ConcatenateResultsJsonUriTemplate(subjectUnderTest, buffer),
            "JsonUuid" => ConcatenateResultsJsonUuid(subjectUnderTest, buffer),
            "JsonContent" => ConcatenateResultsJsonContent(subjectUnderTest, buffer),
            "JsonContentPre201909" => ConcatenateResultsJsonContentPre201909(subjectUnderTest, buffer),
            "JsonBase64Content" => ConcatenateResultsJsonBase64Content(subjectUnderTest, buffer),
            "JsonBase64ContentPre201909" => ConcatenateResultsJsonBase64ContentPre201909(subjectUnderTest, buffer),
            "JsonBase64String" => ConcatenateResultsJsonBase64String(subjectUnderTest, buffer),
            "JsonBase64StringPre201909" => ConcatenateResultsJsonBase64StringPre201909(subjectUnderTest, buffer),
            _ => throw new InvalidOperationException("Unsupported type"),
        };

        if (!this.scenarioContext.TryGetValue(ConcatenateResult, out List<IJsonValue>? list))
        {
            list = [result];
            this.scenarioContext.Set(list, ConcatenateResult);
        }
        else
        {
            list!.Add(result);
        }
    }

    [Then("the results should be equal to the JsonString (.*)")]
    public void WhenTheResultsShouldBeEqualToTheJsonString(string result)
    {
        var stringValue = new JsonString(result);
        foreach (IJsonValue value in this.scenarioContext.Get<List<IJsonValue>>(ConcatenateResult))
        {
            Assert.AreEqual(stringValue, value.AsString);
        }
    }

    private static IJsonValue ConcatenateResultsJsonString(JsonArray subjectUnderTest, Span<byte> buffer)
    {
        return subjectUnderTest.GetArrayLength() switch
        {
            2 => JsonString.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1]),
            3 => JsonString.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2]),
            4 => JsonString.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3]),
            5 => JsonString.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4]),
            6 => JsonString.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5]),
            7 => JsonString.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6]),
            8 => JsonString.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6], subjectUnderTest[7]),
            _ => throw new InvalidOperationException("Unsupported number of items"),
        };
    }

    private static IJsonValue ConcatenateResultsJsonDate(JsonArray subjectUnderTest, Span<byte> buffer)
    {
        return subjectUnderTest.GetArrayLength() switch
        {
            2 => JsonDate.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1]),
            3 => JsonDate.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2]),
            4 => JsonDate.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3]),
            5 => JsonDate.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4]),
            6 => JsonDate.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5]),
            7 => JsonDate.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6]),
            8 => JsonDate.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6], subjectUnderTest[7]),
            _ => throw new InvalidOperationException("Unsupported number of items"),
        };
    }

    private static IJsonValue ConcatenateResultsJsonDateTime(JsonArray subjectUnderTest, Span<byte> buffer)
    {
        return subjectUnderTest.GetArrayLength() switch
        {
            2 => JsonDateTime.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1]),
            3 => JsonDateTime.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2]),
            4 => JsonDateTime.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3]),
            5 => JsonDateTime.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4]),
            6 => JsonDateTime.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5]),
            7 => JsonDateTime.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6]),
            8 => JsonDateTime.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6], subjectUnderTest[7]),
            _ => throw new InvalidOperationException("Unsupported number of items"),
        };
    }

    private static IJsonValue ConcatenateResultsJsonDuration(JsonArray subjectUnderTest, Span<byte> buffer)
    {
        return subjectUnderTest.GetArrayLength() switch
        {
            2 => JsonDuration.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1]),
            3 => JsonDuration.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2]),
            4 => JsonDuration.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3]),
            5 => JsonDuration.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4]),
            6 => JsonDuration.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5]),
            7 => JsonDuration.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6]),
            8 => JsonDuration.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6], subjectUnderTest[7]),
            _ => throw new InvalidOperationException("Unsupported number of items"),
        };
    }

    private static IJsonValue ConcatenateResultsJsonEmail(JsonArray subjectUnderTest, Span<byte> buffer)
    {
        return subjectUnderTest.GetArrayLength() switch
        {
            2 => JsonEmail.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1]),
            3 => JsonEmail.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2]),
            4 => JsonEmail.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3]),
            5 => JsonEmail.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4]),
            6 => JsonEmail.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5]),
            7 => JsonEmail.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6]),
            8 => JsonEmail.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6], subjectUnderTest[7]),
            _ => throw new InvalidOperationException("Unsupported number of items"),
        };
    }

    private static IJsonValue ConcatenateResultsJsonHostname(JsonArray subjectUnderTest, Span<byte> buffer)
    {
        return subjectUnderTest.GetArrayLength() switch
        {
            2 => JsonHostname.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1]),
            3 => JsonHostname.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2]),
            4 => JsonHostname.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3]),
            5 => JsonHostname.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4]),
            6 => JsonHostname.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5]),
            7 => JsonHostname.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6]),
            8 => JsonHostname.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6], subjectUnderTest[7]),
            _ => throw new InvalidOperationException("Unsupported number of items"),
        };
    }

    private static IJsonValue ConcatenateResultsJsonIdnEmail(JsonArray subjectUnderTest, Span<byte> buffer)
    {
        return subjectUnderTest.GetArrayLength() switch
        {
            2 => JsonIdnEmail.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1]),
            3 => JsonIdnEmail.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2]),
            4 => JsonIdnEmail.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3]),
            5 => JsonIdnEmail.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4]),
            6 => JsonIdnEmail.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5]),
            7 => JsonIdnEmail.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6]),
            8 => JsonIdnEmail.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6], subjectUnderTest[7]),
            _ => throw new InvalidOperationException("Unsupported number of items"),
        };
    }

    private static IJsonValue ConcatenateResultsJsonIdnHostname(JsonArray subjectUnderTest, Span<byte> buffer)
    {
        return subjectUnderTest.GetArrayLength() switch
        {
            2 => JsonIdnHostname.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1]),
            3 => JsonIdnHostname.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2]),
            4 => JsonIdnHostname.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3]),
            5 => JsonIdnHostname.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4]),
            6 => JsonIdnHostname.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5]),
            7 => JsonIdnHostname.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6]),
            8 => JsonIdnHostname.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6], subjectUnderTest[7]),
            _ => throw new InvalidOperationException("Unsupported number of items"),
        };
    }

    private static IJsonValue ConcatenateResultsJsonIpV4(JsonArray subjectUnderTest, Span<byte> buffer)
    {
        return subjectUnderTest.GetArrayLength() switch
        {
            2 => JsonIpV4.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1]),
            3 => JsonIpV4.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2]),
            4 => JsonIpV4.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3]),
            5 => JsonIpV4.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4]),
            6 => JsonIpV4.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5]),
            7 => JsonIpV4.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6]),
            8 => JsonIpV4.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6], subjectUnderTest[7]),
            _ => throw new InvalidOperationException("Unsupported number of items"),
        };
    }

    private static IJsonValue ConcatenateResultsJsonIpV6(JsonArray subjectUnderTest, Span<byte> buffer)
    {
        return subjectUnderTest.GetArrayLength() switch
        {
            2 => JsonIpV6.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1]),
            3 => JsonIpV6.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2]),
            4 => JsonIpV6.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3]),
            5 => JsonIpV6.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4]),
            6 => JsonIpV6.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5]),
            7 => JsonIpV6.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6]),
            8 => JsonIpV6.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6], subjectUnderTest[7]),
            _ => throw new InvalidOperationException("Unsupported number of items"),
        };
    }

    private static IJsonValue ConcatenateResultsJsonIri(JsonArray subjectUnderTest, Span<byte> buffer)
    {
        return subjectUnderTest.GetArrayLength() switch
        {
            2 => JsonIri.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1]),
            3 => JsonIri.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2]),
            4 => JsonIri.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3]),
            5 => JsonIri.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4]),
            6 => JsonIri.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5]),
            7 => JsonIri.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6]),
            8 => JsonIri.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6], subjectUnderTest[7]),
            _ => throw new InvalidOperationException("Unsupported number of items"),
        };
    }

    private static IJsonValue ConcatenateResultsJsonIriReference(JsonArray subjectUnderTest, Span<byte> buffer)
    {
        return subjectUnderTest.GetArrayLength() switch
        {
            2 => JsonIriReference.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1]),
            3 => JsonIriReference.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2]),
            4 => JsonIriReference.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3]),
            5 => JsonIriReference.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4]),
            6 => JsonIriReference.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5]),
            7 => JsonIriReference.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6]),
            8 => JsonIriReference.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6], subjectUnderTest[7]),
            _ => throw new InvalidOperationException("Unsupported number of items"),
        };
    }

    private static IJsonValue ConcatenateResultsJsonPointer(JsonArray subjectUnderTest, Span<byte> buffer)
    {
        return subjectUnderTest.GetArrayLength() switch
        {
            2 => JsonPointer.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1]),
            3 => JsonPointer.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2]),
            4 => JsonPointer.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3]),
            5 => JsonPointer.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4]),
            6 => JsonPointer.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5]),
            7 => JsonPointer.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6]),
            8 => JsonPointer.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6], subjectUnderTest[7]),
            _ => throw new InvalidOperationException("Unsupported number of items"),
        };
    }

    private static IJsonValue ConcatenateResultsJsonRegex(JsonArray subjectUnderTest, Span<byte> buffer)
    {
        return subjectUnderTest.GetArrayLength() switch
        {
            2 => JsonRegex.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1]),
            3 => JsonRegex.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2]),
            4 => JsonRegex.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3]),
            5 => JsonRegex.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4]),
            6 => JsonRegex.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5]),
            7 => JsonRegex.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6]),
            8 => JsonRegex.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6], subjectUnderTest[7]),
            _ => throw new InvalidOperationException("Unsupported number of items"),
        };
    }

    private static IJsonValue ConcatenateResultsJsonRelativePointer(JsonArray subjectUnderTest, Span<byte> buffer)
    {
        return subjectUnderTest.GetArrayLength() switch
        {
            2 => JsonRelativePointer.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1]),
            3 => JsonRelativePointer.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2]),
            4 => JsonRelativePointer.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3]),
            5 => JsonRelativePointer.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4]),
            6 => JsonRelativePointer.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5]),
            7 => JsonRelativePointer.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6]),
            8 => JsonRelativePointer.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6], subjectUnderTest[7]),
            _ => throw new InvalidOperationException("Unsupported number of items"),
        };
    }

    private static IJsonValue ConcatenateResultsJsonTime(JsonArray subjectUnderTest, Span<byte> buffer)
    {
        return subjectUnderTest.GetArrayLength() switch
        {
            2 => JsonTime.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1]),
            3 => JsonTime.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2]),
            4 => JsonTime.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3]),
            5 => JsonTime.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4]),
            6 => JsonTime.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5]),
            7 => JsonTime.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6]),
            8 => JsonTime.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6], subjectUnderTest[7]),
            _ => throw new InvalidOperationException("Unsupported number of items"),
        };
    }

    private static IJsonValue ConcatenateResultsJsonUri(JsonArray subjectUnderTest, Span<byte> buffer)
    {
        return subjectUnderTest.GetArrayLength() switch
        {
            2 => JsonUri.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1]),
            3 => JsonUri.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2]),
            4 => JsonUri.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3]),
            5 => JsonUri.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4]),
            6 => JsonUri.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5]),
            7 => JsonUri.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6]),
            8 => JsonUri.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6], subjectUnderTest[7]),
            _ => throw new InvalidOperationException("Unsupported number of items"),
        };
    }

    private static IJsonValue ConcatenateResultsJsonUriReference(JsonArray subjectUnderTest, Span<byte> buffer)
    {
        return subjectUnderTest.GetArrayLength() switch
        {
            2 => JsonUriReference.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1]),
            3 => JsonUriReference.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2]),
            4 => JsonUriReference.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3]),
            5 => JsonUriReference.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4]),
            6 => JsonUriReference.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5]),
            7 => JsonUriReference.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6]),
            8 => JsonUriReference.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6], subjectUnderTest[7]),
            _ => throw new InvalidOperationException("Unsupported number of items"),
        };
    }

    private static IJsonValue ConcatenateResultsJsonUriTemplate(JsonArray subjectUnderTest, Span<byte> buffer)
    {
        return subjectUnderTest.GetArrayLength() switch
        {
            2 => JsonUriTemplate.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1]),
            3 => JsonUriTemplate.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2]),
            4 => JsonUriTemplate.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3]),
            5 => JsonUriTemplate.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4]),
            6 => JsonUriTemplate.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5]),
            7 => JsonUriTemplate.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6]),
            8 => JsonUriTemplate.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6], subjectUnderTest[7]),
            _ => throw new InvalidOperationException("Unsupported number of items"),
        };
    }

    private static IJsonValue ConcatenateResultsJsonUuid(JsonArray subjectUnderTest, Span<byte> buffer)
    {
        return subjectUnderTest.GetArrayLength() switch
        {
            2 => JsonUuid.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1]),
            3 => JsonUuid.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2]),
            4 => JsonUuid.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3]),
            5 => JsonUuid.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4]),
            6 => JsonUuid.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5]),
            7 => JsonUuid.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6]),
            8 => JsonUuid.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6], subjectUnderTest[7]),
            _ => throw new InvalidOperationException("Unsupported number of items"),
        };
    }

    private static IJsonValue ConcatenateResultsJsonBase64StringPre201909(JsonArray subjectUnderTest, Span<byte> buffer)
    {
        return subjectUnderTest.GetArrayLength() switch
        {
            2 => JsonBase64StringPre201909.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1]),
            3 => JsonBase64StringPre201909.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2]),
            4 => JsonBase64StringPre201909.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3]),
            5 => JsonBase64StringPre201909.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4]),
            6 => JsonBase64StringPre201909.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5]),
            7 => JsonBase64StringPre201909.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6]),
            8 => JsonBase64StringPre201909.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6], subjectUnderTest[7]),
            _ => throw new InvalidOperationException("Unsupported number of items"),
        };
    }

    private static IJsonValue ConcatenateResultsJsonBase64String(JsonArray subjectUnderTest, Span<byte> buffer)
    {
        return subjectUnderTest.GetArrayLength() switch
        {
            2 => JsonBase64String.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1]),
            3 => JsonBase64String.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2]),
            4 => JsonBase64String.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3]),
            5 => JsonBase64String.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4]),
            6 => JsonBase64String.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5]),
            7 => JsonBase64String.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6]),
            8 => JsonBase64String.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6], subjectUnderTest[7]),
            _ => throw new InvalidOperationException("Unsupported number of items"),
        };
    }

    private static IJsonValue ConcatenateResultsJsonBase64Content(JsonArray subjectUnderTest, Span<byte> buffer)
    {
        return subjectUnderTest.GetArrayLength() switch
        {
            2 => JsonBase64Content.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1]),
            3 => JsonBase64Content.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2]),
            4 => JsonBase64Content.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3]),
            5 => JsonBase64Content.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4]),
            6 => JsonBase64Content.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5]),
            7 => JsonBase64Content.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6]),
            8 => JsonBase64Content.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6], subjectUnderTest[7]),
            _ => throw new InvalidOperationException("Unsupported number of items"),
        };
    }

    private static IJsonValue ConcatenateResultsJsonBase64ContentPre201909(JsonArray subjectUnderTest, Span<byte> buffer)
    {
        return subjectUnderTest.GetArrayLength() switch
        {
            2 => JsonBase64ContentPre201909.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1]),
            3 => JsonBase64ContentPre201909.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2]),
            4 => JsonBase64ContentPre201909.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3]),
            5 => JsonBase64ContentPre201909.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4]),
            6 => JsonBase64ContentPre201909.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5]),
            7 => JsonBase64ContentPre201909.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6]),
            8 => JsonBase64ContentPre201909.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6], subjectUnderTest[7]),
            _ => throw new InvalidOperationException("Unsupported number of items"),
        };
    }

    private static IJsonValue ConcatenateResultsJsonContentPre201909(JsonArray subjectUnderTest, Span<byte> buffer)
    {
        return subjectUnderTest.GetArrayLength() switch
        {
            2 => JsonContentPre201909.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1]),
            3 => JsonContentPre201909.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2]),
            4 => JsonContentPre201909.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3]),
            5 => JsonContentPre201909.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4]),
            6 => JsonContentPre201909.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5]),
            7 => JsonContentPre201909.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6]),
            8 => JsonContentPre201909.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6], subjectUnderTest[7]),
            _ => throw new InvalidOperationException("Unsupported number of items"),
        };
    }

    private static IJsonValue ConcatenateResultsJsonContent(JsonArray subjectUnderTest, Span<byte> buffer)
    {
        return subjectUnderTest.GetArrayLength() switch
        {
            2 => JsonContent.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1]),
            3 => JsonContent.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2]),
            4 => JsonContent.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3]),
            5 => JsonContent.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4]),
            6 => JsonContent.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5]),
            7 => JsonContent.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6]),
            8 => JsonContent.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6], subjectUnderTest[7]),
            _ => throw new InvalidOperationException("Unsupported number of items"),
        };
    }
}