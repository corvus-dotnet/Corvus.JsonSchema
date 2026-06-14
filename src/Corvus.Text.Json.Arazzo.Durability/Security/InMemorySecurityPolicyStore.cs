// <copyright file="InMemorySecurityPolicyStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// The reference in-memory <see cref="ISecurityPolicyStore"/> — the conformance baseline and a ready store for
/// single-node / local-development hosts. Each record is held as its UTF-8 JSON document (the "push JSON to the
/// store" shape every backend persists), keyed by name/id; reads hand back a pooled <see cref="ParsedJsonDocument{T}"/>
/// the caller disposes. Every mutation bumps a monotonic generation a resolver caches against, and stamps a fresh
/// per-record etag.
/// </summary>
public sealed class InMemorySecurityPolicyStore : ISecurityPolicyStore
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, byte[]> rules = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> bindings = new(StringComparer.Ordinal);
    private readonly TimeProvider timeProvider;
    private long generation;
    private long etagSequence;
    private long bindingSequence;

    /// <summary>Initializes a new instance of the <see cref="InMemorySecurityPolicyStore"/> class.</summary>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    public InMemorySecurityPolicyStore(TimeProvider? timeProvider = null)
        => this.timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<SecurityRuleDocument>> AddRuleAsync(string name, SecurityRuleDefinition definition, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(definition.Expression);
        ArgumentNullException.ThrowIfNull(actor);

        lock (this.gate)
        {
            if (this.rules.ContainsKey(name))
            {
                throw new InvalidOperationException($"A security rule named '{name}' already exists.");
            }

            byte[] json = SecurityPolicySerialization.SerializeNewRule(name, definition, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
            this.rules[name] = json;
            this.generation++;
            return new ValueTask<ParsedJsonDocument<SecurityRuleDocument>>(PersistedJson.ToPooledDocument<SecurityRuleDocument>(json));
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<SecurityRuleDocument>?> GetRuleAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        lock (this.gate)
        {
            return new ValueTask<ParsedJsonDocument<SecurityRuleDocument>?>(
                this.rules.TryGetValue(name, out byte[]? json) ? PersistedJson.ToPooledDocument<SecurityRuleDocument>(json) : null);
        }
    }

    /// <inheritdoc/>
    public ValueTask<PooledDocumentList<SecurityRuleDocument>> ListRulesAsync(CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            var docs = new List<ParsedJsonDocument<SecurityRuleDocument>>(this.rules.Count);
            foreach (byte[] json in this.rules.Values)
            {
                docs.Add(PersistedJson.ToPooledDocument<SecurityRuleDocument>(json));
            }

            docs.Sort(static (a, b) => string.CompareOrdinal(a.RootElement.NameValue, b.RootElement.NameValue));
            return new ValueTask<PooledDocumentList<SecurityRuleDocument>>(new PooledDocumentList<SecurityRuleDocument>(docs));
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<SecurityRuleDocument>?> UpdateRuleAsync(string name, SecurityRuleDefinition definition, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentException.ThrowIfNullOrEmpty(definition.Expression);
        ArgumentNullException.ThrowIfNull(actor);

        lock (this.gate)
        {
            if (!this.rules.TryGetValue(name, out byte[]? existing))
            {
                return new ValueTask<ParsedJsonDocument<SecurityRuleDocument>?>((ParsedJsonDocument<SecurityRuleDocument>?)null);
            }

            byte[] json = SecurityPolicySerialization.SerializeUpdatedRule(existing, "rule", name, expectedEtag, definition, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
            this.rules[name] = json;
            this.generation++;
            return new ValueTask<ParsedJsonDocument<SecurityRuleDocument>?>(PersistedJson.ToPooledDocument<SecurityRuleDocument>(json));
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteRuleAsync(string name, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        lock (this.gate)
        {
            if (!this.rules.TryGetValue(name, out byte[]? existing))
            {
                return new ValueTask<bool>(false);
            }

            if (!expectedEtag.IsNone)
            {
                using ParsedJsonDocument<SecurityRuleDocument> current = PersistedJson.ToPooledDocument<SecurityRuleDocument>(existing);
                SecurityPolicySerialization.EnsureEtag("rule", name, expectedEtag, current.RootElement.EtagValue);
            }

            this.rules.Remove(name);
            this.generation++;
            return new ValueTask<bool>(true);
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<SecurityBindingDocument>> AddBindingAsync(SecurityBindingDefinition definition, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(definition.ClaimType);
        ArgumentNullException.ThrowIfNull(actor);

        lock (this.gate)
        {
            string id = "bnd-" + (++this.bindingSequence).ToString(CultureInfo.InvariantCulture);
            byte[] json = SecurityPolicySerialization.SerializeNewBinding(id, definition, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
            this.bindings[id] = json;
            this.generation++;
            return new ValueTask<ParsedJsonDocument<SecurityBindingDocument>>(PersistedJson.ToPooledDocument<SecurityBindingDocument>(json));
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<SecurityBindingDocument>?> GetBindingAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        lock (this.gate)
        {
            return new ValueTask<ParsedJsonDocument<SecurityBindingDocument>?>(
                this.bindings.TryGetValue(id, out byte[]? json) ? PersistedJson.ToPooledDocument<SecurityBindingDocument>(json) : null);
        }
    }

    /// <inheritdoc/>
    public ValueTask<PooledDocumentList<SecurityBindingDocument>> ListBindingsAsync(CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            return new ValueTask<PooledDocumentList<SecurityBindingDocument>>(this.SortedBindings());
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<SecurityBindingDocument>?> UpdateBindingAsync(string id, SecurityBindingDefinition definition, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrEmpty(definition.ClaimType);
        ArgumentNullException.ThrowIfNull(actor);

        lock (this.gate)
        {
            if (!this.bindings.TryGetValue(id, out byte[]? existing))
            {
                return new ValueTask<ParsedJsonDocument<SecurityBindingDocument>?>((ParsedJsonDocument<SecurityBindingDocument>?)null);
            }

            byte[] json = SecurityPolicySerialization.SerializeUpdatedBinding(existing, "binding", id, expectedEtag, definition, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
            this.bindings[id] = json;
            this.generation++;
            return new ValueTask<ParsedJsonDocument<SecurityBindingDocument>?>(PersistedJson.ToPooledDocument<SecurityBindingDocument>(json));
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteBindingAsync(string id, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        lock (this.gate)
        {
            if (!this.bindings.TryGetValue(id, out byte[]? existing))
            {
                return new ValueTask<bool>(false);
            }

            if (!expectedEtag.IsNone)
            {
                using ParsedJsonDocument<SecurityBindingDocument> current = PersistedJson.ToPooledDocument<SecurityBindingDocument>(existing);
                SecurityPolicySerialization.EnsureEtag("binding", id, expectedEtag, current.RootElement.EtagValue);
            }

            this.bindings.Remove(id);
            this.generation++;
            return new ValueTask<bool>(true);
        }
    }

    /// <inheritdoc/>
    public ValueTask<SecurityPolicySnapshot> LoadSnapshotAsync(CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            var ruleDocs = new List<ParsedJsonDocument<SecurityRuleDocument>>(this.rules.Count);
            foreach (byte[] json in this.rules.Values)
            {
                ruleDocs.Add(PersistedJson.ToPooledDocument<SecurityRuleDocument>(json));
            }

            ruleDocs.Sort(static (a, b) => string.CompareOrdinal(a.RootElement.NameValue, b.RootElement.NameValue));
            return new ValueTask<SecurityPolicySnapshot>(new SecurityPolicySnapshot(
                new PooledDocumentList<SecurityRuleDocument>(ruleDocs),
                this.SortedBindings(),
                this.generation));
        }
    }

    private PooledDocumentList<SecurityBindingDocument> SortedBindings()
    {
        var docs = new List<ParsedJsonDocument<SecurityBindingDocument>>(this.bindings.Count);
        foreach (byte[] json in this.bindings.Values)
        {
            docs.Add(PersistedJson.ToPooledDocument<SecurityBindingDocument>(json));
        }

        docs.Sort(static (a, b) =>
        {
            int byOrder = a.RootElement.OrderValue.CompareTo(b.RootElement.OrderValue);
            return byOrder != 0 ? byOrder : string.CompareOrdinal(a.RootElement.IdValue, b.RootElement.IdValue);
        });
        return new PooledDocumentList<SecurityBindingDocument>(docs);
    }

    private WorkflowEtag NextEtag() => new((++this.etagSequence).ToString(CultureInfo.InvariantCulture));
}