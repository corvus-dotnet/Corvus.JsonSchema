// <copyright file="SecurityPolicyTypes.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// A consistent point-in-time view of all rules and bindings plus a monotonic generation token a resolver caches
/// against: when the store's generation advances, the cached compiled policy is stale. The rule and binding batches
/// are pooled documents the caller owns — <see cref="Dispose"/> the snapshot once it has been read (a resolver that
/// caches derived state, e.g. compiled expressions, keeps no document references and disposes immediately).
/// </summary>
public sealed class SecurityPolicySnapshot : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="SecurityPolicySnapshot"/> class.</summary>
    /// <param name="rules">All persisted rules.</param>
    /// <param name="bindings">All persisted bindings.</param>
    /// <param name="generation">A monotonically increasing token bumped on every mutation.</param>
    public SecurityPolicySnapshot(PooledDocumentList<SecurityRuleDocument> rules, PooledDocumentList<SecurityBindingDocument> bindings, long generation)
    {
        this.Rules = rules;
        this.Bindings = bindings;
        this.Generation = generation;
    }

    /// <summary>Gets all persisted rules.</summary>
    public PooledDocumentList<SecurityRuleDocument> Rules { get; }

    /// <summary>Gets all persisted bindings.</summary>
    public PooledDocumentList<SecurityBindingDocument> Bindings { get; }

    /// <summary>Gets the monotonically increasing token bumped on every mutation.</summary>
    public long Generation { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Rules.Dispose();
        this.Bindings.Dispose();
    }
}