// <copyright file="FunctionAppInvokeAccessException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Serverless.AzureFunctions.Deploy;

/// <summary>
/// Thrown by a <see cref="IFunctionAppConfigurator"/> when the Function App's invoke access cannot be established, for
/// example because Entra is configured and the app's authentication settings do not require it. The deployer reports it
/// as a failed deploy, and nothing is published to the app.
/// </summary>
public sealed class FunctionAppInvokeAccessException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="FunctionAppInvokeAccessException"/> class.</summary>
    public FunctionAppInvokeAccessException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FunctionAppInvokeAccessException"/> class.</summary>
    /// <param name="message">Why the invoke access could not be established.</param>
    public FunctionAppInvokeAccessException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="FunctionAppInvokeAccessException"/> class.</summary>
    /// <param name="message">Why the invoke access could not be established.</param>
    /// <param name="innerException">The underlying failure.</param>
    public FunctionAppInvokeAccessException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}