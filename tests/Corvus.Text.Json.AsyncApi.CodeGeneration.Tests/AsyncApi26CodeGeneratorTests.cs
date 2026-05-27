// <copyright file="AsyncApi26CodeGeneratorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi.CodeGeneration;

namespace Corvus.Text.Json.AsyncApi.CodeGeneration.Tests;

[TestClass]
public class AsyncApi26CodeGeneratorTests
{
    private static JsonElement streetlightsRoot;
    private static JsonElement requestReplyRoot;

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        byte[] streetlightsBytes = File.ReadAllBytes(Path.Combine("TestData", "asyncapi26-streetlights.json"));
        using ParsedJsonDocument<JsonElement> streetlightsDoc = ParsedJsonDocument<JsonElement>.Parse(streetlightsBytes);
        streetlightsRoot = streetlightsDoc.RootElement.Clone();

        byte[] requestReplyBytes = File.ReadAllBytes(Path.Combine("TestData", "asyncapi26-request-reply.json"));
        using ParsedJsonDocument<JsonElement> requestReplyDoc = ParsedJsonDocument<JsonElement>.Parse(requestReplyBytes);
        requestReplyRoot = requestReplyDoc.RootElement.Clone();
    }

    [TestMethod]
    public void ListOperations_MapsPublishToReceiveAndSubscribeToSend()
    {
        AsyncApiOperationSummary[] ops = AsyncApi26CodeGenerator.ListOperations(streetlightsRoot);

        Assert.AreEqual(2, ops.Length);

        AsyncApiOperationSummary receiveOp = ops.First(o => o.Action == OperationAction.Receive);
        Assert.AreEqual("receiveLightMeasurement", receiveOp.OperationId);
        Assert.AreEqual("smartylighting/streetlights/1/0/action/{streetlightId}/lighting/measured", receiveOp.ChannelAddress);
        Assert.AreEqual("Inform about environmental lighting conditions of a particular streetlight.", receiveOp.Summary);
        Assert.AreEqual(1, receiveOp.MessageCount);

        AsyncApiOperationSummary sendOp = ops.First(o => o.Action == OperationAction.Send);
        Assert.AreEqual("turnOn", sendOp.OperationId);
        Assert.AreEqual("smartylighting/streetlights/1/0/action/{streetlightId}/turn/on", sendOp.ChannelAddress);
        Assert.AreEqual(1, sendOp.MessageCount);
    }

    [TestMethod]
    public void ListOperations_WithTagFilter_FiltersChannelOperations()
    {
        var filter = new OperationFilter(tags: ["lighting"]);

        AsyncApiOperationSummary[] ops = AsyncApi26CodeGenerator.ListOperations(streetlightsRoot, filter);

        Assert.AreEqual(2, ops.Length);
    }

    [TestMethod]
    public void CollectSchemaPointers_FindsAsyncApi26ComponentSchemasAndMessages()
    {
        string[] pointers = AsyncApi26CodeGenerator.CollectSchemaPointers(streetlightsRoot);

        CollectionAssert.Contains(pointers, "#/components/schemas/lightMeasuredPayload");
        CollectionAssert.Contains(pointers, "#/components/schemas/turnOnOffPayload");
        CollectionAssert.Contains(pointers, "#/components/messages/lightMeasured/payload");
        CollectionAssert.Contains(pointers, "#/components/messages/turnOnOff/payload");
    }

    [TestMethod]
    public void Generate_ProducesProducerAndConsumerForPublishSubscribe()
    {
        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/schemas/turnOnOffPayload"] = "Streetlights.TurnOnOffPayload",
            ["#/components/schemas/lightMeasuredPayload"] = "Streetlights.LightMeasuredPayload",
        };

        var generator = new AsyncApi26CodeGenerator("Streetlights", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(streetlightsRoot);

        GeneratedFile producer = files.Single(f => f.FileName == "TurnOnProducer.cs");
        GeneratedFile handler = files.Single(f => f.FileName == "IReceiveLightMeasurementHandler.cs");

        StringAssert.Contains(producer.Content, "PublishTurnOnOffAsync");
        StringAssert.Contains(handler.Content, "HandleLightMeasuredAsync");
    }

    [TestMethod]
    public void Generate_RequestReplyExtension_ProducerContainsSendAndReceiveMethod()
    {
        var schemaTypeMap = CreateRequestReplySchemaTypeMap();

        var generator = new AsyncApi26CodeGenerator("Calculator", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(requestReplyRoot);

        GeneratedFile producer = files.Single(f => f.FileName == "CalculateProducer.cs");

        StringAssert.Contains(producer.Content, "SendAndReceiveCalculateRequestAsync");
        StringAssert.Contains(producer.Content, "RequestAsync");
    }

    [TestMethod]
    public void Compile_Streetlights_GeneratedCodeCompiles()
    {
        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/schemas/turnOnOffPayload"] = "Streetlights.TurnOnOffPayload",
            ["#/components/schemas/lightMeasuredPayload"] = "Streetlights.LightMeasuredPayload",
        };

        var generator = new AsyncApi26CodeGenerator("Streetlights", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(streetlightsRoot);

        string stubs = DynamicCompiler.GenerateTypeStubs(schemaTypeMap);
        DynamicCompiler.AssertCompiles(files, "Streetlights.AsyncApi26.Generated", stubs);
    }

    [TestMethod]
    public void Compile_RequestReplyExtension_GeneratedCodeCompiles()
    {
        var schemaTypeMap = CreateRequestReplySchemaTypeMap();

        var generator = new AsyncApi26CodeGenerator("Calculator", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(requestReplyRoot);

        string stubs = DynamicCompiler.GenerateTypeStubs(schemaTypeMap);
        DynamicCompiler.AssertCompiles(files, "Calculator.AsyncApi26.Generated", stubs);
    }

    private static Dictionary<string, string> CreateRequestReplySchemaTypeMap()
    {
        return new()
        {
            ["#/components/schemas/CalculateRequest"] = "Calculator.CalculateRequest",
            ["#/components/schemas/CalculateRequestHeaders"] = "Calculator.CalculateRequestHeaders",
            ["#/components/schemas/CalculateResponse"] = "Calculator.CalculateResponse",
            ["#/components/schemas/CalculateResponseHeaders"] = "Calculator.CalculateResponseHeaders",
        };
    }
}