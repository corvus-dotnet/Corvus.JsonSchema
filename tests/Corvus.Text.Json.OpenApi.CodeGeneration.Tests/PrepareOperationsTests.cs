// <copyright file="PrepareOperationsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi.CodeGeneration.Operations;
using Corvus.Text.Json.OpenApi20;
using Corvus.Text.Json.OpenApi30;
using Corvus.Text.Json.OpenApi31;
using Corvus.Text.Json.OpenApi32;

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Tests;

/// <summary>
/// The promoted operation model: every spec version prepares the same records, for both scopes.
/// </summary>
[TestClass]
public class PrepareOperationsTests
{
    private static readonly Dictionary<string, string> EmptyTypeMap = new(StringComparer.Ordinal);

    [TestMethod]
    public void OpenApi20_Paths_PreparesEveryListedOperation()
    {
        JsonElement root = Load("covspec-2.0.json");
        OpenApiClientModel model = new OpenApi20CodeGenerator("Covspec.Client", EmptyTypeMap).PrepareOperations(root, OperationScope.Paths);

        Assert.AreEqual("2.0", model.SpecVersion);
        Assert.AreEqual(OpenApi20CodeGenerator.ListOperations(root).Length, model.Operations.Length);
        Assert.AreEqual(CountProperties(root, "securityDefinitions"), model.SecuritySchemes.Length);
        Assert.IsNotNull(model.Title);
    }

    [TestMethod]
    public void OpenApi20_WebhooksAndCallbacks_IsEmpty()
    {
        JsonElement root = Load("covspec-2.0.json");
        OpenApiClientModel model = new OpenApi20CodeGenerator("Covspec.Client", EmptyTypeMap).PrepareOperations(root, OperationScope.WebhooksAndCallbacks);

        Assert.AreEqual(0, model.Operations.Length);
    }

    [TestMethod]
    public void OpenApi30_Paths_PreparesEveryListedOperation()
    {
        JsonElement root = Load("covspec-3.0.json");
        OpenApiClientModel model = new OpenApi30CodeGenerator("Covspec.Client", EmptyTypeMap).PrepareOperations(root, OperationScope.Paths);

        Assert.AreEqual("3.0", model.SpecVersion);
        Assert.AreEqual(OpenApi30CodeGenerator.ListOperations(root).Length, model.Operations.Length);
        Assert.AreEqual(CountProperties(root, "components", "securitySchemes"), model.SecuritySchemes.Length);
        Assert.IsNotNull(model.Title);
    }

    [TestMethod]
    public void OpenApi30_WebhooksAndCallbacks_PreparesEveryListedOperation()
    {
        JsonElement root = Load("callbacks-3.0.json");
        OpenApiClientModel model = new OpenApi30CodeGenerator("Callbacks.Client", EmptyTypeMap).PrepareOperations(root, OperationScope.WebhooksAndCallbacks);

        Assert.IsTrue(model.Operations.Length > 0);
        Assert.AreEqual(OpenApi30CodeGenerator.ListWebhookAndCallbackOperations(root).Length, model.Operations.Length);
    }

    [TestMethod]
    public void OpenApi31_Paths_PreparesEveryListedOperation()
    {
        JsonElement root = Load("covspec-3.1.json");
        OpenApiClientModel model = new OpenApi31CodeGenerator("Covspec.Client", EmptyTypeMap).PrepareOperations(root, OperationScope.Paths);

        Assert.AreEqual("3.1", model.SpecVersion);
        Assert.AreEqual(OpenApi31CodeGenerator.ListOperations(root).Length, model.Operations.Length);
        Assert.AreEqual(CountProperties(root, "components", "securitySchemes"), model.SecuritySchemes.Length);
        Assert.IsNotNull(model.Title);
        Assert.IsNotNull(model.RootServer);
    }

    [TestMethod]
    public void OpenApi31_WebhooksAndCallbacks_PreparesEveryListedOperation()
    {
        JsonElement root = Load("callbacks-3.1.json");
        OpenApiClientModel model = new OpenApi31CodeGenerator("Callbacks.Client", EmptyTypeMap).PrepareOperations(root, OperationScope.WebhooksAndCallbacks);

        Assert.IsTrue(model.Operations.Length > 0);
        Assert.AreEqual(OpenApi31CodeGenerator.ListWebhookAndCallbackOperations(root).Length, model.Operations.Length);
    }

    [TestMethod]
    public void OpenApi31_Oauth2Scheme_CarriesFlowDetails()
    {
        JsonElement root = Load("covspec-3.1.json");
        OpenApiClientModel model = new OpenApi31CodeGenerator("Covspec.Client", EmptyTypeMap).PrepareOperations(root, OperationScope.Paths);

        SecuritySchemeInfo oauth2 = model.SecuritySchemes.Single(s => s.SchemeType == "oauth2");
        Assert.IsNotNull(oauth2.AvailableScopes);
        Assert.IsTrue(oauth2.AvailableScopes!.Length > 0);
        Assert.IsTrue(oauth2.TokenUrl is not null || oauth2.AuthorizationUrl is not null);
    }

    [TestMethod]
    public void OpenApi32_Paths_PreparesEveryListedOperation()
    {
        JsonElement root = Load("covspec-3.2.json");
        OpenApiClientModel model = new OpenApi32CodeGenerator("Covspec.Client", EmptyTypeMap).PrepareOperations(root, OperationScope.Paths);

        Assert.AreEqual("3.2", model.SpecVersion);
        Assert.AreEqual(OpenApi32CodeGenerator.ListOperations(root).Length, model.Operations.Length);
        Assert.AreEqual(CountProperties(root, "components", "securitySchemes"), model.SecuritySchemes.Length);
        Assert.IsNotNull(model.Title);
    }

    [TestMethod]
    public void OpenApi32_WebhooksAndCallbacks_PreparesEveryListedOperation()
    {
        JsonElement root = Load("callbacks-3.2.json");
        OpenApiClientModel model = new OpenApi32CodeGenerator("Callbacks.Client", EmptyTypeMap).PrepareOperations(root, OperationScope.WebhooksAndCallbacks);

        Assert.IsTrue(model.Operations.Length > 0);
        Assert.AreEqual(OpenApi32CodeGenerator.ListWebhookAndCallbackOperations(root).Length, model.Operations.Length);
    }

    [TestMethod]
    public void EveryVersion_PreparedOperation_CarriesNeutralIdentity()
    {
        foreach ((string file, Func<JsonElement, OpenApiClientModel> prepare) in new (string, Func<JsonElement, OpenApiClientModel>)[]
        {
            ("covspec-2.0.json", r => new OpenApi20CodeGenerator("C", EmptyTypeMap).PrepareOperations(r, OperationScope.Paths)),
            ("covspec-3.0.json", r => new OpenApi30CodeGenerator("C", EmptyTypeMap).PrepareOperations(r, OperationScope.Paths)),
            ("covspec-3.1.json", r => new OpenApi31CodeGenerator("C", EmptyTypeMap).PrepareOperations(r, OperationScope.Paths)),
            ("covspec-3.2.json", r => new OpenApi32CodeGenerator("C", EmptyTypeMap).PrepareOperations(r, OperationScope.Paths)),
        })
        {
            OpenApiClientModel model = prepare(Load(file));
            foreach (OperationInfo operation in model.Operations)
            {
                Assert.IsFalse(string.IsNullOrEmpty(operation.PathTemplate), file);
                Assert.IsFalse(string.IsNullOrEmpty(operation.CSharpMethodName()), file);
                Assert.IsNotNull(operation.Parameters, file);
                Assert.IsNotNull(operation.Responses, file);
            }
        }
    }

    private static JsonElement Load(string name)
    {
        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", name));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        return doc.RootElement.Clone();
    }

    private static int CountProperties(JsonElement root, params string[] path)
    {
        JsonElement current = root;
        foreach (string segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return 0;
            }
        }

        if (current.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        int count = 0;
        foreach (var property in current.EnumerateObject())
        {
            count++;
        }

        return count;
    }
}