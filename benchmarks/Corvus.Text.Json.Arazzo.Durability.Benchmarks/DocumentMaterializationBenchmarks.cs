// <copyright file="DocumentMaterializationBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Compares the two ways a store hands a persisted document back to a consumer: the <em>detached</em>
/// <see cref="SecurityRuleDocument.FromJson"/> (owns a fresh GC <see cref="byte"/> array + metadata every call) versus
/// the <em>pooled</em> <see cref="PersistedJson.ToPooledDocument{T}"/> (rents its backing buffer + metadata from the
/// pool and returns them on dispose, so only the small document wrapper hits the GC heap).
/// </summary>
public class DocumentMaterializationBenchmarks
{
    private byte[] ruleJson = null!;

    [GlobalSetup]
    public void Setup()
    {
        using ParsedJsonDocument<SecurityRuleDocument> draft = SecurityRuleDocument.Draft("sys:tenant == $claim.tenant", "Tenant isolation.");
        this.ruleJson = SecurityPolicySerialization.SerializeNewRule(
            "tenant-scoped",
            draft.RootElement,
            "alice",
            DateTimeOffset.UnixEpoch,
            new WorkflowEtag("etag-1"));
    }

    /// <summary>Pure materialization, detached: owns a fresh byte[] + metadata each call.</summary>
    /// <returns>The parsed value kind (no leaf-string allocation).</returns>
    [Benchmark(Baseline = true)]
    public JsonValueKind Detached_Materialize()
    {
        SecurityRuleDocument document = SecurityRuleDocument.FromJson(this.ruleJson);
        return document.ValueKind;
    }

    /// <summary>Pure materialization, pooled: rents + returns the backing buffer; only the wrapper is GC.</summary>
    /// <returns>The parsed value kind (no leaf-string allocation).</returns>
    [Benchmark]
    public JsonValueKind Pooled_Materialize()
    {
        using ParsedJsonDocument<SecurityRuleDocument> document = PersistedJson.ToPooledDocument<SecurityRuleDocument>(this.ruleJson);
        return document.RootElement.ValueKind;
    }

    /// <summary>Realistic read, detached: materialize then read a field (the field string is a leaf, common to both).</summary>
    /// <returns>The rule name.</returns>
    [Benchmark]
    public string Detached_ReadField()
    {
        SecurityRuleDocument document = SecurityRuleDocument.FromJson(this.ruleJson);
        return document.NameValue;
    }

    /// <summary>Realistic read, pooled: materialize, read a field, dispose (buffer recycled).</summary>
    /// <returns>The rule name.</returns>
    [Benchmark]
    public string Pooled_ReadField()
    {
        using ParsedJsonDocument<SecurityRuleDocument> document = PersistedJson.ToPooledDocument<SecurityRuleDocument>(this.ruleJson);
        return document.RootElement.NameValue;
    }

    /// <summary>Create() adoption row 1.1 "before": the replaced draft construction — a writer callback serialized
    /// through <see cref="PersistedJson.ToPooledDocument{T,TContext}"/> and reparsed into the pooled document. The old
    /// shape is preserved inline here so the row's delta stays measured, not asserted.</summary>
    /// <returns>The parsed value kind (no leaf-string allocation).</returns>
    [Benchmark]
    public JsonValueKind RunnerAuthorizationDraft_SerializeReparse()
    {
        var state = ("production", "runner-01");
        using ParsedJsonDocument<EnvironmentRunnerAuthorization> draft =
            PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization, (string Environment, string RunnerId)>(
                in state,
                static (Utf8JsonWriter writer, in (string Environment, string RunnerId) c) =>
                {
                    writer.WriteStartObject();
                    writer.WriteString("environment"u8, c.Environment);
                    writer.WriteString("runnerId"u8, c.RunnerId);
                    writer.WriteEndObject();
                });
        return draft.RootElement.ValueKind;
    }

    /// <summary>Create() adoption row 1.1 "after": the production <see cref="EnvironmentRunnerAuthorization.Draft"/>,
    /// now the generated <c>Create()</c> — document text and parse metadata written in one pass, no reparse.</summary>
    /// <returns>The parsed value kind (no leaf-string allocation).</returns>
    [Benchmark]
    public JsonValueKind RunnerAuthorizationDraft_Create()
    {
        using ParsedJsonDocument<EnvironmentRunnerAuthorization> draft = EnvironmentRunnerAuthorization.Draft("production", "runner-01");
        return draft.RootElement.ValueKind;
    }

    private SecurityTagSet environmentTags;

    [GlobalSetup(Targets = [nameof(EnvironmentDraft_WriterSplice), nameof(EnvironmentDraft_CreateEmbed_Rejected)])]
    public void SetupEnvironmentDraft()
        => this.environmentTags = SecurityTagSet.FromTags(
            [new("sys:group", "arazzo-admins"), new("sys:iss", "arazzo-keycloak"), new("classification", "internal")]);

    /// <summary>Create() adoption row 1.2, the KEPT shape: the production string-overload
    /// <see cref="Environments.Environment.Draft(string, string?, string?, SecurityTagSet)"/> — a writer callback whose
    /// raw <see cref="SecurityTagSet"/> splice (<c>WriteRawValue</c>) dominates the document.</summary>
    /// <returns>The parsed value kind (no leaf-string allocation).</returns>
    [Benchmark]
    public JsonValueKind EnvironmentDraft_WriterSplice()
    {
        using ParsedJsonDocument<Environments.Environment> draft =
            Environments.Environment.Draft("production", "Production", "The production environment.", this.environmentTags);
        return draft.RootElement.ValueKind;
    }

    /// <summary>Create() adoption row 1.3 "before": the replaced availability-entry draft — a writer callback (two
    /// strings + a number) serialized through <see cref="PersistedJson.ToPooledDocument{T,TContext}"/> and reparsed.
    /// Preserved inline so the delta stays measured.</summary>
    /// <returns>The parsed value kind (no leaf-string allocation).</returns>
    [Benchmark]
    public JsonValueKind AvailabilityEntryDraft_SerializeReparse()
    {
        var state = ("orders-workflow", 3, "production");
        using ParsedJsonDocument<AvailabilityEntry> draft =
            PersistedJson.ToPooledDocument<AvailabilityEntry, (string BaseWorkflowId, int VersionNumber, string Environment)>(
                in state,
                static (Utf8JsonWriter writer, in (string BaseWorkflowId, int VersionNumber, string Environment) s) =>
                {
                    writer.WriteStartObject();
                    writer.WriteString("baseWorkflowId"u8, s.BaseWorkflowId);
                    writer.WriteNumber("versionNumber"u8, s.VersionNumber);
                    writer.WriteString("environment"u8, s.Environment);
                    writer.WriteEndObject();
                });
        return draft.RootElement.ValueKind;
    }

    /// <summary>Create() adoption row 1.3 "after": the production <see cref="AvailabilityEntry.Draft"/>, now the
    /// generated <c>Create()</c> — document text and parse metadata written in one pass, no reparse.</summary>
    /// <returns>The parsed value kind (no leaf-string allocation).</returns>
    [Benchmark]
    public JsonValueKind AvailabilityEntryDraft_Create()
    {
        using ParsedJsonDocument<AvailabilityEntry> draft = AvailabilityEntry.Draft("orders-workflow", 3, "production");
        return draft.RootElement.ValueKind;
    }

    /// <summary>Create() adoption row 1.4 "before": the replaced availability-request draft — a writer callback (two
    /// strings + a number + an optional reason) serialized through <see cref="PersistedJson.ToPooledDocument{T,TContext}"/>
    /// and reparsed. Preserved inline so the delta stays measured.</summary>
    /// <returns>The parsed value kind (no leaf-string allocation).</returns>
    [Benchmark]
    public JsonValueKind AvailabilityRequestDraft_SerializeReparse()
    {
        var state = ("orders-workflow", 3, "production", (string?)"Rollout wave 2.");
        using ParsedJsonDocument<AvailabilityRequest> draft =
            PersistedJson.ToPooledDocument<AvailabilityRequest, (string BaseWorkflowId, int VersionNumber, string Environment, string? Reason)>(
                in state,
                static (Utf8JsonWriter writer, in (string BaseWorkflowId, int VersionNumber, string Environment, string? Reason) c) =>
                {
                    writer.WriteStartObject();
                    writer.WriteString("baseWorkflowId"u8, c.BaseWorkflowId);
                    writer.WriteNumber("versionNumber"u8, c.VersionNumber);
                    writer.WriteString("environment"u8, c.Environment);
                    if (c.Reason is { } reason)
                    {
                        writer.WriteString("reason"u8, reason);
                    }

                    writer.WriteEndObject();
                });
        return draft.RootElement.ValueKind;
    }

    /// <summary>Create() adoption row 1.4 "after": the production <see cref="AvailabilityRequest.Draft"/>, now the
    /// generated <c>Create()</c> — document text and parse metadata written in one pass, no reparse.</summary>
    /// <returns>The parsed value kind (no leaf-string allocation).</returns>
    [Benchmark]
    public JsonValueKind AvailabilityRequestDraft_Create()
    {
        using ParsedJsonDocument<AvailabilityRequest> draft =
            AvailabilityRequest.Draft("orders-workflow", 3, "production", "Rollout wave 2.");
        return draft.RootElement.ValueKind;
    }

    /// <summary>Create() adoption row 1.5 "before": the replaced access-request draft — a writer callback (values + a
    /// scopes array loop) serialized through <see cref="PersistedJson.ToPooledDocument{T,TContext}"/> and reparsed.
    /// Preserved inline so the delta stays measured.</summary>
    /// <returns>The parsed value kind (no leaf-string allocation).</returns>
    [Benchmark]
    public JsonValueKind AccessRequestDraft_SerializeReparse()
    {
        var state = ("onboard-customer", (IReadOnlyList<string>)["runs:write", "runs:read"], "sub", "alice", (string?)"Alice (Payments)", (string?)"On-call incident response.", (long?)3600);
        using ParsedJsonDocument<AccessRequest> draft =
            PersistedJson.ToPooledDocument<AccessRequest, (string BaseWorkflowId, IReadOnlyList<string> Scopes, string ClaimType, string ClaimValue, string? Label, string? Reason, long? Duration)>(
                in state,
                static (Utf8JsonWriter writer, in (string BaseWorkflowId, IReadOnlyList<string> Scopes, string ClaimType, string ClaimValue, string? Label, string? Reason, long? Duration) c) =>
                {
                    writer.WriteStartObject();
                    writer.WriteString("baseWorkflowId"u8, c.BaseWorkflowId);
                    writer.WriteStartArray("requestedScopes"u8);
                    foreach (string scope in c.Scopes)
                    {
                        writer.WriteStringValue(scope);
                    }

                    writer.WriteEndArray();
                    writer.WriteString("subjectClaimType"u8, c.ClaimType);
                    writer.WriteString("subjectClaimValue"u8, c.ClaimValue);
                    if (c.Label is { } label)
                    {
                        writer.WriteString("requesterLabel"u8, label);
                    }

                    if (c.Reason is { } reason)
                    {
                        writer.WriteString("reason"u8, reason);
                    }

                    if (c.Duration is { } duration)
                    {
                        writer.WriteNumber("requestedDurationSeconds"u8, duration);
                    }

                    writer.WriteEndObject();
                });
        return draft.RootElement.ValueKind;
    }

    /// <summary>Create() adoption row 1.5 "after": the production string-overload <see cref="AccessRequest.Draft(string, IReadOnlyList{string}, string, string, string?, string?, long?)"/>,
    /// now the generated <c>Create()</c> with the scopes folded in closure-free — one pass, no reparse.</summary>
    /// <returns>The parsed value kind (no leaf-string allocation).</returns>
    [Benchmark]
    public JsonValueKind AccessRequestDraft_Create()
    {
        using ParsedJsonDocument<AccessRequest> draft = AccessRequest.Draft(
            "onboard-customer", ["runs:write", "runs:read"], "sub", "alice", "Alice (Payments)", "On-call incident response.", 3600);
        return draft.RootElement.ValueKind;
    }

    /// <summary>Create() adoption row 1.6 "before": the replaced security-rule draft — a writer callback (an expression
    /// + an optional description) serialized through <see cref="PersistedJson.ToPooledDocument{T,TContext}"/> and
    /// reparsed. Preserved inline so the delta stays measured.</summary>
    /// <returns>The parsed value kind (no leaf-string allocation).</returns>
    [Benchmark]
    public JsonValueKind SecurityRuleDraft_SerializeReparse()
    {
        var state = ("sys:tenant == $claim.tenant", (string?)"Tenant isolation.");
        using ParsedJsonDocument<SecurityRuleDocument> draft =
            PersistedJson.ToPooledDocument<SecurityRuleDocument, (string Expression, string? Description)>(
                in state,
                static (Utf8JsonWriter writer, in (string Expression, string? Description) c) =>
                {
                    writer.WriteStartObject();
                    writer.WriteString("expression"u8, c.Expression);
                    if (c.Description is { } description)
                    {
                        writer.WriteString("description"u8, description);
                    }

                    writer.WriteEndObject();
                });
        return draft.RootElement.ValueKind;
    }

    /// <summary>Create() adoption row 1.6 "after": the production <see cref="SecurityRuleDocument.Draft"/>, now the
    /// generated <c>Create()</c> — document text and parse metadata written in one pass, no reparse.</summary>
    /// <returns>The parsed value kind (no leaf-string allocation).</returns>
    [Benchmark]
    public JsonValueKind SecurityRuleDraft_Create()
    {
        using ParsedJsonDocument<SecurityRuleDocument> draft =
            SecurityRuleDocument.Draft("sys:tenant == $claim.tenant", "Tenant isolation.");
        return draft.RootElement.ValueKind;
    }

    /// <summary>Create() adoption row 1.2, the REJECTED shape (kept measurable per the R4 judgment): the generated
    /// <c>Create()</c> with the tag bytes parsed into a temp pooled document and blitted in as an element. Measured
    /// 716→1,299 ns and 152→304 B against the writer splice — the raw tag bytes dominate this document, so the extra
    /// parse + temp wrapper is a regression and the production path keeps the writer splice.</summary>
    /// <returns>The parsed value kind (no leaf-string allocation).</returns>
    [Benchmark]
    public JsonValueKind EnvironmentDraft_CreateEmbed_Rejected()
    {
        using ParsedJsonDocument<Environments.Environment.SecurityTagInfoArray> tags =
            PersistedJson.ToPooledDocument<Environments.Environment.SecurityTagInfoArray>(this.environmentTags.RawJson);
        using ParsedJsonDocument<Environments.Environment> draft = Environments.Environment.Create(
            createdAt: default,
            createdBy: default,
            etag: default,
            name: "production",
            description: "The production environment.",
            displayName: "Production",
            managementTags: tags.RootElement);
        return draft.RootElement.ValueKind;
    }
}