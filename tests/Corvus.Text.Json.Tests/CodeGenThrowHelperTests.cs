// <copyright file="CodeGenThrowHelperTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for <see cref="CodeGenThrowHelper"/> throw paths to verify
/// correct exception types, messages, and Source property values.
/// </summary>
[Trait("Category", "coverage")]
public class CodeGenThrowHelperTests
{
    [Fact]
    public void ThrowFormatException_ThrowsWithSource()
    {
        FormatException ex = Assert.Throws<FormatException>(() => CodeGenThrowHelper.ThrowFormatException());
        Assert.Equal(CodeGenThrowHelper.ExceptionSourceValueToRethrowAsJsonException, ex.Source);
    }

    [Theory]
    [InlineData(CodeGenNumericType.Byte)]
    [InlineData(CodeGenNumericType.SByte)]
    [InlineData(CodeGenNumericType.Int16)]
    [InlineData(CodeGenNumericType.Int32)]
    [InlineData(CodeGenNumericType.Int64)]
    [InlineData(CodeGenNumericType.Int128)]
    [InlineData(CodeGenNumericType.UInt16)]
    [InlineData(CodeGenNumericType.UInt32)]
    [InlineData(CodeGenNumericType.UInt64)]
    [InlineData(CodeGenNumericType.UInt128)]
    [InlineData(CodeGenNumericType.Half)]
    [InlineData(CodeGenNumericType.Single)]
    [InlineData(CodeGenNumericType.Double)]
    [InlineData(CodeGenNumericType.Decimal)]
    public void ThrowFormatException_NumericType_ThrowsWithMessageAndSource(CodeGenNumericType numericType)
    {
        FormatException ex = Assert.Throws<FormatException>(() => CodeGenThrowHelper.ThrowFormatException(numericType));
        Assert.Equal(CodeGenThrowHelper.ExceptionSourceValueToRethrowAsJsonException, ex.Source);
        Assert.NotEmpty(ex.Message);
    }

    [Fact]
    public void ThrowArgumentException_ArrayBufferLength_ThrowsWithExpectedLength()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => CodeGenThrowHelper.ThrowArgumentException_ArrayBufferLength("buffer", 42));
        Assert.Contains("42", ex.Message);
        Assert.Equal("buffer", ex.ParamName);
    }

    [Fact]
    public void GetJsonElementWrongTypeException_ReturnsInvalidOperationWithMessage()
    {
        InvalidOperationException ex = CodeGenThrowHelper.GetJsonElementWrongTypeException(
            JsonValueKind.String, JsonValueKind.Number);
        Assert.Equal(CodeGenThrowHelper.ExceptionSourceValueToRethrowAsJsonException, ex.Source);
        Assert.Contains("String", ex.Message);
        Assert.Contains("Number", ex.Message);
    }

    [Fact]
    public void ThrowInvalidOperationException_SetRequiredPropertyToUndefined_Throws()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => CodeGenThrowHelper.ThrowInvalidOperationException_SetRequiredPropertyToUndefined("myProp"));
        Assert.Equal(CodeGenThrowHelper.ExceptionSourceValueToRethrowAsJsonException, ex.Source);
        Assert.Contains("myProp", ex.Message);
    }

    [Fact]
    public void ThrowInvalidOperationException_PrefixTupleMustBeCreatedFirst_Throws()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => CodeGenThrowHelper.ThrowInvalidOperationException_PrefixTupleMustBeCreatedFirst());
        Assert.Equal(CodeGenThrowHelper.ExceptionSourceValueToRethrowAsJsonException, ex.Source);
    }
}
