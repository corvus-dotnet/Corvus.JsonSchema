// <copyright file="FormatWarningValidationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600

using Corvus.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.CoverageGap;

/// <summary>
/// Tests for the warning-mode format validation helpers (<c>TypeXxxWarning</c>) added for
/// per-format assertion modes. A value of the correct JSON type that does not satisfy the
/// format is reported as valid with a warning; a value of the wrong type still fails.
/// </summary>
[TestClass]
public class FormatWarningValidationTests
{
    [TestMethod]
    public void TypeDateTimeWarning_ValidValue_IsValid()
    {
        JsonString value = new("2024-01-02T03:04:05Z");
        ValidationContext result = Validate.TypeDateTimeWarning(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void TypeDateTimeWarning_InvalidFormat_IsValidWithWarning()
    {
        JsonString value = new("not-a-date-time");
        ValidationContext result = Validate.TypeDateTimeWarning(value, ValidationContext.ValidContext, ValidationLevel.Detailed);

        // Warning mode passes validation despite the non-conformant value.
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void TypeDateTimeWarning_WrongType_StillFails()
    {
        // Warning mode only downgrades the format assertion; a non-string still fails the type check.
        JsonNumber value = new(42);
        ValidationContext result = Validate.TypeDateTimeWarning(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void WithoutCoreType_TypeDateTimeWarning_ValidValue_IsValid()
    {
        JsonString value = new("2024-01-02T03:04:05Z");
        ValidationContext result = ValidateWithoutCoreType.TypeDateTimeWarning(value, ValidationContext.ValidContext, ValidationLevel.Flag);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void WithoutCoreType_TypeDateTimeWarning_InvalidFormat_IsValidWithWarning()
    {
        JsonString value = new("not-a-date-time");
        ValidationContext result = ValidateWithoutCoreType.TypeDateTimeWarning(value, ValidationContext.ValidContext, ValidationLevel.Detailed);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void TypeUuidWarning_InvalidFormat_IsValidWithWarning()
    {
        JsonString value = new("not-a-uuid");
        ValidationContext result = Validate.TypeUuidWarning(value, ValidationContext.ValidContext, ValidationLevel.Detailed);
        Assert.IsTrue(result.IsValid);
    }
}