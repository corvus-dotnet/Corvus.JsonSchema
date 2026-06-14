// <copyright file="ISecurityPolicyStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Durable storage for the deployment's row-authorization policy (design §14.2): named <see cref="SecurityRule"/>
/// definitions and the claim→rule <see cref="SecurityBinding"/> mapping. This is the authoring/persistence layer
/// behind the control-plane security API; a <c>PersistentRowSecurityPolicy</c> loads a snapshot and resolves each
/// principal's <see cref="AccessContext"/> from it.
/// </summary>
/// <remarks>
/// Access to this store is gated by the control-plane's <c>security:read</c>/<c>security:write</c> capability
/// scopes (operation authorization), not by row-level reach — so, unlike the run/catalog stores, it takes no
/// <see cref="AccessContext"/>. Update/delete take an expected <see cref="WorkflowEtag"/> for optimistic
/// concurrency (pass <see cref="WorkflowEtag.None"/> to overwrite unconditionally); a stale etag throws
/// <see cref="SecurityPolicyConflictException"/>.
/// </remarks>
public interface ISecurityPolicyStore
{
    /// <summary>Creates a rule. Throws if a rule with the same name already exists.</summary>
    /// <param name="name">The rule's unique name.</param>
    /// <param name="definition">The rule content.</param>
    /// <param name="actor">The authenticated identity creating the rule (for audit).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The created rule.</returns>
    ValueTask<SecurityRuleDocument> AddRuleAsync(string name, SecurityRuleDefinition definition, string actor, CancellationToken cancellationToken);

    /// <summary>Gets a rule by name, or <see langword="null"/> if absent.</summary>
    /// <param name="name">The rule name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The record or <see langword="null"/>.</returns>
    ValueTask<SecurityRuleDocument?> GetRuleAsync(string name, CancellationToken cancellationToken);

    /// <summary>Lists all rules.</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>All rules.</returns>
    ValueTask<IReadOnlyList<SecurityRuleDocument>> ListRulesAsync(CancellationToken cancellationToken);

    /// <summary>Updates a rule's content under optimistic concurrency.</summary>
    /// <param name="name">The rule name.</param>
    /// <param name="definition">The new content.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> to overwrite unconditionally).</param>
    /// <param name="actor">The authenticated identity updating the rule (for audit).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The updated record, or <see langword="null"/> if no rule with that name exists.</returns>
    /// <exception cref="SecurityPolicyConflictException">The expected etag no longer matches.</exception>
    ValueTask<SecurityRuleDocument?> UpdateRuleAsync(string name, SecurityRuleDefinition definition, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken);

    /// <summary>Deletes a rule under optimistic concurrency.</summary>
    /// <param name="name">The rule name.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> to delete unconditionally).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> if a rule was deleted; <see langword="false"/> if none existed.</returns>
    /// <exception cref="SecurityPolicyConflictException">The expected etag no longer matches.</exception>
    ValueTask<bool> DeleteRuleAsync(string name, WorkflowEtag expectedEtag, CancellationToken cancellationToken);

    /// <summary>Creates a binding, assigning it an id.</summary>
    /// <param name="definition">The binding content.</param>
    /// <param name="actor">The authenticated identity creating the binding (for audit).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The created record (with its assigned id).</returns>
    ValueTask<SecurityBindingDocument> AddBindingAsync(SecurityBindingDefinition definition, string actor, CancellationToken cancellationToken);

    /// <summary>Gets a binding by id, or <see langword="null"/> if absent.</summary>
    /// <param name="id">The binding id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The record or <see langword="null"/>.</returns>
    ValueTask<SecurityBindingDocument?> GetBindingAsync(string id, CancellationToken cancellationToken);

    /// <summary>Lists all bindings (ascending by <see cref="SecurityBindingDocument.OrderValue"/> then id).</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>All bindings.</returns>
    ValueTask<IReadOnlyList<SecurityBindingDocument>> ListBindingsAsync(CancellationToken cancellationToken);

    /// <summary>Updates a binding's content under optimistic concurrency.</summary>
    /// <param name="id">The binding id.</param>
    /// <param name="definition">The new content.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> to overwrite unconditionally).</param>
    /// <param name="actor">The authenticated identity updating the binding (for audit).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The updated record, or <see langword="null"/> if no binding with that id exists.</returns>
    /// <exception cref="SecurityPolicyConflictException">The expected etag no longer matches.</exception>
    ValueTask<SecurityBindingDocument?> UpdateBindingAsync(string id, SecurityBindingDefinition definition, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken);

    /// <summary>Deletes a binding under optimistic concurrency.</summary>
    /// <param name="id">The binding id.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> to delete unconditionally).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> if a binding was deleted; <see langword="false"/> if none existed.</returns>
    /// <exception cref="SecurityPolicyConflictException">The expected etag no longer matches.</exception>
    ValueTask<bool> DeleteBindingAsync(string id, WorkflowEtag expectedEtag, CancellationToken cancellationToken);

    /// <summary>Loads a consistent snapshot of all rules and bindings plus the store's current generation token.</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The snapshot.</returns>
    ValueTask<SecurityPolicySnapshot> LoadSnapshotAsync(CancellationToken cancellationToken);
}