// <copyright file="CachedFormatNumberPicture.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata;

/// <summary>
/// A pre-parsed format-number picture, cached in a static field by generated code.
/// Created by <see cref="JsonataCodeGenHelpers.CreateFormatNumberPicture(string)"/>
/// and consumed by <see cref="JsonataCodeGenHelpers.FormatNumberPreParsed"/>.
/// </summary>
public sealed class CachedFormatNumberPicture
{
    internal CachedFormatNumberPicture(FormatNumberPicture picture)
    {
        Picture = picture;
    }

    internal FormatNumberPicture Picture { get; }
}