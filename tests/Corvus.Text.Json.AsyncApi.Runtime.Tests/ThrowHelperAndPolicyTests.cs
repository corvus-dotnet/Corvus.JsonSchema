// <copyright file="ThrowHelperAndPolicyTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.AsyncApi;

namespace Corvus.Text.Json.AsyncApi.Runtime.Tests;

[TestClass]
public class ThrowHelperAndPolicyTests
{
    [TestMethod]
    public void ThrowMessageHeadersValidationFailed_ThrowsArgumentException()
    {
        ArgumentException ex = Assert.ThrowsExactly<ArgumentException>(
            () => ThrowHelper.ThrowMessageHeadersValidationFailed("headers"));

        Assert.AreEqual("headers", ex.ParamName);
    }

    [TestMethod]
    public void ThrowMessageHeadersValidationFailed_WithDetail_ThrowsArgumentException()
    {
        ArgumentException ex = Assert.ThrowsExactly<ArgumentException>(
            () => ThrowHelper.ThrowMessageHeadersValidationFailed("headers", "some detail"));

        Assert.AreEqual("headers", ex.ParamName);
        StringAssert.Contains(ex.Message, "some detail");
    }

    [TestMethod]
    public void ThrowUnsupportedContentType_ThrowsNotSupportedException()
    {
        NotSupportedException ex = Assert.ThrowsExactly<NotSupportedException>(
            () => ThrowHelper.ThrowUnsupportedContentType("myMessage", "text/xml"));

        StringAssert.Contains(ex.Message, "myMessage");
        StringAssert.Contains(ex.Message, "text/xml");
    }

    [TestMethod]
    public void ThrowUnsupportedSchemaFormat_ThrowsNotSupportedException()
    {
        NotSupportedException ex = Assert.ThrowsExactly<NotSupportedException>(
            () => ThrowHelper.ThrowUnsupportedSchemaFormat("myMessage", "avro"));

        StringAssert.Contains(ex.Message, "myMessage");
        StringAssert.Contains(ex.Message, "avro");
    }

    [TestMethod]
    public void ThrowConsumerNotStarted_ThrowsInvalidOperationException()
    {
        Assert.ThrowsExactly<InvalidOperationException>(
            () => ThrowHelper.ThrowConsumerNotStarted());
    }

    [TestMethod]
    public void ThrowUnsupportedBindingsFormat_ThrowsNotSupportedException()
    {
        NotSupportedException ex = Assert.ThrowsExactly<NotSupportedException>(
            () => ThrowHelper.ThrowUnsupportedBindingsFormat("orders"));

        StringAssert.Contains(ex.Message, "orders");
    }

    [TestMethod]
    public void ThrowArgumentOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        ArgumentOutOfRangeException ex = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ThrowHelper.ThrowArgumentOutOfRange("myParam"));

        Assert.AreEqual("myParam", ex.ParamName);
    }

    [TestMethod]
    public void DefaultMessageErrorPolicy_DefaultConstructor_DeadLettersHandlerErrors()
    {
        DefaultMessageErrorPolicy policy = new();

        Assert.AreEqual(MessageErrorAction.DeadLetter, policy.HandlerAction);
        Assert.AreEqual(MessageErrorAction.DeadLetter, policy.DeserializationAction);
        Assert.AreEqual(MessageErrorAction.Abort, policy.TransportAction);
    }

    [TestMethod]
    public async Task DefaultMessageErrorPolicy_DeserializationError_ReturnsConfiguredAction()
    {
        DefaultMessageErrorPolicy policy = new(MessageErrorAction.Skip, MessageErrorAction.DeadLetter, MessageErrorAction.Abort);
        MessageErrorContext context = new("test-channel"u8.ToArray(), MessageErrorKind.Deserialization);

        MessageErrorAction action = await policy.HandleErrorAsync(
            new Exception("bad json"), context, CancellationToken.None);

        Assert.AreEqual(MessageErrorAction.Skip, action);
    }

    [TestMethod]
    public async Task DefaultMessageErrorPolicy_HandlerError_ReturnsConfiguredAction()
    {
        DefaultMessageErrorPolicy policy = new(MessageErrorAction.Skip, MessageErrorAction.DeadLetter, MessageErrorAction.Abort);
        MessageErrorContext context = new("test-channel"u8.ToArray(), MessageErrorKind.Handler);

        MessageErrorAction action = await policy.HandleErrorAsync(
            new Exception("handler failed"), context, CancellationToken.None);

        Assert.AreEqual(MessageErrorAction.DeadLetter, action);
    }

    [TestMethod]
    public async Task DefaultMessageErrorPolicy_TransportError_ReturnsConfiguredAction()
    {
        DefaultMessageErrorPolicy policy = new(MessageErrorAction.Skip, MessageErrorAction.DeadLetter, MessageErrorAction.Abort);
        MessageErrorContext context = new("test-channel"u8.ToArray(), MessageErrorKind.Transport);

        MessageErrorAction action = await policy.HandleErrorAsync(
            new Exception("connection lost"), context, CancellationToken.None);

        Assert.AreEqual(MessageErrorAction.Abort, action);
    }

    [TestMethod]
    public void MessageAuthenticationContext_ConstructsCorrectly()
    {
        MessageAuthenticationContext ctx = new(SecuritySchemeType.ApiKey, "myApiKey");

        Assert.AreEqual(SecuritySchemeType.ApiKey, ctx.SchemeType);
        Assert.AreEqual("myApiKey", ctx.SchemeName);
        Assert.IsNotNull(ctx.Credentials);
        Assert.AreEqual(0, ctx.Credentials.Count);
    }

    [TestMethod]
    public void MessageAuthenticationContext_CredentialsAreMutable()
    {
        MessageAuthenticationContext ctx = new(SecuritySchemeType.UserPassword, "basic");
        ctx.Credentials["username"] = "admin";
        ctx.Credentials["password"] = "secret";

        Assert.AreEqual(2, ctx.Credentials.Count);
        Assert.AreEqual("admin", ctx.Credentials["username"]);
        Assert.AreEqual("secret", ctx.Credentials["password"]);
    }
}