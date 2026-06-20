// <copyright file="GranteeKind.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// A well-known kind of grantee the deployment can resolve to an exact <c>sys:</c> identity (design §16.5.4) — the
/// vocabulary an operator names a view / operate / administer grantee in, instead of hand-assembling a
/// <c>{dimension, value}</c> tuple. The deployment's <see cref="ControlPlaneRowSecurityPolicy"/> declares which of
/// these it can resolve.
/// </summary>
public enum GranteeKind
{
    /// <summary>An individual principal (resolved to its full per-principal identity, e.g. <c>{sys:tenant, sys:sub}</c>).</summary>
    Person,

    /// <summary>A team / tenant (resolved to the team identity, e.g. <c>sys:tenant=acme</c>).</summary>
    Team,

    /// <summary>A role (resolved to the role identity, e.g. <c>sys:role=operator</c>).</summary>
    Role,

    /// <summary>A workflow, named by its base id (resolved to <c>sys:workflow=&lt;id&gt;</c>).</summary>
    Workflow,
}

/// <summary>Canonical lower-case token mapping for <see cref="GranteeKind"/> — the on-the-wire and persisted form.</summary>
public static class GranteeKinds
{
    /// <summary>Gets the canonical lower-case token for a <see cref="GranteeKind"/> (e.g. <see cref="GranteeKind.Person"/> → <c>person</c>).</summary>
    /// <param name="kind">The grantee kind.</param>
    /// <returns>The canonical token.</returns>
    public static string ToToken(this GranteeKind kind) => kind switch
    {
        GranteeKind.Person => "person",
        GranteeKind.Team => "team",
        GranteeKind.Role => "role",
        GranteeKind.Workflow => "workflow",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown grantee kind."),
    };

    /// <summary>Gets the canonical lower-case token as UTF-8 (for bytes-to-bytes serialization) — the <see cref="ToToken(GranteeKind)"/> form without a managed string.</summary>
    /// <param name="kind">The grantee kind.</param>
    /// <returns>The canonical token as a UTF-8 span.</returns>
    public static ReadOnlySpan<byte> ToTokenUtf8(this GranteeKind kind) => kind switch
    {
        GranteeKind.Person => "person"u8,
        GranteeKind.Team => "team"u8,
        GranteeKind.Role => "role"u8,
        GranteeKind.Workflow => "workflow"u8,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown grantee kind."),
    };

    // ── C# domain enum ⇄ CTJ ObservedIdentity.GranteeKind adapters ───────────────────────────────────────────────────
    //
    // The observed-identity store seam carries the JSON value (ObservedIdentity.GranteeKind), not this domain enum — so a
    // handler converts a request's grantee kind to the store kind with a straight From(). These adapters bridge only the
    // genuinely C#-enum-typed leaves (the directory seam, the policy's whole-grain/dimension maps): a kind that originates
    // as this enum (inferred from a {dimension, value} grant) becomes the store's CTJ kind via ToObservedKind, and the CTJ
    // store kind reifies back to this enum at a C#-enum leaf via ToGranteeKind. Both are allocation-free (the EnumValues
    // are pre-built constants; the token compares are span-equality), so the store key still reifies via the interned
    // ToToken below — never a per-call GC string.

    /// <summary>Gets the canonical lower-case token for a CTJ <see cref="ObservedIdentity.GranteeKind"/> as an interned
    /// literal — the storage-key leaf for a backend that needs a concrete key, without reifying a per-call string (the
    /// counterpart of <see cref="ToToken(GranteeKind)"/> for the value the store seam carries).</summary>
    /// <param name="kind">The CTJ grantee kind.</param>
    /// <returns>The canonical token (an interned literal).</returns>
    public static string ToToken(this ObservedIdentity.GranteeKind kind)
    {
        if (kind.ValueEquals("person"u8))
        {
            return "person";
        }

        if (kind.ValueEquals("team"u8))
        {
            return "team";
        }

        if (kind.ValueEquals("role"u8))
        {
            return "role";
        }

        if (kind.ValueEquals("workflow"u8))
        {
            return "workflow";
        }

        throw new ArgumentOutOfRangeException(nameof(kind), "Unknown grantee kind.");
    }

    /// <summary>Maps a domain <see cref="GranteeKind"/> to the CTJ <see cref="ObservedIdentity.GranteeKind"/> the observed
    /// store seam carries — for a kind that originates as this enum (inferred from a grant dimension). Returns the
    /// pre-built enum constant, so no JSON is parsed and nothing is allocated.</summary>
    /// <param name="kind">The domain grantee kind.</param>
    /// <returns>The equivalent CTJ grantee kind.</returns>
    public static ObservedIdentity.GranteeKind ToObservedKind(this GranteeKind kind) => kind switch
    {
        GranteeKind.Person => ObservedIdentity.GranteeKind.EnumValues.Person,
        GranteeKind.Team => ObservedIdentity.GranteeKind.EnumValues.Team,
        GranteeKind.Role => ObservedIdentity.GranteeKind.EnumValues.Role,
        GranteeKind.Workflow => ObservedIdentity.GranteeKind.EnumValues.Workflow,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown grantee kind."),
    };

    /// <summary>Reifies a CTJ <see cref="ObservedIdentity.GranteeKind"/> back to the domain <see cref="GranteeKind"/> — at
    /// a genuinely C#-enum-typed leaf (the directory seam / the policy), the inverse of <see cref="ToObservedKind"/>.
    /// Allocation-free (span-equality, no managed string).</summary>
    /// <param name="kind">The CTJ grantee kind (must be defined).</param>
    /// <returns>The equivalent domain grantee kind.</returns>
    public static GranteeKind ToGranteeKind(this ObservedIdentity.GranteeKind kind)
    {
        if (kind.ValueEquals("person"u8))
        {
            return GranteeKind.Person;
        }

        if (kind.ValueEquals("team"u8))
        {
            return GranteeKind.Team;
        }

        if (kind.ValueEquals("role"u8))
        {
            return GranteeKind.Role;
        }

        if (kind.ValueEquals("workflow"u8))
        {
            return GranteeKind.Workflow;
        }

        throw new ArgumentOutOfRangeException(nameof(kind), "Unknown grantee kind.");
    }
}