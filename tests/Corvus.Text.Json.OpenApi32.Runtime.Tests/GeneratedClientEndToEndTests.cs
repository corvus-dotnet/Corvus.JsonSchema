// <copyright file="GeneratedClientEndToEndTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Net;
using System.Text;
using CanonTests32.Client;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;

namespace Corvus.Text.Json.OpenApi32.Runtime.Tests;

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
    public async Task GetItemTag_ResponseHeader_ArrayParsedLazily()
    {
        using var harness = new TestHarness(
            HttpStatusCode.OK,
            """{"tag":"v"}""",
            new Dictionary<string, string> { ["X-Tags"] = "alpha, beta, gamma" });

        await using GetItemTagResponse response = await harness.Transport
            .SendAsync<GetItemTagRequest, GetItemTagResponse>(
                new GetItemTagRequest(JsonInt64.ParseValue("1"u8), JsonString.ParseValue("\"t\""u8)),
                CancellationToken.None);

        Assert.IsNotNull(response.XTagsHeader);
        var tags = response.XTagsHeader.Value;
        Assert.AreEqual(3, tags.GetArrayLength());
        Assert.AreEqual("alpha", (string)tags[0]);
        Assert.AreEqual("beta", (string)tags[1]);
        Assert.AreEqual("gamma", (string)tags[2]);
    }

    [TestMethod]
    public async Task GetItemTag_ResponseHeader_ArraySingleElement()
    {
        using var harness = new TestHarness(
            HttpStatusCode.OK,
            """{"tag":"v"}""",
            new Dictionary<string, string> { ["X-Tags"] = "solo" });

        await using GetItemTagResponse response = await harness.Transport
            .SendAsync<GetItemTagRequest, GetItemTagResponse>(
                new GetItemTagRequest(JsonInt64.ParseValue("1"u8), JsonString.ParseValue("\"t\""u8)),
                CancellationToken.None);

        Assert.IsNotNull(response.XTagsHeader);
        var tags = response.XTagsHeader.Value;
        Assert.AreEqual(1, tags.GetArrayLength());
        Assert.AreEqual("solo", (string)tags[0]);
    }

    [TestMethod]
    public async Task GetItemTag_ResponseHeader_ArrayMissing()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"tag":"v"}""");

        await using GetItemTagResponse response = await harness.Transport
            .SendAsync<GetItemTagRequest, GetItemTagResponse>(
                new GetItemTagRequest(JsonInt64.ParseValue("1"u8), JsonString.ParseValue("\"t\""u8)),
                CancellationToken.None);

        Assert.IsNull(response.XTagsHeader);
    }

    [TestMethod]
    public async Task GetItemTag_ResponseHeader_ObjectParsedLazily()
    {
        // style: simple, explode: false for objects → key,value,key,value
        using var harness = new TestHarness(
            HttpStatusCode.OK,
            """{"tag":"v"}""",
            new Dictionary<string, string> { ["X-Metadata"] = "env, production, region, us-east" });

        await using GetItemTagResponse response = await harness.Transport
            .SendAsync<GetItemTagRequest, GetItemTagResponse>(
                new GetItemTagRequest(JsonInt64.ParseValue("1"u8), JsonString.ParseValue("\"t\""u8)),
                CancellationToken.None);

        Assert.IsNotNull(response.XMetadataHeader);
        var metadata = response.XMetadataHeader.Value;
        Assert.IsTrue(metadata.TryGetProperty("env", out var envVal));
        Assert.AreEqual("production", envVal.GetString());
        Assert.IsTrue(metadata.TryGetProperty("region", out var regionVal));
        Assert.AreEqual("us-east", regionVal.GetString());
    }

    [TestMethod]
    public async Task GetItemTag_ResponseHeader_ObjectSinglePair()
    {
        using var harness = new TestHarness(
            HttpStatusCode.OK,
            """{"tag":"v"}""",
            new Dictionary<string, string> { ["X-Metadata"] = "key, value" });

        await using GetItemTagResponse response = await harness.Transport
            .SendAsync<GetItemTagRequest, GetItemTagResponse>(
                new GetItemTagRequest(JsonInt64.ParseValue("1"u8), JsonString.ParseValue("\"t\""u8)),
                CancellationToken.None);

        Assert.IsNotNull(response.XMetadataHeader);
        var metadata = response.XMetadataHeader.Value;
        Assert.IsTrue(metadata.TryGetProperty("key", out var val));
        Assert.AreEqual("value", val.GetString());
    }

    [TestMethod]
    public async Task GetItemTag_ResponseHeader_ObjectMissing()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"tag":"v"}""");

        await using GetItemTagResponse response = await harness.Transport
            .SendAsync<GetItemTagRequest, GetItemTagResponse>(
                new GetItemTagRequest(JsonInt64.ParseValue("1"u8), JsonString.ParseValue("\"t\""u8)),
                CancellationToken.None);

        Assert.IsNull(response.XMetadataHeader);
    }

    [TestMethod]
    public async Task GetItemTag_ResponseHeader_AllFourHeadersPresent()
    {
        using var harness = new TestHarness(
            HttpStatusCode.OK,
            """{"tag":"v"}""",
            new Dictionary<string, string>
            {
                ["X-Total-Count"] = "42",
                ["X-Request-Id"] = "req-123",
                ["X-Tags"] = "a, b",
                ["X-Metadata"] = "k1, v1",
            });

        await using GetItemTagResponse response = await harness.Transport
            .SendAsync<GetItemTagRequest, GetItemTagResponse>(
                new GetItemTagRequest(JsonInt64.ParseValue("1"u8), JsonString.ParseValue("\"t\""u8)),
                CancellationToken.None);

        Assert.AreEqual(42, (int)response.XTotalCountHeader!.Value);
        Assert.AreEqual("req-123", (string)response.XRequestIdHeader!.Value);

        var tags = response.XTagsHeader!.Value;
        Assert.AreEqual(2, tags.GetArrayLength());
        Assert.AreEqual("a", (string)tags[0]);
        Assert.AreEqual("b", (string)tags[1]);

        var metadata = response.XMetadataHeader!.Value;
        Assert.IsTrue(metadata.TryGetProperty("k1", out var v1));
        Assert.AreEqual("v1", v1.GetString());
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
        Assert.IsTrue(CreateItemRequest.HasHeaderParameters);
        Assert.IsFalse(CreateItemRequest.HasCookieParameters);
    }

    [TestMethod]
    public void DeleteItemRequest_StaticMembers()
    {
        Assert.AreEqual(OperationMethod.Delete, DeleteItemRequest.Method);
        Assert.IsTrue(DeleteItemRequest.HasPathParameters);
        Assert.IsFalse(DeleteItemRequest.HasQueryParameters);
        Assert.IsTrue(DeleteItemRequest.HasHeaderParameters);
        Assert.IsFalse(DeleteItemRequest.HasCookieParameters);
    }

    [TestMethod]
    public void GetSessionProfileRequest_StaticMembers()
    {
        Assert.AreEqual(OperationMethod.Get, GetSessionProfileRequest.Method);
        Assert.IsFalse(GetSessionProfileRequest.HasPathParameters);
        Assert.IsFalse(GetSessionProfileRequest.HasQueryParameters);
        Assert.IsTrue(GetSessionProfileRequest.HasHeaderParameters);
        Assert.IsTrue(GetSessionProfileRequest.HasCookieParameters);
    }

    [TestMethod]
    public void UpdateItemRequest_StaticMembers()
    {
        Assert.AreEqual(OperationMethod.Put, UpdateItemRequest.Method);
        Assert.IsFalse(UpdateItemRequest.HasPathParameters);
        Assert.IsFalse(UpdateItemRequest.HasQueryParameters);
        Assert.IsTrue(UpdateItemRequest.HasHeaderParameters);
        Assert.IsFalse(UpdateItemRequest.HasCookieParameters);
    }

    [TestMethod]
    public void GetItemTagRequest_StaticMembers()
    {
        Assert.AreEqual(OperationMethod.Get, GetItemTagRequest.Method);
        Assert.IsTrue(GetItemTagRequest.HasPathParameters);
        Assert.IsTrue(GetItemTagRequest.HasQueryParameters);
        Assert.IsTrue(GetItemTagRequest.HasHeaderParameters);
        Assert.IsFalse(GetItemTagRequest.HasCookieParameters);
    }

    [TestMethod]
    public void GetItemDetailsRequest_StaticMembers()
    {
        Assert.AreEqual(OperationMethod.Get, GetItemDetailsRequest.Method);
        Assert.IsTrue(GetItemDetailsRequest.HasPathParameters);
        Assert.IsFalse(GetItemDetailsRequest.HasQueryParameters);
        Assert.IsTrue(GetItemDetailsRequest.HasHeaderParameters);
        Assert.IsFalse(GetItemDetailsRequest.HasCookieParameters);
    }

    [TestMethod]
    public void SearchRequest_StaticMembers()
    {
        Assert.AreEqual(OperationMethod.Get, SearchRequest.Method);
        Assert.IsFalse(SearchRequest.HasPathParameters);
        Assert.IsTrue(SearchRequest.HasQueryParameters);
        Assert.IsTrue(SearchRequest.HasHeaderParameters);
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
        Assert.IsTrue(GetPageRequest.HasHeaderParameters);
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

    [TestMethod]
    public async Task Client_ApiFormsClient_SubmitContactFormAsync_SendsFormUrlEncoded()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"received":true}""");
        var client = new ApiFormsClient(harness.Transport);

        using var bodyDoc = ParsedJsonDocument<PostFormsContactBody>.Parse(
            """{"name":"Alice","email":"alice@example.com","message":"Hello"}""");

        await using SubmitContactFormResponse response = await client.SubmitContactFormAsync(
            bodyDoc.RootElement);

        Assert.AreEqual(200, response.StatusCode);
        Assert.AreEqual(HttpMethod.Post, harness.CapturedRequest!.Method);
        Assert.AreEqual(
            "application/x-www-form-urlencoded",
            harness.CapturedRequestContentType);
    }

    [TestMethod]
    public async Task Client_ApiFormsClient_SubmitContactFormAsync_EncodesBodyCorrectly()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"received":true}""");
        var client = new ApiFormsClient(harness.Transport);

        using var bodyDoc = ParsedJsonDocument<PostFormsContactBody>.Parse(
            """{"name":"Alice Smith","email":"alice@example.com"}""");

        await using SubmitContactFormResponse response = await client.SubmitContactFormAsync(
            bodyDoc.RootElement);

        string body = System.Text.Encoding.UTF8.GetString(harness.CapturedRequestBody!);
        Assert.AreEqual("name=Alice%20Smith&email=alice%40example.com", body);
    }

    [TestMethod]
    public async Task Client_ApiFormsClient_SubmitContactFormAsync_UrlPath()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"received":true}""");
        var client = new ApiFormsClient(harness.Transport);

        using var bodyDoc = ParsedJsonDocument<PostFormsContactBody>.Parse(
            """{"name":"Bob","email":"b@x.com"}""");

        await using SubmitContactFormResponse response = await client.SubmitContactFormAsync(
            bodyDoc.RootElement);

        Assert.AreEqual(
            "http://localhost/forms/contact",
            harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task Client_ApiFormsClient_UploadDocumentAsync_SendsMultipart()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"uploaded":true}""");
        var client = new ApiFormsClient(harness.Transport);

        using var bodyDoc = ParsedJsonDocument<PostFormsUploadBody>.Parse(
            """{"title":"My Doc","category":"reports"}""");

        await using UploadDocumentResponse response = await client.UploadDocumentAsync(
            bodyDoc.RootElement);

        Assert.AreEqual(200, response.StatusCode);
        Assert.AreEqual(HttpMethod.Post, harness.CapturedRequest!.Method);
        Assert.IsTrue(
            harness.CapturedRequestContentType!.StartsWith("multipart/form-data; boundary="),
            $"Expected multipart content type, got: {harness.CapturedRequestContentType}");
    }

    [TestMethod]
    public async Task Client_ApiFormsClient_UploadDocumentAsync_EncodesBodyCorrectly()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"uploaded":true}""");
        var client = new ApiFormsClient(harness.Transport);

        using var bodyDoc = ParsedJsonDocument<PostFormsUploadBody>.Parse(
            """{"title":"My Doc","category":"reports"}""");

        await using UploadDocumentResponse response = await client.UploadDocumentAsync(
            bodyDoc.RootElement);

        // Extract the boundary from the Content-Type header.
        string contentType = harness.CapturedRequestContentType!;
        string boundary = contentType.Substring(contentType.IndexOf("boundary=") + "boundary=".Length);

        string body = System.Text.Encoding.UTF8.GetString(harness.CapturedRequestBody!);

        // Verify the multipart structure contains the expected parts.
        Assert.IsTrue(body.Contains($"--{boundary}\r\n"), "Missing opening boundary");
        Assert.IsTrue(body.Contains($"--{boundary}--\r\n"), "Missing closing boundary");
        Assert.IsTrue(body.Contains("Content-Disposition: form-data; name=\"title\"\r\n\r\nMy Doc"), "Missing title part");
        Assert.IsTrue(body.Contains("Content-Disposition: form-data; name=\"category\"\r\n\r\nreports"), "Missing category part");
    }

    [TestMethod]
    public async Task Client_ApiFormsClient_UploadDocumentAsync_HandlesArrayAsJson()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"uploaded":true}""");
        var client = new ApiFormsClient(harness.Transport);

        using var bodyDoc = ParsedJsonDocument<PostFormsUploadBody>.Parse(
            """{"title":"Tagged","tags":["alpha","beta"]}""");

        await using UploadDocumentResponse response = await client.UploadDocumentAsync(
            bodyDoc.RootElement);

        string body = System.Text.Encoding.UTF8.GetString(harness.CapturedRequestBody!);

        // Array values are JSON-stringified with an application/json content type.
        Assert.IsTrue(
            body.Contains("Content-Type: application/json\r\n\r\n[\"alpha\",\"beta\"]"),
            $"Expected JSON-encoded tags array, got:\n{body}");
    }

    [TestMethod]
    public async Task Client_ApiFormsClient_UploadDocumentAsync_UrlPath()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"uploaded":true}""");
        var client = new ApiFormsClient(harness.Transport);

        using var bodyDoc = ParsedJsonDocument<PostFormsUploadBody>.Parse(
            """{"title":"Test"}""");

        await using UploadDocumentResponse response = await client.UploadDocumentAsync(
            bodyDoc.RootElement);

        Assert.AreEqual(
            "http://localhost/forms/upload",
            harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task Client_ApiFormsClient_SubmitEncodedContactFormAsync_PipeDelimitedTags()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"received":true}""");
        var client = new ApiFormsClient(harness.Transport);

        using var bodyDoc = ParsedJsonDocument<PostFormsEncodedContactBody>.Parse(
            """{"name":"Alice","email":"alice@example.com","tags":["red","green","blue"]}""");

        await using SubmitEncodedContactFormResponse response = await client.SubmitEncodedContactFormAsync(
            bodyDoc.RootElement);

        string body = System.Text.Encoding.UTF8.GetString(harness.CapturedRequestBody!);

        // Tags should be pipe-delimited (non-exploded): tags=red|green|blue
        Assert.IsTrue(
            body.Contains("tags=red%7Cgreen%7Cblue") || body.Contains("tags=red|green|blue"),
            $"Expected pipe-delimited tags, got: {body}");
    }

    [TestMethod]
    public async Task Client_ApiFormsClient_SubmitEncodedContactFormAsync_DeepObjectAddress()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"received":true}""");
        var client = new ApiFormsClient(harness.Transport);

        using var bodyDoc = ParsedJsonDocument<PostFormsEncodedContactBody>.Parse(
            """{"name":"Alice","email":"alice@example.com","address":{"street":"1 Main St","city":"London"}}""");

        await using SubmitEncodedContactFormResponse response = await client.SubmitEncodedContactFormAsync(
            bodyDoc.RootElement);

        string body = System.Text.Encoding.UTF8.GetString(harness.CapturedRequestBody!);

        // Address should be deep-object style: address[street]=1%20Main%20St&address[city]=London
        Assert.IsTrue(
            body.Contains("address%5Bstreet%5D=1%20Main%20St") || body.Contains("address[street]=1%20Main%20St"),
            $"Expected deep-object address with street, got: {body}");
        Assert.IsTrue(
            body.Contains("address%5Bcity%5D=London") || body.Contains("address[city]=London"),
            $"Expected deep-object address with city, got: {body}");
    }

    [TestMethod]
    public async Task Client_ApiFormsClient_SubmitEncodedContactFormAsync_AllowReservedEmail()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"received":true}""");
        var client = new ApiFormsClient(harness.Transport);

        using var bodyDoc = ParsedJsonDocument<PostFormsEncodedContactBody>.Parse(
            """{"name":"Alice","email":"alice+test@example.com"}""");

        await using SubmitEncodedContactFormResponse response = await client.SubmitEncodedContactFormAsync(
            bodyDoc.RootElement);

        string body = System.Text.Encoding.UTF8.GetString(harness.CapturedRequestBody!);

        // With allowReserved, the + in the email should NOT be percent-encoded
        Assert.IsTrue(
            body.Contains("email=alice+test@example.com"),
            $"Expected unencoded reserved chars in email, got: {body}");
    }

    [TestMethod]
    public async Task Client_ApiFormsClient_UploadEncodedDocumentAsync_MetadataContentTypeOverride()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"uploaded":true}""");
        var client = new ApiFormsClient(harness.Transport);

        using var bodyDoc = ParsedJsonDocument<PostFormsEncodedUploadBody>.Parse(
            """{"title":"My Doc","metadata":{"author":"Alice","version":2}}""");

        await using UploadEncodedDocumentResponse response = await client.UploadEncodedDocumentAsync(
            bodyDoc.RootElement);

        string body = System.Text.Encoding.UTF8.GetString(harness.CapturedRequestBody!);

        // metadata part should have application/json Content-Type override
        Assert.IsTrue(
            body.Contains("Content-Disposition: form-data; name=\"metadata\"\r\nContent-Type: application/json\r\n\r\n"),
            $"Expected metadata with application/json Content-Type, got:\n{body}");
    }

    [TestMethod]
    public async Task Client_ApiFormsClient_UploadEncodedDocumentAsync_TagsContentTypeOverride()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"uploaded":true}""");
        var client = new ApiFormsClient(harness.Transport);

        using var bodyDoc = ParsedJsonDocument<PostFormsEncodedUploadBody>.Parse(
            """{"title":"Tagged","tags":["alpha","beta"]}""");

        await using UploadEncodedDocumentResponse response = await client.UploadEncodedDocumentAsync(
            bodyDoc.RootElement);

        string body = System.Text.Encoding.UTF8.GetString(harness.CapturedRequestBody!);

        // tags part should have application/json Content-Type from encoding override
        Assert.IsTrue(
            body.Contains("Content-Disposition: form-data; name=\"tags\"\r\nContent-Type: application/json\r\n\r\n[\"alpha\",\"beta\"]"),
            $"Expected tags with application/json Content-Type override, got:\n{body}");
    }

    [TestMethod]
    public async Task DisposeAsync_WithDisposeClient_DisposesHttpClient()
    {
        var handler = new MockHandler(HttpStatusCode.OK, """{}""");
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var transport = new HttpClientTransport(client, disposeClient: true);

        await transport.DisposeAsync();

        // After the transport disposes the HttpClient, it should be unusable.
        Assert.ThrowsExactly<ObjectDisposedException>(() =>
            client.GetAsync("http://localhost/test").GetAwaiter().GetResult());

        handler.Dispose();
    }

    // ═══════════════════════════════════════════════════════════════════
    // 3.2-specific: HTTP QUERY method
    // ═══════════════════════════════════════════════════════════════════
    [TestMethod]
    public async Task QuerySearch_200_UsesQueryMethod()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"results":[{"id":"r1","title":"Result 1"}],"total":1}""");

        using var bodyDoc = ParsedJsonDocument<Schema1>.Parse("""{"query":"test"}"""u8.ToArray());
        Schema1 body = bodyDoc.RootElement;

        await using QuerySearchResponse response = await harness.Transport
            .SendAsync<QuerySearchRequest, Schema1, QuerySearchResponse>(
                default(QuerySearchRequest), in body, CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        Assert.AreEqual("QUERY", harness.CapturedRequest.Method.Method);
    }

    [TestMethod]
    public async Task QuerySearch_200_ParsesOkBody()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"results":[{"id":"r1","title":"Result 1"}],"total":1}""");

        using var bodyDoc = ParsedJsonDocument<Schema1>.Parse("""{"query":"test"}"""u8.ToArray());
        Schema1 body = bodyDoc.RootElement;

        await using QuerySearchResponse response = await harness.Transport
            .SendAsync<QuerySearchRequest, Schema1, QuerySearchResponse>(
                default(QuerySearchRequest), in body, CancellationToken.None);

        Assert.AreEqual(200, response.StatusCode);
        Assert.IsTrue(response.IsSuccess);
        Assert.IsTrue(response.TryGetOk(out var searchResult));
        Assert.AreEqual(1, (int)searchResult.Total);
    }

    [TestMethod]
    public async Task QuerySearch_200_RequestPathIsCorrect()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"results":[],"total":0}""");

        using var bodyDoc = ParsedJsonDocument<Schema1>.Parse("""{"query":"hello"}"""u8.ToArray());
        Schema1 body = bodyDoc.RootElement;

        await using QuerySearchResponse response = await harness.Transport
            .SendAsync<QuerySearchRequest, Schema1, QuerySearchResponse>(
                default(QuerySearchRequest), in body, CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        Assert.AreEqual("http://localhost/query-search", harness.CapturedRequest.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task QuerySearch_200_QueryParamsAppended()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"results":[],"total":0}""");

        using var bodyDoc = ParsedJsonDocument<Schema1>.Parse("""{"query":"hello"}"""u8.ToArray());
        Schema1 body = bodyDoc.RootElement;

        var request = new QuerySearchRequest { Limit = JsonInt32.ParseValue("25"u8) };

        await using QuerySearchResponse response = await harness.Transport
            .SendAsync<QuerySearchRequest, Schema1, QuerySearchResponse>(
                in request, in body, CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        StringAssert.Contains(harness.CapturedRequest.RequestUri!.OriginalString, "limit=25");
    }

    [TestMethod]
    public async Task QuerySearch_200_RequestBodyIsSentAsJson()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"results":[],"total":0}""");

        using var bodyDoc = ParsedJsonDocument<Schema1>.Parse("""{"query":"test","tags":["a","b"]}"""u8.ToArray());
        Schema1 body = bodyDoc.RootElement;

        await using QuerySearchResponse response = await harness.Transport
            .SendAsync<QuerySearchRequest, Schema1, QuerySearchResponse>(
                default(QuerySearchRequest), in body, CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        Assert.AreEqual("application/json", harness.CapturedRequest.Content?.Headers.ContentType?.MediaType);
        byte[] sentBody = await harness.CapturedRequest.Content!.ReadAsByteArrayAsync();
        string sentJson = System.Text.Encoding.UTF8.GetString(sentBody);
        Assert.IsTrue(sentJson.Contains("\"query\":\"test\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task QuerySearch_MatchResult_DispatchesToOk()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"results":[{"id":"r1","title":"T"}],"total":1}""");

        using var bodyDoc = ParsedJsonDocument<Schema1>.Parse("""{"query":"x"}"""u8.ToArray());
        Schema1 body = bodyDoc.RootElement;

        await using QuerySearchResponse response = await harness.Transport
            .SendAsync<QuerySearchRequest, Schema1, QuerySearchResponse>(
                default(QuerySearchRequest), in body, CancellationToken.None);

        string result = response.MatchResult(
            matchOk: static body => $"ok:{(int)body.Total}",
            matchDefault: static code => $"default:{code}");

        Assert.AreEqual("ok:1", result);
    }

    [TestMethod]
    public void QuerySearchRequest_StaticMembers()
    {
        Assert.AreEqual(OperationMethod.Query, QuerySearchRequest.Method);
        Assert.IsTrue(QuerySearchRequest.PathTemplateUtf8.SequenceEqual("/query-search"u8));
        Assert.IsFalse(QuerySearchRequest.HasPathParameters);
        Assert.IsTrue(QuerySearchRequest.HasQueryParameters);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 3.2-specific: additionalOperations (COPY custom method)
    // ═══════════════════════════════════════════════════════════════════
    [TestMethod]
    public async Task CopyItem_201_UsesCopyMethod()
    {
        using var harness = new TestHarness(HttpStatusCode.Created, """{"id":"copy-1","location":"/items/copy-1"}""");

        var request = new CopyItemRequest(
            JsonString.ParseValue("\"src-item\""u8),
            JsonString.ParseValue("\"/dest/path\""u8));

        await using CopyItemResponse response = await harness.Transport
            .SendAsync<CopyItemRequest, CopyItemResponse>(in request, CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        Assert.AreEqual("COPY", harness.CapturedRequest.Method.Method);
    }

    [TestMethod]
    public async Task CopyItem_201_RequestPathIsCorrect()
    {
        using var harness = new TestHarness(HttpStatusCode.Created, """{"id":"copy-1","location":"/items/copy-1"}""");

        var request = new CopyItemRequest(
            JsonString.ParseValue("\"my-item\""u8),
            JsonString.ParseValue("\"/dest\""u8));

        await using CopyItemResponse response = await harness.Transport
            .SendAsync<CopyItemRequest, CopyItemResponse>(in request, CancellationToken.None);

        Assert.AreEqual("http://localhost/items/my-item", harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task CopyItem_201_DestinationHeaderIsSent()
    {
        using var harness = new TestHarness(HttpStatusCode.Created, """{"id":"copy-1","location":"/new"}""");

        var request = new CopyItemRequest(
            JsonString.ParseValue("\"src\""u8),
            JsonString.ParseValue("\"/target/location\""u8));

        await using CopyItemResponse response = await harness.Transport
            .SendAsync<CopyItemRequest, CopyItemResponse>(in request, CancellationToken.None);

        Assert.IsTrue(harness.CapturedRequest!.Headers.Contains("Destination"));
        Assert.AreEqual("/target/location", harness.CapturedRequest.Headers.GetValues("Destination").First());
    }

    [TestMethod]
    public async Task CopyItem_201_ParsesCreatedBody()
    {
        using var harness = new TestHarness(HttpStatusCode.Created, """{"id":"new-copy","location":"/items/new-copy"}""");

        var request = new CopyItemRequest(
            JsonString.ParseValue("\"src\""u8),
            JsonString.ParseValue("\"/dest\""u8));

        await using CopyItemResponse response = await harness.Transport
            .SendAsync<CopyItemRequest, CopyItemResponse>(in request, CancellationToken.None);

        Assert.AreEqual(201, response.StatusCode);
        Assert.IsTrue(response.IsSuccess);
        Assert.IsTrue(response.TryGetCreated(out var body));
        Assert.AreEqual("new-copy", (string)body.Id);
        Assert.AreEqual("/items/new-copy", (string)body.Location);
    }

    [TestMethod]
    public async Task CopyItem_201_LocationHeaderParsed()
    {
        using var harness = new TestHarness(
            HttpStatusCode.Created,
            """{"id":"new-copy","location":"/items/new-copy"}""",
            new Dictionary<string, string> { ["Location"] = "https://example.com/items/new-copy" });

        var request = new CopyItemRequest(
            JsonString.ParseValue("\"src\""u8),
            JsonString.ParseValue("\"/dest\""u8));

        await using CopyItemResponse response = await harness.Transport
            .SendAsync<CopyItemRequest, CopyItemResponse>(in request, CancellationToken.None);

        Assert.IsNotNull(response.LocationHeader);
    }

    [TestMethod]
    public async Task CopyItem_MatchResult_DispatchesToCreated()
    {
        using var harness = new TestHarness(HttpStatusCode.Created, """{"id":"c1","location":"/loc"}""");

        var request = new CopyItemRequest(
            JsonString.ParseValue("\"s\""u8),
            JsonString.ParseValue("\"/d\""u8));

        await using CopyItemResponse response = await harness.Transport
            .SendAsync<CopyItemRequest, CopyItemResponse>(in request, CancellationToken.None);

        string result = response.MatchResult(
            matchCreated: static body => $"created:{(string)body.Id}",
            matchDefault: static code => $"default:{code}");

        Assert.AreEqual("created:c1", result);
    }

    [TestMethod]
    public void CopyItemRequest_StaticMembers()
    {
        Assert.AreEqual(OperationMethod.Custom, CopyItemRequest.Method);
        Assert.IsTrue(CopyItemRequest.PathTemplateUtf8.SequenceEqual("/items/{itemId}"u8));
        Assert.IsTrue(CopyItemRequest.CustomMethodNameUtf8.SequenceEqual("COPY"u8));
        Assert.IsTrue(CopyItemRequest.HasPathParameters);
        Assert.IsTrue(CopyItemRequest.HasHeaderParameters);
        Assert.IsFalse(CopyItemRequest.HasQueryParameters);
        Assert.IsFalse(CopyItemRequest.HasCookieParameters);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 3.2-specific: additionalOperations (PURGE custom method - no body)
    // ═══════════════════════════════════════════════════════════════════
    [TestMethod]
    public async Task PurgeItem_204_UsesPurgeMethod()
    {
        using var harness = new TestHarness(HttpStatusCode.NoContent, string.Empty);

        var request = new PurgeItemRequest(JsonString.ParseValue("\"item-to-purge\""u8));

        await using PurgeItemResponse response = await harness.Transport
            .SendAsync<PurgeItemRequest, PurgeItemResponse>(in request, CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        Assert.AreEqual("PURGE", harness.CapturedRequest.Method.Method);
    }

    [TestMethod]
    public async Task PurgeItem_204_RequestPathIsCorrect()
    {
        using var harness = new TestHarness(HttpStatusCode.NoContent, string.Empty);

        var request = new PurgeItemRequest(JsonString.ParseValue("\"my-item\""u8));

        await using PurgeItemResponse response = await harness.Transport
            .SendAsync<PurgeItemRequest, PurgeItemResponse>(in request, CancellationToken.None);

        Assert.AreEqual("http://localhost/items/my-item", harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task PurgeItem_204_NoBody()
    {
        using var harness = new TestHarness(HttpStatusCode.NoContent, string.Empty);

        var request = new PurgeItemRequest(JsonString.ParseValue("\"x\""u8));

        await using PurgeItemResponse response = await harness.Transport
            .SendAsync<PurgeItemRequest, PurgeItemResponse>(in request, CancellationToken.None);

        Assert.AreEqual(204, response.StatusCode);
        Assert.IsTrue(response.IsSuccess);
    }

    [TestMethod]
    public void PurgeItemRequest_StaticMembers()
    {
        Assert.AreEqual(OperationMethod.Custom, PurgeItemRequest.Method);
        Assert.IsTrue(PurgeItemRequest.PathTemplateUtf8.SequenceEqual("/items/{itemId}"u8));
        Assert.IsTrue(PurgeItemRequest.CustomMethodNameUtf8.SequenceEqual("PURGE"u8));
        Assert.IsTrue(PurgeItemRequest.HasPathParameters);
        Assert.IsFalse(PurgeItemRequest.HasHeaderParameters);
        Assert.IsFalse(PurgeItemRequest.HasQueryParameters);
        Assert.IsFalse(PurgeItemRequest.HasCookieParameters);
    }

    [TestMethod]
    public void SearchWithQuerystringRequest_StaticMembers()
    {
        Assert.AreEqual(OperationMethod.Get, SearchWithQuerystringRequest.Method);
        Assert.IsTrue(SearchWithQuerystringRequest.PathTemplateUtf8.SequenceEqual("/search-with-querystring"u8));
        Assert.IsFalse(SearchWithQuerystringRequest.HasPathParameters);
        Assert.IsTrue(SearchWithQuerystringRequest.HasQueryParameters);
        Assert.IsTrue(SearchWithQuerystringRequest.HasHeaderParameters);
        Assert.IsFalse(SearchWithQuerystringRequest.HasCookieParameters);
    }

    [TestMethod]
    public async Task SearchWithQuerystring_WritesFormEncodedQueryString()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"results":[],"count":0}""");

        using var qsDoc = ParsedJsonDocument<GetSearchWithQuerystringQs>.Parse("""{"q":"hello world","page":2}"""u8.ToArray());
        var request = new SearchWithQuerystringRequest(qsDoc.RootElement);

        await using SearchWithQuerystringResponse response = await harness.Transport
            .SendAsync<SearchWithQuerystringRequest, SearchWithQuerystringResponse>(
                in request, CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        string uri = harness.CapturedRequest.RequestUri!.OriginalString;

        // The query string should contain form-urlencoded parameters
        StringAssert.Contains(uri, "q=hello%20world");
        StringAssert.Contains(uri, "page=2");
    }

    [TestMethod]
    public async Task SearchWithQuerystring_ArraysAreExploded()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"results":[],"count":0}""");

        using var qsDoc = ParsedJsonDocument<GetSearchWithQuerystringQs>.Parse("""{"q":"test","tags":["alpha","beta"]}"""u8.ToArray());
        var request = new SearchWithQuerystringRequest(qsDoc.RootElement);

        await using SearchWithQuerystringResponse response = await harness.Transport
            .SendAsync<SearchWithQuerystringRequest, SearchWithQuerystringResponse>(
                in request, CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        string uri = harness.CapturedRequest.RequestUri!.OriginalString;

        // Arrays should be exploded: tags=alpha&tags=beta
        StringAssert.Contains(uri, "tags=alpha");
        StringAssert.Contains(uri, "tags=beta");
    }

    [TestMethod]
    public async Task SearchWithQuerystring_SpecialCharsArePercentEncoded()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"results":[],"count":0}""");

        using var qsDoc = ParsedJsonDocument<GetSearchWithQuerystringQs>.Parse("""{"q":"a&b=c"}"""u8.ToArray());
        var request = new SearchWithQuerystringRequest(qsDoc.RootElement);

        await using SearchWithQuerystringResponse response = await harness.Transport
            .SendAsync<SearchWithQuerystringRequest, SearchWithQuerystringResponse>(
                in request, CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        string uri = harness.CapturedRequest.RequestUri!.OriginalString;

        // Special chars in values should be percent-encoded
        StringAssert.Contains(uri, "q=a%26b%3Dc");
    }

    [TestMethod]
    public async Task SearchWithQuerystring_ResponseParsesCorrectly()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"results":[{"id":"item-1","title":"First"}],"count":1}""");

        using var qsDoc = ParsedJsonDocument<GetSearchWithQuerystringQs>.Parse("""{"q":"first"}"""u8.ToArray());
        var request = new SearchWithQuerystringRequest(qsDoc.RootElement);

        await using SearchWithQuerystringResponse response = await harness.Transport
            .SendAsync<SearchWithQuerystringRequest, SearchWithQuerystringResponse>(
                in request, CancellationToken.None);

        Assert.AreEqual(200, response.StatusCode);
        Assert.IsTrue(response.IsSuccess);
        Assert.IsTrue(response.TryGetOk(out var body));
        Assert.AreEqual(1, (int)body.Count);
    }

    [TestMethod]
    public async Task SearchWithQuerystring_PathHasNoTemplateSegments()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"results":[],"count":0}""");

        using var qsDoc = ParsedJsonDocument<GetSearchWithQuerystringQs>.Parse("""{"q":"test"}"""u8.ToArray());
        var request = new SearchWithQuerystringRequest(qsDoc.RootElement);

        await using SearchWithQuerystringResponse response = await harness.Transport
            .SendAsync<SearchWithQuerystringRequest, SearchWithQuerystringResponse>(
                in request, CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        string uri = harness.CapturedRequest.RequestUri!.OriginalString;

        // Path should be /search-with-querystring?... (no template params)
        Assert.IsTrue(uri.StartsWith("http://localhost/search-with-querystring?"));
    }

    [TestMethod]
    public async Task GetDocument_AllowReservedPath_DoesNotPercentEncodeReservedChars()
    {
        using TestHarness harness = new(
            HttpStatusCode.OK,
            """{"path":"docs/2024/report","content":"hello"}""");

        GetDocumentRequest request = new(
            JsonElement.ParseValue("""  "docs/2024/report"  """u8));

        await harness.Transport.SendAsync<GetDocumentRequest, GetDocumentResponse>(
                in request, CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        string path = harness.CapturedRequest.RequestUri!.AbsolutePath;

        // With allowReserved=true, the slash is NOT percent-encoded
        Assert.AreEqual("/documents/docs/2024/report", path);
    }

    [TestMethod]
    public async Task GetDocument_AllowReservedPath_PreservesColonAndQuestionMark()
    {
        using TestHarness harness = new(
            HttpStatusCode.OK,
            """{"path":"file:name?v=1","content":"data"}""");

        GetDocumentRequest request = new(
            JsonElement.ParseValue("""  "file:name?v=1"  """u8));

        await harness.Transport.SendAsync<GetDocumentRequest, GetDocumentResponse>(
                in request, CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        string uri = harness.CapturedRequest.RequestUri!.OriginalString;

        // Reserved chars (:, ?) are preserved in the path
        Assert.IsTrue(uri.Contains("file:name?v=1"));
    }

    [TestMethod]
    public async Task GetDocument_AllowReservedPath_NumericPathNotAffected()
    {
        using TestHarness harness = new(
            HttpStatusCode.OK,
            """{"path":"42","content":"data"}""");

        // Use a simple numeric-like string value — no reserved chars to preserve
        GetDocumentRequest request = new(
            JsonElement.ParseValue("""  "doc-42"  """u8));

        await harness.Transport.SendAsync<GetDocumentRequest, GetDocumentResponse>(
                in request, CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        string path = harness.CapturedRequest.RequestUri!.AbsolutePath;
        Assert.AreEqual("/documents/doc-42", path);
    }

    [TestMethod]
    public async Task GetPreferences_CookieStyle_SerializesWithoutPercentEncoding()
    {
        using TestHarness harness = new(
            HttpStatusCode.OK,
            """{"theme":"dark","language":"en"}""");

        GetPreferencesRequest request = new(
            JsonElement.ParseValue("""  "abc=123&special"  """u8));

        await harness.Transport.SendAsync<GetPreferencesRequest, GetPreferencesResponse>(
                in request, CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        string? cookie = harness.CapturedRequest.Headers.TryGetValues("Cookie", out var values)
            ? string.Join("; ", values)
            : null;

        Assert.IsNotNull(cookie);
        Assert.AreEqual("session_token=abc=123&special", cookie);
    }

    [TestMethod]
    public async Task GetPreferences_CookieStyle_MultipleParams()
    {
        using TestHarness harness = new(
            HttpStatusCode.OK,
            """{"theme":"dark","language":"en"}""");

        GetPreferencesRequest request = new(
            JsonElement.ParseValue("""  "token-value"  """u8))
        {
            Theme = JsonElement.ParseValue("""  "dark-mode"  """u8),
        };

        await harness.Transport.SendAsync<GetPreferencesRequest, GetPreferencesResponse>(
                in request, CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        string? cookie = harness.CapturedRequest.Headers.TryGetValues("Cookie", out var values)
            ? string.Join("; ", values)
            : null;

        Assert.IsNotNull(cookie);
        Assert.AreEqual("session_token=token-value; theme=dark-mode", cookie);
    }

    [TestMethod]
    public async Task GetPreferences_CookieStyle_OptionalParamOmitted()
    {
        using TestHarness harness = new(
            HttpStatusCode.OK,
            """{"theme":"default","language":"en"}""");

        GetPreferencesRequest request = new(
            JsonElement.ParseValue("""  "my-session"  """u8));

        await harness.Transport.SendAsync<GetPreferencesRequest, GetPreferencesResponse>(
                in request, CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        string? cookie = harness.CapturedRequest.Headers.TryGetValues("Cookie", out var values)
            ? string.Join("; ", values)
            : null;

        Assert.IsNotNull(cookie);
        Assert.AreEqual("session_token=my-session", cookie);
    }

    [TestMethod]
    public async Task TrackEvent_FormCookieAllowReservedTrue_NoPercentEncoding()
    {
        using TestHarness harness = new(
            HttpStatusCode.Accepted,
            string.Empty);

        TrackEventRequest request = new(
            JsonElement.ParseValue("""  "tid=abc&ref=123"  """u8));

        using var bodyDoc = ParsedJsonDocument<PostTrackingBody>.Parse("""{"event":"click"}""");
        PostTrackingBody body = bodyDoc.RootElement;

        await harness.Transport.SendAsync<TrackEventRequest, PostTrackingBody, TrackEventResponse>(
                in request,
                in body,
                CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        string? cookie = harness.CapturedRequest.Headers.TryGetValues("Cookie", out var values)
            ? string.Join("; ", values)
            : null;

        Assert.IsNotNull(cookie);
        Assert.AreEqual("tracker_id=tid=abc&ref=123", cookie);
    }

    [TestMethod]
    public async Task TrackEvent_FormCookieAllowReservedFalse_PercentEncodesReservedChars()
    {
        using TestHarness harness = new(
            HttpStatusCode.Accepted,
            string.Empty);

        TrackEventRequest request = new(
            JsonElement.ParseValue("""  "simple-id"  """u8))
        {
            RefUrl = JsonElement.ParseValue("""  "https://example.com/page?q=1"  """u8),
        };

        using var bodyDoc = ParsedJsonDocument<PostTrackingBody>.Parse("""{"event":"view"}""");
        PostTrackingBody body = bodyDoc.RootElement;

        await harness.Transport.SendAsync<TrackEventRequest, PostTrackingBody, TrackEventResponse>(
                in request,
                in body,
                CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        string? cookie = harness.CapturedRequest.Headers.TryGetValues("Cookie", out var values)
            ? string.Join("; ", values)
            : null;

        Assert.IsNotNull(cookie);
        Assert.IsTrue(cookie!.Contains("ref_url=https%3A%2F%2Fexample.com%2Fpage%3Fq%3D1"));
    }

    [TestMethod]
    public async Task TrackEvent_FormCookieMixed_CorrectEncodingPerParam()
    {
        using TestHarness harness = new(
            HttpStatusCode.Accepted,
            string.Empty);

        TrackEventRequest request = new(
            JsonElement.ParseValue("""  "a/b=c"  """u8))
        {
            RefUrl = JsonElement.ParseValue("""  "a/b=c"  """u8),
        };

        using var bodyDoc = ParsedJsonDocument<PostTrackingBody>.Parse("""{"event":"test"}""");
        PostTrackingBody body = bodyDoc.RootElement;

        await harness.Transport.SendAsync<TrackEventRequest, PostTrackingBody, TrackEventResponse>(
                in request,
                in body,
                CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        string? cookie = harness.CapturedRequest.Headers.TryGetValues("Cookie", out var values)
            ? string.Join("; ", values)
            : null;

        Assert.IsNotNull(cookie);
        Assert.AreEqual("tracker_id=a/b=c; ref_url=a%2Fb%3Dc", cookie);
    }

    // ── 3.2-specific: $self document identity ────────────────────────────
    [TestMethod]
    public void DocumentIdentityUri_ExposedOnGeneratedInterface()
    {
        // The $self field from the spec is emitted as a static property on all interfaces.
        Assert.AreEqual(
            "https://api.example.com/specs/runtime-tests.json",
            IApiItemsClient.DocumentIdentityUri);
    }

    [TestMethod]
    public void DocumentIdentityUri_ConsistentAcrossAllInterfaces()
    {
        // All tag-based interfaces share the same document identity.
        Assert.AreEqual(IApiItemsClient.DocumentIdentityUri, IApiSearchClient.DocumentIdentityUri);
        Assert.AreEqual(IApiItemsClient.DocumentIdentityUri, IApiOrdersClient.DocumentIdentityUri);
        Assert.AreEqual(IApiItemsClient.DocumentIdentityUri, IApiFilesClient.DocumentIdentityUri);
        Assert.AreEqual(IApiItemsClient.DocumentIdentityUri, IApiTextClient.DocumentIdentityUri);
    }

    // ── 3.2-specific: Security scheme metadata ──────────────────────────
    [TestMethod]
    public void SecuritySchemes_BearerAuth_HasCorrectMetadata()
    {
        Assert.AreEqual("bearerAuth", IApiItemsClient.SecuritySchemes.BearerAuthName);
        Assert.AreEqual("http", IApiItemsClient.SecuritySchemes.BearerAuthType);
        Assert.AreEqual("bearer", IApiItemsClient.SecuritySchemes.BearerAuthScheme);
    }

    [TestMethod]
    public void SecuritySchemes_ApiKeyAuth_HasCorrectMetadata()
    {
        Assert.AreEqual("apiKeyAuth", IApiItemsClient.SecuritySchemes.ApiKeyAuthName);
        Assert.AreEqual("apiKey", IApiItemsClient.SecuritySchemes.ApiKeyAuthType);
        Assert.AreEqual("X-API-Key", IApiItemsClient.SecuritySchemes.ApiKeyAuthKeyName);
        Assert.AreEqual("header", IApiItemsClient.SecuritySchemes.ApiKeyAuthKeyLocation);
    }

    [TestMethod]
    public void SecuritySchemes_LegacyApiKey_IsDeprecated()
    {
        // The LegacyApiKey properties have [Obsolete] — verify they are still accessible.
#pragma warning disable CS0618 // Obsolete
        Assert.AreEqual("legacyApiKey", IApiItemsClient.SecuritySchemes.LegacyApiKeyName);
        Assert.AreEqual("apiKey", IApiItemsClient.SecuritySchemes.LegacyApiKeyType);
        Assert.AreEqual("api_key", IApiItemsClient.SecuritySchemes.LegacyApiKeyKeyName);
        Assert.AreEqual("query", IApiItemsClient.SecuritySchemes.LegacyApiKeyKeyLocation);
#pragma warning restore CS0618
    }

    [TestMethod]
    public void SecuritySchemes_Oauth2_HasMetadataUrl()
    {
        Assert.AreEqual("oauth2", IApiItemsClient.SecuritySchemes.Oauth2Name);
        Assert.AreEqual("oauth2", IApiItemsClient.SecuritySchemes.Oauth2Type);
        Assert.AreEqual(
            "https://auth.example.com/.well-known/oauth-authorization-server",
            IApiItemsClient.SecuritySchemes.Oauth2Oauth2MetadataUrl);
    }

    [TestMethod]
    public void SecuritySchemes_Oauth2_HasDeviceAuthorizationUrl()
    {
        Assert.AreEqual(
            "https://auth.example.com/device",
            IApiItemsClient.SecuritySchemes.Oauth2DeviceAuthorizationUrl);
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

        public TestHarness(
            HttpStatusCode statusCode,
            byte[] binaryResponseBody,
            string binaryContentType)
        {
            this.handler = new MockHandler(statusCode, binaryResponseBody, binaryContentType);
            this.client = new HttpClient(this.handler)
            {
                BaseAddress = new Uri("http://localhost"),
            };
            this.Transport = new HttpClientTransport(this.client);
        }

        public HttpClientTransport Transport { get; }

        public HttpRequestMessage? CapturedRequest => this.handler.CapturedRequest;

        public byte[]? CapturedRequestBody => this.handler.CapturedRequestBody;

        public string? CapturedRequestContentType => this.handler.CapturedRequestContentType;

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
        private readonly byte[]? binaryResponseBody;
        private readonly string? binaryContentType;

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

        public MockHandler(
            HttpStatusCode statusCode,
            byte[] binaryResponseBody,
            string binaryContentType)
        {
            this.statusCode = statusCode;
            this.responseBody = string.Empty;
            this.binaryResponseBody = binaryResponseBody;
            this.binaryContentType = binaryContentType;
            this.InnerHandler = new HttpClientHandler();
        }

        public HttpRequestMessage? CapturedRequest { get; private set; }

        public byte[]? CapturedRequestBody { get; private set; }

        public string? CapturedRequestContentType { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            this.CapturedRequest = request;

            if (request.Content is not null)
            {
                this.CapturedRequestBody = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                this.CapturedRequestContentType = request.Content.Headers.ContentType?.ToString();
            }

            HttpContent content;
            if (this.binaryResponseBody is not null)
            {
                content = new ByteArrayContent(this.binaryResponseBody);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(this.binaryContentType!);
            }
            else if (string.IsNullOrEmpty(this.responseBody))
            {
                content = new ByteArrayContent([]);
            }
            else
            {
                content = new StringContent(this.responseBody, Encoding.UTF8, "application/json");
            }

            HttpResponseMessage response = new(this.statusCode) { Content = content };

            if (this.responseHeaders is not null)
            {
                foreach (var (key, value) in this.responseHeaders)
                {
                    response.Headers.TryAddWithoutValidation(key, value);
                }
            }

            return response;
        }
    }

    // ── Complex param (array/object) E2E tests ────────────────────────────
    // These tests verify serialization of array and object parameters across
    // all OpenAPI locations (path, query, header, cookie) and styles.
    // ── Path array ──────────────────────────────────────────────────────
    [TestMethod]
    public async Task PathArraySimple_SerializesCorrectly()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new PathArraySimpleRequest(
            JsonElement.ParseValue("""["a","b","c"]"""u8));

        await using var response = await harness.Transport
            .SendAsync<PathArraySimpleRequest, PathArraySimpleResponse>(
                in request, CancellationToken.None);

        Assert.AreEqual(
            "http://localhost/complex/path-array-simple/a,b,c",
            harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task PathArrayLabel_SerializesCorrectly()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new PathArrayLabelRequest(
            JsonElement.ParseValue("""["a","b","c"]"""u8));

        await using var response = await harness.Transport
            .SendAsync<PathArrayLabelRequest, PathArrayLabelResponse>(
                in request, CancellationToken.None);

        Assert.AreEqual(
            "http://localhost/complex/path-array-label/.a,b,c",
            harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task PathArrayMatrix_SerializesCorrectly()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new PathArrayMatrixRequest(
            JsonElement.ParseValue("""["a","b","c"]"""u8));

        await using var response = await harness.Transport
            .SendAsync<PathArrayMatrixRequest, PathArrayMatrixResponse>(
                in request, CancellationToken.None);

        Assert.AreEqual(
            "http://localhost/complex/path-array-matrix/;ids=a,b,c",
            harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    // ── Path object ─────────────────────────────────────────────────────
    [TestMethod]
    public async Task PathObjectSimple_SerializesCorrectly()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new PathObjectSimpleRequest(
            JsonElement.ParseValue("""{"width":10,"height":20}"""u8));

        await using var response = await harness.Transport
            .SendAsync<PathObjectSimpleRequest, PathObjectSimpleResponse>(
                in request, CancellationToken.None);

        Assert.AreEqual(
            "http://localhost/complex/path-object-simple/width,10,height,20",
            harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task PathObjectSimpleExplode_SerializesCorrectly()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new PathObjectSimpleExplodeRequest(
            JsonElement.ParseValue("""{"width":10,"height":20}"""u8));

        await using var response = await harness.Transport
            .SendAsync<PathObjectSimpleExplodeRequest, PathObjectSimpleExplodeResponse>(
                in request, CancellationToken.None);

        Assert.AreEqual(
            "http://localhost/complex/path-object-simple-explode/width=10,height=20",
            harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task PathObjectLabel_SerializesCorrectly()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new PathObjectLabelRequest(
            JsonElement.ParseValue("""{"width":10,"height":20}"""u8));

        await using var response = await harness.Transport
            .SendAsync<PathObjectLabelRequest, PathObjectLabelResponse>(
                in request, CancellationToken.None);

        Assert.AreEqual(
            "http://localhost/complex/path-object-label/.width,10,height,20",
            harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task PathObjectLabelExplode_SerializesCorrectly()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new PathObjectLabelExplodeRequest(
            JsonElement.ParseValue("""{"width":10,"height":20}"""u8));

        await using var response = await harness.Transport
            .SendAsync<PathObjectLabelExplodeRequest, PathObjectLabelExplodeResponse>(
                in request, CancellationToken.None);

        Assert.AreEqual(
            "http://localhost/complex/path-object-label-explode/.width=10.height=20",
            harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task PathObjectMatrix_SerializesCorrectly()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new PathObjectMatrixRequest(
            JsonElement.ParseValue("""{"width":10,"height":20}"""u8));

        await using var response = await harness.Transport
            .SendAsync<PathObjectMatrixRequest, PathObjectMatrixResponse>(
                in request, CancellationToken.None);

        Assert.AreEqual(
            "http://localhost/complex/path-object-matrix/;dims=width,10,height,20",
            harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task PathObjectMatrixExplode_SerializesCorrectly()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new PathObjectMatrixExplodeRequest(
            JsonElement.ParseValue("""{"width":10,"height":20}"""u8));

        await using var response = await harness.Transport
            .SendAsync<PathObjectMatrixExplodeRequest, PathObjectMatrixExplodeResponse>(
                in request, CancellationToken.None);

        Assert.AreEqual(
            "http://localhost/complex/path-object-matrix-explode/;width=10;height=20",
            harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    // ── Query array ─────────────────────────────────────────────────────
    [TestMethod]
    public async Task QueryArrayExplode_SerializesCorrectly()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new QueryArrayExplodeRequest(
            JsonElement.ParseValue("""["a","b","c"]"""u8));

        await using var response = await harness.Transport
            .SendAsync<QueryArrayExplodeRequest, QueryArrayExplodeResponse>(
                in request, CancellationToken.None);

        Assert.AreEqual(
            "http://localhost/complex/query-array-explode?colors=a&colors=b&colors=c",
            harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task QueryArrayNonexplode_SerializesCorrectly()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new QueryArrayNonexplodeRequest(
            JsonElement.ParseValue("""["a","b","c"]"""u8));

        await using var response = await harness.Transport
            .SendAsync<QueryArrayNonexplodeRequest, QueryArrayNonexplodeResponse>(
                in request, CancellationToken.None);

        Assert.AreEqual(
            "http://localhost/complex/query-array-nonexplode?colors=a,b,c",
            harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task QueryArraySpace_SerializesCorrectly()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new QueryArraySpaceRequest(
            JsonElement.ParseValue("""["a","b","c"]"""u8));

        await using var response = await harness.Transport
            .SendAsync<QueryArraySpaceRequest, QueryArraySpaceResponse>(
                in request, CancellationToken.None);

        Assert.AreEqual(
            "http://localhost/complex/query-array-space?colors=a%20b%20c",
            harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task QueryArrayPipe_SerializesCorrectly()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new QueryArrayPipeRequest(
            JsonElement.ParseValue("""["a","b","c"]"""u8));

        await using var response = await harness.Transport
            .SendAsync<QueryArrayPipeRequest, QueryArrayPipeResponse>(
                in request, CancellationToken.None);

        Assert.AreEqual(
            "http://localhost/complex/query-array-pipe?colors=a%7Cb%7Cc",
            harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    // ── Query object ────────────────────────────────────────────────────
    [TestMethod]
    public async Task QueryObjectExplode_SerializesCorrectly()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new QueryObjectExplodeRequest(
            JsonElement.ParseValue("""{"width":10,"height":20}"""u8));

        await using var response = await harness.Transport
            .SendAsync<QueryObjectExplodeRequest, QueryObjectExplodeResponse>(
                in request, CancellationToken.None);

        Assert.AreEqual(
            "http://localhost/complex/query-object-explode?width=10&height=20",
            harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task QueryObjectNonexplode_SerializesCorrectly()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new QueryObjectNonexplodeRequest(
            JsonElement.ParseValue("""{"width":10,"height":20}"""u8));

        await using var response = await harness.Transport
            .SendAsync<QueryObjectNonexplodeRequest, QueryObjectNonexplodeResponse>(
                in request, CancellationToken.None);

        Assert.AreEqual(
            "http://localhost/complex/query-object-nonexplode?dims=width,10,height,20",
            harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    [TestMethod]
    public async Task QueryObjectDeep_SerializesCorrectly()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new QueryObjectDeepRequest(
            JsonElement.ParseValue("""{"width":10,"height":20}"""u8));

        await using var response = await harness.Transport
            .SendAsync<QueryObjectDeepRequest, QueryObjectDeepResponse>(
                in request, CancellationToken.None);

        Assert.AreEqual(
            "http://localhost/complex/query-object-deep?dims%5Bwidth%5D=10&dims%5Bheight%5D=20",
            harness.CapturedRequest!.RequestUri!.OriginalString);
    }

    // ── Header array / object ───────────────────────────────────────────
    [TestMethod]
    public async Task HeaderArray_SerializesCorrectly()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new HeaderArrayRequest(
            JsonElement.ParseValue("""["a","b","c"]"""u8));

        await using var response = await harness.Transport
            .SendAsync<HeaderArrayRequest, HeaderArrayResponse>(
                in request, CancellationToken.None);

        Assert.IsTrue(harness.CapturedRequest!.Headers.Contains("X-Tags"));
        Assert.AreEqual(
            "a,b,c",
            harness.CapturedRequest.Headers.GetValues("X-Tags").First());
    }

    [TestMethod]
    public async Task HeaderObject_SerializesCorrectly()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new HeaderObjectRequest(
            JsonElement.ParseValue("""{"width":10,"height":20}"""u8));

        await using var response = await harness.Transport
            .SendAsync<HeaderObjectRequest, HeaderObjectResponse>(
                in request, CancellationToken.None);

        Assert.IsTrue(harness.CapturedRequest!.Headers.Contains("X-Dims"));
        Assert.AreEqual(
            "width,10,height,20",
            harness.CapturedRequest.Headers.GetValues("X-Dims").First());
    }

    [TestMethod]
    public async Task HeaderObjectExplode_SerializesCorrectly()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new HeaderObjectExplodeRequest(
            JsonElement.ParseValue("""{"width":10,"height":20}"""u8));

        await using var response = await harness.Transport
            .SendAsync<HeaderObjectExplodeRequest, HeaderObjectExplodeResponse>(
                in request, CancellationToken.None);

        Assert.IsTrue(harness.CapturedRequest!.Headers.Contains("X-Dims"));
        Assert.AreEqual(
            "width=10,height=20",
            harness.CapturedRequest.Headers.GetValues("X-Dims").First());
    }

    // ── Cookie array / object ───────────────────────────────────────────
    [TestMethod]
    public async Task CookieArray_SerializesCorrectly()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new CookieArrayRequest(
            JsonElement.ParseValue("""["a","b","c"]"""u8));

        await using var response = await harness.Transport
            .SendAsync<CookieArrayRequest, CookieArrayResponse>(
                in request, CancellationToken.None);

        Assert.IsTrue(harness.CapturedRequest!.Headers.Contains("Cookie"));
        Assert.AreEqual(
            "colors=a; colors=b; colors=c",
            harness.CapturedRequest.Headers.GetValues("Cookie").First());
    }

    [TestMethod]
    public async Task CookieArrayNonexplode_SerializesCorrectly()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new CookieArrayNonexplodeRequest(
            JsonElement.ParseValue("""["a","b","c"]"""u8));

        await using var response = await harness.Transport
            .SendAsync<CookieArrayNonexplodeRequest, CookieArrayNonexplodeResponse>(
                in request, CancellationToken.None);

        Assert.IsTrue(harness.CapturedRequest!.Headers.Contains("Cookie"));
        Assert.AreEqual(
            "colors=a,b,c",
            harness.CapturedRequest.Headers.GetValues("Cookie").First());
    }

    [TestMethod]
    public async Task CookieObject_SerializesCorrectly()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new CookieObjectRequest(
            JsonElement.ParseValue("""{"width":10,"height":20}"""u8));

        await using var response = await harness.Transport
            .SendAsync<CookieObjectRequest, CookieObjectResponse>(
                in request, CancellationToken.None);

        Assert.IsTrue(harness.CapturedRequest!.Headers.Contains("Cookie"));
        Assert.AreEqual(
            "width=10; height=20",
            harness.CapturedRequest.Headers.GetValues("Cookie").First());
    }

    [TestMethod]
    public async Task CookieObjectNonexplode_SerializesCorrectly()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{}""");

        var request = new CookieObjectNonexplodeRequest(
            JsonElement.ParseValue("""{"width":10,"height":20}"""u8));

        await using var response = await harness.Transport
            .SendAsync<CookieObjectNonexplodeRequest, CookieObjectNonexplodeResponse>(
                in request, CancellationToken.None);

        Assert.IsTrue(harness.CapturedRequest!.Headers.Contains("Cookie"));
        Assert.AreEqual(
            "prefs=width,10,height,20",
            harness.CapturedRequest.Headers.GetValues("Cookie").First());
    }

    // ── Throwing stub tests ─────────────────────────────────────────────
    // Operations that lack a given parameter location emit throwing stubs.
    // These tests verify that calling the stub throws InvalidOperationException.
    [TestMethod]
    public void CreateItemRequest_WriteResolvedPath_ThrowsWhenNoPathParams()
    {
        CreateItemRequest request = default;
        Assert.IsFalse(CreateItemRequest.HasPathParameters);
        Assert.ThrowsExactly<InvalidOperationException>(() => request.WriteResolvedPath(new ArrayBufferWriter<byte>()));
    }

    [TestMethod]
    public void CreateItemRequest_WriteQueryString_ThrowsWhenNoQueryParams()
    {
        CreateItemRequest request = default;
        Assert.IsFalse(CreateItemRequest.HasQueryParameters);
        Assert.ThrowsExactly<InvalidOperationException>(() => request.WriteQueryString(new ArrayBufferWriter<byte>()));
    }

    [TestMethod]
    public void CreateItemRequest_WriteHeaders_EmitsOnlyAcceptHeader()
    {
        CreateItemRequest request = default;
        Assert.IsTrue(CreateItemRequest.HasHeaderParameters);
        var headers = new List<(string Name, string Value)>();
        request.WriteHeaders(
            static (ReadOnlySpan<byte> name, ReadOnlySpan<byte> value, List<(string, string)> state) =>
                state.Add((Encoding.UTF8.GetString(name), Encoding.UTF8.GetString(value))),
            headers);

        Assert.AreEqual(1, headers.Count);
        Assert.AreEqual("Accept", headers[0].Name);
        Assert.AreEqual("application/json", headers[0].Value);
    }

    [TestMethod]
    public void CreateItemRequest_WriteCookies_ThrowsWhenNoCookieParams()
    {
        CreateItemRequest request = default;
        Assert.IsFalse(CreateItemRequest.HasCookieParameters);
        Assert.ThrowsExactly<InvalidOperationException>(() => request.WriteCookies(new ArrayBufferWriter<byte>()));
    }

    [TestMethod]
    public void DeleteItemRequest_WriteQueryString_ThrowsWhenNoQueryParams()
    {
        DeleteItemRequest request = new(JsonElement.ParseValue("\"item-1\""u8));
        Assert.IsFalse(DeleteItemRequest.HasQueryParameters);
        Assert.ThrowsExactly<InvalidOperationException>(() => request.WriteQueryString(new ArrayBufferWriter<byte>()));
    }

    [TestMethod]
    public void GetByFlagRequest_WriteCookies_ThrowsWhenNoCookieParams()
    {
        GetByFlagRequest request = new(
            JsonElement.ParseValue("true"u8),
            JsonElement.ParseValue("\"trace-1\""u8),
            JsonElement.ParseValue("1"u8));
        Assert.IsFalse(GetByFlagRequest.HasCookieParameters);
        Assert.ThrowsExactly<InvalidOperationException>(() => request.WriteCookies(new ArrayBufferWriter<byte>()));
    }

    [TestMethod]
    public void SearchRequest_WriteResolvedPath_ThrowsWhenNoPathParams()
    {
        SearchRequest request = new(JsonElement.ParseValue("\"test\""u8));
        Assert.IsFalse(SearchRequest.HasPathParameters);
        Assert.ThrowsExactly<InvalidOperationException>(() => request.WriteResolvedPath(new ArrayBufferWriter<byte>()));
    }

    [TestMethod]
    public void GetSessionProfileRequest_WriteHeaders_EmitsOnlyAcceptHeader()
    {
        GetSessionProfileRequest request = new(JsonElement.ParseValue("\"session-id\""u8));
        Assert.IsTrue(GetSessionProfileRequest.HasHeaderParameters);
        var headers = new List<(string Name, string Value)>();
        request.WriteHeaders(
            static (ReadOnlySpan<byte> name, ReadOnlySpan<byte> value, List<(string, string)> state) =>
                state.Add((Encoding.UTF8.GetString(name), Encoding.UTF8.GetString(value))),
            headers);

        Assert.AreEqual(1, headers.Count);
        Assert.AreEqual("Accept", headers[0].Name);
        Assert.AreEqual("application/json", headers[0].Value);
    }

    // ── Throwing stub tests for complex-params operations ───────────────
    [TestMethod]
    public void PathArraySimpleRequest_WriteQueryString_ThrowsWhenNoQueryParams()
    {
        PathArraySimpleRequest request = new(JsonElement.ParseValue("""["a"]"""u8));
        Assert.IsFalse(PathArraySimpleRequest.HasQueryParameters);
        Assert.ThrowsExactly<InvalidOperationException>(() => request.WriteQueryString(new ArrayBufferWriter<byte>()));
    }

    [TestMethod]
    public void PathArraySimpleRequest_WriteCookies_ThrowsWhenNoCookieParams()
    {
        PathArraySimpleRequest request = new(JsonElement.ParseValue("""["a"]"""u8));
        Assert.IsFalse(PathArraySimpleRequest.HasCookieParameters);
        Assert.ThrowsExactly<InvalidOperationException>(() => request.WriteCookies(new ArrayBufferWriter<byte>()));
    }

    [TestMethod]
    public void QueryArrayExplodeRequest_WriteResolvedPath_ThrowsWhenNoPathParams()
    {
        QueryArrayExplodeRequest request = new(JsonElement.ParseValue("""["a"]"""u8));
        Assert.IsFalse(QueryArrayExplodeRequest.HasPathParameters);
        Assert.ThrowsExactly<InvalidOperationException>(() => request.WriteResolvedPath(new ArrayBufferWriter<byte>()));
    }

    [TestMethod]
    public void HeaderArrayRequest_WriteCookies_ThrowsWhenNoCookieParams()
    {
        HeaderArrayRequest request = new(JsonElement.ParseValue("""["a"]"""u8));
        Assert.IsFalse(HeaderArrayRequest.HasCookieParameters);
        Assert.ThrowsExactly<InvalidOperationException>(() => request.WriteCookies(new ArrayBufferWriter<byte>()));
    }

    [TestMethod]
    public void CookieArrayRequest_WriteHeaders_EmitsOnlyAcceptHeader()
    {
        CookieArrayRequest request = new(JsonElement.ParseValue("""["a"]"""u8));
        Assert.IsTrue(CookieArrayRequest.HasHeaderParameters);
        var headers = new List<(string Name, string Value)>();
        request.WriteHeaders(
            static (ReadOnlySpan<byte> name, ReadOnlySpan<byte> value, List<(string, string)> state) =>
                state.Add((Encoding.UTF8.GetString(name), Encoding.UTF8.GetString(value))),
            headers);

        Assert.AreEqual(1, headers.Count);
        Assert.AreEqual("Accept", headers[0].Name);
        Assert.AreEqual("application/json", headers[0].Value);
    }

    // ── Stream and vendor JSON E2E tests ──────────────────────────────────
    // These tests verify octet-stream (binary) request/response bodies,
    // mixed (stream + JSON) responses, and vendor +json content types.
    [TestMethod]
    public async Task DownloadFile_200_ReturnsStream()
    {
        byte[] binaryData = [0x00, 0x01, 0x02, 0xFF, 0xFE];
        using var harness = new TestHarness(HttpStatusCode.OK, binaryData, "application/octet-stream");

        await using DownloadFileResponse response = await harness.Transport
            .SendAsync<DownloadFileRequest, DownloadFileResponse>(
                default(DownloadFileRequest),
                CancellationToken.None);

        Assert.AreEqual(200, response.StatusCode);
        Assert.IsTrue(response.IsSuccess);
        Assert.IsTrue(response.TryGetOkStream(out Stream? stream));
        Assert.IsNotNull(stream);

        using MemoryStream ms = new();
        await stream.CopyToAsync(ms);
        CollectionAssert.AreEqual(binaryData, ms.ToArray());
    }

    [TestMethod]
    public async Task DownloadFile_200_MatchResult_CallsOkHandler()
    {
        byte[] binaryData = [0xCA, 0xFE];
        using var harness = new TestHarness(HttpStatusCode.OK, binaryData, "application/octet-stream");

        await using DownloadFileResponse response = await harness.Transport
            .SendAsync<DownloadFileRequest, DownloadFileResponse>(
                default(DownloadFileRequest),
                CancellationToken.None);

        string result = response.MatchResult(
            matchOkStream: stream => stream is not null ? "got-stream" : "null-stream",
            matchDefault: statusCode => $"unmatched-{statusCode}");

        Assert.AreEqual("got-stream", result);
    }

    [TestMethod]
    public async Task DownloadFile_404_MatchResult_CallsDefaultHandler()
    {
        using var harness = new TestHarness(HttpStatusCode.NotFound, string.Empty);

        await using DownloadFileResponse response = await harness.Transport
            .SendAsync<DownloadFileRequest, DownloadFileResponse>(
                default(DownloadFileRequest),
                CancellationToken.None);

        Assert.AreEqual(404, response.StatusCode);
        Assert.IsFalse(response.IsSuccess);
        Assert.IsFalse(response.TryGetOkStream(out _));

        string result = response.MatchResult(
            matchOkStream: _ => "ok",
            matchDefault: statusCode => $"default-{statusCode}");

        Assert.AreEqual("default-404", result);
    }

    [TestMethod]
    public async Task DownloadFile_RequestPathIsCorrect()
    {
        byte[] binaryData = [0x01];
        using var harness = new TestHarness(HttpStatusCode.OK, binaryData, "application/octet-stream");

        await using DownloadFileResponse response = await harness.Transport
            .SendAsync<DownloadFileRequest, DownloadFileResponse>(
                default(DownloadFileRequest),
                CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        Assert.AreEqual("http://localhost/files/download", harness.CapturedRequest.RequestUri!.OriginalString);
        Assert.AreEqual(HttpMethod.Get, harness.CapturedRequest.Method);
    }

    [TestMethod]
    public async Task UploadFile_201_ParsesJsonResponse()
    {
        using var harness = new TestHarness(HttpStatusCode.Created, """{"id":"f-123"}""");

        byte[] fileContent = [0xDE, 0xAD, 0xBE, 0xEF];
        using MemoryStream uploadStream = new(fileContent);

        await using UploadFileResponse response = await harness.Transport
            .SendAsync<UploadFileRequest, UploadFileResponse>(
                default(UploadFileRequest),
                uploadStream,
                "application/octet-stream",
                CancellationToken.None);

        Assert.AreEqual(201, response.StatusCode);
        Assert.IsTrue(response.IsSuccess);
        Assert.IsTrue(response.TryGetCreated(out var body));
        Assert.AreEqual("f-123", (string)body.Id);
    }

    [TestMethod]
    public async Task UploadFile_RequestBodyIsSentCorrectly()
    {
        using var harness = new TestHarness(HttpStatusCode.Created, """{"id":"x"}""");

        byte[] fileContent = [0x01, 0x02, 0x03, 0x04, 0x05];
        using MemoryStream uploadStream = new(fileContent);

        await using UploadFileResponse response = await harness.Transport
            .SendAsync<UploadFileRequest, UploadFileResponse>(
                default(UploadFileRequest),
                uploadStream,
                "application/octet-stream",
                CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        Assert.AreEqual("http://localhost/files/upload", harness.CapturedRequest.RequestUri!.OriginalString);
        Assert.AreEqual(HttpMethod.Post, harness.CapturedRequest.Method);
        Assert.AreEqual("application/octet-stream", harness.CapturedRequestContentType);
        CollectionAssert.AreEqual(fileContent, harness.CapturedRequestBody);
    }

    [TestMethod]
    public async Task DownloadMixed_200_ReturnsStream()
    {
        byte[] binaryData = [0xAA, 0xBB, 0xCC];
        using var harness = new TestHarness(HttpStatusCode.OK, binaryData, "application/octet-stream");

        await using DownloadMixedResponse response = await harness.Transport
            .SendAsync<DownloadMixedRequest, DownloadMixedResponse>(
                default(DownloadMixedRequest),
                CancellationToken.None);

        Assert.AreEqual(200, response.StatusCode);
        Assert.IsTrue(response.IsSuccess);
        Assert.IsTrue(response.TryGetOkStream(out Stream? stream));
        Assert.IsNotNull(stream);

        using MemoryStream ms = new();
        await stream.CopyToAsync(ms);
        CollectionAssert.AreEqual(binaryData, ms.ToArray());
    }

    [TestMethod]
    public async Task DownloadMixed_404_ReturnsJsonErrorBody()
    {
        using var harness = new TestHarness(HttpStatusCode.NotFound, """{"error":"File not found"}""");

        await using DownloadMixedResponse response = await harness.Transport
            .SendAsync<DownloadMixedRequest, DownloadMixedResponse>(
                default(DownloadMixedRequest),
                CancellationToken.None);

        Assert.AreEqual(404, response.StatusCode);
        Assert.IsFalse(response.IsSuccess);
        Assert.IsFalse(response.TryGetOkStream(out _));
        Assert.IsTrue(response.TryGetNotFound(out var errorBody));
        Assert.AreEqual("File not found", (string)errorBody.Error);
    }

    [TestMethod]
    public async Task DownloadMixed_200_MatchResult_CallsStreamHandler()
    {
        byte[] binaryData = [0x01, 0x02];
        using var harness = new TestHarness(HttpStatusCode.OK, binaryData, "application/octet-stream");

        await using DownloadMixedResponse response = await harness.Transport
            .SendAsync<DownloadMixedRequest, DownloadMixedResponse>(
                default(DownloadMixedRequest),
                CancellationToken.None);

        string result = response.MatchResult(
            matchOkStream: stream => stream is not null ? "stream" : "null",
            matchNotFound: error => $"error-{(string)error.Error}",
            matchDefault: statusCode => $"default-{statusCode}");

        Assert.AreEqual("stream", result);
    }

    [TestMethod]
    public async Task DownloadMixed_404_MatchResult_CallsNotFoundHandler()
    {
        using var harness = new TestHarness(HttpStatusCode.NotFound, """{"error":"gone"}""");

        await using DownloadMixedResponse response = await harness.Transport
            .SendAsync<DownloadMixedRequest, DownloadMixedResponse>(
                default(DownloadMixedRequest),
                CancellationToken.None);

        string result = response.MatchResult(
            matchOkStream: _ => "ok",
            matchNotFound: error => $"not-found-{(string)error.Error}",
            matchDefault: statusCode => $"default-{statusCode}");

        Assert.AreEqual("not-found-gone", result);
    }

    [TestMethod]
    public async Task DownloadMixed_500_MatchResult_CallsDefaultHandler()
    {
        using var harness = new TestHarness(HttpStatusCode.InternalServerError, string.Empty);

        await using DownloadMixedResponse response = await harness.Transport
            .SendAsync<DownloadMixedRequest, DownloadMixedResponse>(
                default(DownloadMixedRequest),
                CancellationToken.None);

        string result = response.MatchResult(
            matchOkStream: _ => "ok",
            matchNotFound: _ => "not-found",
            matchDefault: statusCode => $"default-{statusCode}");

        Assert.AreEqual("default-500", result);
    }

    [TestMethod]
    public async Task GetVendorJson_200_ParsesBody()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"data":"vendor-response"}""");

        await using GetVendorJsonResponse response = await harness.Transport
            .SendAsync<GetVendorJsonRequest, GetVendorJsonResponse>(
                default(GetVendorJsonRequest),
                CancellationToken.None);

        Assert.AreEqual(200, response.StatusCode);
        Assert.IsTrue(response.IsSuccess);
        Assert.IsTrue(response.TryGetOk(out var body));
        Assert.AreEqual("vendor-response", (string)body.Data);
    }

    [TestMethod]
    public async Task GetVendorJson_RequestPathIsCorrect()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"data":"x"}""");

        await using GetVendorJsonResponse response = await harness.Transport
            .SendAsync<GetVendorJsonRequest, GetVendorJsonResponse>(
                default(GetVendorJsonRequest),
                CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        Assert.AreEqual("http://localhost/files/vendor-json", harness.CapturedRequest.RequestUri!.OriginalString);
        Assert.AreEqual(HttpMethod.Get, harness.CapturedRequest.Method);
    }

    [TestMethod]
    public async Task GetVendorJson_200_MatchResult_CallsOkHandler()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"data":"d"}""");

        await using GetVendorJsonResponse response = await harness.Transport
            .SendAsync<GetVendorJsonRequest, GetVendorJsonResponse>(
                default(GetVendorJsonRequest),
                CancellationToken.None);

        string result = response.MatchResult(
            matchOk: body => $"data={body.Data}",
            matchDefault: statusCode => $"default-{statusCode}");

        Assert.AreEqual("data=d", result);
    }

    // ── Text/plain E2E tests ──────────────────────────────────────────────
    [TestMethod]
    public async Task EchoText_200_ReturnsText()
    {
        byte[] textBytes = Encoding.UTF8.GetBytes("Hello, world!");
        using var harness = new TestHarness(HttpStatusCode.OK, textBytes, "text/plain");

        using MemoryStream requestBody = new(Encoding.UTF8.GetBytes("Hello, world!"));

        await using EchoTextResponse response = await harness.Transport
            .SendAsync<EchoTextRequest, EchoTextResponse>(
                default(EchoTextRequest),
                requestBody,
                "text/plain",
                CancellationToken.None);

        Assert.AreEqual(200, response.StatusCode);
        Assert.IsTrue(response.IsSuccess);
        Assert.IsTrue(response.TryGetOkString(out string? text));
        Assert.AreEqual("Hello, world!", text);
    }

    [TestMethod]
    public async Task EchoText_OkTextAndUtf8BytesAreConsistent()
    {
        const string expected = "Héllo wörld — emoji 🎉";
        byte[] textBytes = Encoding.UTF8.GetBytes(expected);
        using var harness = new TestHarness(HttpStatusCode.OK, textBytes, "text/plain");

        await using EchoTextResponse response = await harness.Transport
            .SendAsync<EchoTextRequest, EchoTextResponse>(
                default(EchoTextRequest),
                new MemoryStream(textBytes),
                "text/plain",
                CancellationToken.None);

        Assert.AreEqual(expected, response.OkText);

        ReadOnlySpan<byte> utf8 = response.OkUtf8Bytes;
        Assert.AreEqual(textBytes.Length, utf8.Length);
        Assert.IsTrue(utf8.SequenceEqual(textBytes));
    }

    [TestMethod]
    public async Task EchoText_RequestBodyIsSentAsTextPlain()
    {
        byte[] textBytes = Encoding.UTF8.GetBytes("test input");
        using var harness = new TestHarness(HttpStatusCode.OK, textBytes, "text/plain");

        using MemoryStream requestBody = new(Encoding.UTF8.GetBytes("test input"));

        await using EchoTextResponse response = await harness.Transport
            .SendAsync<EchoTextRequest, EchoTextResponse>(
                default(EchoTextRequest),
                requestBody,
                "text/plain",
                CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        Assert.AreEqual("http://localhost/text/echo", harness.CapturedRequest.RequestUri!.OriginalString);
        Assert.AreEqual(HttpMethod.Post, harness.CapturedRequest.Method);
        Assert.AreEqual("text/plain", harness.CapturedRequestContentType);
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("test input"), harness.CapturedRequestBody);
    }

    [TestMethod]
    public async Task EchoText_AcceptHeaderIsTextPlain()
    {
        byte[] textBytes = Encoding.UTF8.GetBytes("x");
        using var harness = new TestHarness(HttpStatusCode.OK, textBytes, "text/plain");

        await using EchoTextResponse response = await harness.Transport
            .SendAsync<EchoTextRequest, EchoTextResponse>(
                default(EchoTextRequest),
                new MemoryStream(textBytes),
                "text/plain",
                CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        Assert.IsTrue(
            harness.CapturedRequest.Headers.Accept.Any(a => a.MediaType == "text/plain"),
            "Accept header should include text/plain");
    }

    [TestMethod]
    public async Task EchoText_404_TryGetOkStringReturnsFalse()
    {
        using var harness = new TestHarness(HttpStatusCode.NotFound, string.Empty);

        await using EchoTextResponse response = await harness.Transport
            .SendAsync<EchoTextRequest, EchoTextResponse>(
                default(EchoTextRequest),
                new MemoryStream([]),
                "text/plain",
                CancellationToken.None);

        Assert.AreEqual(404, response.StatusCode);
        Assert.IsFalse(response.TryGetOkString(out _));
        Assert.IsNull(response.OkText);
    }

    [TestMethod]
    public async Task EchoText_200_MatchResult_CallsStringHandler()
    {
        byte[] textBytes = Encoding.UTF8.GetBytes("matched");
        using var harness = new TestHarness(HttpStatusCode.OK, textBytes, "text/plain");

        await using EchoTextResponse response = await harness.Transport
            .SendAsync<EchoTextRequest, EchoTextResponse>(
                default(EchoTextRequest),
                new MemoryStream(textBytes),
                "text/plain",
                CancellationToken.None);

        string result = response.MatchResult(
            matchOkString: text => $"text={text}",
            matchDefault: statusCode => $"default-{statusCode}");

        Assert.AreEqual("text=matched", result);
    }

    [TestMethod]
    public async Task TextToJson_200_ParsesJsonResponse()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"parsed":"hello"}""");

        using MemoryStream requestBody = new(Encoding.UTF8.GetBytes("hello"));

        await using TextToJsonResponse response = await harness.Transport
            .SendAsync<TextToJsonRequest, TextToJsonResponse>(
                default(TextToJsonRequest),
                requestBody,
                "text/plain",
                CancellationToken.None);

        Assert.AreEqual(200, response.StatusCode);
        Assert.IsTrue(response.TryGetOk(out var body));
        Assert.AreEqual("hello", (string)body.Parsed);
    }

    [TestMethod]
    public async Task TextToJson_RequestIsSentAsTextPlain()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"parsed":"x"}""");

        using MemoryStream requestBody = new(Encoding.UTF8.GetBytes("input text"));

        await using TextToJsonResponse response = await harness.Transport
            .SendAsync<TextToJsonRequest, TextToJsonResponse>(
                default(TextToJsonRequest),
                requestBody,
                "text/plain",
                CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        Assert.AreEqual(HttpMethod.Post, harness.CapturedRequest.Method);
        Assert.AreEqual("text/plain", harness.CapturedRequestContentType);
    }

    [TestMethod]
    public async Task GetTextOrJson_200_Json_ParsesBody()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"value":"json-data"}""");

        await using GetTextOrJsonResponse response = await harness.Transport
            .SendAsync<GetTextOrJsonRequest, GetTextOrJsonResponse>(
                default(GetTextOrJsonRequest),
                CancellationToken.None);

        Assert.AreEqual(200, response.StatusCode);
        Assert.IsTrue(response.TryGetOk(out var body));
        Assert.AreEqual("json-data", (string)body.Value);
    }

    [TestMethod]
    public async Task GetTextOrJson_200_Text_ReturnsString()
    {
        byte[] textBytes = Encoding.UTF8.GetBytes("plain text response");
        using var harness = new TestHarness(HttpStatusCode.OK, textBytes, "text/plain");

        await using GetTextOrJsonResponse response = await harness.Transport
            .SendAsync<GetTextOrJsonRequest, GetTextOrJsonResponse>(
                default(GetTextOrJsonRequest),
                CancellationToken.None);

        Assert.AreEqual(200, response.StatusCode);
        Assert.IsTrue(response.TryGetOkString(out string? text));
        Assert.AreEqual("plain text response", text);
    }

    [TestMethod]
    public async Task GetTextOrJson_404_ParsesJsonError()
    {
        using var harness = new TestHarness(HttpStatusCode.NotFound, """{"error":"not here"}""");

        await using GetTextOrJsonResponse response = await harness.Transport
            .SendAsync<GetTextOrJsonRequest, GetTextOrJsonResponse>(
                default(GetTextOrJsonRequest),
                CancellationToken.None);

        Assert.AreEqual(404, response.StatusCode);
        Assert.IsFalse(response.IsSuccess);
        Assert.IsTrue(response.TryGetNotFound(out var errorBody));
        Assert.AreEqual("not here", (string)errorBody.Error);
    }

    [TestMethod]
    public async Task GetTextOrJson_200_MatchResult_Json_CallsOkHandler()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"value":"matched-json"}""");

        await using GetTextOrJsonResponse response = await harness.Transport
            .SendAsync<GetTextOrJsonRequest, GetTextOrJsonResponse>(
                default(GetTextOrJsonRequest),
                CancellationToken.None);

        string result = response.MatchResult(
            matchOk: body => $"json={body.Value}",
            matchOkString: text => $"text={text}",
            matchNotFound: error => $"error={error.Error}",
            matchDefault: statusCode => $"default-{statusCode}");

        Assert.AreEqual("json=matched-json", result);
    }

    [TestMethod]
    public async Task GetTextOrJson_200_MatchResult_Text_CallsStringHandler()
    {
        byte[] textBytes = Encoding.UTF8.GetBytes("plain matched");
        using var harness = new TestHarness(HttpStatusCode.OK, textBytes, "text/plain");

        await using GetTextOrJsonResponse response = await harness.Transport
            .SendAsync<GetTextOrJsonRequest, GetTextOrJsonResponse>(
                default(GetTextOrJsonRequest),
                CancellationToken.None);

        string result = response.MatchResult(
            matchOk: body => $"json={body.Value}",
            matchOkString: text => $"text={text}",
            matchNotFound: error => $"error={error.Error}",
            matchDefault: statusCode => $"default-{statusCode}");

        Assert.AreEqual("text=plain matched", result);
    }

    [TestMethod]
    public async Task GetTextOrJson_AcceptHeader_ListsBothTypes()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"value":"x"}""");

        await using GetTextOrJsonResponse response = await harness.Transport
            .SendAsync<GetTextOrJsonRequest, GetTextOrJsonResponse>(
                default(GetTextOrJsonRequest),
                CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        var acceptTypes = harness.CapturedRequest.Headers.Accept
            .Select(a => a.MediaType)
            .ToList();
        CollectionAssert.Contains(acceptTypes, "application/json");
        CollectionAssert.Contains(acceptTypes, "text/plain");
    }

    // ── Validate() E2E tests ────────────────────────────────────────
    [TestMethod]
    public void Validate_ValidRequiredParam_DoesNotThrow()
    {
        GetItemRequest request = new(JsonString.ParseValue("\"abc\""u8));
        request.Validate();
    }

    [TestMethod]
    public void Validate_ValidOptionalParam_DoesNotThrow()
    {
        GetItemRequest request = new(JsonString.ParseValue("\"abc\""u8))
        {
            Limit = JsonInt32.ParseValue("10"u8),
        };

        request.Validate();
    }

    [TestMethod]
    public void Validate_NoneMode_SkipsValidation()
    {
        // Even with a default-constructed required param (likely invalid), None should not throw.
        GetItemRequest request = default;
        request.Validate(ValidationMode.None);
    }

    [TestMethod]
    public void Validate_InvalidRequiredParam_BasicMode_ThrowsArgumentException()
    {
        // Parse a JSON boolean as a JsonString — schema validation fails because it's not a string.
        GetItemRequest request = new(JsonString.ParseValue("true"u8));

        ArgumentException ex = Assert.ThrowsExactly<ArgumentException>(() =>
            request.Validate(ValidationMode.Basic));

        StringAssert.Contains(ex.Message, "itemId");
    }

    [TestMethod]
    public void Validate_InvalidRequiredParam_DetailedMode_ThrowsWithJsonDiagnostics()
    {
        GetItemRequest request = new(JsonString.ParseValue("true"u8));

        ArgumentException ex = Assert.ThrowsExactly<ArgumentException>(() =>
            request.Validate(ValidationMode.Detailed));

        StringAssert.Contains(ex.Message, "itemId");
        StringAssert.Contains(ex.Message, "evaluationPath");
        StringAssert.Contains(ex.Message, "instanceLocation");
    }

    [TestMethod]
    public void Validate_InvalidOptionalParam_BasicMode_ThrowsArgumentException()
    {
        // Parse a JSON string as a JsonInt32 — schema validation fails because it's not an integer.
        GetItemRequest request = new(JsonString.ParseValue("\"abc\""u8))
        {
            Limit = JsonInt32.ParseValue("\"not-a-number\""u8),
        };

        ArgumentException ex = Assert.ThrowsExactly<ArgumentException>(() =>
            request.Validate(ValidationMode.Basic));

        StringAssert.Contains(ex.Message, "limit");
    }

    // ── Response Validate() E2E tests ────────────────────────────────────
    [TestMethod]
    public async Task ResponseValidate_ValidOkBody_DoesNotThrow()
    {
        await using GetItemResponse response = await CreateGetItemResponse(
            200, """{"id":"1","name":"Widget"}""");

        response.Validate();
    }

    [TestMethod]
    public async Task ResponseValidate_Valid404Body_DoesNotThrow()
    {
        await using GetItemResponse response = await CreateGetItemResponse(
            404, """{"code":404,"message":"Not found"}""");

        response.Validate();
    }

    [TestMethod]
    public async Task ResponseValidate_ValidDefaultBody_DoesNotThrow()
    {
        await using GetItemResponse response = await CreateGetItemResponse(
            500, """{"error":"Internal server error"}""");

        response.Validate();
    }

    [TestMethod]
    public async Task ResponseValidate_NoneMode_SkipsValidation()
    {
        // Missing required "name" field, but None mode should not throw.
        await using GetItemResponse response = await CreateGetItemResponse(
            200, """{"id":"1"}""");

        response.Validate(ValidationMode.None);
    }

    [TestMethod]
    public async Task ResponseValidate_InvalidOkBody_BasicMode_ThrowsInvalidOperationException()
    {
        // Missing required "name" field.
        await using GetItemResponse response = await CreateGetItemResponse(
            200, """{"id":"1"}""");

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            response.Validate(ValidationMode.Basic));

        StringAssert.Contains(ex.Message, "200");
    }

    [TestMethod]
    public async Task ResponseValidate_Invalid404Body_BasicMode_ThrowsInvalidOperationException()
    {
        // Missing required "message" field.
        await using GetItemResponse response = await CreateGetItemResponse(
            404, """{"code":404}""");

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            response.Validate(ValidationMode.Basic));

        StringAssert.Contains(ex.Message, "404");
    }

    [TestMethod]
    public async Task ResponseValidate_InvalidDefaultBody_BasicMode_ThrowsWithStatusCode()
    {
        // Missing required "error" field.
        await using GetItemResponse response = await CreateGetItemResponse(
            503, """{"unexpected":"field"}""");

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            response.Validate(ValidationMode.Basic));

        StringAssert.Contains(ex.Message, "503");
    }

    [TestMethod]
    public async Task ResponseValidate_InvalidOkBody_DetailedMode_ThrowsWithJsonDiagnostics()
    {
        await using GetItemResponse response = await CreateGetItemResponse(
            200, """{"id":"1"}""");

        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            response.Validate(ValidationMode.Detailed));

        StringAssert.Contains(ex.Message, "200");
        StringAssert.Contains(ex.Message, "evaluationPath");
        StringAssert.Contains(ex.Message, "instanceLocation");
    }

    // ── PATCH / HEAD / OPTIONS / TRACE E2E tests ─────────────────────────
    // These tests exercise HttpClientTransport.MapMethod for the four HTTP
    // methods not covered by the existing spec operations.
    [TestMethod]
    public async Task PatchItem_200_SendsPatchAndParsesJsonResponse()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, """{"id":"i-1","name":"patched"}""");

        using var bodyDoc = ParsedJsonDocument<PatchItemsByItemIdBody>.Parse("""{"name":"patched"}""");
        PatchItemsByItemIdBody body = bodyDoc.RootElement;

        await using PatchItemResponse response = await harness.Transport
            .SendAsync<PatchItemRequest, PatchItemsByItemIdBody, PatchItemResponse>(
                new PatchItemRequest(JsonElement.ParseValue("\"i-1\""u8)),
                in body,
                CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        Assert.AreEqual(new HttpMethod("PATCH"), harness.CapturedRequest.Method);
        Assert.AreEqual(200, response.StatusCode);
        Assert.IsTrue(response.TryGetOk(out var result));
        Assert.AreEqual("patched", (string)result.Name);
    }

    [TestMethod]
    public async Task HeadItem_200_SendsHead()
    {
        using var harness = new TestHarness(HttpStatusCode.OK, string.Empty);

        await using HeadItemResponse response = await harness.Transport
            .SendAsync<HeadItemRequest, HeadItemResponse>(
                new HeadItemRequest(JsonElement.ParseValue("\"h-1\""u8)),
                CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        Assert.AreEqual(HttpMethod.Head, harness.CapturedRequest.Method);
        Assert.AreEqual(200, response.StatusCode);
        Assert.IsTrue(response.IsSuccess);
    }

    [TestMethod]
    public async Task OptionsItems_204_SendsOptions()
    {
        using var harness = new TestHarness(HttpStatusCode.NoContent, string.Empty);

        await using OptionsItemsResponse response = await harness.Transport
            .SendAsync<OptionsItemsRequest, OptionsItemsResponse>(
                default(OptionsItemsRequest),
                CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        Assert.AreEqual(HttpMethod.Options, harness.CapturedRequest.Method);
        Assert.AreEqual(204, response.StatusCode);
    }

    [TestMethod]
    public async Task TraceItem_200_SendsTraceAndReturnsText()
    {
        byte[] textBytes = Encoding.UTF8.GetBytes("TRACE /items/t-1 HTTP/1.1");
        using var harness = new TestHarness(HttpStatusCode.OK, textBytes, "text/plain");

        await using TraceItemResponse response = await harness.Transport
            .SendAsync<TraceItemRequest, TraceItemResponse>(
                new TraceItemRequest(JsonElement.ParseValue("\"t-1\""u8)),
                CancellationToken.None);

        Assert.IsNotNull(harness.CapturedRequest);
        Assert.AreEqual(HttpMethod.Trace, harness.CapturedRequest.Method);
        Assert.AreEqual(200, response.StatusCode);
        Assert.IsTrue(response.TryGetOkString(out string? text));
        Assert.AreEqual("TRACE /items/t-1 HTTP/1.1", text);
    }

    private static async Task<GetItemResponse> CreateGetItemResponse(int statusCode, string json)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        MemoryStream stream = new(bytes);
        return await GetItemResponse.CreateAsync(statusCode, stream, "application/json");
    }

    [TestMethod]
    public async Task GetItemTag_200_TypedIntArrayHeaderParsed()
    {
        var headers = new Dictionary<string, string>
        {
            ["X-Total-Count"] = "3",
            ["X-Request-Id"] = "req-123",
            ["X-Tags"] = "a,b",
            ["X-Metadata"] = "region,us-east-1,version,2",
            ["X-Page-Sizes"] = "10,25,50,100",
            ["X-Flags"] = "true,false,true",
        };

        using var harness = new TestHarness(HttpStatusCode.OK, """{}""", headers);

        await using GetItemTagResponse response = await harness.Transport
            .SendAsync<GetItemTagRequest, GetItemTagResponse>(
                new GetItemTagRequest(
                    JsonInt64.ParseValue("42"u8),
                    JsonString.ParseValue("\"myTag\""u8)),
                CancellationToken.None);

        Assert.AreEqual(200, response.StatusCode);

        // Verify the int32 array header was parsed with int.Parse
        var pageSizes = response.XPageSizesHeader;
        Assert.IsNotNull(pageSizes);
        Assert.AreEqual(4, pageSizes.Value.GetArrayLength());
        Assert.AreEqual(10, (int)pageSizes.Value[0]);
        Assert.AreEqual(25, (int)pageSizes.Value[1]);
        Assert.AreEqual(50, (int)pageSizes.Value[2]);
        Assert.AreEqual(100, (int)pageSizes.Value[3]);
    }

    [TestMethod]
    public async Task GetItemTag_200_TypedBoolArrayHeaderParsed()
    {
        var headers = new Dictionary<string, string>
        {
            ["X-Total-Count"] = "1",
            ["X-Request-Id"] = "req-456",
            ["X-Tags"] = "x",
            ["X-Metadata"] = "region,eu-west-1,version,1",
            ["X-Page-Sizes"] = "20",
            ["X-Flags"] = "true,false,true",
        };

        using var harness = new TestHarness(HttpStatusCode.OK, """{}""", headers);

        await using GetItemTagResponse response = await harness.Transport
            .SendAsync<GetItemTagRequest, GetItemTagResponse>(
                new GetItemTagRequest(
                    JsonInt64.ParseValue("7"u8),
                    JsonString.ParseValue("\"tag2\""u8)),
                CancellationToken.None);

        Assert.AreEqual(200, response.StatusCode);

        // Verify the boolean array header was parsed with bool.Parse
        var flags = response.XFlagsHeader;
        Assert.IsNotNull(flags);
        Assert.AreEqual(3, flags.Value.GetArrayLength());
        Assert.AreEqual(true, (bool)flags.Value[0]);
        Assert.AreEqual(false, (bool)flags.Value[1]);
        Assert.AreEqual(true, (bool)flags.Value[2]);
    }

    // ── Link following E2E tests ─────────────────────────────────────────
    [TestMethod]
    public async Task CreateItem_FollowGetCreatedItemLink_ExtractsIdFromResponseBody()
    {
        using var harness = new SequencedTestHarness(
        [
            (HttpStatusCode.Created, """{"id":"new-42","name":"NewWidget","price":19.99}"""),
            (HttpStatusCode.OK, """{"id":"new-42","name":"NewWidget","price":19.99}"""),
        ]);

        var client = new ApiItemsClient(harness.Transport);
        using var bodyDoc = ParsedJsonDocument<PostItemsBody>.Parse("""{"name":"NewWidget","price":19.99}""");

        CreateItemResponse createResp = await client.CreateItemAsync(body: bodyDoc.RootElement);

        Assert.AreEqual(201, createResp.StatusCode);

        // Follow the GetCreatedItem link — extracts itemId from $response.body#/id.
        GetItemResponse getResp = await createResp.Links.GetCreatedItemAsync();

        Assert.AreEqual(200, getResp.StatusCode);

        // Verify the linked request used itemId = "new-42" from the response body.
        HttpRequestMessage linkedRequest = harness.CapturedRequests[1];
        Assert.AreEqual("/items/new-42", linkedRequest.RequestUri!.AbsolutePath);
    }

    [TestMethod]
    public async Task GetItem_FollowRefreshItemLink_UsesSourceRequestPath()
    {
        using var harness = new SequencedTestHarness(
        [
            (HttpStatusCode.OK, """{"id":"item-7","name":"Widget","price":9.99}"""),
            (HttpStatusCode.OK, """{"id":"item-7","name":"Widget","price":10.99}"""),
        ]);

        var client = new ApiItemsClient(harness.Transport);
        GetItemResponse getResp = await client.GetItemAsync(itemId: "item-7");

        Assert.AreEqual(200, getResp.StatusCode);

        // Follow the RefreshItem link — uses $request.path.itemId from the original request.
        GetItemResponse refreshResp = await getResp.Links.RefreshItemAsync();

        Assert.AreEqual(200, refreshResp.StatusCode);

        HttpRequestMessage linkedRequest = harness.CapturedRequests[1];
        Assert.AreEqual("/items/item-7", linkedRequest.RequestUri!.AbsolutePath);
    }

    [TestMethod]
    public async Task CreateItem_FollowLink_TransportIsPassedThrough()
    {
        using var harness = new SequencedTestHarness(
        [
            (HttpStatusCode.Created, """{"id":"t-1","name":"Test","price":1.00}"""),
            (HttpStatusCode.OK, """{"id":"t-1","name":"Test","price":1.00}"""),
        ]);

        var client = new ApiItemsClient(harness.Transport);
        using var bodyDoc = ParsedJsonDocument<PostItemsBody>.Parse("""{"name":"Test","price":1.00}""");

        CreateItemResponse createResp = await client.CreateItemAsync(body: bodyDoc.RootElement);

        // The link call succeeds — transport was stored in the response and used for linked request.
        GetItemResponse linkedResp = await createResp.Links.GetCreatedItemAsync();

        Assert.AreEqual(2, harness.CapturedRequests.Count);
    }

    [TestMethod]
    public async Task GetItem_FollowRefreshLink_UnsatisfiedOptionalParamsAreUndefined()
    {
        using var harness = new SequencedTestHarness(
        [
            (HttpStatusCode.OK, """{"id":"item-3","name":"Gadget","price":5.00}"""),
            (HttpStatusCode.OK, """{"id":"item-3","name":"Gadget","price":5.00}"""),
        ]);

        var client = new ApiItemsClient(harness.Transport);
        GetItemResponse getResp = await client.GetItemAsync(itemId: "item-3");

        // Follow RefreshItem with no optional params — they should not appear in the query string.
        GetItemResponse refreshResp = await getResp.Links.RefreshItemAsync();

        HttpRequestMessage linkedRequest = harness.CapturedRequests[1];
        string query = linkedRequest.RequestUri!.Query;

        Assert.AreEqual(string.Empty, query);
    }

    [TestMethod]
    public async Task GetItem_FollowRefreshLink_WithOptionalParams()
    {
        using var harness = new SequencedTestHarness(
        [
            (HttpStatusCode.OK, """{"id":"item-5","name":"Widget","price":9.99}"""),
            (HttpStatusCode.OK, """{"id":"item-5","name":"Widget","price":9.99}"""),
        ]);

        var client = new ApiItemsClient(harness.Transport);
        GetItemResponse getResp = await client.GetItemAsync(itemId: "item-5");

        // Follow RefreshItem with an optional filter param.
        GetItemResponse refreshResp = await getResp.Links.RefreshItemAsync(
            filter: JsonString.ParseValue("\"active\""u8));

        HttpRequestMessage linkedRequest = harness.CapturedRequests[1];
        string query = linkedRequest.RequestUri!.Query;

        Assert.IsTrue(query.Contains("filter=active"), $"Expected query to contain 'filter=active' but got '{query}'");
        Assert.AreEqual("/items/item-5", linkedRequest.RequestUri!.AbsolutePath);
    }

    [TestMethod]
    public async Task CreateItem_FollowSearchByNameLink_ExtractsNameFromRequestBody()
    {
        using var harness = new SequencedTestHarness(
        [
            (HttpStatusCode.Created, """{"id":"new-99","name":"Gizmo"}"""),
            (HttpStatusCode.OK, """{}"""),
        ]);

        var client = new ApiItemsClient(harness.Transport);
        using var bodyDoc = ParsedJsonDocument<PostItemsBody>.Parse("""{"name":"Gizmo","price":29.99}""");

        CreateItemResponse createResp = await client.CreateItemAsync(body: bodyDoc.RootElement);

        Assert.AreEqual(201, createResp.StatusCode);

        // Follow the SearchByName link — extracts q from $request.body#/name.
        await using SearchResponse searchResp = await createResp.Links.SearchByNameAsync();

        Assert.AreEqual(200, searchResp.StatusCode);

        // Verify the linked request used q = "Gizmo" from the original request body.
        HttpRequestMessage linkedRequest = harness.CapturedRequests[1];
        Assert.IsTrue(
            linkedRequest.RequestUri!.Query.Contains("q=Gizmo"),
            $"Expected query to contain 'q=Gizmo' but got '{linkedRequest.RequestUri.Query}'");
    }

    [TestMethod]
    public async Task CreateItem_FollowSearchByNameLink_WithOptionalParams()
    {
        using var harness = new SequencedTestHarness(
        [
            (HttpStatusCode.Created, """{"id":"new-100","name":"Widget Pro"}"""),
            (HttpStatusCode.OK, """{}"""),
        ]);

        var client = new ApiItemsClient(harness.Transport);
        using var bodyDoc = ParsedJsonDocument<PostItemsBody>.Parse("""{"name":"Widget Pro","price":49.99}""");

        CreateItemResponse createResp = await client.CreateItemAsync(body: bodyDoc.RootElement);

        // Follow SearchByName with optional page parameter.
        await using SearchResponse searchResp = await createResp.Links.SearchByNameAsync(page: 3);

        HttpRequestMessage linkedRequest = harness.CapturedRequests[1];
        string query = linkedRequest.RequestUri!.Query;

        Assert.IsTrue(query.Contains("q=Widget"), $"Expected query to contain 'q=Widget' but got '{query}'");
        Assert.IsTrue(query.Contains("page=3"), $"Expected query to contain 'page=3' but got '{query}'");
    }

    /// <summary>
    /// A test harness that returns a sequence of pre-configured responses,
    /// capturing all requests in order. Used for link-following tests where
    /// the initial request and the linked request need different responses.
    /// </summary>
    private sealed class SequencedTestHarness : IDisposable
    {
        private readonly SequencedMockHandler handler;
        private readonly HttpClient client;

        public SequencedTestHarness(
            IReadOnlyList<(HttpStatusCode StatusCode, string ResponseBody)> responses)
        {
            this.handler = new SequencedMockHandler(responses);
            this.client = new HttpClient(this.handler)
            {
                BaseAddress = new Uri("http://localhost"),
            };
            this.Transport = new HttpClientTransport(this.client);
        }

        public HttpClientTransport Transport { get; }

        public IReadOnlyList<HttpRequestMessage> CapturedRequests => this.handler.CapturedRequests;

        public void Dispose()
        {
            this.Transport.DisposeAsync().AsTask().GetAwaiter().GetResult();
            this.client.Dispose();
            this.handler.Dispose();
        }
    }

    // ── Streaming (itemSchema) E2E tests ─────────────────────────────────
    // These tests verify that streaming responses (NDJSON and SSE) work correctly
    // with the generated EnumerateOkItems method and JsonStreamReader.
    [TestMethod]
    public async Task StreamEvents_200_NdjsonEnumeratesItems()
    {
        const string NdjsonBody = """
            {"eventId":"evt-1","eventType":"update","timestamp":"2024-01-01T00:00:00Z","payload":{"key":"value1"}}
            {"eventId":"evt-2","eventType":"create","timestamp":"2024-01-02T00:00:00Z","payload":{"key":"value2"}}
            {"eventId":"evt-3","eventType":"delete","timestamp":"2024-01-03T00:00:00Z","payload":{"key":"value3"}}
            """;
        byte[] bytes = Encoding.UTF8.GetBytes(NdjsonBody);

        using var harness = new TestHarness(HttpStatusCode.OK, bytes, "application/x-ndjson");

        StreamEventsRequest request = default;
        await using StreamEventsResponse response = await harness.Transport
            .SendAsync<StreamEventsRequest, StreamEventsResponse>(
                in request,
                CancellationToken.None);

        Assert.AreEqual(200, response.StatusCode);
        Assert.IsTrue(response.IsSuccess);

        List<(string EventId, string EventType)> items = [];
        await foreach (ParsedJsonDocument<ItemSchema> doc in response.EnumerateOkItems())
        {
            using (doc)
            {
                items.Add(((string)doc.RootElement.EventId, (string)doc.RootElement.EventType));
            }
        }

        Assert.AreEqual(3, items.Count);
        Assert.AreEqual("evt-1", items[0].EventId);
        Assert.AreEqual("update", items[0].EventType);
        Assert.AreEqual("evt-2", items[1].EventId);
        Assert.AreEqual("create", items[1].EventType);
        Assert.AreEqual("evt-3", items[2].EventId);
        Assert.AreEqual("delete", items[2].EventType);
    }

    [TestMethod]
    public async Task StreamEvents_200_EmptyStreamYieldsNoItems()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(string.Empty);

        using var harness = new TestHarness(HttpStatusCode.OK, bytes, "application/x-ndjson");

        StreamEventsRequest request = default;
        await using StreamEventsResponse response = await harness.Transport
            .SendAsync<StreamEventsRequest, StreamEventsResponse>(
                in request,
                CancellationToken.None);

        Assert.AreEqual(200, response.StatusCode);

        List<string> items = [];
        await foreach (ParsedJsonDocument<ItemSchema> doc in response.EnumerateOkItems())
        {
            using (doc)
            {
                items.Add((string)doc.RootElement.EventId);
            }
        }

        Assert.AreEqual(0, items.Count);
    }

    [TestMethod]
    public async Task StreamEvents_200_SkipsEmptyLines()
    {
        const string NdjsonBody = """
            {"eventId":"a"}

            {"eventId":"b"}

            """;
        byte[] bytes = Encoding.UTF8.GetBytes(NdjsonBody);

        using var harness = new TestHarness(HttpStatusCode.OK, bytes, "application/x-ndjson");

        StreamEventsRequest request = default;
        await using StreamEventsResponse response = await harness.Transport
            .SendAsync<StreamEventsRequest, StreamEventsResponse>(
                in request,
                CancellationToken.None);

        List<string> items = [];
        await foreach (ParsedJsonDocument<ItemSchema> doc in response.EnumerateOkItems())
        {
            using (doc)
            {
                items.Add((string)doc.RootElement.EventId);
            }
        }

        Assert.AreEqual(2, items.Count);
        Assert.AreEqual("a", items[0]);
        Assert.AreEqual("b", items[1]);
    }

    [TestMethod]
    public async Task StreamEvents_401_ParsesErrorBody()
    {
        using var harness = new TestHarness(HttpStatusCode.Unauthorized, """{"message":"unauthorized"}""");

        StreamEventsRequest request = default;
        await using StreamEventsResponse response = await harness.Transport
            .SendAsync<StreamEventsRequest, StreamEventsResponse>(
                in request,
                CancellationToken.None);

        Assert.AreEqual(401, response.StatusCode);
        Assert.IsFalse(response.IsSuccess);
        Assert.IsTrue(response.TryGetUnauthorized(out var body));
        Assert.AreEqual("unauthorized", (string)body.Message);
    }

    [TestMethod]
    public async Task StreamEvents_401_EnumerateThrowsWhenNoStream()
    {
        using var harness = new TestHarness(HttpStatusCode.Unauthorized, """{"message":"unauthorized"}""");

        StreamEventsRequest request = default;
        await using StreamEventsResponse response = await harness.Transport
            .SendAsync<StreamEventsRequest, StreamEventsResponse>(
                in request,
                CancellationToken.None);

        Assert.ThrowsExactly<InvalidOperationException>(() => response.EnumerateOkItems());
    }

    [TestMethod]
    public async Task ChatCompletions_200_SseEnumeratesItems()
    {
        const string SseBody = """
            :comment line
            event: message
            data: {"id":"cmpl-1","object":"chat.completion.chunk","choices":[{"index":0,"delta":{"content":"Hello"}}]}

            data: {"id":"cmpl-2","object":"chat.completion.chunk","choices":[{"index":0,"delta":{"content":" World"}}]}

            data: {"id":"cmpl-3","object":"chat.completion.chunk","choices":[{"index":0,"delta":{"content":"!"}}]}

            """;
        byte[] bytes = Encoding.UTF8.GetBytes(SseBody);

        using var harness = new TestHarness(HttpStatusCode.OK, bytes, "text/event-stream");

        ChatCompletionsRequest request = default;
        await using ChatCompletionsResponse response = await harness.Transport
            .SendAsync<ChatCompletionsRequest, ChatCompletionsResponse>(
                in request,
                CancellationToken.None);

        Assert.AreEqual(200, response.StatusCode);
        Assert.IsTrue(response.IsSuccess);

        List<(string Id, string Obj)> items = [];
        await foreach (ParsedJsonDocument<ItemSchema1> doc in response.EnumerateOkItems())
        {
            using (doc)
            {
                items.Add(((string)doc.RootElement.Id, (string)doc.RootElement.ObjectValue));
            }
        }

        Assert.AreEqual(3, items.Count);
        Assert.AreEqual("cmpl-1", items[0].Id);
        Assert.AreEqual("chat.completion.chunk", items[0].Obj);
        Assert.AreEqual("cmpl-2", items[1].Id);
        Assert.AreEqual("cmpl-3", items[2].Id);
    }

    [TestMethod]
    public async Task ChatCompletions_200_SseSkipsMetadataAndComments()
    {
        const string SseBody = """
            :this is a comment
            retry: 3000
            id: 123
            event: message
            data: {"id":"c1","choices":[{"index":0,"delta":{"content":"A"}}]}

            :another comment
            event: done
            data: {"id":"c2","choices":[{"index":0,"delta":{"content":"B"}}]}

            """;
        byte[] bytes = Encoding.UTF8.GetBytes(SseBody);

        using var harness = new TestHarness(HttpStatusCode.OK, bytes, "text/event-stream");

        ChatCompletionsRequest request = default;
        await using ChatCompletionsResponse response = await harness.Transport
            .SendAsync<ChatCompletionsRequest, ChatCompletionsResponse>(
                in request,
                CancellationToken.None);

        List<string> items = [];
        await foreach (ParsedJsonDocument<ItemSchema1> doc in response.EnumerateOkItems())
        {
            using (doc)
            {
                items.Add((string)doc.RootElement.Id);
            }
        }

        Assert.AreEqual(2, items.Count);
        Assert.AreEqual("c1", items[0]);
        Assert.AreEqual("c2", items[1]);
    }

    [TestMethod]
    public async Task ChatCompletions_200_SseDataWithoutSpace()
    {
        // SSE data lines can omit the space after "data:"
        const string SseBody = """
            data:{"id":"d1","choices":[{"index":0,"delta":{"content":"X"}}]}
            """;
        byte[] bytes = Encoding.UTF8.GetBytes(SseBody);

        using var harness = new TestHarness(HttpStatusCode.OK, bytes, "text/event-stream");

        ChatCompletionsRequest request = default;
        await using ChatCompletionsResponse response = await harness.Transport
            .SendAsync<ChatCompletionsRequest, ChatCompletionsResponse>(
                in request,
                CancellationToken.None);

        List<string> items = [];
        await foreach (ParsedJsonDocument<ItemSchema1> doc in response.EnumerateOkItems())
        {
            using (doc)
            {
                items.Add((string)doc.RootElement.Id);
            }
        }

        Assert.AreEqual(1, items.Count);
        Assert.AreEqual("d1", items[0]);
    }

    [TestMethod]
    public async Task ChatCompletions_400_ParsesErrorBody()
    {
        using var harness = new TestHarness(HttpStatusCode.BadRequest, """{"error":"invalid model","code":400}""");

        ChatCompletionsRequest request = default;
        await using ChatCompletionsResponse response = await harness.Transport
            .SendAsync<ChatCompletionsRequest, ChatCompletionsResponse>(
                in request,
                CancellationToken.None);

        Assert.AreEqual(400, response.StatusCode);
        Assert.IsFalse(response.IsSuccess);
        Assert.IsTrue(response.TryGetBadRequest(out var body));
        Assert.AreEqual("invalid model", (string)body.Error);
    }

    [TestMethod]
    public async Task ChatCompletions_200_CancellationStopsEnumeration()
    {
        // A stream that yields many items — we cancel after first
        const string SseBody = """
            data: {"id":"n1","choices":[{"index":0,"delta":{"content":"a"}}]}
            data: {"id":"n2","choices":[{"index":0,"delta":{"content":"b"}}]}
            data: {"id":"n3","choices":[{"index":0,"delta":{"content":"c"}}]}
            data: {"id":"n4","choices":[{"index":0,"delta":{"content":"d"}}]}
            data: {"id":"n5","choices":[{"index":0,"delta":{"content":"e"}}]}
            """;
        byte[] bytes = Encoding.UTF8.GetBytes(SseBody);

        using var harness = new TestHarness(HttpStatusCode.OK, bytes, "text/event-stream");

        ChatCompletionsRequest request = default;
        await using ChatCompletionsResponse response = await harness.Transport
            .SendAsync<ChatCompletionsRequest, ChatCompletionsResponse>(
                in request,
                CancellationToken.None);

        using CancellationTokenSource cts = new();
        List<string> items = [];

        await foreach (ParsedJsonDocument<ItemSchema1> doc in response.EnumerateOkItems(cts.Token))
        {
            using (doc)
            {
                items.Add((string)doc.RootElement.Id);
                if (items.Count >= 2)
                {
                    cts.Cancel();
                    break;
                }
            }
        }

        Assert.AreEqual(2, items.Count);
    }

    [TestMethod]
    public async Task StreamEvents_200_MatchResultFallsToDefault()
    {
        // A 200 streaming response doesn't have a MatchResult handler for OK
        // (streaming responses use EnumerateOkItems instead), so it falls to default
        byte[] bytes = "{\"eventId\":\"x\"}\n"u8.ToArray();

        using var harness = new TestHarness(HttpStatusCode.OK, bytes, "application/x-ndjson");

        StreamEventsRequest request = default;
        await using StreamEventsResponse response = await harness.Transport
            .SendAsync<StreamEventsRequest, StreamEventsResponse>(
                in request,
                CancellationToken.None);

        string result = response.MatchResult(
            matchUnauthorized: body => "unauthorized",
            matchDefault: statusCode => $"default:{statusCode}");

        Assert.AreEqual("default:200", result);
    }

    // ── SSE metadata (EnumerateOkSseItems) tests ─────────────────────────
    [TestMethod]
    public async Task StreamEvents_200_SseItemsPreservesMetadata()
    {
        const string SseBody = "event: update\nid: evt-001\nretry: 5000\ndata: {\"eventId\":\"evt-1\",\"eventType\":\"update\",\"timestamp\":\"2024-01-01T00:00:00Z\",\"payload\":{\"key\":\"v1\"}}\n\nevent: create\nid: evt-002\ndata: {\"eventId\":\"evt-2\",\"eventType\":\"create\",\"timestamp\":\"2024-01-02T00:00:00Z\",\"payload\":{\"key\":\"v2\"}}\n\n";
        byte[] bytes = Encoding.UTF8.GetBytes(SseBody);

        using var harness = new TestHarness(HttpStatusCode.OK, bytes, "text/event-stream");

        StreamEventsRequest request = default;
        await using StreamEventsResponse response = await harness.Transport
            .SendAsync<StreamEventsRequest, StreamEventsResponse>(
                in request,
                CancellationToken.None);

        Assert.AreEqual(200, response.StatusCode);

        List<(string EventId, string? EvtType, string? Id, int? Retry)> events = [];
        await foreach (SseEvent<ItemSchema> evt in response.EnumerateOkSseItems())
        {
            using (evt)
            {
                events.Add((
                    (string)evt.Data.EventId,
                    evt.GetEventTypeAsString(),
                    evt.GetIdAsString(),
                    evt.Retry));
            }
        }

        Assert.AreEqual(2, events.Count);

        // First event has all metadata
        Assert.AreEqual("update", events[0].EvtType);
        Assert.AreEqual("evt-001", events[0].Id);
        Assert.AreEqual(5000, events[0].Retry);
        Assert.AreEqual("evt-1", events[0].EventId);

        // Second event has event+id but no retry
        Assert.AreEqual("create", events[1].EvtType);
        Assert.AreEqual("evt-002", events[1].Id);
        Assert.IsNull(events[1].Retry);
        Assert.AreEqual("evt-2", events[1].EventId);
    }

    [TestMethod]
    public async Task ChatCompletions_200_SseItemsPreservesEventType()
    {
        const string SseBody = "event: message\ndata: {\"id\":\"cmpl-1\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"Hello\"}}]}\n\nevent: done\ndata: {\"id\":\"cmpl-2\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"\"}}]}\n\n";
        byte[] bytes = Encoding.UTF8.GetBytes(SseBody);

        using var harness = new TestHarness(HttpStatusCode.OK, bytes, "text/event-stream");

        ChatCompletionsRequest request = default;
        await using ChatCompletionsResponse response = await harness.Transport
            .SendAsync<ChatCompletionsRequest, ChatCompletionsResponse>(
                in request,
                CancellationToken.None);

        List<(string Id, string? EvtType)> events = [];
        await foreach (SseEvent<ItemSchema1> evt in response.EnumerateOkSseItems())
        {
            using (evt)
            {
                events.Add((
                    (string)evt.Data.Id,
                    evt.GetEventTypeAsString()));
            }
        }

        Assert.AreEqual(2, events.Count);
        Assert.AreEqual("message", events[0].EvtType);
        Assert.AreEqual("cmpl-1", events[0].Id);
        Assert.AreEqual("done", events[1].EvtType);
        Assert.AreEqual("cmpl-2", events[1].Id);
    }

    [TestMethod]
    public async Task StreamEvents_200_SseItemsNoMetadataYieldsNulls()
    {
        // SSE data without any metadata fields — EventType, Id, Retry should be empty/null
        const string SseBody = "data: {\"eventId\":\"bare\"}\n\n";
        byte[] bytes = Encoding.UTF8.GetBytes(SseBody);

        using var harness = new TestHarness(HttpStatusCode.OK, bytes, "text/event-stream");

        StreamEventsRequest request = default;
        await using StreamEventsResponse response = await harness.Transport
            .SendAsync<StreamEventsRequest, StreamEventsResponse>(
                in request,
                CancellationToken.None);

        List<(string EventId, string? EvtType, string? Id, int? Retry)> events = [];
        await foreach (SseEvent<ItemSchema> evt in response.EnumerateOkSseItems())
        {
            using (evt)
            {
                events.Add((
                    (string)evt.Data.EventId,
                    evt.GetEventTypeAsString(),
                    evt.GetIdAsString(),
                    evt.Retry));
            }
        }

        Assert.AreEqual(1, events.Count);
        Assert.IsNull(events[0].EvtType);
        Assert.IsNull(events[0].Id);
        Assert.IsNull(events[0].Retry);
        Assert.AreEqual("bare", events[0].EventId);
    }

    [TestMethod]
    public async Task StreamEvents_401_EnumerateSseThrowsWhenNoStream()
    {
        using var harness = new TestHarness(HttpStatusCode.Unauthorized, """{"message":"unauthorized"}""");

        StreamEventsRequest request = default;
        await using StreamEventsResponse response = await harness.Transport
            .SendAsync<StreamEventsRequest, StreamEventsResponse>(
                in request,
                CancellationToken.None);

        Assert.ThrowsExactly<InvalidOperationException>(() => response.EnumerateOkSseItems());
    }

    private sealed class SequencedMockHandler : DelegatingHandler
    {
        private readonly IReadOnlyList<(HttpStatusCode StatusCode, string ResponseBody)> responses;
        private readonly List<HttpRequestMessage> capturedRequests = [];
        private int callIndex;

        public SequencedMockHandler(
            IReadOnlyList<(HttpStatusCode StatusCode, string ResponseBody)> responses)
        {
            this.responses = responses;
            this.InnerHandler = new HttpClientHandler();
        }

        public IReadOnlyList<HttpRequestMessage> CapturedRequests => this.capturedRequests;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            this.capturedRequests.Add(request);

            int index = this.callIndex < this.responses.Count
                ? this.callIndex
                : this.responses.Count - 1;
            this.callIndex++;

            var (statusCode, body) = this.responses[index];

            HttpContent content = string.IsNullOrEmpty(body)
                ? new ByteArrayContent([])
                : new StringContent(body, Encoding.UTF8, "application/json");

            return Task.FromResult(new HttpResponseMessage(statusCode) { Content = content });
        }
    }
}