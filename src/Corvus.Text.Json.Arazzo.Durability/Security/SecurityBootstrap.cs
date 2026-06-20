// <copyright file="SecurityBootstrap.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Seeds the common, ready-to-use bootstrap rules (design §14.2) into an <see cref="ISecurityPolicyStore"/> so the
/// row-security model is usable from the start. These are ordinary, editable rules — a deployment uses them as-is,
/// edits them, or removes them via the security API; they are not hard-coded behaviour. Seeding is idempotent:
/// only rules whose names are absent are added (existing edits are preserved).
/// </summary>
public static class SecurityBootstrap
{
    /// <summary>The tenant-scoped bootstrap rule name: the row's <c>tenant</c> label must match the principal's <c>tenant</c> claim.</summary>
    public const string TenantScopedRuleName = "tenant-scoped";

    /// <summary>The ABAC label-superset bootstrap rule name: the principal must hold every label the row carries.</summary>
    public const string AbacSupersetRuleName = "abac-superset";

    /// <summary>The intersection bootstrap rule name: the principal shares at least one label with the row.</summary>
    public const string IntersectionRuleName = "intersection";

    /// <summary>The bootstrap rules as (name, expression, description) tuples.</summary>
    public static IReadOnlyList<(string Name, string Expression, string Description)> Rules { get; } =
    [
        (TenantScopedRuleName, "tenant == $claim.tenant", "Tenant isolation: the row's tenant label must match the principal's tenant claim."),
        (AbacSupersetRuleName, "$claims.superset", "ABAC clearance: the principal must hold every label the row carries."),
        (IntersectionRuleName, "$claims.intersects", "The principal shares at least one label with the row."),
    ];

    /// <summary>Seeds any missing bootstrap rules into the store (idempotent).</summary>
    /// <param name="store">The store to seed.</param>
    /// <param name="actor">The audit actor recorded as the rules' creator (e.g. <c>"bootstrap"</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The names of the rules that were created (empty if all already existed).</returns>
    public static async ValueTask<IReadOnlyList<string>> SeedAsync(ISecurityPolicyStore store, string actor = "bootstrap", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        var seeded = new List<string>();
        foreach ((string name, string expression, string description) in Rules)
        {
            ParsedJsonDocument<SecurityRuleDocument>? existing = await store.GetRuleAsync(name, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                existing.Dispose();
                continue;
            }

            try
            {
                // The created document is not needed here; dispose it so its pooled buffer is returned. The draft is a
                // pooled document too (the store reads it synchronously) — dispose it once the create completes.
                using ParsedJsonDocument<SecurityRuleDocument> draft = SecurityRuleDocument.Draft(expression, description);
                (await store.AddRuleAsync(name, draft.RootElement, actor, cancellationToken).ConfigureAwait(false)).Dispose();
                seeded.Add(name);
            }
            catch (InvalidOperationException)
            {
                // A concurrent seeder created it between the check and the add — fine, it exists.
            }
        }

        return seeded;
    }
}