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
    public void Generate_ServerSecurity_ProducerAuthenticates()
    {
        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/schemas/turnOnOffPayload"] = "Streetlights.TurnOnOffPayload",
            ["#/components/schemas/lightMeasuredPayload"] = "Streetlights.LightMeasuredPayload",
        };

        var generator = new AsyncApi26CodeGenerator("Streetlights", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(streetlightsRoot);

        GeneratedFile producer = files.Single(f => f.FileName == "TurnOnProducer.cs");

        // The 2.6 document declares security on its server; it must reach the delegated emission
        // as the auth context constant, the authentication call, and the exact SASL variant.
        StringAssert.Contains(producer.Content, "SaslScramAuthContext");
        StringAssert.Contains(producer.Content, "AuthenticateAsync");
        StringAssert.Contains(producer.Content, "SecuritySchemeType.ScramSha256");
    }

    [TestMethod]
    public void Generate_ServerSecurity_UnknownScheme_ReportsDiagnostic()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "asyncapi26-missing-scheme.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement.Clone();

        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/schemas/eventPayload"] = "Events.EventPayload",
        };

        var generator = new AsyncApi26CodeGenerator("Events", schemaTypeMap);
        _ = generator.Generate(root);

        // The server requirement names a scheme that components.securitySchemes does not define;
        // the degradation must be recorded rather than silently emitting a wrong scheme type -
        // and recorded once, not once per operation that collects the server's security.
        Assert.AreEqual(
            1,
            generator.Diagnostics.Count(d => d.Message.Contains("missingScheme")),
            "A security requirement naming an undefined scheme should be reported exactly once");
    }

    [TestMethod]
    public void Generate_ReplyAddressLiteralExpression_SurfacesEmissionDiagnostic()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "asyncapi26-reply-literal-expression.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement.Clone();

        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/schemas/CalculateRequest"] = "Calculator.CalculateRequest",
            ["#/components/schemas/CalculateResponse"] = "Calculator.CalculateResponse",
        };

        var generator = new AsyncApi26CodeGenerator("Calculator", schemaTypeMap);
        _ = generator.Generate(root);

        // The x-corvus-reply address expression "$message.header.replyTo" (no '#') is demoted
        // during emission; that demotion is recorded by the inner 3.0 emitter and must surface
        // through the 2.6 generator's own Diagnostics, or --strict passes on a document the
        // byte-identical 3.0 form fails.
        Assert.IsTrue(
            generator.Diagnostics.Any(d => d.Message.Contains("$message.header.replyTo")),
            "An emission-phase demotion should surface through the 2.6 generator's Diagnostics");
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

    [TestMethod]
    public void DescribeChannelOperations_ResolvesProducerAndPayloadType()
    {
        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/schemas/turnOnOffPayload"] = "Streetlights.TurnOnOffPayload",
            ["#/components/schemas/lightMeasuredPayload"] = "Streetlights.LightMeasuredPayload",
        };

        var generator = new AsyncApi26CodeGenerator("Streetlights", schemaTypeMap);
        IReadOnlyList<AsyncApiChannelDescriptor> channels = generator.DescribeChannelOperations(streetlightsRoot);

        Assert.AreEqual(2, channels.Count);

        // 'subscribe' maps to a send operation, which gets a producer.
        AsyncApiChannelDescriptor send = channels.Single(c => c.Action == OperationAction.Send);
        Assert.AreEqual("Streetlights.TurnOnProducer", send.ProducerClassName);

        AsyncApiChannelMessageDescriptor message = send.Messages.Single();
        Assert.AreEqual("Streetlights.TurnOnOffPayload", message.PayloadTypeName);
        Assert.AreEqual("PublishTurnOnOffAsync", message.ProducerMethodName);

        // 'publish' maps to a receive operation, which has no producer.
        AsyncApiChannelDescriptor receive = channels.Single(c => c.Action == OperationAction.Receive);
        Assert.IsNull(receive.ProducerClassName);
        Assert.AreEqual("Streetlights.LightMeasuredPayload", receive.Messages.Single().PayloadTypeName);
    }

    [TestMethod]
    public void Generate_ParameterizedConsumer_WithReferencedParameter_CarriesParameterMetadata()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "parameterized-consumer-ref-2.6.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);

        var generator = new AsyncApi26CodeGenerator("ParameterizedRef26", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = generator.Generate(doc.RootElement);

        GeneratedFile? consumer = files.FirstOrDefault(f => f.FileName.Contains("OnOrderCreatedConsumer"));
        Assert.IsNotNull(consumer, "The publish operation should generate a consumer class");

        StringAssert.Contains(
            consumer.Content,
            "The order identifier.",
            "The referenced parameter's description should reach the generated doc comment");
        StringAssert.Contains(
            consumer.Content,
            "orderId = \"standard\"",
            "The referenced parameter's schema default should become the argument default");
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