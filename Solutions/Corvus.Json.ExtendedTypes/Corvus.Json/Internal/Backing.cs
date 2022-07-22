// <copyright file="Backing.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.Internal;

/// <summary>
/// Backing field type discriminator.
/// </summary>
[Flags]
public enum Backing : byte
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1602 // Enumeration items should be documented
    Undefined = 0b0,
    JsonElement = 0b1,
    String = 0b10,
    Bool = 0b1_00,
    Number = 0b10_00,
    Array = 0b1_00_00,
    Object = 0b10_00_00,
    Null = 0b1_00_00_00,
    Dotnet = Array | Bool | Number | Null | Object | String,
#pragma warning restore SA1602 // Enumeration items should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}