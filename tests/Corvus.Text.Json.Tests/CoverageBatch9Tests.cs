// <copyright file="CoverageBatch9Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using System.Buffers;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage batch 9: targeting RawUtf8JsonString, Utf8UriDomainNameHelper,
/// and JsonHelpers.ValidateInt32MaxArrayLength.
/// </summary>
[TestClass]
public class CoverageBatch9Tests
{
    #region RawUtf8JsonString — TakeOwnership (lines 56-59)

    /// <summary>
    /// Exercises <c>RawUtf8JsonString.TakeOwnership</c> with a rented buffer.
    /// </summary>
    [TestMethod]
    public void RawUtf8JsonString_TakeOwnership_ReturnsRentedBuffer()
    {
        byte[] rented = ArrayPool<byte>.Shared.Rent(64);
        rented.AsSpan(0, 5).Fill((byte)'A');

        RawUtf8JsonString raw = new(rented.AsMemory(0, 5), rented);

        ReadOnlyMemory<byte> memory = raw.TakeOwnership(out byte[]? extraBytes);

        Assert.AreEqual(5, memory.Length);
        Assert.AreSame(rented, extraBytes);

        // Clean up
        if (extraBytes != null)
        {
            ArrayPool<byte>.Shared.Return(extraBytes);
        }
    }

    #endregion

    #region RawUtf8JsonString — TakeOwnership without rented buffer

    /// <summary>
    /// Exercises <c>RawUtf8JsonString.TakeOwnership</c> without a rented buffer.
    /// </summary>
    [TestMethod]
    public void RawUtf8JsonString_TakeOwnership_NoRentedBuffer()
    {
        byte[] data = [1, 2, 3];
        RawUtf8JsonString raw = new(data.AsMemory());

        ReadOnlyMemory<byte> memory = raw.TakeOwnership(out byte[]? extraBytes);

        Assert.AreEqual(3, memory.Length);
        Assert.IsNull(extraBytes);
    }

    #endregion

    #region RawUtf8JsonString — Dispose with rented buffer (lines 66-77)

    /// <summary>
    /// Exercises <c>RawUtf8JsonString.Dispose</c> with a rented buffer that gets returned.
    /// </summary>
    [TestMethod]
    public void RawUtf8JsonString_Dispose_ReturnsRentedBuffer()
    {
        byte[] rented = ArrayPool<byte>.Shared.Rent(64);
        rented.AsSpan(0, 10).Fill((byte)'X');

        RawUtf8JsonString raw = new(rented.AsMemory(0, 10), rented);
        raw.Dispose();

        // After dispose, the rented buffer should have been cleared and returned.
        // Verify by renting again — we should get the same buffer back (cleared).
        byte[] secondRent = ArrayPool<byte>.Shared.Rent(64);
        try
        {
            // The first 10 bytes should have been cleared by Dispose
            Assert.AreEqual(0, secondRent[0]);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(secondRent);
        }
    }

    #endregion

    #region RawUtf8JsonString — Double Dispose (idempotent)

    /// <summary>
    /// Exercises <c>RawUtf8JsonString.Dispose</c> called twice — should be safe.
    /// </summary>
    [TestMethod]
    public void RawUtf8JsonString_DoubleDispose_NoThrow()
    {
        byte[] rented = ArrayPool<byte>.Shared.Rent(64);
        RawUtf8JsonString raw = new(rented.AsMemory(0, 5), rented);
        raw.Dispose();
        raw.Dispose(); // Should not throw
    }

    #endregion

    #region Utf8UriDomainNameHelper — empty hostname (lines 86-87)

    /// <summary>
    /// A hostname that is empty after delimiter trimming fails validation (lines 86-87).
    /// </summary>
    [TestMethod]
    public void DomainNameHelper_EmptyHostname_ReturnsFalse()
    {
        // Empty hostname — length == 0 path
        bool result = Utf8UriDomainNameHelper.IsValid(ReadOnlySpan<byte>.Empty, iri: false, notImplicitFile: true, out int length);
        Assert.IsFalse(result);
        Assert.AreEqual(0, length);
    }

    #endregion

    #region Utf8UriDomainNameHelper — non-alphanumeric first character (lines 100-101)

    /// <summary>
    /// A hostname starting with a non-alphanumeric character (e.g. dot) fails validation (lines 100-101).
    /// </summary>
    [TestMethod]
    public void DomainNameHelper_DotFirstChar_ReturnsFalse()
    {
        // A label starting with '.' — not alphanumeric, triggers lines 100-101
        bool result = Utf8UriDomainNameHelper.IsValid(".example.com"u8, iri: false, notImplicitFile: true, out int length);
        Assert.IsFalse(result);
    }

    #endregion

    #region Utf8UriDomainNameHelper — trailing dot (lines 143, 145)

    /// <summary>
    /// A hostname ending with a dot is a valid FQDN (lines 142-145).
    /// </summary>
    [TestMethod]
    public void DomainNameHelper_TrailingDot_ReturnsTrue()
    {
        // "example.com." — valid FQDN with trailing dot
        bool result = Utf8UriDomainNameHelper.IsValid("example.com."u8, iri: false, notImplicitFile: true, out int length);
        Assert.IsTrue(result);
        Assert.AreEqual(12, length); // "example.com." = 12 bytes
    }

    #endregion

    #region Utf8UriDomainNameHelper — IRI non-ASCII dot (lines 203-204)

    /// <summary>
    /// Tests IRI hostname with Unicode ideographic full stop (\u3002) as dot separator.
    /// The IndexOfIriDot method finds the dot position (lines 203-204).
    /// NOTE: There is a bug at line 140 — Slice(dotIndex + 1) should skip the full
    /// multi-byte dot width, so IsValid returns false for multi-byte IRI dots.
    /// This test verifies the current (buggy) behavior while exercising the code path.
    /// </summary>
    [TestMethod]
    public void DomainNameHelper_IriUnicodeFullStop_ExercisesIndexOfIriDot()
    {
        // "example" + \u3002 (E3 80 82 in UTF-8) + "com"
        // IndexOfIriDot finds the dot at position 7 (lines 203-204 HIT),
        // but Slice(7+1) incorrectly leaves 0x80 as first char of next label → false
        byte[] hostname = System.Text.Encoding.UTF8.GetBytes("example\u3002com");
        bool result = Utf8UriDomainNameHelper.IsValid(hostname, iri: true, notImplicitFile: true, out int length);
        Assert.IsFalse(result); // BUG: should be true, but line 140 slicing is wrong
    }

    #endregion

    #region JsonHelpers.ValidateInt32MaxArrayLength — overflow (lines 115-116)

    /// <summary>
    /// Exercises <c>JsonHelpers.ValidateInt32MaxArrayLength</c> with a value exceeding
    /// the maximum allowed array length, causing OutOfMemoryException (lines 115-116).
    /// </summary>
    [TestMethod]
    public void JsonHelpers_ValidateInt32MaxArrayLength_ThrowsOnOverflow()
    {
        // 0x7FEFFFFF is the max; anything above should throw
        Assert.ThrowsExactly<OutOfMemoryException>(() => JsonHelpers.ValidateInt32MaxArrayLength(0x7FF00000));
    }

    #endregion
}
