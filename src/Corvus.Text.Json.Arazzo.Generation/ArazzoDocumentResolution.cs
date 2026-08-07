// <copyright file="ArazzoDocumentResolution.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Generation;

/// <summary>
/// What a document loader may reach for a reference it cannot satisfy from the registered documents.
/// </summary>
/// <remarks>
/// <para>
/// This is a stated choice rather than a consequence of whether a registry happened to be supplied, because the two
/// callers want opposite things and used to share one behaviour. The control plane compiles an attacker-authored
/// package in its own process, so for it a reference outside the package is the whole attack: a
/// <c>sourceDescriptions[].url</c> or <c>$self</c> naming <c>file:///etc/</c> or a cloud instance-metadata address
/// turns into a request the control plane makes on the author's behalf, and the content comes back through the
/// generated models and the build error. A developer tool pointed at a document tree by its operator is resolving
/// exactly what it was asked to resolve.
/// </para>
/// <para>
/// <see cref="RegisteredOnly"/> is deliberately the zero value, so <see langword="default"/> is the closed one. An
/// option whose default is the permissive value is how a control ends up switched off wherever someone forgot to name
/// it.
/// </para>
/// </remarks>
public enum ArazzoDocumentResolution
{
    /// <summary>
    /// Registered documents only. A reference to anything else resolves to nothing, and generation fails naming the
    /// reference it could not satisfy. This is correct whenever the caller has assembled the whole document set
    /// itself, which is what a self-contained package is.
    /// </summary>
    RegisteredOnly = 0,

    /// <summary>
    /// Registered documents, then the local file system, then <c>http(s)</c>. For a tool resolving a document tree its
    /// operator chose. Do not use it for input that arrives over an API.
    /// </summary>
    Retrieved = 1,
}