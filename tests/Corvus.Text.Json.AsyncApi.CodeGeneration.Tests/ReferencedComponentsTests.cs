// <copyright file="ReferencedComponentsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.AsyncApi.CodeGeneration;

namespace Corvus.Text.Json.AsyncApi.CodeGeneration.Tests;

/// <summary>
/// Verifies that the AsyncAPI 3.0 generator resolves <c>$ref</c> at every referenceable
/// position rather than silently dropping the referenced object: top-level channels,
/// operations, and servers, server variables, channel parameters, bindings at every
/// level, and security schemes. Guards the defect class of issue #924.
/// </summary>
[TestClass]
public class ReferencedComponentsTests
{
    private static JsonElement root;

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "referenced-components-3.0.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);
        root = doc.RootElement.Clone();
    }

    [TestMethod]
    public void ListServers_ReferencedServer_IsListed()
    {
        ServerInfo[] servers = AsyncApi30CodeGenerator.ListServers(root);

        Assert.AreEqual(1, servers.Length, "The referenced server should be listed");
        Assert.AreEqual("production", servers[0].Name);
        Assert.AreEqual("broker.example.com", servers[0].Host);
        Assert.AreEqual("kafka", servers[0].Protocol);
    }

    [TestMethod]
    public void ListServers_ReferencedServerVariable_IsListed()
    {
        ServerInfo[] servers = AsyncApi30CodeGenerator.ListServers(root);

        Assert.AreEqual(1, servers.Length, "The referenced server should be listed");
        Assert.AreEqual(1, servers[0].Variables.Count, "The referenced server variable should be listed");
        Assert.AreEqual("region", servers[0].Variables[0].Name);
        Assert.AreEqual("eu-west-1", servers[0].Variables[0].DefaultValue);
    }

    [TestMethod]
    public void ListServers_ChainedSecuritySchemeReference_ResolvesType()
    {
        ServerInfo[] servers = AsyncApi30CodeGenerator.ListServers(root);

        Assert.AreEqual(1, servers.Length, "The referenced server should be listed");
        Assert.AreEqual(1, servers[0].SecuritySchemes.Count, "The server security scheme should be listed");
        Assert.AreEqual("sasl", servers[0].SecuritySchemes[0].Type, "The chained scheme reference should resolve to the concrete scheme type");
    }

    [TestMethod]
    public void ListOperations_ReferencedOperation_IsListed()
    {
        AsyncApiOperationSummary[] ops = AsyncApi30CodeGenerator.ListOperations(root);

        Assert.AreEqual(1, ops.Length, "The referenced operation should be listed");
        Assert.AreEqual("subscribeOrders", ops[0].OperationId);
        Assert.AreEqual("orders.{orderId}.created", ops[0].ChannelAddress, "The address should come from the referenced channel, not the channel key");
    }

    [TestMethod]
    public void CollectSchemaPointers_InlineMessageInReferencedChannel_IsCollected()
    {
        string[] pointers = AsyncApi30CodeGenerator.CollectSchemaPointers(root);

        Assert.IsTrue(
            pointers.Any(p => p.Contains("OrderShipped", StringComparison.Ordinal)),
            $"The inline message in the referenced channel should contribute a schema pointer. Got: {string.Join(", ", pointers)}");
    }

    [TestMethod]
    public void GetBindings_ReferencedChannel_ResolvesChannelBindings()
    {
        ChannelBindingInfo bindings = AsyncApi30CodeGenerator.GetBindings(root, "orders");

        Assert.IsTrue(
            bindings.ChannelBindings.IsNotUndefined(),
            "Channel bindings declared on the referenced channel should resolve");
        Assert.IsTrue(bindings.ChannelBindings.Kafka.IsNotUndefined(), "The kafka channel binding should be present");
    }

    [TestMethod]
    public void GetBindings_ReferencedOperation_ResolvesOperationBindings()
    {
        ChannelBindingInfo bindings = AsyncApi30CodeGenerator.GetBindings(root, "orders");

        Assert.IsTrue(
            bindings.OperationBindings.IsNotUndefined(),
            "Operation bindings declared on the referenced operation should resolve");
        Assert.IsTrue(bindings.OperationBindings.Kafka.IsNotUndefined(), "The kafka operation binding should be present");
    }

    [TestMethod]
    public void GetBindings_ReferencedMessage_ResolvesMessageBindings()
    {
        ChannelBindingInfo bindings = AsyncApi30CodeGenerator.GetBindings(root, "orders", "OrderCreated");

        Assert.IsTrue(
            bindings.MessageBindings.IsNotUndefined(),
            "Message bindings declared behind the referenced message (and themselves a reference) should resolve");
        Assert.IsTrue(bindings.MessageBindings.Kafka.IsNotUndefined(), "The kafka message binding should be present");
    }

    [TestMethod]
    public void Generate_ReferencedChannel_ComposesTheRealAddress()
    {
        var generator = new AsyncApi30CodeGenerator("ReferencedComponents", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        GeneratedFile? consumer = files.FirstOrDefault(f => f.FileName.Contains("SubscribeOrdersConsumer"));
        Assert.IsNotNull(consumer, "The referenced receive operation should generate a consumer");

        StringAssert.Contains(
            consumer.Content,
            "public ValueTask StartAsync(string orderId, CancellationToken cancellationToken = default)",
            "The referenced parameter behind the referenced channel should become a StartAsync argument");
        StringAssert.Contains(
            consumer.Content,
            "orders.",
            "The composed address should come from the referenced channel's template, not the channel key");
    }

    [TestMethod]
    public void Generate_ReferencedChannel_EmitsAllowedServers()
    {
        var generator = new AsyncApi30CodeGenerator("ReferencedComponents", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        GeneratedFile? consumer = files.FirstOrDefault(f => f.FileName.Contains("SubscribeOrdersConsumer"));
        Assert.IsNotNull(consumer, "The referenced receive operation should generate a consumer");

        StringAssert.Contains(
            consumer.Content,
            "AllowedServers",
            "The referenced channel's server restriction should reach the generated consumer");
        StringAssert.Contains(consumer.Content, "\"production\"");
    }

    [TestMethod]
    public void Generate_DanglingReference_RecordsDiagnosticAndCompletes()
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine("TestData", "dangling-ref-3.0.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(bytes);

        var generator = new AsyncApi30CodeGenerator("DanglingRef", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = generator.Generate(doc.RootElement);

        Assert.IsTrue(
            files.Any(f => f.FileName.Contains("SubscribeOrdersConsumer")),
            "The intact operation should still generate");
        Assert.AreEqual(1, generator.Diagnostics.Count, "The dangling operation reference should record exactly one diagnostic");
        Assert.AreEqual("#/operations/brokenOp", generator.Diagnostics[0].Location);
        Assert.AreEqual(AsyncApiGenerationDiagnosticSeverity.Warning, generator.Diagnostics[0].Severity);
    }

    [TestMethod]
    public void Generate_CleanSpec_RecordsNoDiagnostics()
    {
        var generator = new AsyncApi30CodeGenerator("ReferencedComponents", new Dictionary<string, string>());
        generator.Generate(root);

        Assert.AreEqual(0, generator.Diagnostics.Count, "A fully resolvable spec should generate without diagnostics");
    }

    [TestMethod]
    public void Generate_ReferencedEverything_Compiles()
    {
        var generator = new AsyncApi30CodeGenerator("ReferencedComponents", new Dictionary<string, string>());
        IReadOnlyList<GeneratedFile> files = generator.Generate(root);

        Assert.IsTrue(files.Count > 0, "Generation should produce files");
        DynamicCompiler.AssertCompiles(files, "ReferencedComponents.Generated");
    }
}