// <copyright file="Utf8UriComponents.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
namespace Corvus.Text.Json.Internal;

/// <summary>
/// Specifies the parts of a URI that should be included when retrieving URI components.
/// </summary>
[Flags]
public enum Utf8UriComponents
{
    /// <summary>
    /// The scheme part of the URI.
    /// </summary>
    Scheme = 0x1,

    /// <summary>
    /// The user information part of the URI.
    /// </summary>
    UserInfo = 0x2,

    /// <summary>
    /// The host part of the URI.
    /// </summary>
    Host = 0x4,

    /// <summary>
    /// The port part of the URI.
    /// </summary>
    Port = 0x8,

    /// <summary>
    /// The path part of the URI.
    /// </summary>
    Path = 0x10,

    /// <summary>
    /// The query part of the URI.
    /// </summary>
    Query = 0x20,

    /// <summary>
    /// The fragment part of the URI.
    /// </summary>
    Fragment = 0x40,

    /// <summary>
    /// The port part of the URI, including default ports.
    /// </summary>
    StrongPort = 0x80,

    /// <summary>
    /// The normalized host part of the URI.
    /// </summary>
    NormalizedHost = 0x100,

    /// <summary>
    /// This will also return respective delimiters for scheme, userinfo or port.
    /// Valid only for a single component requests.
    /// </summary>
    KeepDelimiter = 0x40000000,

    /// <summary>
    /// This is used by GetObjectData and can also be used directly.
    /// Works for both absolute and relative URIs.
    /// </summary>
    SerializationInfoString = unchecked((int)0x80000000),

    /// <summary>
    /// All components of an absolute URI.
    /// </summary>
    AbsoluteUri = Scheme | UserInfo | Host | Port | Path | Query | Fragment,

    /// <summary>
    /// The host and port components, including default ports.
    /// </summary>
    HostAndPort = Host | StrongPort,

    /// <summary>
    /// The user info, host, and port components, including default ports.
    /// </summary>
    StrongAuthority = UserInfo | Host | StrongPort,

    /// <summary>
    /// The scheme, host, and port components.
    /// </summary>
    SchemeAndServer = Scheme | Host | Port,

    /// <summary>
    /// The components typically used in HTTP request URLs.
    /// </summary>
    HttpRequestUrl = Scheme | Host | Port | Path | Query,

    /// <summary>
    /// The path and query components.
    /// </summary>
    PathAndQuery = Path | Query,
}