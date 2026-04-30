// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
using System.Diagnostics.CodeAnalysis;

namespace Corvus.Text.Json;

internal static partial class ThrowHelper
{
    [DoesNotReturn]
    public static void ThrowArgumentNullException(string parameterName)
    {
        throw new ArgumentNullException(parameterName);
    }
}