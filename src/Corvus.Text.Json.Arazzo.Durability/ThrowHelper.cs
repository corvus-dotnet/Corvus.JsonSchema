// <copyright file="ThrowHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Corvus.Text.Json.Arazzo.Durability.Publishing;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Centralized exception-throwing helpers for the Arazzo durability core.
/// </summary>
/// <remarks>
/// <para>
/// Guard-position helpers are <c>Throw*</c> methods marked <see cref="DoesNotReturnAttribute"/> so the JIT can optimize
/// call-site code after a throw. Helpers used in expression position (a switch-expression arm, a <c>??</c>/ternary
/// operand, an expression-bodied member, or a <c>catch</c> where a try-assigned local is used afterwards) are
/// <c>Get*Exception</c> factories the caller throws (the <c>throw</c> keyword provides the terminating semantics, and a
/// try-assigned local stays definitely assigned — a <see cref="DoesNotReturnAttribute"/> void helper would not satisfy
/// C# definite-assignment there). All exception messages come from the embedded <c>Resources/Strings.resx</c> resource
/// file via <c>SR</c>.
/// </para>
/// </remarks>
internal static class ThrowHelper
{
    // ── Continuation-token / paging decode failures ─────────────────────────────────────────────────────────────────
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowContinuationTokenMalformed()
        => throw new FormatException(SR.ContinuationTokenMalformed);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowAvailabilityTokenInvalidBase64()
        => throw new FormatException(SR.AvailabilityTokenInvalidBase64);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowAvailabilityTokenMalformed()
        => throw new FormatException(SR.AvailabilityTokenMalformed);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowAvailabilityTokenInvalidVersion()
        => throw new FormatException(SR.AvailabilityTokenInvalidVersion);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowRunnerTokenInvalidBase64()
        => throw new FormatException(SR.RunnerTokenInvalidBase64);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowAvailabilityRequestInstantNotFormatted()
        => throw new FormatException(SR.AvailabilityRequestInstantNotFormatted);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowAvailabilityRequestTokenInvalidBase64()
        => throw new FormatException(SR.AvailabilityRequestTokenInvalidBase64);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowAvailabilityRequestTokenMalformed()
        => throw new FormatException(SR.AvailabilityRequestTokenMalformed);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowAdministeredWorkflowsTokenInvalidBase64()
        => throw new FormatException(SR.AdministeredWorkflowsTokenInvalidBase64);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowAdministeredEnvironmentsTokenInvalidBase64()
        => throw new FormatException(SR.AdministeredEnvironmentsTokenInvalidBase64);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowNativeBuildJobInstantNotFormatted()
        => throw new FormatException(SR.NativeBuildJobInstantNotFormatted);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowNativeBuildJobTokenInvalidBase64()
        => throw new FormatException(SR.NativeBuildJobTokenInvalidBase64);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowNativeBuildJobTokenMalformed()
        => throw new FormatException(SR.NativeBuildJobTokenMalformed);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowWorkflowDeploymentInstantNotFormatted()
        => throw new FormatException(SR.WorkflowDeploymentInstantNotFormatted);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowWorkflowDeploymentTokenInvalidBase64()
        => throw new FormatException(SR.WorkflowDeploymentTokenInvalidBase64);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowWorkflowDeploymentTokenMalformed()
        => throw new FormatException(SR.WorkflowDeploymentTokenMalformed);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSecurityRuleTokenInvalidBase64()
        => throw new FormatException(SR.SecurityRuleTokenInvalidBase64);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSourcePageTokenInvalidBase64()
        => throw new FormatException(SR.SourcePageTokenInvalidBase64);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSourcePageTokenInvalid(string token)
        => throw new FormatException(SR.Format(SR.SourcePageTokenInvalid, token));

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSourcePageTokenMalformed()
        => throw new FormatException(SR.SourcePageTokenMalformed);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowCredentialTokenInvalidBase64()
        => throw new FormatException(SR.CredentialTokenInvalidBase64);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowCredentialTokenInvalid(string token)
        => throw new FormatException(SR.Format(SR.CredentialTokenInvalid, token));

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowCredentialTokenMalformed()
        => throw new FormatException(SR.CredentialTokenMalformed);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowObservedIdentityTokenInvalidBase64()
        => throw new FormatException(SR.ObservedIdentityTokenInvalidBase64);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowObservedIdentityTokenInvalid(string token)
        => throw new FormatException(SR.Format(SR.ObservedIdentityTokenInvalid, token));

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowObservedIdentityTokenMalformed()
        => throw new FormatException(SR.ObservedIdentityTokenMalformed);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowAccessRequestInstantNotFormatted()
        => throw new FormatException(SR.AccessRequestInstantNotFormatted);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowAccessRequestTokenInvalidBase64()
        => throw new FormatException(SR.AccessRequestTokenInvalidBase64);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowAccessRequestTokenMalformed()
        => throw new FormatException(SR.AccessRequestTokenMalformed);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowRunnerAuthorizationTokenInvalidBase64()
        => throw new FormatException(SR.RunnerAuthorizationTokenInvalidBase64);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowRunnerAuthorizationTokenMalformed()
        => throw new FormatException(SR.RunnerAuthorizationTokenMalformed);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowBindingOrderNotFormatted()
        => throw new FormatException(SR.BindingOrderNotFormatted);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSecurityBindingTokenInvalidBase64()
        => throw new FormatException(SR.SecurityBindingTokenInvalidBase64);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSecurityBindingTokenMalformed()
        => throw new FormatException(SR.SecurityBindingTokenMalformed);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowEnvironmentTokenInvalidBase64()
        => throw new FormatException(SR.EnvironmentTokenInvalidBase64);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowEnvironmentTokenInvalid(string token)
        => throw new FormatException(SR.Format(SR.EnvironmentTokenInvalid, token));

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowEnvironmentTokenMalformed()
        => throw new FormatException(SR.EnvironmentTokenMalformed);

    // ── Checkpoint protection ───────────────────────────────────────────────────────────────────────────────────────
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowCheckpointTooShort()
        => throw new CryptographicException(SR.CheckpointTooShort);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowCheckpointMalformed()
        => throw new CryptographicException(SR.CheckpointMalformed);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowInvalidAesKeyLength()
        => throw new ArgumentException(SR.InvalidAesKeyLength, "key");

    // ── Transport / dispatch / worker binding ───────────────────────────────────────────────────────────────────────
    public static WorkflowTransportBindingException GetWorkflowRequiresApiSourceException(string workflowId, string source)
        => new(SR.Format(SR.WorkflowRequiresApiSource, workflowId, source));

    public static WorkflowTransportBindingException GetWorkflowRequiresChannelSourceException(string workflowId, string source)
        => new(SR.Format(SR.WorkflowRequiresChannelSource, workflowId, source));

    public static ArgumentException GetStateStoreMustImplementDispatchIndexForDispatcherException()
        => new(SR.StateStoreMustImplementDispatchIndexForDispatcher, "store");

    public static ArgumentException GetStateStoreMustImplementWaitIndexForWorkerException()
        => new(SR.StateStoreMustImplementWaitIndexForWorker, "store");

    // ── Management / wrapped-store capability ───────────────────────────────────────────────────────────────────────
    public static NotSupportedException GetLoadRunStateNotSupportedException()
        => new(SR.LoadRunStateNotSupported);

    public static NotSupportedException GetWrappedStoreNoLeaseAdministrationException()
        => new(SR.WrappedStoreNoLeaseAdministration);

    public static NotSupportedException GetWrappedStoreNoDispatchIndexException()
        => new(SR.WrappedStoreNoDispatchIndex);

    public static NotSupportedException GetWrappedStoreNoWaitIndexException()
        => new(SR.WrappedStoreNoWaitIndex);

    public static NotSupportedException GetStateStoreMustImplementWaitIndexForVisibilityException()
        => new(SR.StateStoreMustImplementWaitIndexForVisibility);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowResumerRequired()
        => throw new InvalidOperationException(SR.ResumerRequired);

    public static ArgumentException GetRewindRequiresTargetCursorException()
        => new(SR.RewindRequiresTargetCursor, "options");

    public static ArgumentOutOfRangeException GetUnknownResumeModeException(object? actualValue)
        => new("options", actualValue, SR.UnknownResumeMode);

    // ── Row security / baked host / tag validation ──────────────────────────────────────────────────────────────────
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowStoreDoesNotImplementRowSecurity(string storeTypeName)
        => throw new NotSupportedException(SR.Format(SR.StoreDoesNotImplementRowSecurity, storeTypeName));

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowBakedHostWorkflowMismatch(string bakedWorkflowId, string runWorkflowId)
        => throw new InvalidOperationException(SR.Format(SR.BakedHostWorkflowMismatch, bakedWorkflowId, runWorkflowId));

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowUserTagUsesReservedInternalPrefix(string tagKey, string internalPrefix)
        => throw new ArgumentException(SR.Format(SR.UserTagUsesReservedInternalPrefix, tagKey, internalPrefix), "userTags");

    // ── Hosted-workflow resolution ──────────────────────────────────────────────────────────────────────────────────
    public static InvalidOperationException GetWorkflowIdNotVersionedException(string workflowId)
        => new(SR.Format(SR.WorkflowIdNotVersioned, workflowId));

    public static InvalidOperationException GetVersionNotInCatalogException(int versionNumber, string baseWorkflowId)
        => new(SR.Format(SR.VersionNotInCatalog, versionNumber, baseWorkflowId));

    public static InvalidOperationException GetVersionNotRunnableException(int versionNumber, string baseWorkflowId)
        => new(SR.Format(SR.VersionNotRunnable, versionNumber, baseWorkflowId));

    public static InvalidOperationException GetVersionHasNoManifestException(int versionNumber, string baseWorkflowId)
        => new(SR.Format(SR.VersionHasNoManifest, versionNumber, baseWorkflowId));

    // ── Catalog package ─────────────────────────────────────────────────────────────────────────────────────────────
    public static ArgumentException GetPackageWorkflowHasNoWorkflowIdException()
        => new(SR.PackageWorkflowHasNoWorkflowId, "packageZip");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowUpdatedPackageContentHashDiffers(string updatedHash, string expectedContentHash)
        => throw new InvalidOperationException(SR.Format(SR.UpdatedPackageContentHashDiffers, updatedHash, expectedContentHash));

    // ── Security-rule parser ────────────────────────────────────────────────────────────────────────────────────────
    public static InvalidOperationException GetResolveKnownNotValidForTagKeyException()
        => new(SR.ResolveKnownNotValidForTagKey);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowUnexpectedTrailingContentInRule(int position)
        => throw new FormatException(SR.Format(SR.UnexpectedTrailingContentInRule, position));

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowExpectedCloseParenInRule()
        => throw new FormatException(SR.ExpectedCloseParenInRule);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowOrderedComparisonLeftMustBeDimension()
        => throw new FormatException(SR.OrderedComparisonLeftMustBeDimension);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowOrderedComparisonRightMustBeLiteralOrClaim()
        => throw new FormatException(SR.OrderedComparisonRightMustBeLiteralOrClaim);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowExpectedOpenParenAfterIn()
        => throw new FormatException(SR.ExpectedOpenParenAfterIn);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowInListTakesQuotedLiterals(int position)
        => throw new FormatException(SR.Format(SR.InListTakesQuotedLiterals, position));

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowExpectedCommaOrCloseParenInInList(int position)
        => throw new FormatException(SR.Format(SR.ExpectedCommaOrCloseParenInInList, position));

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowExpectedOperandInRule(int position)
        => throw new FormatException(SR.Format(SR.ExpectedOperandInRule, position));

    public static FormatException GetUnknownClaimsPredicateException(string predicate)
        => new(SR.Format(SR.UnknownClaimsPredicate, predicate));

    // ── Enum token round-trips ──────────────────────────────────────────────────────────────────────────────────────
    public static ArgumentOutOfRangeException GetUnknownCatalogStatusException(object? actualValue)
        => new("status", actualValue, SR.UnknownCatalogStatus);

    public static ArgumentOutOfRangeException GetUnknownGranteeKindException(object? actualValue)
        => new("kind", actualValue, SR.UnknownGranteeKind);

    public static ArgumentOutOfRangeException GetUnknownSourceCredentialKindException(object? actualValue)
        => new("kind", actualValue, SR.UnknownSourceCredentialKind);

    public static ArgumentException GetUnknownSourceCredentialAuthKindException(string token)
        => new(SR.Format(SR.UnknownSourceCredentialAuthKind, token), "token");

    // ── Native build job state ──────────────────────────────────────────────────────────────────────────────────────
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowNativeBuildJobNotBuildingForCompletion(string id)
        => throw new NativeBuildJobStateException(id, SR.Format(SR.NativeBuildJobNotBuildingForCompletion, id));

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowNativeBuildJobNotBuildingForRenewal(string id)
        => throw new NativeBuildJobStateException(id, SR.Format(SR.NativeBuildJobNotBuildingForRenewal, id));

    // ── Workflow deployment state ───────────────────────────────────────────────────────────────────────────────────
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowWorkflowDeploymentNotDeployingForCompletion(string id)
        => throw new WorkflowDeploymentStateException(id, SR.Format(SR.WorkflowDeploymentNotDeployingForCompletion, id));

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowWorkflowDeploymentNotDeployingForRenewal(string id)
        => throw new WorkflowDeploymentStateException(id, SR.Format(SR.WorkflowDeploymentNotDeployingForRenewal, id));

    // ── Secured catalog / administration ────────────────────────────────────────────────────────────────────────────
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowWorkflowIdAlreadyVersioned(string baseWorkflowId)
        => throw new ArgumentException(SR.Format(SR.WorkflowIdAlreadyVersioned, baseWorkflowId), "packageUtf8");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowCannotRemoveLastWorkflowAdministrator()
        => throw new ArgumentException(SR.CannotRemoveLastWorkflowAdministrator, "digest");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowWorkflowTransferRequiresAdministrator()
        => throw new ArgumentException(SR.WorkflowTransferRequiresAdministrator, "newAdministrators");

    public static NotSupportedException GetWorkflowAdministrationRequiresStoreException()
        => new(SR.WorkflowAdministrationRequiresStore);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowCannotRemoveLastEnvironmentAdministrator()
        => throw new ArgumentException(SR.CannotRemoveLastEnvironmentAdministrator, "digest");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowEnvironmentTransferRequiresAdministrator()
        => throw new ArgumentException(SR.EnvironmentTransferRequiresAdministrator, "newAdministrators");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowEnvironmentAdministrationRecordRequiresAdministrator()
        => throw new ArgumentException(SR.EnvironmentAdministrationRecordRequiresAdministrator, "administrators");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowWorkflowAdministrationRecordRequiresAdministrator()
        => throw new ArgumentException(SR.WorkflowAdministrationRecordRequiresAdministrator, "administrators");

    public static NotSupportedException GetWorkflowReverseIndexNotMaintainedException()
        => new(SR.WorkflowReverseIndexNotMaintained);

    public static NotSupportedException GetEnvironmentReverseIndexNotMaintainedException()
        => new(SR.EnvironmentReverseIndexNotMaintained);

    // ── Workflow package binary ─────────────────────────────────────────────────────────────────────────────────────
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowPackageSourceEntryNameTooLong(string key)
        => throw new ArgumentException(SR.Format(SR.PackageSourceEntryNameTooLong, key), "sources");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowNativeBinaryEmpty()
        => throw new ArgumentException(SR.NativeBinaryEmpty, "nativeBinary");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowAttestationManifestEmpty()
        => throw new ArgumentException(SR.AttestationManifestEmpty, "attestationManifest");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowAttestationSignatureEmpty()
        => throw new ArgumentException(SR.AttestationSignatureEmpty, "attestationSignature");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowPackageHasNoWorkflowJson()
        => throw new ArgumentException(SR.PackageHasNoWorkflowJson, "package");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowRuntimeIdentifierContainsSlash(string runtimeIdentifier)
        => throw new ArgumentException(SR.Format(SR.RuntimeIdentifierContainsSlash, runtimeIdentifier), "runtimeIdentifier");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowRuntimeIdentifierTooLong(string runtimeIdentifier)
        => throw new ArgumentException(SR.Format(SR.RuntimeIdentifierTooLong, runtimeIdentifier), "runtimeIdentifier");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowInvalidWorkflowPackageBadMagic()
        => throw new ArgumentException(SR.InvalidWorkflowPackageBadMagic, "span");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowUnsupportedPackageEntryEncoding(byte encoding)
        => throw new InvalidDataException(SR.Format(SR.UnsupportedPackageEntryEncoding, encoding));

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowWorkflowPackageTruncated()
        => throw new InvalidDataException(SR.WorkflowPackageTruncated);

    // ── Draft validation (create/save contracts) ────────────────────────────────────────────────────────────────────
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowAvailabilityEntryRequiresKey()
        => throw new ArgumentException(SR.AvailabilityEntryRequiresKey, "draft");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowAvailabilityRequestRequiresContent()
        => throw new ArgumentException(SR.AvailabilityRequestRequiresContent, "draft");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowNativeBuildJobRequiresContent()
        => throw new ArgumentException(SR.NativeBuildJobRequiresContent, "draft");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowWorkflowDeploymentRequiresContent()
        => throw new ArgumentException(SR.WorkflowDeploymentRequiresContent, "draft");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowRunnerAuthorizationRequiresContent()
        => throw new ArgumentException(SR.RunnerAuthorizationRequiresContent, "draft");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSourceRequiresName()
        => throw new ArgumentException(SR.SourceRequiresName, "draft");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSourceRequiresNonEmptyName()
        => throw new ArgumentException(SR.SourceRequiresNonEmptyName, "draft");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSourceRequiresType()
        => throw new ArgumentException(SR.SourceRequiresType, "draft");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSourceRequiresDocument()
        => throw new ArgumentException(SR.SourceRequiresDocument, "draft");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowWorkingCopyRequiresName()
        => throw new ArgumentException(SR.WorkingCopyRequiresName, "draft");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowWorkingCopyRequiresNonEmptyName()
        => throw new ArgumentException(SR.WorkingCopyRequiresNonEmptyName, "draft");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowWorkingCopyRequiresDocument()
        => throw new ArgumentException(SR.WorkingCopyRequiresDocument, "draft");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowEnvironmentRequiresName()
        => throw new ArgumentException(SR.EnvironmentRequiresName, "draft");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowEnvironmentRequiresNonEmptyName()
        => throw new ArgumentException(SR.EnvironmentRequiresNonEmptyName, "draft");

    // ── Source-credential binding validation ────────────────────────────────────────────────────────────────────────
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowBindingRequiresSecretReference()
        => throw new ArgumentException(SR.BindingRequiresSecretReference, "draft");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSecretReferenceRequiresName()
        => throw new ArgumentException(SR.SecretReferenceRequiresName, "draft");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowInvalidSecretRefInBinding(string referenceName)
        => throw new ArgumentException(SR.Format(SR.InvalidSecretRefInBinding, referenceName), "draft");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowMtlsRequiresCertificate()
        => throw new ArgumentException(SR.MtlsRequiresCertificate, "draft");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowMtlsCannotBeUsageScoped()
        => throw new ArgumentException(SR.MtlsCannotBeUsageScoped, "draft");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowBindingRequiresSourceName()
        => throw new ArgumentException(SR.BindingRequiresSourceName, "draft");

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowBindingRequiresEnvironment()
        => throw new ArgumentException(SR.BindingRequiresEnvironment, "draft");

    // ── Secret resolvers ────────────────────────────────────────────────────────────────────────────────────────────
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowEnvResolverWrongScheme(SecretRef reference)
        => throw new SecretResolutionException(reference, SR.EnvResolverWrongScheme);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowEnvSecretsUnversioned(SecretRef reference)
        => throw new SecretResolutionException(reference, SR.EnvSecretsUnversioned);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowEnvVariableNotSet(SecretRef reference)
        => throw new SecretResolutionException(reference, SR.Format(SR.EnvVariableNotSet, reference.Locator));

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowFileResolverWrongScheme(SecretRef reference)
        => throw new SecretResolutionException(reference, SR.FileResolverWrongScheme);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowFileSecretsUnversioned(SecretRef reference)
        => throw new SecretResolutionException(reference, SR.FileSecretsUnversioned);

    public static SecretResolutionException GetCouldNotReadSecretFileException(SecretRef reference, Exception inner)
        => new(reference, SR.Format(SR.CouldNotReadSecretFile, reference.Locator), inner);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSecretLocatorEscapesRoot(SecretRef reference, string secretRoot)
        => throw new SecretResolutionException(reference, SR.Format(SR.SecretLocatorEscapesRoot, secretRoot));

    public static SecretResolutionException GetNoResolverForSchemeException(SecretRef reference)
        => new(reference, SR.Format(SR.NoResolverForScheme, reference.Scheme));

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowNoSecretResolversRegistered()
        => throw new InvalidOperationException(SR.NoSecretResolversRegistered);

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowMultipleResolversForScheme(SecretScheme scheme)
        => throw new InvalidOperationException(SR.Format(SR.MultipleResolversForScheme, scheme));

    public static FormatException GetInvalidSecretReferenceException(string reference)
        => new(SR.Format(SR.InvalidSecretReference, reference));

    // ── In-memory store uniqueness ──────────────────────────────────────────────────────────────────────────────────
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSourceCredentialBindingAlreadyExists(string key)
        => throw new InvalidOperationException(SR.Format(SR.SourceCredentialBindingAlreadyExists, key));

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSecurityRuleAlreadyExists(string name)
        => throw new InvalidOperationException(SR.Format(SR.SecurityRuleAlreadyExists, name));

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSourceAlreadyExists(string name)
        => throw new InvalidOperationException(SR.Format(SR.SourceAlreadyExists, name));

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowEnvironmentAlreadyExists(string name)
        => throw new InvalidOperationException(SR.Format(SR.EnvironmentAlreadyExists, name));

    // ── Draft runs ──────────────────────────────────────────────────────────────────────────────────────────────────
    public static InvalidOperationException GetDraftResumerDoesNotHostRunException(string runId, string workflowId)
        => new(SR.Format(SR.DraftResumerDoesNotHostRun, runId, workflowId));

    public static InvalidOperationException GetDraftRunHasNoCaptureException(string runId)
        => new(SR.Format(SR.DraftRunHasNoCapture, runId));

    public static InvalidOperationException GetDraftRunHasNoPackageException(string runId)
        => new(SR.Format(SR.DraftRunHasNoPackage, runId));

    public static InvalidOperationException GetDraftRunNotExecutableException(string runId)
        => new(SR.Format(SR.DraftRunNotExecutable, runId));

    public static InvalidOperationException GetNoRunToAssembleTraceException(string runId)
        => new(SR.Format(SR.NoRunToAssembleTrace, runId));

    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowDraftRunnerLoopAlreadyRunning()
        => throw new InvalidOperationException(SR.DraftRunnerLoopAlreadyRunning);
}