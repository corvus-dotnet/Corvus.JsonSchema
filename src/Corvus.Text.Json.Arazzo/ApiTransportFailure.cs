// <copyright file="ApiTransportFailure.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net.Http;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// Classifies the exceptions a step's API call raises when the exchange itself failed, as opposed to completing with a
/// response the step's criteria can judge.
/// </summary>
/// <remarks>
/// <para>
/// A generated executor filters on this around each operation step's client call. A transport failure has no response,
/// so the step has failed and its <c>onFailure</c> actions are dispatched without response context, the same way a
/// step's own declared timeout is. Each retry an action takes is an attempt like any other and is counted as fuel
/// (ADR 0068), so a source that never answers ends the run on its budget and not in a loop.
/// </para>
/// <para>
/// Anything else is not this step's failure to handle and propagates: the caller's cancellation, a rejected or expired
/// source credential (a typed, resumable fault of its own), the budget's own exhaustion, and defects.
/// </para>
/// </remarks>
public static class ApiTransportFailure
{
    /// <summary>
    /// Gets a value indicating whether an exception raised by a step's API call is a failure of the exchange.
    /// </summary>
    /// <param name="exception">The exception the call raised.</param>
    /// <returns>
    /// <see langword="true"/> for a request that ran past the run's step timeout
    /// (<see cref="ApiTransportTimeoutException"/>), a response larger than the run admits
    /// (<see cref="ApiResponseTooLargeException"/>), a request that could not be sent or a connection that failed
    /// (<see cref="HttpRequestException"/>), a body that stopped arriving (<see cref="IOException"/>), and a body that
    /// is not the JSON its content type claims (<see cref="JsonException"/>).
    /// </returns>
    public static bool IsTransportFailure(Exception exception)
        => exception is ApiTransportTimeoutException
            or ApiResponseTooLargeException
            or HttpRequestException
            or IOException
            or JsonException;
}