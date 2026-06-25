// <copyright file="ISecurityPolicyStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Durable storage for the deployment's row-authorization policy (design §14.2): named <see cref="SecurityRule"/>
/// definitions and the claim→rule <see cref="SecurityBinding"/> mapping. This is the authoring/persistence layer
/// behind the control-plane security API; a <c>PersistentRowSecurityPolicy</c> loads a snapshot and resolves each
/// principal's <see cref="AccessContext"/> from it.
/// </summary>
/// <remarks>
/// <para>Access to this store is gated by the control-plane's <c>security:read</c>/<c>security:write</c> capability
/// scopes (operation authorization), not by row-level reach — so, unlike the run/catalog stores, it takes no
/// <see cref="AccessContext"/>. Update/delete take an expected <see cref="WorkflowEtag"/> for optimistic
/// concurrency (pass <see cref="WorkflowEtag.None"/> to overwrite unconditionally); a stale etag throws
/// <see cref="SecurityPolicyConflictException"/>.</para>
/// <para>Read/return methods hand back <strong>pooled documents whose lifetime the caller owns</strong>: dispose the
/// returned <see cref="ParsedJsonDocument{T}"/> / <see cref="PooledDocumentList{T}"/> / <see cref="SecurityPolicySnapshot"/>
/// once read (clone any value that must outlive the dispose). This keeps the document's backing buffer on the pool
/// rather than the GC heap.</para>
/// </remarks>
public interface ISecurityPolicyStore
{
    /// <summary>Creates a rule. Throws if a rule with the same name already exists.</summary>
    /// <param name="name">The rule's unique name.</param>
    /// <param name="draft">The draft rule carrying the operator-supplied content (expression + optional description) as JSON values; the store stamps the name/etag/created metadata. Build one from an HTTP request body via <c>From</c>, or programmatically via <see cref="SecurityRuleDocument.Draft"/>.</param>
    /// <param name="actor">The authenticated identity creating the rule (for audit).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The created rule, as a pooled document the caller must dispose.</returns>
    ValueTask<ParsedJsonDocument<SecurityRuleDocument>> AddRuleAsync(string name, SecurityRuleDocument draft, string actor, CancellationToken cancellationToken);

    /// <summary>Gets a rule by name, or <see langword="null"/> if absent.</summary>
    /// <param name="name">The rule name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The rule as a pooled document the caller must dispose, or <see langword="null"/>.</returns>
    ValueTask<ParsedJsonDocument<SecurityRuleDocument>?> GetRuleAsync(string name, CancellationToken cancellationToken);

    /// <summary>Lists all rules. The full read used by snapshot/compile paths and by the default keyset pager; the
    /// paged <see cref="ListRulesAsync(int, JsonString, string?, CancellationToken)"/> is the API list seam.</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>All rules, as a pooled batch the caller must dispose.</returns>
    ValueTask<PooledDocumentList<SecurityRuleDocument>> ListRulesAsync(CancellationToken cancellationToken);

    /// <summary>Lists rules as a keyset page (design §14.2): rules ordered by <c>name</c>, bounded to
    /// <paramref name="limit"/>, optionally filtered by <paramref name="q"/> (a case-insensitive substring of the name or
    /// expression), resuming strictly after <paramref name="pageToken"/>. The default implementation pages over
    /// <see cref="ListRulesAsync(CancellationToken)"/> in memory; a backend overrides it with a native keyset query so the
    /// read itself is bounded.</summary>
    /// <param name="limit">The maximum rules to return (a non-positive value uses the store's default page size).</param>
    /// <param name="pageToken">The opaque token (its JSON value) from a previous page's <see cref="SecurityRulePage.NextPageToken"/>, or undefined for the first page; decoded bytes-native from its UTF-8.</param>
    /// <param name="q">An optional case-insensitive substring filter (its JSON value) over the rule name and expression; undefined for no filter.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>One keyset page, as a disposable the caller must dispose.</returns>
    /// <exception cref="FormatException"><paramref name="pageToken"/> is not a valid continuation token.</exception>
    async ValueTask<SecurityRulePage> ListRulesAsync(int limit, JsonString pageToken, JsonString q, CancellationToken cancellationToken)
    {
        using PooledDocumentList<SecurityRuleDocument> all = await this.ListRulesAsync(cancellationToken).ConfigureAwait(false);
        return SecurityRulePaging.PageInMemory(all, limit, pageToken, q);
    }

    /// <summary>Updates a rule's content under optimistic concurrency.</summary>
    /// <param name="name">The rule name.</param>
    /// <param name="draft">The draft rule carrying the new operator-supplied content as JSON values; the store carries the name/created metadata forward and stamps the updated etag/last-updated metadata.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> to overwrite unconditionally).</param>
    /// <param name="actor">The authenticated identity updating the rule (for audit).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The updated rule as a pooled document the caller must dispose, or <see langword="null"/> if no rule with that name exists.</returns>
    /// <exception cref="SecurityPolicyConflictException">The expected etag no longer matches.</exception>
    ValueTask<ParsedJsonDocument<SecurityRuleDocument>?> UpdateRuleAsync(string name, SecurityRuleDocument draft, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken);

    /// <summary>Deletes a rule under optimistic concurrency.</summary>
    /// <param name="name">The rule name.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> to delete unconditionally).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> if a rule was deleted; <see langword="false"/> if none existed.</returns>
    /// <exception cref="SecurityPolicyConflictException">The expected etag no longer matches.</exception>
    ValueTask<bool> DeleteRuleAsync(string name, WorkflowEtag expectedEtag, CancellationToken cancellationToken);

    /// <summary>Creates a binding, assigning it an id.</summary>
    /// <param name="draft">The draft binding carrying the operator-supplied content as JSON values; the store stamps the id/etag/created metadata. Build one programmatically via <see cref="SecurityBindingDocument.Draft"/>.</param>
    /// <param name="actor">The authenticated identity creating the binding (for audit).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The created binding (with its assigned id), as a pooled document the caller must dispose.</returns>
    ValueTask<ParsedJsonDocument<SecurityBindingDocument>> AddBindingAsync(SecurityBindingDocument draft, string actor, CancellationToken cancellationToken);

    /// <summary>Gets a binding by id, or <see langword="null"/> if absent.</summary>
    /// <param name="id">The binding id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The binding as a pooled document the caller must dispose, or <see langword="null"/>.</returns>
    ValueTask<ParsedJsonDocument<SecurityBindingDocument>?> GetBindingAsync(string id, CancellationToken cancellationToken);

    /// <summary>Lists all bindings (ascending by <see cref="SecurityBindingDocument.OrderValue"/> then id). The full read
    /// used by snapshot/compile paths and by the default keyset pager; the paged
    /// <see cref="ListBindingsAsync(int, JsonString, string?, CancellationToken)"/> is the API list seam.</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>All bindings, as a pooled batch the caller must dispose.</returns>
    ValueTask<PooledDocumentList<SecurityBindingDocument>> ListBindingsAsync(CancellationToken cancellationToken);

    /// <summary>Lists bindings as a keyset page (design §14.2): bindings ordered by <c>(order, id)</c>, bounded to
    /// <paramref name="limit"/>, optionally filtered by <paramref name="q"/> (a case-insensitive substring of the claim
    /// type, claim value, or description), resuming strictly after <paramref name="pageToken"/>. The default implementation
    /// pages over <see cref="ListBindingsAsync(CancellationToken)"/> in memory; a backend overrides it with a native keyset
    /// query so the read itself is bounded.</summary>
    /// <param name="limit">The maximum bindings to return (a non-positive value uses the store's default page size).</param>
    /// <param name="pageToken">The opaque token (its JSON value) from a previous page's <see cref="SecurityBindingPage.NextPageToken"/>, or undefined for the first page; decoded bytes-native from its UTF-8.</param>
    /// <param name="q">An optional case-insensitive substring filter (its JSON value) over the binding claim type/value/description; undefined for no filter.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>One keyset page, as a disposable the caller must dispose.</returns>
    /// <exception cref="FormatException"><paramref name="pageToken"/> is not a valid continuation token.</exception>
    async ValueTask<SecurityBindingPage> ListBindingsAsync(int limit, JsonString pageToken, JsonString q, CancellationToken cancellationToken)
    {
        using PooledDocumentList<SecurityBindingDocument> all = await this.ListBindingsAsync(cancellationToken).ConfigureAwait(false);
        return SecurityBindingPaging.PageInMemory(all, limit, pageToken, q);
    }

    /// <summary>Updates a binding's content under optimistic concurrency.</summary>
    /// <param name="id">The binding id.</param>
    /// <param name="draft">The draft binding carrying the new operator-supplied content as JSON values; the store carries the id/created metadata forward and stamps the updated etag/last-updated metadata.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> to overwrite unconditionally).</param>
    /// <param name="actor">The authenticated identity updating the binding (for audit).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The updated binding as a pooled document the caller must dispose, or <see langword="null"/> if no binding with that id exists.</returns>
    /// <exception cref="SecurityPolicyConflictException">The expected etag no longer matches.</exception>
    ValueTask<ParsedJsonDocument<SecurityBindingDocument>?> UpdateBindingAsync(string id, SecurityBindingDocument draft, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken);

    /// <summary>Deletes a binding under optimistic concurrency.</summary>
    /// <param name="id">The binding id.</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> to delete unconditionally).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> if a binding was deleted; <see langword="false"/> if none existed.</returns>
    /// <exception cref="SecurityPolicyConflictException">The expected etag no longer matches.</exception>
    ValueTask<bool> DeleteBindingAsync(string id, WorkflowEtag expectedEtag, CancellationToken cancellationToken);

    /// <summary>Loads a consistent snapshot of all rules and bindings plus the store's current generation token.</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The snapshot (a disposable batch the caller must dispose).</returns>
    ValueTask<SecurityPolicySnapshot> LoadSnapshotAsync(CancellationToken cancellationToken);
}