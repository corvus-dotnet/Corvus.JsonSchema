// <copyright file="MessageDeliveryContextGenerationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi.CodeGeneration;

namespace Corvus.Text.Json.AsyncApi.CodeGeneration.Tests;

/// <summary>
/// Tests the opt-in delivery-context generation mode.
/// </summary>
[TestClass]
public class MessageDeliveryContextGenerationTests
{
    [TestMethod]
    public void DefaultGenerationRetainsLegacyHandlerSignature()
    {
        JsonElement root = LoadStreetlights();
        AsyncApi30CodeGenerator generator = new("Streetlights", new Dictionary<string, string>());

        GeneratedFile handler = generator.Generate(root).First(file => file.FileName == "IReceiveLightMeasurementHandler.cs");

        Assert.IsFalse(handler.Content.Contains("MessageDeliveryContext", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MessageContextGenerationEmitsContextOnlyThroughOptInMode()
    {
        JsonElement root = LoadStreetlights();
        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/schemas/turnOnOffPayload"] = "Streetlights.TurnOnOffPayload",
            ["#/components/schemas/lightMeasuredPayload"] = "Streetlights.LightMeasuredPayload",
        };
        AsyncApi30CodeGenerator generator = new("Streetlights", schemaTypeMap, generateMessageDeliveryContext: true);

        IReadOnlyList<GeneratedFile> files = generator.Generate(root);
        GeneratedFile handler = files.First(file => file.FileName == "IReceiveLightMeasurementHandler.cs");
        GeneratedFile consumer = files.First(file => file.FileName == "ReceiveLightMeasurementConsumer.cs");

        StringAssert.Contains(handler.Content, "MessageDeliveryContext context");
        StringAssert.Contains(consumer.Content, "this.transport.SubscribeAsync");
        StringAssert.Contains(consumer.Content, "context, cancellationToken");
        DynamicCompiler.AssertCompiles(files, "Streetlights.Context.Generated", DynamicCompiler.GenerateTypeStubs(schemaTypeMap));
    }

    private static JsonElement LoadStreetlights()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "streetlights.json"));
        using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(bytes);
        return document.RootElement.Clone();
    }
}