// <copyright file="RemainingCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using CanonTests32.Client;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi32.Runtime.Tests;

[TestClass]
public class RemainingCoverageTests
{
    private const string ApplicationJsonContentType = "application/json";
    private const string VendorJsonContentType = "application/vnd.api+json";
    private const string TextPlainContentType = "text/plain";
    private const string TextEventStreamContentType = "text/event-stream";
    private const string NdjsonContentType = "application/x-ndjson";
    private const string InvalidBody = "42";
    private const string CopyItemBody = """{"id":"copy-1","location":"/items/copy-1"}""";
    private const string DownloadMixedNotFoundBody = """{"error":"missing"}""";
    private const string GetTextOrJsonJsonBody = """{"value":"json-data"}""";
    private const string GetTextOrJsonNotFoundBody = """{"error":"not here"}""";
    private const string HeaderNestedArrayBody = """{"ok":true}""";
    private const string HeaderNestedItems = """{"id":"item-1","tags":["a","b"]}, {"id":"item-2","tags":["c"]}""";
    private const string ChatBadRequestBody = """{"error":"bad"}""";
    private const string StreamUnauthorizedBody = """{"message":"unauthorized"}""";
    private const string TraceTextBody = "TRACE /items/t-1 HTTP/1.1";

    // ── Request Validate(None) ──
    [TestMethod]
    public void Request_Validate_None_DoesNotThrow_ForAllCoveredRequests()
    {
        foreach ((string name, Action action) in GetRequestValidateNoneCases())
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Assert.Fail($"{name} threw {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    // ── Request later-parameter validation ──
    [TestMethod]
    public void Request_Validate_LaterParameter_Throws_ForAllMultiParameterRequests()
    {
        foreach ((string name, Action action) in GetRequestLaterParameterCases())
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Assert.Fail($"{name} failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    // ── Response Validate(None) ──
    [TestMethod]
    public async Task Response_Validate_None_DoesNotThrow_ForAllBodyValidatingResponses()
    {
        foreach ((string name, Func<Task> action) in GetResponseValidateNoneCases())
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                Assert.Fail($"{name} threw {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    // ── ChatCompletionsResponse ──
    [TestMethod]
    public async Task ChatCompletions_EnumerateOkItems_WhenNoStream_Throws()
    {
        await using ChatCompletionsResponse response = await ChatCompletionsResponse.CreateAsync(
            400,
            MakeStream(ChatBadRequestBody),
            ApplicationJsonContentType);

        Assert.ThrowsExactly<InvalidOperationException>(() => response.EnumerateOkItems());
    }

    [TestMethod]
    public async Task ChatCompletions_EnumerateOkSseItems_WhenNoStream_Throws()
    {
        await using ChatCompletionsResponse response = await ChatCompletionsResponse.CreateAsync(
            400,
            MakeStream(ChatBadRequestBody),
            ApplicationJsonContentType);

        Assert.ThrowsExactly<InvalidOperationException>(() => response.EnumerateOkSseItems());
    }

    [TestMethod]
    public async Task ChatCompletions_TryGetBadRequest_WhenStatusIsNot400_ReturnsFalse()
    {
        await using ChatCompletionsResponse response = await ChatCompletionsResponse.CreateAsync(
            200,
            MakeStream(string.Empty),
            TextEventStreamContentType);

        Assert.IsFalse(response.TryGetBadRequest(out _));
    }

    [TestMethod]
    public async Task ChatCompletions_MatchResult_Status400_DispatchesToBadRequest()
    {
        await using ChatCompletionsResponse response = await ChatCompletionsResponse.CreateAsync(
            400,
            MakeStream(ChatBadRequestBody),
            ApplicationJsonContentType);

        string result = response.MatchResult(
            matchBadRequest: static body => $"bad:{(string)body.Error}",
            matchDefault: static statusCode => $"default:{statusCode}");

        Assert.AreEqual("bad:bad", result);
    }

    [TestMethod]
    public async Task ChatCompletions_MatchResult_Status200_DispatchesToDefault()
    {
        await using ChatCompletionsResponse response = await ChatCompletionsResponse.CreateAsync(
            200,
            MakeStream(string.Empty),
            TextEventStreamContentType);

        string result = response.MatchResult(
            matchBadRequest: static _ => "bad",
            matchDefault: static statusCode => $"default:{statusCode}");

        Assert.AreEqual("default:200", result);
    }

    [TestMethod]
    public async Task ChatCompletions_MatchResultContext_Status400_DispatchesToBadRequest()
    {
        await using ChatCompletionsResponse response = await ChatCompletionsResponse.CreateAsync(
            400,
            MakeStream(ChatBadRequestBody),
            ApplicationJsonContentType);

        string result = response.MatchResult(
            "ctx",
            matchBadRequest: static (PostChatCompletionsBadRequest body, in string context) => $"bad:{(string)body.Error}:{context}",
            matchDefault: static (int statusCode, in string context) => $"default:{statusCode}:{context}");

        Assert.AreEqual("bad:bad:ctx", result);
    }

    [TestMethod]
    public async Task ChatCompletions_MatchResultContext_Status200_DispatchesToDefault()
    {
        await using ChatCompletionsResponse response = await ChatCompletionsResponse.CreateAsync(
            200,
            MakeStream(string.Empty),
            TextEventStreamContentType);

        string result = response.MatchResult(
            "ctx",
            matchBadRequest: static (PostChatCompletionsBadRequest _, in string context) => $"bad:{context}",
            matchDefault: static (int statusCode, in string context) => $"default:{statusCode}:{context}");

        Assert.AreEqual("default:200:ctx", result);
    }

    [TestMethod]
    public async Task ChatCompletions_Validate_Detailed_Status400_ValidBody_DoesNotThrow()
    {
        await using ChatCompletionsResponse response = await ChatCompletionsResponse.CreateAsync(
            400,
            MakeStream(ChatBadRequestBody),
            ApplicationJsonContentType);

        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task ChatCompletions_Validate_Basic_Status400_ValidBody_DoesNotThrow()
    {
        await using ChatCompletionsResponse response = await ChatCompletionsResponse.CreateAsync(
            400,
            MakeStream(ChatBadRequestBody),
            ApplicationJsonContentType);

        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task ChatCompletions_Validate_Detailed_Status400_InvalidBody_Throws()
    {
        await using ChatCompletionsResponse response = await ChatCompletionsResponse.CreateAsync(
            400,
            MakeStream(InvalidBody),
            ApplicationJsonContentType);

        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Detailed));
    }

    // ── StreamEventsResponse ──
    [TestMethod]
    public async Task StreamEvents_EnumerateOkItems_WhenNoStream_Throws()
    {
        await using StreamEventsResponse response = await StreamEventsResponse.CreateAsync(
            401,
            MakeStream(StreamUnauthorizedBody),
            ApplicationJsonContentType);

        Assert.ThrowsExactly<InvalidOperationException>(() => response.EnumerateOkItems());
    }

    [TestMethod]
    public async Task StreamEvents_EnumerateOkSseItems_WhenNoStream_Throws()
    {
        await using StreamEventsResponse response = await StreamEventsResponse.CreateAsync(
            401,
            MakeStream(StreamUnauthorizedBody),
            ApplicationJsonContentType);

        Assert.ThrowsExactly<InvalidOperationException>(() => response.EnumerateOkSseItems());
    }

    [TestMethod]
    public async Task StreamEvents_TryGetUnauthorized_WhenStatusIsNot401_ReturnsFalse()
    {
        await using StreamEventsResponse response = await StreamEventsResponse.CreateAsync(
            200,
            MakeStream(string.Empty),
            NdjsonContentType);

        Assert.IsFalse(response.TryGetUnauthorized(out _));
    }

    [TestMethod]
    public async Task StreamEvents_MatchResult_Status401_DispatchesToUnauthorized()
    {
        await using StreamEventsResponse response = await StreamEventsResponse.CreateAsync(
            401,
            MakeStream(StreamUnauthorizedBody),
            ApplicationJsonContentType);

        string result = response.MatchResult(
            matchUnauthorized: static body => $"unauthorized:{(string)body.Message}",
            matchDefault: static statusCode => $"default:{statusCode}");

        Assert.AreEqual("unauthorized:unauthorized", result);
    }

    [TestMethod]
    public async Task StreamEvents_MatchResult_Status200_DispatchesToDefault()
    {
        await using StreamEventsResponse response = await StreamEventsResponse.CreateAsync(
            200,
            MakeStream(string.Empty),
            NdjsonContentType);

        string result = response.MatchResult(
            matchUnauthorized: static _ => "unauthorized",
            matchDefault: static statusCode => $"default:{statusCode}");

        Assert.AreEqual("default:200", result);
    }

    [TestMethod]
    public async Task StreamEvents_MatchResultContext_Status401_DispatchesToUnauthorized()
    {
        await using StreamEventsResponse response = await StreamEventsResponse.CreateAsync(
            401,
            MakeStream(StreamUnauthorizedBody),
            ApplicationJsonContentType);

        string result = response.MatchResult(
            "ctx",
            matchUnauthorized: static (GetEventsStreamUnauthorized body, in string context) => $"unauthorized:{(string)body.Message}:{context}",
            matchDefault: static (int statusCode, in string context) => $"default:{statusCode}:{context}");

        Assert.AreEqual("unauthorized:unauthorized:ctx", result);
    }

    [TestMethod]
    public async Task StreamEvents_MatchResultContext_Status200_DispatchesToDefault()
    {
        await using StreamEventsResponse response = await StreamEventsResponse.CreateAsync(
            200,
            MakeStream(string.Empty),
            NdjsonContentType);

        string result = response.MatchResult(
            "ctx",
            matchUnauthorized: static (GetEventsStreamUnauthorized _, in string context) => $"unauthorized:{context}",
            matchDefault: static (int statusCode, in string context) => $"default:{statusCode}:{context}");

        Assert.AreEqual("default:200:ctx", result);
    }

    [TestMethod]
    public async Task StreamEvents_Validate_Detailed_Status401_ValidBody_DoesNotThrow()
    {
        await using StreamEventsResponse response = await StreamEventsResponse.CreateAsync(
            401,
            MakeStream(StreamUnauthorizedBody),
            ApplicationJsonContentType);

        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task StreamEvents_Validate_Basic_Status401_ValidBody_DoesNotThrow()
    {
        await using StreamEventsResponse response = await StreamEventsResponse.CreateAsync(
            401,
            MakeStream(StreamUnauthorizedBody),
            ApplicationJsonContentType);

        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task StreamEvents_Validate_Detailed_Status401_InvalidBody_Throws()
    {
        await using StreamEventsResponse response = await StreamEventsResponse.CreateAsync(
            401,
            MakeStream(InvalidBody),
            ApplicationJsonContentType);

        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Detailed));
    }

    // ── CopyItemResponse ──
    [TestMethod]
    public async Task CopyItem_TryGetCreated_WhenStatusIsNot201_ReturnsFalse()
    {
        await using CopyItemResponse response = await CopyItemResponse.CreateAsync(500, MakeStream(string.Empty));

        Assert.IsFalse(response.TryGetCreated(out _));
    }

    [TestMethod]
    public async Task CopyItem_MatchResult_WhenStatusIsDefault_DispatchesToDefault()
    {
        await using CopyItemResponse response = await CopyItemResponse.CreateAsync(500, MakeStream(string.Empty));

        string result = response.MatchResult(
            matchCreated: static _ => "created",
            matchDefault: static statusCode => $"default:{statusCode}");

        Assert.AreEqual("default:500", result);
    }

    [TestMethod]
    public async Task CopyItem_MatchResultContext_WhenStatusIsCreated_DispatchesToCreated()
    {
        await using CopyItemResponse response = await CopyItemResponse.CreateAsync(
            201,
            MakeStream(CopyItemBody),
            ApplicationJsonContentType);

        string result = response.MatchResult(
            "ctx",
            matchCreated: static (Schema body, in string context) => $"created:{(string)body.Id}:{context}",
            matchDefault: static (int statusCode, in string context) => $"default:{statusCode}:{context}");

        Assert.AreEqual("created:copy-1:ctx", result);
    }

    [TestMethod]
    public async Task CopyItem_MatchResultContext_WhenStatusIsDefault_DispatchesToDefault()
    {
        await using CopyItemResponse response = await CopyItemResponse.CreateAsync(500, MakeStream(string.Empty));

        string result = response.MatchResult(
            "ctx",
            matchCreated: static (Schema _, in string context) => $"created:{context}",
            matchDefault: static (int statusCode, in string context) => $"default:{statusCode}:{context}");

        Assert.AreEqual("default:500:ctx", result);
    }

    [TestMethod]
    public async Task CopyItem_Validate_Detailed_ValidBody_DoesNotThrow()
    {
        await using CopyItemResponse response = await CopyItemResponse.CreateAsync(
            201,
            MakeStream(CopyItemBody),
            ApplicationJsonContentType);

        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task CopyItem_Validate_Basic_ValidBody_DoesNotThrow()
    {
        await using CopyItemResponse response = await CopyItemResponse.CreateAsync(
            201,
            MakeStream(CopyItemBody),
            ApplicationJsonContentType);

        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task CopyItem_Validate_Detailed_InvalidBody_Throws()
    {
        await using CopyItemResponse response = await CopyItemResponse.CreateAsync(
            201,
            MakeStream(InvalidBody),
            ApplicationJsonContentType);

        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public async Task CopyItem_LocationHeader_WhenPresent_IsParsed()
    {
        await using CopyItemResponse response = await CopyItemResponse.CreateAsync(
            201,
            MakeStream(CopyItemBody),
            ApplicationJsonContentType,
            new TestResponseHeaders(new Dictionary<string, string> { ["Location"] = "https://example.com/items/new-copy" }));

        Assert.IsFalse(response.LocationHeader.IsUndefined());
        Assert.AreEqual("https://example.com/items/new-copy", (string)response.LocationHeader);
    }

    [TestMethod]
    public async Task CopyItem_LocationHeader_WhenMissing_IsUndefined()
    {
        await using CopyItemResponse response = await CopyItemResponse.CreateAsync(
            201,
            MakeStream(CopyItemBody),
            ApplicationJsonContentType);

        Assert.IsTrue(response.LocationHeader.IsUndefined());
    }

    // ── TraceItemResponse ──
    [TestMethod]
    public async Task TraceItem_TryGetOkString_WhenStatusIsNot200_ReturnsFalse()
    {
        await using TraceItemResponse response = await TraceItemResponse.CreateAsync(500, MakeStream(string.Empty));

        Assert.IsFalse(response.TryGetOkString(out _));
    }

    [TestMethod]
    public async Task TraceItem_MatchResult_Status200_DispatchesToOkString()
    {
        await using TraceItemResponse response = await TraceItemResponse.CreateAsync(
            200,
            MakeStream(TraceTextBody),
            TextPlainContentType);

        string result = response.MatchResult(
            matchOkString: static text => $"ok:{text}",
            matchDefault: static statusCode => $"default:{statusCode}");

        Assert.AreEqual($"ok:{TraceTextBody}", result);
    }

    [TestMethod]
    public async Task TraceItem_MatchResult_StatusDefault_DispatchesToDefault()
    {
        await using TraceItemResponse response = await TraceItemResponse.CreateAsync(500, MakeStream(string.Empty));

        string result = response.MatchResult(
            matchOkString: static _ => "ok",
            matchDefault: static statusCode => $"default:{statusCode}");

        Assert.AreEqual("default:500", result);
    }

    [TestMethod]
    public async Task TraceItem_MatchResultContext_Status200_DispatchesToOkString()
    {
        await using TraceItemResponse response = await TraceItemResponse.CreateAsync(
            200,
            MakeStream(TraceTextBody),
            TextPlainContentType);

        string result = response.MatchResult(
            "ctx",
            matchOkString: static (string? text, in string context) => $"ok:{text}:{context}",
            matchDefault: static (int statusCode, in string context) => $"default:{statusCode}:{context}");

        Assert.AreEqual($"ok:{TraceTextBody}:ctx", result);
    }

    [TestMethod]
    public async Task TraceItem_MatchResultContext_StatusDefault_DispatchesToDefault()
    {
        await using TraceItemResponse response = await TraceItemResponse.CreateAsync(500, MakeStream(string.Empty));

        string result = response.MatchResult(
            "ctx",
            matchOkString: static (string? _, in string context) => $"ok:{context}",
            matchDefault: static (int statusCode, in string context) => $"default:{statusCode}:{context}");

        Assert.AreEqual("default:500:ctx", result);
    }

    [TestMethod]
    public async Task TraceItem_OkUtf8Bytes_ReturnsExactBytes()
    {
        await using TraceItemResponse response = await TraceItemResponse.CreateAsync(
            200,
            MakeStream(TraceTextBody),
            TextPlainContentType);

        CollectionAssert.AreEqual(System.Text.Encoding.UTF8.GetBytes(TraceTextBody), response.OkUtf8Bytes.ToArray());
    }

    // ── DownloadMixedResponse ──
    [TestMethod]
    public async Task DownloadMixed_MatchResultContext_Status200_DispatchesToOkStream()
    {
        await using DownloadMixedResponse response = await DownloadMixedResponse.CreateAsync(
            200,
            MakeStream("abc"),
            "application/octet-stream");

        string result = response.MatchResult(
            "ctx",
            matchOkStream: static (Stream? stream, in string context) => $"stream:{stream is not null}:{context}",
            matchNotFound: static (GetFilesDownloadMixedNotFound _, in string context) => $"not-found:{context}",
            matchDefault: static (int statusCode, in string context) => $"default:{statusCode}:{context}");

        Assert.AreEqual("stream:True:ctx", result);
    }

    [TestMethod]
    public async Task DownloadMixed_MatchResultContext_Status404_DispatchesToNotFound()
    {
        await using DownloadMixedResponse response = await DownloadMixedResponse.CreateAsync(
            404,
            MakeStream(DownloadMixedNotFoundBody),
            ApplicationJsonContentType);

        string result = response.MatchResult(
            "ctx",
            matchOkStream: static (Stream? _, in string context) => $"stream:{context}",
            matchNotFound: static (GetFilesDownloadMixedNotFound body, in string context) => $"not-found:{(string)body.Error}:{context}",
            matchDefault: static (int statusCode, in string context) => $"default:{statusCode}:{context}");

        Assert.AreEqual("not-found:missing:ctx", result);
    }

    [TestMethod]
    public async Task DownloadMixed_MatchResultContext_StatusDefault_DispatchesToDefault()
    {
        await using DownloadMixedResponse response = await DownloadMixedResponse.CreateAsync(500, MakeStream(string.Empty));

        string result = response.MatchResult(
            "ctx",
            matchOkStream: static (Stream? _, in string context) => $"stream:{context}",
            matchNotFound: static (GetFilesDownloadMixedNotFound _, in string context) => $"not-found:{context}",
            matchDefault: static (int statusCode, in string context) => $"default:{statusCode}:{context}");

        Assert.AreEqual("default:500:ctx", result);
    }

    [TestMethod]
    public async Task DownloadMixed_Validate_Detailed_ValidNotFoundBody_DoesNotThrow()
    {
        await using DownloadMixedResponse response = await DownloadMixedResponse.CreateAsync(
            404,
            MakeStream(DownloadMixedNotFoundBody),
            ApplicationJsonContentType);

        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task DownloadMixed_Validate_Basic_ValidNotFoundBody_DoesNotThrow()
    {
        await using DownloadMixedResponse response = await DownloadMixedResponse.CreateAsync(
            404,
            MakeStream(DownloadMixedNotFoundBody),
            ApplicationJsonContentType);

        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task DownloadMixed_Validate_Detailed_InvalidNotFoundBody_Throws()
    {
        await using DownloadMixedResponse response = await DownloadMixedResponse.CreateAsync(
            404,
            MakeStream(InvalidBody),
            ApplicationJsonContentType);

        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Detailed));
    }

    // ── HeaderNestedArrayResponse ──
    [TestMethod]
    public async Task HeaderNestedArray_MatchResult_StatusDefault_DispatchesToDefault()
    {
        await using HeaderNestedArrayResponse response = await HeaderNestedArrayResponse.CreateAsync(500, MakeStream(string.Empty));

        string result = response.MatchResult(
            matchOk: static _ => "ok",
            matchDefault: static statusCode => $"default:{statusCode}");

        Assert.AreEqual("default:500", result);
    }

    [TestMethod]
    public async Task HeaderNestedArray_MatchResultContext_Status200_DispatchesToOk()
    {
        await using HeaderNestedArrayResponse response = await HeaderNestedArrayResponse.CreateAsync(
            200,
            MakeStream(HeaderNestedArrayBody),
            ApplicationJsonContentType);

        string result = response.MatchResult(
            "ctx",
            matchOk: static (GetComplexHeaderNestedArrayOk _, in string context) => $"ok:{context}",
            matchDefault: static (int statusCode, in string context) => $"default:{statusCode}:{context}");

        Assert.AreEqual("ok:ctx", result);
    }

    [TestMethod]
    public async Task HeaderNestedArray_MatchResultContext_StatusDefault_DispatchesToDefault()
    {
        await using HeaderNestedArrayResponse response = await HeaderNestedArrayResponse.CreateAsync(500, MakeStream(string.Empty));

        string result = response.MatchResult(
            "ctx",
            matchOk: static (GetComplexHeaderNestedArrayOk _, in string context) => $"ok:{context}",
            matchDefault: static (int statusCode, in string context) => $"default:{statusCode}:{context}");

        Assert.AreEqual("default:500:ctx", result);
    }

    [TestMethod]
    public async Task HeaderNestedArray_Validate_Detailed_ValidBody_DoesNotThrow()
    {
        await using HeaderNestedArrayResponse response = await HeaderNestedArrayResponse.CreateAsync(
            200,
            MakeStream(HeaderNestedArrayBody),
            ApplicationJsonContentType);

        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task HeaderNestedArray_Validate_Basic_ValidBody_DoesNotThrow()
    {
        await using HeaderNestedArrayResponse response = await HeaderNestedArrayResponse.CreateAsync(
            200,
            MakeStream(HeaderNestedArrayBody),
            ApplicationJsonContentType);

        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task HeaderNestedArray_Validate_Detailed_InvalidBody_Throws()
    {
        await using HeaderNestedArrayResponse response = await HeaderNestedArrayResponse.CreateAsync(
            200,
            MakeStream(InvalidBody),
            ApplicationJsonContentType);

        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public async Task HeaderNestedArray_XNestedItemsHeader_WhenPresent_IsParsed()
    {
        await using HeaderNestedArrayResponse response = await HeaderNestedArrayResponse.CreateAsync(
            200,
            MakeStream(HeaderNestedArrayBody),
            ApplicationJsonContentType,
            new TestResponseHeaders(new Dictionary<string, string> { ["X-Nested-Items"] = HeaderNestedItems }));

        Assert.IsFalse(response.XNestedItemsHeader.IsUndefined());
        var items = response.XNestedItemsHeader;
        Assert.AreEqual(2, items.GetArrayLength());
        Assert.AreEqual("item-1", (string)items[0].Id);
        Assert.AreEqual(2, items[0].Tags.GetArrayLength());
        Assert.AreEqual("a", (string)items[0].Tags[0]);
        Assert.AreEqual("b", (string)items[0].Tags[1]);
        Assert.AreEqual("item-2", (string)items[1].Id);
        Assert.AreEqual(1, items[1].Tags.GetArrayLength());
        Assert.AreEqual("c", (string)items[1].Tags[0]);
    }

    [TestMethod]
    public async Task HeaderNestedArray_XNestedItemsHeader_WhenMissing_IsUndefined()
    {
        await using HeaderNestedArrayResponse response = await HeaderNestedArrayResponse.CreateAsync(
            200,
            MakeStream(HeaderNestedArrayBody),
            ApplicationJsonContentType);

        Assert.IsTrue(response.XNestedItemsHeader.IsUndefined());
    }

    // ── GetTextOrJsonResponse ──
    [TestMethod]
    public async Task GetTextOrJson_MatchResult_Status404_DispatchesToNotFound()
    {
        await using GetTextOrJsonResponse response = await GetTextOrJsonResponse.CreateAsync(
            404,
            MakeStream(GetTextOrJsonNotFoundBody),
            ApplicationJsonContentType);

        string result = response.MatchResult(
            matchOk: static body => $"json:{(string)body.Value}",
            matchOkString: static text => $"text:{text}",
            matchNotFound: static body => $"not-found:{(string)body.Error}",
            matchDefault: static statusCode => $"default:{statusCode}");

        Assert.AreEqual("not-found:not here", result);
    }

    [TestMethod]
    public async Task GetTextOrJson_MatchResult_StatusDefault_DispatchesToDefault()
    {
        await using GetTextOrJsonResponse response = await GetTextOrJsonResponse.CreateAsync(500, MakeStream(string.Empty));

        string result = response.MatchResult(
            matchOk: static _ => "json",
            matchOkString: static _ => "text",
            matchNotFound: static _ => "not-found",
            matchDefault: static statusCode => $"default:{statusCode}");

        Assert.AreEqual("default:500", result);
    }

    [TestMethod]
    public async Task GetTextOrJson_MatchResultContext_Status200Json_DispatchesToOk()
    {
        await using GetTextOrJsonResponse response = await GetTextOrJsonResponse.CreateAsync(
            200,
            MakeStream(GetTextOrJsonJsonBody),
            ApplicationJsonContentType);

        string result = response.MatchResult(
            "ctx",
            matchOk: static (GetTextMixedOk body, in string context) => $"json:{(string)body.Value}:{context}",
            matchOkString: static (string? text, in string context) => $"text:{text}:{context}",
            matchNotFound: static (GetTextMixedNotFound body, in string context) => $"not-found:{(string)body.Error}:{context}",
            matchDefault: static (int statusCode, in string context) => $"default:{statusCode}:{context}");

        Assert.AreEqual("json:json-data:ctx", result);
    }

    [TestMethod]
    public async Task GetTextOrJson_MatchResultContext_Status200Text_DispatchesToOkString()
    {
        await using GetTextOrJsonResponse response = await GetTextOrJsonResponse.CreateAsync(
            200,
            MakeStream("plain text response"),
            TextPlainContentType);

        string result = response.MatchResult(
            "ctx",
            matchOk: static (GetTextMixedOk body, in string context) => $"json:{(string)body.Value}:{context}",
            matchOkString: static (string? text, in string context) => $"text:{text}:{context}",
            matchNotFound: static (GetTextMixedNotFound body, in string context) => $"not-found:{(string)body.Error}:{context}",
            matchDefault: static (int statusCode, in string context) => $"default:{statusCode}:{context}");

        Assert.AreEqual("text:plain text response:ctx", result);
    }

    [TestMethod]
    public async Task GetTextOrJson_MatchResultContext_Status404_DispatchesToNotFound()
    {
        await using GetTextOrJsonResponse response = await GetTextOrJsonResponse.CreateAsync(
            404,
            MakeStream(GetTextOrJsonNotFoundBody),
            ApplicationJsonContentType);

        string result = response.MatchResult(
            "ctx",
            matchOk: static (GetTextMixedOk body, in string context) => $"json:{(string)body.Value}:{context}",
            matchOkString: static (string? text, in string context) => $"text:{text}:{context}",
            matchNotFound: static (GetTextMixedNotFound body, in string context) => $"not-found:{(string)body.Error}:{context}",
            matchDefault: static (int statusCode, in string context) => $"default:{statusCode}:{context}");

        Assert.AreEqual("not-found:not here:ctx", result);
    }

    [TestMethod]
    public async Task GetTextOrJson_MatchResultContext_StatusDefault_DispatchesToDefault()
    {
        await using GetTextOrJsonResponse response = await GetTextOrJsonResponse.CreateAsync(500, MakeStream(string.Empty));

        string result = response.MatchResult(
            "ctx",
            matchOk: static (GetTextMixedOk _, in string context) => $"json:{context}",
            matchOkString: static (string? _, in string context) => $"text:{context}",
            matchNotFound: static (GetTextMixedNotFound _, in string context) => $"not-found:{context}",
            matchDefault: static (int statusCode, in string context) => $"default:{statusCode}:{context}");

        Assert.AreEqual("default:500:ctx", result);
    }

    [TestMethod]
    public async Task GetTextOrJson_Validate_Detailed_Status200Json_ValidBody_DoesNotThrow()
    {
        await using GetTextOrJsonResponse response = await GetTextOrJsonResponse.CreateAsync(
            200,
            MakeStream(GetTextOrJsonJsonBody),
            ApplicationJsonContentType);

        response.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public async Task GetTextOrJson_Validate_Basic_Status404_ValidBody_DoesNotThrow()
    {
        await using GetTextOrJsonResponse response = await GetTextOrJsonResponse.CreateAsync(
            404,
            MakeStream(GetTextOrJsonNotFoundBody),
            ApplicationJsonContentType);

        response.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public async Task GetTextOrJson_Validate_Detailed_Status200Json_InvalidBody_Throws()
    {
        await using GetTextOrJsonResponse response = await GetTextOrJsonResponse.CreateAsync(
            200,
            MakeStream(InvalidBody),
            ApplicationJsonContentType);

        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public async Task GetTextOrJson_Validate_Basic_Status404_InvalidBody_Throws()
    {
        await using GetTextOrJsonResponse response = await GetTextOrJsonResponse.CreateAsync(
            404,
            MakeStream(InvalidBody),
            ApplicationJsonContentType);

        Assert.ThrowsExactly<InvalidOperationException>(() => response.Validate(ValidationMode.Basic));
    }

    private static MemoryStream MakeStream(string json) => new(System.Text.Encoding.UTF8.GetBytes(json));

    private static IEnumerable<(string Name, Action Action)> GetRequestValidateNoneCases()
    {
        yield return RequestNoneCase("GetByFlagRequest", new GetByFlagRequest(JsonBoolean.ParseValue("42"u8), JsonString.ParseValue("\"valid\""u8), JsonInt32.ParseValue("1"u8)));
        yield return RequestNoneCase("GetItemRequest", new GetItemRequest(JsonString.ParseValue("42"u8)));
        yield return RequestNoneCase("GetItemTagRequest", new GetItemTagRequest(JsonInt64.ParseValue("\"notint\""u8), JsonString.ParseValue("\"valid\""u8)));
        yield return RequestNoneCase("GetOrderRequest", new GetOrderRequest(JsonUuid.ParseValue("\"not-a-uuid\""u8)));
        yield return RequestNoneCase("GetPageRequest", new GetPageRequest(JsonInt32.ParseValue("\"notnum\""u8)));
        yield return RequestNoneCase("GetSessionProfileRequest", new GetSessionProfileRequest(JsonString.ParseValue("42"u8)));
        yield return RequestNoneCase("GetPreferencesRequest", new GetPreferencesRequest(JsonString.ParseValue("42"u8)));
        yield return RequestNoneCase("GetDocumentRequest", new GetDocumentRequest(JsonString.ParseValue("42"u8)));
        yield return RequestNoneCase("GetItemDetailsRequest", new GetItemDetailsRequest(JsonString.ParseValue("42"u8)));
        yield return RequestNoneCase("SearchRequest", new SearchRequest(JsonString.ParseValue("42"u8)));
        yield return RequestNoneCase("SearchWithQuerystringRequest", new SearchWithQuerystringRequest(GetSearchWithQuerystringQs.ParseValue("42"u8)));
        yield return RequestNoneCase("QuerySearchRequest", new QuerySearchRequest { Limit = JsonInt32.ParseValue("\"notnum\""u8) });
        yield return RequestNoneCase("HeadItemRequest", new HeadItemRequest(JsonString.ParseValue("42"u8)));
        yield return RequestNoneCase("CopyItemRequest", new CopyItemRequest(JsonString.ParseValue("42"u8), JsonString.ParseValue("\"valid\""u8)));
        yield return RequestNoneCase("DeleteItemRequest", new DeleteItemRequest(JsonString.ParseValue("42"u8)));
        yield return RequestNoneCase("PurgeItemRequest", new PurgeItemRequest(JsonString.ParseValue("42"u8)));
        yield return RequestNoneCase("TraceItemRequest", new TraceItemRequest(JsonString.ParseValue("42"u8)));
        yield return RequestNoneCase("PatchItemRequest", new PatchItemRequest(JsonString.ParseValue("42"u8)));
        yield return RequestNoneCase("UpdateOrderRequest", new UpdateOrderRequest(JsonUuid.ParseValue("\"not-a-uuid\""u8), JsonUuid.ParseValue("\"also-not-uuid\""u8)));
        yield return RequestNoneCase("TrackEventRequest", new TrackEventRequest(JsonString.ParseValue("42"u8)));
        yield return RequestNoneCase("CookieArrayRequest", new CookieArrayRequest(GetComplexCookieArrayColors.ParseValue("42"u8)));
        yield return RequestNoneCase("CookieArrayNonexplodeRequest", new CookieArrayNonexplodeRequest(GetComplexCookieArrayNonexplodeColors.ParseValue("42"u8)));
        yield return RequestNoneCase("CookieObjectRequest", new CookieObjectRequest(GetComplexCookieObjectPrefs.ParseValue("42"u8)));
        yield return RequestNoneCase("CookieObjectNonexplodeRequest", new CookieObjectNonexplodeRequest(GetComplexCookieObjectNonexplodePrefs.ParseValue("42"u8)));
        yield return RequestNoneCase("HeaderArrayRequest", new HeaderArrayRequest(GetComplexHeaderArrayXTags.ParseValue("42"u8)));
        yield return RequestNoneCase("HeaderObjectRequest", new HeaderObjectRequest(GetComplexHeaderObjectXDims.ParseValue("42"u8)));
        yield return RequestNoneCase("HeaderObjectExplodeRequest", new HeaderObjectExplodeRequest(GetComplexHeaderObjectExplodeXDims.ParseValue("42"u8)));
        yield return RequestNoneCase("PathArraySimpleRequest", new PathArraySimpleRequest(GetComplexPathArraySimpleByIdsIds.ParseValue("42"u8)));
        yield return RequestNoneCase("PathArrayMatrixRequest", new PathArrayMatrixRequest(GetComplexPathArrayMatrixByIdsIds.ParseValue("42"u8)));
        yield return RequestNoneCase("PathArrayLabelRequest", new PathArrayLabelRequest(GetComplexPathArrayLabelByIdsIds.ParseValue("42"u8)));
        yield return RequestNoneCase("PathObjectSimpleRequest", new PathObjectSimpleRequest(GetComplexPathObjectSimpleByDimsDims.ParseValue("42"u8)));
        yield return RequestNoneCase("PathObjectSimpleExplodeRequest", new PathObjectSimpleExplodeRequest(GetComplexPathObjectSimpleExplodeByDimsDims.ParseValue("42"u8)));
        yield return RequestNoneCase("PathObjectMatrixRequest", new PathObjectMatrixRequest(GetComplexPathObjectMatrixByDimsDims.ParseValue("42"u8)));
        yield return RequestNoneCase("PathObjectMatrixExplodeRequest", new PathObjectMatrixExplodeRequest(GetComplexPathObjectMatrixExplodeByDimsDims.ParseValue("42"u8)));
        yield return RequestNoneCase("PathObjectLabelRequest", new PathObjectLabelRequest(GetComplexPathObjectLabelByDimsDims.ParseValue("42"u8)));
        yield return RequestNoneCase("PathObjectLabelExplodeRequest", new PathObjectLabelExplodeRequest(GetComplexPathObjectLabelExplodeByDimsDims.ParseValue("42"u8)));
        yield return RequestNoneCase("QueryArrayExplodeRequest", new QueryArrayExplodeRequest(GetComplexQueryArrayExplodeColors.ParseValue("42"u8)));
        yield return RequestNoneCase("QueryArrayNonexplodeRequest", new QueryArrayNonexplodeRequest(GetComplexQueryArrayNonexplodeColors.ParseValue("42"u8)));
        yield return RequestNoneCase("QueryArrayPipeRequest", new QueryArrayPipeRequest(GetComplexQueryArrayPipeColors.ParseValue("42"u8)));
        yield return RequestNoneCase("QueryArraySpaceRequest", new QueryArraySpaceRequest(GetComplexQueryArraySpaceColors.ParseValue("42"u8)));
        yield return RequestNoneCase("QueryObjectDeepRequest", new QueryObjectDeepRequest(GetComplexQueryObjectDeepDims.ParseValue("42"u8)));
        yield return RequestNoneCase("QueryObjectExplodeRequest", new QueryObjectExplodeRequest(GetComplexQueryObjectExplodeDims.ParseValue("42"u8)));
        yield return RequestNoneCase("QueryObjectNonexplodeRequest", new QueryObjectNonexplodeRequest(GetComplexQueryObjectNonexplodeDims.ParseValue("42"u8)));
    }

    private static IEnumerable<(string Name, Action Action)> GetRequestLaterParameterCases()
    {
        yield return ("GetByFlagRequest", static () =>
        {
            GetByFlagRequest request = new(JsonBoolean.ParseValue("true"u8), JsonString.ParseValue("\"trace\""u8), JsonInt32.ParseValue("1"u8))
            {
                XDebug = JsonBoolean.ParseValue("true"u8),
                XScore = JsonDouble.ParseValue("\"x\""u8),
            };

            Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
            Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        });

        yield return ("GetItemRequest", static () =>
        {
            GetItemRequest request = new(JsonString.ParseValue("\"item-1\""u8))
            {
                Filter = JsonString.ParseValue("\"recent\""u8),
                Limit = JsonInt32.ParseValue("10"u8),
                Verbose = JsonBoolean.ParseValue("true"u8),
                XRequestId = JsonString.ParseValue("42"u8),
            };

            Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
            Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        });

        yield return ("GetItemTagRequest", static () =>
        {
            GetItemTagRequest request = new(JsonInt64.ParseValue("1"u8), JsonString.ParseValue("\"tag\""u8))
            {
                Score = JsonDouble.ParseValue("\"x\""u8),
            };

            Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
            Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        });

        yield return ("GetOrderRequest", static () =>
        {
            GetOrderRequest request = new(JsonUuid.ParseValue("\"550e8400-e29b-41d4-a716-446655440000\""u8))
            {
                XTraceId = JsonString.ParseValue("\"trace\""u8),
                Fields = JsonString.ParseValue("42"u8),
            };

            Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
            Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        });

        yield return ("GetPageRequest", static () =>
        {
            GetPageRequest request = new(JsonInt32.ParseValue("1"u8))
            {
                Offset = JsonInteger.ParseValue("\"x\""u8),
            };

            Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
            Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        });

        yield return ("GetSessionProfileRequest", static () =>
        {
            GetSessionProfileRequest request = new(JsonString.ParseValue("\"session\""u8))
            {
                Theme = JsonString.ParseValue("\"dark\""u8),
                Debug = JsonBoolean.ParseValue("true"u8),
                MaxAge = JsonInt32.ParseValue("\"x\""u8),
            };

            Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
            Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        });

        yield return ("GetPreferencesRequest", static () =>
        {
            GetPreferencesRequest request = new(JsonString.ParseValue("\"token\""u8))
            {
                Theme = JsonString.ParseValue("42"u8),
            };

            Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
            Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        });

        yield return ("SearchRequest", static () =>
        {
            SearchRequest request = new(JsonString.ParseValue("\"term\""u8))
            {
                Page = JsonInt32.ParseValue("1"u8),
                Rating = JsonSingle.ParseValue("\"x\""u8),
            };

            Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
            Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        });

        yield return ("CopyItemRequest", static () =>
        {
            CopyItemRequest request = new(JsonString.ParseValue("\"source\""u8), JsonString.ParseValue("42"u8));

            Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
            Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        });

        yield return ("UpdateOrderRequest", static () =>
        {
            UpdateOrderRequest request = new(
                JsonUuid.ParseValue("\"550e8400-e29b-41d4-a716-446655440000\""u8),
                JsonUuid.ParseValue("\"not-a-uuid\""u8));

            Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
            Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        });

        yield return ("TrackEventRequest", static () =>
        {
            TrackEventRequest request = new(JsonString.ParseValue("\"tracker\""u8))
            {
                RefUrl = JsonString.ParseValue("42"u8),
            };

            Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
            Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        });
    }

    private static IEnumerable<(string Name, Func<Task> Action)> GetResponseValidateNoneCases()
    {
        yield return ResponseNoneCase<ChatCompletionsResponse>("ChatCompletionsResponse", 400, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => ChatCompletionsResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<CookieArrayNonexplodeResponse>("CookieArrayNonexplodeResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => CookieArrayNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<CookieArrayResponse>("CookieArrayResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => CookieArrayResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<CookieObjectNonexplodeResponse>("CookieObjectNonexplodeResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => CookieObjectNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<CookieObjectResponse>("CookieObjectResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => CookieObjectResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<CopyItemResponse>("CopyItemResponse", 201, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => CopyItemResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<CreateItemResponse>("CreateItemResponse", 201, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => CreateItemResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<DeleteItemResponse>("DeleteItemResponse", 404, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => DeleteItemResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<DownloadMixedResponse>("DownloadMixedResponse", 404, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => DownloadMixedResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<GetByFlagResponse>("GetByFlagResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => GetByFlagResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<GetDocumentResponse>("GetDocumentResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => GetDocumentResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<GetItemDetailsResponse>("GetItemDetailsResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => GetItemDetailsResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<GetItemResponse>("GetItemResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => GetItemResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<GetItemTagResponse>("GetItemTagResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => GetItemTagResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<GetOrderResponse>("GetOrderResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => GetOrderResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<GetPageResponse>("GetPageResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => GetPageResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<GetPreferencesResponse>("GetPreferencesResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => GetPreferencesResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<GetSessionProfileResponse>("GetSessionProfileResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => GetSessionProfileResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<GetTextOrJsonResponse>("GetTextOrJsonResponse", 404, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => GetTextOrJsonResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<GetVendorJsonResponse>("GetVendorJsonResponse", 200, InvalidBody, VendorJsonContentType, static (statusCode, contentStream, contentType) => GetVendorJsonResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<HeaderArrayResponse>("HeaderArrayResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => HeaderArrayResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<HeaderNestedArrayResponse>("HeaderNestedArrayResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => HeaderNestedArrayResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<HeaderObjectExplodeResponse>("HeaderObjectExplodeResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => HeaderObjectExplodeResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<HeaderObjectResponse>("HeaderObjectResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => HeaderObjectResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<PatchItemResponse>("PatchItemResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => PatchItemResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<PathArrayLabelResponse>("PathArrayLabelResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => PathArrayLabelResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<PathArrayMatrixResponse>("PathArrayMatrixResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => PathArrayMatrixResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<PathArraySimpleResponse>("PathArraySimpleResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => PathArraySimpleResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<PathObjectLabelExplodeResponse>("PathObjectLabelExplodeResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => PathObjectLabelExplodeResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<PathObjectLabelResponse>("PathObjectLabelResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => PathObjectLabelResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<PathObjectMatrixExplodeResponse>("PathObjectMatrixExplodeResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => PathObjectMatrixExplodeResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<PathObjectMatrixResponse>("PathObjectMatrixResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => PathObjectMatrixResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<PathObjectSimpleExplodeResponse>("PathObjectSimpleExplodeResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => PathObjectSimpleExplodeResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<PathObjectSimpleResponse>("PathObjectSimpleResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => PathObjectSimpleResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<ProcessBatchResponse>("ProcessBatchResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => ProcessBatchResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<QueryArrayExplodeResponse>("QueryArrayExplodeResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => QueryArrayExplodeResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<QueryArrayNonexplodeResponse>("QueryArrayNonexplodeResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => QueryArrayNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<QueryArrayPipeResponse>("QueryArrayPipeResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => QueryArrayPipeResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<QueryArraySpaceResponse>("QueryArraySpaceResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => QueryArraySpaceResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<QueryObjectDeepResponse>("QueryObjectDeepResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => QueryObjectDeepResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<QueryObjectExplodeResponse>("QueryObjectExplodeResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => QueryObjectExplodeResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<QueryObjectNonexplodeResponse>("QueryObjectNonexplodeResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => QueryObjectNonexplodeResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<QuerySearchResponse>("QuerySearchResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => QuerySearchResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<SearchResponse>("SearchResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => SearchResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<SearchWithQuerystringResponse>("SearchWithQuerystringResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => SearchWithQuerystringResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<StreamEventsResponse>("StreamEventsResponse", 401, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => StreamEventsResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<SubmitContactFormResponse>("SubmitContactFormResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => SubmitContactFormResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<SubmitDefaultFormResponse>("SubmitDefaultFormResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => SubmitDefaultFormResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<SubmitEncodedContactFormResponse>("SubmitEncodedContactFormResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => SubmitEncodedContactFormResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<SubmitMultipartTypesResponse>("SubmitMultipartTypesResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => SubmitMultipartTypesResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<SubmitNonexplodedFormResponse>("SubmitNonexplodedFormResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => SubmitNonexplodedFormResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<TextToJsonResponse>("TextToJsonResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => TextToJsonResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<UpdateItemResponse>("UpdateItemResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => UpdateItemResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<UpdateOrderResponse>("UpdateOrderResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => UpdateOrderResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<UploadDocMixedResponse>("UploadDocMixedResponse", 201, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => UploadDocMixedResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<UploadDocumentResponse>("UploadDocumentResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => UploadDocumentResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<UploadEncodedDocumentResponse>("UploadEncodedDocumentResponse", 200, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => UploadEncodedDocumentResponse.CreateAsync(statusCode, contentStream, contentType));
        yield return ResponseNoneCase<UploadFileResponse>("UploadFileResponse", 201, InvalidBody, ApplicationJsonContentType, static (statusCode, contentStream, contentType) => UploadFileResponse.CreateAsync(statusCode, contentStream, contentType));
    }

    private static (string Name, Action Action) RequestNoneCase<TRequest>(string name, TRequest request)
        where TRequest : struct, IApiRequest<TRequest>
    {
        return (name, () => request.Validate(ValidationMode.None));
    }

    private static (string Name, Func<Task> Action) ResponseNoneCase<TResponse>(
        string name,
        int statusCode,
        string body,
        string? contentType,
        Func<int, Stream, string?, ValueTask<TResponse>> create)
        where TResponse : struct, IApiResponse<TResponse>, IAsyncDisposable
    {
        return (name, async () =>
        {
            await using TResponse response = await create(statusCode, MakeStream(body), contentType);
            response.Validate(ValidationMode.None);
        });
    }

    private sealed class TestResponseHeaders : IResponseHeaders
    {
        private readonly Dictionary<string, string> headers;

        public TestResponseHeaders(Dictionary<string, string> headers)
        {
            this.headers = new(headers, StringComparer.OrdinalIgnoreCase);
        }

        public bool TryGetValue(string headerName, out string? value)
        {
            if (this.headers.TryGetValue(headerName, out string? rawValue))
            {
                value = rawValue;
                return true;
            }

            value = null;
            return false;
        }
    }
}