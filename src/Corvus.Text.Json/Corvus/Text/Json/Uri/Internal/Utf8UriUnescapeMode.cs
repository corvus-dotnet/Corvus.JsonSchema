// <copyright file="Utf8UriUnescapeMode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
namespace Corvus.Text.Json.Internal;

/// <summary>
/// Specifies the URI unescaping mode options.
/// </summary>
[Flags]
internal enum Utf8UriUnescapeMode
{
    CopyOnly = 0x0,                          // used for V1.0 ToString() compatibility mode only
    Escape = 0x1,                            // Only used by ImplicitFile, the string is already fully unescaped
    Unescape = 0x2,                          // Only used as V1.0 UserEscaped compatibility mode
    EscapeUnescape = Unescape | Escape,      // does both escaping control+reserved and unescaping of safe characters
    V1ToStringFlag = 0x4,                    // Only used as V1.0 ToString() compatibility mode, assumes DontEscape level also
    UnescapeAll = 0x8,                       // just unescape everything, leave bad escaped sequences as is
}