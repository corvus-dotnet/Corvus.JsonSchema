// <copyright file="RequestLaterParamValidationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using CanonTests32.Client;
using CanonTests32.Client.Models;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.OpenApi32.Runtime.Tests;

/// <summary>
/// Tests that exercise validation failure paths for 2nd, 3rd, and optional parameters
/// in multi-parameter request structs (GetByFlag, GetItem, GetOrder, GetItemTag,
/// GetPage, GetSessionProfile, Search, TrackEvent, HeadItem, PurgeItem, QuerySearch,
/// and various cookie/query array/object requests).
/// </summary>
[TestClass]
public class RequestLaterParamValidationTests
{
    // --- GetByFlagRequest: active(path), X-Trace-Id(header), X-Request-Count(header), X-Debug(optional), X-Score(optional) ---
    [TestMethod]
    public void GetByFlag_InvalidXTraceId_Basic()
    {
        // active is valid (boolean), X-Trace-Id must be string but we give a number
        var request = new GetByFlagRequest(
            JsonBoolean.ParseValue("true"u8),
            JsonString.From(JsonElement.ParseValue("42"u8)),
            JsonInt32.ParseValue("1"u8));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "X-Trace-Id");
    }

    [TestMethod]
    public void GetByFlag_InvalidXTraceId_Detailed()
    {
        var request = new GetByFlagRequest(
            JsonBoolean.ParseValue("true"u8),
            JsonString.From(JsonElement.ParseValue("42"u8)),
            JsonInt32.ParseValue("1"u8));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "X-Trace-Id");
    }

    [TestMethod]
    public void GetByFlag_InvalidXRequestCount_Basic()
    {
        var request = new GetByFlagRequest(
            JsonBoolean.ParseValue("true"u8),
            JsonString.ParseValue("\"trace-123\""u8),
            JsonInt32.From(JsonElement.ParseValue("\"not-a-number\""u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "X-Request-Count");
    }

    [TestMethod]
    public void GetByFlag_InvalidXRequestCount_Detailed()
    {
        var request = new GetByFlagRequest(
            JsonBoolean.ParseValue("true"u8),
            JsonString.ParseValue("\"trace-123\""u8),
            JsonInt32.From(JsonElement.ParseValue("\"not-a-number\""u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "X-Request-Count");
    }

    [TestMethod]
    public void GetByFlag_InvalidXDebug_Basic()
    {
        var request = new GetByFlagRequest(
            JsonBoolean.ParseValue("true"u8),
            JsonString.ParseValue("\"trace-123\""u8),
            JsonInt32.ParseValue("1"u8))
        { XDebug = JsonBoolean.From(JsonElement.ParseValue("\"not-a-bool\""u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "X-Debug");
    }

    [TestMethod]
    public void GetByFlag_InvalidXDebug_Detailed()
    {
        var request = new GetByFlagRequest(
            JsonBoolean.ParseValue("true"u8),
            JsonString.ParseValue("\"trace-123\""u8),
            JsonInt32.ParseValue("1"u8))
        { XDebug = JsonBoolean.From(JsonElement.ParseValue("\"not-a-bool\""u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "X-Debug");
    }

    [TestMethod]
    public void GetByFlag_InvalidXScore_Basic()
    {
        var request = new GetByFlagRequest(
            JsonBoolean.ParseValue("true"u8),
            JsonString.ParseValue("\"trace-123\""u8),
            JsonInt32.ParseValue("1"u8))
        { XScore = JsonDouble.From(JsonElement.ParseValue("\"not-a-number\""u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "X-Score");
    }

    [TestMethod]
    public void GetByFlag_InvalidXScore_Detailed()
    {
        var request = new GetByFlagRequest(
            JsonBoolean.ParseValue("true"u8),
            JsonString.ParseValue("\"trace-123\""u8),
            JsonInt32.ParseValue("1"u8))
        { XScore = JsonDouble.From(JsonElement.ParseValue("\"not-a-number\""u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "X-Score");
    }

    // --- GetByFlagRequest: WriteHeaders path with optional headers ---
    [TestMethod]
    public void GetByFlag_WriteHeaders_WithOptionalParams()
    {
        var request = new GetByFlagRequest(
            JsonBoolean.ParseValue("true"u8),
            JsonString.ParseValue("\"trace-123\""u8),
            JsonInt32.ParseValue("42"u8))
        { XDebug = JsonBoolean.ParseValue("true"u8), XScore = JsonDouble.ParseValue("9.5"u8) };

        const int headerCount = 0;
        request.WriteHeaders(
            static (ReadOnlySpan<byte> name, ReadOnlySpan<byte> value, int state) => { },
            headerCount);
    }

    // --- GetByFlagRequest: static properties ---
    [TestMethod]
    public void GetByFlag_PathTemplateUtf8()
    {
        ReadOnlySpan<byte> path = GetByFlagRequest.PathTemplateUtf8;
        Assert.IsTrue(path.Length > 0);
    }

    [TestMethod]
    public void GetByFlag_Method() => Assert.AreEqual(OperationMethod.Get, GetByFlagRequest.Method);

    // --- GetItemRequest: itemId(path) required; Filter, Limit, Verbose, XRequestId optional via init ---
    [TestMethod]
    public void GetItem_InvalidFilter_Basic()
    {
        var request = new GetItemRequest(
            JsonString.ParseValue("\"item-1\""u8))
        { Filter = JsonString.From(JsonElement.ParseValue("42"u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "filter");
    }

    [TestMethod]
    public void GetItem_InvalidFilter_Detailed()
    {
        var request = new GetItemRequest(
            JsonString.ParseValue("\"item-1\""u8))
        { Filter = JsonString.From(JsonElement.ParseValue("42"u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "filter");
    }

    // --- GetOrderRequest: orderId(path, JsonUuid) required; XTraceId, Fields optional via init ---
    [TestMethod]
    public void GetOrder_InvalidFields_Basic()
    {
        var request = new GetOrderRequest(
            JsonUuid.ParseValue("\"550e8400-e29b-41d4-a716-446655440000\""u8))
        { Fields = JsonString.From(JsonElement.ParseValue("42"u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "fields");
    }

    [TestMethod]
    public void GetOrder_InvalidFields_Detailed()
    {
        var request = new GetOrderRequest(
            JsonUuid.ParseValue("\"550e8400-e29b-41d4-a716-446655440000\""u8))
        { Fields = JsonString.From(JsonElement.ParseValue("42"u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "fields");
    }

    // --- GetItemTagRequest: itemId(path, JsonInt64), tagName(path, JsonString) ---
    [TestMethod]
    public void GetItemTag_InvalidTagName_Basic()
    {
        var request = new GetItemTagRequest(
            JsonInt64.ParseValue("123"u8),
            JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "tagName");
    }

    [TestMethod]
    public void GetItemTag_InvalidTagName_Detailed()
    {
        var request = new GetItemTagRequest(
            JsonInt64.ParseValue("123"u8),
            JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "tagName");
    }

    // --- GetPageRequest: pageNum(query, JsonInt32) required; Offset optional via init ---
    [TestMethod]
    public void GetPage_InvalidOffset_Basic()
    {
        var request = new GetPageRequest(
            JsonInt32.ParseValue("1"u8))
        { Offset = JsonInteger.From(JsonElement.ParseValue("\"not-int\""u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "offset");
    }

    [TestMethod]
    public void GetPage_InvalidOffset_Detailed()
    {
        var request = new GetPageRequest(
            JsonInt32.ParseValue("1"u8))
        { Offset = JsonInteger.From(JsonElement.ParseValue("\"not-int\""u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "offset");
    }

    // --- SearchRequest: q(query, JsonString) required; Page, Rating optional via init ---
    [TestMethod]
    public void Search_InvalidPage_Basic()
    {
        var request = new SearchRequest(
            JsonString.ParseValue("\"test\""u8))
        { Page = JsonInt32.From(JsonElement.ParseValue("\"not-int\""u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "page");
    }

    [TestMethod]
    public void Search_InvalidPage_Detailed()
    {
        var request = new SearchRequest(
            JsonString.ParseValue("\"test\""u8))
        { Page = JsonInt32.From(JsonElement.ParseValue("\"not-int\""u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "page");
    }

    // --- HeadItemRequest: only itemId(path) — test validation failure ---
    [TestMethod]
    public void HeadItem_InvalidItemId_Basic()
    {
        var request = new HeadItemRequest(
            JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "itemId");
    }

    [TestMethod]
    public void HeadItem_InvalidItemId_Detailed()
    {
        var request = new HeadItemRequest(
            JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "itemId");
    }

    // --- PurgeItemRequest: only itemId(path) — test validation failure ---
    [TestMethod]
    public void PurgeItem_InvalidItemId_Basic()
    {
        var request = new PurgeItemRequest(
            JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "itemId");
    }

    [TestMethod]
    public void PurgeItem_InvalidItemId_Detailed()
    {
        var request = new PurgeItemRequest(
            JsonString.From(JsonElement.ParseValue("42"u8)));
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "itemId");
    }

    // --- QuerySearchRequest: Limit (init property) ---
    [TestMethod]
    public void QuerySearch_InvalidLimit_Basic()
    {
        var request = new QuerySearchRequest { Limit = JsonInt32.From(JsonElement.ParseValue("\"not-int\""u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "limit");
    }

    [TestMethod]
    public void QuerySearch_InvalidLimit_Detailed()
    {
        var request = new QuerySearchRequest { Limit = JsonInt32.From(JsonElement.ParseValue("\"not-int\""u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "limit");
    }

    // --- TrackEventRequest: tracker_id(cookie) required, ref_url(cookie) optional ---
    [TestMethod]
    public void TrackEvent_InvalidRefUrl_Basic()
    {
        var request = new TrackEventRequest(
            JsonString.ParseValue("\"tracker-1\""u8))
        { RefUrl = JsonString.From(JsonElement.ParseValue("42"u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "ref_url");
    }

    [TestMethod]
    public void TrackEvent_InvalidRefUrl_Detailed()
    {
        var request = new TrackEventRequest(
            JsonString.ParseValue("\"tracker-1\""u8))
        { RefUrl = JsonString.From(JsonElement.ParseValue("42"u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "ref_url");
    }

    // --- GetPreferencesRequest: session_token(cookie) required, theme(cookie) optional ---
    [TestMethod]
    public void GetPreferences_InvalidTheme_Basic()
    {
        var request = new GetPreferencesRequest(
            JsonString.ParseValue("\"sess-1\""u8))
        { Theme = JsonString.From(JsonElement.ParseValue("42"u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "theme");
    }

    [TestMethod]
    public void GetPreferences_InvalidTheme_Detailed()
    {
        var request = new GetPreferencesRequest(
            JsonString.ParseValue("\"sess-1\""u8))
        { Theme = JsonString.From(JsonElement.ParseValue("42"u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "theme");
    }

    // --- GetPreferencesRequest: WriteCookies with optional theme ---
    [TestMethod]
    public void GetPreferences_WriteCookies_WithTheme()
    {
        var request = new GetPreferencesRequest(
            JsonString.ParseValue("\"sess-1\""u8))
        { Theme = JsonString.ParseValue("\"dark\""u8) };
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        int written = request.WriteCookies(buffer);
        Assert.IsTrue(written > 0);
    }

    // --- TrackEventRequest: WriteCookies with optional ref_url ---
    [TestMethod]
    public void TrackEvent_WriteCookies_WithRefUrl()
    {
        var request = new TrackEventRequest(
            JsonString.ParseValue("\"tracker-1\""u8))
        { RefUrl = JsonString.ParseValue("\"https://example.com\""u8) };
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        int written = request.WriteCookies(buffer);
        Assert.IsTrue(written > 0);
    }

    // --- Static property access (covers PathTemplateUtf8 and Method for complex requests) ---
    [TestMethod]
    public void GetItem_PathTemplateUtf8()
    {
        ReadOnlySpan<byte> path = GetItemRequest.PathTemplateUtf8;
        Assert.IsTrue(path.Length > 0);
    }

    [TestMethod]
    public void GetOrder_PathTemplateUtf8()
    {
        ReadOnlySpan<byte> path = GetOrderRequest.PathTemplateUtf8;
        Assert.IsTrue(path.Length > 0);
    }

    [TestMethod]
    public void Search_PathTemplateUtf8()
    {
        ReadOnlySpan<byte> path = SearchRequest.PathTemplateUtf8;
        Assert.IsTrue(path.Length > 0);
    }

    [TestMethod]
    public void GetItemTag_PathTemplateUtf8()
    {
        ReadOnlySpan<byte> path = GetItemTagRequest.PathTemplateUtf8;
        Assert.IsTrue(path.Length > 0);
    }

    [TestMethod]
    public void GetSessionProfile_PathTemplateUtf8()
    {
        ReadOnlySpan<byte> path = GetSessionProfileRequest.PathTemplateUtf8;
        Assert.IsTrue(path.Length > 0);
    }

    [TestMethod]
    public void HeadItem_PathTemplateUtf8()
    {
        ReadOnlySpan<byte> path = HeadItemRequest.PathTemplateUtf8;
        Assert.IsTrue(path.Length > 0);
    }

    [TestMethod]
    public void PurgeItem_PathTemplateUtf8()
    {
        ReadOnlySpan<byte> path = PurgeItemRequest.PathTemplateUtf8;
        Assert.IsTrue(path.Length > 0);
    }

    // --- GetSessionProfile optional param failures (Theme, Debug, MaxAge) ---
    [TestMethod]
    public void GetSessionProfile_InvalidTheme_Basic()
    {
        var request = new GetSessionProfileRequest(JsonString.ParseValue("\"sess-1\""u8))
        { Theme = JsonString.From(JsonElement.ParseValue("42"u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "theme");
    }

    [TestMethod]
    public void GetSessionProfile_InvalidTheme_Detailed()
    {
        var request = new GetSessionProfileRequest(JsonString.ParseValue("\"sess-1\""u8))
        { Theme = JsonString.From(JsonElement.ParseValue("42"u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "theme");
    }

    [TestMethod]
    public void GetSessionProfile_InvalidDebug_Basic()
    {
        var request = new GetSessionProfileRequest(JsonString.ParseValue("\"sess-1\""u8))
        { Debug = JsonBoolean.From(JsonElement.ParseValue("42"u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "debug");
    }

    [TestMethod]
    public void GetSessionProfile_InvalidDebug_Detailed()
    {
        var request = new GetSessionProfileRequest(JsonString.ParseValue("\"sess-1\""u8))
        { Debug = JsonBoolean.From(JsonElement.ParseValue("42"u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "debug");
    }

    [TestMethod]
    public void GetSessionProfile_InvalidMaxAge_Basic()
    {
        var request = new GetSessionProfileRequest(JsonString.ParseValue("\"sess-1\""u8))
        { MaxAge = JsonInt32.From(JsonElement.ParseValue("\"not-int\""u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "max_age");
    }

    [TestMethod]
    public void GetSessionProfile_InvalidMaxAge_Detailed()
    {
        var request = new GetSessionProfileRequest(JsonString.ParseValue("\"sess-1\""u8))
        { MaxAge = JsonInt32.From(JsonElement.ParseValue("\"not-int\""u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "max_age");
    }

    // --- GetItem optional param failures (Limit, Verbose, XRequestId) ---
    [TestMethod]
    public void GetItem_InvalidLimit_Basic()
    {
        var request = new GetItemRequest(JsonString.ParseValue("\"item-1\""u8))
        { Limit = JsonInt32.From(JsonElement.ParseValue("\"not-int\""u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "limit");
    }

    [TestMethod]
    public void GetItem_InvalidLimit_Detailed()
    {
        var request = new GetItemRequest(JsonString.ParseValue("\"item-1\""u8))
        { Limit = JsonInt32.From(JsonElement.ParseValue("\"not-int\""u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "limit");
    }

    [TestMethod]
    public void GetItem_InvalidVerbose_Basic()
    {
        var request = new GetItemRequest(JsonString.ParseValue("\"item-1\""u8))
        { Verbose = JsonBoolean.From(JsonElement.ParseValue("42"u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "verbose");
    }

    [TestMethod]
    public void GetItem_InvalidVerbose_Detailed()
    {
        var request = new GetItemRequest(JsonString.ParseValue("\"item-1\""u8))
        { Verbose = JsonBoolean.From(JsonElement.ParseValue("42"u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "verbose");
    }

    [TestMethod]
    public void GetItem_InvalidXRequestId_Basic()
    {
        var request = new GetItemRequest(JsonString.ParseValue("\"item-1\""u8))
        { XRequestId = JsonString.From(JsonElement.ParseValue("42"u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "X-Request-Id");
    }

    [TestMethod]
    public void GetItem_InvalidXRequestId_Detailed()
    {
        var request = new GetItemRequest(JsonString.ParseValue("\"item-1\""u8))
        { XRequestId = JsonString.From(JsonElement.ParseValue("42"u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "X-Request-Id");
    }

    // --- GetOrder optional param failures (XTraceId) ---
    [TestMethod]
    public void GetOrder_InvalidXTraceId_Basic()
    {
        var request = new GetOrderRequest(JsonUuid.ParseValue("\"550e8400-e29b-41d4-a716-446655440000\""u8))
        { XTraceId = JsonString.From(JsonElement.ParseValue("42"u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "X-Trace-Id");
    }

    [TestMethod]
    public void GetOrder_InvalidXTraceId_Detailed()
    {
        var request = new GetOrderRequest(JsonUuid.ParseValue("\"550e8400-e29b-41d4-a716-446655440000\""u8))
        { XTraceId = JsonString.From(JsonElement.ParseValue("42"u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "X-Trace-Id");
    }

    // --- Search optional param failures (Rating) ---
    [TestMethod]
    public void Search_InvalidRating_Basic()
    {
        var request = new SearchRequest(JsonString.ParseValue("\"test\""u8))
        { Rating = JsonSingle.From(JsonElement.ParseValue("\"not-num\""u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
        StringAssert.Contains(ex.Message, "rating");
    }

    [TestMethod]
    public void Search_InvalidRating_Detailed()
    {
        var request = new SearchRequest(JsonString.ParseValue("\"test\""u8))
        { Rating = JsonSingle.From(JsonElement.ParseValue("\"not-num\""u8)) };
        var ex = Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
        StringAssert.Contains(ex.Message, "rating");
    }

    // --- Validate with ALL optional params valid (covers optional-param-passes closing braces) ---
    [TestMethod]
    public void GetSessionProfile_AllOptionalValid_Basic()
    {
        var request = new GetSessionProfileRequest(JsonString.ParseValue("\"sess-1\""u8))
        {
            Theme = JsonString.ParseValue("\"dark\""u8),
            Debug = JsonBoolean.ParseValue("true"u8),
            MaxAge = JsonInt32.ParseValue("3600"u8),
        };
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void GetSessionProfile_AllOptionalValid_Detailed()
    {
        var request = new GetSessionProfileRequest(JsonString.ParseValue("\"sess-1\""u8))
        {
            Theme = JsonString.ParseValue("\"dark\""u8),
            Debug = JsonBoolean.ParseValue("true"u8),
            MaxAge = JsonInt32.ParseValue("3600"u8),
        };
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void GetItem_AllOptionalValid_Basic()
    {
        var request = new GetItemRequest(JsonString.ParseValue("\"item-1\""u8))
        {
            Filter = JsonString.ParseValue("\"active\""u8),
            Limit = JsonInt32.ParseValue("10"u8),
            Verbose = JsonBoolean.ParseValue("true"u8),
            XRequestId = JsonString.ParseValue("\"req-123\""u8),
        };
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void GetItem_AllOptionalValid_Detailed()
    {
        var request = new GetItemRequest(JsonString.ParseValue("\"item-1\""u8))
        {
            Filter = JsonString.ParseValue("\"active\""u8),
            Limit = JsonInt32.ParseValue("10"u8),
            Verbose = JsonBoolean.ParseValue("true"u8),
            XRequestId = JsonString.ParseValue("\"req-123\""u8),
        };
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void GetOrder_AllOptionalValid_Basic()
    {
        var request = new GetOrderRequest(JsonUuid.ParseValue("\"550e8400-e29b-41d4-a716-446655440000\""u8))
        {
            XTraceId = JsonString.ParseValue("\"trace-abc\""u8),
            Fields = JsonString.ParseValue("\"name,date\""u8),
        };
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void GetOrder_AllOptionalValid_Detailed()
    {
        var request = new GetOrderRequest(JsonUuid.ParseValue("\"550e8400-e29b-41d4-a716-446655440000\""u8))
        {
            XTraceId = JsonString.ParseValue("\"trace-abc\""u8),
            Fields = JsonString.ParseValue("\"name,date\""u8),
        };
        request.Validate(ValidationMode.Detailed);
    }

    [TestMethod]
    public void Search_AllOptionalValid_Basic()
    {
        var request = new SearchRequest(JsonString.ParseValue("\"test\""u8))
        {
            Page = JsonInt32.ParseValue("1"u8),
            Rating = JsonSingle.ParseValue("4.5"u8),
        };
        request.Validate(ValidationMode.Basic);
    }

    [TestMethod]
    public void Search_AllOptionalValid_Detailed()
    {
        var request = new SearchRequest(JsonString.ParseValue("\"test\""u8))
        {
            Page = JsonInt32.ParseValue("1"u8),
            Rating = JsonSingle.ParseValue("4.5"u8),
        };
        request.Validate(ValidationMode.Detailed);
    }

    // --- WriteQueryString with optional params set (covers separator paths) ---
    [TestMethod]
    public void GetPage_WriteQueryString_WithOffset()
    {
        var request = new GetPageRequest(JsonInt32.ParseValue("1"u8))
        { Offset = JsonInteger.ParseValue("50"u8) };
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        int written = request.WriteQueryString(buffer);
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void GetItem_WriteQueryString_WithOptionals()
    {
        var request = new GetItemRequest(JsonString.ParseValue("\"item-1\""u8))
        {
            Filter = JsonString.ParseValue("\"active\""u8),
            Limit = JsonInt32.ParseValue("10"u8),
        };
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        int written = request.WriteQueryString(buffer);
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void Search_WriteQueryString_WithOptionals()
    {
        var request = new SearchRequest(JsonString.ParseValue("\"test\""u8))
        {
            Page = JsonInt32.ParseValue("1"u8),
            Rating = JsonSingle.ParseValue("4.5"u8),
        };
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        int written = request.WriteQueryString(buffer);
        Assert.IsTrue(written > 0);
    }

    // --- WriteCookies with all optional params (covers separator + optional writing paths) ---
    [TestMethod]
    public void GetSessionProfile_WriteCookies_AllOptional()
    {
        var request = new GetSessionProfileRequest(JsonString.ParseValue("\"sess-1\""u8))
        {
            Theme = JsonString.ParseValue("\"dark\""u8),
            Debug = JsonBoolean.ParseValue("true"u8),
            MaxAge = JsonInt32.ParseValue("3600"u8),
        };
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        int written = request.WriteCookies(buffer);
        Assert.IsTrue(written > 0);
    }
}