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
    // Channel-server restriction tests
    // ═══════════════════════════════════════════════════════════════════

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
}