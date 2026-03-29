// <copyright file="ObjectEnumerator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// An enumerable and enumerator for the properties of a JSON object.
/// </summary>
[DebuggerDisplay("{Current,nq}")]
[CLSCompliant(false)]
public struct ObjectEnumerator
{
    private readonly int _endIdxOrVersion;

    private readonly int _initialIndex;

    private readonly IJsonDocument _targetDocument;

    private int _curIdx;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObjectEnumerator"/> struct.
    /// </summary>
    /// <param name="targetDocument">The target document containing the object to enumerate.</param>
    /// <param name="initialIndex">The initial index of the object in the document.</param>
    public ObjectEnumerator(IJsonDocument targetDocument, int initialIndex)
    {
        _targetDocument = targetDocument;
        _initialIndex = initialIndex;
        _curIdx = -1;

        _endIdxOrVersion = _initialIndex + _targetDocument.GetDbSize(_initialIndex, includeEndElement: false);
    }

    /// <summary>
    /// Gets the current index in the document.
    /// </summary>
    public int CurrentIndex
    {
        get
        {
            return _curIdx;
        }
    }

    /// <summary>
    /// Releases resources used by the enumerator.
    /// </summary>
    public void Dispose()
    {
        _curIdx = _endIdxOrVersion;
    }

    /// <summary>
    /// Advances the enumerator to the next element of the collection.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the enumerator was successfully advanced to the next element;
    /// <see langword="false"/> if the enumerator has passed the end of the collection.
    /// </returns>
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

    /// <summary>
    /// Sets the enumerator to its initial position, which is before the first element.
    /// </summary>
    public void Reset()
    {
        _curIdx = -1;
    }
}