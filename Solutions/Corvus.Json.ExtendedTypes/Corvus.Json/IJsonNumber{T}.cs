// <copyright file="IJsonNumber{T}.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json;

/// <summary>
/// A JSON number.
/// </summary>
/// <typeparam name="T">The type implementin the interface.</typeparam>
public interface IJsonNumber<T> : IJsonValue<T>
    where T : struct, IJsonNumber<T>
{
    /// <summary>
    /// Gets the Json Number as a binary number.
    /// </summary>
    BinaryJsonNumber AsBinaryJsonNumber { get; }
}