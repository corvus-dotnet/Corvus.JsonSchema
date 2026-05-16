// <copyright file="GeneratedClientEndToEndTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Text;
using CanonTests.Client;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;

namespace Corvus.Text.Json.OpenApi.Runtime.Tests;

/// <summary>
/// End-to-end tests that exercise the generated client code through the real
/// <see cref="HttpClientTransport"/>, using an in-memory <see cref="DelegatingHandler"/>
/// to capture requests and return canned responses.
/// </summary>
/// <remarks>
/// <para>
/// These tests verify that:
/// <list type="bullet">
/// <item>Request path, query, headers, cookies, and body are serialized correctly by the generated code.</item>
/// <item>Response bodies are parsed into the correct typed models.</item>
/// <item>Schema validation on response bodies reports expected results.</item>
/// <item>The TryGet/MatchResult discriminated union patterns work correctly.</item>
/// <item>Non-matching status codes, empty bodies, and invalid JSON are handled.</item>
/// <item>Generated client classes delegate correctly through the transport.</item>
/// </list>
/// </para>
/// </remarks>
[TestClass]
public class GeneratedClientEndToEndTests
{
    [TestMethod]
    public async Task GetItem_200_ParsesOkBody()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"id":"item-1","name":"Widget","price":9.99}""");

        await using GetItemResponse response = await harness.Transport
            .SendAsync<GetItemRequest, GetItemResponse>(
                new GetItemRequest(JsonString.ParseValue("\"abc\""u8)),
                CancellationToken.None);

        Assert.AreEqual(200, response.StatusCode);
        Assert.IsTrue(response.IsSuccess);
        Assert.IsTrue(response.TryGetOk(out var body));
        Assert.AreEqual("item-1", (string)body.Id);
        Assert.AreEqual("Widget", (string)body.Name);
    }

    [TestMethod]
    public async Task GetItem_200_RequestPathIsCorrect()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"id":"x","name":"y","price":1}""");

        await using GetItemResponse response = await harness.Transport
            .SendAsync<GetItemRequest, GetItemResponse>(
                new GetItemRequest(JsonString.ParseValue("\"hello world\""u8)),
                CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        Assert.AreEqual("http://localhost/items/hello%20world", harness.CapturedRequest.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task GetItem_200_QueryParamsAppended()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"id":"x","name":"y","price":1}""");

        var request = new GetItemRequest(JsonString.ParseValue("\"id1\""u8))
        {
            Filter = JsonString.ParseValue("\"a&b\""u8),
            Limit = JsonInt32.ParseValue("10"u8),
            Verbose = JsonBoolean.ParseValue("true"u8),
        };

        await using GetItemResponse response = await harness.Transport
            .SendAsync<GetItemRequest, GetItemResponse>(in request, CancellationToken.None);

        Assert.AreEqual("http://localhost/items/id1?filter=a%26b&limit=10&verbose=true", harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task GetItem_200_HeaderIsForwarded()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"id":"x","name":"y","price":1}""");

        var request = new GetItemRequest(JsonString.ParseValue("\"id1\""u8))
        {
            XRequestId = JsonString.ParseValue("\"req-42\""u8),
        };

        await using GetItemResponse response = await harness.Transport
            .SendAsync<GetItemRequest, GetItemResponse>(in request, CancellationToken.None);

        Assert.IsTrue(harness.CapturedRequest!.Headers.Contains("X-Request-Id"));
        Assert.AreEqual("req-42", harness.CapturedRequest.Headers.GetValues("X-Request-Id").First());
    }

    [TestMethod]
    public async Task GetItem_200_SchemaValidOnResponseBody()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"id":"item-1","name":"Widget","price":9.99}""");

        await using GetItemResponse response = await harness.Transport
            .SendAsync<GetItemRequest, GetItemResponse>(
                new GetItemRequest(JsonString.ParseValue("\"abc\""u8)),
                CancellationToken.None);

        Assert.IsTrue(response.TryGetOk(out var body));
        Assert.IsTrue(body.EvaluateSchema(), "Response body with required fields should be schema-valid");
    }

    [TestMethod]
    public async Task GetItem_200_SchemaInvalidResponseBody_MissingRequiredField()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"id":"item-1","price":9.99}""");

        await using GetItemResponse response = await harness.Transport
            .SendAsync<GetItemRequest, GetItemResponse>(
                new GetItemRequest(JsonString.ParseValue("\"abc\""u8)),
                CancellationToken.None);

        Assert.IsTrue(response.TryGetOk(out var body));
        Assert.IsFalse(body.EvaluateSchema(), "Response body missing required 'name' should fail schema validation");
    }

    [TestMethod]
    public async Task GetItem_404_ParsesNotFoundBody()
    {
        using var harness = new TestHarness(HttpStatusCode.NotFound, """{"code":404,"message":"not found"}""");

        await using GetItemResponse response = await harness.Transport
            .SendAsync<GetItemRequest, GetItemResponse>(
                new GetItemRequest(JsonString.ParseValue("\"missing\""u8)),
                CancellationToken.None);

        Assert.AreEqual(404, response.StatusCode);
        Assert.IsFalse(response.IsSuccess);
        Assert.IsTrue(response.TryGetNotFound(out var body));
        Assert.AreEqual(404, (int)body.Code);
        Assert.AreEqual("not found", (string)body.Message);
        Assert.IsFalse(response.TryGetOk(out _));
    }

    [TestMethod]
    public async Task GetItem_404_SchemaValidation()
    {
        using var harness = new TestHarness(HttpStatusCode.NotFound, """{"code":404,"message":"not found"}""");

        await using GetItemResponse response = await harness.Transport
            .SendAsync<GetItemRequest, GetItemResponse>(
                new GetItemRequest(JsonString.ParseValue("\"x\""u8)),
                CancellationToken.None);

        Assert.IsTrue(response.TryGetNotFound(out var body));
        Assert.IsTrue(body.EvaluateSchema(), "NotFound response with required code+message should be schema-valid");
    }

    [TestMethod]
    public async Task GetItem_404_SchemaInvalid_MissingCode()
    {
        using var harness = new TestHarness(HttpStatusCode.NotFound, """{"message":"not found"}""");

        await using GetItemResponse response = await harness.Transport
            .SendAsync<GetItemRequest, GetItemResponse>(
                new GetItemRequest(JsonString.ParseValue("\"x\""u8)),
                CancellationToken.None);

        Assert.IsTrue(response.TryGetNotFound(out var body));
        Assert.IsFalse(body.EvaluateSchema(), "NotFound response missing required 'code' should be invalid");
    }

    [TestMethod]
    public async Task GetItem_500_FallsThroughToDefault()
    {
        using var harness = new TestHarness(HttpStatusCode.InternalServerError, """{"error":"internal error"}""");

        await using GetItemResponse response = await harness.Transport
            .SendAsync<GetItemRequest, GetItemResponse>(
                new GetItemRequest(JsonString.ParseValue("\"x\""u8)),
                CancellationToken.None);

        Assert.AreEqual(500, response.StatusCode);
        Assert.IsFalse(response.IsSuccess);
        Assert.IsTrue(response.TryGetDefault(out var body));
        Assert.AreEqual("internal error", (string)body.Error);
        Assert.IsFalse(response.TryGetOk(out _));
        Assert.IsFalse(response.TryGetNotFound(out _));
    }

    [TestMethod]
    public async Task GetItem_500_DefaultSchemaValidation()
    {
        using var harness = new TestHarness(HttpStatusCode.InternalServerError, """{"error":"fail"}""");

        await using GetItemResponse response = await harness.Transport
            .SendAsync<GetItemRequest, GetItemResponse>(
                new GetItemRequest(JsonString.ParseValue("\"x\""u8)),
                CancellationToken.None);

        Assert.IsTrue(response.TryGetDefault(out var body));
        Assert.IsTrue(body.EvaluateSchema(), "Default response with required 'error' should be schema-valid");
    }

    [TestMethod]
    public async Task GetItem_500_DefaultSchemaInvalid_MissingError()
    {
        using var harness = new TestHarness(HttpStatusCode.InternalServerError, """{"something":"else"}""");

        await using GetItemResponse response = await harness.Transport
            .SendAsync<GetItemRequest, GetItemResponse>(
                new GetItemRequest(JsonString.ParseValue("\"x\""u8)),
                CancellationToken.None);

        Assert.IsTrue(response.TryGetDefault(out var body));
        Assert.IsFalse(body.EvaluateSchema(), "Default response missing required 'error' should be invalid");
    }

    [TestMethod]
    public async Task GetItem_MatchResult_DispatchesToOk()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"id":"x","name":"y","price":1}""");

        await using GetItemResponse response = await harness.Transport
            .SendAsync<GetItemRequest, GetItemResponse>(
                new GetItemRequest(JsonString.ParseValue("\"x\""u8)),
                CancellationToken.None);

        string result = response.MatchResult(
            matchOk: static body => $"ok:{(string)body.Name}",
            matchNotFound: static body => "notfound",
            matchDefault: static body => "default");

        Assert.AreEqual("ok:y", result);
    }

    [TestMethod]
    public async Task GetItem_MatchResult_DispatchesToNotFound()
    {
        using var harness = new TestHarness(HttpStatusCode.NotFound, """{"code":404,"message":"gone"}""");

        await using GetItemResponse response = await harness.Transport
            .SendAsync<GetItemRequest, GetItemResponse>(
                new GetItemRequest(JsonString.ParseValue("\"x\""u8)),
                CancellationToken.None);

        string result = response.MatchResult(
            matchOk: static body => "ok",
            matchNotFound: static body => $"notfound:{(string)body.Message}",
            matchDefault: static body => "default");

        Assert.AreEqual("notfound:gone", result);
    }

    [TestMethod]
    public async Task GetItem_MatchResult_DispatchesToDefault()
    {
        using var harness = new TestHarness(HttpStatusCode.InternalServerError, """{"error":"fail"}""");

        await using GetItemResponse response = await harness.Transport
            .SendAsync<GetItemRequest, GetItemResponse>(
                new GetItemRequest(JsonString.ParseValue("\"x\""u8)),
                CancellationToken.None);

        string result = response.MatchResult(
            matchOk: static body => "ok",
            matchNotFound: static body => "notfound",
            matchDefault: static body => $"default:{(string)body.Error}");

        Assert.AreEqual("default:fail", result);
    }

    [TestMethod]
    public async Task GetItem_MatchResultWithContext_PassesContext()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"id":"x","name":"y","price":1}""");

        await using GetItemResponse response = await harness.Transport
            .SendAsync<GetItemRequest, GetItemResponse>(
                new GetItemRequest(JsonString.ParseValue("\"x\""u8)),
                CancellationToken.None);

        string result = response.MatchResult(
            "prefix",
            matchOk: static (body, in ctx) => $"{ctx}:{(string)body.Name}",
            matchNotFound: static (body, in ctx) => $"{ctx}:notfound",
            matchDefault: static (body, in ctx) => $"{ctx}:default");

        Assert.AreEqual("prefix:y", result);
    }

    [TestMethod]
    public async Task GetItem_MatchResultWithContext_DispatchesToNotFound()
    {
        using var harness = new TestHarness(HttpStatusCode.NotFound, """{"code":404,"message":"gone"}""");

        await using GetItemResponse response = await harness.Transport
            .SendAsync<GetItemRequest, GetItemResponse>(
                new GetItemRequest(JsonString.ParseValue("\"x\""u8)),
                CancellationToken.None);

        string result = response.MatchResult(
            42,
            matchOk: static (body, in ctx) => "ok",
            matchNotFound: static (body, in ctx) => $"notfound:{ctx}:{(string)body.Message}",
            matchDefault: static (body, in ctx) => "default");

        Assert.AreEqual("notfound:42:gone", result);
    }

    [TestMethod]
    public async Task GetItem_MatchResultWithContext_DispatchesToDefault()
    {
        using var harness = new TestHarness(HttpStatusCode.InternalServerError, """{"error":"boom"}""");

        await using GetItemResponse response = await harness.Transport
            .SendAsync<GetItemRequest, GetItemResponse>(
                new GetItemRequest(JsonString.ParseValue("\"x\""u8)),
                CancellationToken.None);

        string result = response.MatchResult(
            "ctx",
            matchOk: static (body, in ctx) => "ok",
            matchNotFound: static (body, in ctx) => "notfound",
            matchDefault: static (body, in ctx) => $"default:{ctx}:{(string)body.Error}");

        Assert.AreEqual("default:ctx:boom", result);
    }

    [TestMethod]
    public async Task DeleteItem_204_NoBody()
    {
        using var harness = new TestHarness(HttpStatusCode.NoContent, string.Empty);

        await using DeleteItemResponse response = await harness.Transport
            .SendAsync<DeleteItemRequest, DeleteItemResponse>(
                new DeleteItemRequest(JsonString.ParseValue("\"del-1\""u8)),
                CancellationToken.None);

        Assert.AreEqual(204, response.StatusCode);
        Assert.IsTrue(response.IsSuccess);
        Assert.IsFalse(response.TryGetNotFound(out _));
    }

    [TestMethod]
    public async Task DeleteItem_204_RequestPathIsCorrect()
    {
        using var harness = new TestHarness(HttpStatusCode.NoContent, string.Empty);

        await using DeleteItemResponse response = await harness.Transport
            .SendAsync<DeleteItemRequest, DeleteItemResponse>(
                new DeleteItemRequest(JsonString.ParseValue("\"abc/def\""u8)),
                CancellationToken.None);

        Assert.AreEqual("http://localhost/items/abc%2Fdef", harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task DeleteItem_204_UsesDeleteMethod()
    {
        using var harness = new TestHarness(HttpStatusCode.NoContent, string.Empty);

        await using DeleteItemResponse response = await harness.Transport
            .SendAsync<DeleteItemRequest, DeleteItemResponse>(
                new DeleteItemRequest(JsonString.ParseValue("\"x\""u8)),
                CancellationToken.None);

        Assert.AreEqual(HttpMethod.Delete, harness.CapturedRequest!.Method);
    }

    [TestMethod]
    public async Task DeleteItem_404_ParsesNotFoundBody()
    {
        using var harness = new TestHarness(HttpStatusCode.NotFound, """{"code":404,"message":"not found"}""");

        await using DeleteItemResponse response = await harness.Transport
            .SendAsync<DeleteItemRequest, DeleteItemResponse>(
                new DeleteItemRequest(JsonString.ParseValue("\"x\""u8)),
                CancellationToken.None);

        Assert.AreEqual(404, response.StatusCode);
        Assert.IsFalse(response.IsSuccess);
        Assert.IsTrue(response.TryGetNotFound(out var body));
        Assert.AreEqual(404, (int)body.Code);
        Assert.AreEqual("not found", (string)body.Message);
    }

    [TestMethod]
    public async Task DeleteItem_MatchResult_DispatchesToNotFound()
    {
        using var harness = new TestHarness(HttpStatusCode.NotFound, """{"code":404,"message":"gone"}""");

        await using DeleteItemResponse response = await harness.Transport
            .SendAsync<DeleteItemRequest, DeleteItemResponse>(
                new DeleteItemRequest(JsonString.ParseValue("\"x\""u8)),
                CancellationToken.None);

        string result = response.MatchResult(
            matchNotFound: static body => $"notfound:{(int)body.Code}",
            matchDefault: static code => $"default:{code}");

        Assert.AreEqual("notfound:404", result);
    }

    [TestMethod]
    public async Task DeleteItem_MatchResult_DispatchesToDefaultWithStatusCode()
    {
        using var harness = new TestHarness(HttpStatusCode.NoContent, string.Empty);

        await using DeleteItemResponse response = await harness.Transport
            .SendAsync<DeleteItemRequest, DeleteItemResponse>(
                new DeleteItemRequest(JsonString.ParseValue("\"x\""u8)),
                CancellationToken.None);

        string result = response.MatchResult(
            matchNotFound: static body => "notfound",
            matchDefault: static code => $"default:{code}");

        Assert.AreEqual("default:204", result);
    }

    [TestMethod]
    public async Task DeleteItem_MatchResultWithContext()
    {
        using var harness = new TestHarness(HttpStatusCode.NotFound, """{"code":404,"message":"gone"}""");

        await using DeleteItemResponse response = await harness.Transport
            .SendAsync<DeleteItemRequest, DeleteItemResponse>(
                new DeleteItemRequest(JsonString.ParseValue("\"x\""u8)),
                CancellationToken.None);

        string result = response.MatchResult(
            "ctx",
            matchNotFound: static (body, in ctx) => $"{ctx}:notfound:{(int)body.Code}",
            matchDefault: static (code, in ctx) => $"{ctx}:default:{code}");

        Assert.AreEqual("ctx:notfound:404", result);
    }

    [TestMethod]
    public async Task CreateItem_201_ParsesCreatedBody()
    {
        using var harness = new TestHarness(HttpStatusCode.Created, """{"id":"new-1","name":"Gadget"}""");

        using var bodyDoc = ParsedJsonDocument<PostItemsBody>.Parse("""{"name":"Gadget","price":12.5}""");
        PostItemsBody body = bodyDoc.RootElement;

        await using CreateItemResponse response = await harness.Transport
            .SendAsync<CreateItemRequest, PostItemsBody, CreateItemResponse>(
                default(CreateItemRequest), in body, CancellationToken.None);

        Assert.AreEqual(201, response.StatusCode);
        Assert.IsTrue(response.IsSuccess);
        Assert.IsTrue(response.TryGetCreated(out var created));
        Assert.AreEqual("new-1", (string)created.Id);
        Assert.AreEqual("Gadget", (string)created.Name);
    }

    [TestMethod]
    public async Task CreateItem_201_RequestBodyIsSentAsJson()
    {
        using var harness = new TestHarness(HttpStatusCode.Created, """{"id":"x","name":"y"}""");

        using var bodyDoc = ParsedJsonDocument<PostItemsBody>.Parse("""{"name":"Test","price":5.0}""");
        PostItemsBody body = bodyDoc.RootElement;

        await using CreateItemResponse response = await harness.Transport
            .SendAsync<CreateItemRequest, PostItemsBody, CreateItemResponse>(
                default(CreateItemRequest), in body, CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        Assert.AreEqual(HttpMethod.Post, harness.CapturedRequest.Method);
        Assert.AreEqual("application/json", harness.CapturedRequest.Content?.Headers.ContentType?.MediaType);

        byte[] sentBody = await harness.CapturedRequest.Content!.ReadAsByteArrayAsync();
        string sentJson = Encoding.UTF8.GetString(sentBody);
        Assert.IsTrue(sentJson.Contains("\"name\":\"Test\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task CreateItem_201_SchemaValidOnCreatedBody()
    {
        using var harness = new TestHarness(HttpStatusCode.Created, """{"id":"new-1","name":"Gadget"}""");

        using var bodyDoc = ParsedJsonDocument<PostItemsBody>.Parse("""{"name":"x"}""");
        PostItemsBody body = bodyDoc.RootElement;

        await using CreateItemResponse response = await harness.Transport
            .SendAsync<CreateItemRequest, PostItemsBody, CreateItemResponse>(
                default(CreateItemRequest), in body, CancellationToken.None);

        Assert.IsTrue(response.TryGetCreated(out var created));
        Assert.IsTrue(created.EvaluateSchema(), "Response with required id and name should be schema-valid");
    }

    [TestMethod]
    public async Task CreateItem_201_SchemaInvalidResponseBody_MissingRequiredField()
    {
        using var harness = new TestHarness(HttpStatusCode.Created, """{"name":"Gadget"}""");

        using var bodyDoc = ParsedJsonDocument<PostItemsBody>.Parse("""{"name":"x"}""");
        PostItemsBody body = bodyDoc.RootElement;

        await using CreateItemResponse response = await harness.Transport
            .SendAsync<CreateItemRequest, PostItemsBody, CreateItemResponse>(
                default(CreateItemRequest), in body, CancellationToken.None);

        Assert.IsTrue(response.TryGetCreated(out var created));
        Assert.IsFalse(created.EvaluateSchema(), "Response missing required 'id' should fail schema validation");
    }

    [TestMethod]
    public void CreateItem_RequestBodySchemaValidation()
    {
        using var invalidBody = ParsedJsonDocument<PostItemsBody>.Parse("""{"price":5.0}""");
        Assert.IsFalse(invalidBody.RootElement.EvaluateSchema(), "Request body missing required 'name' should fail validation");

        using var validBody = ParsedJsonDocument<PostItemsBody>.Parse("""{"name":"Widget"}""");
        Assert.IsTrue(validBody.RootElement.EvaluateSchema(), "Request body with required 'name' should pass validation");
    }

    [TestMethod]
    public async Task CreateItem_422_ParsesUnprocessableEntityBody()
    {
        using var harness = new TestHarness(
            HttpStatusCode.UnprocessableEntity, """{"errors":["name is required","price must be positive"]}""");

        using var bodyDoc = ParsedJsonDocument<PostItemsBody>.Parse("""{"name":"x"}""");
        PostItemsBody body = bodyDoc.RootElement;

        await using CreateItemResponse response = await harness.Transport
            .SendAsync<CreateItemRequest, PostItemsBody, CreateItemResponse>(
                default(CreateItemRequest), in body, CancellationToken.None);

        Assert.AreEqual(422, response.StatusCode);
        Assert.IsFalse(response.IsSuccess);
        Assert.IsTrue(response.TryGetUnprocessableEntity(out var errorBody));
        Assert.IsFalse(response.TryGetCreated(out _));
        Assert.IsTrue(errorBody.EvaluateSchema(), "422 response with errors array should be schema-valid");
    }

    [TestMethod]
    public async Task CreateItem_422_SchemaInvalid_MissingErrors()
    {
        using var harness = new TestHarness(
            HttpStatusCode.UnprocessableEntity, """{"detail":"something"}""");

        using var bodyDoc = ParsedJsonDocument<PostItemsBody>.Parse("""{"name":"x"}""");
        PostItemsBody body = bodyDoc.RootElement;

        await using CreateItemResponse response = await harness.Transport
            .SendAsync<CreateItemRequest, PostItemsBody, CreateItemResponse>(
                default(CreateItemRequest), in body, CancellationToken.None);

        Assert.IsTrue(response.TryGetUnprocessableEntity(out var errorBody));
        Assert.IsFalse(errorBody.EvaluateSchema(), "422 response missing required 'errors' should be schema-invalid");
    }

    [TestMethod]
    public async Task CreateItem_MatchResult_DispatchesToCreated()
    {
        using var harness = new TestHarness(HttpStatusCode.Created, """{"id":"new-1","name":"Gadget"}""");

        using var bodyDoc = ParsedJsonDocument<PostItemsBody>.Parse("""{"name":"x"}""");
        PostItemsBody body = bodyDoc.RootElement;

        await using CreateItemResponse response = await harness.Transport
            .SendAsync<CreateItemRequest, PostItemsBody, CreateItemResponse>(
                default(CreateItemRequest), in body, CancellationToken.None);

        string result = response.MatchResult(
            matchCreated: static body => $"created:{(string)body.Id}",
            matchUnprocessableEntity: static body => "validation-error",
            matchDefault: static code => $"other:{code}");

        Assert.AreEqual("created:new-1", result);
    }

    [TestMethod]
    public async Task CreateItem_MatchResult_DispatchesToUnprocessableEntity()
    {
        using var harness = new TestHarness(
            HttpStatusCode.UnprocessableEntity, """{"errors":["bad"]}""");

        using var bodyDoc = ParsedJsonDocument<PostItemsBody>.Parse("""{"name":"x"}""");
        PostItemsBody body = bodyDoc.RootElement;

        await using CreateItemResponse response = await harness.Transport
            .SendAsync<CreateItemRequest, PostItemsBody, CreateItemResponse>(
                default(CreateItemRequest), in body, CancellationToken.None);

        string result = response.MatchResult(
            matchCreated: static body => "created",
            matchUnprocessableEntity: static body => "unprocessable",
            matchDefault: static code => $"other:{code}");

        Assert.AreEqual("unprocessable", result);
    }

    [TestMethod]
    public async Task CreateItem_MatchResult_DispatchesToDefaultWithStatusCode()
    {
        using var harness = new TestHarness(HttpStatusCode.InternalServerError, """{}""");

        using var bodyDoc = ParsedJsonDocument<PostItemsBody>.Parse("""{"name":"x"}""");
        PostItemsBody body = bodyDoc.RootElement;

        await using CreateItemResponse response = await harness.Transport
            .SendAsync<CreateItemRequest, PostItemsBody, CreateItemResponse>(
                default(CreateItemRequest), in body, CancellationToken.None);

        string result = response.MatchResult(
            matchCreated: static body => "created",
            matchUnprocessableEntity: static body => "unprocessable",
            matchDefault: static code => $"default:{code}");

        Assert.AreEqual("default:500", result);
    }

    [TestMethod]
    public async Task CreateItem_MatchResultWithContext()
    {
        using var harness = new TestHarness(HttpStatusCode.Created, """{"id":"new-1","name":"Gadget"}""");

        using var bodyDoc = ParsedJsonDocument<PostItemsBody>.Parse("""{"name":"x"}""");
        PostItemsBody body = bodyDoc.RootElement;

        await using CreateItemResponse response = await harness.Transport
            .SendAsync<CreateItemRequest, PostItemsBody, CreateItemResponse>(
                default(CreateItemRequest), in body, CancellationToken.None);

        string result = response.MatchResult(
            "pfx",
            matchCreated: static (body, in ctx) => $"{ctx}:created:{(string)body.Id}",
            matchUnprocessableEntity: static (body, in ctx) => $"{ctx}:unprocessable",
            matchDefault: static (code, in ctx) => $"{ctx}:default:{code}");

        Assert.AreEqual("pfx:created:new-1", result);
    }

    [TestMethod]
    public async Task UpdateItem_200_ParsesOkBody()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"id":"up-1","name":"Updated"}""");

        using var bodyDoc = ParsedJsonDocument<PutItemsBody>.Parse("""{"id":"up-1","name":"Updated"}""");
        PutItemsBody body = bodyDoc.RootElement;

        await using UpdateItemResponse response = await harness.Transport
            .SendAsync<UpdateItemRequest, PutItemsBody, UpdateItemResponse>(
                default(UpdateItemRequest), in body, CancellationToken.None);

        Assert.AreEqual(200, response.StatusCode);
        Assert.IsTrue(response.IsSuccess);
        Assert.IsTrue(response.TryGetOk(out var ok));
        Assert.AreEqual("up-1", (string)ok.Id);
        Assert.AreEqual("Updated", (string)ok.Name);
    }

    [TestMethod]
    public async Task UpdateItem_UsesPutMethod()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"id":"x","name":"y"}""");

        using var bodyDoc = ParsedJsonDocument<PutItemsBody>.Parse("""{"id":"x","name":"y"}""");
        PutItemsBody body = bodyDoc.RootElement;

        await using UpdateItemResponse response = await harness.Transport
            .SendAsync<UpdateItemRequest, PutItemsBody, UpdateItemResponse>(
                default(UpdateItemRequest), in body, CancellationToken.None);

        Assert.AreEqual(HttpMethod.Put, harness.CapturedRequest!.Method);
    }

    [TestMethod]
    public async Task UpdateItem_200_SchemaValid()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"id":"x","name":"y"}""");

        using var bodyDoc = ParsedJsonDocument<PutItemsBody>.Parse("""{"id":"x","name":"y"}""");
        PutItemsBody body = bodyDoc.RootElement;

        await using UpdateItemResponse response = await harness.Transport
            .SendAsync<UpdateItemRequest, PutItemsBody, UpdateItemResponse>(
                default(UpdateItemRequest), in body, CancellationToken.None);

        Assert.IsTrue(response.TryGetOk(out var ok));
        Assert.IsTrue(ok.EvaluateSchema(), "PutItemsOk with id+name should be schema-valid");
    }

    [TestMethod]
    public async Task UpdateItem_MatchResult_DispatchesToOk()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"id":"x","name":"y"}""");

        using var bodyDoc = ParsedJsonDocument<PutItemsBody>.Parse("""{"id":"x","name":"y"}""");
        PutItemsBody body = bodyDoc.RootElement;

        await using UpdateItemResponse response = await harness.Transport
            .SendAsync<UpdateItemRequest, PutItemsBody, UpdateItemResponse>(
                default(UpdateItemRequest), in body, CancellationToken.None);

        string result = response.MatchResult(
            matchOk: static body => $"ok:{(string)body.Name}",
            matchDefault: static code => $"default:{code}");

        Assert.AreEqual("ok:y", result);
    }

    [TestMethod]
    public async Task UpdateItem_MatchResult_DispatchesToDefaultWithStatusCode()
    {
        using var harness = new TestHarness(HttpStatusCode.BadRequest, """{}""");

        using var bodyDoc = ParsedJsonDocument<PutItemsBody>.Parse("""{"id":"x","name":"y"}""");
        PutItemsBody body = bodyDoc.RootElement;

        await using UpdateItemResponse response = await harness.Transport
            .SendAsync<UpdateItemRequest, PutItemsBody, UpdateItemResponse>(
                default(UpdateItemRequest), in body, CancellationToken.None);

        string result = response.MatchResult(
            matchOk: static body => "ok",
            matchDefault: static code => $"default:{code}");

        Assert.AreEqual("default:400", result);
    }

    [TestMethod]
    public async Task GetByFlag_BooleanPathParam_True()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new GetByFlagRequest(
            JsonBoolean.ParseValue("true"u8),
            JsonString.ParseValue("\"trace-1\""u8),
            JsonInt32.ParseValue("5"u8));

        await using GetByFlagResponse response = await harness.Transport
            .SendAsync<GetByFlagRequest, GetByFlagResponse>(in request, CancellationToken.None);

        Assert.AreEqual("http://localhost/flags/true", harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task GetByFlag_BooleanPathParam_False()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new GetByFlagRequest(
            JsonBoolean.ParseValue("false"u8),
            JsonString.ParseValue("\"trace-2\""u8),
            JsonInt32.ParseValue("1"u8));

        await using GetByFlagResponse response = await harness.Transport
            .SendAsync<GetByFlagRequest, GetByFlagResponse>(in request, CancellationToken.None);

        Assert.AreEqual("http://localhost/flags/false", harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task GetByFlag_RequiredAndOptionalHeaders()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new GetByFlagRequest(
            JsonBoolean.ParseValue("true"u8),
            JsonString.ParseValue("\"trace-abc\""u8),
            JsonInt32.ParseValue("42"u8))
        {
            XDebug = JsonBoolean.ParseValue("true"u8),
            XScore = JsonDouble.ParseValue("3.14"u8),
        };

        await using GetByFlagResponse response = await harness.Transport
            .SendAsync<GetByFlagRequest, GetByFlagResponse>(in request, CancellationToken.None);

        var headers = harness.CapturedRequest!.Headers;
        Assert.AreEqual("trace-abc", headers.GetValues("X-Trace-Id").First());
        Assert.AreEqual("42", headers.GetValues("X-Request-Count").First());
        Assert.AreEqual("true", headers.GetValues("X-Debug").First());
        Assert.AreEqual("3.14", headers.GetValues("X-Score").First());
    }

    [TestMethod]
    public async Task GetByFlag_OptionalHeadersOmitted()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new GetByFlagRequest(
            JsonBoolean.ParseValue("true"u8),
            JsonString.ParseValue("\"trace\""u8),
            JsonInt32.ParseValue("1"u8));

        await using GetByFlagResponse response = await harness.Transport
            .SendAsync<GetByFlagRequest, GetByFlagResponse>(in request, CancellationToken.None);

        var headers = harness.CapturedRequest!.Headers;
        Assert.IsTrue(headers.Contains("X-Trace-Id"));
        Assert.IsTrue(headers.Contains("X-Request-Count"));
        Assert.IsFalse(headers.Contains("X-Debug"));
        Assert.IsFalse(headers.Contains("X-Score"));
    }

    [TestMethod]
    public async Task GetByFlag_MatchResult_DispatchesToOk()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"active":true}""");

        var request = new GetByFlagRequest(
            JsonBoolean.ParseValue("true"u8),
            JsonString.ParseValue("\"t\""u8),
            JsonInt32.ParseValue("1"u8));

        await using GetByFlagResponse response = await harness.Transport
            .SendAsync<GetByFlagRequest, GetByFlagResponse>(in request, CancellationToken.None);

        string result = response.MatchResult(
            matchOk: static body => "ok",
            matchDefault: static code => $"default:{code}");

        Assert.AreEqual("ok", result);
    }

    [TestMethod]
    public async Task GetByFlag_MatchResult_DispatchesToDefaultWithStatusCode()
    {
        using var harness = new TestHarness(HttpStatusCode.ServiceUnavailable, """{}""");

        var request = new GetByFlagRequest(
            JsonBoolean.ParseValue("true"u8),
            JsonString.ParseValue("\"t\""u8),
            JsonInt32.ParseValue("1"u8));

        await using GetByFlagResponse response = await harness.Transport
            .SendAsync<GetByFlagRequest, GetByFlagResponse>(in request, CancellationToken.None);

        string result = response.MatchResult(
            matchOk: static body => "ok",
            matchDefault: static code => $"default:{code}");

        Assert.AreEqual("default:503", result);
    }

    [TestMethod]
    public async Task GetSessionProfile_CookieParamsAreSent()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new GetSessionProfileRequest(JsonString.ParseValue("\"sess-123\""u8))
        {
            Theme = JsonString.ParseValue("\"dark\""u8),
            Debug = JsonBoolean.ParseValue("true"u8),
            MaxAge = JsonInt32.ParseValue("3600"u8),
        };

        await using GetSessionProfileResponse response = await harness.Transport
            .SendAsync<GetSessionProfileRequest, GetSessionProfileResponse>(in request, CancellationToken.None);

        Assert.IsTrue(harness.CapturedRequest!.Headers.Contains("Cookie"));
        Assert.AreEqual(
            "session_id=sess-123; theme=dark; debug=true; max_age=3600",
            harness.CapturedRequest.Headers.GetValues("Cookie").First());
    }

    [TestMethod]
    public async Task GetSessionProfile_OnlyRequiredCookie()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new GetSessionProfileRequest(JsonString.ParseValue("\"sess-456\""u8));

        await using GetSessionProfileResponse response = await harness.Transport
            .SendAsync<GetSessionProfileRequest, GetSessionProfileResponse>(in request, CancellationToken.None);

        Assert.IsTrue(harness.CapturedRequest!.Headers.Contains("Cookie"));
        Assert.AreEqual(
            "session_id=sess-456",
            harness.CapturedRequest.Headers.GetValues("Cookie").First());
    }

    [TestMethod]
    public async Task GetSessionProfile_MatchResult_DispatchesToOk()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"user":"alice"}""");

        var request = new GetSessionProfileRequest(JsonString.ParseValue("\"sess\""u8));

        await using GetSessionProfileResponse response = await harness.Transport
            .SendAsync<GetSessionProfileRequest, GetSessionProfileResponse>(in request, CancellationToken.None);

        string result = response.MatchResult(
            matchOk: static body => "ok",
            matchDefault: static code => $"default:{code}");

        Assert.AreEqual("ok", result);
    }

    [TestMethod]
    public async Task GetOrder_UuidPathParam()
    {
        using var harness = new TestHarness(
            HttpStatusCode.OK,
            """{"orderId":"550e8400-e29b-41d4-a716-446655440000","status":"shipped","total":42.5}""");

        var request = new GetOrderRequest(
            JsonUuid.ParseValue("\"550e8400-e29b-41d4-a716-446655440000\""u8))
        {
            Fields = JsonString.ParseValue("\"status,total\""u8),
            XTraceId = JsonString.ParseValue("\"trace-99\""u8),
        };

        await using GetOrderResponse response = await harness.Transport
            .SendAsync<GetOrderRequest, GetOrderResponse>(in request, CancellationToken.None);

        Assert.AreEqual(
            "http://localhost/orders/550e8400-e29b-41d4-a716-446655440000?fields=status%2Ctotal",
            harness.CapturedRequest!.RequestUri!.OriginalString);

        Assert.AreEqual(200, response.StatusCode);
        Assert.IsTrue(response.TryGetOk(out var body));
        Assert.AreEqual("shipped", (string)body.Status);
        Assert.AreEqual("550e8400-e29b-41d4-a716-446655440000", (string)body.OrderId);
    }

    [TestMethod]
    public async Task GetOrder_ResponseSchemaValid()
    {
        using var harness = new TestHarness(
            HttpStatusCode.OK,
            """{"orderId":"550e8400-e29b-41d4-a716-446655440000","status":"pending","total":99.99}""");

        await using GetOrderResponse response = await harness.Transport
            .SendAsync<GetOrderRequest, GetOrderResponse>(
                new GetOrderRequest(JsonUuid.ParseValue("\"550e8400-e29b-41d4-a716-446655440000\""u8)),
                CancellationToken.None);

        Assert.IsTrue(response.TryGetOk(out var body));
        Assert.IsTrue(body.EvaluateSchema(), "Order response with all required fields should be schema-valid");
    }

    [TestMethod]
    public async Task GetOrder_ResponseSchemaInvalid_MissingRequired()
    {
        using var harness = new TestHarness(
            HttpStatusCode.OK,
            """{"orderId":"550e8400-e29b-41d4-a716-446655440000","status":"pending"}""");

        await using GetOrderResponse response = await harness.Transport
            .SendAsync<GetOrderRequest, GetOrderResponse>(
                new GetOrderRequest(JsonUuid.ParseValue("\"550e8400-e29b-41d4-a716-446655440000\""u8)),
                CancellationToken.None);

        Assert.IsTrue(response.TryGetOk(out var body));
        Assert.IsFalse(body.EvaluateSchema(), "Order response missing required 'total' should fail schema validation");
    }

    [TestMethod]
    public async Task GetOrder_MatchResult_DispatchesToOk()
    {
        using var harness = new TestHarness(
            HttpStatusCode.OK,
            """{"orderId":"550e8400-e29b-41d4-a716-446655440000","status":"shipped","total":42.5}""");

        await using GetOrderResponse response = await harness.Transport
            .SendAsync<GetOrderRequest, GetOrderResponse>(
                new GetOrderRequest(JsonUuid.ParseValue("\"550e8400-e29b-41d4-a716-446655440000\""u8)),
                CancellationToken.None);

        string result = response.MatchResult(
            matchOk: static body => $"ok:{(string)body.Status}",
            matchDefault: static code => $"default:{code}");

        Assert.AreEqual("ok:shipped", result);
    }

    [TestMethod]
    public async Task GetOrder_MatchResult_DispatchesToDefaultWithStatusCode()
    {
        using var harness = new TestHarness(HttpStatusCode.NotFound, """{}""");

        await using GetOrderResponse response = await harness.Transport
            .SendAsync<GetOrderRequest, GetOrderResponse>(
                new GetOrderRequest(JsonUuid.ParseValue("\"550e8400-e29b-41d4-a716-446655440000\""u8)),
                CancellationToken.None);

        string result = response.MatchResult(
            matchOk: static body => "ok",
            matchDefault: static code => $"default:{code}");

        Assert.AreEqual("default:404", result);
    }

    [TestMethod]
    public async Task GetOrder_XTraceIdHeaderForwarded()
    {
        using var harness = new TestHarness(
            HttpStatusCode.OK,
            """{"orderId":"550e8400-e29b-41d4-a716-446655440000","status":"s","total":1}""");

        var request = new GetOrderRequest(
            JsonUuid.ParseValue("\"550e8400-e29b-41d4-a716-446655440000\""u8))
        {
            XTraceId = JsonString.ParseValue("\"trace-header\""u8),
        };

        await using GetOrderResponse response = await harness.Transport
            .SendAsync<GetOrderRequest, GetOrderResponse>(in request, CancellationToken.None);

        Assert.IsTrue(harness.CapturedRequest!.Headers.Contains("X-Trace-Id"));
        Assert.AreEqual("trace-header", harness.CapturedRequest.Headers.GetValues("X-Trace-Id").First());
    }

    [TestMethod]
    public async Task Search_RequiredQueryParam()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new SearchRequest(JsonString.ParseValue("\"widgets\""u8));

        await using SearchResponse response = await harness.Transport
            .SendAsync<SearchRequest, SearchResponse>(in request, CancellationToken.None);

        Assert.AreEqual("http://localhost/search?q=widgets", harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task Search_AllQueryParams()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new SearchRequest(JsonString.ParseValue("\"test\""u8))
        {
            Page = JsonInt32.ParseValue("3"u8),
            Rating = JsonSingle.ParseValue("4.5"u8),
        };

        await using SearchResponse response = await harness.Transport
            .SendAsync<SearchRequest, SearchResponse>(in request, CancellationToken.None);

        Assert.AreEqual("http://localhost/search?q=test&page=3&rating=4.5", harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task Search_MatchResult_DispatchesToOk()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"results":[]}""");

        await using SearchResponse response = await harness.Transport
            .SendAsync<SearchRequest, SearchResponse>(
                new SearchRequest(JsonString.ParseValue("\"q\""u8)),
                CancellationToken.None);

        string result = response.MatchResult(
            matchOk: static body => "ok",
            matchDefault: static code => $"default:{code}");

        Assert.AreEqual("ok", result);
    }

    [TestMethod]
    public async Task Search_MatchResult_DispatchesToDefaultWithStatusCode()
    {
        using var harness = new TestHarness(HttpStatusCode.BadGateway, """{}""");

        await using SearchResponse response = await harness.Transport
            .SendAsync<SearchRequest, SearchResponse>(
                new SearchRequest(JsonString.ParseValue("\"q\""u8)),
                CancellationToken.None);

        string result = response.MatchResult(
            matchOk: static body => "ok",
            matchDefault: static code => $"default:{code}");

        Assert.AreEqual("default:502", result);
    }

    [TestMethod]
    public async Task GetPage_Int32PathParam()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new GetPageRequest(JsonInt32.ParseValue("42"u8))
        {
            Offset = JsonInteger.ParseValue("100"u8),
        };

        await using GetPageResponse response = await harness.Transport
            .SendAsync<GetPageRequest, GetPageResponse>(in request, CancellationToken.None);

        Assert.AreEqual("http://localhost/pages/42?offset=100", harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task GetPage_MatchResult_DispatchesToOk()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"pageNum":1}""");

        await using GetPageResponse response = await harness.Transport
            .SendAsync<GetPageRequest, GetPageResponse>(
                new GetPageRequest(JsonInt32.ParseValue("1"u8)),
                CancellationToken.None);

        string result = response.MatchResult(
            matchOk: static body => "ok",
            matchDefault: static code => $"default:{code}");

        Assert.AreEqual("ok", result);
    }

    [TestMethod]
    public async Task GetItemTag_MultiplePathParams()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new GetItemTagRequest(
            JsonInt64.ParseValue("99"u8),
            JsonString.ParseValue("\"my tag\""u8))
        {
            Score = JsonDouble.ParseValue("7.5"u8),
        };

        await using GetItemTagResponse response = await harness.Transport
            .SendAsync<GetItemTagRequest, GetItemTagResponse>(in request, CancellationToken.None);

        Assert.AreEqual("http://localhost/items/99/tags/my%20tag?score=7.5", harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task GetItemTag_200_ParsesJsonObjectBody()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"tag":"important","weight":5}""");

        await using GetItemTagResponse response = await harness.Transport
            .SendAsync<GetItemTagRequest, GetItemTagResponse>(
                new GetItemTagRequest(JsonInt64.ParseValue("1"u8), JsonString.ParseValue("\"t\""u8)),
                CancellationToken.None);

        Assert.AreEqual(200, response.StatusCode);
        Assert.IsTrue(response.IsSuccess);
        Assert.IsTrue(response.TryGetOk(out var body));
        Assert.AreEqual(JsonValueKind.Object, body.ValueKind);
    }

    [TestMethod]
    public async Task GetItemTag_MatchResult_DispatchesToOk()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"tag":"v"}""");

        await using GetItemTagResponse response = await harness.Transport
            .SendAsync<GetItemTagRequest, GetItemTagResponse>(
                new GetItemTagRequest(JsonInt64.ParseValue("1"u8), JsonString.ParseValue("\"t\""u8)),
                CancellationToken.None);

        string result = response.MatchResult(
            matchOk: static body => "ok",
            matchDefault: static code => $"default:{code}");

        Assert.AreEqual("ok", result);
    }

    [TestMethod]
    public async Task GetItemTag_MatchResult_DispatchesToDefaultWithStatusCode()
    {
        using var harness = new TestHarness(HttpStatusCode.Forbidden, """{}""");

        await using GetItemTagResponse response = await harness.Transport
            .SendAsync<GetItemTagRequest, GetItemTagResponse>(
                new GetItemTagRequest(JsonInt64.ParseValue("1"u8), JsonString.ParseValue("\"t\""u8)),
                CancellationToken.None);

        string result = response.MatchResult(
            matchOk: static body => "ok",
            matchDefault: static code => $"default:{code}");

        Assert.AreEqual("default:403", result);
    }

    // ── Response header lazy parsing tests ──────────────────────────────
    [TestMethod]
    public async Task GetItemTag_ResponseHeader_IntegerParsedLazily()
    {
        using var harness = new TestHarness(
            HttpStatusCode.OK,
            """{"tag":"v"}""",
            new Dictionary<string, string> { ["X-Total-Count"] = "42" });

        await using GetItemTagResponse response = await harness.Transport
            .SendAsync<GetItemTagRequest, GetItemTagResponse>(
                new GetItemTagRequest(JsonInt64.ParseValue("1"u8), JsonString.ParseValue("\"t\""u8)),
                CancellationToken.None);

        // Accessing XTotalCountHeader should lazily parse the integer value.
        Assert.IsNotNull(response.XTotalCountHeader);
        Assert.AreEqual(42, (int)response.XTotalCountHeader.Value);
    }

    [TestMethod]
    public async Task GetItemTag_ResponseHeader_StringParsedLazily()
    {
        using var harness = new TestHarness(
            HttpStatusCode.OK,
            """{"tag":"v"}""",
            new Dictionary<string, string> { ["X-Request-Id"] = "req-abc-123" });

        await using GetItemTagResponse response = await harness.Transport
            .SendAsync<GetItemTagRequest, GetItemTagResponse>(
                new GetItemTagRequest(JsonInt64.ParseValue("1"u8), JsonString.ParseValue("\"t\""u8)),
                CancellationToken.None);

        Assert.IsNotNull(response.XRequestIdHeader);
        Assert.AreEqual("req-abc-123", (string)response.XRequestIdHeader.Value);
    }

    [TestMethod]
    public async Task GetItemTag_ResponseHeader_MissingHeaderReturnsNull()
    {
        // No response headers set at all.
        using var harness = new TestHarness(HttpStatusCode.OK, """{"tag":"v"}""");

        await using GetItemTagResponse response = await harness.Transport
            .SendAsync<GetItemTagRequest, GetItemTagResponse>(
                new GetItemTagRequest(JsonInt64.ParseValue("1"u8), JsonString.ParseValue("\"t\""u8)),
                CancellationToken.None);

        Assert.IsNull(response.XTotalCountHeader);
        Assert.IsNull(response.XRequestIdHeader);
    }

    [TestMethod]
    public async Task GetItemTag_ResponseHeader_CachedOnSecondAccess()
    {
        using var harness = new TestHarness(
            HttpStatusCode.OK,
            """{"tag":"v"}""",
            new Dictionary<string, string> { ["X-Total-Count"] = "99" });

        await using GetItemTagResponse response = await harness.Transport
            .SendAsync<GetItemTagRequest, GetItemTagResponse>(
                new GetItemTagRequest(JsonInt64.ParseValue("1"u8), JsonString.ParseValue("\"t\""u8)),
                CancellationToken.None);

        // First access parses, second access returns cached value.
        var first = response.XTotalCountHeader;
        var second = response.XTotalCountHeader;

        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
        Assert.AreEqual((int)first.Value, (int)second.Value);
    }

    [TestMethod]
    public async Task GetItemTag_ResponseHeader_BothHeadersPresent()
    {
        using var harness = new TestHarness(
            HttpStatusCode.OK,
            """{"tag":"v"}""",
            new Dictionary<string, string>
            {
                ["X-Total-Count"] = "7",
                ["X-Request-Id"] = "id-xyz",
            });

        await using GetItemTagResponse response = await harness.Transport
            .SendAsync<GetItemTagRequest, GetItemTagResponse>(
                new GetItemTagRequest(JsonInt64.ParseValue("1"u8), JsonString.ParseValue("\"t\""u8)),
                CancellationToken.None);

        Assert.AreEqual(7, (int)response.XTotalCountHeader!.Value);
        Assert.AreEqual("id-xyz", (string)response.XRequestIdHeader!.Value);
    }

    [TestMethod]
    public async Task GetItemTag_ResponseHeader_NonMatchingStatusStillParsesHeaders()
    {
        // Even for a 404 (no body parsed), headers should still be available.
        using var harness = new TestHarness(
            HttpStatusCode.NotFound,
            string.Empty,
            new Dictionary<string, string> { ["X-Request-Id"] = "err-404" });

        await using GetItemTagResponse response = await harness.Transport
            .SendAsync<GetItemTagRequest, GetItemTagResponse>(
                new GetItemTagRequest(JsonInt64.ParseValue("1"u8), JsonString.ParseValue("\"t\""u8)),
                CancellationToken.None);

        Assert.AreEqual(404, response.StatusCode);
        Assert.IsFalse(response.IsSuccess);
        Assert.AreEqual("err-404", (string)response.XRequestIdHeader!.Value);
    }

    [TestMethod]
    public async Task GetItemDetails_PathWithTrailingLiteral()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"detail":"some detail"}""");

        await using GetItemDetailsResponse response = await harness.Transport
            .SendAsync<GetItemDetailsRequest, GetItemDetailsResponse>(
                new GetItemDetailsRequest(JsonString.ParseValue("\"abc\""u8)),
                CancellationToken.None);

        Assert.AreEqual("http://localhost/items/abc/details", harness.CapturedRequest!.RequestUri!.OriginalString);
        Assert.AreEqual(200, response.StatusCode);
        Assert.IsTrue(response.IsSuccess);
        Assert.IsTrue(response.TryGetOk(out var body));
        Assert.AreEqual(JsonValueKind.Object, body.ValueKind);
    }

    [TestMethod]
    public async Task GetItemDetails_MatchResult_DispatchesToOk()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"x":1}""");

        await using GetItemDetailsResponse response = await harness.Transport
            .SendAsync<GetItemDetailsRequest, GetItemDetailsResponse>(
                new GetItemDetailsRequest(JsonString.ParseValue("\"abc\""u8)),
                CancellationToken.None);

        string result = response.MatchResult(
            matchOk: static body => "ok",
            matchDefault: static code => $"default:{code}");

        Assert.AreEqual("ok", result);
    }

    [TestMethod]
    public async Task GetItemDetails_MatchResult_DispatchesToDefaultWithStatusCode()
    {
        using var harness = new TestHarness(HttpStatusCode.Gone, """{}""");

        await using GetItemDetailsResponse response = await harness.Transport
            .SendAsync<GetItemDetailsRequest, GetItemDetailsResponse>(
                new GetItemDetailsRequest(JsonString.ParseValue("\"abc\""u8)),
                CancellationToken.None);

        string result = response.MatchResult(
            matchOk: static body => "ok",
            matchDefault: static code => $"default:{code}");

        Assert.AreEqual("default:410", result);
    }

    [TestMethod]
    public async Task GetItemDetails_MatchResultWithContext()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"detail":"v"}""");

        await using GetItemDetailsResponse response = await harness.Transport
            .SendAsync<GetItemDetailsRequest, GetItemDetailsResponse>(
                new GetItemDetailsRequest(JsonString.ParseValue("\"abc\""u8)),
                CancellationToken.None);

        string result = response.MatchResult(
            "ctx",
            matchOk: static (body, in ctx) => $"{ctx}:ok",
            matchDefault: static (code, in ctx) => $"{ctx}:default:{code}");

        Assert.AreEqual("ctx:ok", result);
    }

    [TestMethod]
    public async Task UpdateOrder_InheritedPathParam_UuidInPath()
    {
        using var harness = new TestHarness(
            HttpStatusCode.OK,
            """{"orderId":"550e8400-e29b-41d4-a716-446655440000","status":"confirmed","total":100}""");

        using var bodyDoc = ParsedJsonDocument<PutOrdersByOrderIdBody>.Parse("""{"status":"confirmed"}""");
        var body = bodyDoc.RootElement;

        var request = new UpdateOrderRequest(
            JsonUuid.ParseValue("\"550e8400-e29b-41d4-a716-446655440000\""u8),
            JsonUuid.ParseValue("\"11111111-2222-3333-4444-555555555555\""u8));

        await using UpdateOrderResponse response = await harness.Transport
            .SendAsync<UpdateOrderRequest, PutOrdersByOrderIdBody, UpdateOrderResponse>(
                in request, in body, CancellationToken.None);

        Assert.AreEqual(HttpMethod.Put, harness.CapturedRequest!.Method);
        Assert.AreEqual(
            "http://localhost/orders/550e8400-e29b-41d4-a716-446655440000",
            harness.CapturedRequest.RequestUri!.OriginalString);

        Assert.IsTrue(harness.CapturedRequest.Headers.Contains("X-Trace-Id"));
        Assert.AreEqual(
            "11111111-2222-3333-4444-555555555555",
            harness.CapturedRequest.Headers.GetValues("X-Trace-Id").First());
    }

    [TestMethod]
    public async Task UpdateOrder_200_ParsesOkBody()
    {
        using var harness = new TestHarness(
            HttpStatusCode.OK,
            """{"orderId":"550e8400-e29b-41d4-a716-446655440000","status":"confirmed","total":100}""");

        using var bodyDoc = ParsedJsonDocument<PutOrdersByOrderIdBody>.Parse("""{"status":"confirmed"}""");
        var body = bodyDoc.RootElement;

        var request = new UpdateOrderRequest(
            JsonUuid.ParseValue("\"550e8400-e29b-41d4-a716-446655440000\""u8),
            JsonUuid.ParseValue("\"11111111-2222-3333-4444-555555555555\""u8));

        await using UpdateOrderResponse response = await harness.Transport
            .SendAsync<UpdateOrderRequest, PutOrdersByOrderIdBody, UpdateOrderResponse>(
                in request, in body, CancellationToken.None);

        Assert.IsTrue(response.TryGetOk(out var ok));
        Assert.AreEqual("confirmed", (string)ok.Status);
        Assert.AreEqual("550e8400-e29b-41d4-a716-446655440000", (string)ok.OrderId);
        Assert.IsTrue(ok.EvaluateSchema());
    }

    [TestMethod]
    public async Task UpdateOrder_MatchResult_DispatchesToOk()
    {
        using var harness = new TestHarness(
            HttpStatusCode.OK,
            """{"orderId":"550e8400-e29b-41d4-a716-446655440000","status":"ok","total":1}""");

        using var bodyDoc = ParsedJsonDocument<PutOrdersByOrderIdBody>.Parse("""{"status":"ok"}""");
        var body = bodyDoc.RootElement;

        var request = new UpdateOrderRequest(
            JsonUuid.ParseValue("\"550e8400-e29b-41d4-a716-446655440000\""u8),
            JsonUuid.ParseValue("\"11111111-2222-3333-4444-555555555555\""u8));

        await using UpdateOrderResponse response = await harness.Transport
            .SendAsync<UpdateOrderRequest, PutOrdersByOrderIdBody, UpdateOrderResponse>(
                in request, in body, CancellationToken.None);

        string result = response.MatchResult(
            matchOk: static body => $"ok:{(string)body.Status}",
            matchDefault: static code => $"default:{code}");

        Assert.AreEqual("ok:ok", result);
    }

    [TestMethod]
    public async Task Response_DisposeAsync_IsIdempotent()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"id":"x","name":"y","price":1}""");

        GetItemResponse response = await harness.Transport
            .SendAsync<GetItemRequest, GetItemResponse>(
                new GetItemRequest(JsonString.ParseValue("\"x\""u8)),
                CancellationToken.None);

        await response.DisposeAsync();
        await response.DisposeAsync();
    }

    [TestMethod]
    public void PutItemsBody_SchemaValidation_RequiredFields()
    {
        using var validDoc = ParsedJsonDocument<PutItemsBody>.Parse("""{"id":"x","name":"y"}""");
        Assert.IsTrue(validDoc.RootElement.EvaluateSchema(), "PutItemsBody with id+name should be valid");

        using var missingId = ParsedJsonDocument<PutItemsBody>.Parse("""{"name":"y"}""");
        Assert.IsFalse(missingId.RootElement.EvaluateSchema(), "PutItemsBody missing 'id' should be invalid");

        using var missingName = ParsedJsonDocument<PutItemsBody>.Parse("""{"id":"x"}""");
        Assert.IsFalse(missingName.RootElement.EvaluateSchema(), "PutItemsBody missing 'name' should be invalid");
    }

    [TestMethod]
    public void PostItemsBody_SchemaValidation_RequiredFields()
    {
        using var validDoc = ParsedJsonDocument<PostItemsBody>.Parse("""{"name":"Widget"}""");
        Assert.IsTrue(validDoc.RootElement.EvaluateSchema());

        using var withPrice = ParsedJsonDocument<PostItemsBody>.Parse("""{"name":"Widget","price":9.99}""");
        Assert.IsTrue(withPrice.RootElement.EvaluateSchema());

        using var missingName = ParsedJsonDocument<PostItemsBody>.Parse("""{"price":5.0}""");
        Assert.IsFalse(missingName.RootElement.EvaluateSchema());
    }

    [TestMethod]
    public void PutOrdersByOrderIdBody_SchemaValidation_RequiredFields()
    {
        using var validDoc = ParsedJsonDocument<PutOrdersByOrderIdBody>.Parse("""{"status":"shipped"}""");
        Assert.IsTrue(validDoc.RootElement.EvaluateSchema());

        using var withTotal = ParsedJsonDocument<PutOrdersByOrderIdBody>.Parse("""{"status":"shipped","total":42}""");
        Assert.IsTrue(withTotal.RootElement.EvaluateSchema());

        using var missingStatus = ParsedJsonDocument<PutOrdersByOrderIdBody>.Parse("""{"total":42}""");
        Assert.IsFalse(missingStatus.RootElement.EvaluateSchema());
    }

    [TestMethod]
    public void GetItemsByItemIdNotFound_SchemaValidation()
    {
        using var valid = ParsedJsonDocument<GetItemsByItemIdNotFound>.Parse("""{"code":404,"message":"not found"}""");
        Assert.IsTrue(valid.RootElement.EvaluateSchema());

        using var missingCode = ParsedJsonDocument<GetItemsByItemIdNotFound>.Parse("""{"message":"x"}""");
        Assert.IsFalse(missingCode.RootElement.EvaluateSchema());

        using var missingMessage = ParsedJsonDocument<GetItemsByItemIdNotFound>.Parse("""{"code":404}""");
        Assert.IsFalse(missingMessage.RootElement.EvaluateSchema());
    }

    [TestMethod]
    public void GetItemsByItemIdDefault_SchemaValidation()
    {
        using var valid = ParsedJsonDocument<GetItemsByItemIdDefault>.Parse("""{"error":"something went wrong"}""");
        Assert.IsTrue(valid.RootElement.EvaluateSchema());

        using var missingError = ParsedJsonDocument<GetItemsByItemIdDefault>.Parse("""{"detail":"x"}""");
        Assert.IsFalse(missingError.RootElement.EvaluateSchema());
    }

    [TestMethod]
    public void PostItemsCreated_SchemaValidation()
    {
        using var valid = ParsedJsonDocument<PostItemsCreated>.Parse("""{"id":"x","name":"y"}""");
        Assert.IsTrue(valid.RootElement.EvaluateSchema());

        using var missingId = ParsedJsonDocument<PostItemsCreated>.Parse("""{"name":"y"}""");
        Assert.IsFalse(missingId.RootElement.EvaluateSchema());

        using var missingName = ParsedJsonDocument<PostItemsCreated>.Parse("""{"id":"x"}""");
        Assert.IsFalse(missingName.RootElement.EvaluateSchema());
    }

    [TestMethod]
    public void PostItemsUnprocessableEntity_SchemaValidation()
    {
        using var valid = ParsedJsonDocument<PostItemsUnprocessableEntity>.Parse("""{"errors":["bad"]}""");
        Assert.IsTrue(valid.RootElement.EvaluateSchema());

        using var missingErrors = ParsedJsonDocument<PostItemsUnprocessableEntity>.Parse("""{"detail":"x"}""");
        Assert.IsFalse(missingErrors.RootElement.EvaluateSchema());
    }

    [TestMethod]
    public void PutItemsOk_SchemaValidation()
    {
        using var valid = ParsedJsonDocument<PutItemsOk>.Parse("""{"id":"x","name":"y"}""");
        Assert.IsTrue(valid.RootElement.EvaluateSchema());

        using var missingId = ParsedJsonDocument<PutItemsOk>.Parse("""{"name":"y"}""");
        Assert.IsFalse(missingId.RootElement.EvaluateSchema());

        using var missingName = ParsedJsonDocument<PutItemsOk>.Parse("""{"id":"x"}""");
        Assert.IsFalse(missingName.RootElement.EvaluateSchema());
    }

    [TestMethod]
    public void GetOrdersByOrderIdOk_SchemaValidation()
    {
        using var valid = ParsedJsonDocument<GetOrdersByOrderIdOk>.Parse(
            """{"orderId":"550e8400-e29b-41d4-a716-446655440000","status":"shipped","total":42.5}""");
        Assert.IsTrue(valid.RootElement.EvaluateSchema());

        using var missingOrderId = ParsedJsonDocument<GetOrdersByOrderIdOk>.Parse(
            """{"status":"shipped","total":42.5}""");
        Assert.IsFalse(missingOrderId.RootElement.EvaluateSchema());

        using var missingStatus = ParsedJsonDocument<GetOrdersByOrderIdOk>.Parse(
            """{"orderId":"550e8400-e29b-41d4-a716-446655440000","total":42.5}""");
        Assert.IsFalse(missingStatus.RootElement.EvaluateSchema());

        using var missingTotal = ParsedJsonDocument<GetOrdersByOrderIdOk>.Parse(
            """{"orderId":"550e8400-e29b-41d4-a716-446655440000","status":"shipped"}""");
        Assert.IsFalse(missingTotal.RootElement.EvaluateSchema());
    }

    [TestMethod]
    public void PutOrdersByOrderIdOk_SchemaValidation()
    {
        using var valid = ParsedJsonDocument<PutOrdersByOrderIdOk>.Parse(
            """{"orderId":"550e8400-e29b-41d4-a716-446655440000","status":"confirmed","total":100}""");
        Assert.IsTrue(valid.RootElement.EvaluateSchema());

        using var missingOrderId = ParsedJsonDocument<PutOrdersByOrderIdOk>.Parse(
            """{"status":"confirmed","total":100}""");
        Assert.IsFalse(missingOrderId.RootElement.EvaluateSchema());
    }

    [TestMethod]
    public void DeleteItemsByItemIdNotFound_SchemaValidation()
    {
        using var valid = ParsedJsonDocument<DeleteItemsByItemIdNotFound>.Parse("""{"code":404,"message":"gone"}""");
        Assert.IsTrue(valid.RootElement.EvaluateSchema());

        using var missingCode = ParsedJsonDocument<DeleteItemsByItemIdNotFound>.Parse("""{"message":"x"}""");
        Assert.IsFalse(missingCode.RootElement.EvaluateSchema());
    }

    [TestMethod]
    public void GetItemRequest_StaticMembers()
    {
        Assert.AreEqual(OperationMethod.Get, GetItemRequest.Method);
        Assert.IsTrue(GetItemRequest.HasPathParameters);
        Assert.IsTrue(GetItemRequest.HasQueryParameters);
        Assert.IsTrue(GetItemRequest.HasHeaderParameters);
        Assert.IsFalse(GetItemRequest.HasCookieParameters);
    }

    [TestMethod]
    public void CreateItemRequest_StaticMembers()
    {
        Assert.AreEqual(OperationMethod.Post, CreateItemRequest.Method);
        Assert.IsFalse(CreateItemRequest.HasPathParameters);
        Assert.IsFalse(CreateItemRequest.HasQueryParameters);
        Assert.IsFalse(CreateItemRequest.HasHeaderParameters);
        Assert.IsFalse(CreateItemRequest.HasCookieParameters);
    }

    [TestMethod]
    public void DeleteItemRequest_StaticMembers()
    {
        Assert.AreEqual(OperationMethod.Delete, DeleteItemRequest.Method);
        Assert.IsTrue(DeleteItemRequest.HasPathParameters);
        Assert.IsFalse(DeleteItemRequest.HasQueryParameters);
        Assert.IsFalse(DeleteItemRequest.HasHeaderParameters);
        Assert.IsFalse(DeleteItemRequest.HasCookieParameters);
    }

    [TestMethod]
    public void GetSessionProfileRequest_StaticMembers()
    {
        Assert.AreEqual(OperationMethod.Get, GetSessionProfileRequest.Method);
        Assert.IsFalse(GetSessionProfileRequest.HasPathParameters);
        Assert.IsFalse(GetSessionProfileRequest.HasQueryParameters);
        Assert.IsFalse(GetSessionProfileRequest.HasHeaderParameters);
        Assert.IsTrue(GetSessionProfileRequest.HasCookieParameters);
    }

    [TestMethod]
    public void UpdateItemRequest_StaticMembers()
    {
        Assert.AreEqual(OperationMethod.Put, UpdateItemRequest.Method);
        Assert.IsFalse(UpdateItemRequest.HasPathParameters);
        Assert.IsFalse(UpdateItemRequest.HasQueryParameters);
        Assert.IsFalse(UpdateItemRequest.HasHeaderParameters);
        Assert.IsFalse(UpdateItemRequest.HasCookieParameters);
    }

    [TestMethod]
    public void GetItemTagRequest_StaticMembers()
    {
        Assert.AreEqual(OperationMethod.Get, GetItemTagRequest.Method);
        Assert.IsTrue(GetItemTagRequest.HasPathParameters);
        Assert.IsTrue(GetItemTagRequest.HasQueryParameters);
        Assert.IsFalse(GetItemTagRequest.HasHeaderParameters);
        Assert.IsFalse(GetItemTagRequest.HasCookieParameters);
    }

    [TestMethod]
    public void GetItemDetailsRequest_StaticMembers()
    {
        Assert.AreEqual(OperationMethod.Get, GetItemDetailsRequest.Method);
        Assert.IsTrue(GetItemDetailsRequest.HasPathParameters);
        Assert.IsFalse(GetItemDetailsRequest.HasQueryParameters);
        Assert.IsFalse(GetItemDetailsRequest.HasHeaderParameters);
        Assert.IsFalse(GetItemDetailsRequest.HasCookieParameters);
    }

    [TestMethod]
    public void SearchRequest_StaticMembers()
    {
        Assert.AreEqual(OperationMethod.Get, SearchRequest.Method);
        Assert.IsFalse(SearchRequest.HasPathParameters);
        Assert.IsTrue(SearchRequest.HasQueryParameters);
        Assert.IsFalse(SearchRequest.HasHeaderParameters);
        Assert.IsFalse(SearchRequest.HasCookieParameters);
    }

    [TestMethod]
    public void GetByFlagRequest_StaticMembers()
    {
        Assert.AreEqual(OperationMethod.Get, GetByFlagRequest.Method);
        Assert.IsTrue(GetByFlagRequest.HasPathParameters);
        Assert.IsFalse(GetByFlagRequest.HasQueryParameters);
        Assert.IsTrue(GetByFlagRequest.HasHeaderParameters);
        Assert.IsFalse(GetByFlagRequest.HasCookieParameters);
    }

    [TestMethod]
    public void GetPageRequest_StaticMembers()
    {
        Assert.AreEqual(OperationMethod.Get, GetPageRequest.Method);
        Assert.IsTrue(GetPageRequest.HasPathParameters);
        Assert.IsTrue(GetPageRequest.HasQueryParameters);
        Assert.IsFalse(GetPageRequest.HasHeaderParameters);
        Assert.IsFalse(GetPageRequest.HasCookieParameters);
    }

    [TestMethod]
    public void GetOrderRequest_StaticMembers()
    {
        Assert.AreEqual(OperationMethod.Get, GetOrderRequest.Method);
        Assert.IsTrue(GetOrderRequest.HasPathParameters);
        Assert.IsTrue(GetOrderRequest.HasQueryParameters);
        Assert.IsTrue(GetOrderRequest.HasHeaderParameters);
        Assert.IsFalse(GetOrderRequest.HasCookieParameters);
    }

    [TestMethod]
    public void UpdateOrderRequest_StaticMembers()
    {
        Assert.AreEqual(OperationMethod.Put, UpdateOrderRequest.Method);
        Assert.IsTrue(UpdateOrderRequest.HasPathParameters);
        Assert.IsFalse(UpdateOrderRequest.HasQueryParameters);
        Assert.IsTrue(UpdateOrderRequest.HasHeaderParameters);
        Assert.IsFalse(UpdateOrderRequest.HasCookieParameters);
    }

    [TestMethod]
    public async Task Client_ApiItemsClient_GetItemAsync()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"id":"x","name":"y","price":1}""");
        var client = new ApiItemsClient(harness.Transport);

        await using GetItemResponse response = await client.GetItemAsync("x");

        Assert.AreEqual(200, response.StatusCode);
        Assert.IsTrue(response.TryGetOk(out var body));
        Assert.AreEqual("y", (string)body.Name);
    }

    [TestMethod]
    public async Task Client_ApiItemsClient_GetItemAsync_WithOptionalParams()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"id":"x","name":"y","price":1}""");
        var client = new ApiItemsClient(harness.Transport);

        await using GetItemResponse response = await client.GetItemAsync("id1", filter: "ab", limit: 10, verbose: true, xRequestId: "req-1");

        Assert.AreEqual("http://localhost/items/id1?filter=ab&limit=10&verbose=true", harness.CapturedRequest!.RequestUri!.OriginalString);
        Assert.IsTrue(harness.CapturedRequest.Headers.Contains("X-Request-Id"));
    }

    [TestMethod]
    public async Task Client_ApiItemsClient_DeleteItemAsync()
    {
        using var harness = new TestHarness(HttpStatusCode.NoContent, string.Empty);
        var client = new ApiItemsClient(harness.Transport);

        await using DeleteItemResponse response = await client.DeleteItemAsync("del-1");

        Assert.AreEqual(204, response.StatusCode);
        Assert.AreEqual(HttpMethod.Delete, harness.CapturedRequest!.Method);
    }

    [TestMethod]
    public async Task Client_ApiItemsClient_GetItemTagAsync()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"tag":"v"}""");
        var client = new ApiItemsClient(harness.Transport);

        await using GetItemTagResponse response = await client.GetItemTagAsync(99L, "mytag", score: 7.5);

        Assert.AreEqual("http://localhost/items/99/tags/mytag?score=7.5", harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task Client_ApiItemsClient_GetItemDetailsAsync()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"detail":"x"}""");
        var client = new ApiItemsClient(harness.Transport);

        await using GetItemDetailsResponse response = await client.GetItemDetailsAsync("abc");

        Assert.AreEqual("http://localhost/items/abc/details", harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task Client_ApiItemsClient_CreateItemAsync()
    {
        using var harness = new TestHarness(HttpStatusCode.Created, """{"id":"new","name":"w"}""");
        var client = new ApiItemsClient(harness.Transport);

        using var bodyDoc = ParsedJsonDocument<PostItemsBody>.Parse("""{"name":"Widget","price":5}""");

        await using CreateItemResponse response = await client.CreateItemAsync(bodyDoc.RootElement);

        Assert.AreEqual(201, response.StatusCode);
        Assert.AreEqual(HttpMethod.Post, harness.CapturedRequest!.Method);
    }

    [TestMethod]
    public async Task Client_ApiItemsClient_UpdateItemAsync()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"id":"up","name":"u"}""");
        var client = new ApiItemsClient(harness.Transport);

        using var bodyDoc = ParsedJsonDocument<PutItemsBody>.Parse("""{"id":"up","name":"Updated"}""");

        await using UpdateItemResponse response = await client.UpdateItemAsync(bodyDoc.RootElement);

        Assert.AreEqual(200, response.StatusCode);
        Assert.AreEqual(HttpMethod.Put, harness.CapturedRequest!.Method);
    }

    [TestMethod]
    public async Task Client_ApiOrdersClient_GetOrderAsync()
    {
        using var harness = new TestHarness(
            HttpStatusCode.OK,
            """{"orderId":"550e8400-e29b-41d4-a716-446655440000","status":"s","total":1}""");
        var client = new ApiOrdersClient(harness.Transport);

        await using GetOrderResponse response = await client.GetOrderAsync(
            Guid.Parse("550e8400-e29b-41d4-a716-446655440000"),
            xTraceId: "trace",
            fields: "status");

        Assert.AreEqual(200, response.StatusCode);
        Assert.IsTrue(harness.CapturedRequest!.Headers.Contains("X-Trace-Id"));
    }

    [TestMethod]
    public async Task Client_ApiOrdersClient_UpdateOrderAsync()
    {
        using var harness = new TestHarness(
            HttpStatusCode.OK,
            """{"orderId":"550e8400-e29b-41d4-a716-446655440000","status":"ok","total":1}""");
        var client = new ApiOrdersClient(harness.Transport);

        using var bodyDoc = ParsedJsonDocument<PutOrdersByOrderIdBody>.Parse("""{"status":"confirmed"}""");

        await using UpdateOrderResponse response = await client.UpdateOrderAsync(
            Guid.Parse("550e8400-e29b-41d4-a716-446655440000"),
            Guid.Parse("11111111-2222-3333-4444-555555555555"),
            bodyDoc.RootElement);

        Assert.AreEqual(200, response.StatusCode);
        Assert.AreEqual(HttpMethod.Put, harness.CapturedRequest!.Method);
    }

    [TestMethod]
    public async Task Client_ApiFlagsClient_GetByFlagAsync()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");
        var client = new ApiFlagsClient(harness.Transport);

        await using GetByFlagResponse response = await client.GetByFlagAsync(true, "trace-id", 42, xDebug: true, xScore: 3.14);

        Assert.AreEqual("http://localhost/flags/true", harness.CapturedRequest!.RequestUri!.OriginalString);
        Assert.AreEqual("trace-id", harness.CapturedRequest.Headers.GetValues("X-Trace-Id").First());
        Assert.AreEqual("42", harness.CapturedRequest.Headers.GetValues("X-Request-Count").First());
    }

    [TestMethod]
    public async Task Client_ApiPagesClient_GetPageAsync()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");
        var client = new ApiPagesClient(harness.Transport);

        await using GetPageResponse response = await client.GetPageAsync(7, offset: 50);

        Assert.AreEqual("http://localhost/pages/7?offset=50", harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task Client_ApiSearchClient_SearchAsync()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");
        var client = new ApiSearchClient(harness.Transport);

        await using SearchResponse response = await client.SearchAsync("widgets", page: 2, rating: 4.5f);

        Assert.AreEqual("http://localhost/search?q=widgets&page=2&rating=4.5", harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task Client_ApiSessionClient_GetSessionProfileAsync()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");
        var client = new ApiSessionClient(harness.Transport);

        await using GetSessionProfileResponse response = await client.GetSessionProfileAsync(
            "sess-123", theme: "dark", debug: true, max_age: 3600);

        Assert.IsTrue(harness.CapturedRequest!.Headers.Contains("Cookie"));
        Assert.AreEqual(
            "session_id=sess-123; theme=dark; debug=true; max_age=3600",
            harness.CapturedRequest.Headers.GetValues("Cookie").First());
    }

    /// <summary>
    /// Encapsulates a mock HTTP handler, HttpClient, and HttpClientTransport for testing.
    /// The handler captures the outgoing request and returns a canned response.
    /// </summary>
    private sealed class TestHarness : IDisposable
    {
        private readonly MockHandler handler;
        private readonly HttpClient client;

        public TestHarness(HttpStatusCode statusCode, string responseBody)
            : this(statusCode, responseBody, null)
        {
        }

        public TestHarness(
            HttpStatusCode statusCode,
            string responseBody,
            Dictionary<string, string>? responseHeaders)
        {
            this.handler = new MockHandler(statusCode, responseBody, responseHeaders);
            this.client = new HttpClient(this.handler)
            {
                BaseAddress = new Uri("http://localhost"),
            };
            this.Transport = new HttpClientTransport(this.client);
        }

        public HttpClientTransport Transport { get; }

        public HttpRequestMessage? CapturedRequest => this.handler.CapturedRequest;

        public void Dispose()
        {
            this.Transport.DisposeAsync().AsTask().GetAwaiter().GetResult();
            this.client.Dispose();
            this.handler.Dispose();
        }
    }

    /// <summary>
    /// A <see cref="DelegatingHandler"/> that captures the request and returns
    /// a preconfigured response with the given status code and body.
    /// </summary>
    private sealed class MockHandler : DelegatingHandler
    {
        private readonly HttpStatusCode statusCode;
        private readonly string responseBody;
        private readonly Dictionary<string, string>? responseHeaders;

        public MockHandler(
            HttpStatusCode statusCode,
            string responseBody,
            Dictionary<string, string>? responseHeaders = null)
        {
            this.statusCode = statusCode;
            this.responseBody = responseBody;
            this.responseHeaders = responseHeaders;
            this.InnerHandler = new HttpClientHandler();
        }

        public HttpRequestMessage? CapturedRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            this.CapturedRequest = request;

            HttpResponseMessage response = new(this.statusCode)
            {
                Content = string.IsNullOrEmpty(this.responseBody)
                    ? new ByteArrayContent([])
                    : new StringContent(this.responseBody, Encoding.UTF8, "application/json"),
            };

            if (this.responseHeaders is not null)
            {
                foreach (var (key, value) in this.responseHeaders)
                {
                    response.Headers.TryAddWithoutValidation(key, value);
                }
            }

            return Task.FromResult(response);
        }
    }
}