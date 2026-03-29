// <copyright file="Utf8UriSyntaxFlags.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
namespace Corvus.Text.Json;

/// <summary>
/// Specifies the URI syntax flags that are understood by the built-in URI parser.
/// </summary>
[Flags]
internal enum Utf8UriSyntaxFlags
{
    None = 0x0,

    MustHaveAuthority = 0x1,  // must have "// " after scheme:
    OptionalAuthority = 0x2,  // used by generic parser due to unknown Uri syntax
    MayHaveUserInfo = 0x4,
    MayHavePort = 0x8,
    MayHavePath = 0x10,
    MayHaveQuery = 0x20,
    MayHaveFragment = 0x40,

    AllowEmptyHost = 0x80,
    AllowUncHost = 0x100,
    AllowDnsHost = 0x200,
    AllowIPv4Host = 0x400,
    AllowIPv6Host = 0x800,
    AllowAnInternetHost = AllowDnsHost | AllowIPv4Host | AllowIPv6Host,
    AllowAnyOtherHost = 0x1000, // Relaxed authority syntax

    FileLikeUri = 0x2000, // Special case to allow file:\\balbla or file:// \\balbla
    MailToLikeUri = 0x4000, // V1 parser inheritance mailTo:AuthorityButNoSlashes

    V1_UnknownUri = 0x10000, // a Compatibility with V1 parser for an unknown scheme
    SimpleUserSyntax = 0x20000, // It is safe to not call virtual UriParser methods

    AllowDOSPath = 0x100000,  // will check for "x:\"
    PathIsRooted = 0x200000,  // For an authority based Uri the first path char is '/'
    ConvertPathSlashes = 0x400000,  // will turn '\' into '/'
    CompressPath = 0x800000,  // For an authority based Uri remove/compress /./ /../ in the path
    CanonicalizeAsFilePath = 0x1000000, // remove/convert sequences /.../ /x../ /x./ dangerous for a DOS path
    UnEscapeDotsAndSlashes = 0x2000000, // additionally unescape dots and slashes before doing path compression
    AllowIdn = 0x4000000,    // IDN host conversion allowed
    AllowIriParsing = 0x10000000,   // Iri parsing. String is normalized, bidi control

    // characters are removed, unicode char limits are checked etc.
}