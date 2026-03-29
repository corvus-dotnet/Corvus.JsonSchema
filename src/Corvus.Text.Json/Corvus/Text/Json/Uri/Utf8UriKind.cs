// <copyright file="Utf8UriKind.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
namespace Corvus.Text.Json.Internal;

/// <summary>
/// Defines the kind of URI, controlling whether absolute or relative URIs are used.
/// </summary>
public enum Utf8UriKind
{
    /// <summary>
    /// The kind of URI is indeterminate. The URI can be either relative or absolute.
    /// </summary>
    RelativeOrAbsolute = 0,

    /// <summary>
    /// The URI is an absolute URI.
    /// </summary>
    Absolute = 1,

    /// <summary>
    /// The URI is a relative URI.
    /// </summary>
    Relative = 2
}