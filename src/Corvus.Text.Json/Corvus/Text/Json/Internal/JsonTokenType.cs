// <copyright file="JsonTokenType.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
namespace Corvus.Text.Json.Internal;

/// <summary>
/// This enum defines the various JSON tokens that make up a JSON text and is used by
/// the <see cref="Utf8JsonReader"/> when moving from one token to the next.
/// The <see cref="Utf8JsonReader"/> starts at 'None' by default. The 'Comment' enum value
/// is only ever reached in a specific <see cref="Utf8JsonReader"/> mode and is not
/// reachable by default.
/// </summary>
public enum JsonTokenType : byte
{
    // Do not re-number.
    // We rely on the underlying values to quickly check things like JsonReaderHelper.IsTokenTypePrimitive and Utf8JsonWriter.CanWriteValue

    /// <summary>
    /// Indicates that there is no value (as distinct from <see cref="Null"/>).
    /// </summary>
    /// <remarks>
    /// This is the default token type if no data has been read by the <see cref="Utf8JsonReader"/>.
    /// </remarks>
    None = 0,

    /// <summary>
    /// Indicates that the token type is the start of a JSON object.
    /// </summary>
    StartObject = 1,

    /// <summary>
    /// Indicates that the token type is the end of a JSON object.
    /// </summary>
    EndObject = 2,

    /// <summary>
    /// Indicates that the token type is the start of a JSON array.
    /// </summary>
    StartArray = 3,

    /// <summary>
    /// Indicates that the token type is the end of a JSON array.
    /// </summary>
    EndArray = 4,

    /// <summary>
    /// Indicates that the token type is a JSON property name.
    /// </summary>
    PropertyName = 5,

    /// <summary>
    /// Indicates that the token type is the comment string.
    /// </summary>
    Comment = 6,

    /// <summary>
    /// Indicates that the token type is a JSON string.
    /// </summary>
    String = 7,

    /// <summary>
    /// Indicates that the token type is a JSON number.
    /// </summary>
    Number = 8,

    /// <summary>
    /// Indicates that the token type is the JSON literal <c>true</c>.
    /// </summary>
    True = 9,

    /// <summary>
    /// Indicates that the token type is the JSON literal <c>false</c>.
    /// </summary>
    False = 10,

    /// <summary>
    /// Indicates that the token type is the JSON literal <c>null</c>.
    /// </summary>
    Null = 11,
}