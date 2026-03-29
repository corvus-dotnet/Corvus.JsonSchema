// <copyright file="ObjectEnumerator{T}.cs" company="Endjin Limited">
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
/// An enumerable and enumerator for the properties of a JSON object.
/// </summary>
[DebuggerDisplay("{Current,nq}")]
[CLSCompliant(false)]
public struct ObjectEnumerator<TValue> : IEnumerable<JsonProperty<TValue>>, IEnumerator<JsonProperty<TValue>>
    where TValue : struct, IJsonElement<TValue>
{
    private readonly IJsonDocument _targetDocument;

    private readonly int _initialIndex;

    private int _curIdx;

    private readonly int _endIdxOrVersion;

    internal ObjectEnumerator(IJsonDocument targetDocument, int initialIndex)
    {
        _targetDocument = targetDocument;
        _initialIndex = initialIndex;
        _curIdx = -1;

        _endIdxOrVersion = _initialIndex + _targetDocument.GetDbSize(_initialIndex, includeEndElement: false);
    }

    /// <summary>
    /// Gets the current element in the enumeration.
    /// </summary>
    public JsonProperty<TValue> Current
    {
        get
        {
            if (_curIdx < 0)
            {
                return default;
            }

#if NET
            return new JsonProperty<TValue>(TValue.CreateInstance(_targetDocument, _curIdx));
#else
            return new JsonProperty<TValue>(JsonElementHelpers.CreateInstance<TValue>(_targetDocument, _curIdx));
#endif
        }
    }

    /// <summary>
    /// Returns an enumerator that iterates the properties of an object.
    /// </summary>
    /// <returns>
    /// An <see cref="ObjectEnumerator"/> value that can be used to iterate
    /// through the object.
    /// </returns>
    /// <remarks>
    /// The enumerator will enumerate the properties in the order they are
    /// declared, and when an object has multiple definitions of a single
    /// property they will all individually be returned (each in the order
    /// they appear in the content).
    /// </remarks>
    public ObjectEnumerator<TValue> GetEnumerator()
    {
        ObjectEnumerator<TValue> ator = this;
        ator._curIdx = -1;
        return ator;
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    IEnumerator<JsonProperty<TValue>> IEnumerable<JsonProperty<TValue>>.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public void Dispose()
    {
        _curIdx = _endIdxOrVersion;
    }

    /// <inheritdoc />
    public void Reset()
    {
        _curIdx = -1;
    }

    /// <inheritdoc />
    object IEnumerator.Current => Current;

    /// <inheritdoc />
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

        // _curIdx is now pointing at a property name, move one more to get the value
        _curIdx += DbRow.Size;

        return _curIdx < _endIdxOrVersion;
    }
}