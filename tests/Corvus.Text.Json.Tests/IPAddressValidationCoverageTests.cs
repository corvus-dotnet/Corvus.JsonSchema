// <copyright file="IPAddressValidationCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests targeting uncovered paths in IPv6AddressHelper and IPv4AddressHelper.
/// </summary>
[TestClass]
public class IPAddressValidationCoverageTests
{
    #region IPv6 bracketed address paths (L80-83, L147-189)

    [TestMethod]
    public void IPv6_BracketedAddress_IsValid()
    {
        // Bracketed IPv6 address — triggers needsClosingBracket path
        Assert.IsTrue(IPAddressParser.IsValidIPV6("[::1]"u8));
    }

    [TestMethod]
    public void IPv6_BracketedFullAddress_IsValid()
    {
        Assert.IsTrue(IPAddressParser.IsValidIPV6("[2001:db8::1]"u8));
    }

    [TestMethod]
    public void IPv6_BracketedWithDecimalPort_IsValid()
    {
        // Triggers L177-186: decimal port parsing after ]
        Assert.IsTrue(IPAddressParser.IsValidIPV6("[::1]:80"u8));
    }

    [TestMethod]
    public void IPv6_BracketedWithHexPort_IsValid()
    {
        // Triggers L164-176: hex port parsing after ]:0x
        Assert.IsTrue(IPAddressParser.IsValidIPV6("[::1]:0x50"u8));
    }

    [TestMethod]
    public void IPv6_BracketedWithLargeDecimalPort_IsValid()
    {
        Assert.IsTrue(IPAddressParser.IsValidIPV6("[fe80::1]:8080"u8));
    }

    [TestMethod]
    public void IPv6_BracketedWithInvalidCharAfterBracket_ReturnsFalse()
    {
        // Triggers L157-159: after ], next char not ':'
        Assert.IsFalse(IPAddressParser.IsValidIPV6("[::1]x"u8));
    }

    [TestMethod]
    public void IPv6_BracketedWithNonDigitInDecimalPort_ReturnsFalse()
    {
        // Triggers L182-184: non-digit character in decimal port
        Assert.IsFalse(IPAddressParser.IsValidIPV6("[::1]:8z"u8));
    }

    [TestMethod]
    public void IPv6_BracketedWithNonHexInHexPort_ReturnsFalse()
    {
        // Triggers L171-173: non-hex character in hex port
        Assert.IsFalse(IPAddressParser.IsValidIPV6("[::1]:0xGG"u8));
    }

    [TestMethod]
    public void IPv6_UnmatchedOpenBracket_ReturnsFalse()
    {
        // Address starts with [ but never closes — needsClosingBracket remains true
        Assert.IsFalse(IPAddressParser.IsValidIPV6("[::1"u8));
    }

    [TestMethod]
    public void IPv6_CloseBracketWithoutOpen_ReturnsFalse()
    {
        // Triggers L148-150: ']' case when needsClosingBracket is false
        Assert.IsFalse(IPAddressParser.IsValidIPV6("::1]"u8));
    }

    #endregion

    #region IPv6 scope identifier paths (L132-145)

    [TestMethod]
    public void IPv6_ScopeIdentifier_AllowScope_IsValid()
    {
        // Triggers L132-137: scope parsing with loop
        Assert.IsTrue(IPAddressParser.IsValidIPV6("fe80::1%eth0"u8, disallowScope: false));
    }

    [TestMethod]
    public void IPv6_ScopeIdentifier_DisallowScope_ReturnsFalse()
    {
        // Triggers L124-127 (already covered) — but confirms behavior
        Assert.IsFalse(IPAddressParser.IsValidIPV6("fe80::1%eth0"u8, disallowScope: true));
    }

    [TestMethod]
    public void IPv6_ScopeWithBracketTerminator_AllowScope_IsValid()
    {
        // Scope terminated by ']' — triggers L135-137 goto case ']'
        Assert.IsTrue(IPAddressParser.IsValidIPV6("[fe80::1%eth0]"u8, disallowScope: false));
    }

    [TestMethod]
    public void IPv6_ScopeWithSlashTerminator_AllowScope_ReturnsFalse()
    {
        // Scope terminated by '/' — triggers L139-141 goto case '/'
        // '/' in IPv6 means prefix which is invalid
        Assert.IsFalse(IPAddressParser.IsValidIPV6("fe80::1%eth0/64"u8, disallowScope: false));
    }

    [TestMethod]
    public void IPv6_ScopeAtEnd_AllowScope_IsValid()
    {
        // Scope runs to end of string — triggers L132 loop exit (L143 break, L145 break)
        Assert.IsTrue(IPAddressParser.IsValidIPV6("fe80::1%0"u8, disallowScope: false));
    }

    #endregion

    #region IPv6 embedded IPv4 double-dot (L219-220)

    [TestMethod]
    public void IPv6_EmbeddedIPv4_IsValid()
    {
        // Standard IPv4-mapped IPv6 address
        Assert.IsTrue(IPAddressParser.IsValidIPV6("::ffff:192.168.1.1"u8));
    }

    [TestMethod]
    public void IPv6_EmbeddedIPv4_InvalidIPv4_ReturnsFalse()
    {
        // IPv4 section is invalid (999.999.999.999)
        Assert.IsFalse(IPAddressParser.IsValidIPV6("::ffff:999.999.999.999"u8));
    }

    [TestMethod]
    public void IPv6_EmbeddedIPv4_DoubleZeroPrefix_ReturnsFalse()
    {
        // Triggers L116-119 in IsValidCanonical: 00 prefix disallowed
        Assert.IsFalse(IPAddressParser.IsValidIPV6("::ffff:00.168.1.1"u8));
    }

    #endregion

    #region IPv6 basic edge cases

    [TestMethod]
    public void IPv6_NoColon_ReturnsFalse()
    {
        // IPAddressParser.IsValidIPV6 checks for ':' first
        Assert.IsFalse(IPAddressParser.IsValidIPV6("not-an-ipv6"u8));
    }

    [TestMethod]
    public void IPv6_PrefixSlash_ReturnsFalse()
    {
        // '/' indicates a prefix which is invalid (L212-215)
        Assert.IsFalse(IPAddressParser.IsValidIPV6("::1/64"u8));
    }

    [TestMethod]
    public void IPv6_DoubleCompressor_ReturnsFalse()
    {
        // Two '::' compressors — invalid (L196-200)
        Assert.IsFalse(IPAddressParser.IsValidIPV6("2001::db8::1"u8));
    }

    [TestMethod]
    public void IPv6_LeadingSingleColon_ReturnsFalse()
    {
        // Starts with single ':' (not '::') — invalid (L92-95)
        Assert.IsFalse(IPAddressParser.IsValidIPV6(":1:2:3:4:5:6:7:8"u8));
    }

    [TestMethod]
    public void IPv6_SegmentTooLong_ReturnsFalse()
    {
        // A segment > 4 hex chars is invalid (L109-112)
        Assert.IsFalse(IPAddressParser.IsValidIPV6("2001:db800:85a3::1"u8));
    }

    [TestMethod]
    public void IPv6_InvalidCharacter_ReturnsFalse()
    {
        // Default case: invalid character (L237-238)
        Assert.IsFalse(IPAddressParser.IsValidIPV6("2001:db8:zzzz::1"u8));
    }

    #endregion

    #region IPv4 canonical validation (L117, L119)

    [TestMethod]
    public void IPv4_Valid_Canonical()
    {
        Assert.IsTrue(IPAddressParser.IsValidIPV4("192.168.1.1"u8));
    }

    [TestMethod]
    public void IPv4_AllZeros()
    {
        Assert.IsTrue(IPAddressParser.IsValidIPV4("0.0.0.0"u8));
    }

    [TestMethod]
    public void IPv4_DoubleZeroPrefix_ReturnsFalse()
    {
        // Triggers L116-119: 00 prefix is not allowed in canonical
        Assert.IsFalse(IPAddressParser.IsValidIPV4("192.168.001.1"u8));
    }

    [TestMethod]
    public void IPv4_LeadingZero_SingleDigitAfter_IsValid()
    {
        // Leading zero followed by dot is valid (just "0")
        Assert.IsTrue(IPAddressParser.IsValidIPV4("0.168.1.1"u8));
    }

    [TestMethod]
    public void IPv4_TooLong_ReturnsFalse()
    {
        // Exceeds MaxIPv4StringLength (15)
        Assert.IsFalse(IPAddressParser.IsValidIPV4("192.168.001.001.x"u8));
    }

    [TestMethod]
    public void IPv4_SegmentTooLarge_ReturnsFalse()
    {
        // Segment value > 255
        Assert.IsFalse(IPAddressParser.IsValidIPV4("192.168.256.1"u8));
    }

    [TestMethod]
    public void IPv4_TooFewDots_ReturnsFalse()
    {
        // Only 2 dots (3 segments) — needs exactly 3 dots (4 segments)
        Assert.IsFalse(IPAddressParser.IsValidIPV4("192.168.1"u8));
    }

    [TestMethod]
    public void IPv4_TooManyDots_ReturnsFalse()
    {
        // 4 dots (5 segments)
        Assert.IsFalse(IPAddressParser.IsValidIPV4("1.2.3.4.5"u8));
    }

    [TestMethod]
    public void IPv4_EmptySegment_ReturnsFalse()
    {
        // Consecutive dots
        Assert.IsFalse(IPAddressParser.IsValidIPV4("192..168.1"u8));
    }

    [TestMethod]
    public void IPv4_TrailingDot_ReturnsFalse()
    {
        Assert.IsFalse(IPAddressParser.IsValidIPV4("192.168.1.1."u8));
    }

    [TestMethod]
    public void IPv4_NonDigitChar_ReturnsFalse()
    {
        Assert.IsFalse(IPAddressParser.IsValidIPV4("192.168.a.1"u8));
    }

    #endregion

    #region IPv4 non-canonical paths (requireCanonical: false)

    [TestMethod]
    public void IPv4_HexFormat_NonCanonical_IsValid()
    {
        // 0x7f = 127, triggers L213-217 hex base path
        Assert.IsTrue(IPAddressParser.IsValidIPV4("0x7f.0.0.1"u8, requireCanonical: false));
    }

    [TestMethod]
    public void IPv4_HexFormat_Canonical_ReturnsFalse()
    {
        // Hex format is rejected when requireCanonical = true (L208-210)
        Assert.IsFalse(IPAddressParser.IsValidIPV4("0x7f.0.0.1"u8, requireCanonical: true));
    }

    [TestMethod]
    public void IPv4_OctalFormat_NonCanonical_IsValid()
    {
        // 0177 = 127 (octal), triggers L225-226 octal base path
        Assert.IsTrue(IPAddressParser.IsValidIPV4("0177.0.0.1"u8, requireCanonical: false));
    }

    [TestMethod]
    public void IPv4_OctalFormat_Canonical_ReturnsFalse()
    {
        // Octal format rejected when requireCanonical (L220-222)
        Assert.IsFalse(IPAddressParser.IsValidIPV4("0177.0.0.1"u8, requireCanonical: true));
    }

    [TestMethod]
    public void IPv4_HexOverflow_ReturnsFalse()
    {
        // Triggers L243-245: hex value exceeds MaxIPv4Value
        Assert.IsFalse(IPAddressParser.IsValidIPV4("0xFFFFFFFF1.0.0.1"u8, requireCanonical: false));
    }

    [TestMethod]
    public void IPv4_InvalidHexDigit_NonCanonical_ReturnsFalse()
    {
        // 0xGG — 'G' is not a valid hex char, triggers break at L237-238
        Assert.IsFalse(IPAddressParser.IsValidIPV4("0xGG.0.0.1"u8, requireCanonical: false));
    }

    [TestMethod]
    public void IPv4_TwoDotFormat_NonCanonical_IsValid()
    {
        // 2-dot format: a.b.c where c is 16-bit (triggers L309-316)
        Assert.IsTrue(IPAddressParser.IsValidIPV4("10.0.65535"u8, requireCanonical: false));
    }

    [TestMethod]
    public void IPv4_OneDotFormat_NonCanonical_IsValid()
    {
        // 1-dot format: a.b where b is 24-bit (triggers L306)
        Assert.IsTrue(IPAddressParser.IsValidIPV4("10.16777215"u8, requireCanonical: false));
    }

    [TestMethod]
    public void IPv4_NoDotFormat_NonCanonical_IsValid()
    {
        // 0-dot format: single 32-bit number (triggers L297)
        Assert.IsTrue(IPAddressParser.IsValidIPV4("2130706433"u8, requireCanonical: false));
    }

    [TestMethod]
    public void IPv4_TwoDotOverflow_ReturnsFalse()
    {
        // 2-dot: last part > 0xFFFF (triggers L311-313)
        Assert.IsFalse(IPAddressParser.IsValidIPV4("10.0.65536"u8, requireCanonical: false));
    }

    [TestMethod]
    public void IPv4_OneDotOverflow_ReturnsFalse()
    {
        // 1-dot: last part > 0xFFFFFF (triggers L301-303)
        Assert.IsFalse(IPAddressParser.IsValidIPV4("10.16777216"u8, requireCanonical: false));
    }

    [TestMethod]
    public void IPv4_Terminator_Slash_NonCanonical_ReturnsFalse()
    {
        // Triggers L281-285: '/' as terminator in ParseNonCanonical
        // IPAddressParser.IsValidIPV4 rejects it because end != ipSpan.Length after terminator
        Assert.IsFalse(IPAddressParser.IsValidIPV4("192.168.1.1/24"u8, requireCanonical: false));
    }

    [TestMethod]
    public void IPv4_FourDots_NonCanonical_ReturnsFalse()
    {
        // More than 3 dots — triggers L330 (default case)
        Assert.IsFalse(IPAddressParser.IsValidIPV4("1.2.3.4.5"u8, requireCanonical: false));
    }

    #endregion
}
