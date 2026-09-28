// <copyright file="VersionReadiness.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Publishing;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Whether a workflow version is ready in an environment (ADR 0074): every source it references has a credential there
/// that its runs may use, and, where the environment requires evidence, its publish suite is green. It is the one rule
/// promotion is gated on (a direct make-available and an approved availability request) and the rule the readiness
/// endpoints report.
/// </summary>
/// <remarks>
/// A credential counts when a run of the version would get it: the store's usage path
/// (<see cref="ISourceCredentialStore.ResolveForUsageAsync"/>) against the version's own security tags, the identity its
/// runs inherit. It is not the approver's management view, so readiness is a fact about the version, the same for every
/// caller, and a credential restricted to another workflow, or to a group the version's publisher is not in, does not
/// count.
/// </remarks>
internal static class VersionReadiness
{
    /// <summary>The sources of <paramref name="version"/> that have no credential in <paramref name="environment"/> its
    /// runs may use.</summary>
    /// <param name="credentials">The source-credential store.</param>
    /// <param name="version">The catalogued version.</param>
    /// <param name="environment">The target environment.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The names of the sources with no usable credential, in the version's order; empty when all have one.</returns>
    internal static async ValueTask<List<string>> MissingSourcesAsync(ISourceCredentialStore credentials, CatalogVersion version, string environment, CancellationToken cancellationToken)
    {
        SecurityTagSet runTags = version.SecurityTagsValue;
        var missing = new List<string>();
        foreach (CatalogSourceRef source in version.SourcesValue.ToList())
        {
            using ParsedJsonDocument<SourceCredentialBinding>? binding = await credentials.ResolveForUsageAsync(source.Name, environment, runTags, cancellationToken).ConfigureAwait(false);
            if (binding is null)
            {
                missing.Add(source.Name);
            }
        }

        return missing;
    }

    /// <summary>Evaluates readiness in one environment for a version, or a draft of one, with the given sources and run tags.</summary>
    /// <param name="credentials">The source-credential store.</param>
    /// <param name="sourceNames">The names of the sources the version references, in its order.</param>
    /// <param name="runTags">The security tags the version's runs carry (or would carry, for a draft).</param>
    /// <param name="environment">The environment.</param>
    /// <param name="evidenceGreen">Whether the version's publish suite is green, or <see langword="null"/> for a draft.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The evaluation, which holds the resolved credentials until it is disposed.</returns>
    internal static async ValueTask<Evaluation> EvaluateAsync(ISourceCredentialStore credentials, IReadOnlyList<string> sourceNames, SecurityTagSet runTags, Environment environment, bool? evidenceGreen, CancellationToken cancellationToken)
    {
        string environmentName = environment.NameValue;
        var resolved = new ParsedJsonDocument<SourceCredentialBinding>?[sourceNames.Count];
        var evaluation = new Evaluation(environmentName, sourceNames, resolved, RequiresEvidence(environment), evidenceGreen);
        try
        {
            for (int i = 0; i < sourceNames.Count; i++)
            {
                resolved[i] = await credentials.ResolveForUsageAsync(sourceNames[i], environmentName, runTags, cancellationToken).ConfigureAwait(false);
            }

            return evaluation;
        }
        catch
        {
            evaluation.Dispose();
            throw;
        }
    }

    /// <summary>Whether the environment requires green publish evidence for promotion (workflow-designer design §4.6).</summary>
    /// <param name="environment">The environment.</param>
    /// <returns><see langword="true"/> if it does.</returns>
    internal static bool RequiresEvidence(in Environment environment)
        => environment.RequireEvidence.IsNotUndefined() && (bool)environment.RequireEvidence;

    /// <summary>Whether the version's package carries publish evidence whose attested suite is green: it ran at least one
    /// scenario and none failed (the evidence half of the §4.6 readiness formula).</summary>
    /// <param name="catalog">The workflow catalog.</param>
    /// <param name="context">The caller's access context.</param>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> if the suite is green.</returns>
    internal static async ValueTask<bool> HasGreenEvidenceAsync(ISecuredWorkflowCatalog catalog, AccessContext context, string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        ReadOnlyMemory<byte>? package = await catalog.GetPackageAsync(baseWorkflowId, versionNumber, context, cancellationToken).ConfigureAwait(false);
        if (package is not { } bytes || !WorkflowPackage.TryReadEntry(bytes, "metadata/evidence.json"u8, out ReadOnlyMemory<byte> entry))
        {
            return false;
        }

        using var evidence = ParsedJsonDocument<Models.PublishEvidence>.Parse(entry);
        Models.EvidenceSuite suite = evidence.RootElement.Suite;
        return suite.Total.IsNotUndefined() && (int)suite.Total > 0
            && suite.Failed.IsNotUndefined() && (int)suite.Failed == 0;
    }

    /// <summary>Readiness in one environment, holding the credential each source resolved (if any) so a response can be
    /// built from their display fields in place; dispose it once the response is built.</summary>
    internal sealed class Evaluation : IDisposable
    {
        private readonly ParsedJsonDocument<SourceCredentialBinding>?[] resolved;

        /// <summary>Initializes a new instance of the <see cref="Evaluation"/> class.</summary>
        /// <param name="environment">The environment's name.</param>
        /// <param name="sourceNames">The names of the sources, in the version's order.</param>
        /// <param name="resolved">The credential each source resolved, or <see langword="null"/>, by index.</param>
        /// <param name="evidenceRequired">Whether the environment requires evidence.</param>
        /// <param name="evidenceGreen">Whether the version's suite is green, or <see langword="null"/> for a draft.</param>
        internal Evaluation(string environment, IReadOnlyList<string> sourceNames, ParsedJsonDocument<SourceCredentialBinding>?[] resolved, bool evidenceRequired, bool? evidenceGreen)
        {
            this.Environment = environment;
            this.SourceNames = sourceNames;
            this.resolved = resolved;
            this.EvidenceRequired = evidenceRequired;
            this.EvidenceGreen = evidenceGreen;
        }

        /// <summary>Gets the environment's name.</summary>
        internal string Environment { get; }

        /// <summary>Gets the names of the sources, in the version's order.</summary>
        internal IReadOnlyList<string> SourceNames { get; }

        /// <summary>Gets a value indicating whether the environment requires green publish evidence.</summary>
        internal bool EvidenceRequired { get; }

        /// <summary>Gets whether the version's publish suite is green, or <see langword="null"/> for a draft.</summary>
        internal bool? EvidenceGreen { get; }

        /// <summary>Gets a value indicating whether every source has a credential the version's runs may use.</summary>
        internal bool CredentialsReady => Array.TrueForAll(this.resolved, static binding => binding is not null);

        /// <summary>Gets a value indicating whether the version may be promoted into the environment.</summary>
        internal bool Ready => this.CredentialsReady && (!this.EvidenceRequired || this.EvidenceGreen == true);

        /// <summary>Gets the credential the source at <paramref name="index"/> resolved, or <see langword="null"/>.</summary>
        /// <param name="index">The source's index.</param>
        /// <returns>The resolved binding.</returns>
        internal ParsedJsonDocument<SourceCredentialBinding>? Resolved(int index) => this.resolved[index];

        /// <inheritdoc/>
        public void Dispose()
        {
            foreach (ParsedJsonDocument<SourceCredentialBinding>? binding in this.resolved)
            {
                binding?.Dispose();
            }
        }
    }
}