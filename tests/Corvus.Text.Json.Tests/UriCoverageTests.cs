// <copyright file="UriCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Data-driven coverage tests targeting uncovered TryApply and TryMakeRelative
/// methods on the Utf8Iri, Utf8IriReference, Utf8UriReference, and Utf8Uri types.
/// Each type is a readonly ref struct with private constructors; instances are
/// created through static TryCreate* factory methods.
/// </summary>
[TestClass]
public class UriCoverageTests
{
    // Utf8 byte representations for test URIs/IRIs.
    // These are stored as byte arrays because the ref struct types
    // cannot be stored in static fields.
    private static readonly byte[] AbsoluteIri = "http://example.com/base/"u8.ToArray();
    private static readonly byte[] AbsoluteIri2 = "http://example.com/base/target"u8.ToArray();
    private static readonly byte[] AbsoluteUri = "http://example.com/base/"u8.ToArray();
    private static readonly byte[] AbsoluteUri2 = "http://example.com/base/target"u8.ToArray();
    private static readonly byte[] RelativeRef = "relative/path"u8.ToArray();
    private static readonly byte[] OtherAbsoluteIri = "http://other.example.com/abs"u8.ToArray();
    private static readonly byte[] OtherAbsoluteUri = "http://other.example.com/abs"u8.ToArray();

    #region Utf8Iri.TryApply (4 overloads, lines 249-321)

    [TestMethod]
    public void Utf8Iri_TryApply_Iri_Succeeds()
    {
        Assert.IsTrue(Utf8Iri.TryCreateIri(AbsoluteIri, out Utf8Iri baseIri));
        Assert.IsTrue(Utf8Iri.TryCreateIri(OtherAbsoluteIri, out Utf8Iri other));
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(baseIri.TryApply(in other, buffer, out Utf8Iri result));
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void Utf8Iri_TryApply_IriReference_Succeeds()
    {
        Assert.IsTrue(Utf8Iri.TryCreateIri(AbsoluteIri, out Utf8Iri baseIri));
        Assert.IsTrue(Utf8IriReference.TryCreateIriReference(RelativeRef, out Utf8IriReference iriRef));
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(baseIri.TryApply(in iriRef, buffer, out Utf8Iri result));
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void Utf8Iri_TryApply_UriReference_Succeeds()
    {
        Assert.IsTrue(Utf8Iri.TryCreateIri(AbsoluteIri, out Utf8Iri baseIri));
        Assert.IsTrue(Utf8UriReference.TryCreateUriReference(RelativeRef, out Utf8UriReference uriRef));
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(baseIri.TryApply(in uriRef, buffer, out Utf8Iri result));
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void Utf8Iri_TryApply_Uri_Succeeds()
    {
        Assert.IsTrue(Utf8Iri.TryCreateIri(AbsoluteIri, out Utf8Iri baseIri));
        Assert.IsTrue(Utf8Uri.TryCreateUri(OtherAbsoluteUri, out Utf8Uri uri));
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(baseIri.TryApply(in uri, buffer, out Utf8Iri result));
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void Utf8Iri_TryApply_Iri_BufferTooSmall_ReturnsFalse()
    {
        Assert.IsTrue(Utf8Iri.TryCreateIri(AbsoluteIri, out Utf8Iri baseIri));
        Assert.IsTrue(Utf8Iri.TryCreateIri(OtherAbsoluteIri, out Utf8Iri other));
        Span<byte> buffer = stackalloc byte[1];
        Assert.IsFalse(baseIri.TryApply(in other, buffer, out _));
    }

    [TestMethod]
    public void Utf8Iri_TryApply_IriReference_BufferTooSmall_ReturnsFalse()
    {
        Assert.IsTrue(Utf8Iri.TryCreateIri(AbsoluteIri, out Utf8Iri baseIri));
        Assert.IsTrue(Utf8IriReference.TryCreateIriReference(RelativeRef, out Utf8IriReference iriRef));
        Span<byte> buffer = stackalloc byte[1];
        Assert.IsFalse(baseIri.TryApply(in iriRef, buffer, out _));
    }

    [TestMethod]
    public void Utf8Iri_TryApply_UriReference_BufferTooSmall_ReturnsFalse()
    {
        Assert.IsTrue(Utf8Iri.TryCreateIri(AbsoluteIri, out Utf8Iri baseIri));
        Assert.IsTrue(Utf8UriReference.TryCreateUriReference(RelativeRef, out Utf8UriReference uriRef));
        Span<byte> buffer = stackalloc byte[1];
        Assert.IsFalse(baseIri.TryApply(in uriRef, buffer, out _));
    }

    [TestMethod]
    public void Utf8Iri_TryApply_Uri_BufferTooSmall_ReturnsFalse()
    {
        Assert.IsTrue(Utf8Iri.TryCreateIri(AbsoluteIri, out Utf8Iri baseIri));
        Assert.IsTrue(Utf8Uri.TryCreateUri(OtherAbsoluteUri, out Utf8Uri uri));
        Span<byte> buffer = stackalloc byte[1];
        Assert.IsFalse(baseIri.TryApply(in uri, buffer, out _));
    }

    #endregion

    #region Utf8Iri.TryMakeRelative (2 overloads, lines 333-362)

    [TestMethod]
    public void Utf8Iri_TryMakeRelative_Iri_Succeeds()
    {
        Assert.IsTrue(Utf8Iri.TryCreateIri(AbsoluteIri, out Utf8Iri baseIri));
        Assert.IsTrue(Utf8Iri.TryCreateIri(AbsoluteIri2, out Utf8Iri target));
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(baseIri.TryMakeRelative(in target, buffer, out Utf8IriReference result));
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void Utf8Iri_TryMakeRelative_Uri_Succeeds()
    {
        Assert.IsTrue(Utf8Iri.TryCreateIri(AbsoluteIri, out Utf8Iri baseIri));
        Assert.IsTrue(Utf8Uri.TryCreateUri(AbsoluteUri2, out Utf8Uri target));
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(baseIri.TryMakeRelative(in target, buffer, out Utf8IriReference result));
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void Utf8Iri_TryMakeRelative_Iri_BufferTooSmall_ReturnsFalse()
    {
        Assert.IsTrue(Utf8Iri.TryCreateIri(AbsoluteIri, out Utf8Iri baseIri));
        Assert.IsTrue(Utf8Iri.TryCreateIri(AbsoluteIri2, out Utf8Iri target));
        Span<byte> buffer = stackalloc byte[1];
        Assert.IsFalse(baseIri.TryMakeRelative(in target, buffer, out _));
    }

    [TestMethod]
    public void Utf8Iri_TryMakeRelative_Uri_BufferTooSmall_ReturnsFalse()
    {
        Assert.IsTrue(Utf8Iri.TryCreateIri(AbsoluteIri, out Utf8Iri baseIri));
        Assert.IsTrue(Utf8Uri.TryCreateUri(AbsoluteUri2, out Utf8Uri target));
        Span<byte> buffer = stackalloc byte[1];
        Assert.IsFalse(baseIri.TryMakeRelative(in target, buffer, out _));
    }

    #endregion

    #region Utf8IriReference.TryApply (4 overloads, lines 247-319, with !IsRelative guards on 3)

    [TestMethod]
    public void Utf8IriReference_TryApply_Iri_AbsoluteRef_Succeeds()
    {
        // Absolute IRI reference (IsRelative == false) passes the !IsRelative guard
        Assert.IsTrue(Utf8IriReference.TryCreateIriReference(AbsoluteIri, out Utf8IriReference absRef));
        Assert.IsTrue(Utf8Iri.TryCreateIri(OtherAbsoluteIri, out Utf8Iri iri));
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(absRef.TryApply(in iri, buffer, out Utf8Iri result));
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void Utf8IriReference_TryApply_Iri_RelativeRef_ReturnsFalse()
    {
        // Relative IRI reference (IsRelative == true) hits the !IsRelative guard
        Assert.IsTrue(Utf8IriReference.TryCreateIriReference(RelativeRef, out Utf8IriReference relRef));
        Assert.IsTrue(Utf8Iri.TryCreateIri(AbsoluteIri, out Utf8Iri iri));
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsFalse(relRef.TryApply(in iri, buffer, out _));
    }

    [TestMethod]
    public void Utf8IriReference_TryApply_IriReference_AbsoluteRef_Succeeds()
    {
        Assert.IsTrue(Utf8IriReference.TryCreateIriReference(AbsoluteIri, out Utf8IriReference absRef));
        Assert.IsTrue(Utf8IriReference.TryCreateIriReference(RelativeRef, out Utf8IriReference other));
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(absRef.TryApply(in other, buffer, out Utf8Iri result));
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void Utf8IriReference_TryApply_IriReference_RelativeRef_ReturnsFalse()
    {
        Assert.IsTrue(Utf8IriReference.TryCreateIriReference(RelativeRef, out Utf8IriReference relRef));
        Assert.IsTrue(Utf8IriReference.TryCreateIriReference(RelativeRef, out Utf8IriReference other));
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsFalse(relRef.TryApply(in other, buffer, out _));
    }

    [TestMethod]
    public void Utf8IriReference_TryApply_UriReference_AbsoluteRef_Succeeds()
    {
        Assert.IsTrue(Utf8IriReference.TryCreateIriReference(AbsoluteIri, out Utf8IriReference absRef));
        Assert.IsTrue(Utf8UriReference.TryCreateUriReference(RelativeRef, out Utf8UriReference uriRef));
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(absRef.TryApply(in uriRef, buffer, out Utf8Iri result));
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void Utf8IriReference_TryApply_UriReference_RelativeRef_ReturnsFalse()
    {
        Assert.IsTrue(Utf8IriReference.TryCreateIriReference(RelativeRef, out Utf8IriReference relRef));
        Assert.IsTrue(Utf8UriReference.TryCreateUriReference(RelativeRef, out Utf8UriReference uriRef));
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsFalse(relRef.TryApply(in uriRef, buffer, out _));
    }

    [TestMethod]
    public void Utf8IriReference_TryApply_Uri_Succeeds()
    {
        // The Uri overload does NOT have an !IsRelative guard
        Assert.IsTrue(Utf8IriReference.TryCreateIriReference(RelativeRef, out Utf8IriReference relRef));
        Assert.IsTrue(Utf8Uri.TryCreateUri(OtherAbsoluteUri, out Utf8Uri uri));
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(relRef.TryApply(in uri, buffer, out Utf8Iri result));
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void Utf8IriReference_TryApply_IriReference_BufferTooSmall_ReturnsFalse()
    {
        Assert.IsTrue(Utf8IriReference.TryCreateIriReference(AbsoluteIri, out Utf8IriReference absRef));
        Assert.IsTrue(Utf8IriReference.TryCreateIriReference(RelativeRef, out Utf8IriReference other));
        Span<byte> buffer = stackalloc byte[1];
        Assert.IsFalse(absRef.TryApply(in other, buffer, out _));
    }

    [TestMethod]
    public void Utf8IriReference_TryApply_UriReference_BufferTooSmall_ReturnsFalse()
    {
        Assert.IsTrue(Utf8IriReference.TryCreateIriReference(AbsoluteIri, out Utf8IriReference absRef));
        Assert.IsTrue(Utf8UriReference.TryCreateUriReference(RelativeRef, out Utf8UriReference uriRef));
        Span<byte> buffer = stackalloc byte[1];
        Assert.IsFalse(absRef.TryApply(in uriRef, buffer, out _));
    }

    [TestMethod]
    public void Utf8IriReference_TryApply_Uri_BufferTooSmall_ReturnsFalse()
    {
        Assert.IsTrue(Utf8IriReference.TryCreateIriReference(RelativeRef, out Utf8IriReference relRef));
        Assert.IsTrue(Utf8Uri.TryCreateUri(OtherAbsoluteUri, out Utf8Uri uri));
        Span<byte> buffer = stackalloc byte[1];
        Assert.IsFalse(relRef.TryApply(in uri, buffer, out _));
    }

    #endregion

    #region Utf8UriReference.TryApply (2 overloads, lines 247-277, with !IsRelative guards)

    [TestMethod]
    public void Utf8UriReference_TryApply_UriReference_AbsoluteRef_Succeeds()
    {
        Assert.IsTrue(Utf8UriReference.TryCreateUriReference(AbsoluteUri, out Utf8UriReference absRef));
        Assert.IsTrue(Utf8UriReference.TryCreateUriReference(RelativeRef, out Utf8UriReference other));
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(absRef.TryApply(in other, buffer, out Utf8Uri result));
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void Utf8UriReference_TryApply_UriReference_RelativeRef_ReturnsFalse()
    {
        Assert.IsTrue(Utf8UriReference.TryCreateUriReference(RelativeRef, out Utf8UriReference relRef));
        Assert.IsTrue(Utf8UriReference.TryCreateUriReference(RelativeRef, out Utf8UriReference other));
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsFalse(relRef.TryApply(in other, buffer, out _));
    }

    [TestMethod]
    public void Utf8UriReference_TryApply_Uri_AbsoluteRef_Succeeds()
    {
        Assert.IsTrue(Utf8UriReference.TryCreateUriReference(AbsoluteUri, out Utf8UriReference absRef));
        Assert.IsTrue(Utf8Uri.TryCreateUri(OtherAbsoluteUri, out Utf8Uri uri));
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(absRef.TryApply(in uri, buffer, out Utf8Uri result));
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void Utf8UriReference_TryApply_Uri_RelativeRef_ReturnsFalse()
    {
        Assert.IsTrue(Utf8UriReference.TryCreateUriReference(RelativeRef, out Utf8UriReference relRef));
        Assert.IsTrue(Utf8Uri.TryCreateUri(OtherAbsoluteUri, out Utf8Uri uri));
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsFalse(relRef.TryApply(in uri, buffer, out _));
    }

    [TestMethod]
    public void Utf8UriReference_TryApply_BufferTooSmall_ReturnsFalse()
    {
        Assert.IsTrue(Utf8UriReference.TryCreateUriReference(AbsoluteUri, out Utf8UriReference absRef));
        Assert.IsTrue(Utf8UriReference.TryCreateUriReference(RelativeRef, out Utf8UriReference other));
        Span<byte> buffer = stackalloc byte[1];
        Assert.IsFalse(absRef.TryApply(in other, buffer, out _));
    }

    #endregion

    #region Utf8Uri.TryApply (2 overloads, lines 247-277)

    [TestMethod]
    public void Utf8Uri_TryApply_UriReference_Succeeds()
    {
        Assert.IsTrue(Utf8Uri.TryCreateUri(AbsoluteUri, out Utf8Uri baseUri));
        Assert.IsTrue(Utf8UriReference.TryCreateUriReference(RelativeRef, out Utf8UriReference uriRef));
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(baseUri.TryApply(in uriRef, buffer, out Utf8Uri result));
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void Utf8Uri_TryApply_Uri_Succeeds()
    {
        Assert.IsTrue(Utf8Uri.TryCreateUri(AbsoluteUri, out Utf8Uri baseUri));
        Assert.IsTrue(Utf8Uri.TryCreateUri(OtherAbsoluteUri, out Utf8Uri other));
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(baseUri.TryApply(in other, buffer, out Utf8Uri result));
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void Utf8Uri_TryApply_BufferTooSmall_ReturnsFalse()
    {
        Assert.IsTrue(Utf8Uri.TryCreateUri(AbsoluteUri, out Utf8Uri baseUri));
        Assert.IsTrue(Utf8UriReference.TryCreateUriReference(RelativeRef, out Utf8UriReference uriRef));
        Span<byte> buffer = stackalloc byte[1];
        Assert.IsFalse(baseUri.TryApply(in uriRef, buffer, out _));
    }

    #endregion

    #region Utf8Uri.TryMakeRelative (1 overload, lines 289-297)

    [TestMethod]
    public void Utf8Uri_TryMakeRelative_Succeeds()
    {
        Assert.IsTrue(Utf8Uri.TryCreateUri(AbsoluteUri, out Utf8Uri baseUri));
        Assert.IsTrue(Utf8Uri.TryCreateUri(AbsoluteUri2, out Utf8Uri target));
        Span<byte> buffer = stackalloc byte[256];
        Assert.IsTrue(baseUri.TryMakeRelative(in target, buffer, out Utf8UriReference result));
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void Utf8Uri_TryApply_Uri_BufferTooSmall_ReturnsFalse()
    {
        Assert.IsTrue(Utf8Uri.TryCreateUri(AbsoluteUri, out Utf8Uri baseUri));
        Assert.IsTrue(Utf8Uri.TryCreateUri(OtherAbsoluteUri, out Utf8Uri other));
        Span<byte> buffer = stackalloc byte[1];
        Assert.IsFalse(baseUri.TryApply(in other, buffer, out _));
    }

    [TestMethod]
    public void Utf8Uri_TryMakeRelative_BufferTooSmall_ReturnsFalse()
    {
        Assert.IsTrue(Utf8Uri.TryCreateUri(AbsoluteUri, out Utf8Uri baseUri));
        Assert.IsTrue(Utf8Uri.TryCreateUri(AbsoluteUri2, out Utf8Uri target));
        Span<byte> buffer = stackalloc byte[1];
        Assert.IsFalse(baseUri.TryMakeRelative(in target, buffer, out _));
    }

    #endregion

    #region ToString on invalid/default instances (return string.Empty paths)

    [TestMethod]
    public void Utf8Iri_ToString_Default_ReturnsEmpty()
    {
        // Default Utf8Iri has no data, TryFormatDisplay fails → returns string.Empty
        Utf8Iri.TryCreateIri(ReadOnlySpan<byte>.Empty, out Utf8Iri invalid);
        Assert.AreEqual(string.Empty, invalid.ToString());
    }

    [TestMethod]
    public void Utf8IriReference_ToString_Default_ReturnsEmpty()
    {
        Utf8IriReference.TryCreateIriReference(ReadOnlySpan<byte>.Empty, out Utf8IriReference invalid);
        Assert.AreEqual(string.Empty, invalid.ToString());
    }

    [TestMethod]
    public void Utf8UriReference_ToString_Default_ReturnsEmpty()
    {
        Utf8UriReference.TryCreateUriReference(ReadOnlySpan<byte>.Empty, out Utf8UriReference invalid);
        Assert.AreEqual(string.Empty, invalid.ToString());
    }

    [TestMethod]
    public void Utf8Uri_ToString_Default_ReturnsEmpty()
    {
        Utf8Uri.TryCreateUri(ReadOnlySpan<byte>.Empty, out Utf8Uri invalid);
        Assert.AreEqual(string.Empty, invalid.ToString());
    }

    #endregion
}
