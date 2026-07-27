// <copyright file="ServerThrowHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Centralized exception-throwing helpers for the control-plane server.
/// </summary>
/// <remarks>
/// <para>
/// Guard-position helpers are <c>Throw*</c> methods marked <see cref="DoesNotReturnAttribute"/> so the JIT can optimize call-site code after a throw; helpers used from an expression position (a <c>??</c>, <c>?:</c>, or <c>switch</c> arm, where a value-producing path must terminate) are <c>Get*Exception</c> factories the caller throws. All exception messages come from the embedded <c>Resources/Strings.resx</c> resource file via <c>SR</c>.
/// </para>
/// </remarks>
internal static class ServerThrowHelper
{
    /// <summary>Creates the exception for a self-elevation that could not be completed, for the caller to throw.</summary>
    /// <param name="requestId">The access-request id.</param>
    /// <returns>The exception to throw.</returns>
    public static AccessRequestStateException GetSelfElevationIncompleteException(string requestId)
        => new(requestId, SR.SelfElevationIncomplete);

    /// <summary>Throws when none of the requested scopes is grantable on the eligibility path.</summary>
    /// <param name="requestId">The access-request id.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowNoGrantableScopesEligibility(string requestId)
        => throw new AccessRequestStateException(requestId, SR.NoGrantableScopesEligibility);

    /// <summary>Throws when a caller other than the requester attempts to withdraw a request.</summary>
    /// <param name="requestId">The access-request id.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowOnlyRequesterMayWithdraw(string requestId)
        => throw new AccessRequestStateException(requestId, SR.OnlyRequesterMayWithdraw);

    /// <summary>Creates the exception for an unrecognised settlement outcome, for the caller to throw.</summary>
    /// <param name="outcome">The unrecognised outcome value.</param>
    /// <returns>The exception to throw.</returns>
    public static ArgumentException GetUnrecognisedSettlementOutcomeException(string outcome)
        => new(SR.Format(SR.UnrecognisedSettlementOutcome, outcome), nameof(outcome));

    /// <summary>Throws when a request is not in the status an operation requires.</summary>
    /// <param name="requestId">The access-request id.</param>
    /// <param name="actualStatus">The request's current status.</param>
    /// <param name="expectedStatus">The status the operation requires.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowRequestStatusMismatch(string requestId, object? actualStatus, object? expectedStatus)
        => throw new AccessRequestStateException(requestId, SR.Format(SR.RequestStatusMismatch, actualStatus, expectedStatus));

    /// <summary>Throws when a request is in a status from which it cannot be revoked.</summary>
    /// <param name="requestId">The access-request id.</param>
    /// <param name="actualStatus">The request's current status.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowRequestNotRevocable(string requestId, object? actualStatus)
        => throw new AccessRequestStateException(requestId, SR.Format(SR.RequestNotRevocable, actualStatus));

    /// <summary>Throws when a workflow id is not a permitted access-request target.</summary>
    /// <param name="baseWorkflowId">The rejected workflow id.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowWorkflowIdNotPermitted(string baseWorkflowId)
        => throw new AccessRequestStateException(baseWorkflowId, SR.Format(SR.WorkflowIdNotPermitted, baseWorkflowId));

    /// <summary>Throws when none of the requested scopes is grantable on the approval path.</summary>
    /// <param name="requestId">The access-request id.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowNoGrantableScopesApproval(string requestId)
        => throw new AccessRequestStateException(requestId, SR.NoGrantableScopesApproval);

    /// <summary>Throws when neither a resolved grantee identity nor a single dimension/value grant was provided.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowGranteeIdentityOrDimensionRequired()
        => throw new ArgumentException(SR.GranteeIdentityOrDimensionRequired);

    /// <summary>Throws when a named grantee does not resolve to a deployment identity.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowGranteeDoesNotResolve()
        => throw new ArgumentException(SR.GranteeDoesNotResolve);

    /// <summary>Throws when an administrator grant does not resolve to a deployment identity.</summary>
    /// <param name="dimension">The grant's dimension.</param>
    /// <param name="value">The grant's value.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowAdministratorGrantDoesNotResolve(string dimension, string value)
        => throw new ArgumentException(SR.Format(SR.AdministratorGrantDoesNotResolve, dimension, value));

    /// <summary>Throws when the provider registry names a provider more than once.</summary>
    /// <param name="providerName">The duplicated provider name.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowProviderNamedMoreThanOnce(string? providerName)
        => throw new ArgumentException(SR.Format(SR.ProviderNamedMoreThanOnce, providerName), "providers");

    /// <summary>Throws when a connected provider is missing a required field.</summary>
    /// <param name="providerName">The provider name.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowConnectedProviderMissingFields(string? providerName)
        => throw new ArgumentException(SR.Format(SR.ConnectedProviderMissingFields, providerName));

    /// <summary>Throws when a connected provider's endpoint configuration is ambiguous.</summary>
    /// <param name="providerName">The provider name.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowConnectedProviderEndpointAmbiguous(string? providerName)
        => throw new ArgumentException(SR.Format(SR.ConnectedProviderEndpointAmbiguous, providerName));

    /// <summary>Throws when a connected provider carries no hosts pattern.</summary>
    /// <param name="providerName">The provider name.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowConnectedProviderNoHosts(string? providerName)
        => throw new ArgumentException(SR.Format(SR.ConnectedProviderNoHosts, providerName));

    /// <summary>Throws when the provider broker carries no entry for the GitHub provider.</summary>
    /// <param name="providerName">The expected provider entry name.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowNoGitHubProviderEntry(string providerName)
        => throw new ArgumentException(SR.Format(SR.NoGitHubProviderEntry, providerName), "providers");

    /// <summary>Creates the exception for a GitHub sign-in that could not begin, for the caller to throw.</summary>
    /// <param name="outcome">The begin-auth outcome.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException GetGitHubSignInBeginFailedException(object? outcome)
        => new(SR.Format(SR.GitHubSignInBeginFailed, outcome));

    /// <summary>Throws when a GitHub broker is missing a required option.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowGitHubBrokerMissingFields()
        => throw new ArgumentException(SR.GitHubBrokerMissingFields);

    /// <summary>Throws when a reach-enforcing security mode is missing its required row-security policy.</summary>
    /// <param name="securityMode">The configured security mode.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowRowSecurityPolicyRequired(ControlPlaneSecurityMode securityMode)
        => throw new ArgumentException(SR.Format(SR.RowSecurityPolicyRequired, securityMode), "rowSecurity");

    /// <summary>Throws when a System-reach security mode is given a row-security policy it would ignore.</summary>
    /// <param name="securityMode">The configured security mode.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowRowSecurityPolicyForbidden(ControlPlaneSecurityMode securityMode)
        => throw new ArgumentException(SR.Format(SR.RowSecurityPolicyForbidden, securityMode), "rowSecurity");

    /// <summary>Throws when a required 'name' field is missing.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowNameRequired()
        => throw new ArgumentException(SR.NameRequired);

    /// <summary>Creates the exception for a working copy that vanished during source carry-over, for the caller to throw.</summary>
    /// <param name="id">The working-copy id.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException GetWorkingCopyVanishedException(object? id)
        => new(SR.Format(SR.WorkingCopyVanished, id));

    /// <summary>Creates the exception for a missing embedded Arazzo meta-schema, for the caller to throw.</summary>
    /// <param name="name">The meta-schema name.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException GetMetaSchemaMissingException(object? name)
        => new(SR.Format(SR.MetaSchemaMissing, name));

    /// <summary>Throws when a required 'type' field is missing.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowTypeRequired()
        => throw new ArgumentException(SR.TypeRequired);

    /// <summary>Throws when a required 'document' field is missing.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowDocumentRequired()
        => throw new ArgumentException(SR.DocumentRequired);

    /// <summary>Throws when a required 'sourceName' field is missing.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSourceNameRequired()
        => throw new ArgumentException(SR.SourceNameRequired);

    /// <summary>Throws when a required 'environment' field is missing.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowEnvironmentRequired()
        => throw new ArgumentException(SR.EnvironmentRequired);

    /// <summary>Throws when an mTLS credential is given a usage grantee it cannot carry.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowMtlsCannotBeUsageScoped()
        => throw new ArgumentException(SR.MtlsCannotBeUsageScoped);

    /// <summary>Throws when an API key credential is bound to a channel source.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowApiKeyNotForChannelSource()
        => throw new ArgumentException(SR.ApiKeyNotForChannelSource);

    /// <summary>Throws when a channel credential is given a usage grantee it cannot carry.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowChannelCredentialCannotBeUsageScoped()
        => throw new ArgumentException(SR.ChannelCredentialCannotBeUsageScoped);

    /// <summary>Throws when a channel credential does not carry the broker 'serverUrl' config entry.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowChannelCredentialNeedsServerUrl()
        => throw new ArgumentException(SR.ChannelCredentialNeedsServerUrl);

    /// <summary>Creates the exception for a missing required 'authKind' field, for the caller to throw.</summary>
    /// <returns>The exception to throw.</returns>
    public static ArgumentException GetAuthKindRequiredException()
        => new(SR.AuthKindRequired);

    /// <summary>Creates the exception for an unrecognised credential status, for the caller to throw.</summary>
    /// <param name="status">The unrecognised status.</param>
    /// <returns>The exception to throw.</returns>
    public static ArgumentOutOfRangeException GetUnknownCredentialStatusException(CredentialStatus status)
        => new(nameof(status), status, SR.UnknownCredentialStatus);

    /// <summary>Creates the exception for an unrecognised grantee kind, for the caller to throw.</summary>
    /// <param name="kind">The unrecognised kind.</param>
    /// <returns>The exception to throw.</returns>
    public static ArgumentOutOfRangeException GetUnknownGranteeKindException(GranteeKind kind)
        => new(nameof(kind), kind, SR.UnknownGranteeKind);

    /// <summary>Throws when a serverless run host returns a non-success status advancing a run.</summary>
    /// <param name="statusCode">The status code the host returned.</param>
    /// <param name="runId">The run id.</param>
    /// <param name="url">The invocation URL.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowServerlessRunHostFailed(HttpStatusCode statusCode, object? runId, object? url)
        => throw new HttpRequestException(SR.Format(SR.ServerlessRunHostFailed, (int)statusCode, runId, url), null, statusCode);
}