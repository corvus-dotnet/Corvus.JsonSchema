// <copyright file="ThrowHelper.Rune.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics.CodeAnalysis;

namespace System;

internal enum ExceptionArgument
{
    ch,
    culture,
    index,
    input,
    value,
}

internal static class ThrowHelper
{
    [DoesNotReturn]
    internal static void ThrowArgumentException_CannotExtractScalar(ExceptionArgument argument)
    {
        throw new ArgumentException("Cannot extract a Unicode scalar value from the specified index in the input.", GetArgumentName(argument));
    }

    [DoesNotReturn]
    internal static void ThrowArgumentException_DestinationTooShort()
    {
        throw new ArgumentException("Destination is too short.", "destination");
    }

    [DoesNotReturn]
    internal static void ThrowArgumentNullException(ExceptionArgument argument)
    {
        throw new ArgumentNullException(GetArgumentName(argument));
    }

    [DoesNotReturn]
    internal static void ThrowArgumentOutOfRange_IndexMustBeLessException()
    {
        throw new ArgumentOutOfRangeException("index", "Index was out of range. Must be non-negative and less than the size of the collection.");
    }

    [DoesNotReturn]
    internal static void ThrowArgumentOutOfRangeException(ExceptionArgument argument)
    {
        throw new ArgumentOutOfRangeException(GetArgumentName(argument));
    }

    private static string GetArgumentName(ExceptionArgument argument) => argument switch
    {
        ExceptionArgument.ch => nameof(ExceptionArgument.ch),
        ExceptionArgument.culture => nameof(ExceptionArgument.culture),
        ExceptionArgument.index => nameof(ExceptionArgument.index),
        ExceptionArgument.input => nameof(ExceptionArgument.input),
        ExceptionArgument.value => nameof(ExceptionArgument.value),
        _ => string.Empty,
    };
}