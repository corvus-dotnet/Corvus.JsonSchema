// <copyright file="JsonValueHelpers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.Internal;

/// <summary>
/// Methods that help you to implement <see cref="IJsonValue{T}"/>.
/// </summary>
public static partial class JsonValueHelpers
{
    /// <summary>
    /// Gets the maximum size to allocate on the stack.
    /// </summary>
    public const int MaxStackAlloc = JsonConstants.StackallocThreshold;
}