// <copyright file="AsyncApiSchemaNameHeuristicTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.AsyncApi.CodeGeneration;
using Corvus.Text.Json.CodeGeneration;

namespace Corvus.Text.Json.AsyncApi.CodeGeneration.Tests;

/// <summary>
/// Integration tests for <see cref="AsyncApiSchemaNameHeuristic"/>.
/// Exercises the full codegen pipeline with the heuristic registered to verify
/// that inline schemas in AsyncAPI specs receive contextual type names.
/// </summary>
[TestClass]
public class AsyncApiSchemaNameHeuristicTests
{
    private static readonly string SpecPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "TestData", "naming-heuristic.json"));

    private static Dictionary<string, string>? pointerToTypeName;

    [ClassInitialize]
    public static async Task ClassInit(TestContext context)
    {
        CompoundDocumentResolver documentResolver = new(
            new FileSystemDocumentResolver());

        VocabularyRegistry vocabularyRegistry = new();
        Corvus.Json.CodeGeneration.Draft7.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);

        JsonSchemaTypeBuilder typeBuilder = new(documentResolver, vocabularyRegistry);

        byte[] specBytes = File.ReadAllBytes(SpecPath);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement specRoot = doc.RootElement;

        string[] pointers = AsyncApi30CodeGenerator.CollectSchemaPointers(specRoot);

        Dictionary<string, TypeDeclaration> pointerToType = new(StringComparer.Ordinal);
        List<TypeDeclaration> typesToGenerate = [];

        foreach (string pointer in pointers)
        {
            JsonReference reference = new(SpecPath, pointer[1..]); // Strip leading # for JsonReference

            TypeDeclaration rootType = await typeBuilder.AddTypeDeclarationsAsync(
                reference,
                Corvus.Json.CodeGeneration.Draft7.VocabularyAnalyser.DefaultVocabulary,
                rebaseAsRoot: false);

            pointerToType[pointer] = rootType;
            typesToGenerate.Add(rootType);
        }

        CSharpLanguageProvider.Options options = new("TestApi");
        CSharpLanguageProvider languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);
        languageProvider.RegisterNameHeuristics(AsyncApiSchemaNameHeuristic.Instance);

        _ = typeBuilder.GenerateCodeUsing(
            languageProvider, typesToGenerate, CancellationToken.None);

        pointerToTypeName = new(StringComparer.Ordinal);
        foreach ((string pointer, TypeDeclaration td) in pointerToType)
        {
            TypeDeclaration reduced = td.ReducedTypeDeclaration().ReducedType;
            if (reduced.HasDotnetTypeName())
            {
                pointerToTypeName[pointer] = reduced.DotnetTypeName()?.ToString() ?? string.Empty;
            }
        }
    }

    [TestMethod]
    public void ComponentSchema_NamedAddress()
    {
        Assert.IsNotNull(pointerToTypeName);
        Assert.IsTrue(pointerToTypeName.TryGetValue("#/components/schemas/Address", out string? name));
        Assert.AreEqual("Address", name);
    }

    [TestMethod]
    public void ComponentSchema_HyphenatedName_PascalCased()
    {
        Assert.IsNotNull(pointerToTypeName);
        Assert.IsTrue(pointerToTypeName.TryGetValue("#/components/schemas/customer-info", out string? name));
        Assert.AreEqual("CustomerInfo", name);
    }

    [TestMethod]
    public void ComponentMessagePayload_Named()
    {
        Assert.IsNotNull(pointerToTypeName);
        Assert.IsTrue(pointerToTypeName.TryGetValue("#/components/messages/PaymentReceived/payload", out string? name));
        Assert.AreEqual("PaymentReceivedPayload", name);
    }

    [TestMethod]
    public void ComponentMessageHeaders_Named()
    {
        Assert.IsNotNull(pointerToTypeName);
        Assert.IsTrue(pointerToTypeName.TryGetValue("#/components/messages/PaymentReceived/headers", out string? name));
        Assert.AreEqual("PaymentReceivedHeaders", name);
    }

    [TestMethod]
    public void ChannelMessagePayload_OrderCreated()
    {
        Assert.IsNotNull(pointerToTypeName);
        Assert.IsTrue(pointerToTypeName.TryGetValue("#/channels/orders/messages/orderCreated/payload", out string? name));
        Assert.AreEqual("OrderCreatedPayload", name);
    }

    [TestMethod]
    public void ChannelMessageHeaders_OrderCreated()
    {
        Assert.IsNotNull(pointerToTypeName);
        Assert.IsTrue(pointerToTypeName.TryGetValue("#/channels/orders/messages/orderCreated/headers", out string? name));
        Assert.AreEqual("OrderCreatedHeaders", name);
    }

    [TestMethod]
    public void ChannelMessagePayload_OrderUpdated()
    {
        Assert.IsNotNull(pointerToTypeName);
        Assert.IsTrue(pointerToTypeName.TryGetValue("#/channels/orders/messages/orderUpdated/payload", out string? name));
        Assert.AreEqual("OrderUpdatedPayload", name);
    }

    [TestMethod]
    public void ChannelMessagePayload_AlertSent()
    {
        Assert.IsNotNull(pointerToTypeName);
        Assert.IsTrue(pointerToTypeName.TryGetValue("#/channels/user-notifications/messages/alertSent/payload", out string? name));
        Assert.AreEqual("AlertSentPayload", name);
    }

    [TestMethod]
    public void Heuristic_Priority_Is500()
    {
        Assert.AreEqual(500u, AsyncApiSchemaNameHeuristic.Instance.Priority);
    }

    [TestMethod]
    public void Heuristic_IsNotOptional()
    {
        Assert.IsFalse(AsyncApiSchemaNameHeuristic.Instance.IsOptional);
    }
}