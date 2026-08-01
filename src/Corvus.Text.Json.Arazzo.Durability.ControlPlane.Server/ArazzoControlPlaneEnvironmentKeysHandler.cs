// <copyright file="ArazzoControlPlaneEnvironmentKeysHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Extensions.Logging;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Serves an environment's checkpoint key registrations (ADR 0065), under the environment's own administrators and
/// the <c>environments:read</c>/<c>environments:write</c> capability scopes.
/// </summary>
/// <remarks>
/// <para>
/// Generations live on the environment record rather than in a store of their own. The tenancy invariant asks whether
/// every tenant-owned environment has an active generation, which is then one scan instead of a scan plus a lookup per
/// environment, and it costs no new backend implementation across the nine durable stores.
/// </para>
/// <para>
/// Registration is self-authenticating: the request carries a signature over a framed tuple, made with the private
/// half of the seal key it presents. That proves the registrant controls the pair. It does not prove the symmetric
/// payload key exists, which cannot be proved to a party that must never hold it, and is instead enforced at runtime
/// by a runner faulting rather than writing cleartext. What it removes is the weaker failure, where "this environment
/// has a registered key" would be satisfiable by typing a string.
/// </para>
/// </remarks>
internal sealed class ArazzoControlPlaneEnvironmentKeysHandler : IApiEnvironmentKeysHandler
{
    private const string TargetKind = "environment-key";
    private const string ProblemBase = "https://corvus-oss.org/arazzo/control-plane/problems/";

    private readonly IEnvironmentStore environments;
    private readonly SecuredEnvironmentAdministration administration;
    private readonly ControlPlaneAccess access;
    private readonly TimeProvider timeProvider;
    private readonly string subjectClaimType;
    private readonly ILogger? auditLogger;

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneEnvironmentKeysHandler"/> class.</summary>
    /// <param name="environments">The environment store.</param>
    /// <param name="administration">The environment administration set.</param>
    /// <param name="access">The caller's access context.</param>
    /// <param name="timeProvider">The time source for registration timestamps and the freshness check.</param>
    /// <param name="subjectClaimType">The claim type identifying the deciding subject (the recorded actor); default <c>sub</c>.</param>
    /// <param name="auditLogger">The governance audit sink, if any.</param>
    internal ArazzoControlPlaneEnvironmentKeysHandler(
        IEnvironmentStore environments,
        SecuredEnvironmentAdministration administration,
        ControlPlaneAccess access,
        TimeProvider? timeProvider = null,
        string subjectClaimType = "sub",
        ILogger? auditLogger = null)
    {
        ArgumentNullException.ThrowIfNull(environments);
        ArgumentNullException.ThrowIfNull(administration);
        ArgumentNullException.ThrowIfNull(access);
        this.environments = environments;
        this.administration = administration;
        this.access = access;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.subjectClaimType = subjectClaimType;
        this.auditLogger = auditLogger;
    }

    /// <inheritdoc/>
    public async ValueTask<ListEnvironmentKeysResult> HandleListEnvironmentKeysAsync(ListEnvironmentKeysParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string environment = (string)parameters.Name;

        ParsedJsonDocument<Environment>? stored = await this.environments.GetAsync(environment, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return ListEnvironmentKeysResult.NotFound(EnvironmentNotFoundProblem(environment), workspace);
        }

        // The views are built lazily over these bytes, so the workspace owns the document until the response is
        // written. Disposing here would leave the response reading freed memory.
        workspace.TakeOwnership(stored);

        string? stateFilter = ((JsonElement)parameters.State).ValueKind == JsonValueKind.String ? (string)parameters.State : null;
        List<Environment.EnvironmentKeyGeneration> generations = Matching(stored.RootElement, stateFilter);

        return ListEnvironmentKeysResult.Ok(
            new Models.EnvironmentKeyList.Source((ref Models.EnvironmentKeyList.Builder b) => b.Create(
                keys: new Models.EnvironmentKeyList.EnvironmentKeyViewArray.Source((ref Models.EnvironmentKeyList.EnvironmentKeyViewArray.Builder array) =>
                {
                    foreach (Environment.EnvironmentKeyGeneration generation in generations)
                    {
                        array.AddItem(Models.EnvironmentKeyView.From(generation));
                    }
                }))),
            workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<RegisterEnvironmentKeyResult> HandleRegisterEnvironmentKeyAsync(RegisterEnvironmentKeyParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string environment = (string)parameters.Name;
        string keyId = (string)parameters.Body.KeyId;

        GovernanceGate gate = await this.AuthorizeEnvironmentAdminAsync(environment, cancellationToken).ConfigureAwait(false);
        if (gate == GovernanceGate.NotFound)
        {
            return RegisterEnvironmentKeyResult.NotFound(EnvironmentNotFoundProblem(environment), workspace);
        }

        if (gate != GovernanceGate.Authorized)
        {
            GovernanceAudit.Mutation(this.auditLogger, "environment.key.register", this.CallerActor(), TargetKind, KeyKey(environment, keyId), "refused-not-administrator");
            return RegisterEnvironmentKeyResult.Forbidden(NotAdministratorProblem(environment), workspace);
        }

        string sealPublicKeyBase64 = (string)parameters.Body.SealPublicKey;
        if (!TryDecode(sealPublicKeyBase64, out byte[] sealPublicKey) || !TryDecode((string)parameters.Body.Signature, out byte[] signature))
        {
            // Malformed base64 is the caller's own input, so this is a refusal rather than an exception surfacing
            // from the crypto layer as a 500.
            GovernanceAudit.Mutation(this.auditLogger, "environment.key.register", this.CallerActor(), TargetKind, KeyKey(environment, keyId), "refused-KeyUnreadable");
            return RegisterEnvironmentKeyResult.BadRequest(PossessionProblem(environment, keyId, EnvironmentKeyPossessionResult.KeyUnreadable), workspace);
        }

        DateTimeOffset notBefore = ((NodaTime.OffsetDateTime)parameters.Body.NotBefore).ToDateTimeOffset();

        EnvironmentKeyPossessionResult possession = EnvironmentKeyPossession.Verify(
            environment,
            keyId,
            (string)parameters.Body.Algorithm,
            sealPublicKey,
            notBefore,
            signature,
            this.timeProvider.GetUtcNow());

        if (possession != EnvironmentKeyPossessionResult.Verified)
        {
            // The refusal reason is diagnostic, not disclosing: every value here is one the caller supplied, so
            // naming which check failed tells them nothing they did not already know.
            GovernanceAudit.Mutation(this.auditLogger, "environment.key.register", this.CallerActor(), TargetKind, KeyKey(environment, keyId), $"refused-{possession}");
            return RegisterEnvironmentKeyResult.BadRequest(PossessionProblem(environment, keyId, possession), workspace);
        }

        ParsedJsonDocument<Environment>? stored = await this.environments.GetAsync(environment, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return RegisterEnvironmentKeyResult.NotFound(EnvironmentNotFoundProblem(environment), workspace);
        }

        // Replay is deliberately not an error. The signed tuple determines the effect, so re-presenting it names the
        // generation that already exists, and returning it keeps registration idempotent without a server nonce store.
        if (Find(stored.RootElement, keyId) is { } existing)
        {
            workspace.TakeOwnership(stored);
            return RegisterEnvironmentKeyResult.Ok(Models.EnvironmentKeyView.From(existing), workspace);
        }

        string actor = this.CallerActor();
        using ParsedJsonDocument<Environment> draft = Environment.DraftWithKeyRegistered(
            stored.RootElement, keyId, sealPublicKeyBase64, (string)parameters.Body.Algorithm, actor, this.timeProvider.GetUtcNow());

        ParsedJsonDocument<Environment>? updated = await this.environments.UpdateAsync(
            environment, draft.RootElement, stored.RootElement.EtagValue, actor, this.access.Current(), cancellationToken).ConfigureAwait(false);
        stored.Dispose();
        if (updated is null)
        {
            return RegisterEnvironmentKeyResult.Conflict(ConcurrentWriteProblem(environment, keyId), workspace);
        }

        GovernanceAudit.Mutation(this.auditLogger, "environment.key.register", actor, TargetKind, KeyKey(environment, keyId), "registered");
        workspace.TakeOwnership(updated);
        return RegisterEnvironmentKeyResult.Ok(Models.EnvironmentKeyView.From(Find(updated.RootElement, keyId)!.Value), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<RetireEnvironmentKeyResult> HandleRetireEnvironmentKeyAsync(RetireEnvironmentKeyParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string environment = (string)parameters.Name;
        string keyId = (string)parameters.KeyId;

        GovernanceGate gate = await this.AuthorizeEnvironmentAdminAsync(environment, cancellationToken).ConfigureAwait(false);
        if (gate == GovernanceGate.NotFound)
        {
            return RetireEnvironmentKeyResult.NotFound(EnvironmentNotFoundProblem(environment), workspace);
        }

        if (gate != GovernanceGate.Authorized)
        {
            GovernanceAudit.Mutation(this.auditLogger, "environment.key.retire", this.CallerActor(), TargetKind, KeyKey(environment, keyId), "refused-not-administrator");
            return RetireEnvironmentKeyResult.Forbidden(NotAdministratorProblem(environment), workspace);
        }

        ParsedJsonDocument<Environment>? stored = await this.environments.GetAsync(environment, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return RetireEnvironmentKeyResult.NotFound(EnvironmentNotFoundProblem(environment), workspace);
        }

        if (Find(stored.RootElement, keyId) is not { } target)
        {
            stored.Dispose();
            return RetireEnvironmentKeyResult.NotFound(KeyNotFoundProblem(environment, keyId), workspace);
        }

        // Idempotent: retiring an already-Retired generation returns the existing record.
        if (IsRetired(target))
        {
            workspace.TakeOwnership(stored);
            return RetireEnvironmentKeyResult.Ok(Models.EnvironmentKeyView.From(target), workspace);
        }

        string actor = this.CallerActor();
        DateTimeOffset retiredAt = this.timeProvider.GetUtcNow();
        string? reason = ((JsonElement)parameters.Body).ValueKind == JsonValueKind.Object && ((JsonElement)parameters.Body.Reason).ValueKind == JsonValueKind.String
            ? (string)parameters.Body.Reason
            : null;

        using ParsedJsonDocument<Environment> draft = Environment.DraftWithKeyRetired(stored.RootElement, keyId, actor, retiredAt, reason);
        ParsedJsonDocument<Environment>? updated = await this.environments.UpdateAsync(
            environment, draft.RootElement, stored.RootElement.EtagValue, actor, this.access.Current(), cancellationToken).ConfigureAwait(false);
        stored.Dispose();
        if (updated is null)
        {
            return RetireEnvironmentKeyResult.Conflict(ConcurrentWriteProblem(environment, keyId), workspace);
        }

        GovernanceAudit.Mutation(this.auditLogger, "environment.key.retire", actor, TargetKind, KeyKey(environment, keyId), "retired");
        workspace.TakeOwnership(updated);
        return RetireEnvironmentKeyResult.Ok(Models.EnvironmentKeyView.From(Find(updated.RootElement, keyId)!.Value), workspace);
    }

    private static List<Environment.EnvironmentKeyGeneration> Matching(in Environment environment, string? state)
    {
        var generations = new List<Environment.EnvironmentKeyGeneration>();
        foreach (Environment.EnvironmentKeyGeneration generation in Environment.Enumerate(environment.KeyGenerations))
        {
            if (state is null || string.Equals((string)generation.State, state, StringComparison.Ordinal))
            {
                generations.Add(generation);
            }
        }

        return generations;
    }

    private static Environment.EnvironmentKeyGeneration? Find(in Environment environment, string keyId)
    {
        foreach (Environment.EnvironmentKeyGeneration generation in Environment.Enumerate(environment.KeyGenerations))
        {
            if (generation.KeyId.ValueEquals(keyId))
            {
                return generation;
            }
        }

        return null;
    }

    private static bool IsRetired(in Environment.EnvironmentKeyGeneration generation)
        => generation.State.ValueEquals("Retired"u8);

    private static string KeyKey(string environment, string keyId) => $"{environment}/{keyId}";

    private static bool TryDecode(string value, out byte[] bytes)
    {
        try
        {
            bytes = Convert.FromBase64String(value);
            return true;
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }
    }

    private async ValueTask<GovernanceGate> AuthorizeEnvironmentAdminAsync(string environment, CancellationToken cancellationToken)
    {
        using ParsedJsonDocument<Environment>? environmentDoc = await this.environments.GetAsync(environment, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (environmentDoc is null)
        {
            return GovernanceGate.NotFound;
        }

        using ParsedJsonDocument<EnvironmentAdministrators>? record = await this.administration.GetAdministratorsAsync(environment, cancellationToken).ConfigureAwait(false);
        return record?.RootElement.IsAdministeredBy(this.CallerIdentity()) == true
            ? GovernanceGate.Authorized
            : GovernanceGate.Forbidden;
    }

    private static Models.ProblemDetails.Source EnvironmentNotFoundProblem(string environment)
        => Problem("environment-not-found", "Environment not found", 404, $"No environment named '{environment}' exists, or it is outside your reach.");

    private static Models.ProblemDetails.Source KeyNotFoundProblem(string environment, string keyId)
        => Problem("environment-key-not-found", "Key generation not found", 404, $"No key generation '{keyId}' is registered for environment '{environment}'.");

    private static Models.ProblemDetails.Source NotAdministratorProblem(string environment)
        => Problem("not-administrator", "Not an administrator", 403, $"You are not a current administrator of environment '{environment}'.");

    private static Models.ProblemDetails.Source ConcurrentWriteProblem(string environment, string keyId)
        => Problem("environment-key-conflict", "Concurrent key change", 409, $"Environment '{environment}' changed while registering or retiring '{keyId}'. Re-read and retry.");

    private static Models.ProblemDetails.Source PossessionProblem(string environment, string keyId, EnvironmentKeyPossessionResult result)
        => Problem("environment-key-possession", "Key registration not accepted", 400, PossessionDetail(environment, keyId, result));

    private static string PossessionDetail(string environment, string keyId, EnvironmentKeyPossessionResult result) => result switch
    {
        EnvironmentKeyPossessionResult.AlgorithmUnsupported => "The declared signature algorithm is not accepted; register with ES256.",
        EnvironmentKeyPossessionResult.NotFresh => "The signing instant falls outside the freshness window; sign the registration again.",
        EnvironmentKeyPossessionResult.IdentifierTooLong => "The environment name or key id exceeds 256 characters.",
        EnvironmentKeyPossessionResult.KeyUnreadable => "The seal public key did not parse as a P-256 SPKI key.",
        _ => $"The signature did not verify for environment '{environment}' and key '{keyId}'. It must be made with the private half of the presented seal key, over the framed registration tuple.",
    };

    private static Models.ProblemDetails.Source Problem(string type, string title, int status, string detail)
        => new((ref Models.ProblemDetails.Builder b) => b.Create(
            detail: detail,
            status: status,
            title: title,
            type: ProblemBase + type));

    private SecurityTagSet CallerIdentity() => SecurityTagSet.FromTags(this.access.InternalTags());

    // Registering or retiring a key is a governance decision, so the actor recorded is the deciding subject, the same
    // one the runner-authorization decisions record, rather than a display name that may not be present.
    private string CallerActor()
        => this.access.CurrentPrincipal?.FindFirst(this.subjectClaimType)?.Value
        ?? PrincipalDisplayName.Resolve(this.access.CurrentPrincipal)
        ?? "unknown";

    private enum GovernanceGate
    {
        NotFound,
        Forbidden,
        Authorized,
    }
}