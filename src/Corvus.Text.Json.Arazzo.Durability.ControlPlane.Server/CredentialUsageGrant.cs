// <copyright file="CredentialUsageGrant.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// An operator-supplied grant of which identity may <em>use</em> a source credential binding (design §13): an identity
/// reference the deployment maps to an <strong>unforgeable internal tag</strong>, not a free-form label. A grant names a
/// dimension (e.g. <c>workflow</c> or <c>tenant</c>) and the identity value (e.g. the base workflow id, or a tenant id);
/// the <see cref="ControlPlaneRowSecurityPolicy.ResolveUsageGrants"/> hook maps it to the internal tag a run carries
/// (e.g. <c>sys:workflow=nightly-reconcile</c>). Because the run's matching tags are deployment-stamped (never set by
/// the workflow author), a grant cannot be self-satisfied.
/// </summary>
/// <param name="Dimension">The identity dimension the grant scopes by (e.g. <c>workflow</c>, <c>tenant</c>).</param>
/// <param name="Value">The identity value (e.g. the base workflow id, or a tenant id).</param>
public readonly record struct CredentialUsageGrant(string Dimension, string Value);