// <copyright file="ParsedJsonDocument.TryGetProperty.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics.CodeAnalysis;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json;

public sealed partial class ParsedJsonDocument<T>
{
    /// <inheritdoc />
    bool IJsonDocument.TryGetNamedPropertyValue(int index, ReadOnlySpan<char> propertyName, out JsonElement value)
    {
        CheckNotDisposed();

        if (TryGetNamedPropertyValueIndexUnsafe(
            index,
            propertyName,
            out int valueIndex))
        {
            value = new JsonElement(this, valueIndex);
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetNamedPropertyValue(int index, ReadOnlySpan<byte> propertyName, out JsonElement value)
    {
        CheckNotDisposed();

        if (TryGetNamedPropertyValueIndexUnsafe(
            index,
            propertyName,
            out int valueIndex))
        {
            value = new JsonElement(this, valueIndex);
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetNamedPropertyValue<TElement>(int index, ReadOnlySpan<byte> propertyName, out TElement value)
    {
        CheckNotDisposed();

        if (TryGetNamedPropertyValueIndexUnsafe(
            index,
            propertyName,
            out int valueIndex))
        {
#if NET
            value = TElement.CreateInstance(this, valueIndex);
#else
            value = JsonElementHelpers.CreateInstance<TElement>(this, valueIndex);
#endif
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetNamedPropertyValue<TElement>(int index, ReadOnlySpan<char> propertyName, out TElement value)
    {
        CheckNotDisposed();

        if (TryGetNamedPropertyValueIndexUnsafe(
            index,
            propertyName,
            out int valueIndex))
        {
#if NET
            value = TElement.CreateInstance(this, valueIndex);
#else
            value = JsonElementHelpers.CreateInstance<TElement>(this, valueIndex);
#endif
            return true;
        }

        value = default;
        return false;
    }

    bool IJsonDocument.TryGetNamedPropertyValue(int index, ReadOnlySpan<char> propertyName, [NotNullWhen(true)] out IJsonDocument? elementParent, out int elementIdx)
    {
        CheckNotDisposed();

        if (TryGetNamedPropertyValueIndexUnsafe(
            index,
            propertyName,
            out int valueIndex))
        {
            elementParent = this;
            elementIdx = valueIndex;
            return true;
        }

        elementIdx = -1;
        elementParent = null;
        return false;
    }

    bool IJsonDocument.TryGetNamedPropertyValue(int index, ReadOnlySpan<byte> propertyName, [NotNullWhen(true)] out IJsonDocument? elementParent, out int elementIdx)
    {
        CheckNotDisposed();

        if (TryGetNamedPropertyValueIndexUnsafe(
            index,
            propertyName,
            out int valueIndex))
        {
            elementParent = this;
            elementIdx = valueIndex;
            return true;
        }

        elementIdx = -1;
        elementParent = null;
        return false;
    }
}