// <copyright file="FormatValidationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600

using Corvus.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.CoverageGap;

[TestClass]
public class FormatValidationTests
{
    [TestMethod]
    public void TypeIdnHostName_ValidAsciiHostname_IsValid()
    {
        JsonString value = new("example.com");
        ValidationContext result = Validate.TypeIdnHostName(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void TypeIdnHostName_ValidIdnHostname_IsValid()
    {
        JsonString value = new("münchen.de");
        ValidationContext result = Validate.TypeIdnHostName(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void TypeIdnHostName_InvalidHostname_IsInvalid()
    {
        JsonString value = new("-invalid.com");
        ValidationContext result = Validate.TypeIdnHostName(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeIdnHostName_NonString_IsInvalid()
    {
        JsonInteger value = new(42);
        ValidationContext result = Validate.TypeIdnHostName(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeIdnHostName_Invalid_DetailedLevel()
    {
        JsonString value = new("-invalid.com");
        ValidationContext result = Validate.TypeIdnHostName(value, ValidationContext.ValidContext, ValidationLevel.Detailed);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeIdnHostName_Invalid_BasicLevel()
    {
        JsonString value = new("-invalid.com");
        ValidationContext result = Validate.TypeIdnHostName(value, ValidationContext.ValidContext, ValidationLevel.Basic);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeIdnHostName_Valid_VerboseLevel()
    {
        JsonString value = new("example.com");
        ValidationContext result = Validate.TypeIdnHostName(value, ValidationContext.ValidContext, ValidationLevel.Verbose);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void TypeIdnHostName_NonString_DetailedLevel()
    {
        JsonInteger value = new(42);
        ValidationContext result = Validate.TypeIdnHostName(value, ValidationContext.ValidContext, ValidationLevel.Detailed);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeIdnHostName_NonString_BasicLevel()
    {
        JsonInteger value = new(42);
        ValidationContext result = Validate.TypeIdnHostName(value, ValidationContext.ValidContext, ValidationLevel.Basic);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeIdnEmail_ValidEmail_IsValid()
    {
        JsonString value = new("user@example.com");
        ValidationContext result = Validate.TypeIdnEmail(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void TypeIdnEmail_ValidIdnEmail_IsValid()
    {
        JsonString value = new("user@münchen.de");
        ValidationContext result = Validate.TypeIdnEmail(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void TypeIdnEmail_InvalidEmail_IsInvalid()
    {
        JsonString value = new("not-an-email");
        ValidationContext result = Validate.TypeIdnEmail(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeIdnEmail_NonString_IsInvalid()
    {
        JsonInteger value = new(42);
        ValidationContext result = Validate.TypeIdnEmail(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeIdnEmail_Invalid_DetailedLevel()
    {
        JsonString value = new("not-an-email");
        ValidationContext result = Validate.TypeIdnEmail(value, ValidationContext.ValidContext, ValidationLevel.Detailed);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeIdnEmail_Valid_VerboseLevel()
    {
        JsonString value = new("user@example.com");
        ValidationContext result = Validate.TypeIdnEmail(value, ValidationContext.ValidContext, ValidationLevel.Verbose);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void TypeDuration_ValidDuration_IsValid()
    {
        JsonString value = new("P1Y2M3DT4H5M6S");
        ValidationContext result = Validate.TypeDuration(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void TypeDuration_ValidDaysOnly_IsValid()
    {
        JsonString value = new("P30D");
        ValidationContext result = Validate.TypeDuration(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void TypeDuration_ValidTimeOnly_IsValid()
    {
        JsonString value = new("PT1H30M");
        ValidationContext result = Validate.TypeDuration(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void TypeDuration_InvalidDuration_IsInvalid()
    {
        JsonString value = new("not-a-duration");
        ValidationContext result = Validate.TypeDuration(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeDuration_NonString_IsInvalid()
    {
        JsonInteger value = new(42);
        ValidationContext result = Validate.TypeDuration(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeDuration_Invalid_DetailedLevel()
    {
        JsonString value = new("xyz");
        ValidationContext result = Validate.TypeDuration(value, ValidationContext.ValidContext, ValidationLevel.Detailed);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeDuration_Invalid_BasicLevel()
    {
        JsonString value = new("xyz");
        ValidationContext result = Validate.TypeDuration(value, ValidationContext.ValidContext, ValidationLevel.Basic);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeDuration_Valid_VerboseLevel()
    {
        JsonString value = new("P1Y");
        ValidationContext result = Validate.TypeDuration(value, ValidationContext.ValidContext, ValidationLevel.Verbose);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void TypeDuration_NonString_DetailedLevel()
    {
        JsonInteger value = new(42);
        ValidationContext result = Validate.TypeDuration(value, ValidationContext.ValidContext, ValidationLevel.Detailed);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeDuration_NonString_BasicLevel()
    {
        JsonInteger value = new(42);
        ValidationContext result = Validate.TypeDuration(value, ValidationContext.ValidContext, ValidationLevel.Basic);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeHostname_ValidHostname_IsValid()
    {
        JsonString value = new("example.com");
        ValidationContext result = Validate.TypeHostname(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void TypeHostname_InvalidHostname_IsInvalid()
    {
        JsonString value = new("-invalid.com");
        ValidationContext result = Validate.TypeHostname(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeHostname_NonString_IsInvalid()
    {
        JsonInteger value = new(42);
        ValidationContext result = Validate.TypeHostname(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeHostname_Valid_VerboseLevel()
    {
        JsonString value = new("example.com");
        ValidationContext result = Validate.TypeHostname(value, ValidationContext.ValidContext, ValidationLevel.Verbose);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void TypeHostname_Invalid_DetailedLevel()
    {
        JsonString value = new("-invalid");
        ValidationContext result = Validate.TypeHostname(value, ValidationContext.ValidContext, ValidationLevel.Detailed);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeHostname_Invalid_BasicLevel()
    {
        JsonString value = new("-invalid");
        ValidationContext result = Validate.TypeHostname(value, ValidationContext.ValidContext, ValidationLevel.Basic);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeUriTemplate_Valid_IsValid()
    {
        JsonString value = new("http://example.com/{id}");
        ValidationContext result = Validate.TypeUriTemplate(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void TypeUriTemplate_Invalid_IsInvalid()
    {
        JsonString value = new("http://example.com/{invalid");
        ValidationContext result = Validate.TypeUriTemplate(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeUriTemplate_NonString_IsInvalid()
    {
        JsonInteger value = new(42);
        ValidationContext result = Validate.TypeUriTemplate(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeUriTemplate_Valid_VerboseLevel()
    {
        JsonString value = new("http://example.com/{id}");
        ValidationContext result = Validate.TypeUriTemplate(value, ValidationContext.ValidContext, ValidationLevel.Verbose);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void TypeUriTemplate_Invalid_DetailedLevel()
    {
        JsonString value = new("http://example.com/{invalid");
        ValidationContext result = Validate.TypeUriTemplate(value, ValidationContext.ValidContext, ValidationLevel.Detailed);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeUuid_Valid_IsValid()
    {
        JsonString value = new("550e8400-e29b-41d4-a716-446655440000");
        ValidationContext result = Validate.TypeUuid(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void TypeUuid_Invalid_IsInvalid()
    {
        JsonString value = new("not-a-uuid");
        ValidationContext result = Validate.TypeUuid(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeUuid_NonString_IsInvalid()
    {
        JsonInteger value = new(42);
        ValidationContext result = Validate.TypeUuid(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeUuid_Valid_VerboseLevel()
    {
        JsonString value = new("550e8400-e29b-41d4-a716-446655440000");
        ValidationContext result = Validate.TypeUuid(value, ValidationContext.ValidContext, ValidationLevel.Verbose);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void TypeUuid_Invalid_DetailedLevel()
    {
        JsonString value = new("not-a-uuid");
        ValidationContext result = Validate.TypeUuid(value, ValidationContext.ValidContext, ValidationLevel.Detailed);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeEmail_Valid_IsValid()
    {
        JsonString value = new("user@example.com");
        ValidationContext result = Validate.TypeEmail(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void TypeEmail_Invalid_IsInvalid()
    {
        JsonString value = new("not-an-email");
        ValidationContext result = Validate.TypeEmail(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeEmail_Valid_VerboseLevel()
    {
        JsonString value = new("user@example.com");
        ValidationContext result = Validate.TypeEmail(value, ValidationContext.ValidContext, ValidationLevel.Verbose);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void TypeInt128_Valid_IsValid()
    {
        JsonNumber value = new(42);
        ValidationContext result = Validate.TypeInt128(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void TypeInt128_NonNumber_IsInvalid()
    {
        JsonString value = new("not-a-number");
        ValidationContext result = Validate.TypeInt128(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeInt128_FloatingPoint_IsInvalid()
    {
        JsonNumber value = new(3.14);
        ValidationContext result = Validate.TypeInt128(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeInt128_Valid_VerboseLevel()
    {
        JsonNumber value = new(100);
        ValidationContext result = Validate.TypeInt128(value, ValidationContext.ValidContext, ValidationLevel.Verbose);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void TypeUInt128_Valid_IsValid()
    {
        JsonNumber value = new(42);
        ValidationContext result = Validate.TypeUInt128(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void TypeUInt128_Negative_IsInvalid()
    {
        JsonNumber value = new(-1);
        ValidationContext result = Validate.TypeUInt128(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeHalf_Valid_IsValid()
    {
        JsonNumber value = new(1.5);
        ValidationContext result = Validate.TypeHalf(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void TypeHalf_NonNumber_IsInvalid()
    {
        JsonString value = new("not-a-number");
        ValidationContext result = Validate.TypeHalf(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeSByte_Valid_IsValid()
    {
        JsonNumber value = new(42);
        ValidationContext result = Validate.TypeSByte(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void TypeSByte_OutOfRange_IsInvalid()
    {
        JsonNumber value = new(200);
        ValidationContext result = Validate.TypeSByte(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeByte_Valid_IsValid()
    {
        JsonNumber value = new(200);
        ValidationContext result = Validate.TypeByte(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void TypeByte_OutOfRange_IsInvalid()
    {
        JsonNumber value = new(300);
        ValidationContext result = Validate.TypeByte(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeInt16_Valid_IsValid()
    {
        JsonNumber value = new(1000);
        ValidationContext result = Validate.TypeInt16(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void TypeInt16_OutOfRange_IsInvalid()
    {
        JsonNumber value = new(40000);
        ValidationContext result = Validate.TypeInt16(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void TypeUInt16_Valid_IsValid()
    {
        JsonNumber value = new(40000);
        ValidationContext result = Validate.TypeUInt16(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void TypeUInt16_Negative_IsInvalid()
    {
        JsonNumber value = new(-1);
        ValidationContext result = Validate.TypeUInt16(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsFalse(result.IsValid);
    }
}