// <copyright file="Utf8Iri.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics.CodeAnalysis;
using Corvus.Text.Json.Internal;
using static Corvus.Text.Json.JsonSchemaResultsCollector;

namespace Corvus.Text.Json;

/// <summary>
/// A UTF-8 IRI.
/// </summary>
/// <remarks>
/// <code>
/// <![CDATA[
/// foo:// user@example.com:8042/over/there?name= ferret#nose
/// \_/   \___________________/\_________/ \_________/ \__/
/// |           |                 |           |       |
/// scheme     authority           path       query  fragment
/// \___/\______________/
/// |        |
/// user      host (including port)
/// ]]>
/// </code>
/// </remarks>
public readonly ref struct Utf8Iri
{
    internal readonly Utf8UriTools.Flags _flags;

    internal readonly Utf8UriOffset _offsets;

    internal readonly ReadOnlySpan<byte> _originalIri;

    private Utf8Iri(ReadOnlySpan<byte> iri)
    {
        _originalIri = iri;
        IsValid = Utf8UriTools.ParseUriInfo(_originalIri, Utf8UriKind.Absolute, requireAbsolute: true, allowIri: true, out _offsets, out _flags);
    }

    private Utf8Iri(ReadOnlyMemory<byte> originalIri, Utf8UriOffset offsets, Utf8UriTools.Flags flags)
        : this()
    {
        _offsets = offsets;
        _flags = flags;
        _originalIri = originalIri.Span;
    }

    internal static Utf8Iri CreateIriUnsafe(ReadOnlyMemory<byte> iri, Utf8UriOffset offsets, Utf8UriTools.Flags flags)
    {
        return new(iri, offsets, flags);
    }

    /// <summary>
    /// Gets the authority component of the reference.
    /// </summary>
    public ReadOnlySpan<byte> Authority => _originalIri.Slice(_offsets.User, _offsets.Path - _offsets.User);

    /// <summary>
    /// Gets the fragment component of the reference.
    /// </summary>
    public ReadOnlySpan<byte> Fragment => HasFragment ? _originalIri.Slice(_offsets.Fragment + 1, _offsets.End - _offsets.Fragment - 1) : [];

    /// <summary>
    /// Gets a value indicating whether this reference has an authority.
    /// </summary>
    public bool HasAuthority => _offsets.Path - _offsets.User > 0;

    /// <summary>
    /// Gets a value indicating whether this reference has a fragment.
    /// </summary>
    public bool HasFragment => _offsets.End - _offsets.Fragment > 0;

    /// <summary>
    /// Gets a value indicating whether this reference has a host.
    /// </summary>
    public bool HasHost => _offsets.Path - _offsets.Host > 0;

    /// <summary>
    /// Gets a value indicating whether this reference has a path.
    /// </summary>
    public bool HasPath => _offsets.Query - _offsets.Path > 0;

    /// <summary>
    /// Gets a value indicating whether this reference has a port.
    /// </summary>
    public bool HasPort => _offsets.Port > 0 && (_offsets.Path - _offsets.Port > 0);

    /// <summary>
    /// Gets a value indicating whether this reference has a query.
    /// </summary>
    public bool HasQuery => _offsets.Fragment - _offsets.Query > 0;

    /// <summary>
    /// Gets a value indicating whether this reference has a scheme.
    /// </summary>
    public bool HasScheme => _offsets.User - _offsets.Scheme > 0;

    /// <summary>
    /// Gets a value indicating whether this reference has a user.
    /// </summary>
    public bool HasUser => _offsets.Host - _offsets.User > 0;

    /// <summary>
    /// Gets the host component of the reference (includes both host and port).
    /// </summary>
    public ReadOnlySpan<byte> Host => HasPort ? _originalIri.Slice(_offsets.Host, _offsets.Port - _offsets.Host) : _originalIri.Slice(_offsets.Host, _offsets.Path - _offsets.Host);

    /// <summary>
    /// Gets a value indicating whether this is the default port for the scheme.
    /// </summary>
    public bool IsDefaultPort => (_flags & Utf8UriTools.Flags.NotDefaultPort) == 0;

    /// <summary>
    /// Gets a value indicating whether this is a relative IRI.
    /// </summary>
    public bool IsRelative => !HasScheme;

    /// <summary>
    /// Gets a value indicating whether this is a valid IRI.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Gets the original (fully encoded) string.
    /// </summary>
    public ReadOnlySpan<byte> OriginalIri => _originalIri;

    /// <summary>
    /// Gets the path component of the IRI.
    /// </summary>
    public ReadOnlySpan<byte> Path => HasPath ? _originalIri.Slice(_offsets.Path, _offsets.Query - _offsets.Path) : [];

    /// <summary>
    /// Gets the port component of the IRI as a byte span.
    /// </summary>
    public ReadOnlySpan<byte> Port => HasPort ? _originalIri.Slice(_offsets.Port + 1, _offsets.Path - _offsets.Port - 1) : [];

    /// <summary>
    /// Gets the port value as an integer.
    /// </summary>
    public int PortValue => _offsets.PortValue;

    /// <summary>
    /// Gets the query component of the IRI.
    /// </summary>
    public ReadOnlySpan<byte> Query => HasQuery ? _originalIri.Slice(_offsets.Query + 1, _offsets.Fragment - _offsets.Query - 1) : [];

    /// <summary>
    /// Gets the scheme component of the IRI.
    /// </summary>
    public ReadOnlySpan<byte> Scheme
    {
        get
        {
            if (!HasScheme)
            {
                return [];
            }

            // Distinguish between ':' and ':// '
            if (_originalIri[_offsets.User - 1] == '/')
            {
                return _originalIri.Slice(_offsets.Scheme, _offsets.User - _offsets.Scheme - 3);
            }

            return _originalIri.Slice(_offsets.Scheme, _offsets.User - _offsets.Scheme - 1);
        }
    }

    /// <summary>
    /// Gets the user component of the IRI.
    /// </summary>
    public ReadOnlySpan<byte> User => HasUser ? _originalIri.Slice(_offsets.User, _offsets.Host - _offsets.User - 1) : [];

    /// <summary>
    /// Creates a new UTF-8 IRI from the specified IRI bytes.
    /// </summary>
    /// <param name="iri">The IRI bytes from which to create the UTF-8 IRI.</param>
    /// <returns>A new UTF-8 IRI.</returns>
    /// <exception cref="ArgumentException">Thrown when the IRI is invalid.</exception>
    public static Utf8Iri CreateIri(ReadOnlySpan<byte> iri)
    {
        if (!TryCreateIri(iri, out Utf8Iri reference))
        {
            ThrowHelper.ThrowArgumentException(SR.InvalidJsonReference);
        }

        return reference;
    }

    /// <summary>
    /// Tries to create a new UTF-8 IRI from the specified IRI bytes.
    /// </summary>
    /// <param name="iri">The IRI bytes from which to create the UTF-8 IRI.</param>
    /// <param name="utf8Iri">When this method returns, contains the created UTF-8 IRI if successful; otherwise, the default value.</param>
    /// <returns><see langword="true"/> if the UTF-8 IRI was created successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryCreateIri(ReadOnlySpan<byte> iri, out Utf8Iri utf8Iri)
    {
        utf8Iri = new(iri);
        return utf8Iri.IsValid;
    }

    /// <summary>
    /// Gets the value as a <see cref="Uri"/>.
    /// </summary>
    /// <returns>The URI representation of the UTF-8 IRI.</returns>
    public Uri GetUri()
    {
        return new Uri(JsonReaderHelper.TranscodeHelper(_originalIri), UriKind.Absolute);
    }

    /// <summary>
    /// Gets the IRI in canonical form for display.
    /// </summary>
    /// <param name="buffer">The buffer into which to write the result in canonical form with the encoded characters decoded for display.</param>
    /// <param name="writtenBytes">The number of bytes written.</param>
    /// <returns><see langword="true"/> if the result was successfully written to the buffer; otherwise, <see langword="false"/>.</returns>
    public bool TryFormatDisplay(Span<byte> buffer, out int writtenBytes)
    {
        return Utf8UriTools.TryFormatDisplay(_originalIri, _offsets, _flags, buffer, out writtenBytes);
    }

    /// <summary>
    /// Gets the IRI in canonical form.
    /// </summary>
    /// <param name="buffer">The buffer into which to write the result in canonical form with reserved characters encoded.</param>
    /// <param name="writtenBytes">The number of bytes written.</param>
    /// <returns><see langword="true"/> if the result was successfully written to the buffer; otherwise, <see langword="false"/>.</returns>
    public bool TryFormatCanonical(Span<byte> buffer, out int writtenBytes)
    {
        return Utf8UriTools.TryFormatCanonical(_originalIri, _offsets, _flags, allowIri: true, buffer, out writtenBytes);
    }

    /// <summary>
    /// Applies the given IRI to the current (base) IRI and writes the result to the provided buffer.
    /// It uses the rules of RFC 3986 Section 5.2 to resolve the reference against the base IRI, including handling
    /// of relative references and merging of paths as needed.
    /// </summary>
    /// <param name="iri">The IRI to apply.</param>
    /// <param name="buffer">The buffer to which to write the backing for the result. This needs to have a lifetime scoped to that
    /// of the resulting reference.</param>
    /// <param name="result">The resulting IRI.</param>
    /// <returns><see langword="true"/> if the result was successfully written and produced a valid IRI; otherwise, <see langword="false"/>.</returns>
    public bool TryApply(in Utf8Iri iri, Span<byte> buffer, out Utf8Iri result)
    {
        if (Utf8UriTools.TryApply(_originalIri, _offsets, _flags, iri._originalIri, iri._offsets, iri._flags, buffer, out int writtenBytes))
        {
            return TryCreateIri(buffer.Slice(0, writtenBytes), out result);
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Applies the given IRI reference to the current (base) IRI and writes the result to the provided buffer.
    /// It uses the rules of RFC 3986 Section 5.2 to resolve the reference against the base IRI, including handling
    /// of relative references and merging of paths as needed.
    /// </summary>
    /// <param name="iriReference">The IRI reference to apply.</param>
    /// <param name="buffer">The buffer to which to write the backing for the result. This needs to have a lifetime scoped to that
    /// of the resulting reference.</param>
    /// <param name="result">The resulting IRI.</param>
    /// <returns><see langword="true"/> if the result was successfully written and produced a valid IRI; otherwise, <see langword="false"/>.</returns>
    public bool TryApply(in Utf8IriReference iriReference, Span<byte> buffer, out Utf8Iri result)
    {
        if (Utf8UriTools.TryApply(_originalIri, _offsets, _flags, iriReference._originalIriReference, iriReference._offsets, iriReference._flags, buffer, out int writtenBytes))
        {
            return TryCreateIri(buffer.Slice(0, writtenBytes), out result);
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Applies the given URI reference to the current (base) IRI and writes the result to the provided buffer.
    /// It uses the rules of RFC 3986 Section 5.2 to resolve the reference against the base IRI, including handling
    /// of relative references and merging of paths as needed.
    /// </summary>
    /// <param name="uriReference">The IRI to apply.</param>
    /// <param name="buffer">The buffer to which to write the backing for the result. This needs to have a lifetime scoped to that
    /// of the resulting reference.</param>
    /// <param name="result">The resulting IRI.</param>
    /// <returns><see langword="true"/> if the result was successfully written and produced a valid IRI; otherwise, <see langword="false"/>.</returns>
    public bool TryApply(in Utf8UriReference uriReference, Span<byte> buffer, out Utf8Iri result)
    {
        if (Utf8UriTools.TryApply(_originalIri, _offsets, _flags, uriReference._originalUriReference, uriReference._offsets, uriReference._flags, buffer, out int writtenBytes))
        {
            return TryCreateIri(buffer.Slice(0, writtenBytes), out result);
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Applies the given URI reference to the current (base) IRI and writes the result to the provided buffer.
    /// It uses the rules of RFC 3986 Section 5.2 to resolve the reference against the base IRI, including handling
    /// of relative references and merging of paths as needed.
    /// </summary>
    /// <param name="uri">The IRI to apply.</param>
    /// <param name="buffer">The buffer to which to write the backing for the result. This needs to have a lifetime scoped to that
    /// of the resulting reference.</param>
    /// <param name="result">The resulting IRI.</param>
    /// <returns><see langword="true"/> if the result was successfully written and produced a valid IRI; otherwise, <see langword="false"/>.</returns>
    public bool TryApply(in Utf8Uri uri, Span<byte> buffer, out Utf8Iri result)
    {
        if (Utf8UriTools.TryApply(_originalIri, _offsets, _flags, uri._originalUri, uri._offsets, uri._flags, buffer, out int writtenBytes))
        {
            return TryCreateIri(buffer.Slice(0, writtenBytes), out result);
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Makes a relative IRI reference from the current (base) IRI to the target IRI.
    /// If the scheme, host, and port match, a relative reference is created; otherwise,
    /// the full target IRI is returned.
    /// </summary>
    /// <param name="targetIri">The target IRI to make relative.</param>
    /// <param name="buffer">The buffer to which to write the backing for the result. This needs to have a lifetime scoped to that
    /// of the resulting reference.</param>
    /// <param name="result">The resulting IRI reference (relative or absolute).</param>
    /// <returns><see langword="true"/> if the result was successfully written; otherwise, <see langword="false"/>.</returns>
    public bool TryMakeRelative(in Utf8Iri targetIri, Span<byte> buffer, out Utf8IriReference result)
    {
        if (Utf8UriTools.MakeRelative(_originalIri, _offsets, _flags, targetIri._originalIri, targetIri._offsets, targetIri._flags, buffer, out int writtenBytes, allowIri: true))
        {
            return Utf8IriReference.TryCreateIriReference(buffer.Slice(0, writtenBytes), out result);
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Makes a relative IRI reference from the current (base) IRI to the target URI.
    /// If the scheme, host, and port match, a relative reference is created; otherwise,
    /// the full target URI is returned.
    /// </summary>
    /// <param name="targetUri">The target URI to make relative.</param>
    /// <param name="buffer">The buffer to which to write the backing for the result. This needs to have a lifetime scoped to that
    /// of the resulting reference.</param>
    /// <param name="result">The resulting IRI reference (relative or absolute).</param>
    /// <returns><see langword="true"/> if the result was successfully written; otherwise, <see langword="false"/>.</returns>
    public bool TryMakeRelative(in Utf8Uri targetUri, Span<byte> buffer, out Utf8IriReference result)
    {
        if (Utf8UriTools.MakeRelative(_originalIri, _offsets, _flags, targetUri._originalUri, targetUri._offsets, targetUri._flags, buffer, out int writtenBytes, allowIri: true))
        {
            return Utf8IriReference.TryCreateIriReference(buffer.Slice(0, writtenBytes), out result);
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Returns a string representation of the IRI in display format.
    /// </summary>
    /// <returns>A string representation of the IRI.</returns>
    public override string ToString()
    {
        Span<byte> buffer = stackalloc byte[2048];
        if (TryFormatDisplay(buffer, out int written))
        {
            return JsonReaderHelper.GetTextFromUtf8(buffer.Slice(0, written));
        }

        return string.Empty;
    }
}