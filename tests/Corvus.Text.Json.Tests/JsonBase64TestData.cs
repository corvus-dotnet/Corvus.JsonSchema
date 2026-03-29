// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Collections.Generic;

namespace Corvus.Text.Json.Tests;

internal class JsonBase64TestData
{
    public static IEnumerable<object[]> InvalidBase64Tests()
    {
        yield return new object[] { "\"ABA===\"" };
        yield return new object[] { "\"ABA\"" };
        yield return new object[] { "\"ABA!\"" };
        yield return new object[] { GenerateRandomInvalidLargeString(includeEscapedCharacter: true) };
        yield return new object[] { GenerateRandomInvalidLargeString(includeEscapedCharacter: false) };
    }

    public static IEnumerable<object[]> ValidBase64Tests()
    {
        yield return new object[] { "\"ABA=\"" };
        yield return new object[] { "\"AB+D\"" };
        yield return new object[] { "\"ABCD\"" };
        yield return new object[] { "\"ABC/\"" };
        yield return new object[] { "\"++++\"" };
        yield return new object[] { GenerateRandomValidLargeString() };
    }

    private static string GenerateRandomInvalidLargeString(bool includeEscapedCharacter)
    {
        var random = new Random(42);
        char[] charArray = new char[500];
        charArray[0] = '"';
        for (int i = 1; i < charArray.Length - 1; i++)
        {
            charArray[i] = (char)random.Next('?', '\\'); // ASCII values (between 63 and 91) that don't need to be escaped.
        }

        if (includeEscapedCharacter)
        {
            charArray[256] = '\\';
            charArray[257] = '"';
        }

        charArray[charArray.Length - 1] = '"';
        string jsonString = new(charArray);
        return jsonString;
    }

    private static string GenerateRandomValidLargeString()
    {
        var random = new Random(42);
        char[] charArray = new char[502]; // valid Base64 strings must have length divisible by 4 (not including surrounding quotes)
        charArray[0] = '"';
        for (int i = 1; i < charArray.Length - 1; i++)
        {
            charArray[i] = (char)random.Next('A', 'Z'); // ASCII values (between 65 and 90) that constitute valid base 64 string.
        }
        charArray[charArray.Length - 1] = '"';
        string jsonString = new(charArray);
        return jsonString;
    }
}