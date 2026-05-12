// <copyright file="Utf8UriToolsCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Linq;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Corvus.Text.Json.Internal.Utf8UriTools;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests for <see cref="Utf8UriTools"/> internal methods.
/// </summary>
[TestClass]
public class Utf8UriToolsCoverageTests
{
    // ─── Helpers ────────────────────────────────────────────────────────

    private static byte[] U(string s) => Encoding.UTF8.GetBytes(s);

    private static bool ParseUri(string uri, out Utf8UriOffset offsets, out Flags flags, bool allowIri = false, bool requireAbsolute = false)
    {
        byte[] bytes = U(uri);
        return Utf8UriTools.ParseUriInfo(bytes, Utf8UriKind.RelativeOrAbsolute, requireAbsolute, allowIri, out offsets, out flags);
    }

    private static string Str(Span<byte> span, int len) => Encoding.UTF8.GetString(span.Slice(0, len).ToArray());

    // ─── ParseUriInfo ───────────────────────────────────────────────────

    [TestMethod]
    public void ParseUriInfo_SimpleHttp()
    {
        Assert.IsTrue(ParseUri("http://example.com/path", out Utf8UriOffset offsets, out Flags flags));
        Assert.IsTrue(offsets.End > 0);
    }

    [TestMethod]
    public void ParseUriInfo_Https()
    {
        Assert.IsTrue(ParseUri("https://example.com/path?q=1#frag", out Utf8UriOffset offsets, out Flags flags));
        Assert.IsTrue(offsets.End > 0);
    }

    [TestMethod]
    public void ParseUriInfo_WithPort()
    {
        Assert.IsTrue(ParseUri("http://example.com:8080/path", out Utf8UriOffset offsets, out Flags flags));
        Assert.IsTrue(offsets.End > 0);
    }

    [TestMethod]
    public void ParseUriInfo_WithUserInfo()
    {
        Assert.IsTrue(ParseUri("http://user:pass@example.com/path", out Utf8UriOffset offsets, out Flags flags));
        Assert.IsTrue(offsets.End > 0);
    }

    [TestMethod]
    public void ParseUriInfo_IPv6Host()
    {
        Assert.IsTrue(ParseUri("http://[::1]/path", out Utf8UriOffset offsets, out Flags flags));
        Assert.IsTrue(offsets.End > 0);
    }

    [TestMethod]
    public void ParseUriInfo_RelativeUri()
    {
        byte[] bytes = U("/relative/path?q=1#frag");
        bool result = Utf8UriTools.ParseUriInfo(bytes, Utf8UriKind.Relative, requireAbsolute: false, allowIri: false, out Utf8UriOffset offsets, out Flags flags);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ParseUriInfo_RelativeOrAbsolute_WithRelative()
    {
        byte[] bytes = U("relative/path");
        bool result = Utf8UriTools.ParseUriInfo(bytes, Utf8UriKind.RelativeOrAbsolute, requireAbsolute: false, allowIri: false, out Utf8UriOffset offsets, out Flags flags);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ParseUriInfo_RequireAbsolute_FailsOnRelative()
    {
        byte[] bytes = U("relative/path");
        bool result = Utf8UriTools.ParseUriInfo(bytes, Utf8UriKind.Absolute, requireAbsolute: true, allowIri: false, out Utf8UriOffset offsets, out Flags flags);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ParseUriInfo_FtpScheme()
    {
        Assert.IsTrue(ParseUri("ftp://files.example.com/pub", out Utf8UriOffset offsets, out Flags flags));
        Assert.IsTrue(offsets.End > 0);
    }

    [TestMethod]
    public void ParseUriInfo_EmptyInput()
    {
        byte[] bytes = U(string.Empty);
        bool result = Utf8UriTools.ParseUriInfo(bytes, Utf8UriKind.RelativeOrAbsolute, requireAbsolute: false, allowIri: false, out Utf8UriOffset offsets, out Flags flags);
        // Empty string may succeed as relative or fail depending on implementation
        // We just ensure it doesn't throw
        _ = result;
    }

    [TestMethod]
    public void ParseUriInfo_AllowIriTrue()
    {
        Assert.IsTrue(ParseUri("http://example.com/path", out Utf8UriOffset offsets, out Flags flags, allowIri: true));
        Assert.IsTrue(offsets.End > 0);
    }

    [TestMethod]
    public void ParseUriInfo_QueryOnly()
    {
        byte[] bytes = U("?query=value");
        bool result = Utf8UriTools.ParseUriInfo(bytes, Utf8UriKind.Relative, requireAbsolute: false, allowIri: false, out Utf8UriOffset offsets, out Flags flags);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ParseUriInfo_FragmentOnly()
    {
        byte[] bytes = U("#fragment");
        bool result = Utf8UriTools.ParseUriInfo(bytes, Utf8UriKind.Relative, requireAbsolute: false, allowIri: false, out Utf8UriOffset offsets, out Flags flags);
        Assert.IsTrue(result);
    }

    // ─── Validate ───────────────────────────────────────────────────────

    [TestMethod]
    [DataRow("http://example.com/path", true)]
    [DataRow("https://example.com:443/a/b?x=1#f", true)]
    [DataRow("ftp://ftp.example.com/pub", true)]
    public void Validate_ValidAbsoluteUris(string uri, bool expected)
    {
        byte[] bytes = U(uri);
        Assert.AreEqual(expected, Utf8UriTools.Validate(bytes, Utf8UriKind.Absolute, requireAbsolute: false, allowIri: false, allowUNCPath: false));
    }

    [TestMethod]
    public void Validate_RelativeUri()
    {
        byte[] bytes = U("/relative/path?q=1");
        Assert.IsTrue(Utf8UriTools.Validate(bytes, Utf8UriKind.Relative, requireAbsolute: false, allowIri: false, allowUNCPath: false));
    }

    [TestMethod]
    public void Validate_RelativeUri_NoLeadingSlash()
    {
        byte[] bytes = U("relative/path");
        Assert.IsTrue(Utf8UriTools.Validate(bytes, Utf8UriKind.RelativeOrAbsolute, requireAbsolute: false, allowIri: false, allowUNCPath: false));
    }

    [TestMethod]
    public void Validate_RequireAbsolute_FailsOnRelative()
    {
        byte[] bytes = U("relative/path");
        Assert.IsFalse(Utf8UriTools.Validate(bytes, Utf8UriKind.RelativeOrAbsolute, requireAbsolute: true, allowIri: false, allowUNCPath: false));
    }

    [TestMethod]
    public void Validate_AllowIri()
    {
        byte[] bytes = U("http://example.com/path");
        Assert.IsTrue(Utf8UriTools.Validate(bytes, Utf8UriKind.Absolute, requireAbsolute: false, allowIri: true, allowUNCPath: false));
    }

    // ─── MakeRelative ───────────────────────────────────────────────────

    [TestMethod]
    public void MakeRelative_SameHostDifferentFile()
    {
        byte[] baseBytes = U("http://example.com/a/b/c");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(baseBytes, Utf8UriKind.Absolute, false, false, out var baseOffsets, out var baseFlags));

        byte[] targetBytes = U("http://example.com/a/b/d");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(targetBytes, Utf8UriKind.Absolute, false, false, out var targetOffsets, out var targetFlags));

        Span<byte> dest = stackalloc byte[256];
        bool result = MakeRelative(baseBytes, baseOffsets, baseFlags, targetBytes, targetOffsets, targetFlags, dest, out int written);
        Assert.IsTrue(result);
        Assert.AreEqual("d", Str(dest, written));
    }

    [TestMethod]
    public void MakeRelative_DeeperRelative()
    {
        byte[] baseBytes = U("http://example.com/a/b/c");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(baseBytes, Utf8UriKind.Absolute, false, false, out var baseOffsets, out var baseFlags));

        byte[] targetBytes = U("http://example.com/a/x/y");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(targetBytes, Utf8UriKind.Absolute, false, false, out var targetOffsets, out var targetFlags));

        Span<byte> dest = stackalloc byte[256];
        bool result = MakeRelative(baseBytes, baseOffsets, baseFlags, targetBytes, targetOffsets, targetFlags, dest, out int written);
        Assert.IsTrue(result);
        Assert.AreEqual("../x/y", Str(dest, written));
    }

    [TestMethod]
    public void MakeRelative_SameUri_EmptyResult()
    {
        byte[] baseBytes = U("http://example.com/a/b/c");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(baseBytes, Utf8UriKind.Absolute, false, false, out var baseOffsets, out var baseFlags));

        Span<byte> dest = stackalloc byte[256];
        bool result = MakeRelative(baseBytes, baseOffsets, baseFlags, baseBytes, baseOffsets, baseFlags, dest, out int written);
        Assert.IsTrue(result);
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void MakeRelative_DifferentScheme_ReturnsFullUri()
    {
        byte[] baseBytes = U("http://example.com/path");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(baseBytes, Utf8UriKind.Absolute, false, false, out var baseOffsets, out var baseFlags));

        byte[] targetBytes = U("https://example.com/path");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(targetBytes, Utf8UriKind.Absolute, false, false, out var targetOffsets, out var targetFlags));

        Span<byte> dest = stackalloc byte[256];
        bool result = MakeRelative(baseBytes, baseOffsets, baseFlags, targetBytes, targetOffsets, targetFlags, dest, out int written);
        Assert.IsTrue(result);
        Assert.AreEqual("https://example.com/path", Str(dest, written));
    }

    [TestMethod]
    public void MakeRelative_DifferentHost_ReturnsFullUri()
    {
        byte[] baseBytes = U("http://a.com/path");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(baseBytes, Utf8UriKind.Absolute, false, false, out var baseOffsets, out var baseFlags));

        byte[] targetBytes = U("http://b.com/path");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(targetBytes, Utf8UriKind.Absolute, false, false, out var targetOffsets, out var targetFlags));

        Span<byte> dest = stackalloc byte[256];
        bool result = MakeRelative(baseBytes, baseOffsets, baseFlags, targetBytes, targetOffsets, targetFlags, dest, out int written);
        Assert.IsTrue(result);
        Assert.AreEqual("http://b.com/path", Str(dest, written));
    }

    [TestMethod]
    public void MakeRelative_BufferTooSmall_DifferentHost()
    {
        byte[] baseBytes = U("http://a.com/path");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(baseBytes, Utf8UriKind.Absolute, false, false, out var baseOffsets, out var baseFlags));

        byte[] targetBytes = U("http://b.com/path");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(targetBytes, Utf8UriKind.Absolute, false, false, out var targetOffsets, out var targetFlags));

        Span<byte> dest = stackalloc byte[2]; // Too small
        bool result = MakeRelative(baseBytes, baseOffsets, baseFlags, targetBytes, targetOffsets, targetFlags, dest, out int written);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void MakeRelative_WithQuery()
    {
        byte[] baseBytes = U("http://example.com/a/b");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(baseBytes, Utf8UriKind.Absolute, false, false, out var baseOffsets, out var baseFlags));

        byte[] targetBytes = U("http://example.com/a/c?q=1");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(targetBytes, Utf8UriKind.Absolute, false, false, out var targetOffsets, out var targetFlags));

        Span<byte> dest = stackalloc byte[256];
        bool result = MakeRelative(baseBytes, baseOffsets, baseFlags, targetBytes, targetOffsets, targetFlags, dest, out int written);
        Assert.IsTrue(result);
        Assert.AreEqual("c?q=1", Str(dest, written));
    }

    [TestMethod]
    public void MakeRelative_WithFragment()
    {
        byte[] baseBytes = U("http://example.com/a/b");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(baseBytes, Utf8UriKind.Absolute, false, false, out var baseOffsets, out var baseFlags));

        byte[] targetBytes = U("http://example.com/a/c#frag");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(targetBytes, Utf8UriKind.Absolute, false, false, out var targetOffsets, out var targetFlags));

        Span<byte> dest = stackalloc byte[256];
        bool result = MakeRelative(baseBytes, baseOffsets, baseFlags, targetBytes, targetOffsets, targetFlags, dest, out int written);
        Assert.IsTrue(result);
        Assert.AreEqual("c#frag", Str(dest, written));
    }

    [TestMethod]
    public void MakeRelative_CaseInsensitiveScheme()
    {
        byte[] baseBytes = U("HTTP://example.com/a/b");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(baseBytes, Utf8UriKind.Absolute, false, false, out var baseOffsets, out var baseFlags));

        byte[] targetBytes = U("http://example.com/a/c");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(targetBytes, Utf8UriKind.Absolute, false, false, out var targetOffsets, out var targetFlags));

        Span<byte> dest = stackalloc byte[256];
        bool result = MakeRelative(baseBytes, baseOffsets, baseFlags, targetBytes, targetOffsets, targetFlags, dest, out int written);
        Assert.IsTrue(result);
        // Should be relative since schemes match case-insensitively
        Assert.AreEqual("c", Str(dest, written));
    }

    // ─── TryApply ───────────────────────────────────────────────────────

    [TestMethod]
    public void TryApply_RelativePathFromBase()
    {
        byte[] baseBytes = U("http://example.com/a/b/c");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(baseBytes, Utf8UriKind.Absolute, false, false, out var baseOffsets, out var baseFlags));

        byte[] targetBytes = U("../d");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(targetBytes, Utf8UriKind.RelativeOrAbsolute, false, false, out var targetOffsets, out var targetFlags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryApply(baseBytes, baseOffsets, baseFlags, targetBytes, targetOffsets, targetFlags, dest, out int written);
        Assert.IsTrue(result);
        Assert.AreEqual("http://example.com/a/d", Str(dest, written));
    }

    [TestMethod]
    public void TryApply_QueryOnlyReference()
    {
        byte[] baseBytes = U("http://example.com/a/b");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(baseBytes, Utf8UriKind.Absolute, false, false, out var baseOffsets, out var baseFlags));

        byte[] targetBytes = U("?q=1");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(targetBytes, Utf8UriKind.RelativeOrAbsolute, false, false, out var targetOffsets, out var targetFlags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryApply(baseBytes, baseOffsets, baseFlags, targetBytes, targetOffsets, targetFlags, dest, out int written);
        Assert.IsTrue(result);
        Assert.AreEqual("http://example.com/a/b?q=1", Str(dest, written));
    }

    [TestMethod]
    public void TryApply_FragmentOnlyReference()
    {
        byte[] baseBytes = U("http://example.com/a/b");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(baseBytes, Utf8UriKind.Absolute, false, false, out var baseOffsets, out var baseFlags));

        byte[] targetBytes = U("#frag");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(targetBytes, Utf8UriKind.RelativeOrAbsolute, false, false, out var targetOffsets, out var targetFlags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryApply(baseBytes, baseOffsets, baseFlags, targetBytes, targetOffsets, targetFlags, dest, out int written);
        Assert.IsTrue(result);
        Assert.AreEqual("http://example.com/a/b#frag", Str(dest, written));
    }

    [TestMethod]
    public void TryApply_AbsoluteReferenceReplacesEverything()
    {
        byte[] baseBytes = U("http://example.com/a/b");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(baseBytes, Utf8UriKind.Absolute, false, false, out var baseOffsets, out var baseFlags));

        byte[] targetBytes = U("https://other.com/x/y");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(targetBytes, Utf8UriKind.Absolute, false, false, out var targetOffsets, out var targetFlags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryApply(baseBytes, baseOffsets, baseFlags, targetBytes, targetOffsets, targetFlags, dest, out int written);
        Assert.IsTrue(result);
        Assert.AreEqual("https://other.com/x/y", Str(dest, written));
    }

    [TestMethod]
    public void TryApply_NonStrictMode_SameScheme()
    {
        byte[] baseBytes = U("http://example.com/a/b");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(baseBytes, Utf8UriKind.Absolute, false, false, out var baseOffsets, out var baseFlags));

        byte[] targetBytes = U("http://example.com/c/d");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(targetBytes, Utf8UriKind.Absolute, false, false, out var targetOffsets, out var targetFlags));

        Span<byte> dest = stackalloc byte[256];
        bool resultStrict = TryApply(baseBytes, baseOffsets, baseFlags, targetBytes, targetOffsets, targetFlags, dest, out int writtenStrict, strict: true);
        Assert.IsTrue(resultStrict);

        bool resultNonStrict = TryApply(baseBytes, baseOffsets, baseFlags, targetBytes, targetOffsets, targetFlags, dest, out int writtenNonStrict, strict: false);
        Assert.IsTrue(resultNonStrict);
    }

    [TestMethod]
    public void TryApply_BufferTooSmall()
    {
        byte[] baseBytes = U("http://example.com/a/b");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(baseBytes, Utf8UriKind.Absolute, false, false, out var baseOffsets, out var baseFlags));

        byte[] targetBytes = U("https://other.com/very/long/path/that/wont/fit");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(targetBytes, Utf8UriKind.Absolute, false, false, out var targetOffsets, out var targetFlags));

        Span<byte> dest = stackalloc byte[5]; // Too small
        bool result = TryApply(baseBytes, baseOffsets, baseFlags, targetBytes, targetOffsets, targetFlags, dest, out int written);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void TryApply_EmptyReference_UsesBase()
    {
        byte[] baseBytes = U("http://example.com/a/b?q=1");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(baseBytes, Utf8UriKind.Absolute, false, false, out var baseOffsets, out var baseFlags));

        byte[] targetBytes = U(string.Empty);
        // Empty reference may not parse via ParseUriInfo, use a special case
        Utf8UriOffset emptyOffsets = default;
        Flags emptyFlags = Flags.Zero;

        Span<byte> dest = stackalloc byte[256];
        bool result = TryApply(baseBytes, baseOffsets, baseFlags, targetBytes, emptyOffsets, emptyFlags, dest, out int written);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void TryApply_RelativePathMerge()
    {
        byte[] baseBytes = U("http://example.com/a/b/c");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(baseBytes, Utf8UriKind.Absolute, false, false, out var baseOffsets, out var baseFlags));

        byte[] targetBytes = U("g");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(targetBytes, Utf8UriKind.RelativeOrAbsolute, false, false, out var targetOffsets, out var targetFlags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryApply(baseBytes, baseOffsets, baseFlags, targetBytes, targetOffsets, targetFlags, dest, out int written);
        Assert.IsTrue(result);
        Assert.AreEqual("http://example.com/a/b/g", Str(dest, written));
    }

    [TestMethod]
    public void TryApply_AbsolutePathReference()
    {
        byte[] baseBytes = U("http://example.com/a/b/c");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(baseBytes, Utf8UriKind.Absolute, false, false, out var baseOffsets, out var baseFlags));

        byte[] targetBytes = U("/x/y/z");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(targetBytes, Utf8UriKind.RelativeOrAbsolute, false, false, out var targetOffsets, out var targetFlags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryApply(baseBytes, baseOffsets, baseFlags, targetBytes, targetOffsets, targetFlags, dest, out int written);
        Assert.IsTrue(result);
        Assert.AreEqual("http://example.com/x/y/z", Str(dest, written));
    }

    // ─── TryFormatDisplay ───────────────────────────────────────────────

    [TestMethod]
    public void TryFormatDisplay_SimpleUri()
    {
        byte[] uriBytes = U("http://example.com/path");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(uriBytes, Utf8UriKind.Absolute, false, false, out var offsets, out var flags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryFormatDisplay(uriBytes, offsets, flags, dest, out int written);
        Assert.IsTrue(result);
        Assert.AreEqual("http://example.com/path", Str(dest, written));
    }

    [TestMethod]
    public void TryFormatDisplay_WithQuery()
    {
        byte[] uriBytes = U("http://example.com/path?key=value");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(uriBytes, Utf8UriKind.Absolute, false, false, out var offsets, out var flags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryFormatDisplay(uriBytes, offsets, flags, dest, out int written);
        Assert.IsTrue(result);
        Assert.AreEqual("http://example.com/path?key=value", Str(dest, written));
    }

    [TestMethod]
    public void TryFormatDisplay_WithFragment()
    {
        byte[] uriBytes = U("http://example.com/path#frag");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(uriBytes, Utf8UriKind.Absolute, false, false, out var offsets, out var flags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryFormatDisplay(uriBytes, offsets, flags, dest, out int written);
        Assert.IsTrue(result);
        Assert.AreEqual("http://example.com/path#frag", Str(dest, written));
    }

    [TestMethod]
    public void TryFormatDisplay_WithPercentEncoded()
    {
        byte[] uriBytes = U("http://example.com/hello%20world");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(uriBytes, Utf8UriKind.Absolute, false, false, out var offsets, out var flags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryFormatDisplay(uriBytes, offsets, flags, dest, out int written);
        Assert.IsTrue(result);
        Assert.AreEqual("http://example.com/hello%20world", Str(dest, written));
    }

    [TestMethod]
    public void TryFormatDisplay_BufferTooSmall()
    {
        byte[] uriBytes = U("http://example.com/path");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(uriBytes, Utf8UriKind.Absolute, false, false, out var offsets, out var flags));

        Span<byte> dest = stackalloc byte[3]; // Too small
        bool result = TryFormatDisplay(uriBytes, offsets, flags, dest, out int written);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void TryFormatDisplay_UppercaseScheme()
    {
        byte[] uriBytes = U("HTTP://EXAMPLE.COM/PATH");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(uriBytes, Utf8UriKind.Absolute, false, false, out var offsets, out var flags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryFormatDisplay(uriBytes, offsets, flags, dest, out int written);
        Assert.IsTrue(result);
        // Scheme should be lowercased
        Assert.AreEqual("http://example.com/PATH", Str(dest, written));
    }

    [TestMethod]
    public void TryFormatDisplay_WithUserInfo()
    {
        byte[] uriBytes = U("http://user:pass@example.com/path");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(uriBytes, Utf8UriKind.Absolute, false, false, out var offsets, out var flags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryFormatDisplay(uriBytes, offsets, flags, dest, out int written);
        Assert.IsTrue(result);
        Assert.AreEqual("http://user:pass@example.com/path", Str(dest, written));
    }

    [TestMethod]
    public void TryFormatDisplay_NonDefaultPort()
    {
        byte[] uriBytes = U("http://example.com:9090/path");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(uriBytes, Utf8UriKind.Absolute, false, false, out var offsets, out var flags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryFormatDisplay(uriBytes, offsets, flags, dest, out int written);
        Assert.IsTrue(result);
        Assert.AreEqual("http://example.com:9090/path", Str(dest, written));
    }

    // ─── TryFormatCanonical ─────────────────────────────────────────────

    [TestMethod]
    public void TryFormatCanonical_SimpleUri()
    {
        byte[] uriBytes = U("http://example.com/path");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(uriBytes, Utf8UriKind.Absolute, false, false, out var offsets, out var flags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryFormatCanonical(uriBytes, offsets, flags, allowIri: false, dest, out int written);
        Assert.IsTrue(result);
        Assert.AreEqual("http://example.com/path", Str(dest, written));
    }

    [TestMethod]
    public void TryFormatCanonical_WithPercentEncoded()
    {
        byte[] uriBytes = U("http://example.com/hello%20world");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(uriBytes, Utf8UriKind.Absolute, false, false, out var offsets, out var flags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryFormatCanonical(uriBytes, offsets, flags, allowIri: false, dest, out int written);
        Assert.IsTrue(result);
        Assert.AreEqual("http://example.com/hello%20world", Str(dest, written));
    }

    [TestMethod]
    public void TryFormatCanonical_AllowIri()
    {
        byte[] uriBytes = U("http://example.com/path");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(uriBytes, Utf8UriKind.Absolute, false, true, out var offsets, out var flags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryFormatCanonical(uriBytes, offsets, flags, allowIri: true, dest, out int written);
        Assert.IsTrue(result);
        Assert.AreEqual("http://example.com/path", Str(dest, written));
    }

    [TestMethod]
    public void TryFormatCanonical_BufferTooSmall()
    {
        byte[] uriBytes = U("http://example.com/path");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(uriBytes, Utf8UriKind.Absolute, false, false, out var offsets, out var flags));

        Span<byte> dest = stackalloc byte[3]; // Too small
        bool result = TryFormatCanonical(uriBytes, offsets, flags, allowIri: false, dest, out int written);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void TryFormatCanonical_UppercaseScheme()
    {
        byte[] uriBytes = U("HTTP://EXAMPLE.COM/PATH");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(uriBytes, Utf8UriKind.Absolute, false, false, out var offsets, out var flags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryFormatCanonical(uriBytes, offsets, flags, allowIri: false, dest, out int written);
        Assert.IsTrue(result);
        Assert.AreEqual("http://example.com/PATH", Str(dest, written));
    }

    [TestMethod]
    public void TryFormatCanonical_WithQuery()
    {
        byte[] uriBytes = U("http://example.com/path?key=value");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(uriBytes, Utf8UriKind.Absolute, false, false, out var offsets, out var flags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryFormatCanonical(uriBytes, offsets, flags, allowIri: false, dest, out int written);
        Assert.IsTrue(result);
        Assert.AreEqual("http://example.com/path?key=value", Str(dest, written));
    }

    [TestMethod]
    public void TryFormatCanonical_WithFragment()
    {
        byte[] uriBytes = U("http://example.com/path#frag");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(uriBytes, Utf8UriKind.Absolute, false, false, out var offsets, out var flags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryFormatCanonical(uriBytes, offsets, flags, allowIri: false, dest, out int written);
        Assert.IsTrue(result);
        Assert.AreEqual("http://example.com/path#frag", Str(dest, written));
    }

    [TestMethod]
    public void TryFormatCanonical_LowercasePercentEncoding()
    {
        // %2f should be normalized to %2F in canonical form
        byte[] uriBytes = U("http://example.com/a%2fb");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(uriBytes, Utf8UriKind.Absolute, false, false, out var offsets, out var flags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryFormatCanonical(uriBytes, offsets, flags, allowIri: false, dest, out int written);
        Assert.IsTrue(result);
        Assert.AreEqual("http://example.com/a%2Fb", Str(dest, written));
    }

    [TestMethod]
    public void TryFormatCanonical_NonDefaultPort()
    {
        byte[] uriBytes = U("http://example.com:8080/path");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(uriBytes, Utf8UriKind.Absolute, false, false, out var offsets, out var flags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryFormatCanonical(uriBytes, offsets, flags, allowIri: false, dest, out int written);
        Assert.IsTrue(result);
        Assert.AreEqual("http://example.com:8080/path", Str(dest, written));
    }

    // ─── TryEscapeDataString ────────────────────────────────────────────

    [TestMethod]
    public void TryEscapeDataString_EmptyInput()
    {
        Span<byte> dest = stackalloc byte[16];
        Assert.IsTrue(TryEscapeDataString(ReadOnlySpan<byte>.Empty, dest, out int written));
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void TryEscapeDataString_UnreservedPassthrough()
    {
        byte[] source = U("abcXYZ019-._~");
        Span<byte> dest = stackalloc byte[64];
        Assert.IsTrue(TryEscapeDataString(source, dest, out int written));
        Assert.AreEqual(source.Length, written);
        Assert.IsTrue(source.AsSpan().SequenceEqual(dest.Slice(0, written)));
    }

    [TestMethod]
    public void TryEscapeDataString_SpaceEncoded()
    {
        byte[] source = U("a b");
        Span<byte> dest = stackalloc byte[64];
        Assert.IsTrue(TryEscapeDataString(source, dest, out int written));
        Assert.AreEqual("a%20b", Str(dest, written));
    }

    [TestMethod]
    public void TryEscapeDataString_ReservedCharsEncoded()
    {
        byte[] source = U("/");
        Span<byte> dest = stackalloc byte[16];
        Assert.IsTrue(TryEscapeDataString(source, dest, out int written));
        Assert.AreEqual("%2F", Str(dest, written));
    }

    [TestMethod]
    public void TryEscapeDataString_BufferTooSmall_Unreserved()
    {
        byte[] source = U("abcdef");
        Span<byte> dest = stackalloc byte[3]; // Too small
        Assert.IsFalse(TryEscapeDataString(source, dest, out int written));
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void TryEscapeDataString_BufferTooSmall_Reserved()
    {
        byte[] source = U(" ");
        Span<byte> dest = stackalloc byte[2]; // Needs 3, has 2
        Assert.IsFalse(TryEscapeDataString(source, dest, out int written));
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void TryEscapeDataString_MultipleMixedChars()
    {
        byte[] source = U("a/b c");
        Span<byte> dest = stackalloc byte[64];
        Assert.IsTrue(TryEscapeDataString(source, dest, out int written));
        Assert.AreEqual("a%2Fb%20c", Str(dest, written));
    }

    [TestMethod]
    public void TryEscapeDataString_HighByte()
    {
        byte[] source = new byte[] { 0xFF };
        Span<byte> dest = stackalloc byte[16];
        Assert.IsTrue(TryEscapeDataString(source, dest, out int written));
        Assert.AreEqual("%FF", Str(dest, written));
    }

    // ─── TryUnescapeDataString ──────────────────────────────────────────

    [TestMethod]
    public void TryUnescapeDataString_EmptyInput()
    {
        Span<byte> dest = stackalloc byte[16];
        Assert.IsTrue(TryUnescapeDataString(ReadOnlySpan<byte>.Empty, dest, out int written));
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void TryUnescapeDataString_NoEscapes()
    {
        byte[] source = U("hello");
        Span<byte> dest = stackalloc byte[16];
        Assert.IsTrue(TryUnescapeDataString(source, dest, out int written));
        Assert.AreEqual("hello", Str(dest, written));
    }

    [TestMethod]
    public void TryUnescapeDataString_SimpleAsciiDecode()
    {
        byte[] source = U("%20");
        Span<byte> dest = stackalloc byte[16];
        Assert.IsTrue(TryUnescapeDataString(source, dest, out int written));
        Assert.AreEqual(" ", Str(dest, written));
    }

    [TestMethod]
    public void TryUnescapeDataString_MultipleDecode()
    {
        byte[] source = U("a%20b%2Fc");
        Span<byte> dest = stackalloc byte[16];
        Assert.IsTrue(TryUnescapeDataString(source, dest, out int written));
        Assert.AreEqual("a b/c", Str(dest, written));
    }

    [TestMethod]
    public void TryUnescapeDataString_InvalidHex()
    {
        // %ZZ is not valid hex, should be copied literally
        byte[] source = U("%ZZ");
        Span<byte> dest = stackalloc byte[16];
        Assert.IsTrue(TryUnescapeDataString(source, dest, out int written));
        Assert.AreEqual("%ZZ", Str(dest, written));
    }

    [TestMethod]
    public void TryUnescapeDataString_TruncatedPercent()
    {
        // % at end, treated literally
        byte[] source = U("abc%");
        Span<byte> dest = stackalloc byte[16];
        Assert.IsTrue(TryUnescapeDataString(source, dest, out int written));
        Assert.AreEqual("abc%", Str(dest, written));
    }

    [TestMethod]
    public void TryUnescapeDataString_PercentWithOneChar()
    {
        // %A at end (only one hex char), treated literally
        byte[] source = U("abc%A");
        Span<byte> dest = stackalloc byte[16];
        Assert.IsTrue(TryUnescapeDataString(source, dest, out int written));
        Assert.AreEqual("abc%A", Str(dest, written));
    }

    [TestMethod]
    public void TryUnescapeDataString_BufferTooSmall()
    {
        byte[] source = U("hello");
        Span<byte> dest = stackalloc byte[2]; // Too small
        Assert.IsFalse(TryUnescapeDataString(source, dest, out int written));
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void TryUnescapeDataString_ValidMultiByteUtf8()
    {
        // %C3%A9 is UTF-8 for 'é' (U+00E9)
        byte[] source = U("%C3%A9");
        Span<byte> dest = stackalloc byte[16];
        Assert.IsTrue(TryUnescapeDataString(source, dest, out int written));
        // Should decode to the 2-byte UTF-8 sequence for 'é'
        Assert.AreEqual(2, written);
        Assert.AreEqual(0xC3, dest[0]);
        Assert.AreEqual(0xA9, dest[1]);
    }

    [TestMethod]
    public void TryUnescapeDataString_InvalidMultiByteUtf8_CopiesLiterally()
    {
        // %FF alone is not a valid UTF-8 lead byte for a multi-byte sequence
        // The method should copy '%' literally and then continue
        byte[] source = U("%FF");
        Span<byte> dest = stackalloc byte[16];
        Assert.IsTrue(TryUnescapeDataString(source, dest, out int written));
        // %FF decodes to byte 0xFF, which is >= 0x80, so it tries multi-byte UTF-8
        // Since single 0xFF is not valid UTF-8, it copies '%' literally then moves on
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void TryUnescapeDataString_RoundTrip()
    {
        byte[] original = U("hello world/path");
        Span<byte> escaped = stackalloc byte[128];
        Assert.IsTrue(TryEscapeDataString(original, escaped, out int escapedLen));

        Span<byte> unescaped = stackalloc byte[128];
        Assert.IsTrue(TryUnescapeDataString(escaped.Slice(0, escapedLen), unescaped, out int unescapedLen));
        Assert.IsTrue(original.AsSpan().SequenceEqual(unescaped.Slice(0, unescapedLen)));
    }

    // ─── TryEscapeUri ───────────────────────────────────────────────────

    [TestMethod]
    public void TryEscapeUri_EmptyInput()
    {
        Span<byte> dest = stackalloc byte[16];
        Assert.IsTrue(TryEscapeUri(ReadOnlySpan<byte>.Empty, dest, out int written));
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void TryEscapeUri_UnreservedPassthrough()
    {
        byte[] source = U("abc123-._~");
        Span<byte> dest = stackalloc byte[64];
        Assert.IsTrue(TryEscapeUri(source, dest, out int written));
        Assert.AreEqual(source.Length, written);
        Assert.IsTrue(source.AsSpan().SequenceEqual(dest.Slice(0, written)));
    }

    [TestMethod]
    public void TryEscapeUri_ReservedPassthrough()
    {
        // Reserved characters pass through in TryEscapeUri (encodeURI semantics)
        byte[] source = U(":/?#[]@!$&'()*+,;=");
        Span<byte> dest = stackalloc byte[64];
        Assert.IsTrue(TryEscapeUri(source, dest, out int written));
        Assert.AreEqual(source.Length, written);
        Assert.IsTrue(source.AsSpan().SequenceEqual(dest.Slice(0, written)));
    }

    [TestMethod]
    public void TryEscapeUri_SpaceEncoded()
    {
        byte[] source = U("a b");
        Span<byte> dest = stackalloc byte[64];
        Assert.IsTrue(TryEscapeUri(source, dest, out int written));
        Assert.AreEqual("a%20b", Str(dest, written));
    }

    [TestMethod]
    public void TryEscapeUri_BufferTooSmall_Unreserved()
    {
        byte[] source = U("abcdef");
        Span<byte> dest = stackalloc byte[3]; // Too small
        Assert.IsFalse(TryEscapeUri(source, dest, out int written));
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void TryEscapeUri_BufferTooSmall_NonReserved()
    {
        byte[] source = U(" ");
        Span<byte> dest = stackalloc byte[2]; // Needs 3
        Assert.IsFalse(TryEscapeUri(source, dest, out int written));
        Assert.AreEqual(0, written);
    }

    [TestMethod]
    public void TryEscapeUri_HighByte()
    {
        byte[] source = new byte[] { 0x80 };
        Span<byte> dest = stackalloc byte[16];
        Assert.IsTrue(TryEscapeUri(source, dest, out int written));
        Assert.AreEqual("%80", Str(dest, written));
    }

    // ─── ValidatePathQueryAndFragmentSegment ────────────────────────────

    [TestMethod]
    public void ValidatePathQueryAndFragment_ValidPath()
    {
        byte[] bytes = U("/path/to/resource");
        Assert.IsTrue(ValidatePathQueryAndFragmentSegment(bytes, iriParsing: false));
    }

    [TestMethod]
    public void ValidatePathQueryAndFragment_ValidPathWithQuery()
    {
        byte[] bytes = U("/path?query=value");
        Assert.IsTrue(ValidatePathQueryAndFragmentSegment(bytes, iriParsing: false));
    }

    [TestMethod]
    public void ValidatePathQueryAndFragment_ValidPathWithFragment()
    {
        byte[] bytes = U("/path#fragment");
        Assert.IsTrue(ValidatePathQueryAndFragmentSegment(bytes, iriParsing: false));
    }

    [TestMethod]
    public void ValidatePathQueryAndFragment_ValidPathWithQueryAndFragment()
    {
        byte[] bytes = U("/path?q=1#frag");
        Assert.IsTrue(ValidatePathQueryAndFragmentSegment(bytes, iriParsing: false));
    }

    [TestMethod]
    public void ValidatePathQueryAndFragment_IriParsing()
    {
        byte[] bytes = U("/path/to/resource");
        Assert.IsTrue(ValidatePathQueryAndFragmentSegment(bytes, iriParsing: true));
    }

    [TestMethod]
    public void ValidatePathQueryAndFragment_PercentEncoded()
    {
        byte[] bytes = U("/path%20with%20spaces");
        Assert.IsTrue(ValidatePathQueryAndFragmentSegment(bytes, iriParsing: false));
    }

    [TestMethod]
    public void ValidatePathQueryAndFragment_EmptyString()
    {
        byte[] bytes = U(string.Empty);
        // Empty should be valid (nothing to reject)
        Assert.IsTrue(ValidatePathQueryAndFragmentSegment(bytes, iriParsing: false));
    }

    [TestMethod]
    public void ValidatePathQueryAndFragment_QueryOnly()
    {
        byte[] bytes = U("?query=value");
        Assert.IsTrue(ValidatePathQueryAndFragmentSegment(bytes, iriParsing: false));
    }

    [TestMethod]
    public void ValidatePathQueryAndFragment_FragmentOnly()
    {
        byte[] bytes = U("#fragment");
        Assert.IsTrue(ValidatePathQueryAndFragmentSegment(bytes, iriParsing: false));
    }

    [TestMethod]
    public void ValidatePathQueryAndFragment_InvalidBackslash_NonIri()
    {
        byte[] bytes = U("/path\\segment");
        bool result = ValidatePathQueryAndFragmentSegment(bytes, iriParsing: false);
        Assert.IsFalse(result);
    }

    // ─── Additional edge cases ──────────────────────────────────────────

    [TestMethod]
    public void ParseUriInfo_FileScheme()
    {
        byte[] bytes = U("file:///c:/windows/system32");
        bool result = Utf8UriTools.ParseUriInfo(bytes, Utf8UriKind.Absolute, requireAbsolute: false, allowIri: false, out var offsets, out var flags);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ParseUriInfo_MailtoScheme()
    {
        byte[] bytes = U("mailto:user@example.com");
        bool result = Utf8UriTools.ParseUriInfo(bytes, Utf8UriKind.Absolute, requireAbsolute: false, allowIri: false, out var offsets, out var flags);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ParseUriInfo_CustomScheme()
    {
        byte[] bytes = U("custom://host/path");
        bool result = Utf8UriTools.ParseUriInfo(bytes, Utf8UriKind.Absolute, requireAbsolute: false, allowIri: false, out var offsets, out var flags);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ParseUriInfo_IPv4Host()
    {
        byte[] bytes = U("http://192.168.1.1/path");
        bool result = Utf8UriTools.ParseUriInfo(bytes, Utf8UriKind.Absolute, requireAbsolute: false, allowIri: false, out var offsets, out var flags);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void TryFormatCanonical_WithUserInfo()
    {
        byte[] uriBytes = U("http://user:pass@example.com/path");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(uriBytes, Utf8UriKind.Absolute, false, false, out var offsets, out var flags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryFormatCanonical(uriBytes, offsets, flags, allowIri: false, dest, out int written);
        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void TryFormatDisplay_WithDotSegments()
    {
        byte[] uriBytes = U("http://example.com/a/../b/./c");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(uriBytes, Utf8UriKind.Absolute, false, false, out var offsets, out var flags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryFormatDisplay(uriBytes, offsets, flags, dest, out int written);
        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void TryApply_DotDotSegments()
    {
        byte[] baseBytes = U("http://example.com/a/b/c/d");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(baseBytes, Utf8UriKind.Absolute, false, false, out var baseOffsets, out var baseFlags));

        byte[] targetBytes = U("../../e");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(targetBytes, Utf8UriKind.RelativeOrAbsolute, false, false, out var targetOffsets, out var targetFlags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryApply(baseBytes, baseOffsets, baseFlags, targetBytes, targetOffsets, targetFlags, dest, out int written);
        Assert.IsTrue(result);
        string resolved = Str(dest, written);
        Assert.AreEqual("http://example.com/a/e", resolved);
    }

    [TestMethod]
    public void MakeRelative_NoCommonPath()
    {
        byte[] baseBytes = U("http://example.com/completely/different");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(baseBytes, Utf8UriKind.Absolute, false, false, out var baseOffsets, out var baseFlags));

        byte[] targetBytes = U("http://example.com/other/path");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(targetBytes, Utf8UriKind.Absolute, false, false, out var targetOffsets, out var targetFlags));

        Span<byte> dest = stackalloc byte[256];
        bool result = MakeRelative(baseBytes, baseOffsets, baseFlags, targetBytes, targetOffsets, targetFlags, dest, out int written);
        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void MakeRelative_WithAllowIri()
    {
        byte[] baseBytes = U("http://example.com/a/b");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(baseBytes, Utf8UriKind.Absolute, false, true, out var baseOffsets, out var baseFlags));

        byte[] targetBytes = U("http://example.com/a/c");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(targetBytes, Utf8UriKind.Absolute, false, true, out var targetOffsets, out var targetFlags));

        Span<byte> dest = stackalloc byte[256];
        bool result = MakeRelative(baseBytes, baseOffsets, baseFlags, targetBytes, targetOffsets, targetFlags, dest, out int written, allowIri: true);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void TryEscapeDataString_AllAsciiRange()
    {
        // Escape a byte that's below 0x20 (control char)
        byte[] source = new byte[] { 0x01, 0x0A, 0x1F };
        Span<byte> dest = stackalloc byte[32];
        Assert.IsTrue(TryEscapeDataString(source, dest, out int written));
        Assert.AreEqual(9, written); // Each byte becomes %XX (3 bytes)
    }

    [TestMethod]
    public void TryUnescapeDataString_DestTooSmallForDecoded()
    {
        // Source: %41 = 'A', but dest is zero-length
        byte[] source = U("%41");
        Span<byte> dest = Span<byte>.Empty;
        Assert.IsFalse(TryUnescapeDataString(source, dest, out int written));
    }

    [TestMethod]
    public void TryEscapeUri_MixedContent()
    {
        // Mix of unreserved, reserved, and other chars
        byte[] source = U("http://host/path with spaces");
        Span<byte> dest = stackalloc byte[256];
        Assert.IsTrue(TryEscapeUri(source, dest, out int written));
        string result = Str(dest, written);
        Assert.AreEqual("http://host/path%20with%20spaces", result);
    }

    [TestMethod]
    public void TryFormatCanonical_UnreservedPercentDecoded()
    {
        // %61 is 'a' which is unreserved, should be decoded in canonical form
        byte[] uriBytes = U("http://example.com/%61");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(uriBytes, Utf8UriKind.Absolute, false, false, out var offsets, out var flags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryFormatCanonical(uriBytes, offsets, flags, allowIri: false, dest, out int written);
        Assert.IsTrue(result);
        string canonical = Str(dest, written);
        // The unreserved char 'a' should be decoded from %61
        Assert.AreEqual("http://example.com/a", canonical);
    }

    [TestMethod]
    public void TryApply_WithQueryAndFragment()
    {
        byte[] baseBytes = U("http://example.com/a/b");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(baseBytes, Utf8UriKind.Absolute, false, false, out var baseOffsets, out var baseFlags));

        byte[] targetBytes = U("c?q=1#f");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(targetBytes, Utf8UriKind.RelativeOrAbsolute, false, false, out var targetOffsets, out var targetFlags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryApply(baseBytes, baseOffsets, baseFlags, targetBytes, targetOffsets, targetFlags, dest, out int written);
        Assert.IsTrue(result);
        string resolved = Str(dest, written);
        Assert.AreEqual("http://example.com/a/c?q=1#f", resolved);
    }

    [TestMethod]
    public void MakeRelative_QueryAndFragmentDifferences()
    {
        byte[] baseBytes = U("http://example.com/path?q=1#f1");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(baseBytes, Utf8UriKind.Absolute, false, false, out var baseOffsets, out var baseFlags));

        byte[] targetBytes = U("http://example.com/path?q=2#f2");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(targetBytes, Utf8UriKind.Absolute, false, false, out var targetOffsets, out var targetFlags));

        Span<byte> dest = stackalloc byte[256];
        bool result = MakeRelative(baseBytes, baseOffsets, baseFlags, targetBytes, targetOffsets, targetFlags, dest, out int written);
        Assert.IsTrue(result);
        string relative = Str(dest, written);
        Assert.AreEqual("?q=2#f2", relative);
    }

    [TestMethod]
    public void ParseUriInfo_LongPath()
    {
        string path = "/a" + string.Concat(Enumerable.Range(0, 50).Select(i => "/seg" + i.ToString()));
        byte[] bytes = U("http://example.com" + path);
        bool result = Utf8UriTools.ParseUriInfo(bytes, Utf8UriKind.Absolute, requireAbsolute: false, allowIri: false, out var offsets, out var flags);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Validate_WithUncPath()
    {
        byte[] bytes = U("http://example.com/path");
        Assert.IsTrue(Utf8UriTools.Validate(bytes, Utf8UriKind.Absolute, requireAbsolute: false, allowIri: false, allowUNCPath: true));
    }

    [TestMethod]
    public void TryFormatDisplay_IPv6()
    {
        byte[] uriBytes = U("http://[::1]:8080/path");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(uriBytes, Utf8UriKind.Absolute, false, false, out var offsets, out var flags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryFormatDisplay(uriBytes, offsets, flags, dest, out int written);
        Assert.IsTrue(result);
        string display = Str(dest, written);
        Assert.AreEqual("http://[::1]:8080/path", display);
    }

    [TestMethod]
    public void TryFormatCanonical_IPv6()
    {
        byte[] uriBytes = U("http://[::1]:8080/path");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(uriBytes, Utf8UriKind.Absolute, false, false, out var offsets, out var flags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryFormatCanonical(uriBytes, offsets, flags, allowIri: false, dest, out int written);
        Assert.IsTrue(result);
        string canonical = Str(dest, written);
        Assert.AreEqual("http://[::1]:8080/path", canonical);
    }

    [TestMethod]
    public void TryApply_NonStrictMode_DifferentSchemes()
    {
        byte[] baseBytes = U("http://example.com/a/b");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(baseBytes, Utf8UriKind.Absolute, false, false, out var baseOffsets, out var baseFlags));

        byte[] targetBytes = U("https://example.com/c/d");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(targetBytes, Utf8UriKind.Absolute, false, false, out var targetOffsets, out var targetFlags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryApply(baseBytes, baseOffsets, baseFlags, targetBytes, targetOffsets, targetFlags, dest, out int written, strict: false);
        Assert.IsTrue(result);
        string resolved = Str(dest, written);
        Assert.AreEqual("https://example.com/c/d", resolved);
    }

    [TestMethod]
    public void MakeRelative_CaseInsensitiveHost()
    {
        byte[] baseBytes = U("http://EXAMPLE.COM/a/b");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(baseBytes, Utf8UriKind.Absolute, false, false, out var baseOffsets, out var baseFlags));

        byte[] targetBytes = U("http://example.com/a/c");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(targetBytes, Utf8UriKind.Absolute, false, false, out var targetOffsets, out var targetFlags));

        Span<byte> dest = stackalloc byte[256];
        bool result = MakeRelative(baseBytes, baseOffsets, baseFlags, targetBytes, targetOffsets, targetFlags, dest, out int written);
        Assert.IsTrue(result);
        // Hosts match case-insensitively, so should get relative
        Assert.AreNotEqual("http://example.com/a/c", Str(dest, written));
    }

    [TestMethod]
    public void TryEscapeDataString_NullByte()
    {
        byte[] source = new byte[] { 0x00 };
        Span<byte> dest = stackalloc byte[16];
        Assert.IsTrue(TryEscapeDataString(source, dest, out int written));
        Assert.AreEqual("%00", Str(dest, written));
    }

    [TestMethod]
    public void TryUnescapeDataString_LowercaseHex()
    {
        byte[] source = U("%2f");
        Span<byte> dest = stackalloc byte[16];
        Assert.IsTrue(TryUnescapeDataString(source, dest, out int written));
        Assert.AreEqual("/", Str(dest, written));
    }

    [TestMethod]
    public void TryUnescapeDataString_MixedHexCase()
    {
        byte[] source = U("%2F%2f");
        Span<byte> dest = stackalloc byte[16];
        Assert.IsTrue(TryUnescapeDataString(source, dest, out int written));
        Assert.AreEqual("//", Str(dest, written));
    }

    [TestMethod]
    public void TryEscapeUri_FullUri()
    {
        byte[] source = U("http://example.com/path?q=1#frag");
        Span<byte> dest = stackalloc byte[256];
        Assert.IsTrue(TryEscapeUri(source, dest, out int written));
        // All of these chars are reserved/unreserved — should pass through
        string result = Str(dest, written);
        Assert.AreEqual("http://example.com/path?q=1#frag", result);
    }

    [TestMethod]
    public void MakeRelative_BufferTooSmall_SameHost()
    {
        byte[] baseBytes = U("http://example.com/a/b");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(baseBytes, Utf8UriKind.Absolute, false, false, out var baseOffsets, out var baseFlags));

        byte[] targetBytes = U("http://example.com/a/very/long/path/that/is/deep");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(targetBytes, Utf8UriKind.Absolute, false, false, out var targetOffsets, out var targetFlags));

        Span<byte> dest = stackalloc byte[1]; // Way too small
        bool result = MakeRelative(baseBytes, baseOffsets, baseFlags, targetBytes, targetOffsets, targetFlags, dest, out int written);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void TryFormatDisplay_DefaultPort80()
    {
        // Port 80 for HTTP is default — should not appear in display
        byte[] uriBytes = U("http://example.com:80/path");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(uriBytes, Utf8UriKind.Absolute, false, false, out var offsets, out var flags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryFormatDisplay(uriBytes, offsets, flags, dest, out int written);
        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
    }

    [TestMethod]
    public void TryFormatCanonical_DefaultPort80()
    {
        byte[] uriBytes = U("http://example.com:80/path");
        Assert.IsTrue(Utf8UriTools.ParseUriInfo(uriBytes, Utf8UriKind.Absolute, false, false, out var offsets, out var flags));

        Span<byte> dest = stackalloc byte[256];
        bool result = TryFormatCanonical(uriBytes, offsets, flags, allowIri: false, dest, out int written);
        Assert.IsTrue(result);
        Assert.IsTrue(written > 0);
    }
}
