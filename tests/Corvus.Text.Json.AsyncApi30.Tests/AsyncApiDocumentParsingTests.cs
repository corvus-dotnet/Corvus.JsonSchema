// <copyright file="AsyncApiDocumentParsingTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi30;

namespace Corvus.Text.Json.AsyncApi30.Tests;

/// <summary>
/// Tests that verify well-known AsyncAPI 3.0 documents can be parsed
/// and navigated using the generated type library.
/// </summary>
[TestClass]
public class AsyncApiDocumentParsingTests
{
    private static readonly string TestDataDir = Path.Combine(
        AppContext.BaseDirectory, "TestData");

    [TestMethod]
    public void ParseStreetlights_VersionIsCorrect()
    {
        using ParsedJsonDocument<AsyncApiDocument> doc = ParseFile("streetlights.json");
        AsyncApiDocument root = doc.RootElement;

        Assert.AreEqual(JsonValueKind.String, root.Asyncapi.ValueKind);
        Assert.AreEqual("3.0.0", (string)root.Asyncapi);
    }

    [TestMethod]
    public void ParseStreetlights_InfoTitleIsCorrect()
    {
        using ParsedJsonDocument<AsyncApiDocument> doc = ParseFile("streetlights.json");
        AsyncApiDocument root = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Object, root.InfoValue.ValueKind);
        Assert.AreEqual("Streetlights Kafka API", (string)root.InfoValue.Title);
    }

    [TestMethod]
    public void ParseStreetlights_HasChannels()
    {
        using ParsedJsonDocument<AsyncApiDocument> doc = ParseFile("streetlights.json");
        AsyncApiDocument root = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Object, root.ChannelsValue.ValueKind);
        int channelCount = root.ChannelsValue.GetPropertyCount();
        Assert.AreEqual(2, channelCount);
    }

    [TestMethod]
    public void ParseStreetlights_HasOperations()
    {
        using ParsedJsonDocument<AsyncApiDocument> doc = ParseFile("streetlights.json");
        AsyncApiDocument root = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Object, root.OperationsValue.ValueKind);
        int operationCount = root.OperationsValue.GetPropertyCount();
        Assert.AreEqual(2, operationCount);
    }

    [TestMethod]
    public void ParseStreetlights_HasServers()
    {
        using ParsedJsonDocument<AsyncApiDocument> doc = ParseFile("streetlights.json");
        AsyncApiDocument root = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Object, root.ServersValue.ValueKind);
        int serverCount = root.ServersValue.GetPropertyCount();
        Assert.AreEqual(1, serverCount);
    }

    [TestMethod]
    public void ParseStreetlights_DefaultContentType()
    {
        using ParsedJsonDocument<AsyncApiDocument> doc = ParseFile("streetlights.json");
        AsyncApiDocument root = doc.RootElement;

        Assert.AreEqual("application/json", (string)root.DefaultContentType);
    }

    [TestMethod]
    public void ParseStreetlights_HasComponents()
    {
        using ParsedJsonDocument<AsyncApiDocument> doc = ParseFile("streetlights.json");
        AsyncApiDocument root = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Object, root.ComponentsValue.ValueKind);
    }

    [TestMethod]
    public void ParseUserSignup_VersionIsCorrect()
    {
        using ParsedJsonDocument<AsyncApiDocument> doc = ParseFile("user-signup.json");
        AsyncApiDocument root = doc.RootElement;

        Assert.AreEqual("3.0.0", (string)root.Asyncapi);
    }

    [TestMethod]
    public void ParseUserSignup_HasOneChannel()
    {
        using ParsedJsonDocument<AsyncApiDocument> doc = ParseFile("user-signup.json");
        AsyncApiDocument root = doc.RootElement;

        Assert.AreEqual(1, root.ChannelsValue.GetPropertyCount());
    }

    [TestMethod]
    public void ParseUserSignup_HasOneOperation()
    {
        using ParsedJsonDocument<AsyncApiDocument> doc = ParseFile("user-signup.json");
        AsyncApiDocument root = doc.RootElement;

        Assert.AreEqual(1, root.OperationsValue.GetPropertyCount());
    }

    [TestMethod]
    public void ParseUserSignup_InfoTitle()
    {
        using ParsedJsonDocument<AsyncApiDocument> doc = ParseFile("user-signup.json");
        AsyncApiDocument root = doc.RootElement;

        Assert.AreEqual("User Signup Service", (string)root.InfoValue.Title);
    }

    [TestMethod]
    public void RoundTrip_PreservesContent()
    {
        using ParsedJsonDocument<AsyncApiDocument> doc = ParseFile("user-signup.json");
        AsyncApiDocument root = doc.RootElement;

        string serialized = root.ToString();
        using ParsedJsonDocument<AsyncApiDocument> doc2 = ParsedJsonDocument<AsyncApiDocument>.Parse(
            System.Text.Encoding.UTF8.GetBytes(serialized));

        Assert.AreEqual("3.0.0", (string)doc2.RootElement.Asyncapi);
        Assert.AreEqual(1, doc2.RootElement.OperationsValue.GetPropertyCount());
    }

    [TestMethod]
    public void ParseStreetlights_ChannelAddress()
    {
        using ParsedJsonDocument<AsyncApiDocument> doc = ParseFile("streetlights.json");
        AsyncApiDocument root = doc.RootElement;

        Assert.IsTrue(root.ChannelsValue.TryGetProperty("lightingMeasured"u8, out var channelEntity));
        AsyncApiDocument.Channel channel = (AsyncApiDocument.Channel)channelEntity;
        Assert.AreEqual(
            "smartylighting.streetlights.1.0.action.{streetlightId}.lighting.measured",
            (string)channel.Address);
    }

    [TestMethod]
    public void ParseStreetlights_ChannelDescription()
    {
        using ParsedJsonDocument<AsyncApiDocument> doc = ParseFile("streetlights.json");
        AsyncApiDocument root = doc.RootElement;

        Assert.IsTrue(root.ChannelsValue.TryGetProperty("lightingMeasured"u8, out var channelEntity));
        AsyncApiDocument.Channel channel = (AsyncApiDocument.Channel)channelEntity;
        Assert.AreEqual(
            "The topic on which measured values may be produced and consumed.",
            (string)channel.Description);
    }

    [TestMethod]
    public void ParseStreetlights_InfoVersion()
    {
        using ParsedJsonDocument<AsyncApiDocument> doc = ParseFile("streetlights.json");
        AsyncApiDocument root = doc.RootElement;

        Assert.AreEqual("1.0.0", (string)root.InfoValue.Version);
    }

    private static ParsedJsonDocument<AsyncApiDocument> ParseFile(string filename)
    {
        string path = Path.Combine(TestDataDir, filename);
        byte[] bytes = File.ReadAllBytes(path);
        return ParsedJsonDocument<AsyncApiDocument>.Parse(bytes);
    }
}