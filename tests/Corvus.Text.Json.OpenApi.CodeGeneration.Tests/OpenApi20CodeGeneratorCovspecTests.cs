// <copyright file="OpenApi20CodeGeneratorCovspecTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Corvus.Text.Json.OpenApi20;

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Tests;

/// <summary>
/// Hard-corner tests for <see cref="OpenApi20CodeGenerator"/> over the purpose-built
/// coverage spec: every collectionFormat, shared parameter/response $refs, formData
/// multipart with tsv fields, file responses, operation-level schemes/produces/security
/// overrides, hostile definition keys, and path-level parameter merging.
/// </summary>
[TestClass]
public class OpenApi20CodeGeneratorCovspecTests
{
    private static JsonElement covspecRoot;
    private static IReadOnlyList<GeneratedFile> generatedFiles = null!;
    private static IReadOnlyList<GeneratedFile> generatedServerFiles = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        string json = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "TestData", "covspec-2.0.json"));
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        covspecRoot = doc.RootElement.Clone();

        OpenApi20CodeGenerator generator = new("Covspec.Client", new Dictionary<string, string>(StringComparer.Ordinal));
        generatedFiles = generator.Generate(covspecRoot);
        generatedServerFiles = generator.GenerateServer(covspecRoot);
    }

    private static string FileContent(string name)
        => generatedFiles.First(f => f.FileName == name).Content;

    [TestMethod]
    public void SharedParameterAndResponseRefsResolveToComponentPointers()
    {
        SchemaReference[] refs = OpenApi20CodeGenerator.CollectSchemaPointers(covspecRoot, out _);

        // A $ref'd shared body parameter resolves through #/parameters with a /schema tail.
        SchemaReference sharedBody = refs.Single(r => r.PositionalPointer == "#/paths/~1widgets/post/parameters/0/schema");
        Assert.AreEqual("#/parameters/SharedBody/schema", sharedBody.ResolvablePointer);

        // A $ref'd shared non-body parameter resolves to the Parameter Object itself.
        SchemaReference sharedLimit = refs.Single(r => r.PositionalPointer == "#/paths/~1legacy/post/parameters/0");
        Assert.AreEqual("#/parameters/SharedLimit", sharedLimit.ResolvablePointer);

        // A $ref'd shared response resolves through #/responses with a /schema tail.
        SchemaReference sharedError = refs.Single(r => r.PositionalPointer == "#/paths/~1widgets/get/responses/default/schema");
        Assert.AreEqual("#/responses/SharedError/schema", sharedError.ResolvablePointer);
    }

    [TestMethod]
    public void PathLevelParametersMergeIntoOperations()
    {
        SchemaReference[] refs = OpenApi20CodeGenerator.CollectSchemaPointers(covspecRoot, out var parameterNames);

        // The path-level tenant parameter is a path-level Parameter Object position.
        Assert.IsTrue(refs.Any(r => r.PositionalPointer == "#/paths/~1widgets/parameters/0"));
        Assert.AreEqual("tenant", parameterNames["/paths/~1widgets/parameters/0"]);
    }

    [TestMethod]
    public void FileTypedResponseSchemaIsNotCollected()
    {
        SchemaReference[] refs = OpenApi20CodeGenerator.CollectSchemaPointers(covspecRoot, out _);

        // type: file responses route to the raw-stream path; no schema type is generated.
        Assert.IsFalse(refs.Any(r => r.PositionalPointer.Contains("render", StringComparison.Ordinal)
            && r.PositionalPointer.EndsWith("/responses/200/schema", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void EveryQueryCollectionFormatEmitsItsWireFormat()
    {
        string request = FileContent("ListWidgetsRequest.cs");

        // csv and the csv default join with commas; ssv %20; tsv %09; pipes %7C.
        StringAssert.Contains(request, "\"%20\"u8");
        StringAssert.Contains(request, "\"%09\"u8");
        StringAssert.Contains(request, "\"%7C\"u8");

        // multi explodes into repeated name=value pairs.
        StringAssert.Contains(request, "multiTags=");
    }

    [TestMethod]
    public void MultipartFormDataWithTsvFieldEmitsEncodings()
    {
        // The uploads operation is multipart with a binary archive part and a
        // tab-delimited labels field carried via the encodings map.
        string client = FileContent("ApiUploadsClient.cs");
        StringAssert.Contains(client, "multipart/form-data");

        string clientInterface = FileContent("IApiUploadsClient.cs");
        StringAssert.Contains(clientInterface, "BinaryPartData archive");
    }

    [TestMethod]
    public void OperationSchemesOverrideSelectsHttp()
    {
        // renderWidget declares schemes: [http], overriding the root https preference.
        string request = FileContent("RenderWidgetRequest.cs");
        StringAssert.Contains(request, "http://api.example.com/v1");
    }

    [TestMethod]
    public void RootServerPrefersHttps()
    {
        // The root declares [http, https]; https is preferred.
        string request = FileContent("ListWidgetsRequest.cs");
        StringAssert.Contains(request, "https://api.example.com/v1");
    }

    [TestMethod]
    public void FileResponseWithOctetStreamProducesRawStream()
    {
        // The media type surfaces in the request struct's accept negotiation.
        string request = FileContent("RenderWidgetRequest.cs");
        StringAssert.Contains(request, "application/octet-stream");
    }

    [TestMethod]
    public void ProducesCrossProductEmitsJsonAndXml()
    {
        // listWidgets inherits the root produces [json, xml]; both media types appear
        // in the request struct's accept negotiation.
        string request = FileContent("ListWidgetsRequest.cs");
        StringAssert.Contains(request, "application/json");
        StringAssert.Contains(request, "application/xml");
    }

    [TestMethod]
    public void OperationWithoutOperationIdDerivesMethodNameFromPath()
    {
        // POST /legacy has no operationId; the method name derives from the verb + path.
        Assert.IsTrue(generatedFiles.Any(f => f.FileName == "PostLegacyRequest.cs"));
    }

    [TestMethod]
    public void SecurityRequirementsFlowIntoServerEndpointMetadata()
    {
        // createWidget declares oauthAccessCode (scope admin) with anonymous fallback;
        // scheme names and their securityDefinitions types surface in the generated
        // server endpoint security metadata.
        string allServerContent = string.Concat(generatedServerFiles.Select(f => f.Content));
        StringAssert.Contains(allServerContent, "oauthAccessCode");
        StringAssert.Contains(allServerContent, "oauth2");
    }

    [TestMethod]
    public void UrlEncodedFormBodyFromSharedAndFormDataMix()
    {
        // POST /legacy mixes a shared non-body $ref parameter and a pipes formData
        // array: the formData field synthesizes an urlencoded body. The untagged
        // operation groups under the default client.
        string client = FileContent("ApiDefaultClient.cs");
        StringAssert.Contains(client, "application/x-www-form-urlencoded");
    }

    [TestMethod]
    public void InlineResponseSchemaWithHostileRefTargetIsCollectedPositionally()
    {
        SchemaReference[] refs = OpenApi20CodeGenerator.CollectSchemaPointers(covspecRoot, out _);

        // The response object is inline (its schema $refs #/definitions/weird key!~x);
        // the $ref inside the schema is the type builder's concern, so the resolvable
        // pointer is the positional document location.
        SchemaReference weird = refs.Single(r => r.PositionalPointer == "#/paths/~1legacy/post/responses/200/schema");
        Assert.AreEqual(weird.PositionalPointer, weird.ResolvablePointer);
    }

    [TestMethod]
    public void ServerEmitsEndpointRegistrationAndHandlers()
    {
        string[] names = [.. generatedServerFiles.Select(f => f.FileName)];
        CollectionAssert.Contains(names, "ApiEndpointRegistration.cs");
        CollectionAssert.Contains(names, "IApiWidgetsHandler.cs");
        CollectionAssert.Contains(names, "IApiUploadsHandler.cs");
    }

    [TestMethod]
    public void PathSsvDegradesToCsvOnBothSidesWithWarning()
    {
        // renderWidget's sections path parameter declares collectionFormat: ssv, which
        // has no wire mapping outside query parameters: both sides use csv semantics
        // and the generated request struct carries a #warning so it is never silent.
        string request = FileContent("RenderWidgetRequest.cs");
        StringAssert.Contains(request, "#warning");
        StringAssert.Contains(request, "'sections' path parameter");
    }

    [TestMethod]
    public void ServerSplitsTsvQueryParametersOnTab()
    {
        // listWidgets declares a tsv query array; the server-side binder splits on tab.
        string registration = generatedServerFiles.First(f => f.FileName == "ApiEndpointRegistration.cs").Content;
        StringAssert.Contains(registration, "IndexOf('\\t')");
    }

    [TestMethod]
    public void ServerParsesMultipartUpload()
    {
        // The uploads operation deserializes its synthesized form body from multipart.
        string registration = generatedServerFiles.First(f => f.FileName == "ApiEndpointRegistration.cs").Content;
        StringAssert.Contains(registration, "MultipartFormDataSerializer.DeserializeAsync");
    }

    [TestMethod]
    public void ServerThreadsBodyLimitsAndFailureMapping()
    {
        string registration = generatedServerFiles.First(f => f.FileName == "ApiEndpointRegistration.cs").Content;

        StringAssert.Contains(registration, "ApiServerOptions? serverOptions = null");
        StringAssert.Contains(registration, "maxBodyLength: serverOptions.MaxBufferedRequestBodyLength");
        StringAssert.Contains(registration, "catch (OperationCanceledException)");
        StringAssert.Contains(registration, "catch (RequestBodyTooLargeException)");
        StringAssert.Contains(registration, "Payload Too Large");
    }

    [TestMethod]
    public void HeaderObjectsPrepareWithoutSchemas()
    {
        SchemaReference[] refs = OpenApi20CodeGenerator.CollectSchemaPointers(covspecRoot, out _);
        Assert.IsTrue(refs.Any(r => r.PositionalPointer == "#/paths/~1widgets/get/responses/200/headers/X-Total-Count"));

        string response = FileContent("ListWidgetsResponse.cs");
        StringAssert.Contains(response, "X-Total-Count");
    }
}