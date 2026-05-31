// <copyright file="RequestValidationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using CanonTests32.Client;
using CanonTests32.Client.Models;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi32.Runtime.Tests;

/// <summary>
/// Tests that exercise the <c>Validate()</c> method on generated Request structs
/// with invalid parameter values to trigger schema validation failure paths.
/// </summary>
[TestClass]
public class RequestValidationTests
{
    // ── GetByFlag ──
    [TestMethod]
    public void GetByFlag_Validate_Detailed_ThrowsOnInvalidParam()
    {
        GetByFlagRequest request = new(
            JsonBoolean.ParseValue("42"u8),
            JsonString.ParseValue("\"valid\""u8),
            JsonInt32.ParseValue("1"u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void GetByFlag_Validate_Basic_ThrowsOnInvalidParam()
    {
        GetByFlagRequest request = new(
            JsonBoolean.ParseValue("42"u8),
            JsonString.ParseValue("\"valid\""u8),
            JsonInt32.ParseValue("1"u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // ── GetItem ──
    [TestMethod]
    public void GetItem_Validate_Detailed_ThrowsOnInvalidParam()
    {
        GetItemRequest request = new(JsonString.ParseValue("42"u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void GetItem_Validate_Basic_ThrowsOnInvalidParam()
    {
        GetItemRequest request = new(JsonString.ParseValue("42"u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // ── GetItemDetails ──
    [TestMethod]
    public void GetItemDetails_Validate_Detailed_ThrowsOnInvalidParam()
    {
        GetItemDetailsRequest request = new(JsonString.ParseValue("42"u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void GetItemDetails_Validate_Basic_ThrowsOnInvalidParam()
    {
        GetItemDetailsRequest request = new(JsonString.ParseValue("42"u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // ── GetItemTag ──
    [TestMethod]
    public void GetItemTag_Validate_Detailed_ThrowsOnInvalidParam()
    {
        GetItemTagRequest request = new(
            JsonInt64.ParseValue("\"notint\""u8),
            JsonString.ParseValue("\"valid\""u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void GetItemTag_Validate_Basic_ThrowsOnInvalidParam()
    {
        GetItemTagRequest request = new(
            JsonInt64.ParseValue("\"notint\""u8),
            JsonString.ParseValue("\"valid\""u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // ── GetOrder ──
    [TestMethod]
    public void GetOrder_Validate_Detailed_ThrowsOnInvalidParam()
    {
        GetOrderRequest request = new(JsonUuid.ParseValue("\"not-a-uuid\""u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void GetOrder_Validate_Basic_ThrowsOnInvalidParam()
    {
        GetOrderRequest request = new(JsonUuid.ParseValue("\"not-a-uuid\""u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // ── GetPage ──
    [TestMethod]
    public void GetPage_Validate_Detailed_ThrowsOnInvalidParam()
    {
        GetPageRequest request = new(JsonInt32.ParseValue("\"notnum\""u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void GetPage_Validate_Basic_ThrowsOnInvalidParam()
    {
        GetPageRequest request = new(JsonInt32.ParseValue("\"notnum\""u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // ── GetPreferences ──
    [TestMethod]
    public void GetPreferences_Validate_Detailed_ThrowsOnInvalidParam()
    {
        GetPreferencesRequest request = new(JsonString.ParseValue("42"u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void GetPreferences_Validate_Basic_ThrowsOnInvalidParam()
    {
        GetPreferencesRequest request = new(JsonString.ParseValue("42"u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // ── GetSessionProfile ──
    [TestMethod]
    public void GetSessionProfile_Validate_Detailed_ThrowsOnInvalidParam()
    {
        GetSessionProfileRequest request = new(JsonString.ParseValue("42"u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void GetSessionProfile_Validate_Basic_ThrowsOnInvalidParam()
    {
        GetSessionProfileRequest request = new(JsonString.ParseValue("42"u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // ── Search ──
    [TestMethod]
    public void Search_Validate_Detailed_ThrowsOnInvalidParam()
    {
        SearchRequest request = new(JsonString.ParseValue("42"u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void Search_Validate_Basic_ThrowsOnInvalidParam()
    {
        SearchRequest request = new(JsonString.ParseValue("42"u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // ── TrackEvent ──
    [TestMethod]
    public void TrackEvent_Validate_Detailed_ThrowsOnInvalidParam()
    {
        TrackEventRequest request = new(JsonString.ParseValue("42"u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void TrackEvent_Validate_Basic_ThrowsOnInvalidParam()
    {
        TrackEventRequest request = new(JsonString.ParseValue("42"u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // ── CopyItem ──
    [TestMethod]
    public void CopyItem_Validate_Detailed_ThrowsOnInvalidParam()
    {
        CopyItemRequest request = new(
            JsonString.ParseValue("42"u8),
            JsonString.ParseValue("\"valid\""u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void CopyItem_Validate_Basic_ThrowsOnInvalidParam()
    {
        CopyItemRequest request = new(
            JsonString.ParseValue("42"u8),
            JsonString.ParseValue("\"valid\""u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // ── DeleteItem ──
    [TestMethod]
    public void DeleteItem_Validate_Detailed_ThrowsOnInvalidParam()
    {
        DeleteItemRequest request = new(JsonString.ParseValue("42"u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void DeleteItem_Validate_Basic_ThrowsOnInvalidParam()
    {
        DeleteItemRequest request = new(JsonString.ParseValue("42"u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // ── HeadItem ──
    [TestMethod]
    public void HeadItem_Validate_Detailed_ThrowsOnInvalidParam()
    {
        HeadItemRequest request = new(JsonString.ParseValue("42"u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void HeadItem_Validate_Basic_ThrowsOnInvalidParam()
    {
        HeadItemRequest request = new(JsonString.ParseValue("42"u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // ── PurgeItem ──
    [TestMethod]
    public void PurgeItem_Validate_Detailed_ThrowsOnInvalidParam()
    {
        PurgeItemRequest request = new(JsonString.ParseValue("42"u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void PurgeItem_Validate_Basic_ThrowsOnInvalidParam()
    {
        PurgeItemRequest request = new(JsonString.ParseValue("42"u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // ── TraceItem ──
    [TestMethod]
    public void TraceItem_Validate_Detailed_ThrowsOnInvalidParam()
    {
        TraceItemRequest request = new(JsonString.ParseValue("42"u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void TraceItem_Validate_Basic_ThrowsOnInvalidParam()
    {
        TraceItemRequest request = new(JsonString.ParseValue("42"u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // ── PatchItem ──
    [TestMethod]
    public void PatchItem_Validate_Detailed_ThrowsOnInvalidParam()
    {
        PatchItemRequest request = new(JsonString.ParseValue("42"u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void PatchItem_Validate_Basic_ThrowsOnInvalidParam()
    {
        PatchItemRequest request = new(JsonString.ParseValue("42"u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // ── GetDocument ──
    [TestMethod]
    public void GetDocument_Validate_Detailed_ThrowsOnInvalidParam()
    {
        GetDocumentRequest request = new(JsonString.ParseValue("42"u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void GetDocument_Validate_Basic_ThrowsOnInvalidParam()
    {
        GetDocumentRequest request = new(JsonString.ParseValue("42"u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // ── UpdateOrder ──
    [TestMethod]
    public void UpdateOrder_Validate_Detailed_ThrowsOnInvalidParam()
    {
        UpdateOrderRequest request = new(
            JsonUuid.ParseValue("\"not-a-uuid\""u8),
            JsonUuid.ParseValue("\"also-not-uuid\""u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void UpdateOrder_Validate_Basic_ThrowsOnInvalidParam()
    {
        UpdateOrderRequest request = new(
            JsonUuid.ParseValue("\"not-a-uuid\""u8),
            JsonUuid.ParseValue("\"also-not-uuid\""u8));

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // ── QuerySearch ──
    [TestMethod]
    public void QuerySearch_Validate_Detailed_ThrowsOnInvalidParam()
    {
        QuerySearchRequest request = new() { Limit = JsonInt32.ParseValue("\"notnum\""u8) };

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void QuerySearch_Validate_Basic_ThrowsOnInvalidParam()
    {
        QuerySearchRequest request = new() { Limit = JsonInt32.ParseValue("\"notnum\""u8) };

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // ── Complex parameter types (arrays/objects) ──
    // CookieArrayNonexplode - expects array of strings
    [TestMethod]
    public void CookieArrayNonexplode_Validate_Detailed_ThrowsOnInvalidParam()
    {
        var colors = GetComplexCookieArrayNonexplodeColors.ParseValue("42"u8);
        CookieArrayNonexplodeRequest request = new(colors);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void CookieArrayNonexplode_Validate_Basic_ThrowsOnInvalidParam()
    {
        var colors = GetComplexCookieArrayNonexplodeColors.ParseValue("42"u8);
        CookieArrayNonexplodeRequest request = new(colors);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // CookieArray - expects array of strings
    [TestMethod]
    public void CookieArray_Validate_Detailed_ThrowsOnInvalidParam()
    {
        var colors = GetComplexCookieArrayColors.ParseValue("42"u8);
        CookieArrayRequest request = new(colors);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void CookieArray_Validate_Basic_ThrowsOnInvalidParam()
    {
        var colors = GetComplexCookieArrayColors.ParseValue("42"u8);
        CookieArrayRequest request = new(colors);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // CookieObjectNonexplode - expects object
    [TestMethod]
    public void CookieObjectNonexplode_Validate_Detailed_ThrowsOnInvalidParam()
    {
        var prefs = GetComplexCookieObjectNonexplodePrefs.ParseValue("42"u8);
        CookieObjectNonexplodeRequest request = new(prefs);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void CookieObjectNonexplode_Validate_Basic_ThrowsOnInvalidParam()
    {
        var prefs = GetComplexCookieObjectNonexplodePrefs.ParseValue("42"u8);
        CookieObjectNonexplodeRequest request = new(prefs);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // CookieObject - expects object
    [TestMethod]
    public void CookieObject_Validate_Detailed_ThrowsOnInvalidParam()
    {
        var prefs = GetComplexCookieObjectPrefs.ParseValue("42"u8);
        CookieObjectRequest request = new(prefs);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void CookieObject_Validate_Basic_ThrowsOnInvalidParam()
    {
        var prefs = GetComplexCookieObjectPrefs.ParseValue("42"u8);
        CookieObjectRequest request = new(prefs);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // HeaderArray
    [TestMethod]
    public void HeaderArray_Validate_Detailed_ThrowsOnInvalidParam()
    {
        var tags = GetComplexHeaderArrayXTags.ParseValue("42"u8);
        HeaderArrayRequest request = new(tags);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void HeaderArray_Validate_Basic_ThrowsOnInvalidParam()
    {
        var tags = GetComplexHeaderArrayXTags.ParseValue("42"u8);
        HeaderArrayRequest request = new(tags);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // HeaderObject
    [TestMethod]
    public void HeaderObject_Validate_Detailed_ThrowsOnInvalidParam()
    {
        var dims = GetComplexHeaderObjectXDims.ParseValue("42"u8);
        HeaderObjectRequest request = new(dims);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void HeaderObject_Validate_Basic_ThrowsOnInvalidParam()
    {
        var dims = GetComplexHeaderObjectXDims.ParseValue("42"u8);
        HeaderObjectRequest request = new(dims);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // HeaderObjectExplode
    [TestMethod]
    public void HeaderObjectExplode_Validate_Detailed_ThrowsOnInvalidParam()
    {
        var dims = GetComplexHeaderObjectExplodeXDims.ParseValue("42"u8);
        HeaderObjectExplodeRequest request = new(dims);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void HeaderObjectExplode_Validate_Basic_ThrowsOnInvalidParam()
    {
        var dims = GetComplexHeaderObjectExplodeXDims.ParseValue("42"u8);
        HeaderObjectExplodeRequest request = new(dims);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // PathArraySimple
    [TestMethod]
    public void PathArraySimple_Validate_Detailed_ThrowsOnInvalidParam()
    {
        var ids = GetComplexPathArraySimpleByIdsIds.ParseValue("42"u8);
        PathArraySimpleRequest request = new(ids);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void PathArraySimple_Validate_Basic_ThrowsOnInvalidParam()
    {
        var ids = GetComplexPathArraySimpleByIdsIds.ParseValue("42"u8);
        PathArraySimpleRequest request = new(ids);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // PathArrayLabel
    [TestMethod]
    public void PathArrayLabel_Validate_Detailed_ThrowsOnInvalidParam()
    {
        var ids = GetComplexPathArrayLabelByIdsIds.ParseValue("42"u8);
        PathArrayLabelRequest request = new(ids);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void PathArrayLabel_Validate_Basic_ThrowsOnInvalidParam()
    {
        var ids = GetComplexPathArrayLabelByIdsIds.ParseValue("42"u8);
        PathArrayLabelRequest request = new(ids);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // PathArrayMatrix
    [TestMethod]
    public void PathArrayMatrix_Validate_Detailed_ThrowsOnInvalidParam()
    {
        var ids = GetComplexPathArrayMatrixByIdsIds.ParseValue("42"u8);
        PathArrayMatrixRequest request = new(ids);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void PathArrayMatrix_Validate_Basic_ThrowsOnInvalidParam()
    {
        var ids = GetComplexPathArrayMatrixByIdsIds.ParseValue("42"u8);
        PathArrayMatrixRequest request = new(ids);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // PathObjectSimple
    [TestMethod]
    public void PathObjectSimple_Validate_Detailed_ThrowsOnInvalidParam()
    {
        var dims = GetComplexPathObjectSimpleByDimsDims.ParseValue("42"u8);
        PathObjectSimpleRequest request = new(dims);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void PathObjectSimple_Validate_Basic_ThrowsOnInvalidParam()
    {
        var dims = GetComplexPathObjectSimpleByDimsDims.ParseValue("42"u8);
        PathObjectSimpleRequest request = new(dims);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // PathObjectSimpleExplode
    [TestMethod]
    public void PathObjectSimpleExplode_Validate_Detailed_ThrowsOnInvalidParam()
    {
        var dims = GetComplexPathObjectSimpleExplodeByDimsDims.ParseValue("42"u8);
        PathObjectSimpleExplodeRequest request = new(dims);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void PathObjectSimpleExplode_Validate_Basic_ThrowsOnInvalidParam()
    {
        var dims = GetComplexPathObjectSimpleExplodeByDimsDims.ParseValue("42"u8);
        PathObjectSimpleExplodeRequest request = new(dims);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // PathObjectLabel
    [TestMethod]
    public void PathObjectLabel_Validate_Detailed_ThrowsOnInvalidParam()
    {
        var dims = GetComplexPathObjectLabelByDimsDims.ParseValue("42"u8);
        PathObjectLabelRequest request = new(dims);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void PathObjectLabel_Validate_Basic_ThrowsOnInvalidParam()
    {
        var dims = GetComplexPathObjectLabelByDimsDims.ParseValue("42"u8);
        PathObjectLabelRequest request = new(dims);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // PathObjectLabelExplode
    [TestMethod]
    public void PathObjectLabelExplode_Validate_Detailed_ThrowsOnInvalidParam()
    {
        var dims = GetComplexPathObjectLabelExplodeByDimsDims.ParseValue("42"u8);
        PathObjectLabelExplodeRequest request = new(dims);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void PathObjectLabelExplode_Validate_Basic_ThrowsOnInvalidParam()
    {
        var dims = GetComplexPathObjectLabelExplodeByDimsDims.ParseValue("42"u8);
        PathObjectLabelExplodeRequest request = new(dims);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // PathObjectMatrix
    [TestMethod]
    public void PathObjectMatrix_Validate_Detailed_ThrowsOnInvalidParam()
    {
        var dims = GetComplexPathObjectMatrixByDimsDims.ParseValue("42"u8);
        PathObjectMatrixRequest request = new(dims);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void PathObjectMatrix_Validate_Basic_ThrowsOnInvalidParam()
    {
        var dims = GetComplexPathObjectMatrixByDimsDims.ParseValue("42"u8);
        PathObjectMatrixRequest request = new(dims);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // PathObjectMatrixExplode
    [TestMethod]
    public void PathObjectMatrixExplode_Validate_Detailed_ThrowsOnInvalidParam()
    {
        var dims = GetComplexPathObjectMatrixExplodeByDimsDims.ParseValue("42"u8);
        PathObjectMatrixExplodeRequest request = new(dims);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void PathObjectMatrixExplode_Validate_Basic_ThrowsOnInvalidParam()
    {
        var dims = GetComplexPathObjectMatrixExplodeByDimsDims.ParseValue("42"u8);
        PathObjectMatrixExplodeRequest request = new(dims);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // QueryArrayExplode
    [TestMethod]
    public void QueryArrayExplode_Validate_Detailed_ThrowsOnInvalidParam()
    {
        var colors = GetComplexQueryArrayExplodeColors.ParseValue("42"u8);
        QueryArrayExplodeRequest request = new(colors);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void QueryArrayExplode_Validate_Basic_ThrowsOnInvalidParam()
    {
        var colors = GetComplexQueryArrayExplodeColors.ParseValue("42"u8);
        QueryArrayExplodeRequest request = new(colors);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // QueryArrayNonexplode
    [TestMethod]
    public void QueryArrayNonexplode_Validate_Detailed_ThrowsOnInvalidParam()
    {
        var colors = GetComplexQueryArrayNonexplodeColors.ParseValue("42"u8);
        QueryArrayNonexplodeRequest request = new(colors);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void QueryArrayNonexplode_Validate_Basic_ThrowsOnInvalidParam()
    {
        var colors = GetComplexQueryArrayNonexplodeColors.ParseValue("42"u8);
        QueryArrayNonexplodeRequest request = new(colors);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // QueryArrayPipe
    [TestMethod]
    public void QueryArrayPipe_Validate_Detailed_ThrowsOnInvalidParam()
    {
        var colors = GetComplexQueryArrayPipeColors.ParseValue("42"u8);
        QueryArrayPipeRequest request = new(colors);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void QueryArrayPipe_Validate_Basic_ThrowsOnInvalidParam()
    {
        var colors = GetComplexQueryArrayPipeColors.ParseValue("42"u8);
        QueryArrayPipeRequest request = new(colors);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // QueryArraySpace
    [TestMethod]
    public void QueryArraySpace_Validate_Detailed_ThrowsOnInvalidParam()
    {
        var colors = GetComplexQueryArraySpaceColors.ParseValue("42"u8);
        QueryArraySpaceRequest request = new(colors);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void QueryArraySpace_Validate_Basic_ThrowsOnInvalidParam()
    {
        var colors = GetComplexQueryArraySpaceColors.ParseValue("42"u8);
        QueryArraySpaceRequest request = new(colors);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // QueryObjectExplode
    [TestMethod]
    public void QueryObjectExplode_Validate_Detailed_ThrowsOnInvalidParam()
    {
        var dims = GetComplexQueryObjectExplodeDims.ParseValue("42"u8);
        QueryObjectExplodeRequest request = new(dims);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void QueryObjectExplode_Validate_Basic_ThrowsOnInvalidParam()
    {
        var dims = GetComplexQueryObjectExplodeDims.ParseValue("42"u8);
        QueryObjectExplodeRequest request = new(dims);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // QueryObjectNonexplode
    [TestMethod]
    public void QueryObjectNonexplode_Validate_Detailed_ThrowsOnInvalidParam()
    {
        var dims = GetComplexQueryObjectNonexplodeDims.ParseValue("42"u8);
        QueryObjectNonexplodeRequest request = new(dims);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void QueryObjectNonexplode_Validate_Basic_ThrowsOnInvalidParam()
    {
        var dims = GetComplexQueryObjectNonexplodeDims.ParseValue("42"u8);
        QueryObjectNonexplodeRequest request = new(dims);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // QueryObjectDeep
    [TestMethod]
    public void QueryObjectDeep_Validate_Detailed_ThrowsOnInvalidParam()
    {
        var dims = GetComplexQueryObjectDeepDims.ParseValue("42"u8);
        QueryObjectDeepRequest request = new(dims);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void QueryObjectDeep_Validate_Basic_ThrowsOnInvalidParam()
    {
        var dims = GetComplexQueryObjectDeepDims.ParseValue("42"u8);
        QueryObjectDeepRequest request = new(dims);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }

    // SearchWithQuerystring - uses a special query object
    [TestMethod]
    public void SearchWithQuerystring_Validate_Detailed_ThrowsOnInvalidParam()
    {
        var qs = GetSearchWithQuerystringQs.ParseValue("42"u8);
        SearchWithQuerystringRequest request = new(qs);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Detailed));
    }

    [TestMethod]
    public void SearchWithQuerystring_Validate_Basic_ThrowsOnInvalidParam()
    {
        var qs = GetSearchWithQuerystringQs.ParseValue("42"u8);
        SearchWithQuerystringRequest request = new(qs);

        Assert.ThrowsExactly<ArgumentException>(() => request.Validate(ValidationMode.Basic));
    }
}