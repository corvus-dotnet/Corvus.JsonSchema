// <copyright file="ArazzoControlPlaneEnvironmentKeysHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
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

    // The page size for the owner-group scan. The scan stops at the first page proving a second group exists, so this
    // bounds the work of the single-owner-group case, which is the one that reads every environment.
    private const int OwnerGroupScanPageSize = 200;

    // A P-256 SPKI decodes to ~91 bytes and an IEEE P1363 signature to 64, so both sit well inside this in practice;
    // the length is caller-supplied, so anything larger falls back to the pool rather than the stack.
    private const int StackDecodeThreshold = 256;

    // The two generation states, pre-encoded so every comparison is span-to-span.
    private static ReadOnlySpan<byte> ActiveUtf8 => "Active"u8;

    private static ReadOnlySpan<byte> RetiredUtf8 => "Retired"u8;

    // The request's state filter, resolved once per request.
    private enum StateFilter : byte
    {
        None,
        Active,
        Retired,
        Unrecognized,
    }

    // The context threaded into the list projection: the stored generation set and the resolved filter. A struct, so
    // the builder delegates stay static and nothing is captured.
    private readonly struct KeyListState(Environment.EnvironmentKeyGenerationArray generations, StateFilter filter)
    {
        public Environment.EnvironmentKeyGenerationArray Generations { get; } = generations;

        public StateFilter Filter { get; } = filter;
    }

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

        // The generations and the resolved filter are threaded through as context rather than captured, and the
        // matching generations are written straight into the array builder. Collecting them into a List first would
        // allocate a list (and its growth) per request purely to re-iterate it one statement later, and comparing the
        // filter as a managed string would add one string per generation between two UTF-8 ends.
        var state = new KeyListState(stored.RootElement.KeyGenerations, ResolveStateFilter(parameters.State));
        Models.EnvironmentKeyList.Source<KeyListState> body = Models.EnvironmentKeyList.Build(
            in state,
            keys: Models.EnvironmentKeyList.EnvironmentKeyViewArray.Build(in state, BuildKeyViews));

        return ListEnvironmentKeysResult.Ok(body, workspace);
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

        // The base64 key and signature are decoded straight out of the request's UTF-8 into pooled buffers, and the
        // algorithm is matched span-to-span. Realizing any of them as managed strings (and letting
        // Convert.FromBase64String allocate the decoded arrays) would put four allocations between a UTF-8 body and
        // span-only crypto APIs, neither end of which is a string.
        EnvironmentKeyPossessionResult possession = this.VerifyPossession(environment, keyId, parameters.Body);

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

        // The seal key and algorithm are carried into the document as the request's own JSON values, copied verbatim.
        // Round-tripping them through managed strings would decode and re-encode two values that are already the exact
        // bytes being stored.
        using ParsedJsonDocument<Environment> draft = Environment.DraftWithKeyRegistered(
            stored.RootElement,
            keyId,
            (JsonElement)parameters.Body.SealPublicKey,
            (JsonElement)parameters.Body.Algorithm,
            actor,
            this.timeProvider.GetUtcNow());

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

        // ADR 0065: the tenancy invariant is write-time on creation, so without a symmetric refusal here an operator
        // registers a key, onboards the second owner group past the gate, and then retires the key — arriving at
        // exactly the state the gate exists to refuse, by a route the gate never sees. Retiring the LAST ACTIVE
        // generation is therefore refused while more than one owner group exists. Retiring a generation that is not
        // the last active one is a rotation and is always allowed.
        if (IsLastActive(stored.RootElement, keyId) && await this.MoreThanOneOwnerGroupAsync(cancellationToken).ConfigureAwait(false))
        {
            stored.Dispose();
            GovernanceAudit.Mutation(this.auditLogger, "environment.key.retire", this.CallerActor(), TargetKind, KeyKey(environment, keyId), "refused-last-active-generation");
            return RetireEnvironmentKeyResult.Conflict(LastActiveGenerationProblem(environment, keyId), workspace);
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

    // The request's state filter, resolved ONCE against UTF-8 literals rather than per generation. An unrecognized
    // value matches nothing rather than everything: a filter the server does not understand must not silently widen
    // the response to the full set.
    private static StateFilter ResolveStateFilter(in Models.GetEnvironmentsByNameKeysState state)
    {
        if (((JsonElement)state).ValueKind != JsonValueKind.String)
        {
            return StateFilter.None;
        }

        return state.ValueEquals(ActiveUtf8) ? StateFilter.Active
            : state.ValueEquals(RetiredUtf8) ? StateFilter.Retired
            : StateFilter.Unrecognized;
    }

    private static bool Matches(in Environment.EnvironmentKeyGeneration generation, StateFilter filter) => filter switch
    {
        StateFilter.None => true,
        StateFilter.Active => generation.State.ValueEquals(ActiveUtf8),
        StateFilter.Retired => generation.State.ValueEquals(RetiredUtf8),
        _ => false,
    };

    // Writes the matching generations into the response array. Each view is a whole-document re-wrap of the stored
    // generation, so nothing is projected field by field.
    private static void BuildKeyViews(in KeyListState state, ref Models.EnvironmentKeyList.EnvironmentKeyViewArray.Builder array)
    {
        foreach (Environment.EnvironmentKeyGeneration generation in Environment.Enumerate(state.Generations))
        {
            if (Matches(generation, state.Filter))
            {
                array.AddItem(Models.EnvironmentKeyView.From(generation));
            }
        }
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
        => generation.State.ValueEquals(RetiredUtf8);

    // Whether retiring `keyId` would leave this environment with no ACTIVE generation. The predicate the tenancy
    // invariant reads is "at least one ACTIVE generation", not "at least one generation": an environment whose only
    // generation is retired holds nothing that can protect a payload, so counting it would let the gate pass on a
    // record that means nothing.
    private static bool IsLastActive(in Environment environment, string keyId)
    {
        bool retiringAnActiveOne = false;
        int otherActive = 0;
        foreach (Environment.EnvironmentKeyGeneration generation in Environment.Enumerate(environment.KeyGenerations))
        {
            if (IsRetired(generation))
            {
                continue;
            }

            if (generation.KeyId.ValueEquals(keyId))
            {
                retiringAnActiveOne = true;
            }
            else
            {
                ++otherActive;
            }
        }

        return retiringAnActiveOne && otherActive == 0;
    }

    private static string KeyKey(string environment, string keyId) => $"{environment}/{keyId}";

    // Decodes the base64 seal key and signature from the request's own UTF-8 and verifies the proof of possession.
    // Synchronous by construction: the decoded spans and the request's UTF-8 views are ref structs that cannot cross
    // an await, and keeping the whole proof in one synchronous frame is what lets them stay spans.
    private EnvironmentKeyPossessionResult VerifyPossession(string environment, string keyId, in Models.EnvironmentKeyRegistration body)
    {
        ReadOnlySpan<byte> sealBase64 = ((JsonElement)body.SealPublicKey).GetUtf8String().Span;
        ReadOnlySpan<byte> signatureBase64 = ((JsonElement)body.Signature).GetUtf8String().Span;

        int maxSeal = Base64.GetMaxDecodedFromUtf8Length(sealBase64.Length);
        int maxSignature = Base64.GetMaxDecodedFromUtf8Length(signatureBase64.Length);
        byte[]? rentedSeal = maxSeal > StackDecodeThreshold ? ArrayPool<byte>.Shared.Rent(maxSeal) : null;
        byte[]? rentedSignature = maxSignature > StackDecodeThreshold ? ArrayPool<byte>.Shared.Rent(maxSignature) : null;
        try
        {
            Span<byte> sealBuffer = rentedSeal ?? stackalloc byte[StackDecodeThreshold];
            Span<byte> signatureBuffer = rentedSignature ?? stackalloc byte[StackDecodeThreshold];

            // Malformed base64 is the caller's own input, so it is a refusal rather than an exception surfacing from
            // the crypto layer as a 500.
            if (Base64.DecodeFromUtf8(sealBase64, sealBuffer, out _, out int sealLength) != OperationStatus.Done ||
                Base64.DecodeFromUtf8(signatureBase64, signatureBuffer, out _, out int signatureLength) != OperationStatus.Done)
            {
                return EnvironmentKeyPossessionResult.KeyUnreadable;
            }

            return EnvironmentKeyPossession.Verify(
                environment,
                keyId,
                ((JsonElement)body.Algorithm).GetUtf8String().Span,
                sealBuffer[..sealLength],
                ((NodaTime.OffsetDateTime)body.NotBefore).ToDateTimeOffset(),
                signatureBuffer[..signatureLength],
                this.timeProvider.GetUtcNow());
        }
        finally
        {
            if (rentedSeal is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedSeal);
            }

            if (rentedSignature is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedSignature);
            }
        }
    }

    // Whether the deployment holds environments belonging to more than one owner group. Read at SYSTEM reach and over
    // every environment, not the caller's: the invariant is a property of the deployment, and counting only what this
    // administrator can see would let a second owner group hide behind the very reach isolation the count is meant to
    // decide about. Bounded at two, so it stops at the first environment that proves the answer.
    private async ValueTask<bool> MoreThanOneOwnerGroupAsync(CancellationToken cancellationToken)
    {
        // Declared with an explicit try/finally rather than `using var`: a using local is implicitly read-only, and
        // calling a mutating method on a read-only struct local runs it against a defensive copy, so the probe would
        // observe every page and remember nothing.
        OwnerGroupProbe probe = default;
        ParsedJsonDocument<JsonString>? token = null;
        try
        {
            while (true)
            {
                using EnvironmentPage page = await this.environments.ListAsync(
                    AccessContext.System, OwnerGroupScanPageSize, token?.RootElement ?? default, cancellationToken).ConfigureAwait(false);

                if (probe.Observe(page.Environments, this.access.OwnerGroupTagKeyUtf8))
                {
                    return true;
                }

                ReadOnlySpan<byte> next = page.NextPageToken.Span;
                if (next.IsEmpty)
                {
                    return false;
                }

                // The page's continuation token is pooled and freed when the page disposes at the end of this
                // iteration, so it cannot be carried straight into the next call. Copy it into a pooled document that
                // owns its own buffer, and release the previous one.
                token?.Dispose();
                token = QuoteToken(next);
            }
        }
        finally
        {
            probe.Dispose();
            token?.Dispose();
        }
    }

    // Wraps a continuation token's UTF-8 as a JSON string value in a pooled document the caller owns. The token is
    // base64url, so it needs no JSON escaping and the quoting is two bytes; the rented buffer is handed to the
    // document, which returns it on dispose.
    private static ParsedJsonDocument<JsonString> QuoteToken(ReadOnlySpan<byte> token)
    {
        int length = token.Length + 2;
        byte[] rented = ArrayPool<byte>.Shared.Rent(length);
        rented[0] = (byte)'"';
        token.CopyTo(rented.AsSpan(1));
        rented[token.Length + 1] = (byte)'"';
        return ParsedJsonDocument<JsonString>.Parse(rented.AsMemory(0, length), rented);
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

    private static Models.ProblemDetails.Source LastActiveGenerationProblem(string environment, string keyId)
        => Problem(
            "environment-key-last-active",
            "Last active key generation",
            409,
            $"'{keyId}' is the last active key generation for environment '{environment}', and this deployment serves more than one owner group. Register a replacement generation before retiring this one.");

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