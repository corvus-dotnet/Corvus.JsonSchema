// <copyright file="CodeGenThrowHelperTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using System.Linq;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for <see cref="CodeGenThrowHelper"/> throw paths to verify
/// correct exception types, messages, and Source property values.
/// </summary>
[TestCategory("coverage")]
[TestClass]
public class CodeGenThrowHelperTests
{
    [TestMethod]
    public void ThrowFormatException_ThrowsWithSource()
    {
        FormatException ex = Assert.ThrowsExactly<FormatException>(() => CodeGenThrowHelper.ThrowFormatException());
        Assert.AreEqual(CodeGenThrowHelper.ExceptionSourceValueToRethrowAsJsonException, ex.Source);
    }

    [TestMethod]
    [DataRow(CodeGenNumericType.Byte)]
    [DataRow(CodeGenNumericType.SByte)]
    [DataRow(CodeGenNumericType.Int16)]
    [DataRow(CodeGenNumericType.Int32)]
    [DataRow(CodeGenNumericType.Int64)]
    [DataRow(CodeGenNumericType.Int128)]
    [DataRow(CodeGenNumericType.UInt16)]
    [DataRow(CodeGenNumericType.UInt32)]
    [DataRow(CodeGenNumericType.UInt64)]
    [DataRow(CodeGenNumericType.UInt128)]
    [DataRow(CodeGenNumericType.Half)]
    [DataRow(CodeGenNumericType.Single)]
    [DataRow(CodeGenNumericType.Double)]
    [DataRow(CodeGenNumericType.Decimal)]
    public void ThrowFormatException_NumericType_ThrowsWithMessageAndSource(CodeGenNumericType numericType)
    {
        FormatException ex = Assert.ThrowsExactly<FormatException>(() => CodeGenThrowHelper.ThrowFormatException(numericType));
        Assert.AreEqual(CodeGenThrowHelper.ExceptionSourceValueToRethrowAsJsonException, ex.Source);
        Assert.IsTrue((ex.Message).Any());
    }

    [TestMethod]
    public void ThrowArgumentException_ArrayBufferLength_ThrowsWithExpectedLength()
    {
        ArgumentException ex = Assert.ThrowsExactly<ArgumentException>(
            () => CodeGenThrowHelper.ThrowArgumentException_ArrayBufferLength("buffer", 42));
        StringAssert.Contains(ex.Message, "42");
        Assert.AreEqual("buffer", ex.ParamName);
    }

    [TestMethod]
    public void GetJsonElementWrongTypeException_ReturnsInvalidOperationWithMessage()
    {
        InvalidOperationException ex = CodeGenThrowHelper.GetJsonElementWrongTypeException(
            JsonValueKind.String, JsonValueKind.Number);
        Assert.AreEqual(CodeGenThrowHelper.ExceptionSourceValueToRethrowAsJsonException, ex.Source);
        StringAssert.Contains(ex.Message, "String");
        StringAssert.Contains(ex.Message, "Number");
    }

    [TestMethod]
    public void ThrowInvalidOperationException_SetRequiredPropertyToUndefined_Throws()
    {
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => CodeGenThrowHelper.ThrowInvalidOperationException_SetRequiredPropertyToUndefined("myProp"));
        Assert.AreEqual(CodeGenThrowHelper.ExceptionSourceValueToRethrowAsJsonException, ex.Source);
        StringAssert.Contains(ex.Message, "myProp");
    }

    [TestMethod]
    public void ThrowInvalidOperationException_PrefixTupleMustBeCreatedFirst_Throws()
    {
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => CodeGenThrowHelper.ThrowInvalidOperationException_PrefixTupleMustBeCreatedFirst());
        Assert.AreEqual(CodeGenThrowHelper.ExceptionSourceValueToRethrowAsJsonException, ex.Source);
    }
}
