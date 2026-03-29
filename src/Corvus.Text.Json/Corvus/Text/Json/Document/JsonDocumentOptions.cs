// <copyright file="JsonDocumentOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics;

namespace Corvus.Text.Json;

/// <summary>
/// Provides the ability for the user to define custom behavior when parsing JSON to create a <see cref="JsonDocument"/>.
/// </summary>
public struct JsonDocumentOptions
{
    internal const int DefaultMaxDepth = 64;

    private JsonCommentHandling _commentHandling;

    private int _maxDepth;

    /// <summary>
    /// Defines whether an extra comma at the end of a list of JSON values in an object or array
    /// is allowed (and ignored) within the JSON payload being read.
    /// </summary>
    /// <remarks>
    /// By default, it's set to false, and a <see cref="JsonException"/> is thrown if a trailing comma is encountered.
    /// </remarks>
    public bool AllowTrailingCommas { get; set; }

    /// <summary>
    /// Defines how the <see cref="Utf8JsonReader"/> should handle comments when reading through the JSON.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the comment handling enum is set to a value that is not supported (or not within the <see cref="JsonCommentHandling"/> enum range).
    /// </exception>
    /// <remarks>
    /// By default a <see cref="JsonException"/> is thrown if a comment is encountered.
    /// </remarks>
    public JsonCommentHandling CommentHandling
    {
        readonly get => _commentHandling;
        set
        {
            Debug.Assert(value >= 0);
            if (value > JsonCommentHandling.Skip)
                throw new ArgumentOutOfRangeException(nameof(value), SR.JsonDocumentDoesNotSupportComments);

            _commentHandling = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum depth allowed when reading JSON, with the default (i.e. 0) indicating a max depth of 64.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the max depth is set to a negative value.
    /// </exception>
    /// <remarks>
    /// Reading past this depth will throw a <see cref="JsonException"/>.
    /// </remarks>
    public int MaxDepth
    {
        readonly get => _maxDepth;
        set
        {
            if (value < 0)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_MaxDepthMustBePositive(nameof(value));
            }

            _maxDepth = value;
        }
    }

    internal JsonReaderOptions GetReaderOptions()
    {
        return new JsonReaderOptions
        {
            AllowTrailingCommas = AllowTrailingCommas,
            CommentHandling = CommentHandling,
            MaxDepth = MaxDepth
        };
    }
}