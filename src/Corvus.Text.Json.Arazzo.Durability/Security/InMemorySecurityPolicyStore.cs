// <copyright file="InMemorySecurityPolicyStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

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
    private readonly Dictionary<string, SecurityBinding> bindings = new(StringComparer.Ordinal);
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
            SecurityRuleDocument rule = SecurityRuleDocument.CreateRule(name, definition, actor, now, this.NextEtag());
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
            SecurityRuleDocument updated = current.WithUpdate(definition, actor, this.timeProvider.GetUtcNow(), this.NextEtag());
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
    public ValueTask<SecurityBinding> AddBindingAsync(SecurityBindingDefinition definition, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(definition.ClaimType);
        ArgumentNullException.ThrowIfNull(actor);

        lock (this.gate)
        {
            string id = "bnd-" + (++this.bindingSequence).ToString(CultureInfo.InvariantCulture);
            DateTimeOffset now = this.timeProvider.GetUtcNow();
            var record = new SecurityBinding(
                id,
                definition.ClaimType,
                definition.ClaimValue,
                definition.Read,
                definition.Write,
                definition.Purge,
                definition.Order,
                definition.Description,
                actor,
                now,
                null,
                null,
                this.NextEtag());
            this.bindings[id] = record;
            this.generation++;
            return new ValueTask<SecurityBinding>(record);
        }
    }

    /// <inheritdoc/>
    public ValueTask<SecurityBinding?> GetBindingAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        lock (this.gate)
        {
            return new ValueTask<SecurityBinding?>(this.bindings.TryGetValue(id, out SecurityBinding record) ? record : null);
        }
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<SecurityBinding>> ListBindingsAsync(CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            return new ValueTask<IReadOnlyList<SecurityBinding>>(SortBindings(this.bindings.Values));
        }
    }

    /// <inheritdoc/>
    public ValueTask<SecurityBinding?> UpdateBindingAsync(string id, SecurityBindingDefinition definition, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrEmpty(definition.ClaimType);
        ArgumentNullException.ThrowIfNull(actor);

        lock (this.gate)
        {
            if (!this.bindings.TryGetValue(id, out SecurityBinding current))
            {
                return new ValueTask<SecurityBinding?>((SecurityBinding?)null);
            }

            EnsureEtag("binding", id, expectedEtag, current.Etag);
            var updated = current with
            {
                ClaimType = definition.ClaimType,
                ClaimValue = definition.ClaimValue,
                Read = definition.Read,
                Write = definition.Write,
                Purge = definition.Purge,
                Order = definition.Order,
                Description = definition.Description,
                UpdatedBy = actor,
                UpdatedAt = this.timeProvider.GetUtcNow(),
                Etag = this.NextEtag(),
            };
            this.bindings[id] = updated;
            this.generation++;
            return new ValueTask<SecurityBinding?>(updated);
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteBindingAsync(string id, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        lock (this.gate)
        {
            if (!this.bindings.TryGetValue(id, out SecurityBinding current))
            {
                return new ValueTask<bool>(false);
            }

            EnsureEtag("binding", id, expectedEtag, current.Etag);
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

    private static List<SecurityBinding> SortBindings(IEnumerable<SecurityBinding> source)
    {
        var list = new List<SecurityBinding>(source);
        list.Sort(static (a, b) =>
        {
            int byOrder = a.Order.CompareTo(b.Order);
            return byOrder != 0 ? byOrder : string.CompareOrdinal(a.Id, b.Id);
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