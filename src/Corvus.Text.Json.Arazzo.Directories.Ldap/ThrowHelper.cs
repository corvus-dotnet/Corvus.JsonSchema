// <copyright file="ThrowHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Corvus.Text.Json.Arazzo.Directories.Ldap;

/// <summary>
/// Centralized exception-throwing helpers for the LDAP directory adapter.
/// </summary>
/// <remarks>
/// <para>
/// Guard-position helpers are <c>Throw*</c> methods marked <see cref="DoesNotReturnAttribute"/> so the JIT can optimize call-site code after a throw. All exception messages come from the embedded <c>Resources/Strings.resx</c> resource file via <c>SR</c>.
/// </para>
/// </remarks>
internal static class ThrowHelper
{
    /// <summary>Throws when the configured LDAP bind method is not supported.</summary>
    /// <param name="bindMethodTypeName">The unsupported bind method type name.</param>
    public static InvalidOperationException GetUnsupportedBindMethodException(string bindMethodTypeName)
        => new(SR.Format(SR.UnsupportedBindMethod, bindMethodTypeName));
}