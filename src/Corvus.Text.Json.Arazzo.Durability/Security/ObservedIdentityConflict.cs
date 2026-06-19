// <copyright file="ObservedIdentityConflict.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// The existing grantee an identity-collision probe found (design §16.5.4): a (kind, value) <em>different</em> from the
/// one being authored that already holds a set-equal <c>sys:</c> identity. Surfacing it lets the grant-authoring path
/// refuse an ambiguous grant. Because the probe runs at full reach (a collision is unsafe regardless of who can see the
/// other party), a handler must not echo cross-reach details to the caller — a generic refusal is the safe response;
/// the conflict is for the server's decision and audit log.
/// </summary>
/// <param name="Kind">The conflicting grantee's kind.</param>
/// <param name="Value">The conflicting grantee's value.</param>
/// <param name="Label">The conflicting grantee's display label, if any.</param>
public readonly record struct ObservedIdentityConflict(GranteeKind Kind, string Value, string? Label);