// <copyright file="SecurityBindingDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// A persisted claim→rule binding (design §14.2): the per-verb grants (read/write/purge) that a principal carrying
/// a matching claim resolves to, plus audit/concurrency metadata. This is the single binding type — generated from
/// <c>Schemas/SecurityBindingDocument.json</c> — used as the domain value <em>and</em> the persisted form.
/// </summary>
/// <remarks>
/// Construction threads the destination through: a store passes the buffer (or stream) it already owns and the
/// binding is realised and written into it in one pass (<see cref="WriteNewBinding"/>/<see cref="WriteUpdatedBinding"/>)
/// — no interim detached clone, no second serialization, and no array copied out of a hidden buffer. The backend
/// consumes that span directly, and where the value is needed back it is parsed once (<see cref="FromJson"/>). The
/// leaf accessors realise a <see cref="string"/> only where one is actually required.
/// </remarks>
[JsonSchemaTypeGenerator("../Schemas/SecurityBindingDocument.json")]
public readonly partial struct SecurityBindingDocument
{
    /// <summary>Gets the binding's stable id.</summary>
    public string IdValue => (string)this.Id;

    /// <summary>Gets the principal claim type this binding keys on (<c>"*"</c> = any authenticated).</summary>
    public string ClaimTypeValue => (string)this.ClaimType;

    /// <summary>Gets the required claim value, or <see langword="null"/> (matches any value).</summary>
    public string? ClaimValueOrNull => this.ClaimValue.IsNotUndefined() ? (string)this.ClaimValue : null;

    /// <summary>Gets the resolution order (ascending).</summary>
    public int OrderValue => this.Order;

    /// <summary>Gets the optional human description, or <see langword="null"/>.</summary>
    public string? DescriptionOrNull => this.Description.IsNotUndefined() ? (string)this.Description : null;

    /// <summary>Gets the actor that created the binding.</summary>
    public string CreatedByValue => (string)this.CreatedBy;

    /// <summary>Gets when the binding was created.</summary>
    public DateTimeOffset CreatedAtValue => ((NodaTime.OffsetDateTime)this.CreatedAt).ToDateTimeOffset();

    /// <summary>Gets the actor that last updated the binding, or <see langword="null"/>.</summary>
    public string? UpdatedByOrNull => this.LastUpdatedBy.IsNotUndefined() ? (string)this.LastUpdatedBy : null;

    /// <summary>Gets when the binding was last updated, or <see langword="null"/>.</summary>
    public DateTimeOffset? UpdatedAtValue => this.LastUpdatedAt.IsNotUndefined() ? ((NodaTime.OffsetDateTime)this.LastUpdatedAt).ToDateTimeOffset() : null;

    /// <summary>Gets the optimistic-concurrency token.</summary>
    public WorkflowEtag EtagValue => new((string)this.Etag);

    /// <summary>Realises a new binding and writes its canonical JSON into the caller's buffer in a single pass.</summary>
    /// <param name="buffer">The destination the caller owns (a rented buffer, or a writer over a stream).</param>
    /// <param name="id">The binding id.</param>
    /// <param name="definition">The binding content (claim match + per-verb grants).</param>
    /// <param name="actor">The actor creating the binding (audit).</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <param name="etag">The optimistic-concurrency token to assign.</param>
    public static void WriteNewBinding(IBufferWriter<byte> buffer, string id, SecurityBindingDefinition definition, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Mutable> builder = CreateBuilder(
            workspace,
            claimType: definition.ClaimType,
            createdAt: createdAt,
            createdBy: actor,
            etag: etag.Value ?? string.Empty,
            id: id,
            order: definition.Order,
            purge: definition.Purge,
            read: definition.Read,
            write: definition.Write,
            claimValue: definition.ClaimValue is { } claimValue ? (JsonString.Source)claimValue : default,
            description: definition.Description is { } description ? (JsonString.Source)description : default);
        Utf8JsonWriter writer = workspace.RentWriter(buffer);
        try
        {
            builder.RootElement.WriteTo(writer);
            writer.Flush();
        }
        finally
        {
            workspace.ReturnWriter(writer);
        }
    }

    /// <summary>Realises an updated copy of this binding (preserving id/created metadata) into the caller's buffer.</summary>
    /// <param name="buffer">The destination the caller owns (a rented buffer, or a writer over a stream).</param>
    /// <param name="definition">The new binding content.</param>
    /// <param name="actor">The actor performing the update (audit).</param>
    /// <param name="updatedAt">The update instant.</param>
    /// <param name="etag">The new optimistic-concurrency token to assign.</param>
    public void WriteUpdatedBinding(IBufferWriter<byte> buffer, SecurityBindingDefinition definition, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();

        // Realise a mutable builder over this document and modify only the fields the update touches; id and the
        // created-* metadata are carried through unchanged (no field-by-field rebuild).
        using JsonDocumentBuilder<Mutable> builder = this.CreateBuilder(workspace);
        builder.RootElement.SetClaimType(definition.ClaimType);
        builder.RootElement.SetOrder(definition.Order);
        builder.RootElement.SetRead(definition.Read);
        builder.RootElement.SetWrite(definition.Write);
        builder.RootElement.SetPurge(definition.Purge);
        builder.RootElement.SetEtag(etag.Value ?? string.Empty);
        builder.RootElement.SetLastUpdatedAt(updatedAt);
        builder.RootElement.SetLastUpdatedBy(actor);
        if (definition.ClaimValue is { } claimValue)
        {
            builder.RootElement.SetClaimValue(claimValue);
        }
        else
        {
            builder.RootElement.RemoveClaimValue();
        }

        if (definition.Description is { } description)
        {
            builder.RootElement.SetDescription(description);
        }
        else
        {
            builder.RootElement.RemoveDescription();
        }

        Utf8JsonWriter writer = workspace.RentWriter(buffer);
        try
        {
            builder.RootElement.WriteTo(writer);
            writer.Flush();
        }
        finally
        {
            workspace.ReturnWriter(writer);
        }
    }

    /// <summary>Parses a binding from its persisted JSON as a detached value (one owned copy).</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The binding.</returns>
    public static SecurityBindingDocument FromJson(ReadOnlyMemory<byte> utf8) => ParseValue(utf8.Span);

    /// <summary>
    /// A per-verb grant: either <see cref="IsUnrestrictedValue"/> access (a <see langword="null"/> reach — the
    /// operator escape) or a set of rule names ANDed together. The generated grant type; the constant
    /// <see cref="None"/>/<see cref="Full"/> grants are realised once and cached, and the rule names are read by
    /// enumerating <see cref="VerbGrantInfo.RuleNames"/> directly (no intermediate list).
    /// </summary>
    public readonly partial struct VerbGrantInfo
    {
        // The two constant grants are parsed once from their canonical JSON literal — no builder, no clone.
        private static readonly VerbGrantInfo NoneGrant = ParseValue("{\"unrestricted\":false}"u8);
        private static readonly VerbGrantInfo FullGrant = ParseValue("{\"unrestricted\":true}"u8);

        /// <summary>Gets a grant that confers nothing (the verb is not granted by this binding).</summary>
        public static VerbGrantInfo None => NoneGrant;

        /// <summary>Gets a grant of unrestricted (full-reach) access for the verb.</summary>
        public static VerbGrantInfo Full => FullGrant;

        /// <summary>Gets a value indicating whether the verb is unrestricted (full reach).</summary>
        public bool IsUnrestrictedValue => this.Unrestricted.IsNotUndefined() && (bool)this.Unrestricted;

        /// <summary>Gets the number of rule names ANDed for the verb (zero when unrestricted or ungranted).</summary>
        public int RuleNameCount => this.RuleNames.IsNotUndefined() ? this.RuleNames.GetArrayLength() : 0;

        /// <summary>Gets a value indicating whether this grant names one or more rules.</summary>
        public bool HasRuleNames => this.RuleNameCount > 0;

        /// <summary>Gets a value indicating whether this grant confers nothing.</summary>
        public bool IsEmptyValue => !this.IsUnrestrictedValue && !this.HasRuleNames;

        /// <summary>Builds a grant of the conjunction of the named rules, detached and ready to use.</summary>
        /// <param name="ruleNames">The rule names (ANDed).</param>
        /// <returns>The grant.</returns>
        public static VerbGrantInfo Rules(params string[] ruleNames)
        {
            // Thread the names through as the build context so the array callback can be static — no closure allocation.
            using JsonWorkspace workspace = JsonWorkspace.Create();
            using JsonDocumentBuilder<Mutable> builder = CreateBuilder(
                workspace,
                ruleNames,
                ruleNames: JsonStringArray.Build(
                    ruleNames,
                    static (in string[] names, ref JsonStringArray.Builder array) =>
                    {
                        foreach (string ruleName in names)
                        {
                            array.AddItem(ruleName);
                        }
                    }),
                unrestricted: false);
            return builder.RootElement.Clone();
        }
    }
}