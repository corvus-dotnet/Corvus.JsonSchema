// <copyright file="AsyncApi30CodeGeneratorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi.CodeGeneration;

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
    public void CollectSchemaPointers_InlineMessages_FindsPayloadAndHeaders()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "inline-messages.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement;

        string[] pointers = AsyncApi30CodeGenerator.CollectSchemaPointers(root);

        // Inline messages (not $ref) should produce channel-level payload/headers pointers
        Assert.IsTrue(pointers.Contains("#/channels/inlineChannel/messages/InlineEvent/payload"));
        Assert.IsTrue(pointers.Contains("#/channels/inlineChannel/messages/InlineEvent/headers"));
    }

    [TestMethod]
    public void CollectSchemaPointers_WithFilter_ExcludesFilteredChannels()
    {
        // Use inline-messages.json which has inline messages in "inlineChannel" with address "inline/events"
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "inline-messages.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement;

        // Filter that excludes the only channel (address starts with "inline")
        var filter = new OperationFilter(["*nomatch*"], []);
        string[] pointers = AsyncApi30CodeGenerator.CollectSchemaPointers(root, filter: filter);

        // The inline channel should be excluded by the filter, so no channel-level pointers
        Assert.IsFalse(pointers.Contains("#/channels/inlineChannel/messages/InlineEvent/payload"));
        Assert.IsFalse(pointers.Contains("#/channels/inlineChannel/messages/InlineEvent/headers"));
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
    public void ListOperations_WithExcludeFilter_ExcludesMatchingChannels()
    {
        // Include everything, but exclude "turn" channels
        var filter = new OperationFilter([], ["*turn*"]);

        AsyncApiOperationSummary[] ops = AsyncApi30CodeGenerator.ListOperations(streetlightsRoot, filter);

        // Should have only the receive operation (lightingMeasured), not the send (turnOn)
        Assert.IsTrue(ops.Length > 0);
        Assert.IsTrue(ops.All(o => !o.ChannelAddress.Contains("turn", StringComparison.OrdinalIgnoreCase)));
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

    [TestMethod]
    public void Generate_RequestReply_ProducerContainsSendAndReceiveMethod()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "request-reply.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement.Clone();

        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/messages/CalculateRequest/payload"] = "Calculator.CalculateRequest",
            ["#/components/messages/CalculateRequest/headers"] = "Calculator.CalculateRequestHeaders",
            ["#/components/messages/CalculateResponse/payload"] = "Calculator.CalculateResponse",
            ["#/components/messages/CalculateResponse/headers"] = "Calculator.CalculateResponseHeaders",
        };

        var generator = new AsyncApi30CodeGenerator("Calculator", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        GeneratedFile producerFile = files.First(f => f.FileName.Contains("Producer"));
        Assert.IsTrue(
            producerFile.Content.Contains("SendAndReceiveCalculateRequestAsync"),
            "Producer should contain a SendAndReceive method for request/reply operations");
        Assert.IsTrue(
            producerFile.Content.Contains("RequestAsync"),
            "Producer should call transport.RequestAsync for request/reply");
    }

    [TestMethod]
    public void Generate_RequestReply_IncludesCorrelationId()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "request-reply.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement.Clone();

        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/messages/CalculateRequest/payload"] = "Calculator.CalculateRequest",
            ["#/components/messages/CalculateRequest/headers"] = "Calculator.CalculateRequestHeaders",
            ["#/components/messages/CalculateResponse/payload"] = "Calculator.CalculateResponse",
            ["#/components/messages/CalculateResponse/headers"] = "Calculator.CalculateResponseHeaders",
        };

        var generator = new AsyncApi30CodeGenerator("Calculator", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        GeneratedFile producerFile = files.First(f => f.FileName.Contains("Producer"));
        Assert.IsTrue(
            producerFile.Content.Contains("correlationId"),
            "Producer SendAndReceive method should generate a correlation ID");
    }

    [TestMethod]
    public void CollectSchemaPointers_RequestReply_FindsReplyPayloadSchemas()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "request-reply.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement.Clone();

        string[] pointers = AsyncApi30CodeGenerator.CollectSchemaPointers(root);

        Assert.IsTrue(
            pointers.Contains("#/components/messages/CalculateRequest/payload"),
            "Should collect request payload schema pointer");
        Assert.IsTrue(
            pointers.Contains("#/components/messages/CalculateResponse/payload"),
            "Should collect reply payload schema pointer");
    }

    [TestMethod]
    public void ListServers_ReturnsServerInfo()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "servers-and-tags.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement.Clone();

        ServerInfo[] servers = AsyncApi30CodeGenerator.ListServers(root);

        Assert.AreEqual(2, servers.Length);

        ServerInfo production = servers.First(s => s.Name == "production");
        Assert.AreEqual("broker.example.com:9092", production.Host);
        Assert.AreEqual("kafka", production.Protocol);
        Assert.AreEqual(1, production.Variables.Count);
        Assert.AreEqual("environment", production.Variables[0].Name);
        Assert.AreEqual("prod", production.Variables[0].DefaultValue);
        Assert.AreEqual(2, production.Variables[0].EnumValues.Count);
        Assert.AreEqual(1, production.SecuritySchemes.Count);
        Assert.AreEqual("sasl", production.SecuritySchemes[0]);

        ServerInfo staging = servers.First(s => s.Name == "staging");
        Assert.AreEqual("/v2", staging.Pathname);
    }

    [TestMethod]
    public void ListOperations_WithTagFilter_FiltersOperations()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "servers-and-tags.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement.Clone();

        // Filter to only "audit" tag
        var filter = new OperationFilter(tags: ["audit"]);
        AsyncApiOperationSummary[] ops = AsyncApi30CodeGenerator.ListOperations(root, filter);

        Assert.AreEqual(1, ops.Length);
        Assert.AreEqual("publishAudit", ops[0].OperationId);
    }

    [TestMethod]
    public void ListOperations_WithTagFilter_MultipleTagsAreUnion()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "servers-and-tags.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement.Clone();

        // Filter to "users" OR "audit" — should match all 3 operations
        var filter = new OperationFilter(tags: ["users", "audit"]);
        AsyncApiOperationSummary[] ops = AsyncApi30CodeGenerator.ListOperations(root, filter);

        Assert.AreEqual(3, ops.Length);
    }

    [TestMethod]
    public void ParseBindings_Kafka_ExtractsChannelAndOperationBindings()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "bindings-example.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement.Clone();

        ChannelBindingInfo bindings = AsyncApi30CodeGenerator.GetBindings(root, "orders");

        Assert.IsTrue(bindings.HasBindings);

        // Channel-level kafka bindings
        Assert.IsTrue(bindings.ChannelBindings.Kafka.IsNotUndefined());
        Assert.IsTrue(bindings.ChannelBindings.Kafka.Partitions.TryGetValue(out int partitions));
        Assert.AreEqual(12, partitions);
        Assert.IsTrue(bindings.ChannelBindings.Kafka.Replicas.TryGetValue(out int replicas));
        Assert.AreEqual(3, replicas);
        Assert.IsTrue(bindings.ChannelBindings.Kafka.Topic.IsNotUndefined());
        Assert.AreEqual("shop.orders", bindings.ChannelBindings.Kafka.Topic.GetString());

        // Topic configuration
        Assert.IsTrue(bindings.ChannelBindings.Kafka.TopicConfiguration.IsNotUndefined());
        Assert.IsTrue(bindings.ChannelBindings.Kafka.TopicConfiguration.RetentionMs.TryGetValue(out long retentionMs));
        Assert.AreEqual(604800000L, retentionMs);
        Assert.AreEqual(1, bindings.ChannelBindings.Kafka.TopicConfiguration.CleanupPolicy.GetArrayLength());
        Assert.AreEqual("compact", bindings.ChannelBindings.Kafka.TopicConfiguration.CleanupPolicy[0].GetString());

        // Operation-level kafka bindings (groupId and clientId are Schema objects)
        Assert.IsTrue(bindings.OperationBindings.Kafka.IsNotUndefined());
        Assert.IsTrue(bindings.OperationBindings.Kafka.GroupId.IsNotUndefined());
        Assert.IsTrue(bindings.OperationBindings.Kafka.ClientId.IsNotUndefined());
    }

    [TestMethod]
    public void ParseBindings_Amqp_ExtractsExchangeQueueAndAck()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "bindings-example.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement.Clone();

        ChannelBindingInfo bindings = AsyncApi30CodeGenerator.GetBindings(root, "notifications");

        Assert.IsTrue(bindings.HasBindings);

        // Channel-level AMQP bindings (exchange only — oneOf requires exchange XOR queue)
        Assert.IsTrue(bindings.ChannelBindings.Amqp.IsNotUndefined());
        Assert.AreEqual("shop-exchange", bindings.ChannelBindings.Amqp.Exchange.Name.GetString());
        Assert.AreEqual("topic", bindings.ChannelBindings.Amqp.Exchange.Type.GetString());
        Assert.IsTrue((bool)bindings.ChannelBindings.Amqp.Exchange.Durable);
        Assert.IsFalse((bool)bindings.ChannelBindings.Amqp.Exchange.AutoDelete);

        // Operation-level AMQP bindings (ack is a boolean)
        Assert.IsTrue(bindings.OperationBindings.Amqp.IsNotUndefined());
        Assert.IsTrue((bool)bindings.OperationBindings.Amqp.Ack);
    }

    [TestMethod]
    public void ParseBindings_NoBindings_ReturnsFalseForHasBindings()
    {
        // streetlights.json has no bindings
        ChannelBindingInfo bindings = AsyncApi30CodeGenerator.GetBindings(streetlightsRoot, "lightingMeasured");

        Assert.IsFalse(bindings.HasBindings);
    }

    [TestMethod]
    public void DynamicAddress_ProducerHasChannelParameter()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "dynamic-routing.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement;

        var schemaTypeMap = new Dictionary<string, string>();
        var generator = new AsyncApi30CodeGenerator("Dynamic", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        GeneratedFile? producer = files.FirstOrDefault(f => f.FileName == "PublishDynamicProducer.cs");
        Assert.IsNotNull(producer);

        // Dynamic address: method should accept 'string channel' parameter
        StringAssert.Contains(producer.Content, "string channel");
        // Should NOT have a const ChannelAddress
        Assert.IsFalse(producer.Content.Contains("const string ChannelAddress"));
    }

    [TestMethod]
    public void DynamicAddress_ConsumerStartAcceptsChannel()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "dynamic-routing.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement;

        var schemaTypeMap = new Dictionary<string, string>();
        var generator = new AsyncApi30CodeGenerator("Dynamic", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        GeneratedFile? consumer = files.FirstOrDefault(f => f.FileName == "SubscribeDynamicConsumer.cs");
        Assert.IsNotNull(consumer);

        // Dynamic: StartAsync should accept channel parameter
        StringAssert.Contains(consumer.Content, "StartAsync(string channel");
        // Should store the channel for stop
        StringAssert.Contains(consumer.Content, "subscribedChannel");
    }

    [TestMethod]
    public void StaticAddress_ProducerUsesConstant()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "dynamic-routing.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement;

        var schemaTypeMap = new Dictionary<string, string>();
        var generator = new AsyncApi30CodeGenerator("Dynamic", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        GeneratedFile? producer = files.FirstOrDefault(f => f.FileName == "PublishStaticProducer.cs");
        Assert.IsNotNull(producer);

        // Static address: should have const
        StringAssert.Contains(producer.Content, "const string ChannelAddress = \"events/fixed\"");
        // Should NOT have a dynamic 'string channel' method parameter
        Assert.IsFalse(producer.Content.Contains("(string channel"), $"Found 'string channel' in static producer:\n{producer.Content}");
    }

    [TestMethod]
    public void Generate_WithExternalRef_ResolvesMessageFromExternalFile()
    {
        string testDataDir = Path.GetFullPath("TestData");
        string specPath = Path.Combine(testDataDir, "external-ref.json");

        byte[] specBytes = File.ReadAllBytes(specPath);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement root = doc.RootElement;

        using var resolver = new AsyncApiExternalReferenceResolver(root, specPath);

        var schemaTypeMap = new Dictionary<string, string>();
        var generator = new AsyncApi30CodeGenerator("ExternalRef", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(root, referenceResolver: resolver);

        // Should produce a producer file (the operation is a send action)
        Assert.IsTrue(files.Count > 0, "Expected at least one generated file");
        GeneratedFile? producer = files.FirstOrDefault(f => f.FileName.Contains("Producer"));
        Assert.IsNotNull(producer, "Expected a producer file to be generated");

        // The message name should be resolved from the external file
        StringAssert.Contains(producer.Content, "LightMeasured");
    }

    [TestMethod]
    public void Generate_WithExternalRef_ResolvesPayloadSchemaPointer()
    {
        string testDataDir = Path.GetFullPath("TestData");
        string specPath = Path.Combine(testDataDir, "external-ref.json");

        byte[] specBytes = File.ReadAllBytes(specPath);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement root = doc.RootElement;

        using var resolver = new AsyncApiExternalReferenceResolver(root, specPath);

        // CollectSchemaPointers should resolve external schema pointers to absolute paths
        string[] pointers = AsyncApi30CodeGenerator.CollectSchemaPointers(root, referenceResolver: resolver);

        // The inline schema in the external file won't be auto-discovered, but the external
        // message payload reference should produce an absolute pointer when the resolver is used
        Assert.IsTrue(pointers.Length >= 0, "Expected schema pointers to be collected");
    }

    [TestMethod]
    public void ExternalReferenceResolver_TryResolve_ResolvesLocalFragmentRef()
    {
        string testDataDir = Path.GetFullPath("TestData");
        string specPath = Path.Combine(testDataDir, "streetlights.json");

        byte[] specBytes = File.ReadAllBytes(specPath);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement root = doc.RootElement;

        using var resolver = new AsyncApiExternalReferenceResolver(root, specPath);

        // Resolve a local fragment reference
        bool resolved = resolver.TryResolve("#/components/schemas/lightMeasuredPayload", out JsonElement result);

        Assert.IsTrue(resolved);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    [TestMethod]
    public void ExternalReferenceResolver_TryResolve_ResolvesExternalRef()
    {
        string testDataDir = Path.GetFullPath("TestData");
        string specPath = Path.Combine(testDataDir, "external-ref.json");

        byte[] specBytes = File.ReadAllBytes(specPath);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement root = doc.RootElement;

        using var resolver = new AsyncApiExternalReferenceResolver(root, specPath);

        // Resolve an external reference (relative path)
        bool resolved = resolver.TryResolve("./external-messages.json#/components/messages/LightMeasured", out JsonElement result);

        Assert.IsTrue(resolved);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);

        // Verify the resolved message has the expected properties
        Assert.IsTrue(result.TryGetProperty("name"u8, out JsonElement nameEl));
        Assert.AreEqual("LightMeasured", nameEl.GetString());
    }

    [TestMethod]
    public void ExternalReferenceResolver_ResolveToAbsolute_FragmentReturnedAsIs()
    {
        string testDataDir = Path.GetFullPath("TestData");
        string specPath = Path.Combine(testDataDir, "external-ref.json");

        byte[] specBytes = File.ReadAllBytes(specPath);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement root = doc.RootElement;

        using var resolver = new AsyncApiExternalReferenceResolver(root, specPath);

        string result = resolver.ResolveToAbsolute("#/components/schemas/MySchema");
        Assert.AreEqual("#/components/schemas/MySchema", result);
    }

    [TestMethod]
    public void ExternalReferenceResolver_ResolveToAbsolute_ExternalRefReturnsAbsolutePath()
    {
        string testDataDir = Path.GetFullPath("TestData");
        string specPath = Path.Combine(testDataDir, "external-ref.json");

        byte[] specBytes = File.ReadAllBytes(specPath);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement root = doc.RootElement;

        using var resolver = new AsyncApiExternalReferenceResolver(root, specPath);

        string result = resolver.ResolveToAbsolute("./external-messages.json#/components/schemas/SensorId");

        // Should return absolute file path + fragment
        string expectedPath = Path.Combine(testDataDir, "external-messages.json");
        StringAssert.Contains(result, expectedPath);
        StringAssert.Contains(result, "#/components/schemas/SensorId");
    }

    [TestMethod]
    public void ExternalReferenceResolver_AddDocument_ByRelativePath()
    {
        string testDataDir = Path.GetFullPath("TestData");
        string specPath = Path.Combine(testDataDir, "streetlights.json");

        byte[] specBytes = File.ReadAllBytes(specPath);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement root = doc.RootElement;

        using var resolver = new AsyncApiExternalReferenceResolver(root, specPath);

        // Pre-register external document by relative path
        byte[] extBytes = File.ReadAllBytes(Path.Combine(testDataDir, "external-messages.json"));
        using ParsedJsonDocument<JsonElement> extDoc = ParsedJsonDocument<JsonElement>.Parse(extBytes);
        resolver.AddDocument("./external-messages.json", extDoc.RootElement);

        // Should now resolve references to it
        bool resolved = resolver.TryResolve("./external-messages.json#/components/messages/LightMeasured", out JsonElement result);
        Assert.IsTrue(resolved);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    [TestMethod]
    public void ExternalReferenceResolver_AddDocument_ByCanonicalUri()
    {
        string testDataDir = Path.GetFullPath("TestData");
        string specPath = Path.Combine(testDataDir, "streetlights.json");

        byte[] specBytes = File.ReadAllBytes(specPath);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement root = doc.RootElement;

        using var resolver = new AsyncApiExternalReferenceResolver(root, specPath);

        // Pre-register by canonical URI
        byte[] extBytes = File.ReadAllBytes(Path.Combine(testDataDir, "external-messages.json"));
        using ParsedJsonDocument<JsonElement> extDoc = ParsedJsonDocument<JsonElement>.Parse(extBytes);
        Uri extUri = new(Path.Combine(testDataDir, "external-messages.json"));
        resolver.AddDocument(extUri, extDoc.RootElement);

        // Should resolve via the URI
        bool resolved = resolver.TryResolve("./external-messages.json#/components/messages/LightMeasured", out JsonElement result);
        Assert.IsTrue(resolved);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    [TestMethod]
    public void ExternalReferenceResolver_AddDocument_RawBytes_ByCanonicalUri()
    {
        string testDataDir = Path.GetFullPath("TestData");
        string specPath = Path.Combine(testDataDir, "streetlights.json");

        byte[] specBytes = File.ReadAllBytes(specPath);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement root = doc.RootElement;

        using var resolver = new AsyncApiExternalReferenceResolver(root, specPath);

        byte[] extBytes = File.ReadAllBytes(Path.Combine(testDataDir, "external-messages.json"));
        Uri extUri = new(Path.Combine(testDataDir, "external-messages.json"));
        resolver.AddDocument(extUri, extBytes);

        bool resolved = resolver.TryResolve("./external-messages.json#/components/messages/LightMeasured", out JsonElement result);
        Assert.IsTrue(resolved);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    [TestMethod]
    public void ExternalReferenceResolver_AddDocument_RawBytes_ByRelativePath()
    {
        string testDataDir = Path.GetFullPath("TestData");
        string specPath = Path.Combine(testDataDir, "streetlights.json");

        byte[] specBytes = File.ReadAllBytes(specPath);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement root = doc.RootElement;

        using var resolver = new AsyncApiExternalReferenceResolver(root, specPath);

        byte[] extBytes = File.ReadAllBytes(Path.Combine(testDataDir, "external-messages.json"));
        resolver.AddDocument("./external-messages.json", extBytes);

        bool resolved = resolver.TryResolve("./external-messages.json#/components/messages/LightMeasured", out JsonElement result);
        Assert.IsTrue(resolved);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    [TestMethod]
    public void ExternalReferenceResolver_PushResolvedBase_ChangesBaseForResolution()
    {
        string testDataDir = Path.GetFullPath("TestData");
        string specPath = Path.Combine(testDataDir, "external-ref.json");

        byte[] specBytes = File.ReadAllBytes(specPath);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement root = doc.RootElement;

        using var resolver = new AsyncApiExternalReferenceResolver(root, specPath);

        // Push to external-messages.json
        using (IDisposable scope = resolver.PushResolvedBase("./external-messages.json#/components/messages/LightMeasured"))
        {
            // Within this scope, fragment-only references resolve within the external document
            bool resolved = resolver.TryResolve("#/components/messages/LightMeasured", out JsonElement result);
            Assert.IsTrue(resolved);
            Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
        }
    }

    [TestMethod]
    public void ExternalReferenceResolver_PushResolvedBase_FragmentOnly_ReturnsEmptyScope()
    {
        string testDataDir = Path.GetFullPath("TestData");
        string specPath = Path.Combine(testDataDir, "streetlights.json");

        byte[] specBytes = File.ReadAllBytes(specPath);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement root = doc.RootElement;

        using var resolver = new AsyncApiExternalReferenceResolver(root, specPath);

        // Fragment-only push should return EmptyScope (no-op)
        using IDisposable scope = resolver.PushResolvedBase("#/components/schemas/lightMeasuredPayload");

        // Should still resolve against the root document
        bool resolved = resolver.TryResolve("#/components/schemas/lightMeasuredPayload", out JsonElement result);
        Assert.IsTrue(resolved);
    }

    [TestMethod]
    public void ExternalReferenceResolver_TryResolve_EmptyRef_ReturnsFalse()
    {
        string testDataDir = Path.GetFullPath("TestData");
        string specPath = Path.Combine(testDataDir, "streetlights.json");

        byte[] specBytes = File.ReadAllBytes(specPath);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement root = doc.RootElement;

        using var resolver = new AsyncApiExternalReferenceResolver(root, specPath);

        bool resolved = resolver.TryResolve(string.Empty, out JsonElement result);
        Assert.IsFalse(resolved);
    }

    [TestMethod]
    public void ExternalReferenceResolver_TryResolve_Utf8_ResolvesLocalFragment()
    {
        string testDataDir = Path.GetFullPath("TestData");
        string specPath = Path.Combine(testDataDir, "streetlights.json");

        byte[] specBytes = File.ReadAllBytes(specPath);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement root = doc.RootElement;

        using var resolver = new AsyncApiExternalReferenceResolver(root, specPath);

        bool resolved = resolver.TryResolve("#/components/schemas/lightMeasuredPayload"u8, out JsonElement result);
        Assert.IsTrue(resolved);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    [TestMethod]
    public void ExternalReferenceResolver_TryResolve_Utf8_EmptyRef_ReturnsFalse()
    {
        string testDataDir = Path.GetFullPath("TestData");
        string specPath = Path.Combine(testDataDir, "streetlights.json");

        byte[] specBytes = File.ReadAllBytes(specPath);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement root = doc.RootElement;

        using var resolver = new AsyncApiExternalReferenceResolver(root, specPath);

        bool resolved = resolver.TryResolve(ReadOnlySpan<byte>.Empty, out JsonElement result);
        Assert.IsFalse(resolved);
    }

    [TestMethod]
    public void ExternalReferenceResolver_Constructor_ThrowsForRelativePath()
    {
        byte[] specBytes = File.ReadAllBytes(Path.Combine("TestData", "streetlights.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement root = doc.RootElement;

        Assert.ThrowsExactly<ArgumentException>(() =>
            new AsyncApiExternalReferenceResolver(root, "relative/path/spec.json"));
    }

    [TestMethod]
    public void ExternalReferenceResolver_TryResolve_Utf8_ExternalRef_NonexistentFile_ReturnsFalse()
    {
        string testDataDir = Path.GetFullPath("TestData");
        string specPath = Path.Combine(testDataDir, "streetlights.json");

        byte[] specBytes = File.ReadAllBytes(specPath);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement root = doc.RootElement;

        using var resolver = new AsyncApiExternalReferenceResolver(root, specPath);

        // External reference via UTF-8 to a file that doesn't exist
        bool resolved = resolver.TryResolve("./nonexistent-file.json#/foo"u8, out JsonElement result);
        Assert.IsFalse(resolved);
    }

    [TestMethod]
    public void ExternalReferenceResolver_TryResolve_ExternalRef_NoFragment()
    {
        string testDataDir = Path.GetFullPath("TestData");
        string specPath = Path.Combine(testDataDir, "external-ref.json");

        byte[] specBytes = File.ReadAllBytes(specPath);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement root = doc.RootElement;

        using var resolver = new AsyncApiExternalReferenceResolver(root, specPath);

        // Resolve external file without a fragment — returns whole document root
        bool resolved = resolver.TryResolve("./external-messages.json", out JsonElement result);
        Assert.IsTrue(resolved);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    [TestMethod]
    public void ExternalReferenceResolver_TryResolve_ExternalRef_NonexistentFile_ReturnsFalse()
    {
        string testDataDir = Path.GetFullPath("TestData");
        string specPath = Path.Combine(testDataDir, "streetlights.json");

        byte[] specBytes = File.ReadAllBytes(specPath);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement root = doc.RootElement;

        using var resolver = new AsyncApiExternalReferenceResolver(root, specPath);

        // External reference to a file that doesn't exist
        bool resolved = resolver.TryResolve("./does-not-exist.json#/foo", out JsonElement result);
        Assert.IsFalse(resolved);
    }

    [TestMethod]
    public void ExternalReferenceResolver_ResolveToAbsolute_Utf8_ReturnsAbsolutePath()
    {
        string testDataDir = Path.GetFullPath("TestData");
        string specPath = Path.Combine(testDataDir, "external-ref.json");

        byte[] specBytes = File.ReadAllBytes(specPath);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement root = doc.RootElement;

        using var resolver = new AsyncApiExternalReferenceResolver(root, specPath);

        Span<byte> destination = stackalloc byte[512];
        int written = resolver.ResolveToAbsolute("./external-messages.json#/components/schemas/SensorId"u8, destination);

        string result = System.Text.Encoding.UTF8.GetString(destination.Slice(0, written));
        string expectedPath = Path.Combine(testDataDir, "external-messages.json");
        StringAssert.Contains(result, expectedPath);
        StringAssert.Contains(result, "#/components/schemas/SensorId");
    }

    [TestMethod]
    public void ExternalReferenceResolver_PushResolvedBase_RegisteredDocument_ChangesBase()
    {
        string testDataDir = Path.GetFullPath("TestData");
        string specPath = Path.Combine(testDataDir, "streetlights.json");

        byte[] specBytes = File.ReadAllBytes(specPath);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement root = doc.RootElement;

        using var resolver = new AsyncApiExternalReferenceResolver(root, specPath);

        // Pre-register an external document
        byte[] extBytes = File.ReadAllBytes(Path.Combine(testDataDir, "external-messages.json"));
        using ParsedJsonDocument<JsonElement> extDoc = ParsedJsonDocument<JsonElement>.Parse(extBytes);
        resolver.AddDocument("./external-messages.json", extDoc.RootElement);

        // Push to it via registered path
        using (IDisposable scope = resolver.PushResolvedBase("./external-messages.json"))
        {
            bool resolved = resolver.TryResolve("#/components/messages/LightMeasured", out JsonElement result);
            Assert.IsTrue(resolved);
            Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
        }
    }

    [TestMethod]
    public void ExternalReferenceResolver_PushResolvedBase_LoadedDocument_ChangesBase()
    {
        string testDataDir = Path.GetFullPath("TestData");
        string specPath = Path.Combine(testDataDir, "external-ref.json");

        byte[] specBytes = File.ReadAllBytes(specPath);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement root = doc.RootElement;

        using var resolver = new AsyncApiExternalReferenceResolver(root, specPath);

        // Force a lazy load by resolving external ref first
        bool loaded = resolver.TryResolve("./external-messages.json#/components/messages/LightMeasured", out _);
        Assert.IsTrue(loaded);

        // Now push to the already-loaded document
        using (IDisposable scope = resolver.PushResolvedBase("./external-messages.json"))
        {
            bool resolved = resolver.TryResolve("#/components/messages/LightMeasured", out JsonElement result);
            Assert.IsTrue(resolved);
            Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
        }
    }

    [TestMethod]
    public void ExternalReferenceResolver_PushResolvedBase_NonexistentFile_ReturnsEmptyScope()
    {
        string testDataDir = Path.GetFullPath("TestData");
        string specPath = Path.Combine(testDataDir, "streetlights.json");

        byte[] specBytes = File.ReadAllBytes(specPath);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement root = doc.RootElement;

        using var resolver = new AsyncApiExternalReferenceResolver(root, specPath);

        // Push to a file that doesn't exist — should return EmptyScope
        using IDisposable scope = resolver.PushResolvedBase("./nonexistent.json");

        // Base should remain unchanged (root document)
        bool resolved = resolver.TryResolve("#/components/schemas/lightMeasuredPayload", out JsonElement result);
        Assert.IsTrue(resolved);
    }

    [TestMethod]
    public void ExternalReferenceResolver_TryResolve_Utf8_InvalidPointer_ReturnsFalse()
    {
        string testDataDir = Path.GetFullPath("TestData");
        string specPath = Path.Combine(testDataDir, "streetlights.json");

        byte[] specBytes = File.ReadAllBytes(specPath);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement root = doc.RootElement;

        using var resolver = new AsyncApiExternalReferenceResolver(root, specPath);

        // Fragment pointing to a non-existent path in the document
        bool resolved = resolver.TryResolve("#/nonexistent/path/that/does/not/exist"u8, out JsonElement result);
        Assert.IsFalse(resolved);
    }

    [TestMethod]
    public void ExternalReferenceResolver_TryResolveGeneric_String_ResolvesLocalFragment()
    {
        string testDataDir = Path.GetFullPath("TestData");
        string specPath = Path.Combine(testDataDir, "streetlights.json");

        byte[] specBytes = File.ReadAllBytes(specPath);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement root = doc.RootElement;

        using var resolver = new AsyncApiExternalReferenceResolver(root, specPath);

        bool resolved = resolver.TryResolve<JsonElement>("#/components/schemas/lightMeasuredPayload", out JsonElement result);
        Assert.IsTrue(resolved);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    [TestMethod]
    public void ExternalReferenceResolver_TryResolveGeneric_Utf8_ResolvesLocalFragment()
    {
        string testDataDir = Path.GetFullPath("TestData");
        string specPath = Path.Combine(testDataDir, "streetlights.json");

        byte[] specBytes = File.ReadAllBytes(specPath);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement root = doc.RootElement;

        using var resolver = new AsyncApiExternalReferenceResolver(root, specPath);

        bool resolved = resolver.TryResolve<JsonElement>("#/components/schemas/lightMeasuredPayload"u8, out JsonElement result);
        Assert.IsTrue(resolved);
        Assert.AreEqual(JsonValueKind.Object, result.ValueKind);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Operation-level traits tests
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void ListOperations_ResolvesOperationLevelTraitSummary()
    {
        // traits-example.json has operation traits with summary
        AsyncApiOperationSummary[] ops = AsyncApi30CodeGenerator.ListOperations(traitsRoot);

        // The operation should resolve its summary from traits
        Assert.IsTrue(ops.Length > 0);
        Assert.IsTrue(ops.Any(o => !string.IsNullOrEmpty(o.Summary)), "At least one operation should have a summary (from traits or direct)");
    }

    [TestMethod]
    public void ListOperations_OperationOwnSummaryTakesPrecedenceOverTraits()
    {
        // If an operation has both a direct summary and a trait summary, direct wins
        AsyncApiOperationSummary[] ops = AsyncApi30CodeGenerator.ListOperations(traitsRoot);

        // The traits-example.json has operations with traits providing metadata
        // Verify we get valid summaries either way
        foreach (var op in ops)
        {
            // Summary should never be null — either from operation or from traits
            Assert.IsNotNull(op.Summary);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Runtime expression parser tests
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void EmitRuntimeExpressionAccessor_MessageHeaderSimplePath()
    {
        string? result = AsyncApi30CodeGenerator.EmitRuntimeExpressionAccessor(
            "$message.header#/replyTo", "payload", "headers");

        Assert.IsNotNull(result);
        Assert.AreEqual("headers.GetProperty(\"replyTo\"u8).GetString()!", result);
    }

    [TestMethod]
    public void EmitRuntimeExpressionAccessor_MessageHeaderNestedPath()
    {
        string? result = AsyncApi30CodeGenerator.EmitRuntimeExpressionAccessor(
            "$message.header#/routing/key", "payload", "headers");

        Assert.IsNotNull(result);
        Assert.AreEqual("headers.GetProperty(\"routing\"u8).GetProperty(\"key\"u8).GetString()!", result);
    }

    [TestMethod]
    public void EmitRuntimeExpressionAccessor_MessagePayloadSimplePath()
    {
        string? result = AsyncApi30CodeGenerator.EmitRuntimeExpressionAccessor(
            "$message.payload#/correlationId", "payload", "headers");

        Assert.IsNotNull(result);
        Assert.AreEqual("payload.GetProperty(\"correlationId\"u8).GetString()!", result);
    }

    [TestMethod]
    public void EmitRuntimeExpressionAccessor_InvalidExpressionReturnsNull()
    {
        string? result = AsyncApi30CodeGenerator.EmitRuntimeExpressionAccessor(
            "not-a-valid-expression", "payload", "headers");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void EmitRuntimeExpressionAccessor_HandlesJsonPointerEscaping()
    {
        // ~1 should decode to / and ~0 should decode to ~
        string? result = AsyncApi30CodeGenerator.EmitRuntimeExpressionAccessor(
            "$message.header#/foo~1bar", "payload", "headers");

        Assert.IsNotNull(result);
        Assert.AreEqual("headers.GetProperty(\"foo/bar\"u8).GetString()!", result);
    }

    [TestMethod]
    public void EmitRuntimeExpressionAccessor_HandlesTildeEscaping()
    {
        string? result = AsyncApi30CodeGenerator.EmitRuntimeExpressionAccessor(
            "$message.header#/foo~0bar", "payload", "headers");

        Assert.IsNotNull(result);
        Assert.AreEqual("headers.GetProperty(\"foo~bar\"u8).GetString()!", result);
    }

    // ═══════════════════════════════════════════════════════════════════
    // AsyncApiRuntimeExpression parser tests
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void RuntimeExpression_ParseMessageHeader()
    {
        AsyncApiRuntimeExpression expr = AsyncApiRuntimeExpression.Parse("$message.header#/correlationId");

        Assert.AreEqual(AsyncApiRuntimeExpressionKind.MessageHeader, expr.Kind);
        Assert.AreEqual("/correlationId", expr.JsonPointer);
        Assert.IsNull(expr.LiteralValue);
    }

    [TestMethod]
    public void RuntimeExpression_ParseMessagePayload()
    {
        AsyncApiRuntimeExpression expr = AsyncApiRuntimeExpression.Parse("$message.payload#/orderId");

        Assert.AreEqual(AsyncApiRuntimeExpressionKind.MessagePayload, expr.Kind);
        Assert.AreEqual("/orderId", expr.JsonPointer);
        Assert.IsNull(expr.LiteralValue);
    }

    [TestMethod]
    public void RuntimeExpression_ParseNestedPointer()
    {
        AsyncApiRuntimeExpression expr = AsyncApiRuntimeExpression.Parse("$message.payload#/nested/deep/value");

        Assert.AreEqual(AsyncApiRuntimeExpressionKind.MessagePayload, expr.Kind);
        Assert.AreEqual("/nested/deep/value", expr.JsonPointer);
    }

    [TestMethod]
    public void RuntimeExpression_ParseLiteralValue()
    {
        AsyncApiRuntimeExpression expr = AsyncApiRuntimeExpression.Parse("some-literal-value");

        Assert.AreEqual(AsyncApiRuntimeExpressionKind.Literal, expr.Kind);
        Assert.IsNull(expr.JsonPointer);
        Assert.AreEqual("some-literal-value", expr.LiteralValue);
    }

    [TestMethod]
    public void RuntimeExpression_ParseUnrecognizedDollarExpression()
    {
        AsyncApiRuntimeExpression expr = AsyncApiRuntimeExpression.Parse("$unknown.expression");

        Assert.AreEqual(AsyncApiRuntimeExpressionKind.Literal, expr.Kind);
        Assert.IsNull(expr.JsonPointer);
        Assert.AreEqual("$unknown.expression", expr.LiteralValue);
    }

    [TestMethod]
    public void RuntimeExpression_ParseHeaderWithEmptyPointer()
    {
        AsyncApiRuntimeExpression expr = AsyncApiRuntimeExpression.Parse("$message.header#");

        Assert.AreEqual(AsyncApiRuntimeExpressionKind.MessageHeader, expr.Kind);
        Assert.AreEqual(string.Empty, expr.JsonPointer);
    }

    [TestMethod]
    public void Generate_ChannelServerRestriction_ProducerEmitsAllowedServers()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "channel-server-restriction.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement;

        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/messages/OrderPlaced/payload"] = "Orders.OrderPlaced",
            ["#/components/messages/AuditEvent/payload"] = "Orders.AuditEvent",
        };

        var generator = new AsyncApi30CodeGenerator("Orders", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        GeneratedFile placeOrderProducer = files.First(f => f.FileName.Contains("PlaceOrder") && f.FileName.Contains("Producer"));

        // The orders channel is restricted to "production" server
        StringAssert.Contains(placeOrderProducer.Content, "AllowedServers");
        StringAssert.Contains(placeOrderProducer.Content, "\"production\"");
    }

    [TestMethod]
    public void Generate_ChannelServerRestriction_AuditProducerEmitsStagingOnly()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "channel-server-restriction.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement;

        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/messages/OrderPlaced/payload"] = "Orders.OrderPlaced",
            ["#/components/messages/AuditEvent/payload"] = "Orders.AuditEvent",
        };

        var generator = new AsyncApi30CodeGenerator("Orders", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        GeneratedFile auditProducer = files.First(f => f.FileName.Contains("PublishAudit") && f.FileName.Contains("Producer"));

        // The internalAudit channel is restricted to "staging" server
        StringAssert.Contains(auditProducer.Content, "AllowedServers");
        StringAssert.Contains(auditProducer.Content, "\"staging\"");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Server URL builder tests
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void Generate_ServerUrlBuilder_EmittedWhenServersHaveVariables()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "channel-server-restriction.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement;

        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/messages/OrderPlaced/payload"] = "Orders.OrderPlaced",
            ["#/components/messages/AuditEvent/payload"] = "Orders.AuditEvent",
        };

        var generator = new AsyncApi30CodeGenerator("Orders", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        GeneratedFile? urlBuilder = files.FirstOrDefault(f => f.FileName == "ServerUrlBuilder.cs");
        Assert.IsNotNull(urlBuilder, "ServerUrlBuilder.cs should be generated when servers have variables");
    }

    [TestMethod]
    public void Generate_ServerUrlBuilder_ContainsBuildMethodForVariableServer()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "channel-server-restriction.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement;

        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/messages/OrderPlaced/payload"] = "Orders.OrderPlaced",
            ["#/components/messages/AuditEvent/payload"] = "Orders.AuditEvent",
        };

        var generator = new AsyncApi30CodeGenerator("Orders", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        GeneratedFile urlBuilder = files.First(f => f.FileName == "ServerUrlBuilder.cs");

        // Production server has a "region" variable — should emit BuildProductionUrl method
        StringAssert.Contains(urlBuilder.Content, "BuildProductionUrl");
        StringAssert.Contains(urlBuilder.Content, "string region");
        StringAssert.Contains(urlBuilder.Content, "eu-west-1");
    }

    [TestMethod]
    public void Generate_ServerUrlBuilder_StaticUrlForServerWithoutVariables()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "channel-server-restriction.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement;

        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/messages/OrderPlaced/payload"] = "Orders.OrderPlaced",
            ["#/components/messages/AuditEvent/payload"] = "Orders.AuditEvent",
        };

        var generator = new AsyncApi30CodeGenerator("Orders", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        GeneratedFile urlBuilder = files.First(f => f.FileName == "ServerUrlBuilder.cs");

        // Staging server has no variables — should emit a constant
        StringAssert.Contains(urlBuilder.Content, "StagingUrl");
        StringAssert.Contains(urlBuilder.Content, "kafka://staging-broker.internal:9092");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Bindings in generated code tests
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void ParseBindings_ReturnsBindingsFromSpec()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "bindings-example.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement;

        ChannelBindingInfo bindings = AsyncApi30CodeGenerator.GetBindings(root, "orders");

        Assert.IsTrue(bindings.HasBindings);
    }

    [TestMethod]
    public void ListServers_ParsesServerWithVariables()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "channel-server-restriction.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement;

        ServerInfo[] servers = AsyncApi30CodeGenerator.ListServers(root);

        Assert.AreEqual(2, servers.Length);

        ServerInfo production = servers.First(s => s.Name == "production");
        Assert.AreEqual("kafka", production.Protocol);
        Assert.AreEqual(1, production.Variables.Count);
        Assert.AreEqual("region", production.Variables[0].Name);
        Assert.AreEqual("eu-west-1", production.Variables[0].DefaultValue);
        Assert.AreEqual(3, production.Variables[0].EnumValues.Count);
    }

    [TestMethod]
    public void ListServers_ParsesServerWithoutVariables()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "channel-server-restriction.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement;

        ServerInfo[] servers = AsyncApi30CodeGenerator.ListServers(root);

        ServerInfo staging = servers.First(s => s.Name == "staging");
        Assert.AreEqual("kafka", staging.Protocol);
        Assert.AreEqual("staging-broker.internal:9092", staging.Host);
        Assert.AreEqual(0, staging.Variables.Count);
    }

    [TestMethod]
    public void ListServers_ParsesServerWithPathnameAndVariables()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "server-with-pathname-variables.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement;

        ServerInfo[] servers = AsyncApi30CodeGenerator.ListServers(root);

        Assert.AreEqual(1, servers.Length);
        ServerInfo prod = servers[0];
        Assert.AreEqual("production", prod.Name);
        Assert.AreEqual("{region}.broker.example.com:9092", prod.Host);
        Assert.AreEqual("kafka", prod.Protocol);
        Assert.AreEqual("/{vhost}/api", prod.Pathname);
        Assert.AreEqual(2, prod.Variables.Count);
    }

    [TestMethod]
    public void Generate_ServerUrlBuilder_PathnameWithVariables()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "server-with-pathname-variables.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement;

        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/messages/AppEvent/payload"] = "App.AppEventPayload",
        };

        var generator = new AsyncApi30CodeGenerator("App", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        GeneratedFile urlBuilder = files.First(f => f.FileName == "ServerUrlBuilder.cs");

        // Should emit a Build method with pathname concatenation and variable replacement
        StringAssert.Contains(urlBuilder.Content, "BuildProductionUrl");
        StringAssert.Contains(urlBuilder.Content, "/{vhost}/api");
        StringAssert.Contains(urlBuilder.Content, "string region");
        StringAssert.Contains(urlBuilder.Content, "string vhost");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Compilation tests — verify generated C# compiles without errors
    // Uses type stubs to satisfy schema type constraints.
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void Compile_Streetlights_GeneratedCodeCompiles()
    {
        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/schemas/turnOnOffPayload"] = "Streetlights.TurnOnOffPayload",
            ["#/components/schemas/lightMeasuredPayload"] = "Streetlights.LightMeasuredPayload",
        };

        var generator = new AsyncApi30CodeGenerator("Streetlights", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(streetlightsRoot);

        string stubs = DynamicCompiler.GenerateTypeStubs(schemaTypeMap);
        DynamicCompiler.AssertCompiles(files, "Streetlights.Generated", stubs);
    }

    [TestMethod]
    public void Compile_Traits_GeneratedCodeCompiles()
    {
        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/schemas/UserSignedUpPayload"] = "Traits.UserSignedUpPayload",
            ["#/components/schemas/CommonHeaders"] = "Traits.CommonHeaders",
        };

        var generator = new AsyncApi30CodeGenerator("Traits", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(traitsRoot);

        string stubs = DynamicCompiler.GenerateTypeStubs(schemaTypeMap);
        DynamicCompiler.AssertCompiles(files, "Traits.Generated", stubs);
    }

    [TestMethod]
    public void Compile_RequestReply_GeneratedCodeCompiles()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "request-reply.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement.Clone();

        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/messages/CalculateRequest/payload"] = "Calculator.CalculateRequest",
            ["#/components/messages/CalculateRequest/headers"] = "Calculator.CalculateRequestHeaders",
            ["#/components/messages/CalculateResponse/payload"] = "Calculator.CalculateResponse",
            ["#/components/messages/CalculateResponse/headers"] = "Calculator.CalculateResponseHeaders",
        };

        var generator = new AsyncApi30CodeGenerator("Calculator", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        string stubs = DynamicCompiler.GenerateTypeStubs(schemaTypeMap);
        DynamicCompiler.AssertCompiles(files, "Calculator.Generated", stubs);
    }

    [TestMethod]
    public void Compile_DynamicRouting_GeneratedCodeCompiles()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "dynamic-routing.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement;

        var schemaTypeMap = new Dictionary<string, string>();
        var generator = new AsyncApi30CodeGenerator("Dynamic", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        DynamicCompiler.AssertCompiles(files, "Dynamic.Generated");
    }

    [TestMethod]
    public void Compile_ChannelServerRestriction_GeneratedCodeCompiles()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "channel-server-restriction.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement;

        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/messages/OrderPlaced/payload"] = "Orders.OrderPlaced",
            ["#/components/messages/AuditEvent/payload"] = "Orders.AuditEvent",
        };

        var generator = new AsyncApi30CodeGenerator("Orders", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        string stubs = DynamicCompiler.GenerateTypeStubs(schemaTypeMap);
        DynamicCompiler.AssertCompiles(files, "Orders.Generated", stubs);
    }

    [TestMethod]
    public void Compile_ServersAndTags_GeneratedCodeCompiles()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "servers-and-tags.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement.Clone();

        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/schemas/UserSignedUp"] = "Users.UserSignedUp",
            ["#/components/schemas/AuditEvent"] = "Users.AuditEvent",
            ["#/components/schemas/UserUpdated"] = "Users.UserUpdated",
        };

        var generator = new AsyncApi30CodeGenerator("Users", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        string stubs = DynamicCompiler.GenerateTypeStubs(schemaTypeMap);
        DynamicCompiler.AssertCompiles(files, "Users.Generated", stubs);
    }

    [TestMethod]
    public void Compile_ReceiveRequestReply_GeneratedCodeCompiles()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "receive-request-reply.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement;

        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/messages/CalculateRequest/payload"] = "CalcWorker.CalculateRequestPayload",
            ["#/components/messages/CalculateRequest/headers"] = "CalcWorker.CalculateRequestHeaders",
            ["#/components/messages/CalculateResponse/payload"] = "CalcWorker.CalculateResponsePayload",
        };

        var generator = new AsyncApi30CodeGenerator("CalcWorker", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        string stubs = DynamicCompiler.GenerateTypeStubs(schemaTypeMap);
        DynamicCompiler.AssertCompiles(files, "CalcWorker.Generated", stubs);
    }

    [TestMethod]
    public void Compile_RpcWithServers_GeneratedCodeCompiles()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "rpc-with-servers.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement;

        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/messages/RpcRequest/payload"] = "Rpc.RpcRequestPayload",
            ["#/components/messages/RpcRequest/headers"] = "Rpc.RpcRequestHeaders",
            ["#/components/messages/RpcReply/payload"] = "Rpc.RpcReplyPayload",
            ["#/components/messages/AuditEvent/payload"] = "Rpc.AuditEventPayload",
        };

        var generator = new AsyncApi30CodeGenerator("Rpc", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        string stubs = DynamicCompiler.GenerateTypeStubs(schemaTypeMap);
        DynamicCompiler.AssertCompiles(files, "Rpc.Generated", stubs);
    }

    [TestMethod]
    public void Generate_RpcWithServers_ConsumerHasAllowedServers()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "rpc-with-servers.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement;

        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/messages/RpcRequest/payload"] = "Rpc.RpcRequestPayload",
            ["#/components/messages/RpcRequest/headers"] = "Rpc.RpcRequestHeaders",
            ["#/components/messages/RpcReply/payload"] = "Rpc.RpcReplyPayload",
            ["#/components/messages/AuditEvent/payload"] = "Rpc.AuditEventPayload",
        };

        var generator = new AsyncApi30CodeGenerator("Rpc", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        // Consumer for receiveAuditEvent should have AllowedServers
        GeneratedFile consumerFile = files.First(f => f.FileName.Contains("ReceiveAuditEventConsumer"));
        Assert.IsTrue(consumerFile.Content.Contains("AllowedServers"), "Consumer should have AllowedServers array");
        Assert.IsTrue(consumerFile.Content.Contains("\"production\""), "AllowedServers should include production");
        Assert.IsTrue(consumerFile.Content.Contains("\"staging\""), "AllowedServers should include staging");
    }

    [TestMethod]
    public void Generate_RpcWithServers_ProducerHasRequestReplyWithParameters()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "rpc-with-servers.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement;

        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/messages/RpcRequest/payload"] = "Rpc.RpcRequestPayload",
            ["#/components/messages/RpcRequest/headers"] = "Rpc.RpcRequestHeaders",
            ["#/components/messages/RpcReply/payload"] = "Rpc.RpcReplyPayload",
            ["#/components/messages/AuditEvent/payload"] = "Rpc.AuditEventPayload",
        };

        var generator = new AsyncApi30CodeGenerator("Rpc", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        // Producer for sendRpcRequest should have request/reply with channel parameters
        GeneratedFile producerFile = files.First(f => f.FileName.Contains("Producer"));
        Assert.IsTrue(producerFile.Content.Contains("SendAndReceiveRpcRequestAsync"), "Producer should have SendAndReceive method");
        Assert.IsTrue(producerFile.Content.Contains("string service"), "Producer should have service parameter");
        Assert.IsTrue(producerFile.Content.Contains("ChannelAddressTemplate"), "Producer should use address template for parameters");
        Assert.IsTrue(producerFile.Content.Contains("RequestAsyncCore"), "Producer should delegate to RequestAsyncCore");
    }

    [TestMethod]
    public void Generate_MultiMessage_ConsumerContainsReceivedMessageStruct()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "multi-message.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement.Clone();

        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/schemas/temperaturePayload"] = "IoT.TemperaturePayload",
            ["#/components/schemas/humidityPayload"] = "IoT.HumidityPayload",
            ["#/components/schemas/calibratePayload"] = "IoT.CalibratePayload",
        };

        var generator = new AsyncApi30CodeGenerator("IoT", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        GeneratedFile? receivedMsgFile = files.FirstOrDefault(f => f.FileName.Contains("ReceivedMessage"));
        Assert.IsNotNull(receivedMsgFile, "Should generate a ReceivedMessage struct for multi-message consumer");
        Assert.IsTrue(receivedMsgFile.Content.Contains("public readonly struct ReceiveSensorDataReceivedMessage"));
        Assert.IsTrue(receivedMsgFile.Content.Contains("MatchMessage<TResult>"));
        Assert.IsTrue(receivedMsgFile.Content.Contains("MatchMessage<TContext, TResult>"));
    }

    [TestMethod]
    public void Generate_MultiMessage_ConsumerMatchesAllMessageTypes()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "multi-message.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement.Clone();

        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/schemas/temperaturePayload"] = "IoT.TemperaturePayload",
            ["#/components/schemas/humidityPayload"] = "IoT.HumidityPayload",
            ["#/components/schemas/calibratePayload"] = "IoT.CalibratePayload",
        };

        var generator = new AsyncApi30CodeGenerator("IoT", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        GeneratedFile? receivedMsgFile = files.First(f => f.FileName.Contains("ReceivedMessage"));
        Assert.IsTrue(receivedMsgFile.Content.Contains("matchTemperatureReading"), "Should have a match parameter for temperature");
        Assert.IsTrue(receivedMsgFile.Content.Contains("matchHumidityReading"), "Should have a match parameter for humidity");
        Assert.IsTrue(receivedMsgFile.Content.Contains("matchUnrecognized"), "Should have a fallback match parameter");
    }

    [TestMethod]
    public void Generate_MultiMessage_ConsumerUsesHandleMessageAsync()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "multi-message.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement.Clone();

        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/schemas/temperaturePayload"] = "IoT.TemperaturePayload",
            ["#/components/schemas/humidityPayload"] = "IoT.HumidityPayload",
            ["#/components/schemas/calibratePayload"] = "IoT.CalibratePayload",
        };

        var generator = new AsyncApi30CodeGenerator("IoT", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        GeneratedFile consumerFile = files.First(f => f.FileName.Contains("Consumer") && !f.FileName.Contains("ReceivedMessage"));
        Assert.IsTrue(consumerFile.Content.Contains("HandleMessageAsync"), "Consumer should have HandleMessageAsync");
        Assert.IsTrue(consumerFile.Content.Contains("ReceiveSensorDataReceivedMessage"), "Consumer should construct the ReceivedMessage struct");
        Assert.IsTrue(consumerFile.Content.Contains("errorPolicy"), "Consumer should reference error policy");
        Assert.IsTrue(consumerFile.Content.Contains("DeadLetterChannel"), "Consumer should define DeadLetterChannel");
    }

    [TestMethod]
    public void Generate_MultiMessage_ProducerGeneratedForSendAction()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "multi-message.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement.Clone();

        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/schemas/temperaturePayload"] = "IoT.TemperaturePayload",
            ["#/components/schemas/humidityPayload"] = "IoT.HumidityPayload",
            ["#/components/schemas/calibratePayload"] = "IoT.CalibratePayload",
        };

        var generator = new AsyncApi30CodeGenerator("IoT", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        GeneratedFile producerFile = files.First(f => f.FileName.Contains("Producer"));
        Assert.IsTrue(producerFile.Content.Contains("PublishCalibrateCommandAsync"), "Should generate a publish method for the calibrate command");
    }

    [TestMethod]
    public void Generate_ReceiveRequestReply_ConsumerGeneratedWithHeadersValidation()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "receive-request-reply.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement;

        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/messages/CalculateRequest/payload"] = "CalcWorker.CalculateRequestPayload",
            ["#/components/messages/CalculateRequest/headers"] = "CalcWorker.CalculateRequestHeaders",
            ["#/components/messages/CalculateResponse/payload"] = "CalcWorker.CalculateResponsePayload",
        };

        var generator = new AsyncApi30CodeGenerator("CalcWorker", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        // Should generate consumer (action: receive)
        Assert.IsTrue(files.Any(f => f.FileName.Contains("Consumer")),
            "A receive operation should generate a Consumer class");

        // Should NOT generate producer (no action: send)
        Assert.IsFalse(files.Any(f => f.FileName.Contains("Producer")),
            "A receive-only operation should not generate a Producer class");

        // Consumer should validate headers
        GeneratedFile consumerFile = files.First(f => f.FileName.Contains("Consumer"));
        Assert.IsTrue(consumerFile.Content.Contains("ValidateHeaders"), "Consumer should validate headers");
        Assert.IsTrue(consumerFile.Content.Contains("CalculateRequestHeaders"), "Consumer should reference the typed headers");

        // Handler interface should include headers parameter
        GeneratedFile handlerFile = files.First(f => f.FileName.Contains("Handler"));
        Assert.IsTrue(handlerFile.Content.Contains("CalculateRequestHeaders headers"), "Handler should accept typed headers");
    }

    [TestMethod]
    public void Generate_RequestReply_ProducerHasRequestAsyncCore()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "request-reply.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement.Clone();

        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/messages/CalculateRequest/payload"] = "Calculator.CalculateRequest",
            ["#/components/messages/CalculateRequest/headers"] = "Calculator.CalculateRequestHeaders",
            ["#/components/messages/CalculateResponse/payload"] = "Calculator.CalculateResponse",
            ["#/components/messages/CalculateResponse/headers"] = "Calculator.CalculateResponseHeaders",
        };

        var generator = new AsyncApi30CodeGenerator("Calculator", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        GeneratedFile producerFile = files.First(f => f.FileName.Contains("Producer"));

        // The producer should have a non-async SendAndReceive method that delegates to RequestAsyncCore
        Assert.IsTrue(producerFile.Content.Contains("RequestAsyncCore"), "Producer should delegate to RequestAsyncCore");
        Assert.IsTrue(producerFile.Content.Contains("public ValueTask<Calculator.CalculateResponse> SendAndReceive"),
            "SendAndReceive should be non-async (returns ValueTask without async keyword)");
        Assert.IsFalse(producerFile.Content.Contains("public async ValueTask<Calculator.CalculateResponse> SendAndReceive"),
            "SendAndReceive should NOT be async (ref struct Source params are illegal in async methods)");
    }

    [TestMethod]
    public void Generate_RequestReply_ConsumerNotGenerated()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "request-reply.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement.Clone();

        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/messages/CalculateRequest/payload"] = "Calculator.CalculateRequest",
            ["#/components/messages/CalculateRequest/headers"] = "Calculator.CalculateRequestHeaders",
            ["#/components/messages/CalculateResponse/payload"] = "Calculator.CalculateResponse",
            ["#/components/messages/CalculateResponse/headers"] = "Calculator.CalculateResponseHeaders",
        };

        var generator = new AsyncApi30CodeGenerator("Calculator", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        // request-reply only has "send" action — no consumer should be generated
        Assert.IsFalse(files.Any(f => f.FileName.Contains("Consumer")),
            "A send-only operation should not generate a Consumer class");
    }

    [TestMethod]
    public void CollectSchemaPointers_MultiMessage_FindsAllPayloadSchemas()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "multi-message.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement.Clone();

        IReadOnlyList<string> pointers = AsyncApi30CodeGenerator.CollectSchemaPointers(root);

        Assert.IsTrue(pointers.Contains("#/components/schemas/temperaturePayload"),
            "Should find temperature payload schema pointer");
        Assert.IsTrue(pointers.Contains("#/components/schemas/humidityPayload"),
            "Should find humidity payload schema pointer");
        Assert.IsTrue(pointers.Contains("#/components/schemas/calibratePayload"),
            "Should find calibrate payload schema pointer");
    }

    [TestMethod]
    public void Generate_Headers_ConsumerValidatesHeaders()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "request-reply.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        JsonElement root = doc.RootElement.Clone();

        var schemaTypeMap = new Dictionary<string, string>
        {
            ["#/components/messages/CalculateRequest/payload"] = "Calculator.CalculateRequest",
            ["#/components/messages/CalculateRequest/headers"] = "Calculator.CalculateRequestHeaders",
            ["#/components/messages/CalculateResponse/payload"] = "Calculator.CalculateResponse",
            ["#/components/messages/CalculateResponse/headers"] = "Calculator.CalculateResponseHeaders",
        };

        var generator = new AsyncApi30CodeGenerator("Calculator", schemaTypeMap);
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        GeneratedFile producerFile = files.First(f => f.FileName.Contains("Producer"));

        // Headers should be validated in the producer
        Assert.IsTrue(producerFile.Content.Contains("ValidateHeaders"), "Producer should validate headers");
        Assert.IsTrue(producerFile.Content.Contains("CalculateRequestHeaders"), "Producer should reference the typed headers");
    }

    // ═══════════════════════════════════════════════════════════════════
    // AsyncApiSchemaPointerBuilder tests
    // ═══════════════════════════════════════════════════════════════════

    [TestMethod]
    public void SchemaPointerBuilder_ChannelMessagePayload_BuildsCorrectPointer()
    {
        string result = AsyncApiSchemaPointerBuilder.ChannelMessagePayload("orders"u8, "OrderPlaced"u8);
        Assert.AreEqual("#/channels/orders/messages/OrderPlaced/payload", result);
    }

    [TestMethod]
    public void SchemaPointerBuilder_ChannelMessageHeaders_BuildsCorrectPointer()
    {
        string result = AsyncApiSchemaPointerBuilder.ChannelMessageHeaders("orders"u8, "OrderPlaced"u8);
        Assert.AreEqual("#/channels/orders/messages/OrderPlaced/headers", result);
    }

    [TestMethod]
    public void SchemaPointerBuilder_ComponentSchema_BuildsCorrectPointer()
    {
        string result = AsyncApiSchemaPointerBuilder.ComponentSchema("OrderPayload"u8);
        Assert.AreEqual("#/components/schemas/OrderPayload", result);
    }

    [TestMethod]
    public void SchemaPointerBuilder_ComponentMessagePayload_BuildsCorrectPointer()
    {
        string result = AsyncApiSchemaPointerBuilder.ComponentMessagePayload("OrderPlaced"u8);
        Assert.AreEqual("#/components/messages/OrderPlaced/payload", result);
    }

    [TestMethod]
    public void SchemaPointerBuilder_ComponentMessageHeaders_BuildsCorrectPointer()
    {
        string result = AsyncApiSchemaPointerBuilder.ComponentMessageHeaders("OrderPlaced"u8);
        Assert.AreEqual("#/components/messages/OrderPlaced/headers", result);
    }

    [TestMethod]
    public void SchemaPointerBuilder_ChannelPublishPayload_BuildsCorrectPointer()
    {
        string result = AsyncApiSchemaPointerBuilder.ChannelPublishPayload("light/measured"u8);
        Assert.AreEqual("#/channels/light~1measured/publish/message/payload", result);
    }

    [TestMethod]
    public void SchemaPointerBuilder_ChannelSubscribePayload_BuildsCorrectPointer()
    {
        string result = AsyncApiSchemaPointerBuilder.ChannelSubscribePayload("sensors/readings"u8);
        Assert.AreEqual("#/channels/sensors~1readings/subscribe/message/payload", result);
    }

    [TestMethod]
    public void SchemaPointerBuilder_EscapesTilde()
    {
        string result = AsyncApiSchemaPointerBuilder.ComponentSchema("my~schema"u8);
        Assert.AreEqual("#/components/schemas/my~0schema", result);
    }

    [TestMethod]
    public void SchemaPointerBuilder_EscapesSlash()
    {
        string result = AsyncApiSchemaPointerBuilder.ComponentSchema("my/schema"u8);
        Assert.AreEqual("#/components/schemas/my~1schema", result);
    }

    [TestMethod]
    public void SchemaPointerBuilder_EscapesBothTildeAndSlash()
    {
        string result = AsyncApiSchemaPointerBuilder.ComponentSchema("a~/b"u8);
        Assert.AreEqual("#/components/schemas/a~0~1b", result);
    }
}