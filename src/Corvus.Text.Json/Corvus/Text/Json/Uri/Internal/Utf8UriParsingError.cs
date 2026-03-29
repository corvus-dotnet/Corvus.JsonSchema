// <copyright file="Utf8UriParsingError.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
namespace Corvus.Text.Json.Internal;

/// <summary>
/// Defines the types of errors that can occur during UTF-8 URI parsing.
/// </summary>
internal enum Utf8UriParsingError
{
    // looks good
    None = 0,

    // These first errors indicate that the Uri cannot be absolute, but may be relative.
    BadFormat = 1,

    BadScheme = 2,
    BadAuthority = 3,
    EmptyUriString = 4,
    LastRelativeUriOkErrIndex = 4,

    // All higher error values are fatal, indicating that neither an absolute or relative
    // Uri could be generated.
    SchemeLimit = 5,

    SizeLimit = 6,
    MustRootedPath = 7,

    // derived class controlled
    BadHostName = 8,

    NonEmptyHost = 9, // unix only
    BadPort = 10,
    BadAuthorityTerminator = 11,

    // The user requested only a relative Uri, but an absolute Uri was parsed.
    CannotCreateRelative = 12
}