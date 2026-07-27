// <copyright file="ThrowHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Corvus.Text.Json.Arazzo.Generation;

/// <summary>
/// Centralized exception-throwing helpers for the Arazzo generation driver.
/// </summary>
/// <remarks>
/// <para>
/// Guard-position helpers are <c>Throw*</c> methods marked <see cref="DoesNotReturnAttribute"/> so the JIT can optimize call-site code after a throw; expression-position helpers used from a <c>??</c> or ternary are <c>Get*Exception</c> factories the caller throws. All exception messages come from the embedded <c>Resources/Strings.resx</c> resource file via <c>SR</c>.
/// </para>
/// </remarks>
internal static class ThrowHelper
{
    /// <summary>Creates the exception for an AsyncAPI source document that could not be loaded, for the caller to throw.</summary>
    /// <param name="specUri">The source document URI that could not be loaded.</param>
    /// <returns>The exception to throw.</returns>
    public static FileNotFoundException GetAsyncApiSourceDocumentNotFoundException(Uri specUri)
        => new(SR.Format(SR.AsyncApiSourceDocumentNotFound, specUri));

    /// <summary>Throws when an AsyncAPI source is not a supported version.</summary>
    /// <param name="schemaEntryKey">The source description key.</param>
    /// <param name="version">The detected AsyncAPI version.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowUnsupportedAsyncApiVersion(string schemaEntryKey, string version)
        => throw new NotSupportedException(SR.Format(SR.UnsupportedAsyncApiVersion, schemaEntryKey, version));

    /// <summary>Creates the exception for an OpenAPI source document that could not be loaded, for the caller to throw.</summary>
    /// <param name="specUri">The source document URI that could not be loaded.</param>
    /// <returns>The exception to throw.</returns>
    public static FileNotFoundException GetOpenApiSourceDocumentNotFoundException(Uri specUri)
        => new(SR.Format(SR.OpenApiSourceDocumentNotFound, specUri));

    /// <summary>Creates the exception for an Arazzo document that could not be loaded, for the caller to throw.</summary>
    /// <param name="arazzoRetrievalUri">The Arazzo document URI that could not be loaded.</param>
    /// <returns>The exception to throw.</returns>
    public static FileNotFoundException GetArazzoDocumentNotFoundException(Uri arazzoRetrievalUri)
        => new(SR.Format(SR.ArazzoDocumentNotFound, arazzoRetrievalUri));

    /// <summary>Creates the exception for an Arazzo source document that could not be loaded, for the caller to throw.</summary>
    /// <param name="specUri">The Arazzo source document URI that could not be loaded.</param>
    /// <returns>The exception to throw.</returns>
    public static FileNotFoundException GetArazzoSourceDocumentNotFoundException(Uri specUri)
        => new(SR.Format(SR.ArazzoSourceDocumentNotFound, specUri));

    /// <summary>Throws when a cyclic Arazzo source-description reference is detected.</summary>
    /// <param name="childBaseUri">The base URI at which the cycle was detected.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowCyclicArazzoSourceReference(Uri childBaseUri)
        => throw new InvalidOperationException(SR.Format(SR.CyclicArazzoSourceReference, childBaseUri));

    /// <summary>Throws when an unexpected duplicate output file is generated.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowUnexpectedDuplicateFile()
        => throw new InvalidOperationException(SR.UnexpectedDuplicateFile);
}