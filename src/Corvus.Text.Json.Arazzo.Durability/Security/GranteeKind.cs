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

    /// <summary>Gets the canonical lower-case token as UTF-8 (for bytes-to-bytes serialization) — the <see cref="ToToken"/> form without a managed string.</summary>
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

    /// <summary>Parses a canonical token back to a <see cref="GranteeKind"/>.</summary>
    /// <param name="token">The token (e.g. <c>person</c>).</param>
    /// <param name="kind">The parsed kind on success.</param>
    /// <returns><see langword="true"/> if the token is a known grantee kind.</returns>
    public static bool TryParse(string? token, out GranteeKind kind)
    {
        switch (token)
        {
            case "person": kind = GranteeKind.Person; return true;
            case "team": kind = GranteeKind.Team; return true;
            case "role": kind = GranteeKind.Role; return true;
            case "workflow": kind = GranteeKind.Workflow; return true;
            default: kind = default; return false;
        }
    }

    /// <summary>Parses a canonical token (UTF-8) back to a <see cref="GranteeKind"/> — the span counterpart of <see cref="TryParse(string?, out GranteeKind)"/>, no managed string.</summary>
    /// <param name="token">The token as UTF-8 (e.g. <c>person</c>).</param>
    /// <param name="kind">The parsed kind on success.</param>
    /// <returns><see langword="true"/> if the token is a known grantee kind.</returns>
    public static bool TryParse(ReadOnlySpan<byte> token, out GranteeKind kind)
    {
        if (token.SequenceEqual("person"u8))
        {
            kind = GranteeKind.Person;
            return true;
        }

        if (token.SequenceEqual("team"u8))
        {
            kind = GranteeKind.Team;
            return true;
        }

        if (token.SequenceEqual("role"u8))
        {
            kind = GranteeKind.Role;
            return true;
        }

        if (token.SequenceEqual("workflow"u8))
        {
            kind = GranteeKind.Workflow;
            return true;
        }

        kind = default;
        return false;
    }

    /// <summary>Maps a usage-grant dimension back to the <see cref="GranteeKind"/> it names (the inverse of the policy's
    /// grantee→dimension map: <c>sub→person</c>, <c>tenant→team</c>, <c>role→role</c>, <c>workflow→workflow</c>).</summary>
    /// <param name="dimension">The grant dimension (e.g. <c>tenant</c>).</param>
    /// <param name="kind">The grantee kind on success.</param>
    /// <returns><see langword="true"/> if the dimension maps to a well-known grantee kind (a custom dimension does not).</returns>
    public static bool FromDimension(string? dimension, out GranteeKind kind)
    {
        switch (dimension)
        {
            case "sub": kind = GranteeKind.Person; return true;
            case "tenant": kind = GranteeKind.Team; return true;
            case "role": kind = GranteeKind.Role; return true;
            case "workflow": kind = GranteeKind.Workflow; return true;
            default: kind = default; return false;
        }
    }

    /// <summary>Maps a usage-grant dimension (UTF-8) back to the <see cref="GranteeKind"/> it names — the span counterpart of <see cref="FromDimension(string?, out GranteeKind)"/>, no managed string.</summary>
    /// <param name="dimension">The grant dimension as UTF-8 (e.g. <c>tenant</c>).</param>
    /// <param name="kind">The grantee kind on success.</param>
    /// <returns><see langword="true"/> if the dimension maps to a well-known grantee kind.</returns>
    public static bool FromDimension(ReadOnlySpan<byte> dimension, out GranteeKind kind)
    {
        if (dimension.SequenceEqual("sub"u8))
        {
            kind = GranteeKind.Person;
            return true;
        }

        if (dimension.SequenceEqual("tenant"u8))
        {
            kind = GranteeKind.Team;
            return true;
        }

        if (dimension.SequenceEqual("role"u8))
        {
            kind = GranteeKind.Role;
            return true;
        }

        if (dimension.SequenceEqual("workflow"u8))
        {
            kind = GranteeKind.Workflow;
            return true;
        }

        kind = default;
        return false;
    }
}