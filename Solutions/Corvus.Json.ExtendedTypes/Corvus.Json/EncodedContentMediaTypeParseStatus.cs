// <copyright file="EncodedContentMediaTypeParseStatus.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json;

/// <summary>
/// Represents the result of parsing encoded content with particular media type.
/// </summary>
public enum EncodedContentMediaTypeParseStatus
{
    /// <summary>
    /// The content was successfully decoded.
    /// </summary>
    Success,

    /// <summary>
    /// Unable to decode the content using the specified encoding.
    /// </summary>
    UnableToDecode,

    /// <summary>
    /// Unable to parse the decoded content as the given media type.
    /// </summary>
    UnableToParseToMediaType,
}