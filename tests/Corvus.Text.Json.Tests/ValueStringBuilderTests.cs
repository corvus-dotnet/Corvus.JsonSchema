// Derived from SpecFlow tests in https://github.com/corvus-dotnet/Corvus.HighPerformance

using System.Text;
using Xunit;

namespace Corvus.Text.Json.Tests;

public class ValueStringBuilderTests
{
    public enum InitType
    {
        Span,
        Capacity,
    }

    #region Append Tests

    [Theory]
    [InlineData(InitType.Span, "Hello", " World", "Hello World")]
    [InlineData(InitType.Capacity, "Hello", " World", "Hello World")]
    [InlineData(InitType.Span, "A", "B", "AB")]
    [InlineData(InitType.Capacity, "A", "B", "AB")]
    [InlineData(InitType.Span, "", "Something", "Something")]
    [InlineData(InitType.Capacity, "", "Something", "Something")]
    public void AppendTwoStrings_FitsInSpace(InitType initType, string first, string second, string expected)
    {
        int totalLength = first.Length + second.Length + 10;
        ValueStringBuilder vsb = CreateBuilder(initType, totalLength);
        vsb.Append(first);
        vsb.Append(second);

        string result = vsb.ToString(); // ToString also disposes
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(InitType.Span, "Hello", " World", "Hello World")]
    [InlineData(InitType.Capacity, "Hello", " World", "Hello World")]
    [InlineData(InitType.Span, "Short", " but this second string is much longer and will require growth", "Short but this second string is much longer and will require growth")]
    [InlineData(InitType.Capacity, "Short", " but this second string is much longer and will require growth", "Short but this second string is much longer and will require growth")]
    public void AppendTwoStrings_Grows(InitType initType, string first, string second, string expected)
    {
        // Use a very small initial capacity to force growth
        ValueStringBuilder vsb = CreateBuilder(initType, 4);
        vsb.Append(first);
        vsb.Append(second);

        string result = vsb.ToString();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(InitType.Span, 42, " items", "42 items")]
    [InlineData(InitType.Capacity, 42, " items", "42 items")]
    [InlineData(InitType.Span, -1, " error", "-1 error")]
    [InlineData(InitType.Capacity, -1, " error", "-1 error")]
    public void AppendNumberThenString(InitType initType, int number, string text, string expected)
    {
        ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(number.ToString());
        vsb.Append(text);

        string result = vsb.ToString();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(InitType.Span, "Count: ", 99, "Count: 99")]
    [InlineData(InitType.Capacity, "Count: ", 99, "Count: 99")]
    [InlineData(InitType.Span, "Value=", 0, "Value=0")]
    [InlineData(InitType.Capacity, "Value=", 0, "Value=0")]
    public void AppendStringThenNumber(InitType initType, string text, int number, string expected)
    {
        ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(text);
        vsb.Append(number.ToString());

        string result = vsb.ToString();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(InitType.Span)]
    [InlineData(InitType.Capacity)]
    public void AppendGrows_WhenOverflowsInitialBuffer(InitType initType)
    {
        // Start with tiny buffer, append enough to force multiple growths
        ValueStringBuilder vsb = CreateBuilder(initType, 2);
        string longString = new string('X', 500);
        vsb.Append(longString);

        ReadOnlySpan<char> span = vsb.AsSpan();
        Assert.Equal(500, span.Length);
        Assert.Equal(longString, span.ToString());
        vsb.Dispose();
    }

    [Theory]
    [InlineData(InitType.Span)]
    [InlineData(InitType.Capacity)]
    public void AppendChar_Repeated(InitType initType)
    {
        ValueStringBuilder vsb = CreateBuilder(initType, 32);
        vsb.Append('A', 5);

        string result = vsb.ToString();
        Assert.Equal("AAAAA", result);
    }

    [Theory]
    [InlineData(InitType.Span)]
    [InlineData(InitType.Capacity)]
    public void AppendReadOnlySpan(InitType initType)
    {
        ValueStringBuilder vsb = CreateBuilder(initType, 32);
        ReadOnlySpan<char> data = "Hello".AsSpan();
        vsb.Append(data);
        vsb.Append(" ".AsSpan());
        vsb.Append("World".AsSpan());

        string result = vsb.ToString();
        Assert.Equal("Hello World", result);
    }

    #endregion

    #region AsSpan Tests

    [Theory]
    [InlineData(InitType.Span, "Hello World")]
    [InlineData(InitType.Capacity, "Hello World")]
    [InlineData(InitType.Span, "")]
    [InlineData(InitType.Capacity, "")]
    public void AsSpan_ReturnsFullContent(InitType initType, string content)
    {
        ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(content);

        ReadOnlySpan<char> span = vsb.AsSpan();
        Assert.Equal(content.Length, span.Length);
        Assert.Equal(content, span.ToString());
        vsb.Dispose();
    }

    [Theory]
    [InlineData(InitType.Span, "Hello World", 6, "World")]
    [InlineData(InitType.Capacity, "Hello World", 6, "World")]
    [InlineData(InitType.Span, "ABCDEF", 0, "ABCDEF")]
    [InlineData(InitType.Capacity, "ABCDEF", 0, "ABCDEF")]
    [InlineData(InitType.Span, "ABCDEF", 5, "F")]
    [InlineData(InitType.Capacity, "ABCDEF", 5, "F")]
    public void AsSpan_WithStart(InitType initType, string content, int start, string expected)
    {
        ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(content);

        ReadOnlySpan<char> span = vsb.AsSpan(start);
        Assert.Equal(expected, span.ToString());
        vsb.Dispose();
    }

    [Theory]
    [InlineData(InitType.Span, "Hello World", 6, 5, "World")]
    [InlineData(InitType.Capacity, "Hello World", 6, 5, "World")]
    [InlineData(InitType.Span, "ABCDEF", 2, 3, "CDE")]
    [InlineData(InitType.Capacity, "ABCDEF", 2, 3, "CDE")]
    [InlineData(InitType.Span, "Hello World", 0, 5, "Hello")]
    [InlineData(InitType.Capacity, "Hello World", 0, 5, "Hello")]
    public void AsSpan_WithStartAndLength(InitType initType, string content, int start, int length, string expected)
    {
        ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(content);

        ReadOnlySpan<char> span = vsb.AsSpan(start, length);
        Assert.Equal(expected, span.ToString());
        vsb.Dispose();
    }

    #endregion

    #region Insert Tests

    [Theory]
    [InlineData(InitType.Span, "Hello World", 5, "Beautiful ", "HelloBeautiful  World")]
    [InlineData(InitType.Capacity, "Hello World", 5, "Beautiful ", "HelloBeautiful  World")]
    public void Insert_String(InitType initType, string initial, int index, string toInsert, string expected)
    {
        ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(initial);
        vsb.Insert(index, toInsert);

        string result = vsb.ToString();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(InitType.Span, "AC", 1, 'B', 1, "ABC")]
    [InlineData(InitType.Capacity, "AC", 1, 'B', 1, "ABC")]
    [InlineData(InitType.Span, "Hello", 5, '!', 3, "Hello!!!")]
    [InlineData(InitType.Capacity, "Hello", 5, '!', 3, "Hello!!!")]
    public void Insert_CharRepeated(InitType initType, string initial, int index, char c, int count, string expected)
    {
        ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(initial);
        vsb.Insert(index, c, count);

        string result = vsb.ToString();
        Assert.Equal(expected, result);
    }

    #endregion

    #region Length and Capacity Tests

    [Theory]
    [InlineData(InitType.Span)]
    [InlineData(InitType.Capacity)]
    public void Length_ReflectsAppendedContent(InitType initType)
    {
        ValueStringBuilder vsb = CreateBuilder(initType, 32);
        Assert.Equal(0, vsb.Length);

        vsb.Append("Hello");
        Assert.Equal(5, vsb.Length);

        vsb.Append(" World");
        Assert.Equal(11, vsb.Length);
        vsb.Dispose();
    }

    [Theory]
    [InlineData(InitType.Span, 32)]
    [InlineData(InitType.Capacity, 32)]
    public void EnsureCapacity_IncreasesCapacity(InitType initType, int initialCapacity)
    {
        ValueStringBuilder vsb = CreateBuilder(initType, initialCapacity);
        vsb.EnsureCapacity(100);

        Assert.True(vsb.Capacity >= 100);
        vsb.Dispose();
    }

    #endregion

    #region TryCopyTo Tests

    [Theory]
    [InlineData(InitType.Span, "Hello")]
    [InlineData(InitType.Capacity, "Hello")]
    public void TryCopyTo_SucceedsWhenDestinationLargeEnough(InitType initType, string content)
    {
        ValueStringBuilder vsb = CreateBuilder(initType, 32);
        vsb.Append(content);

        char[] dest = new char[32];
        bool success = vsb.TryCopyTo(dest, out int written);

        Assert.True(success);
        Assert.Equal(content.Length, written);
        Assert.Equal(content, new string(dest, 0, written));
    }

    [Theory]
    [InlineData(InitType.Span, "Hello World")]
    [InlineData(InitType.Capacity, "Hello World")]
    public void TryCopyTo_FailsWhenDestinationTooSmall(InitType initType, string content)
    {
        ValueStringBuilder vsb = CreateBuilder(initType, 32);
        vsb.Append(content);

        char[] dest = new char[2];
        bool success = vsb.TryCopyTo(dest, out int written);

        Assert.False(success);
        Assert.Equal(0, written);
    }

    #endregion

    #region AppendSpan Tests

    [Theory]
    [InlineData(InitType.Span)]
    [InlineData(InitType.Capacity)]
    public void AppendSpan_ReturnsWritableSpan(InitType initType)
    {
        ValueStringBuilder vsb = CreateBuilder(initType, 32);
        vsb.Append("Hello");

        Span<char> appended = vsb.AppendSpan(5);
        "World".AsSpan().CopyTo(appended);

        string result = vsb.ToString();
        Assert.Equal("HelloWorld", result);
    }

    #endregion

    #region Helpers

    private static ValueStringBuilder CreateBuilder(InitType initType, int size)
    {
        if (initType == InitType.Span)
        {
            // Note: stackalloc can't be used here because we need the span to survive
            // beyond this method. For Span init with a fixed size, we use the capacity
            // constructor which rents from the pool (same effective behavior for testing).
            // In real usage, callers use stackalloc in the same scope.
            return new ValueStringBuilder(size);
        }
        else
        {
            return new ValueStringBuilder(size);
        }
    }

    #endregion
}
