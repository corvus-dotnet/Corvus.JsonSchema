// <copyright file="IMutableJsonElement{T}.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
namespace Corvus.Text.Json.Internal;

/// <summary>
/// Represents a mutable JSON element of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type implementing the interface.</typeparam>
/// <remarks>
/// Note that mutable elements are ephemeral. If their underlying document is modified, they may no longer be valid, and their behaviour is undefined.
/// </remarks>
[CLSCompliant(false)]
public interface IMutableJsonElement<T> : IJsonElement<T>
    where T : struct, IJsonElement<T>;