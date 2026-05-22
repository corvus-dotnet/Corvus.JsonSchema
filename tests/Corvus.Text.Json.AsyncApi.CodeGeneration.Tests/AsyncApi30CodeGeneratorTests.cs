// <copyright file="AsyncApi30CodeGeneratorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi.CodeGeneration;
using Corvus.Text.Json.OpenApi.CodeGeneration;

namespace Corvus.Text.Json.AsyncApi.CodeGeneration.Tests;

[TestClass]
public class AsyncApi30CodeGeneratorTests
{
    private static JsonElement streetlightsRoot;
    private static JsonElement traitsRoot;

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "streetlights.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        streetlightsRoot = doc.RootElement.Clone();

        byte[] traitsBytes = File.ReadAllBytes(Path.Combine("TestData", "traits-example.json"));
        using ParsedJsonDocument<JsonElement> traitsDoc = ParsedJsonDocument<JsonElement>.Parse(traitsBytes);
        traitsRoot = traitsDoc.RootElement.Clone();
    }

    [TestMethod]
    public void ListOperations_ReturnsAllOperations()
    {
        AsyncApiOperationSummary[] ops = AsyncApi30CodeGenerator.ListOperations(streetlightsRoot);

        Assert.AreEqual(2, ops.Length);
    }

    [TestMethod]
    public void ListOperations_IdentifiesSendAction()
    {
        AsyncApiOperationSummary[] ops = AsyncApi30CodeGenerator.ListOperations(streetlightsRoot);

        AsyncApiOperationSummary sendOp = ops.First(o => o.Action == OperationAction.Send);
        Assert.AreEqual("turnOn", sendOp.OperationId);
        Assert.AreEqual("smartylighting.streetlights.1.0.action.{streetlightId}.turn.on", sendOp.ChannelAddress);
        Assert.AreEqual(1, sendOp.MessageCount);
    }

    [TestMethod]
    public void ListOperations_IdentifiesReceiveAction()
    {
        AsyncApiOperationSummary[] ops = AsyncApi30CodeGenerator.ListOperations(streetlightsRoot);

        AsyncApiOperationSummary recvOp = ops.First(o => o.Action == OperationAction.Receive);
        Assert.AreEqual("receiveLightMeasurement", recvOp.OperationId);
        Assert.AreEqual("smartylighting.streetlights.1.0.action.{streetlightId}.lighting.measured", recvOp.ChannelAddress);
        Assert.AreEqual(1, recvOp.MessageCount);
    }

    [TestMethod]
    public void ListOperations_IncludesOperationSummary()
    {
        AsyncApiOperationSummary[] ops = AsyncApi30CodeGenerator.ListOperations(streetlightsRoot);

        AsyncApiOperationSummary recvOp = ops.First(o => o.Action == OperationAction.Receive);
        Assert.AreEqual("Inform about environmental lighting conditions of a particular streetlight.", recvOp.Summary);
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsComponentSchemas()
    {
        string[] pointers = AsyncApi30CodeGenerator.CollectSchemaPointers(streetlightsRoot);

        // In the streetlights spec, all message payloads are $refs to component schemas.
        // The collector picks up component schemas from #/components/schemas.
        Assert.IsTrue(pointers.Contains("#/components/schemas/lightMeasuredPayload"));
        Assert.IsTrue(pointers.Contains("#/components/schemas/turnOnOffPayload"));
    }

    [TestMethod]
    public void CollectSchemaPointers_DoesNotIncludeRefMessages()
    {
        string[] pointers = AsyncApi30CodeGenerator.CollectSchemaPointers(streetlightsRoot);

        // Channel messages that are $refs should not produce channel-level pointers;
        // the actual schemas are collected from their target locations.
        Assert.IsFalse(pointers.Contains("#/channels/lightingMeasured/messages/lightMeasured/payload"));
    }

    [TestMethod]
    public void ListOperations_WithFilter_FiltersbyChannelAddress()
    {
        var filter = new OperationFilter(["*turn*"], []);

        AsyncApiOperationSummary[] ops = AsyncApi30CodeGenerator.ListOperations(streetlightsRoot, filter);

        Assert.AreEqual(1, ops.Length);
        Assert.AreEqual(OperationAction.Send, ops[0].Action);
    }

    [TestMethod]
    public void Generate_ProducesFilesForStreetlights()
    {
        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/schemas/turnOnOffPayload"] = "Streetlights.TurnOnOffPayload",
            ["#/components/schemas/lightMeasuredPayload"] = "Streetlights.LightMeasuredPayload",
        };

        var generator = new AsyncApi30CodeGenerator("Streetlights", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(streetlightsRoot);

        // Should have at least: 1 producer + 1 consumer handler + message wrappers
        Assert.IsTrue(files.Count >= 2, $"Expected at least 2 files, got {files.Count}");

        // Verify producer file exists
        Assert.IsTrue(files.Any(f => f.FileName.Contains("Producer")), "Expected a producer file");

        // Verify consumer handler file exists
        Assert.IsTrue(files.Any(f => f.FileName.Contains("Handler")), "Expected a handler file");
    }

    [TestMethod]
    public void Generate_ProducerContainsPublishMethod()
    {
        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/schemas/turnOnOffPayload"] = "Streetlights.TurnOnOffPayload",
        };

        var generator = new AsyncApi30CodeGenerator("Streetlights", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(streetlightsRoot);

        GeneratedFile producerFile = files.First(f => f.FileName.Contains("Producer"));
        Assert.IsTrue(producerFile.Content.Contains("PublishAsync"), "Producer should contain a Publish method");
        Assert.IsTrue(producerFile.Content.Contains("TurnOnOffPayload"), "Producer should reference the payload type");
    }

    [TestMethod]
    public void Generate_ConsumerHandlerContainsHandleMethod()
    {
        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/schemas/lightMeasuredPayload"] = "Streetlights.LightMeasuredPayload",
        };

        var generator = new AsyncApi30CodeGenerator("Streetlights", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(streetlightsRoot);

        GeneratedFile handlerFile = files.First(f => f.FileName.Contains("Handler"));
        Assert.IsTrue(handlerFile.Content.Contains("HandleLightMeasuredAsync"), "Handler should contain HandleLightMeasuredAsync method");
        Assert.IsTrue(handlerFile.Content.Contains("LightMeasuredPayload"), "Handler should reference the payload type");
    }

    [TestMethod]
    public void Generate_TraitsProvideHeadersToMessage()
    {
        // The traits-example.json has a message trait that supplies headers
        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/schemas/UserSignedUpPayload"] = "Traits.UserSignedUpPayload",
            ["#/components/schemas/CommonHeaders"] = "Traits.CommonHeaders",
        };

        var generator = new AsyncApi30CodeGenerator("Traits", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(traitsRoot);

        // The producer should include typed headers from the trait
        GeneratedFile producerFile = files.First(f => f.FileName.Contains("Producer"));
        Assert.IsTrue(
            producerFile.Content.Contains("CommonHeaders"),
            "Producer should reference headers type provided by message trait");
    }

    [TestMethod]
    public void Generate_TraitsProvidedHeaders_ConsumerHandlerIncludesHeaders()
    {
        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/schemas/UserSignedUpPayload"] = "Traits.UserSignedUpPayload",
            ["#/components/schemas/CommonHeaders"] = "Traits.CommonHeaders",
        };

        var generator = new AsyncApi30CodeGenerator("Traits", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(traitsRoot);

        // The consumer handler interface should include headers parameter
        GeneratedFile handlerFile = files.First(f => f.FileName.Contains("Handler"));
        Assert.IsTrue(
            handlerFile.Content.Contains("CommonHeaders"),
            "Handler should include headers type from trait in method signature");
    }

    [TestMethod]
    public void Generate_UnsupportedSchemaFormat_ThrowsNotSupportedException()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "unsupported-schema-format.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement.Clone();

        var schemaTypeMap = new Dictionary<string, string>();
        var generator = new AsyncApi30CodeGenerator("Avro", schemaTypeMap);

        var ex = Assert.ThrowsExactly<NotSupportedException>(() => generator.Generate(root));
        StringAssert.Contains(ex.Message, "avroMessage");
        StringAssert.Contains(ex.Message, "application/vnd.apache.avro");
    }
}