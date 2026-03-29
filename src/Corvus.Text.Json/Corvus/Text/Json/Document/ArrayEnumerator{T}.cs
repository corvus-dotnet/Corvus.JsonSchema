// <copyright file="ArrayEnumerator{T}.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json;

/// <summary>
/// Provides an enumerator and enumerable for iterating over the elements of a JSON array in a document.
/// </summary>
/// <typeparam name="TItem">The type of the JSON element to enumerate, which must implement <see cref="IJsonElement{TItem}"/>.</typeparam>
[DebuggerDisplay("{Current,nq}")]
[CLSCompliant(false)]
public struct ArrayEnumerator<TItem> : IEnumerable<TItem>, IEnumerator<TItem>
    where TItem : struct, IJsonElement<TItem>
{
    private readonly IJsonDocument _targetDocument;

    private readonly int _initialIndex;

    private int _curIdx;

    private readonly int _endIdxOrVersion;

    internal ArrayEnumerator(IJsonDocument targetDocument, int initialIndex)
    {
        _targetDocument = targetDocument;
        _initialIndex = initialIndex;
        _curIdx = -1;

        _endIdxOrVersion = _initialIndex + _targetDocument.GetDbSize(_initialIndex, includeEndElement: false);
    }

    /// <summary>
    /// Gets the current element in the collection.
    /// </summary>
    public TItem Current
    {
        get
        {
            if (_curIdx < 0)
            {
                return default;
            }

#if NET
            return TItem.CreateInstance(_targetDocument, _curIdx);
#else
            return JsonElementHelpers.CreateInstance<TItem>(_targetDocument, _curIdx);
#endif
        }
    }

    /// <summary>
    /// Returns an enumerator that iterates through the JSON array.
    /// </summary>
    /// <returns>An <see cref="ArrayEnumerator{TItem}"/> value that can be used to iterate through the array.</returns>
    public ArrayEnumerator<TItem> GetEnumerator()
    {
        ArrayEnumerator<TItem> ator = this;
        ator._curIdx = -1;
        return ator;
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    IEnumerator<TItem> IEnumerable<TItem>.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Releases resources used by the enumerator.
    /// </summary>
    public void Dispose()
    {
        _curIdx = _endIdxOrVersion;
    }

    /// <summary>
    /// Sets the enumerator to its initial position, which is before the first element in the collection.
    /// </summary>
    public void Reset()
    {
        _curIdx = -1;
    }

    /// <inheritdoc />
    object IEnumerator.Current => Current;

    /// <summary>
    /// Advances the enumerator to the next element of the collection.
    /// </summary>
    /// <returns><see langword="true"/> if the enumerator was successfully advanced to the next element; <see langword="false"/> if the enumerator has passed the end of the collection.</returns>
    public bool MoveNext()
    {
        if (_curIdx >= _endIdxOrVersion)
        {
            return false;
        }

        if (_curIdx < 0)
        {
            _curIdx = _initialIndex + DbRow.Size;
        }
        else
        {
            _curIdx += _targetDocument.GetDbSize(_curIdx, includeEndElement: true);
        }

        return _curIdx < _endIdxOrVersion;
    }
}