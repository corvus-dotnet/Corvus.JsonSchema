// <copyright file="StringBuilderPool.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Microsoft.Extensions.ObjectPool;

namespace Corvus.UriTemplates.Internal;

/// <summary>
/// A <see cref="StringBuilder"/> provider.
/// </summary>
internal static class StringBuilderPool
{
    /// <summary>
    /// Gets a shared <see cref="StringBuilder"/> pool.
    /// </summary>
    public static readonly DefaultObjectPool<StringBuilder> Shared = new(new StringBuilderPooledObjectPolicy());
}