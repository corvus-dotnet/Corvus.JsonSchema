// <copyright file="MockHandlers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using CanonTests32.Server;
using Corvus.Text.Json;

namespace Corvus.Text.Json.OpenApi32.Server.Runtime.Tests;

/// <summary>
/// Mock implementation of <see cref="IApiDefaultHandler"/> for testing.
/// Returns canned responses for each operation.
/// </summary>
internal sealed class MockDefaultHandler : IApiDefaultHandler
{
    private static readonly ItemEntity DefaultItem = ItemEntity.ParseValue("""{"id":1,"name":"Widget"}"""u8);
    private static readonly JsonString OptionsAllowMethodsHeader = JsonString.ParseValue("\"GET,POST,OPTIONS,PURGE\""u8);
    private static readonly JsonInt32 RateLimitHeader = JsonInt32.ParseValue("100"u8);
    private static readonly JsonBoolean ActiveHeader = JsonBoolean.ParseValue("true"u8);
    private static readonly JsonElement RequestIdHeader = JsonElement.ParseValue(System.Text.Encoding.UTF8.GetBytes("\"req-123\""));
    private static readonly GetItemsByItemIdOkXTags TagsHeader = GetItemsByItemIdOkXTags.ParseValue("""["alpha","beta"]"""u8);
    private static readonly GetItemsByItemIdOkXPageSizes PageSizesHeader = GetItemsByItemIdOkXPageSizes.ParseValue("""[10,25]"""u8);
    private static readonly GetItemsByItemIdOkXFlags FlagsHeader = GetItemsByItemIdOkXFlags.ParseValue("""[true,false]"""u8);
    private static readonly JsonString AdvancedRequestIdHeader = JsonString.ParseValue("\"adv-123\""u8);
    private static readonly GetAdvancedStylesByIdsOkXCounts AdvancedCountsHeader = GetAdvancedStylesByIdsOkXCounts.ParseValue("""[1,2]"""u8);
    private static readonly GetAdvancedStylesByIdsOkXScores AdvancedScoresHeader = GetAdvancedStylesByIdsOkXScores.ParseValue("""[99.5,42.25]"""u8);
    private static readonly GetAdvancedStylesByIdsOkXWeights AdvancedWeightsHeader = GetAdvancedStylesByIdsOkXWeights.ParseValue("""[1.25,2.5]"""u8);
    private static readonly JsonInt32 GetDocumentVersionHeader = JsonInt32.ParseValue("1"u8);
    private static readonly JsonInt32 UpdateDocumentVersionHeader = JsonInt32.ParseValue("2"u8);
    private static readonly JsonUri CopyLocationHeader = JsonUri.ParseValue("\"https://example.com/resources/res-2\""u8);

    internal static bool ReturnInvalidResponse { get; set; }

    public ValueTask<ListItemsResult> HandleListItemsAsync(ListItemsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        GetItemsOk body = ReturnInvalidResponse
            ? GetItemsOk.ParseValue("""[{"missing":"fields"}]"""u8)
            : GetItemsOk.ParseValue("""[{"id":1,"name":"Test Item"}]"""u8);
        return new(ListItemsResult.Ok(body, workspace));
    }

    public ValueTask<CreateItemResult> HandleCreateItemAsync(CreateItemParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        PostItemsCreated body = ReturnInvalidResponse
            ? PostItemsCreated.ParseValue("""{}"""u8)
            : PostItemsCreated.ParseValue("""{"id":"new-1","name":"Created"}"""u8);
        return new(CreateItemResult.Created(body, workspace));
    }

    public ValueTask<OptionsItemsResult> HandleOptionsItemsAsync(OptionsItemsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(OptionsItemsResult.NoContent(OptionsAllowMethodsHeader));

    public ValueTask<PurgeItemsResult> HandlePurgeItemsAsync(PurgeItemsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(PurgeItemsResult.NoContent());

    public ValueTask<GetItemResult> HandleGetItemAsync(GetItemParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        ItemEntity body = ReturnInvalidResponse
            ? ItemEntity.ParseValue("""{"broken":true}"""u8)
            : ItemEntity.ParseValue(System.Text.Encoding.UTF8.GetBytes($$"""{"id":42,"name":"Widget-{{(string)parameters.ItemId}}"}"""));

        return new(GetItemResult.Ok(body, workspace, RateLimitHeader, ActiveHeader, RequestIdHeader, TagsHeader, PageSizesHeader, FlagsHeader));
    }

    public ValueTask<PatchItemsItemIdResult> HandlePatchItemsItemIdAsync(PatchItemsItemIdParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(PatchItemsItemIdResult.Ok(ReturnInvalidResponse ? ItemEntity.ParseValue("""{}"""u8) : DefaultItem, workspace));

    public ValueTask<UpdateItemFormResult> HandleUpdateItemFormAsync(UpdateItemFormParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        // Echo the parsed form body back as JSON to verify deserialization.
        string json = parameters.Body.ToString();
        JsonElement echoBody = JsonElement.ParseValue(System.Text.Encoding.UTF8.GetBytes(json));
        return new(UpdateItemFormResult.Ok(echoBody, workspace));
    }

    public ValueTask<UploadItemDataResult> HandleUploadItemDataAsync(UploadItemDataParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(UploadItemDataResult.Created(ReturnInvalidResponse ? ItemEntity.ParseValue("""{}"""u8) : DefaultItem, workspace));

    public ValueTask<DownloadFileResult> HandleDownloadFileAsync(DownloadFileParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(DownloadFileResult.Ok());

    public ValueTask<GetQuirkyResult> HandleGetQuirkyAsync(GetQuirkyParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(GetQuirkyResult.Ok(ReturnInvalidResponse ? ItemEntity.ParseValue("""{}"""u8) : DefaultItem, workspace));

    public ValueTask<GetStyledQuirkyResult> HandleGetStyledQuirkyAsync(GetStyledQuirkyParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(GetStyledQuirkyResult.Ok(ReturnInvalidResponse ? ItemEntity.ParseValue("""{}"""u8) : DefaultItem, workspace));

    public ValueTask<ExportDataResult> HandleExportDataAsync(ExportDataParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(ExportDataResult.Ok());

    public ValueTask<GetEmptyServersResult> HandleGetEmptyServersAsync(GetEmptyServersParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        GetEmptyServersOk body = ReturnInvalidResponse
            ? GetEmptyServersOk.ParseValue("""[]"""u8)
            : GetEmptyServersOk.ParseValue("""{"ok":true}"""u8);
        return new(GetEmptyServersResult.Ok(body, workspace));
    }

    public ValueTask<HeadHealthResult> HandleHeadHealthAsync(HeadHealthParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(HeadHealthResult.Ok());

    public ValueTask<TraceHealthResult> HandleTraceHealthAsync(TraceHealthParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(TraceHealthResult.Ok());

    public ValueTask<GetAdvancedStylesResult> HandleGetAdvancedStylesAsync(GetAdvancedStylesParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        GetAdvancedStylesByIdsOk body = ReturnInvalidResponse
            ? GetAdvancedStylesByIdsOk.ParseValue("""[]"""u8)
            : GetAdvancedStylesByIdsOk.ParseValue("""{"items":[],"total":0}"""u8);
        return new(GetAdvancedStylesResult.Ok(body, workspace, AdvancedRequestIdHeader, AdvancedCountsHeader, AdvancedScoresHeader, AdvancedWeightsHeader));
    }

    public ValueTask<GetByMatrixCodesResult> HandleGetByMatrixCodesAsync(GetByMatrixCodesParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        GetMatrixTestByCodesOk body = ReturnInvalidResponse
            ? GetMatrixTestByCodesOk.ParseValue("""[]"""u8)
            : GetMatrixTestByCodesOk.ParseValue("""{"count":0}"""u8);
        return new(GetByMatrixCodesResult.Ok(body, workspace));
    }

    public ValueTask<GetByMatrixTagsResult> HandleGetByMatrixTagsAsync(GetByMatrixTagsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        GetMatrixNoExplodeByTagsOk body = ReturnInvalidResponse
            ? GetMatrixNoExplodeByTagsOk.ParseValue("""[]"""u8)
            : GetMatrixNoExplodeByTagsOk.ParseValue("""{"count":0}"""u8);
        return new(GetByMatrixTagsResult.Ok(body, workspace));
    }

    public ValueTask<GetByLabelItemsResult> HandleGetByLabelItemsAsync(GetByLabelItemsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        GetLabelNoExplodeByItemsOk body = ReturnInvalidResponse
            ? GetLabelNoExplodeByItemsOk.ParseValue("""[]"""u8)
            : GetLabelNoExplodeByItemsOk.ParseValue("""{"count":0}"""u8);
        return new(GetByLabelItemsResult.Ok(body, workspace));
    }

    public ValueTask<GetByStyledObjectResult> HandleGetByStyledObjectAsync(GetByStyledObjectParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        GetStyledObjectByObjOk body = ReturnInvalidResponse
            ? GetStyledObjectByObjOk.ParseValue("""[]"""u8)
            : GetStyledObjectByObjOk.ParseValue("""{}"""u8);
        return new(GetByStyledObjectResult.Ok(body, workspace));
    }

    public ValueTask<QueryItemsResult> HandleQueryItemsAsync(QueryItemsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        Schema1 body = ReturnInvalidResponse
            ? Schema1.ParseValue("""{}"""u8)
            : Schema1.ParseValue("""[]"""u8);
        return new(QueryItemsResult.Ok(body, workspace));
    }

    public ValueTask<GetResourceResult> HandleGetResourceAsync(GetResourceParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(GetResourceResult.Ok(ReturnInvalidResponse ? ItemEntity.ParseValue("""{}"""u8) : DefaultItem, workspace));

    public ValueTask<CopyResourceResult> HandleCopyResourceAsync(CopyResourceParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(CopyResourceResult.Created(ReturnInvalidResponse ? ItemEntity.ParseValue("""{}"""u8) : DefaultItem, workspace, CopyLocationHeader));

    public ValueTask<PurgeResourceResult> HandlePurgeResourceAsync(PurgeResourceParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(PurgeResourceResult.NoContent());

    public ValueTask<MoveResourceResult> HandleMoveResourceAsync(MoveResourceParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(MoveResourceResult.Ok());

    public ValueTask<StreamEventsResult> HandleStreamEventsAsync(StreamEventsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(StreamEventsResult.Ok());

    public ValueTask<SearchWithQuerystringResult> HandleSearchWithQuerystringAsync(SearchWithQuerystringParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        GetSearchQsOk body = ReturnInvalidResponse
            ? GetSearchQsOk.ParseValue("""[]"""u8)
            : GetSearchQsOk.ParseValue("""{"results":[]}"""u8);
        return new(SearchWithQuerystringResult.Ok(body, workspace));
    }

    public ValueTask<GetDocumentResult> HandleGetDocumentAsync(GetDocumentParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        Schema5 body = ReturnInvalidResponse
            ? Schema5.ParseValue("""{}"""u8)
            : Schema5.ParseValue("""{"id":"550e8400-e29b-41d4-a716-446655440000","title":"Test Document","version":1}"""u8);
        return new(GetDocumentResult.Ok(body, workspace, GetDocumentVersionHeader));
    }

    public ValueTask<UpdateDocumentResult> HandleUpdateDocumentAsync(UpdateDocumentParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        Schema5 body = ReturnInvalidResponse
            ? Schema5.ParseValue("""{}"""u8)
            : Schema5.ParseValue("""{"id":"550e8400-e29b-41d4-a716-446655440000","title":"Updated Document","version":2}"""u8);
        return new(UpdateDocumentResult.Ok(body, workspace, UpdateDocumentVersionHeader));
    }

    public ValueTask<UploadRawFileResult> HandleUploadRawFileAsync(UploadRawFileParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        PostUploadRawCreated body = ReturnInvalidResponse
            ? PostUploadRawCreated.ParseValue("""[]"""u8)
            : PostUploadRawCreated.ParseValue("""{"url":"https://example.com/file.bin"}"""u8);
        return new(UploadRawFileResult.Created(body, workspace));
    }

    public ValueTask<GetResourceVersionResult> HandleGetResourceVersionAsync(GetResourceVersionParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        Schema7 body = ReturnInvalidResponse
            ? Schema7.ParseValue("""[]"""u8)
            : Schema7.ParseValue("""{"version":1}"""u8);
        return new(GetResourceVersionResult.Ok(body, workspace));
    }

    public ValueTask<GetMonitoringStatusResult> HandleGetMonitoringStatusAsync(GetMonitoringStatusParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        GetMonitoringStatusOk body = ReturnInvalidResponse
            ? GetMonitoringStatusOk.ParseValue("""[]"""u8)
            : GetMonitoringStatusOk.ParseValue("""{"status":"ok"}"""u8);
        return new(GetMonitoringStatusResult.Ok(body, workspace));
    }

    public ValueTask<PutMonitoringStatusResult> HandlePutMonitoringStatusAsync(PutMonitoringStatusParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        PutMonitoringStatusOk body = ReturnInvalidResponse
            ? PutMonitoringStatusOk.ParseValue("""[]"""u8)
            : PutMonitoringStatusOk.ParseValue("""{"status":"updated"}"""u8);
        return new(PutMonitoringStatusResult.Ok(body, workspace));
    }

    public ValueTask<PostMonitoringStatusResult> HandlePostMonitoringStatusAsync(PostMonitoringStatusParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        PostMonitoringStatusCreated body = ReturnInvalidResponse
            ? PostMonitoringStatusCreated.ParseValue("""[]"""u8)
            : PostMonitoringStatusCreated.ParseValue("""{"status":"created"}"""u8);
        return new(PostMonitoringStatusResult.Created(body, workspace));
    }

    public ValueTask<DeleteMonitoringStatusResult> HandleDeleteMonitoringStatusAsync(DeleteMonitoringStatusParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(DeleteMonitoringStatusResult.NoContent());

    public ValueTask<QueryMonitoringStatusResult> HandleQueryMonitoringStatusAsync(QueryMonitoringStatusParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        Schema9 body = ReturnInvalidResponse
            ? Schema9.ParseValue("""[]"""u8)
            : Schema9.ParseValue("""{}"""u8);
        return new(QueryMonitoringStatusResult.Ok(body, workspace));
    }

    public ValueTask<BatchResourceResult> HandleBatchResourceAsync(BatchResourceParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        Schema4 body = ReturnInvalidResponse
            ? Schema4.ParseValue("""[]"""u8)
            : Schema4.ParseValue("""{"processed":1}"""u8);
        return new(BatchResourceResult.Ok(body, workspace));
    }
}

/// <summary>
/// Mock implementation of <see cref="IApiItemsHandler"/> for testing.
/// </summary>
internal sealed class MockItemsHandler : IApiItemsHandler
{
    private static readonly ItemEntity DefaultItem = ItemEntity.ParseValue("""{"id":1,"name":"Widget"}"""u8);
    private static readonly GetSearchOkXFacets SearchFacetsHeader = GetSearchOkXFacets.ParseValue("""{"status":"active"}"""u8);

    public ValueTask<SearchItemsResult> HandleSearchItemsAsync(SearchItemsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string q = parameters.Q.IsUndefined() ? "undefined" : (string)parameters.Q;
        GetSearchOk body = MockDefaultHandler.ReturnInvalidResponse
            ? GetSearchOk.ParseValue("""{}"""u8)
            : GetSearchOk.ParseValue(System.Text.Encoding.UTF8.GetBytes($$"""[{"id":1,"name":"{{q}}"}]"""));
        return new(SearchItemsResult.Ok(body, workspace, SearchFacetsHeader));
    }

    public ValueTask<UploadFileResult> HandleUploadFileAsync(UploadFileParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(UploadFileResult.Created(MockDefaultHandler.ReturnInvalidResponse ? ItemEntity.ParseValue("""{}"""u8) : DefaultItem, workspace));

    public ValueTask<SubmitFeedbackResult> HandleSubmitFeedbackAsync(SubmitFeedbackParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        // Echo the parsed form body back as JSON to verify deserialization.
        string json = parameters.Body.ToString();
        JsonElement echoBody = JsonElement.ParseValue(System.Text.Encoding.UTF8.GetBytes(json));
        return new(SubmitFeedbackResult.Created(echoBody, workspace));
    }

    public ValueTask<UploadAttachmentResult> HandleUploadAttachmentAsync(UploadAttachmentParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(UploadAttachmentResult.Created(MockDefaultHandler.ReturnInvalidResponse ? ItemEntity.ParseValue("""{}"""u8) : DefaultItem, workspace));

    public ValueTask<SubmitFeedbackEncodedResult> HandleSubmitFeedbackEncodedAsync(SubmitFeedbackEncodedParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        // Echo the parsed form body back as JSON to verify deserialization.
        string json = parameters.Body.ToString();
        JsonElement echoBody = JsonElement.ParseValue(System.Text.Encoding.UTF8.GetBytes(json));
        return new(SubmitFeedbackEncodedResult.Created(echoBody, workspace));
    }

    public ValueTask<UploadAttachmentEncodedResult> HandleUploadAttachmentEncodedAsync(UploadAttachmentEncodedParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(UploadAttachmentEncodedResult.Created(MockDefaultHandler.ReturnInvalidResponse ? ItemEntity.ParseValue("""{}"""u8) : DefaultItem, workspace));
}

/// <summary>
/// Mock implementation of <see cref="IApiSearchHandler"/> for testing.
/// </summary>
internal sealed class MockSearchHandler : IApiSearchHandler
{
    public ValueTask<SearchItemsResult> HandleSearchItemsAsync(SearchItemsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        GetSearchOk body = MockDefaultHandler.ReturnInvalidResponse
            ? GetSearchOk.ParseValue("""[{"oops":true}]"""u8)
            : GetSearchOk.ParseValue("""[{"id":1,"name":"Search Result"}]"""u8);
        return new(SearchItemsResult.Ok(body, workspace));
    }

    public ValueTask<SearchItemsAdvancedResult> HandleSearchItemsAdvancedAsync(SearchItemsAdvancedParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        PostSearchOk body = MockDefaultHandler.ReturnInvalidResponse
            ? PostSearchOk.ParseValue("""[]"""u8)
            : PostSearchOk.ParseValue("""{"results":[{"id":1,"name":"Advanced Result"}]}"""u8);
        return new(SearchItemsAdvancedResult.Ok(body, workspace));
    }
}