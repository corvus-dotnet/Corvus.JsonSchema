// <copyright file="ThrowHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Centralized exception-throwing helpers for the Arazzo workflow code generator.
/// </summary>
/// <remarks>
/// <para>
/// Guard-position helpers are <c>Throw*</c> methods marked <see cref="DoesNotReturnAttribute"/> so the JIT can optimize call-site code after a throw; helpers used from an expression (a <c>??</c>/ternary/switch-arm), before a use of a pattern variable or an out variable across a short-circuit, or as the terminal of a value-producing path are <c>Get*Exception</c> factories the caller throws (a void helper cannot satisfy C# definite-assignment or all-paths-return there). All exception messages come from the embedded <c>Resources/Strings.resx</c> resource file via <c>SR</c>.
/// </para>
/// </remarks>
internal static class ThrowHelper
{
    /// <summary>Creates the exception for a channel/action that no AsyncAPI source defines, for the caller to throw.</summary>
    /// <param name="channelPath">The channel path.</param>
    /// <param name="action">The action (lower-cased).</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException GetNoChannelForActionException(object? channelPath, object? action)
        => new(SR.Format(SR.NoChannelForAction, channelPath, action));

    /// <summary>Throws when a source-qualified operationId references an undefined source description.</summary>
    /// <param name="operationId">The operationId.</param>
    /// <param name="qualifiedSource">The referenced source description name.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSourceDescriptionNotDefined(object? operationId, object? qualifiedSource)
        => throw new InvalidOperationException(SR.Format(SR.SourceDescriptionNotDefined, operationId, qualifiedSource));

    /// <summary>Creates the exception for an operationId that does not resolve in the named source, for the caller to throw.</summary>
    /// <param name="operationId">The operationId.</param>
    /// <param name="qualifiedSource">The source description name.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException GetOperationIdNotInSourceException(object? operationId, object? qualifiedSource)
        => new(SR.Format(SR.OperationIdNotInSource, operationId, qualifiedSource));

    /// <summary>Throws when a plain operationId is defined by more than one source description.</summary>
    /// <param name="operationId">The operationId.</param>
    /// <param name="matchedSource">The first matching source description name.</param>
    /// <param name="operationSourceName">The second matching source description name.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowAmbiguousOperationId(object? operationId, object? matchedSource, object? operationSourceName)
        => throw new InvalidOperationException(SR.Format(SR.AmbiguousOperationId, operationId, matchedSource, operationSourceName));

    /// <summary>Creates the exception for an operationId no source description defines, for the caller to throw.</summary>
    /// <param name="operationId">The operationId.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException GetNoSourceForOperationIdException(object? operationId)
        => new(SR.Format(SR.NoSourceForOperationId, operationId));

    /// <summary>Creates the exception for an operationPath that does not resolve in the named source, for the caller to throw.</summary>
    /// <param name="operationPath">The operationPath.</param>
    /// <param name="sourceName">The source description name.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException GetOperationPathNotInSourceException(object? operationPath, object? sourceName)
        => new(SR.Format(SR.OperationPathNotInSource, operationPath, sourceName));

    /// <summary>Creates the exception for an operationPath that resolves to no source operation, for the caller to throw.</summary>
    /// <param name="operationPath">The operationPath.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException GetOperationPathNotResolvedException(object? operationPath)
        => new(SR.Format(SR.OperationPathNotResolved, operationPath));

    /// <summary>Creates the exception for a channel step targeting a non-send channel, for the caller to throw.</summary>
    /// <param name="stepId">The step id.</param>
    /// <param name="channelAddress">The channel address.</param>
    /// <returns>The exception to throw.</returns>
    public static NotSupportedException GetNonSendChannelException(object? stepId, object? channelAddress)
        => new(SR.Format(SR.NonSendChannel, stepId, channelAddress));

    /// <summary>Throws when a channel step targets a channel with no message.</summary>
    /// <param name="stepId">The step id.</param>
    /// <param name="channelAddress">The channel address.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowChannelNoMessage(object? stepId, object? channelAddress)
        => throw new NotSupportedException(SR.Format(SR.ChannelNoMessage, stepId, channelAddress));

    /// <summary>Creates the exception for a channel step with no requestBody payload, for the caller to throw.</summary>
    /// <param name="stepId">The step id.</param>
    /// <returns>The exception to throw.</returns>
    public static NotSupportedException GetNoRequestBodyToPublishException(object? stepId)
        => new(SR.Format(SR.NoRequestBodyToPublish, stepId));

    /// <summary>Creates the exception for a channel step binding an unsupported payload kind, for the caller to throw.</summary>
    /// <param name="stepId">The step id.</param>
    /// <param name="payloadKind">The unsupported payload kind.</param>
    /// <returns>The exception to throw.</returns>
    public static NotSupportedException GetUnsupportedPayloadKindException(object? stepId, object? payloadKind)
        => new(SR.Format(SR.UnsupportedPayloadKind, stepId, payloadKind));

    /// <summary>Throws when a message on a channel declares no headers schema for the set headers.</summary>
    /// <param name="stepId">The step id.</param>
    /// <param name="messageName">The message name.</param>
    /// <param name="channelAddress">The channel address.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowChannelMessageNoHeadersSchema(object? stepId, object? messageName, object? channelAddress)
        => throw new NotSupportedException(SR.Format(SR.ChannelMessageNoHeadersSchema, stepId, messageName, channelAddress));

    /// <summary>Creates the exception for a channel message that is not publishable, for the caller to throw.</summary>
    /// <param name="stepId">The step id.</param>
    /// <param name="channelAddress">The channel address.</param>
    /// <param name="messageName">The message name.</param>
    /// <returns>The exception to throw.</returns>
    public static NotSupportedException GetMessageNotPublishableException(object? stepId, object? channelAddress, object? messageName)
        => new(SR.Format(SR.MessageNotPublishable, stepId, channelAddress, messageName));

    /// <summary>Creates the exception for a channel with no publishable message, for the caller to throw.</summary>
    /// <param name="stepId">The step id.</param>
    /// <param name="channelAddress">The channel address.</param>
    /// <returns>The exception to throw.</returns>
    public static NotSupportedException GetChannelNoPublishableMessageException(object? stepId, object? channelAddress)
        => new(SR.Format(SR.ChannelNoPublishableMessage, stepId, channelAddress));

    /// <summary>Creates the exception for a request/reply channel message with no request/reply method, for the caller to throw.</summary>
    /// <param name="stepId">The step id.</param>
    /// <param name="channelAddress">The channel address.</param>
    /// <param name="messageName">The message name.</param>
    /// <returns>The exception to throw.</returns>
    public static NotSupportedException GetMessageNoRequestReplyMethodException(object? stepId, object? channelAddress, object? messageName)
        => new(SR.Format(SR.MessageNoRequestReplyMethod, stepId, channelAddress, messageName));

    /// <summary>Creates the exception for a request/reply channel with no request/reply method, for the caller to throw.</summary>
    /// <param name="stepId">The step id.</param>
    /// <param name="channelAddress">The channel address.</param>
    /// <returns>The exception to throw.</returns>
    public static NotSupportedException GetChannelNoRequestReplyMethodException(object? stepId, object? channelAddress)
        => new(SR.Format(SR.ChannelNoRequestReplyMethod, stepId, channelAddress));

    /// <summary>Creates the exception for a multi-message channel message with no distinct payload type, for the caller to throw.</summary>
    /// <param name="stepId">The step id.</param>
    /// <param name="channelAddress">The channel address.</param>
    /// <param name="messageName">The message name.</param>
    /// <returns>The exception to throw.</returns>
    public static NotSupportedException GetMultiMessageNoDistinctPayloadTypeException(object? stepId, object? channelAddress, object? messageName)
        => new(SR.Format(SR.MultiMessageNoDistinctPayloadType, stepId, channelAddress, messageName));

    /// <summary>Throws when a responder step declares a step-level correlationId.</summary>
    /// <param name="stepId">The step id.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowResponderCorrelationIdUnsupported(object? stepId)
        => throw new NotSupportedException(SR.Format(SR.ResponderCorrelationIdUnsupported, stepId));

    /// <summary>Creates the exception for a responder step with no requestBody, for the caller to throw.</summary>
    /// <param name="stepId">The step id.</param>
    /// <returns>The exception to throw.</returns>
    public static NotSupportedException GetResponderNoRequestBodyException(object? stepId)
        => new(SR.Format(SR.ResponderNoRequestBody, stepId));

    /// <summary>Throws when a responder reply value cannot be resolved.</summary>
    /// <param name="stepId">The step id.</param>
    /// <param name="value">The reply value.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowResponderReplyValueUnresolvable(object? stepId, object? value)
        => throw new NotSupportedException(SR.Format(SR.ResponderReplyValueUnresolvable, stepId, value));

    /// <summary>Creates the exception for a responder reply payload kind that is unsupported, for the caller to throw.</summary>
    /// <param name="stepId">The step id.</param>
    /// <param name="payloadKind">The unsupported reply payload kind.</param>
    /// <returns>The exception to throw.</returns>
    public static NotSupportedException GetUnsupportedReplyPayloadKindException(object? stepId, object? payloadKind)
        => new(SR.Format(SR.UnsupportedReplyPayloadKind, stepId, payloadKind));

    /// <summary>Throws when a receive channel step's criterion references a forbidden token.</summary>
    /// <param name="stepId">The step id.</param>
    /// <param name="token">The forbidden token.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowReceiveCriterionForbiddenReference(object? stepId, object? token)
        => throw new NotSupportedException(SR.Format(SR.ReceiveCriterionForbiddenReference, stepId, token));

    /// <summary>Throws when a step provides no value for a required parameter.</summary>
    /// <param name="parameterName">The required parameter's name.</param>
    /// <param name="operationName">The operation's identifier.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowNoValueForRequiredParameter(object? parameterName, object? operationName)
        => throw new InvalidOperationException(SR.Format(SR.NoValueForRequiredParameter, parameterName, operationName));

    /// <summary>Creates the exception for a request body kind unsupported as a replacement base, for the caller to throw.</summary>
    /// <param name="bodyKind">The unsupported body kind.</param>
    /// <returns>The exception to throw.</returns>
    public static NotSupportedException GetUnsupportedRequestBodyBaseKindException(object? bodyKind)
        => new(SR.Format(SR.UnsupportedRequestBodyBaseKind, bodyKind));

    /// <summary>Creates the exception for an argument value kind that cannot be resolved to an element, for the caller to throw.</summary>
    /// <param name="kind">The unsupported argument value kind.</param>
    /// <returns>The exception to throw.</returns>
    public static NotSupportedException GetUnresolvableArgumentValueKindException(object? kind)
        => new(SR.Format(SR.UnresolvableArgumentValueKind, kind));

    /// <summary>Creates the exception for a step missing its required stepId, for the caller to throw.</summary>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException GetStepMissingStepIdException()
        => new(SR.StepMissingStepId);

    /// <summary>Throws when a fire-and-forget send step declares success criteria.</summary>
    /// <param name="stepId">The step id.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowFireAndForgetSuccessCriteria(object? stepId)
        => throw new NotSupportedException(SR.Format(SR.FireAndForgetSuccessCriteria, stepId));

    /// <summary>Throws when a correlationId is declared on a step that is not a receive step.</summary>
    /// <param name="stepId">The step id.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowCorrelationIdNotReceiveStep(object? stepId)
        => throw new NotSupportedException(SR.Format(SR.CorrelationIdNotReceiveStep, stepId));

    /// <summary>Throws when a correlationId is declared on a request/reply (responder) step.</summary>
    /// <param name="stepId">The step id.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowCorrelationIdOnRequestReply(object? stepId)
        => throw new NotSupportedException(SR.Format(SR.CorrelationIdOnRequestReply, stepId));

    /// <summary>Creates the exception for a correlationId with no matching correlation id in the message, for the caller to throw.</summary>
    /// <param name="stepId">The step id.</param>
    /// <param name="correlationName">The correlationId name.</param>
    /// <returns>The exception to throw.</returns>
    public static NotSupportedException GetCorrelationIdNotDefinedException(object? stepId, object? correlationName)
        => new(SR.Format(SR.CorrelationIdNotDefined, stepId, correlationName));

    /// <summary>Creates the exception for a correlationId with an unsupported location, for the caller to throw.</summary>
    /// <param name="stepId">The step id.</param>
    /// <param name="correlationName">The correlationId name.</param>
    /// <param name="rawLocation">The declared location.</param>
    /// <returns>The exception to throw.</returns>
    public static NotSupportedException GetUnsupportedCorrelationLocationException(object? stepId, object? correlationName, object? rawLocation)
        => new(SR.Format(SR.UnsupportedCorrelationLocation, stepId, correlationName, rawLocation));

    /// <summary>Creates the exception for a step whose target kind is unsupported, for the caller to throw.</summary>
    /// <param name="stepId">The step id.</param>
    /// <param name="bindingKind">The step's target kind.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException GetUnsupportedStepTargetException(object? stepId, object? bindingKind)
        => new(SR.Format(SR.UnsupportedStepTarget, stepId, bindingKind));

    /// <summary>Creates the exception for a channel source that declares no transport protocol, for the caller to throw.</summary>
    /// <param name="sourceName">The channel source name.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException GetChannelSourceNoProtocolException(object? sourceName)
        => new(SR.Format(SR.ChannelSourceNoProtocol, sourceName));

    /// <summary>Creates the exception for an unresolved reusable-parameter reference, for the caller to throw.</summary>
    /// <param name="reference">The reference.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException GetUnresolvedParameterReferenceException(object? reference)
        => new(SR.Format(SR.UnresolvedParameterReference, reference));

    /// <summary>Creates the exception for an unresolved reusable-action reference, for the caller to throw.</summary>
    /// <param name="reference">The reference.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException GetUnresolvedActionReferenceException(object? reference)
        => new(SR.Format(SR.UnresolvedActionReference, reference));

    /// <summary>Throws when a sub-workflow step's criterion references a forbidden token.</summary>
    /// <param name="stepId">The step id.</param>
    /// <param name="token">The forbidden token.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSubWorkflowCriterionForbiddenReference(object? stepId, object? token)
        => throw new NotSupportedException(SR.Format(SR.SubWorkflowCriterionForbiddenReference, stepId, token));

    /// <summary>Throws when a cycle is detected in the steps' dependency relationships.</summary>
    /// <param name="implicitNote">A trailing note about implicit dependencies contributing to the cycle (may be empty).</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowStepDependencyCycle(object? implicitNote)
        => throw new InvalidOperationException(SR.Format(SR.StepDependencyCycle, implicitNote));

    /// <summary>Creates the exception for a sub-workflow source that is not a generated Arazzo source, for the caller to throw.</summary>
    /// <param name="subWorkflowSource">The source description name.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException GetSubWorkflowSourceNotGeneratedException(object? subWorkflowSource)
        => new(SR.Format(SR.SubWorkflowSourceNotGenerated, subWorkflowSource));

    /// <summary>Creates the exception for a channel address parameter with no matching step parameter, for the caller to throw.</summary>
    /// <param name="name">The parameter name.</param>
    /// <returns>The exception to throw.</returns>
    public static NotSupportedException GetChannelParameterNoStepParameterException(object? name)
        => new(SR.Format(SR.ChannelParameterNoStepParameter, name));

    /// <summary>Creates the exception for a channel address parameter binding an unsupported value kind, for the caller to throw.</summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="parameterKind">The unsupported value kind.</param>
    /// <returns>The exception to throw.</returns>
    public static NotSupportedException GetUnsupportedChannelParameterKindException(object? name, object? parameterKind)
        => new(SR.Format(SR.UnsupportedChannelParameterKind, name, parameterKind));

    /// <summary>Throws when a channel step's action criterion references a forbidden token.</summary>
    /// <param name="stepId">The step id.</param>
    /// <param name="token">The forbidden token.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowChannelActionCriterionForbiddenReference(object? stepId, object? token)
        => throw new NotSupportedException(SR.Format(SR.ChannelActionCriterionForbiddenReference, stepId, token));

    /// <summary>Creates the exception for a goto to an unknown step, for the caller to throw.</summary>
    /// <param name="actionName">The action name.</param>
    /// <param name="targetStepId">The target step id.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException GetGotoUnknownStepException(object? actionName, object? targetStepId)
        => new(SR.Format(SR.GotoUnknownStep, actionName, targetStepId));

    /// <summary>Creates the exception for a workflow missing its required workflowId, for the caller to throw.</summary>
    /// <param name="index">The workflow's index.</param>
    /// <returns>The exception to throw.</returns>
    public static InvalidOperationException GetWorkflowMissingIdException(object? index)
        => new(SR.Format(SR.WorkflowMissingId, index));

    /// <summary>Throws when a cycle is detected in the workflows' dependsOn relationships.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowWorkflowDependencyCycle()
        => throw new InvalidOperationException(SR.WorkflowDependencyCycle);

    /// <summary>Throws when an interpolated value fragment references an unresolvable value.</summary>
    /// <param name="context">The step context.</param>
    /// <param name="template">The interpolated fragment.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowInterpolatedValueUnresolvable(object? context, object? template)
        => throw new NotSupportedException(SR.Format(SR.InterpolatedValueUnresolvable, context, template));

    /// <summary>Creates the exception for a step value that cannot be resolved, for the caller to throw.</summary>
    /// <param name="context">The step context.</param>
    /// <param name="value">The value.</param>
    /// <returns>The exception to throw.</returns>
    public static NotSupportedException GetUnresolvableStepValueException(object? context, object? value)
        => new(SR.Format(SR.UnresolvableStepValue, context, value));
}