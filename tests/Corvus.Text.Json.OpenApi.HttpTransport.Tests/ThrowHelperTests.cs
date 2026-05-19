// <copyright file="ThrowHelperTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi.HttpTransport.Tests;

/// <summary>
/// Tests for the <see cref="ThrowHelper"/> centralized exception-throwing helpers.
/// </summary>
[TestClass]
public class ThrowHelperTests
{
    [TestMethod]
    public void ThrowNoPathParameters_ThrowsInvalidOperationException()
    {
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            ThrowHelper.ThrowNoPathParameters);
        StringAssert.Contains(ex.Message, "path");
    }

    [TestMethod]
    public void ThrowNoQueryParameters_ThrowsInvalidOperationException()
    {
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            ThrowHelper.ThrowNoQueryParameters);
        StringAssert.Contains(ex.Message, "query");
    }

    [TestMethod]
    public void ThrowNoHeaderParameters_ThrowsInvalidOperationException()
    {
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            ThrowHelper.ThrowNoHeaderParameters);
        StringAssert.Contains(ex.Message, "header");
    }

    [TestMethod]
    public void ThrowNoCookieParameters_ThrowsInvalidOperationException()
    {
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            ThrowHelper.ThrowNoCookieParameters);
        StringAssert.Contains(ex.Message, "cookie");
    }

    [TestMethod]
    public void ThrowRequestParameterValidationFailed_ThrowsArgumentException()
    {
        ArgumentException ex = Assert.ThrowsExactly<ArgumentException>(
            () => ThrowHelper.ThrowRequestParameterValidationFailed("myParam"));
        StringAssert.Contains(ex.Message, "myParam");
    }

    [TestMethod]
    public void ThrowRequestParameterValidationFailed_WithDetail_ThrowsArgumentException()
    {
        ArgumentException ex = Assert.ThrowsExactly<ArgumentException>(
            () => ThrowHelper.ThrowRequestParameterValidationFailed("myParam", "detail info"));
        StringAssert.Contains(ex.Message, "myParam");
        StringAssert.Contains(ex.Message, "detail info");
    }

    [TestMethod]
    public void ThrowRequestBodyValidationFailed_ThrowsInvalidOperationException()
    {
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            ThrowHelper.ThrowRequestBodyValidationFailed);
        Assert.IsNotNull(ex.Message);
    }

    [TestMethod]
    public void ThrowRequestBodyValidationFailed_WithDetail_ThrowsInvalidOperationException()
    {
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => ThrowHelper.ThrowRequestBodyValidationFailed("validation detail"));
        StringAssert.Contains(ex.Message, "validation detail");
    }

    [TestMethod]
    public void ThrowUnableToResolveRequestBodyRef_ThrowsInvalidOperationException()
    {
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            ThrowHelper.ThrowUnableToResolveRequestBodyRef);
        Assert.IsNotNull(ex.Message);
    }

    [TestMethod]
    public void ThrowUnableToResolveResponseRef_ThrowsInvalidOperationException()
    {
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            ThrowHelper.ThrowUnableToResolveResponseRef);
        Assert.IsNotNull(ex.Message);
    }

    [TestMethod]
    public void ThrowUnableToResolveHeaderRef_ThrowsInvalidOperationException()
    {
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            ThrowHelper.ThrowUnableToResolveHeaderRef);
        Assert.IsNotNull(ex.Message);
    }

    [TestMethod]
    public void ThrowResponseBodyValidationFailed_ThrowsInvalidOperationException()
    {
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => ThrowHelper.ThrowResponseBodyValidationFailed(500));
        StringAssert.Contains(ex.Message, "500");
    }

    [TestMethod]
    public void ThrowResponseBodyValidationFailed_WithDetail_ThrowsInvalidOperationException()
    {
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            () => ThrowHelper.ThrowResponseBodyValidationFailed(422, "schema errors"));
        StringAssert.Contains(ex.Message, "422");
        StringAssert.Contains(ex.Message, "schema errors");
    }

    [TestMethod]
    public void ThrowFormBodyMustBeObject_ThrowsInvalidOperationException()
    {
        InvalidOperationException ex = Assert.ThrowsExactly<InvalidOperationException>(
            ThrowHelper.ThrowFormBodyMustBeObject);
        Assert.IsNotNull(ex.Message);
    }
}