// <copyright file="Utf8UriParser.BuiltInParser.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
namespace Corvus.Text.Json.Internal;

internal partial class Utf8UriParser
{
    /// <summary>
    /// Provides a built-in URI parser for well-known URI schemes.
    /// </summary>
    private sealed class BuiltInUriParser : Utf8UriParser
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BuiltInUriParser"/> class.
        /// All built-in parsers use this constructor. They are marked with "simple" and "built-in" flags.
        /// </summary>
        /// <param name="lwrCaseScheme">The lowercase scheme name.</param>
        /// <param name="defaultPort">The default port for the scheme.</param>
        /// <param name="syntaxFlags">The syntax flags for the scheme.</param>
        internal BuiltInUriParser(string lwrCaseScheme, int defaultPort, Utf8UriSyntaxFlags syntaxFlags)
            : base(syntaxFlags | Utf8UriSyntaxFlags.SimpleUserSyntax)
        {
            _scheme = lwrCaseScheme;
            _port = defaultPort;
        }
    }
}