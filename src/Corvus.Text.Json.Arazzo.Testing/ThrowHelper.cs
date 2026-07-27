// <copyright file="ThrowHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Corvus.Text.Json.Arazzo.Testing;

/// <summary>
/// Centralized exception-throwing helpers for the Arazzo testing support.
/// </summary>
/// <remarks>
/// <para>
/// Guard-position helpers are <c>Throw*</c> methods marked <see cref="DoesNotReturnAttribute"/> so the JIT can optimize call-site code after a throw. All exception messages come from the embedded <c>Resources/Strings.resx</c> resource file via <c>SR</c>.
/// </para>
/// </remarks>
internal static class ThrowHelper
{
    /// <summary>Throws when a request/reply send has no scripted reply in the scenario.</summary>
    /// <param name="requestChannel">The request channel that was sent on.</param>
    /// <param name="replyChannel">The reply channel with no scripted trigger.</param>
    public static InvalidOperationException GetNoScriptedReplyException(string requestChannel, string replyChannel)
        => new(SR.Format(SR.NoScriptedReply, requestChannel, replyChannel));
}