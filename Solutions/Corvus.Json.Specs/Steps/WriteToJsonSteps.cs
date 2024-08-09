// <copyright file="WriteToJsonSteps.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json;
using NUnit.Framework;
using TechTalk.SpecFlow;

namespace Steps;

[Binding]
public class WriteToJsonSteps
{
    private readonly ScenarioContext scenarioContext;

    public WriteToJsonSteps(ScenarioContext scenarioContext)
    {
        this.scenarioContext = scenarioContext;
    }

    [When("I write the value to a UTF8 JSON writer and store the resulting JSON")]
    public void WhenIWriteTheValueToAUTFJSONWriterAndStoreTheResultingJSON()
    {
        IJsonValue value = this.scenarioContext.Get<IJsonValue>(JsonValueSteps.SubjectUnderTest);

        ArrayBufferWriter<byte> abw = new();
        using Utf8JsonWriter writer = new(abw);
        value.WriteTo(writer);
        writer.Flush();
        string result =
#if NET8_0_OR_GREATER
            System.Text.Encoding.UTF8.GetString(abw.WrittenSpan);
#else
            System.Text.Encoding.UTF8.GetString(abw.WrittenArray, 0, abw.WrittenCount);
#endif

        this.scenarioContext.Set(result, JsonValueSteps.SerializationResult);
    }

    [Then("the JSON should equal a valid instance of the (.*)")]
    public void ThenTheJSONShouldBe(string type)
    {
        IJsonValue sut = this.scenarioContext.Get<IJsonValue>(JsonValueSteps.SubjectUnderTest);
        string result = this.scenarioContext.Get<string>(JsonValueSteps.SerializationResult);
        switch (type)
        {
            case "JsonString":
                var jsonString = JsonString.Parse(result);
                Assert.IsTrue(jsonString.IsValid());
                Assert.AreEqual(jsonString, sut);
                break;
            case "JsonNumber":
                var jsonNumber = JsonNumber.Parse(result);
                Assert.IsTrue(jsonNumber.IsValid());
                Assert.AreEqual(jsonNumber, sut);
                break;
            case "JsonInteger":
                var jsonInteger = JsonInteger.Parse(result);
                Assert.IsTrue(jsonInteger.IsValid());
                Assert.AreEqual(jsonInteger, sut);
                break;
            case "JsonBoolean":
                var jsonBoolean = JsonBoolean.Parse(result);
                Assert.IsTrue(jsonBoolean.IsValid());
                Assert.AreEqual(jsonBoolean, sut);
                break;
            case "JsonArray":
                var jsonArray = JsonArray.Parse(result);
                Assert.IsTrue(jsonArray.IsValid());
                Assert.AreEqual(jsonArray, sut);
                break;
            case "JsonObject":
                var jsonObject = JsonObject.Parse(result);
                Assert.IsTrue(jsonObject.IsValid());
                Assert.AreEqual(jsonObject, sut);
                break;
            case "JsonDate":
                var jsonDate = JsonDate.Parse(result);
                Assert.IsTrue(jsonDate.IsValid());
                Assert.AreEqual(jsonDate, sut);
                break;
            case "JsonDateTime":
                var jsonDateTime = JsonDateTime.Parse(result);
                Assert.IsTrue(jsonDateTime.IsValid());
                Assert.AreEqual(jsonDateTime, sut);
                break;
            case "JsonDuration":
                var jsonDuration = JsonDuration.Parse(result);
                Assert.IsTrue(jsonDuration.IsValid());
                Assert.AreEqual(jsonDuration, sut);
                break;
            case "JsonEmail":
                var jsonEmail = JsonEmail.Parse(result);
                Assert.IsTrue(jsonEmail.IsValid());
                Assert.AreEqual(jsonEmail, sut);
                break;
            case "JsonHostname":
                var jsonHostname = JsonHostname.Parse(result);
                Assert.IsTrue(jsonHostname.IsValid());
                Assert.AreEqual(jsonHostname, sut);
                break;
            case "JsonIdnEmail":
                var jsonIdnEmail = JsonIdnEmail.Parse(result);
                Assert.IsTrue(jsonIdnEmail.IsValid());
                Assert.AreEqual(jsonIdnEmail, sut);
                break;
            case "JsonIdnHostname":
                var jsonIdnHostname = JsonIdnHostname.Parse(result);
                Assert.IsTrue(jsonIdnHostname.IsValid());
                Assert.AreEqual(jsonIdnHostname, sut);
                break;
            case "JsonIpV4":
                var jsonIpV4 = JsonIpV4.Parse(result);
                Assert.IsTrue(jsonIpV4.IsValid());
                Assert.AreEqual(jsonIpV4, sut);
                break;
            case "JsonIpV6":
                var jsonIpV6 = JsonIpV6.Parse(result);
                Assert.IsTrue(jsonIpV6.IsValid());
                Assert.AreEqual(jsonIpV6, sut);
                break;
            case "JsonIri":
                var jsonIri = JsonIri.Parse(result);
                Assert.IsTrue(jsonIri.IsValid());
                Assert.AreEqual(jsonIri, sut);
                break;
            case "JsonIriReference":
                var jsonIriReference = JsonIriReference.Parse(result);
                Assert.IsTrue(jsonIriReference.IsValid());
                Assert.AreEqual(jsonIriReference, sut);
                break;
            case "JsonPointer":
                var jsonPointer = JsonPointer.Parse(result);
                Assert.IsTrue(jsonPointer.IsValid());
                Assert.AreEqual(jsonPointer, sut);
                break;
            case "JsonRegex":
                var jsonRegex = JsonRegex.Parse(result);
                Assert.IsTrue(jsonRegex.IsValid());
                Assert.AreEqual(jsonRegex, sut);
                break;
            case "JsonRelativePointer":
                var jsonRelativePointer = JsonRelativePointer.Parse(result);
                Assert.IsTrue(jsonRelativePointer.IsValid());
                Assert.AreEqual(jsonRelativePointer, sut);
                break;
            case "JsonTime":
                var jsonTime = JsonTime.Parse(result);
                Assert.IsTrue(jsonTime.IsValid());
                Assert.AreEqual(jsonTime, sut);
                break;
            case "JsonUri":
                var jsonUri = JsonUri.Parse(result);
                Assert.IsTrue(jsonUri.IsValid());
                Assert.AreEqual(jsonUri, sut);
                break;
            case "JsonUriReference":
                var jsonUriReference = JsonUriReference.Parse(result);
                Assert.IsTrue(jsonUriReference.IsValid());
                Assert.AreEqual(jsonUriReference, sut);
                break;
            case "JsonUuid":
                var jsonUuid = JsonUuid.Parse(result);
                Assert.IsTrue(jsonUuid.IsValid());
                Assert.AreEqual(jsonUuid, sut);
                break;
            case "JsonContent":
                var jsonContent = JsonContent.Parse(result);
                Assert.IsTrue(jsonContent.IsValid());
                Assert.AreEqual(jsonContent, sut);
                break;
            case "JsonContentPre201909":
                var jsonContentPre201909 = JsonContentPre201909.Parse(result);
                Assert.IsTrue(jsonContentPre201909.IsValid());
                Assert.AreEqual(jsonContentPre201909, sut);
                break;
            case "JsonBase64Content":
                var jsonBase64Content = JsonBase64Content.Parse(result);
                Assert.IsTrue(jsonBase64Content.IsValid());
                Assert.AreEqual(jsonBase64Content, sut);
                break;
            case "JsonBase64ContentPre201909":
                var jsonBase64ContentPre201909 = JsonBase64ContentPre201909.Parse(result);
                Assert.IsTrue(jsonBase64ContentPre201909.IsValid());
                Assert.AreEqual(jsonBase64ContentPre201909, sut);
                break;
            case "JsonBase64String":
                var jsonBase64String = JsonBase64String.Parse(result);
                Assert.IsTrue(jsonBase64String.IsValid());
                Assert.AreEqual(jsonBase64String, sut);
                break;
            case "JsonBase64StringPre201909":
                var jsonBase64StringPre201909 = JsonBase64StringPre201909.Parse(result);
                Assert.IsTrue(jsonBase64StringPre201909.IsValid());
                Assert.AreEqual(jsonBase64StringPre201909, sut);
                break;
            case "JsonInt64":
                var jsonInt64 = JsonInt64.Parse(result);
                Assert.IsTrue(jsonInt64.IsValid());
                Assert.AreEqual(jsonInt64, sut);
                break;
            case "JsonInt32":
                var jsonInt32 = JsonInt32.Parse(result);
                Assert.IsTrue(jsonInt32.IsValid());
                Assert.AreEqual(jsonInt32, sut);
                break;
            case "JsonInt16":
                var jsonInt16 = JsonInt16.Parse(result);
                Assert.IsTrue(jsonInt16.IsValid());
                Assert.AreEqual(jsonInt16, sut);
                break;
            case "JsonSByte":
                var jsonSByte = JsonSByte.Parse(result);
                Assert.IsTrue(jsonSByte.IsValid());
                Assert.AreEqual(jsonSByte, sut);
                break;
            case "JsonUInt64":
                var jsonUInt64 = JsonUInt64.Parse(result);
                Assert.IsTrue(jsonUInt64.IsValid());
                Assert.AreEqual(jsonUInt64, sut);
                break;
            case "JsonUInt32":
                var jsonUInt32 = JsonUInt32.Parse(result);
                Assert.IsTrue(jsonUInt32.IsValid());
                Assert.AreEqual(jsonUInt32, sut);
                break;
            case "JsonUInt16":
                var jsonUInt16 = JsonUInt16.Parse(result);
                Assert.IsTrue(jsonUInt16.IsValid());
                Assert.AreEqual(jsonUInt16, sut);
                break;
            case "JsonByte":
                var jsonByte = JsonByte.Parse(result);
                Assert.IsTrue(jsonByte.IsValid());
                Assert.AreEqual(jsonByte, sut);
                break;
            case "JsonSingle":
                var jsonSingle = JsonSingle.Parse(result);
                Assert.IsTrue(jsonSingle.IsValid());
                Assert.AreEqual(jsonSingle, sut);
                break;
            case "JsonDouble":
                var jsonDouble = JsonDouble.Parse(result);
                Assert.IsTrue(jsonDouble.IsValid());
                Assert.AreEqual(jsonDouble, sut);
                break;
            case "JsonDecimal":
                var jsonDecimal = JsonDecimal.Parse(result);
                Assert.IsTrue(jsonDecimal.IsValid());
                Assert.AreEqual(jsonDecimal, sut);
                break;
            default:
                throw new InvalidOperationException($"Unexpected type: {type}");
        }
    }
}