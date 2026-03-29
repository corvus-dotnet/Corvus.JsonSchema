// <copyright file="ArrayReverseEnumerator.cs" company="Endjin Limited">
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
/// Provides an enumerator and enumerable for iterating over the elements of a JSON array in a document.
/// </summary>
[DebuggerDisplay("{CurrentIndex,nq}")]
[CLSCompliant(false)]
public struct ArrayReverseEnumerator
{
    private readonly int _endIdx;

    private readonly int _arrayDocumentIndex;

    private readonly IJsonDocument _targetDocument;

    private int _curIdx;

    private int _curEndIdx;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArrayReverseEnumerator"/> struct.
    /// </summary>
    /// <param name="targetDocument">The document containing the array to enumerate.</param>
    /// <param name="arrayDocumentIndex">The initial index of the array element in the document.</param>
    public ArrayReverseEnumerator(IJsonDocument targetDocument, int arrayDocumentIndex)
    {
        _targetDocument = targetDocument;
        _arrayDocumentIndex = arrayDocumentIndex;
        _curIdx = -1;
        _curEndIdx = -1;
        _endIdx = _arrayDocumentIndex + _targetDocument.GetDbSize(_arrayDocumentIndex, includeEndElement: false);
    }

    /// <summary>
    /// Gets the current index within the JSON array.
    /// </summary>
    public readonly int CurrentIndex => _curIdx;

    /// <summary>
    /// Gets the current end index of the item within the JSON array.
    /// </summary>
    public readonly int CurrentEndIndex => _curEndIdx;

    /// <summary>
    /// Releases resources used by the enumerator.
    /// </summary>
    public void Dispose()
    {
        _curIdx = _endIdx;
    }

    /// <summary>
    /// Advances the enumerator to the next element of the collection.
    /// </summary>
    /// <returns><see langword="true"/> if the enumerator was successfully advanced to the next element; <see langword="false"/> if the enumerator has passed the end of the collection.</returns>
    public bool MoveNext()
    {
        if (_curEndIdx < 0)
        {
            _curEndIdx = _endIdx - DbRow.Size;

            // No return from here so we pick up the next check
            // which confirms we don't have an empty array.
        }
        else
        {
            // Move back to the previous row
            _curEndIdx = _curIdx - DbRow.Size;
        }

        if (_curEndIdx <= _arrayDocumentIndex)
        {
            return false;
        }

        // Move back to the start index of the current element
        _curIdx = _targetDocument.GetStartIndex(_curEndIdx);

        return true;
    }

    /// <summary>
    /// Sets the enumerator to its initial position, which is before the first element in the collection.
    /// </summary>
    public void Reset()
    {
        _curIdx = -1;
    }
}