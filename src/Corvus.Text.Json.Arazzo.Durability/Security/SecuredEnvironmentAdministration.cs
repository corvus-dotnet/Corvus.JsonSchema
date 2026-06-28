// <copyright file="SecuredEnvironmentAdministration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using AdministratorIdentity = Corvus.Text.Json.Arazzo.Durability.Security.EnvironmentAdministrators.AdministratorIdentity;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// The governance service for a deployment environment's administrator set (design §7.7), over an
/// <see cref="IEnvironmentAdministratorStore"/>. Mirrors the §15 workflow-administration logic in
/// <c>SecuredWorkflowCatalog</c> — but environments have no version-1 fallback: administration is materialized eagerly
/// when the environment is created (<see cref="EstablishAsync"/>, "creating one grants the creator administration"), and
/// add / transfer / remove mutate it under optimistic concurrency.
/// </summary>
/// <remarks>
/// <para>Every mutation is gated by <em>current-administrator membership</em> (the caller's stamped identity must be one
/// of the set, by order-independent tag set-equality) — never reach. An unknown environment and a non-administrator are
/// refused identically (<see cref="EnvironmentAdministrationException"/> → 403, non-disclosing). The last administrator
/// cannot be removed (→ 409). A concurrent change that loses the CAS race is retried a bounded number of times.</para>
/// </remarks>
public sealed class SecuredEnvironmentAdministration
{
    private const int AdministrationMutationRetries = 3;

    private readonly IEnvironmentAdministratorStore store;
    private readonly string actor;

    /// <summary>Initializes a new instance of the <see cref="SecuredEnvironmentAdministration"/> class.</summary>
    /// <param name="store">The environment administrator store this service governs.</param>
    /// <param name="actor">The audit actor recorded on writes.</param>
    public SecuredEnvironmentAdministration(IEnvironmentAdministratorStore store, string actor = "control-plane")
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(actor);
        this.store = store;
        this.actor = actor;
    }

    /// <summary>Gets the administration record for an environment, or <see langword="null"/> if none exists.</summary>
    /// <param name="environmentName">The environment name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The record as a pooled document the caller disposes, or <see langword="null"/>.</returns>
    public ValueTask<ParsedJsonDocument<EnvironmentAdministrators>?> GetAdministratorsAsync(string environmentName, CancellationToken cancellationToken)
        => this.store.GetAsync(environmentName, cancellationToken);

    /// <summary>Removes an environment's administration record entirely (when the environment is deleted). A missing record
    /// is a no-op.</summary>
    /// <param name="environmentName">The environment name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the record is gone.</returns>
    public ValueTask DeleteRecordAsync(string environmentName, CancellationToken cancellationToken)
        => this.store.DeleteAsync(environmentName, cancellationToken);

    /// <summary>Materializes the initial administration record for a freshly-created environment (§7.7): the creator's
    /// resolved identity becomes the sole, removable administrator. Idempotent — a concurrent establish (None-etag race)
    /// is harmless. The identity is built in an unrented workspace held across the <c>PutAsync</c> await.</summary>
    /// <param name="environmentName">The environment name.</param>
    /// <param name="ownerIdentity">The creating principal's resolved internal identity.</param>
    /// <param name="kind">The resolved grantee kind (written when <paramref name="hasKind"/>).</param>
    /// <param name="hasKind">Whether <paramref name="kind"/> is present.</param>
    /// <param name="label">The display label (written when <paramref name="hasLabel"/>).</param>
    /// <param name="hasLabel">Whether <paramref name="label"/> is present.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once administration is established (or already was).</returns>
    public async ValueTask EstablishAsync(string environmentName, SecurityTagSet ownerIdentity, AdministratorIdentity.KindEntity kind, bool hasKind, JsonString label, bool hasLabel, CancellationToken cancellationToken)
    {
        using JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
        AdministratorIdentity owner = EnvironmentAdministrators.BuildIdentity(workspace, ownerIdentity, kind, hasKind, label, hasLabel);
        try
        {
            (await this.store.PutAsync(environmentName, [owner], WorkflowEtag.None, this.actor, cancellationToken).ConfigureAwait(false)).Dispose();
        }
        catch (EnvironmentAdministrationConflictException)
        {
            // Already established (idempotent re-create / concurrent establish) — administration exists, nothing to do.
        }
    }

    /// <summary>Adds an administrator identity to the environment's set (idempotent), gated by current-administrator
    /// membership.</summary>
    /// <param name="environmentName">The environment name.</param>
    /// <param name="identity">The resolved administrator identity to add.</param>
    /// <param name="kind">The resolved grantee kind (written when <paramref name="hasKind"/>).</param>
    /// <param name="hasKind">Whether <paramref name="kind"/> is present.</param>
    /// <param name="label">The display label (written when <paramref name="hasLabel"/>).</param>
    /// <param name="hasLabel">Whether <paramref name="label"/> is present.</param>
    /// <param name="callerIdentity">The caller's resolved internal identity (the current-administrator gate).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The resulting administration record as a pooled document the caller disposes.</returns>
    public async ValueTask<ParsedJsonDocument<EnvironmentAdministrators>> AddAdministratorAsync(string environmentName, SecurityTagSet identity, AdministratorIdentity.KindEntity kind, bool hasKind, JsonString label, bool hasLabel, SecurityTagSet callerIdentity, CancellationToken cancellationToken)
    {
        using JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
        AdministratorIdentity newAdministrator = EnvironmentAdministrators.BuildIdentity(workspace, identity, kind, hasKind, label, hasLabel);
        return await this.MutateAsync(
            environmentName,
            callerIdentity,
            static (admins, newAdministrator) =>
            {
                if (IsMember(admins, TagsOf(newAdministrator)))
                {
                    return null; // already an administrator — idempotent no-op
                }

                admins.Add(newAdministrator);
                return admins;
            },
            newAdministrator,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Removes the administrator identified by its identity digest (idempotent for an unknown digest), gated by
    /// current-administrator membership. Removing the last administrator is refused.</summary>
    /// <param name="environmentName">The environment name.</param>
    /// <param name="digest">The administrator identity digest (the removal key).</param>
    /// <param name="callerIdentity">The caller's resolved internal identity (the current-administrator gate).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The resulting administration record as a pooled document the caller disposes.</returns>
    /// <exception cref="ArgumentException">Removing the last administrator.</exception>
    public ValueTask<ParsedJsonDocument<EnvironmentAdministrators>> RemoveAdministratorAsync(string environmentName, string digest, SecurityTagSet callerIdentity, CancellationToken cancellationToken)
        => this.MutateAsync(
            environmentName,
            callerIdentity,
            static (admins, digest) =>
            {
                int index = IndexOfDigest(admins, digest);
                if (index < 0)
                {
                    return null; // no administrator with that digest — idempotent no-op
                }

                if (admins.Count == 1)
                {
                    throw new ArgumentException("Cannot remove the last administrator of an environment; an environment must always have at least one administrator.", nameof(digest));
                }

                admins.RemoveAt(index);
                return admins;
            },
            digest,
            cancellationToken);

    /// <summary>Replaces the entire administrator set (transfer), gated by current-administrator membership. Duplicates are
    /// coalesced; an administrator may transfer administration away from itself.</summary>
    /// <param name="environmentName">The environment name.</param>
    /// <param name="newAdministrators">The replacement administrator identities (at least one).</param>
    /// <param name="callerIdentity">The caller's resolved internal identity (the current-administrator gate).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The resulting administration record as a pooled document the caller disposes.</returns>
    public async ValueTask<ParsedJsonDocument<EnvironmentAdministrators>> TransferAdministrationAsync(string environmentName, IReadOnlyList<SecurityTagSet> newAdministrators, SecurityTagSet callerIdentity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(newAdministrators);
        if (newAdministrators.Count == 0)
        {
            throw new ArgumentException("An environment administration transfer requires at least one new administrator.", nameof(newAdministrators));
        }

        using JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
        var identities = new List<AdministratorIdentity>(newAdministrators.Count);
        foreach (SecurityTagSet tags in newAdministrators)
        {
            identities.Add(EnvironmentAdministrators.BuildIdentity(workspace, tags, default, hasKind: false, default, hasLabel: false));
        }

        List<AdministratorIdentity> deduped = Dedupe(identities);
        return await this.MutateAsync(
            environmentName,
            callerIdentity,
            static (_, replacement) => replacement,
            deduped,
            cancellationToken).ConfigureAwait(false);
    }

    // Whether a candidate identity is a member of a set (order-independent set equality on any entry's tags).
    private static bool IsMember(List<AdministratorIdentity> admins, SecurityTagSet candidate)
    {
        foreach (AdministratorIdentity administrator in admins)
        {
            if (WorkflowIdentity.SameAdministrator(TagsOf(administrator), candidate))
            {
                return true;
            }
        }

        return false;
    }

    // The index of the administrator whose identity digest equals the (hex, ASCII) target, or -1 if none matches.
    private static int IndexOfDigest(List<AdministratorIdentity> admins, string digest)
    {
        if (digest.Length != SecurityIdentityDigest.DigestUtf8Length)
        {
            return -1;
        }

        Span<byte> target = stackalloc byte[SecurityIdentityDigest.DigestUtf8Length];
        int targetLength = Encoding.ASCII.GetBytes(digest, target);
        Span<byte> buffer = stackalloc byte[SecurityIdentityDigest.DigestUtf8Length];
        for (int i = 0; i < admins.Count; i++)
        {
            int written = SecurityIdentityDigest.FormatUtf8(TagsOf(admins[i]), buffer);
            if (written == targetLength && buffer[..written].SequenceEqual(target[..targetLength]))
            {
                return i;
            }
        }

        return -1;
    }

    // A non-owning view of an administrator identity's tags (the raw {key,value} array UTF-8), valid while the backing
    // document/workspace is alive — no per-identity copy.
    private static SecurityTagSet TagsOf(in AdministratorIdentity administrator)
        => SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(administrator.Tags).Memory);

    // Coalesces an administrator list, dropping set-equal duplicates so the persisted set carries each identity once.
    private static List<AdministratorIdentity> Dedupe(IReadOnlyList<AdministratorIdentity> admins)
    {
        var deduped = new List<AdministratorIdentity>(admins.Count);
        foreach (AdministratorIdentity administrator in admins)
        {
            if (!IsMember(deduped, TagsOf(administrator)))
            {
                deduped.Add(administrator);
            }
        }

        return deduped;
    }

    // The read-modify-write core for the administration mutations (§7.7): load the current administrators, authorize the
    // caller as a current administrator, apply the mutation, and persist under optimistic concurrency — retrying a bounded
    // number of times if a concurrent change wins the CAS race. A mutation returning null is an idempotent no-op (returns
    // the current record unchanged). The existing identities carry forward bytes-to-bytes (referencing the loaded record,
    // held alive across the write).
    private async ValueTask<ParsedJsonDocument<EnvironmentAdministrators>> MutateAsync<TArg>(string environmentName, SecurityTagSet callerIdentity, Func<List<AdministratorIdentity>, TArg, List<AdministratorIdentity>?> mutate, TArg argument, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environmentName);
        for (int attempt = 0; ; attempt++)
        {
            ParsedJsonDocument<EnvironmentAdministrators>? record = await this.store.GetAsync(environmentName, cancellationToken).ConfigureAwait(false);
            bool handedOffRecord = false;
            try
            {
                // No record (unknown environment or unestablished) or a caller who is not a current administrator is
                // refused identically (non-disclosing).
                if (record is null)
                {
                    throw new EnvironmentAdministrationException(environmentName);
                }

                EnvironmentAdministrators current = record.RootElement;
                var admins = new List<AdministratorIdentity>(current.AdministratorCount);
                if (current.Administrators.IsNotUndefined())
                {
                    admins.AddRange(current.Administrators.EnumerateArray());
                }

                if (admins.Count == 0 || !IsMember(admins, callerIdentity))
                {
                    throw new EnvironmentAdministrationException(environmentName);
                }

                List<AdministratorIdentity>? next = mutate(admins, argument);
                if (next is null)
                {
                    handedOffRecord = true; // unchanged — return the loaded record as-is
                    return record;
                }

                try
                {
                    return await this.store.PutAsync(environmentName, next, current.EtagValue, this.actor, cancellationToken).ConfigureAwait(false);
                }
                catch (EnvironmentAdministrationConflictException) when (attempt < AdministrationMutationRetries)
                {
                    // A concurrent administration change rotated the etag; reload and retry.
                }
            }
            finally
            {
                if (!handedOffRecord)
                {
                    record?.Dispose();
                }
            }
        }
    }
}