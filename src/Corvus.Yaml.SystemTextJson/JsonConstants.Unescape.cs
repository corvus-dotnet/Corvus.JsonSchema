// <copyright file="JsonConstants.Unescape.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

namespace Corvus.Yaml.Internal;

/// <summary>
/// JSON byte constants and surrogate pair values required by the unescape implementation.
/// </summary>
internal static partial class JsonConstants
{
    public const byte Quote = (byte)'"';
    public const byte BackSlash = (byte)'\\';
    public const byte Slash = (byte)'/';
    public const byte BackSpace = (byte)'\b';
    public const byte FormFeed = (byte)'\f';
    public const byte LineFeed = (byte)'\n';
    public const byte CarriageReturn = (byte)'\r';
    public const byte Tab = (byte)'\t';

    public const int HighSurrogateStartValue = 0xD800;
    public const int HighSurrogateEndValue = 0xDBFF;
    public const int LowSurrogateStartValue = 0xDC00;
    public const int LowSurrogateEndValue = 0xDFFF;
    public const int UnicodePlane01StartValue = 0x10000;
    public const int BitShiftBy10 = 0x400;
}