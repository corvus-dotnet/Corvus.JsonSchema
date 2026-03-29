// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
namespace Corvus.Text.Json;

internal static partial class JsonConstants
{
    // Standard format for double and single on non-inbox frameworks.
    public const string DoubleFormatString = "G17";

    public const string SingleFormatString = "G9";

    public const int StackallocByteThreshold = 256;

    public const int StackallocCharThreshold = StackallocByteThreshold / 2;

    public const int StackallocNonRecursiveByteThreshold = 4096;

    public const int StackallocNonRecursiveCharThreshold = StackallocNonRecursiveByteThreshold / 2;
}