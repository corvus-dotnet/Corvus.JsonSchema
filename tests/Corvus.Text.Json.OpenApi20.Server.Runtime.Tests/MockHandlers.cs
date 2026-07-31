// <copyright file="MockHandlers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using CanonTests20.Server;
using CanonTests20.Server.Models;
using Corvus.Text.Json;

namespace Corvus.Text.Json.OpenApi20.Server.Runtime.Tests;

internal sealed class MockWidgetsHandler : IApiWidgetsHandler
{
    public static string? CapturedTsvTagsJson { get; set; }

    public static int CapturedTsvTagsCount { get; set; }

    public ValueTask<ListWidgetsResult> HandleListWidgetsAsync(ListWidgetsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (parameters.TsvTags.IsNotUndefined())
        {
            CapturedTsvTagsJson = parameters.TsvTags.ToString();
            int count = 0;
            foreach (var item in parameters.TsvTags.EnumerateArray())
            {
                count++;
            }

            CapturedTsvTagsCount = count;
        }

        return new(ListWidgetsResult.Ok(GetWidgetsOk.ParseValue("[]"u8), workspace));
    }

    public ValueTask<CreateWidgetResult> HandleCreateWidgetAsync(CreateWidgetParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(CreateWidgetResult.Created(parameters.Body, workspace));

    public ValueTask<RenderWidgetResult> HandleRenderWidgetAsync(RenderWidgetParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => new(RenderWidgetResult.Ok());
}

internal sealed class MockUploadsHandler : IApiUploadsHandler
{
    public static string? CapturedNotes { get; set; }

    public static string? CapturedBodyJson { get; set; }

    public ValueTask<UploadBundleResult> HandleUploadBundleAsync(UploadBundleParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        CapturedBodyJson = parameters.Body.IsNotUndefined() ? parameters.Body.ToString() : "(undefined)";

        if (parameters.Body.IsNotUndefined()
            && ((JsonElement)parameters.Body).TryGetProperty("notes"u8, out JsonElement notesEl))
        {
            CapturedNotes = notesEl.GetString();
        }

        return new(UploadBundleResult.NoContent());
    }
}

internal sealed class MockDefaultHandler : IApiDefaultHandler
{
    public static int CapturedFlagsCount { get; set; }

    public ValueTask<PostLegacyResult> HandlePostLegacyAsync(PostLegacyParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (parameters.Body.IsNotUndefined()
            && ((JsonElement)parameters.Body).TryGetProperty("flags"u8, out JsonElement flagsEl)
            && flagsEl.ValueKind == JsonValueKind.Array)
        {
            int count = 0;
            foreach (JsonElement item in flagsEl.EnumerateArray())
            {
                count++;
            }

            CapturedFlagsCount = count;
        }

        return new(PostLegacyResult.Ok(WeirdKey0x.ParseValue("\"alpha\""u8), workspace));
    }
}