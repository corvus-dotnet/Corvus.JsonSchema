// <copyright file="SourceCredentialStoreExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Programmatic convenience over <see cref="ISourceCredentialStore"/> for callers that hold a
/// <see cref="SourceCredentialDefinition"/> value rather than a parsed request body (tests, seeding, CLI/demo). Each
/// method builds a pooled draft via <see cref="SourceCredentialBinding.Draft(SourceCredentialDefinition)"/> and carries
/// it through the store's draft seam, so there is a single bytes-to-bytes write path: the warm HTTP handler builds the
/// draft straight from the parsed body, and value-holding callers get the same path with the draft built for them.
/// </summary>
public static class SourceCredentialStoreExtensions
{
    /// <summary>Creates a binding from a <see cref="SourceCredentialDefinition"/> value (builds a pooled draft and carries
    /// it through <see cref="ISourceCredentialStore.AddAsync(SourceCredentialBinding, string, CancellationToken)"/>).</summary>
    /// <param name="store">The store.</param>
    /// <param name="definition">The binding content (references + non-secret metadata + security tags).</param>
    /// <param name="actor">The authenticated identity creating the binding (for audit).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The created binding, as a pooled document the caller must dispose.</returns>
    public static async ValueTask<ParsedJsonDocument<SourceCredentialBinding>> AddAsync(this ISourceCredentialStore store, SourceCredentialDefinition definition, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        using ParsedJsonDocument<SourceCredentialBinding> draft = SourceCredentialBinding.Draft(definition);
        return await store.AddAsync(draft.RootElement, actor, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Updates a binding from a <see cref="SourceCredentialDefinition"/> value (builds a pooled draft and carries
    /// it through <see cref="ISourceCredentialStore.UpdateAsync(string, string, SourceCredentialBinding, WorkflowEtag, string, AccessContext, CancellationToken)"/>).</summary>
    /// <param name="store">The store.</param>
    /// <param name="sourceName">The Arazzo source description name.</param>
    /// <param name="environment">The deployment environment.</param>
    /// <param name="definition">The new content (its identity and security tags are ignored — those are immutable).</param>
    /// <param name="expectedEtag">The expected current etag (<see cref="WorkflowEtag.None"/> to overwrite unconditionally).</param>
    /// <param name="actor">The authenticated identity updating the binding (for audit).</param>
    /// <param name="context">The caller's row-access grant (use <see cref="AccessContext.System"/> for full reach).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The updated binding as a pooled document the caller must dispose, or <see langword="null"/> if no writable binding exists.</returns>
    public static async ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> UpdateAsync(this ISourceCredentialStore store, string sourceName, string environment, SourceCredentialDefinition definition, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        using ParsedJsonDocument<SourceCredentialBinding> draft = SourceCredentialBinding.Draft(definition);
        return await store.UpdateAsync(sourceName, environment, draft.RootElement, expectedEtag, actor, context, cancellationToken).ConfigureAwait(false);
    }
}