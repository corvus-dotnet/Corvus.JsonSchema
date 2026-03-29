// <copyright file="Utf8UriFormat.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
namespace Corvus.Text.Json.Internal;

/// <summary>
/// Specifies the format options for URI string representation.
/// </summary>
public enum Utf8UriFormat
{
    /// <summary>
    /// The URI is represented with URI escaping applied.
    /// </summary>
    UriEscaped = 1,

    /// <summary>
    /// The URI is completely unescaped.
    /// </summary>
    Unescaped = 2,

    /// <summary>
    /// The URI is canonically unescaped, allowing the same URI to be reconstructed from the output.
    /// If the unescaped sequence results in a new escaped sequence, it will revert to the original sequence.
    /// </summary>
    SafeUnescaped = 3

    // This value is reserved for the default ToString() format that is historically none of the above.
    // V1ToStringUnescape = 0x7FFF
}