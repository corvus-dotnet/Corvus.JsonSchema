// <copyright file="AccessRequestViewProjectionBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Models;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the control-plane access-request warm read path (§16.5): turning a stored <see cref="AccessRequest"/>
/// document into its <c>AccessRequestView</c> response body. The view is an exact, congruent projection of the
/// persisted document (identical property names/types/required set), so the handler wraps the stored element
/// (<c>AccessRequestView.From</c>, a pointer reinterpret) and serializes its backing verbatim — versus the
/// field-by-field rebuild it replaced, which realised a managed <see cref="string"/> for every scalar (id,
/// baseWorkflowId, status, createdBy, decidedBy, …) plus an array for the scopes. The headline result the
/// access-request work must hold: the <c>From()</c> projection allocates <b>zero</b> bytes on the warm path (the
/// reused pooled writer copies the backing), while the materialising projection pays O(fields) per request.
/// </summary>
/// <remarks>
/// The baseline reproduces the dominant cost of the field-by-field projection — realising every leaf accessor — by
/// writing those realised values into the same pooled writer the wrap path uses, so the reported delta is the
/// per-request materialisation the change removed (the generated builder the old handler also ran sits on top of
/// this, so the baseline is a conservative lower bound on the old cost).
/// </remarks>
[MemoryDiagnoser]
public class AccessRequestViewProjectionBenchmarks
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    private ParsedJsonDocument<AccessRequest> document = null!;
    private JsonWorkspace workspace = null!;

    [GlobalSetup]
    public void Setup()
    {
        // A decided (approved) request with every optional field populated — the largest realistic projection.
        using ParsedJsonDocument<AccessRequest> draft = AccessRequest.Draft(
            "orders-export",
            ["runs:write"],
            "sub",
            "alice",
            "Alice Smith",
            "Need to run the nightly export ad hoc.",
            3600);
        byte[] pending = AccessRequestSerialization.SerializeNew("req-7f3c", draft.RootElement, "alice", DateTimeOffset.UnixEpoch, new WorkflowEtag("etag-1"));
        var decision = new AccessRequestDecision(AccessRequestStatus.Approved, "Approved for the export window.", "binding-42", DateTimeOffset.UnixEpoch.AddHours(8));
        using ParsedJsonDocument<AccessRequest> pendingDoc = ParsedJsonDocument<AccessRequest>.Parse(pending.AsMemory());
        byte[] stored = AccessRequestSerialization.SerializeDecision(pendingDoc.RootElement, "req-7f3c", new WorkflowEtag("etag-1"), decision, "boss", DateTimeOffset.UnixEpoch.AddMinutes(5), new WorkflowEtag("etag-2"));

        this.document = ParsedJsonDocument<AccessRequest>.Parse(stored);
        this.workspace = JsonWorkspace.Create();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.document.Dispose();
        this.workspace.Dispose();
    }

    /// <summary>The field-by-field projection the handler replaced — realises a managed value per scalar field.</summary>
    /// <returns>The serialized body length (returned to defeat dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public long Materialize_fieldByField()
    {
        AccessRequest request = this.document.RootElement;
        Utf8JsonWriter writer = this.workspace.RentWriterAndBuffer(WriterOptions, 1024, out IByteBufferWriter buffer);
        try
        {
            WriteMaterialized(writer, request);
            writer.Flush();
            return buffer.WrittenSpan.Length;
        }
        finally
        {
            this.workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    /// <summary>The element-wrap projection in use — <c>From()</c> reinterprets the backing, <c>WriteTo</c> copies it verbatim.</summary>
    /// <returns>The serialized body length.</returns>
    [Benchmark]
    public long ElementWrap_From()
    {
        Models.AccessRequestView view = Models.AccessRequestView.From(this.document.RootElement);
        Utf8JsonWriter writer = this.workspace.RentWriterAndBuffer(WriterOptions, 1024, out IByteBufferWriter buffer);
        try
        {
            view.WriteTo(writer);
            writer.Flush();
            return buffer.WrittenSpan.Length;
        }
        finally
        {
            this.workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    // The projection the handler used to perform: realise every leaf as a managed value, then write the view object.
    private static void WriteMaterialized(Utf8JsonWriter writer, AccessRequest request)
    {
        string id = request.IdValue;
        string baseWorkflowId = request.BaseWorkflowIdValue;
        string[] scopes = request.RequestedScopesArray();
        string subjectClaimType = request.SubjectClaimTypeValue;
        string subjectClaimValue = request.SubjectClaimValueValue;
        string status = request.StatusValue;
        string createdBy = request.CreatedByValue;
        DateTimeOffset createdAt = request.CreatedAtValue;
        string? requesterLabel = request.RequesterLabelOrNull;
        string? reason = request.ReasonOrNull;
        long? duration = request.RequestedDurationSecondsOrNull;
        string? decidedBy = request.DecidedByOrNull;
        DateTimeOffset? decidedAt = request.DecidedAtValue;
        string? decisionReason = request.DecisionReasonOrNull;
        string? grantedBindingId = request.GrantedBindingIdOrNull;
        DateTimeOffset? grantedUntil = request.GrantedUntilValue;
        string etag = request.EtagValue.Value ?? string.Empty;

        writer.WriteStartObject();
        writer.WriteString("id", id);
        writer.WriteString("baseWorkflowId", baseWorkflowId);
        writer.WriteStartArray("requestedScopes");
        foreach (string scope in scopes)
        {
            writer.WriteStringValue(scope);
        }

        writer.WriteEndArray();
        writer.WriteString("subjectClaimType", subjectClaimType);
        writer.WriteString("subjectClaimValue", subjectClaimValue);
        if (requesterLabel is not null)
        {
            writer.WriteString("requesterLabel", requesterLabel);
        }

        if (reason is not null)
        {
            writer.WriteString("reason", reason);
        }

        if (duration is { } durationValue)
        {
            writer.WriteNumber("requestedDurationSeconds", durationValue);
        }

        if (grantedUntil is { } grantedUntilValue)
        {
            writer.WriteString("grantedUntil", grantedUntilValue);
        }

        writer.WriteString("status", status);
        writer.WriteString("createdBy", createdBy);
        writer.WriteString("createdAt", createdAt);
        if (decidedBy is not null)
        {
            writer.WriteString("decidedBy", decidedBy);
        }

        if (decidedAt is { } decidedAtValue)
        {
            writer.WriteString("decidedAt", decidedAtValue);
        }

        if (decisionReason is not null)
        {
            writer.WriteString("decisionReason", decisionReason);
        }

        if (grantedBindingId is not null)
        {
            writer.WriteString("grantedBindingId", grantedBindingId);
        }

        writer.WriteString("etag", etag);
        writer.WriteEndObject();
    }
}