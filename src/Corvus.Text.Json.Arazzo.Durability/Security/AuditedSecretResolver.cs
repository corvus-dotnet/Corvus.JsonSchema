// <copyright file="AuditedSecretResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Records every secret a host resolves (ADR 0070): which secret, by which host, and whether it resolved. A host wraps
/// its resolver in this once, where it composes it, so every consumer of the resolver is covered: the HTTP credential
/// providers, the channel transports, and whatever is added later.
/// </summary>
/// <remarks>
/// <para>
/// A runner is a process of its own that the control plane does not trust (ADR 0065) and that has no path to the control
/// plane's audit sink, so it keeps an audit chain of its own: its own <see cref="GovernanceAuditor"/>, with its own sink,
/// head key and writer id. The records are the runner's evidence, checked by the same verify command.
/// </para>
/// <para>
/// The record names the secret's reference, which is a locator the credential binding already stores, and never the
/// material. A secret is resolved for a source and an environment and then cached, and not for a run, so the record
/// names the host and the secret and no run.
/// </para>
/// <para>
/// A resolution is never refused because its record could not be appended. Resolving a secret is part of running a
/// workflow, and run execution is never gated on the sink (ADR 0069): an outage of the audit sink must not stop or fault
/// a run. The failure counts, logs at error and degrades the host's audit health. A resolution that fails is recorded
/// too, since a secret that will not decrypt or is not where its reference says is the clearest tamper signal there is.
/// </para>
/// </remarks>
public sealed class AuditedSecretResolver : ISecretResolver
{
    /// <summary>The action a secret resolution is recorded as.</summary>
    public const string Action = "secret.resolve";

    private const int MaxReferenceLength = 256;

    private readonly ISecretResolver inner;
    private readonly GovernanceAuditor auditor;
    private readonly AuditSubject actor;

    /// <summary>Initializes a new instance of the <see cref="AuditedSecretResolver"/> class.</summary>
    /// <param name="inner">The resolver that resolves.</param>
    /// <param name="auditor">The host's own auditor.</param>
    /// <param name="actor">The host that is resolving: a runner's id.</param>
    public AuditedSecretResolver(ISecretResolver inner, GovernanceAuditor auditor, string actor)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(auditor);
        ArgumentException.ThrowIfNullOrEmpty(actor);
        this.inner = inner;
        this.auditor = auditor;
        this.actor = new AuditSubject(actor, null);
    }

    /// <inheritdoc/>
    public bool CanResolve(SecretScheme scheme) => this.inner.CanResolve(scheme);

    /// <inheritdoc/>
    public async ValueTask<SecretMaterial> ResolveAsync(SecretRef reference, CancellationToken cancellationToken)
    {
        SecretMaterial material;
        try
        {
            material = await this.inner.ResolveAsync(reference, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await this.RecordAsync(reference, "failed").ConfigureAwait(false);
            throw;
        }

        await this.RecordAsync(reference, "resolved").ConfigureAwait(false);
        return material;
    }

    private ValueTask RecordAsync(SecretRef reference, string disclosure)
    {
        string raw = reference.Raw ?? string.Empty;
        return this.auditor.ReadAsync(Action, this.actor, "secret", raw.Length <= MaxReferenceLength ? raw : raw[..MaxReferenceLength], disclosure, failClosed: false);
    }
}