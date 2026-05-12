// <copyright file="DynamicJsonElement.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Validator;

/// <summary>
/// A dynamically-compiled JSON element backed by an <see cref="IJsonElement"/>.
/// </summary>
public readonly struct DynamicJsonElement : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicJsonElement"/> struct.
    /// </summary>
    /// <param name="type">The generated .NET type of the element.</param>
    /// <param name="element">The underlying JSON element.</param>
    public DynamicJsonElement(Type type, IJsonElement element)
    {
        this.Type = type;
        this.Element = element;
    }

    /// <summary>
    /// Gets the generated .NET type of the element.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Gets the underlying JSON element.
    /// </summary>
    public IJsonElement Element { get; }

    /// <summary>
    /// Evaluates the schema for this element.
    /// </summary>
    /// <param name="resultsCollector">An optional results collector.</param>
    /// <returns><see langword="true"/> if the element is valid; otherwise <see langword="false"/>.</returns>
    public bool EvaluateSchema(IJsonSchemaResultsCollector? resultsCollector = null)
    {
        return this.Element.EvaluateSchema(resultsCollector);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Element.ParentDocument.Dispose();
    }
}