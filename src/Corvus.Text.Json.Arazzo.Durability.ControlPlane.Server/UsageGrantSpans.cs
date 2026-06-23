// <copyright file="UsageGrantSpans.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Carries a described usage grant's (dimension, value) as unescaped UTF-8 spans through a closure-free
/// <c>Build&lt;TContext&gt;</c> item projection — the allocation-free counterpart of the
/// <see cref="CredentialUsageGrant"/> record (no managed strings). Each response handler threads this to a static item
/// builder that writes the two spans into its own generated model via the <c>(JsonString.Source)</c> span conversion;
/// the spans are valid only for the synchronous build, which is all the builder needs (it copies them into the document).
/// </summary>
internal readonly ref struct UsageGrantSpans(ReadOnlySpan<byte> dimension, ReadOnlySpan<byte> value)
{
    /// <summary>Gets the grant dimension (the stored tag key with the internal prefix stripped) as unescaped UTF-8.</summary>
    public ReadOnlySpan<byte> Dimension { get; } = dimension;

    /// <summary>Gets the grant value as unescaped UTF-8.</summary>
    public ReadOnlySpan<byte> Value { get; } = value;
}