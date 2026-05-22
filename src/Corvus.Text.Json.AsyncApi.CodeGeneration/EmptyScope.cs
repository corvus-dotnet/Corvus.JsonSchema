// <copyright file="EmptyScope.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi.CodeGeneration;

/// <summary>
/// A no-op <see cref="IDisposable"/> returned when no base URI change is needed.
/// </summary>
internal sealed class EmptyScope : IDisposable
{
    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static readonly EmptyScope Instance = new();

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}