// <copyright file="Utf8JsonPointerTools.Decode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
namespace Corvus.Text.Json.Internal;

/// <content>
/// Decoding JSON Pointer reference tokens.
/// </content>
internal static partial class Utf8JsonPointerTools
{
    /// <summary>
    /// Decodes the ~ encoding in a reference.
    /// </summary>
    /// <param name="encodedFragment">The encoded reference.</param>
    /// <param name="fragment">The span into which to write the result.</param>
    /// <returns>The length of the decoded reference.</returns>
    public static int DecodePointer(ReadOnlySpan<byte> encodedFragment, Span<byte> fragment)
    {
        int readIndex = 0;
        int writeIndex = 0;

        while (readIndex < encodedFragment.Length)
        {
            if (encodedFragment[readIndex] != '~')
            {
                fragment[writeIndex] = encodedFragment[readIndex];
                readIndex++;
                writeIndex++;
            }
            else
            {
                if (readIndex >= encodedFragment.Length - 1)
                {
                    ThrowHelper.ThrowInvalidOperation_JsonPointer_Expected_Digit_After_Escape_Character_Found_End();
                }

                if (encodedFragment[readIndex + 1] == '0')
                {
                    fragment[writeIndex] = (byte)'~';
                }
                else if (encodedFragment[readIndex + 1] == '1')
                {
                    fragment[writeIndex] = (byte)'/';
                }
                else
                {
                    ThrowHelper.ThrowInvalidOperation_JsonPointer_Expected_Digit_After_Escape_Character();
                }

                readIndex += 2;
                writeIndex++;
            }
        }

        return writeIndex;
    }
}