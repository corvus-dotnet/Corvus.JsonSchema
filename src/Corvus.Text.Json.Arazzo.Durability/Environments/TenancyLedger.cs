// <copyright file="TenancyLedger.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Environments;

/// <summary>
/// The deployment's single serialization row for ADR 0065's tenancy invariant: the distinct owner groups admitted to
/// hold an environment here. Generated from <c>Schemas/TenancyLedger.json</c> and used as the domain value <em>and</em>
/// the persisted form. There is exactly one of these per deployment.
/// </summary>
/// <remarks>
/// <para>
/// It is both the census and the interlock. As a census it answers "how many distinct owner groups does this deployment
/// hold" from one row rather than a scan of every environment. As an interlock it carries an etag that every governed
/// write compare-and-swaps, so two simultaneous writes cannot both observe a single-group deployment and both commit,
/// which is the race a gate that only reads-and-decides leaves open.
/// </para>
/// <para>
/// The set is <strong>append-only</strong>. Deleting the last environment of an owner group does not remove it: the
/// removal would have to prove absence by a scan and commit by a compare-and-swap, and those are not one operation, so
/// a concurrent create would slip between them. Over-refusal is the direction a gate must fail in.
/// </para>
/// <para>
/// Queries are string-free (<see cref="Admits"/> / <see cref="Introduces"/> compare the caller's UTF-8 against the
/// stored bytes), and writes carry the already-admitted groups <strong>bytes-to-bytes</strong> — an owner group is
/// never realised as a managed string on the way through.
/// </para>
/// </remarks>
[JsonSchemaTypeGenerator("../Schemas/TenancyLedger.json")]
public readonly partial struct TenancyLedger
{
    /// <summary>Gets the optimistic-concurrency token — the whole of the interlock.</summary>
    public WorkflowEtag EtagValue => new((string)this.Etag);

    /// <summary>Gets the number of distinct owner groups admitted so far (zero when the row carries none, and zero on
    /// an undefined ledger, which is how "no row exists yet" reads).</summary>
    public int OwnerGroupCount => this.IsNotUndefined() && this.OwnerGroups.IsNotUndefined() ? this.OwnerGroups.GetArrayLength() : 0;

    /// <summary>Determines whether <paramref name="ownerGroup"/> has already been admitted, comparing its UTF-8 against
    /// the stored bytes (no per-entry string).</summary>
    /// <param name="ownerGroup">The owner group's UTF-8 value.</param>
    /// <returns><see langword="true"/> when the group is already in the ledger.</returns>
    public bool Admits(ReadOnlySpan<byte> ownerGroup)
    {
        // The undefined check comes first and is not folded into the array check below: a property getter on an
        // undefined value dereferences a backing document that is not there, so the guard has to precede the read.
        if (this.IsUndefined() || this.OwnerGroups.IsUndefined())
        {
            return false;
        }

        foreach (JsonString admitted in this.OwnerGroups.EnumerateArray())
        {
            if (admitted.ValueEquals(ownerGroup))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Answers the gate's whole question in one pass: whether admitting <paramref name="ownerGroup"/> would
    /// introduce a group this deployment has not held before, and how many distinct groups it would then hold.</summary>
    /// <param name="ownerGroup">The writing principal's owner group, as UTF-8.</param>
    /// <param name="distinctAfterwards">The distinct owner-group count the deployment would hold once the write lands.</param>
    /// <returns><see langword="true"/> when the group is not yet admitted, so the write introduces one and must
    /// compare-and-swap it in; <see langword="false"/> when the group is already present.</returns>
    public bool Introduces(ReadOnlySpan<byte> ownerGroup, out int distinctAfterwards)
    {
        int count = 0;
        bool present = false;
        if (this.IsNotUndefined() && this.OwnerGroups.IsNotUndefined())
        {
            foreach (JsonString admitted in this.OwnerGroups.EnumerateArray())
            {
                ++count;
                if (!present && admitted.ValueEquals(ownerGroup))
                {
                    present = true;
                }
            }
        }

        distinctAfterwards = present ? count : count + 1;
        return !present;
    }

    /// <summary>Writes the ledger's next state into the caller's (pooled) writer in one pass: the already-admitted
    /// groups carried bytes-to-bytes from <paramref name="current"/>, then <paramref name="admitting"/> appended when it
    /// is non-empty, then the stamped audit and concurrency values.</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="current">The ledger being replaced, or an undefined value when no row exists yet.</param>
    /// <param name="admitting">The owner group this write introduces, as UTF-8. An empty value admits nothing and
    /// commits the already-admitted set unchanged under a fresh etag.</param>
    /// <param name="actor">The actor whose governed write this is (audit).</param>
    /// <param name="committedAt">The commit instant.</param>
    /// <param name="etag">The new optimistic-concurrency token to assign.</param>
    public static void WriteCommitted(Utf8JsonWriter writer, in TenancyLedger current, ReadOnlySpan<byte> admitting, string actor, DateTimeOffset committedAt, WorkflowEtag etag)
    {
        writer.WriteStartObject();
        writer.WritePropertyName(JsonPropertyNames.OwnerGroupsUtf8);
        writer.WriteStartArray();

        // Already-admitted groups copied verbatim — the stored tokens, never parsed-and-reformatted.
        if (current.IsNotUndefined() && current.OwnerGroups.IsNotUndefined())
        {
            foreach (JsonString admitted in current.OwnerGroups.EnumerateArray())
            {
                admitted.WriteTo(writer);
            }
        }

        // Admission order, not sorted: the only reader is Admits/Introduces, which finds an entry by value. Sorting
        // would mean comparing an unescaped UTF-8 value against stored JSON text that may carry escapes, which is work
        // no consumer asks for.
        if (!admitting.IsEmpty)
        {
            writer.WriteStringValue(admitting);
        }

        writer.WriteEndArray();
        writer.WriteString(JsonPropertyNames.LastUpdatedByUtf8, actor);
        writer.WriteString(JsonPropertyNames.LastUpdatedAtUtf8, committedAt);
        writer.WriteString(JsonPropertyNames.EtagUtf8, etag.Value ?? string.Empty);
        writer.WriteEndObject();
    }
}