// <copyright file="BaseUriResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.DocumentResolvers;

/// <summary>
/// A callback that can resolve a canonical base URI to a JSON Document.
/// </summary>
/// <param name="baseUri">The canonical base URI for the document.</param>
/// <returns>The <see cref="JsonDocument"/>, or <see langword="null"/> if the document could not be resolved.</returns>
/// <remarks>The callback transfers ownership of the <see cref="JsonDocument"/> to the caller.</remarks>
public delegate JsonDocument? BaseUriResolver(string baseUri);