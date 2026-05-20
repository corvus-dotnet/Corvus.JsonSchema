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

    public ValueTask<ListItemsResult> HandleListItemsAsync(ListItemsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        GetItemsOk body = GetItemsOk.ParseValue("""[{"id":1,"name":"Test Item"}]"""u8);
        return new(ListItemsResult.Ok(body, workspace));
    }

    public ValueTask<CreateItemResult> HandleCreateItemAsync(CreateItemParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        PostItemsCreated body = PostItemsCreated.ParseValue("""{"id":"new-1","name":"Created"}"""u8);
        return new(CreateItemResult.Created(body, workspace));
    }

    public ValueTask<OptionsItemsResult> HandleOptionsItemsAsync(OptionsItemsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(OptionsItemsResult.NoContent());

    public ValueTask<PurgeItemsResult> HandlePurgeItemsAsync(PurgeItemsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(PurgeItemsResult.NoContent());

    public ValueTask<GetItemResult> HandleGetItemAsync(GetItemParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string itemId = (string)parameters.ItemId;
        ItemEntity body = ItemEntity.ParseValue(System.Text.Encoding.UTF8.GetBytes($$"""{"id":42,"name":"Widget-{{itemId}}"}"""));
        return new(GetItemResult.Ok(body, workspace));
    }

    public ValueTask<PatchItemsItemIdResult> HandlePatchItemsItemIdAsync(PatchItemsItemIdParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(PatchItemsItemIdResult.Ok(DefaultItem, workspace));

    public ValueTask<UpdateItemFormResult> HandleUpdateItemFormAsync(UpdateItemFormParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        // Echo the parsed form body back as JSON to verify deserialization.
        string json = parameters.Body.ToString();
        JsonElement echoBody = JsonElement.ParseValue(System.Text.Encoding.UTF8.GetBytes(json));
        return new(UpdateItemFormResult.Ok(echoBody, workspace));
    }

    public ValueTask<UploadItemDataResult> HandleUploadItemDataAsync(UploadItemDataParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(UploadItemDataResult.Created(DefaultItem, workspace));

    public ValueTask<DownloadFileResult> HandleDownloadFileAsync(DownloadFileParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(DownloadFileResult.Ok());

    public ValueTask<GetQuirkyResult> HandleGetQuirkyAsync(GetQuirkyParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(GetQuirkyResult.Ok(DefaultItem, workspace));

    public ValueTask<GetStyledQuirkyResult> HandleGetStyledQuirkyAsync(GetStyledQuirkyParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(GetStyledQuirkyResult.Ok(DefaultItem, workspace));

    public ValueTask<ExportDataResult> HandleExportDataAsync(ExportDataParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(ExportDataResult.Ok());

    public ValueTask<GetEmptyServersResult> HandleGetEmptyServersAsync(GetEmptyServersParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        GetEmptyServersOk body = GetEmptyServersOk.ParseValue("""{"ok":true}"""u8);
        return new(GetEmptyServersResult.Ok(body, workspace));
    }

    public ValueTask<HeadHealthResult> HandleHeadHealthAsync(HeadHealthParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(HeadHealthResult.Ok());

    public ValueTask<TraceHealthResult> HandleTraceHealthAsync(TraceHealthParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(TraceHealthResult.Ok());

    public ValueTask<GetAdvancedStylesResult> HandleGetAdvancedStylesAsync(GetAdvancedStylesParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        GetAdvancedStylesByIdsOk body = GetAdvancedStylesByIdsOk.ParseValue("""{"items":[],"total":0}"""u8);
        return new(GetAdvancedStylesResult.Ok(body, workspace));
    }

    public ValueTask<GetByMatrixCodesResult> HandleGetByMatrixCodesAsync(GetByMatrixCodesParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        GetMatrixTestByCodesOk body = GetMatrixTestByCodesOk.ParseValue("""{"count":0}"""u8);
        return new(GetByMatrixCodesResult.Ok(body, workspace));
    }

    public ValueTask<GetByMatrixTagsResult> HandleGetByMatrixTagsAsync(GetByMatrixTagsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        GetMatrixNoExplodeByTagsOk body = GetMatrixNoExplodeByTagsOk.ParseValue("""{"count":0}"""u8);
        return new(GetByMatrixTagsResult.Ok(body, workspace));
    }

    public ValueTask<GetByLabelItemsResult> HandleGetByLabelItemsAsync(GetByLabelItemsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        GetLabelNoExplodeByItemsOk body = GetLabelNoExplodeByItemsOk.ParseValue("""{"count":0}"""u8);
        return new(GetByLabelItemsResult.Ok(body, workspace));
    }

    public ValueTask<GetByStyledObjectResult> HandleGetByStyledObjectAsync(GetByStyledObjectParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        GetStyledObjectByObjOk body = GetStyledObjectByObjOk.ParseValue("""{}"""u8);
        return new(GetByStyledObjectResult.Ok(body, workspace));
    }

    public ValueTask<QueryItemsResult> HandleQueryItemsAsync(QueryItemsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        Schema1 body = Schema1.ParseValue("""[]"""u8);
        return new(QueryItemsResult.Ok(body, workspace));
    }

    public ValueTask<GetResourceResult> HandleGetResourceAsync(GetResourceParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(GetResourceResult.Ok(DefaultItem, workspace));

    public ValueTask<CopyResourceResult> HandleCopyResourceAsync(CopyResourceParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(CopyResourceResult.Created(DefaultItem, workspace));

    public ValueTask<PurgeResourceResult> HandlePurgeResourceAsync(PurgeResourceParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(PurgeResourceResult.NoContent());

    public ValueTask<MoveResourceResult> HandleMoveResourceAsync(MoveResourceParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(MoveResourceResult.Ok());

    public ValueTask<StreamEventsResult> HandleStreamEventsAsync(StreamEventsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(StreamEventsResult.Ok());

    public ValueTask<SearchWithQuerystringResult> HandleSearchWithQuerystringAsync(SearchWithQuerystringParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        GetSearchQsOk body = GetSearchQsOk.ParseValue("""{"results":[]}"""u8);
        return new(SearchWithQuerystringResult.Ok(body, workspace));
    }

    public ValueTask<GetDocumentResult> HandleGetDocumentAsync(GetDocumentParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        Schema5 body = Schema5.ParseValue("""{"id":"550e8400-e29b-41d4-a716-446655440000","title":"Test Document","version":1}"""u8);
        return new(GetDocumentResult.Ok(body, workspace));
    }

    public ValueTask<UpdateDocumentResult> HandleUpdateDocumentAsync(UpdateDocumentParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        Schema5 body = Schema5.ParseValue("""{"id":"550e8400-e29b-41d4-a716-446655440000","title":"Updated Document","version":2}"""u8);
        return new(UpdateDocumentResult.Ok(body, workspace));
    }

    public ValueTask<UploadRawFileResult> HandleUploadRawFileAsync(UploadRawFileParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        PostUploadRawCreated body = PostUploadRawCreated.ParseValue("""{"url":"https://example.com/file.bin"}"""u8);
        return new(UploadRawFileResult.Created(body, workspace));
    }

    public ValueTask<GetResourceVersionResult> HandleGetResourceVersionAsync(GetResourceVersionParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        Schema7 body = Schema7.ParseValue("""{"version":"1.0"}"""u8);
        return new(GetResourceVersionResult.Ok(body, workspace));
    }

    public ValueTask<GetMonitoringStatusResult> HandleGetMonitoringStatusAsync(GetMonitoringStatusParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        GetMonitoringStatusOk body = GetMonitoringStatusOk.ParseValue("""{"status":"ok"}"""u8);
        return new(GetMonitoringStatusResult.Ok(body, workspace));
    }

    public ValueTask<PutMonitoringStatusResult> HandlePutMonitoringStatusAsync(PutMonitoringStatusParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        PutMonitoringStatusOk body = PutMonitoringStatusOk.ParseValue("""{"status":"updated"}"""u8);
        return new(PutMonitoringStatusResult.Ok(body, workspace));
    }

    public ValueTask<PostMonitoringStatusResult> HandlePostMonitoringStatusAsync(PostMonitoringStatusParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        PostMonitoringStatusCreated body = PostMonitoringStatusCreated.ParseValue("""{"status":"created"}"""u8);
        return new(PostMonitoringStatusResult.Created(body, workspace));
    }

    public ValueTask<DeleteMonitoringStatusResult> HandleDeleteMonitoringStatusAsync(DeleteMonitoringStatusParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(DeleteMonitoringStatusResult.NoContent());

    public ValueTask<QueryMonitoringStatusResult> HandleQueryMonitoringStatusAsync(QueryMonitoringStatusParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        Schema9 body = Schema9.ParseValue("""[]"""u8);
        return new(QueryMonitoringStatusResult.Ok(body, workspace));
    }

    public ValueTask<BatchResourceResult> HandleBatchResourceAsync(BatchResourceParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        Schema4 body = Schema4.ParseValue("""{"processed":1}"""u8);
        return new(BatchResourceResult.Ok(body, workspace));
    }
}

/// <summary>
/// Mock implementation of <see cref="IApiItemsHandler"/> for testing.
/// </summary>
internal sealed class MockItemsHandler : IApiItemsHandler
{
    private static readonly ItemEntity DefaultItem = ItemEntity.ParseValue("""{"id":1,"name":"Widget"}"""u8);

    public ValueTask<SearchItemsResult> HandleSearchItemsAsync(SearchItemsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        GetSearchOk body = GetSearchOk.ParseValue("""[{"id":1,"name":"Found Item"}]"""u8);
        return new(SearchItemsResult.Ok(body, workspace));
    }

    public ValueTask<UploadFileResult> HandleUploadFileAsync(UploadFileParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(UploadFileResult.Created(DefaultItem, workspace));

    public ValueTask<SubmitFeedbackResult> HandleSubmitFeedbackAsync(SubmitFeedbackParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        // Echo the parsed form body back as JSON to verify deserialization.
        string json = parameters.Body.ToString();
        JsonElement echoBody = JsonElement.ParseValue(System.Text.Encoding.UTF8.GetBytes(json));
        return new(SubmitFeedbackResult.Created(echoBody, workspace));
    }

    public ValueTask<UploadAttachmentResult> HandleUploadAttachmentAsync(UploadAttachmentParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(UploadAttachmentResult.Created(DefaultItem, workspace));

    public ValueTask<SubmitFeedbackEncodedResult> HandleSubmitFeedbackEncodedAsync(SubmitFeedbackEncodedParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        // Echo the parsed form body back as JSON to verify deserialization.
        string json = parameters.Body.ToString();
        JsonElement echoBody = JsonElement.ParseValue(System.Text.Encoding.UTF8.GetBytes(json));
        return new(SubmitFeedbackEncodedResult.Created(echoBody, workspace));
    }

    public ValueTask<UploadAttachmentEncodedResult> HandleUploadAttachmentEncodedAsync(UploadAttachmentEncodedParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(UploadAttachmentEncodedResult.Created(DefaultItem, workspace));
}

/// <summary>
/// Mock implementation of <see cref="IApiSearchHandler"/> for testing.
/// </summary>
internal sealed class MockSearchHandler : IApiSearchHandler
{
    public ValueTask<SearchItemsResult> HandleSearchItemsAsync(SearchItemsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        GetSearchOk body = GetSearchOk.ParseValue("""[{"id":1,"name":"Search Result"}]"""u8);
        return new(SearchItemsResult.Ok(body, workspace));
    }

    public ValueTask<SearchItemsAdvancedResult> HandleSearchItemsAdvancedAsync(SearchItemsAdvancedParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        PostSearchOk body = PostSearchOk.ParseValue("""{"results":[{"id":1,"name":"Advanced Result"}]}"""u8);
        return new(SearchItemsAdvancedResult.Ok(body, workspace));
    }
}