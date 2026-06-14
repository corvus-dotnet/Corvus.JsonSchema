// <copyright file="InMemorySecurityPolicyStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// The reference in-memory <see cref="ISecurityPolicyStore"/> — the conformance baseline and a ready store for
/// single-node / local-development hosts. Rules are keyed by name, bindings by an assigned id; every mutation
/// bumps a monotonic generation a resolver caches against, and stamps a fresh per-record etag.
/// </summary>
public sealed class InMemorySecurityPolicyStore : ISecurityPolicyStore
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, SecurityRuleDocument> rules = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SecurityBindingDocument> bindings = new(StringComparer.Ordinal);
    private readonly TimeProvider timeProvider;
    private long generation;
    private long etagSequence;
    private long bindingSequence;

    /// <summary>Initializes a new instance of the <see cref="InMemorySecurityPolicyStore"/> class.</summary>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    public InMemorySecurityPolicyStore(TimeProvider? timeProvider = null)
        => this.timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc/>
    public ValueTask<SecurityRuleDocument> AddRuleAsync(string name, SecurityRuleDefinition definition, string actor, CancellationToken cancellationToken)
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

            DateTimeOffset now = this.timeProvider.GetUtcNow();
            var buffer = new ArrayBufferWriter<byte>();
            SecurityRuleDocument.WriteNewRule(buffer, name, definition, actor, now, this.NextEtag());
            SecurityRuleDocument rule = SecurityRuleDocument.FromJson(buffer.WrittenMemory);
            this.rules[name] = rule;
            this.generation++;
            return new ValueTask<SecurityRuleDocument>(rule);
        }
    }

    /// <inheritdoc/>
    public ValueTask<SecurityRuleDocument?> GetRuleAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        lock (this.gate)
        {
            return new ValueTask<SecurityRuleDocument?>(this.rules.TryGetValue(name, out SecurityRuleDocument rule) ? rule : null);
        }
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<SecurityRuleDocument>> ListRulesAsync(CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            var list = new List<SecurityRuleDocument>(this.rules.Values);
            list.Sort(static (a, b) => string.CompareOrdinal(a.NameValue, b.NameValue));
            return new ValueTask<IReadOnlyList<SecurityRuleDocument>>(list);
        }
    }

    /// <inheritdoc/>
    public ValueTask<SecurityRuleDocument?> UpdateRuleAsync(string name, SecurityRuleDefinition definition, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentException.ThrowIfNullOrEmpty(definition.Expression);
        ArgumentNullException.ThrowIfNull(actor);

        lock (this.gate)
        {
            if (!this.rules.TryGetValue(name, out SecurityRuleDocument current))
            {
                return new ValueTask<SecurityRuleDocument?>((SecurityRuleDocument?)null);
            }

            EnsureEtag("rule", name, expectedEtag, current.EtagValue);
            var buffer = new ArrayBufferWriter<byte>();
            current.WriteUpdatedRule(buffer, definition, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
            SecurityRuleDocument updated = SecurityRuleDocument.FromJson(buffer.WrittenMemory);
            this.rules[name] = updated;
            this.generation++;
            return new ValueTask<SecurityRuleDocument?>(updated);
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteRuleAsync(string name, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        lock (this.gate)
        {
            if (!this.rules.TryGetValue(name, out SecurityRuleDocument current))
            {
                return new ValueTask<bool>(false);
            }

            EnsureEtag("rule", name, expectedEtag, current.EtagValue);
            this.rules.Remove(name);
            this.generation++;
            return new ValueTask<bool>(true);
        }
    }

    /// <inheritdoc/>
    public ValueTask<SecurityBindingDocument> AddBindingAsync(SecurityBindingDefinition definition, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(definition.ClaimType);
        ArgumentNullException.ThrowIfNull(actor);

        lock (this.gate)
        {
            string id = "bnd-" + (++this.bindingSequence).ToString(CultureInfo.InvariantCulture);
            var buffer = new ArrayBufferWriter<byte>();
            SecurityBindingDocument.WriteNewBinding(buffer, id, definition, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
            SecurityBindingDocument binding = SecurityBindingDocument.FromJson(buffer.WrittenMemory);
            this.bindings[id] = binding;
            this.generation++;
            return new ValueTask<SecurityBindingDocument>(binding);
        }
    }

    /// <inheritdoc/>
    public ValueTask<SecurityBindingDocument?> GetBindingAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        lock (this.gate)
        {
            return new ValueTask<SecurityBindingDocument?>(this.bindings.TryGetValue(id, out SecurityBindingDocument binding) ? binding : null);
        }
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<SecurityBindingDocument>> ListBindingsAsync(CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            return new ValueTask<IReadOnlyList<SecurityBindingDocument>>(SortBindings(this.bindings.Values));
        }
    }

    /// <inheritdoc/>
    public ValueTask<SecurityBindingDocument?> UpdateBindingAsync(string id, SecurityBindingDefinition definition, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrEmpty(definition.ClaimType);
        ArgumentNullException.ThrowIfNull(actor);

        lock (this.gate)
        {
            if (!this.bindings.TryGetValue(id, out SecurityBindingDocument current))
            {
                return new ValueTask<SecurityBindingDocument?>((SecurityBindingDocument?)null);
            }

            EnsureEtag("binding", id, expectedEtag, current.EtagValue);
            var buffer = new ArrayBufferWriter<byte>();
            current.WriteUpdatedBinding(buffer, definition, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
            SecurityBindingDocument updated = SecurityBindingDocument.FromJson(buffer.WrittenMemory);
            this.bindings[id] = updated;
            this.generation++;
            return new ValueTask<SecurityBindingDocument?>(updated);
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteBindingAsync(string id, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        lock (this.gate)
        {
            if (!this.bindings.TryGetValue(id, out SecurityBindingDocument current))
            {
                return new ValueTask<bool>(false);
            }

            EnsureEtag("binding", id, expectedEtag, current.EtagValue);
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
            var ruleList = new List<SecurityRuleDocument>(this.rules.Values);
            ruleList.Sort(static (a, b) => string.CompareOrdinal(a.NameValue, b.NameValue));
            return new ValueTask<SecurityPolicySnapshot>(new SecurityPolicySnapshot(ruleList, SortBindings(this.bindings.Values), this.generation));
        }
    }

    private static List<SecurityBindingDocument> SortBindings(IEnumerable<SecurityBindingDocument> source)
    {
        var list = new List<SecurityBindingDocument>(source);
        list.Sort(static (a, b) =>
        {
            int byOrder = a.OrderValue.CompareTo(b.OrderValue);
            return byOrder != 0 ? byOrder : string.CompareOrdinal(a.IdValue, b.IdValue);
        });
        return list;
    }

    private static void EnsureEtag(string kind, string id, WorkflowEtag expected, WorkflowEtag actual)
    {
        if (!expected.IsNone && expected != actual)
        {
            throw new SecurityPolicyConflictException(kind, id, expected);
        }
    }

    private WorkflowEtag NextEtag() => new((++this.etagSequence).ToString(CultureInfo.InvariantCulture));
}