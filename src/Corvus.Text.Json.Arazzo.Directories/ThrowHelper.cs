// <copyright file="ThrowHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Directories;

/// <summary>
/// Centralized exception-throwing helpers for the directory search seam.
/// </summary>
/// <remarks>
/// <para>
/// Helpers used from a value-producing expression are <c>Get*Exception</c> factories the caller throws. All exception messages come from the embedded <c>Resources/Strings.resx</c> resource file via <c>SR</c>.
/// </para>
/// </remarks>
internal static class ThrowHelper
{
    /// <summary>Creates the exception for the string map path being invoked on a span-only identity mapper, for the caller to throw.</summary>
    /// <returns>The exception to throw.</returns>
    public static NotSupportedException GetSpanMapperMapNotSupportedException()
        => new(SR.SpanMapperMapNotSupported);
}