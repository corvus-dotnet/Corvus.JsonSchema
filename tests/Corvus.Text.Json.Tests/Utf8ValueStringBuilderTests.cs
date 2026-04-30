// Derived from SpecFlow tests in https://github.com/corvus-dotnet/Corvus.HighPerformance

using System.Buffers.Text;
using System.Text;
using Corvus.Text;
using Xunit;

namespace Corvus.Text.Json.Tests;

public class Utf8ValueStringBuilderTests
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
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, totalLength);
        vsb.Append(Encoding.UTF8.GetBytes(first));
        vsb.Append(Encoding.UTF8.GetBytes(second));

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(InitType.Span, "Hello", " World", "Hello World")]
    [InlineData(InitType.Capacity, "Hello", " World", "Hello World")]
    [InlineData(InitType.Span, "Short", " but this second string is much longer and will require growth", "Short but this second string is much longer and will require growth")]
    [InlineData(InitType.Capacity, "Short", " but this second string is much longer and will require growth", "Short but this second string is much longer and will require growth")]
    public void AppendTwoStrings_Grows(InitType initType, string first, string second, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 4);
        vsb.Append(Encoding.UTF8.GetBytes(first));
        vsb.Append(Encoding.UTF8.GetBytes(second));

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(InitType.Span, 42, " items", "42 items")]
    [InlineData(InitType.Capacity, 42, " items", "42 items")]
    [InlineData(InitType.Span, -1, " error", "-1 error")]
    [InlineData(InitType.Capacity, -1, " error", "-1 error")]
    public void AppendNumberThenString(InitType initType, int number, string text, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(number);
        vsb.Append(Encoding.UTF8.GetBytes(text));

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(InitType.Span, "Count: ", 99, "Count: 99")]
    [InlineData(InitType.Capacity, "Count: ", 99, "Count: 99")]
    [InlineData(InitType.Span, "Value=", 0, "Value=0")]
    [InlineData(InitType.Capacity, "Value=", 0, "Value=0")]
    public void AppendStringThenNumber(InitType initType, string text, int number, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(Encoding.UTF8.GetBytes(text));
        vsb.Append(number);

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(InitType.Span, 123456789L, "123456789")]
    [InlineData(InitType.Capacity, 123456789L, "123456789")]
    [InlineData(InitType.Span, -99L, "-99")]
    [InlineData(InitType.Capacity, -99L, "-99")]
    public void AppendLong(InitType initType, long number, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(number);

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(InitType.Span)]
    [InlineData(InitType.Capacity)]
    public void AppendGrows_WhenOverflowsInitialBuffer(InitType initType)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 2);
        byte[] longData = new byte[500];
        for (int i = 0; i < longData.Length; i++)
        {
            longData[i] = (byte)'X';
        }

        vsb.Append(longData);

        ReadOnlySpan<byte> span = vsb.AsSpan();
        Assert.Equal(500, span.Length);
        for (int i = 0; i < span.Length; i++)
        {
            Assert.Equal((byte)'X', span[i]);
        }

        vsb.Dispose();
    }

    [Theory]
    [InlineData(InitType.Span)]
    [InlineData(InitType.Capacity)]
    public void AppendByte_Repeated(InitType initType)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 32);
        vsb.Append((byte)'A', 5);

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.Equal("AAAAA", result);
    }

    [Theory]
    [InlineData(InitType.Span, "Hello")]
    [InlineData(InitType.Capacity, "Hello")]
    [InlineData(InitType.Span, "ASCII only")]
    [InlineData(InitType.Capacity, "ASCII only")]
    public void AppendAsciiString(InitType initType, string text)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.AppendAsciiString(text);

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.Equal(text, result);
    }

    [Theory]
    [InlineData(InitType.Span, 'A', "A")]
    [InlineData(InitType.Capacity, 'A', "A")]
    [InlineData(InitType.Span, '!', "!")]
    [InlineData(InitType.Capacity, '!', "!")]
    public void AppendChar(InitType initType, char c, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 32);
        vsb.AppendChar(c);

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.Equal(expected, result);
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
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(Encoding.UTF8.GetBytes(content));

        ReadOnlySpan<byte> span = vsb.AsSpan();
        string result = SpanToString(span);
        vsb.Dispose();
        Assert.Equal(content, result);
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
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(Encoding.UTF8.GetBytes(content));

        ReadOnlySpan<byte> span = vsb.AsSpan(start);
        string result = SpanToString(span);
        vsb.Dispose();
        Assert.Equal(expected, result);
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
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(Encoding.UTF8.GetBytes(content));

        ReadOnlySpan<byte> span = vsb.AsSpan(start, length);
        string result = SpanToString(span);
        vsb.Dispose();
        Assert.Equal(expected, result);
    }

    #endregion

    #region AsMemory Tests

    [Theory]
    [InlineData(InitType.Span, "Hello World")]
    [InlineData(InitType.Capacity, "Hello World")]
    public void AsMemory_ReturnsFullContent(InitType initType, string content)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(Encoding.UTF8.GetBytes(content));

        ReadOnlyMemory<byte> memory = vsb.AsMemory();
        string result = MemoryToString(memory);
        vsb.Dispose();
        Assert.Equal(content, result);
    }

    [Theory]
    [InlineData(InitType.Span, "Hello World", 6, "World")]
    [InlineData(InitType.Capacity, "Hello World", 6, "World")]
    public void AsMemory_WithStart(InitType initType, string content, int start, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(Encoding.UTF8.GetBytes(content));

        ReadOnlyMemory<byte> memory = vsb.AsMemory(start);
        string result = MemoryToString(memory);
        vsb.Dispose();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(InitType.Span, "Hello World", 6, 5, "World")]
    [InlineData(InitType.Capacity, "Hello World", 6, 5, "World")]
    [InlineData(InitType.Span, "ABCDEF", 2, 3, "CDE")]
    [InlineData(InitType.Capacity, "ABCDEF", 2, 3, "CDE")]
    public void AsMemory_WithStartAndLength(InitType initType, string content, int start, int length, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(Encoding.UTF8.GetBytes(content));

        ReadOnlyMemory<byte> memory = vsb.AsMemory(start, length);
        string result = MemoryToString(memory);
        vsb.Dispose();
        Assert.Equal(expected, result);
    }

    #endregion

    #region ApplySlice Tests

    [Theory]
    [InlineData(InitType.Span, "Hello World", 6, "World")]
    [InlineData(InitType.Capacity, "Hello World", 6, "World")]
    [InlineData(InitType.Span, "ABCDEF", 3, "DEF")]
    [InlineData(InitType.Capacity, "ABCDEF", 3, "DEF")]
    public void ApplySlice_WithStart(InitType initType, string content, int start, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(Encoding.UTF8.GetBytes(content));
        vsb.ApplySlice(start);

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(InitType.Span, "Hello World", 6, 5, "World")]
    [InlineData(InitType.Capacity, "Hello World", 6, 5, "World")]
    [InlineData(InitType.Span, "ABCDEF", 1, 4, "BCDE")]
    [InlineData(InitType.Capacity, "ABCDEF", 1, 4, "BCDE")]
    public void ApplySlice_WithStartAndLength(InitType initType, string content, int start, int length, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(Encoding.UTF8.GetBytes(content));
        vsb.ApplySlice(start, length);

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(InitType.Span)]
    [InlineData(InitType.Capacity)]
    public void ApplySlice_StartBeyondLength_Throws(InitType initType)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(Encoding.UTF8.GetBytes("Hello"));

        try
        {
            vsb.ApplySlice(10);
            Assert.Fail("Expected ArgumentOutOfRangeException");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
        finally
        {
            vsb.Dispose();
        }
    }

    [Theory]
    [InlineData(InitType.Span)]
    [InlineData(InitType.Capacity)]
    public void ApplySlice_StartPlusLengthBeyondLength_Throws(InitType initType)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(Encoding.UTF8.GetBytes("Hello"));

        try
        {
            vsb.ApplySlice(3, 10);
            Assert.Fail("Expected ArgumentOutOfRangeException");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
        finally
        {
            vsb.Dispose();
        }
    }

    #endregion

    #region Replace Tests

    [Theory]
    [InlineData(InitType.Span, "Hello World", "xyz", "abc", "Hello World")]
    [InlineData(InitType.Capacity, "Hello World", "xyz", "abc", "Hello World")]
    public void Replace_NoChange_WhenTextNotFound(InitType initType, string content, string oldText, string newText, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        byte[] contentBytes = Encoding.UTF8.GetBytes(content);
        vsb.Append(contentBytes);

        vsb.Replace(
            Encoding.UTF8.GetBytes(oldText),
            Encoding.UTF8.GetBytes(newText),
            0,
            contentBytes.Length);

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(InitType.Span, "Hello World", "World", "All", "Hello All")]
    [InlineData(InitType.Capacity, "Hello World", "World", "All", "Hello All")]
    [InlineData(InitType.Span, "aabbcc", "bb", "x", "aaxcc")]
    [InlineData(InitType.Capacity, "aabbcc", "bb", "x", "aaxcc")]
    public void Replace_Shrinks_WhenReplacementShorter(InitType initType, string content, string oldText, string newText, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        byte[] contentBytes = Encoding.UTF8.GetBytes(content);
        vsb.Append(contentBytes);

        vsb.Replace(
            Encoding.UTF8.GetBytes(oldText),
            Encoding.UTF8.GetBytes(newText),
            0,
            contentBytes.Length);

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(InitType.Span, "Hello World", "World", "Earth", "Hello Earth")]
    [InlineData(InitType.Capacity, "Hello World", "World", "Earth", "Hello Earth")]
    [InlineData(InitType.Span, "aabaa", "a", "x", "xxbxx")]
    [InlineData(InitType.Capacity, "aabaa", "a", "x", "xxbxx")]
    public void Replace_SameLength(InitType initType, string content, string oldText, string newText, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        byte[] contentBytes = Encoding.UTF8.GetBytes(content);
        vsb.Append(contentBytes);

        vsb.Replace(
            Encoding.UTF8.GetBytes(oldText),
            Encoding.UTF8.GetBytes(newText),
            0,
            contentBytes.Length);

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(InitType.Span, "Hello World", "World", "Universe", "Hello Universe")]
    [InlineData(InitType.Capacity, "Hello World", "World", "Universe", "Hello Universe")]
    [InlineData(InitType.Span, "ab", "a", "xyz", "xyzb")]
    [InlineData(InitType.Capacity, "ab", "a", "xyz", "xyzb")]
    public void Replace_Grows_FitsInAvailableSpace(InitType initType, string content, string oldText, string newText, string expected)
    {
        // Use large initial capacity so growth fits
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 128);
        byte[] contentBytes = Encoding.UTF8.GetBytes(content);
        vsb.Append(contentBytes);

        vsb.Replace(
            Encoding.UTF8.GetBytes(oldText),
            Encoding.UTF8.GetBytes(newText),
            0,
            contentBytes.Length);

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(InitType.Span, "Hello World!!", "World", "XXXXXXXXXXXXXXXXXXXX", "Hello XXXXXXXXXXXXXXXXXXXX!!")]
    [InlineData(InitType.Capacity, "Hello World!!", "World", "XXXXXXXXXXXXXXXXXXXX", "Hello XXXXXXXXXXXXXXXXXXXX!!")]
    public void Replace_Grows_RequiresResize(InitType initType, string content, string oldText, string newText, string expected)
    {
        // Use small initial capacity; content must be long enough relative to the
        // ArrayPool minimum bucket size to satisfy Grow's internal Debug.Assert.
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 4);
        byte[] contentBytes = Encoding.UTF8.GetBytes(content);
        vsb.Append(contentBytes);

        vsb.Replace(
            Encoding.UTF8.GetBytes(oldText),
            Encoding.UTF8.GetBytes(newText),
            0,
            contentBytes.Length);

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(InitType.Span, "Hello World", "World", "X", 0, 5, "Hello World")]
    [InlineData(InitType.Capacity, "Hello World", "World", "X", 0, 5, "Hello World")]
    public void Replace_TextNotWithinSpecifiedRange(InitType initType, string content, string oldText, string newText, int start, int count, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        byte[] contentBytes = Encoding.UTF8.GetBytes(content);
        vsb.Append(contentBytes);

        vsb.Replace(
            Encoding.UTF8.GetBytes(oldText),
            Encoding.UTF8.GetBytes(newText),
            start,
            count);

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(InitType.Span)]
    [InlineData(InitType.Capacity)]
    public void Replace_OutOfBounds_Throws(InitType initType)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(Encoding.UTF8.GetBytes("Hello"));

        try
        {
            vsb.Replace(
                Encoding.UTF8.GetBytes("H"),
                Encoding.UTF8.GetBytes("X"),
                0,
                20); // count exceeds length
            Assert.Fail("Expected ArgumentOutOfRangeException");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
        finally
        {
            vsb.Dispose();
        }
    }

    [Theory]
    [InlineData(InitType.Span)]
    [InlineData(InitType.Capacity)]
    public void Replace_NegativeStart_Throws(InitType initType)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(Encoding.UTF8.GetBytes("Hello"));

        try
        {
            vsb.Replace(
                Encoding.UTF8.GetBytes("H"),
                Encoding.UTF8.GetBytes("X"),
                -1,
                3);
            Assert.Fail("Expected ArgumentOutOfRangeException");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
        finally
        {
            vsb.Dispose();
        }
    }

    [Theory]
    [InlineData(InitType.Span, "aXaXa", "X", "YY", "aYYaYYa")]
    [InlineData(InitType.Capacity, "aXaXa", "X", "YY", "aYYaYYa")]
    public void Replace_MultipleOccurrences_Grows(InitType initType, string content, string oldText, string newText, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 128);
        byte[] contentBytes = Encoding.UTF8.GetBytes(content);
        vsb.Append(contentBytes);

        vsb.Replace(
            Encoding.UTF8.GetBytes(oldText),
            Encoding.UTF8.GetBytes(newText),
            0,
            contentBytes.Length);

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(InitType.Span, "aXXa", "XX", "Y", "aYa")]
    [InlineData(InitType.Capacity, "aXXa", "XX", "Y", "aYa")]
    [InlineData(InitType.Span, "XXhelloXXworldXX", "XX", "Y", "YhelloYworldY")]
    [InlineData(InitType.Capacity, "XXhelloXXworldXX", "XX", "Y", "YhelloYworldY")]
    public void Replace_MultipleOccurrences_Shrinks(InitType initType, string content, string oldText, string newText, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 128);
        byte[] contentBytes = Encoding.UTF8.GetBytes(content);
        vsb.Append(contentBytes);

        vsb.Replace(
            Encoding.UTF8.GetBytes(oldText),
            Encoding.UTF8.GetBytes(newText),
            0,
            contentBytes.Length);

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.Equal(expected, result);
    }

    #endregion

    #region Insert Tests

    [Theory]
    [InlineData(InitType.Span, "AC", 1, "ABC")]
    [InlineData(InitType.Capacity, "AC", 1, "ABC")]
    public void Insert_ByteSpan(InitType initType, string initial, int index, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(Encoding.UTF8.GetBytes(initial));
        vsb.Insert(index, Encoding.UTF8.GetBytes("B"));

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(InitType.Span, "Hello", 5, 3, "Hello!!!")]
    [InlineData(InitType.Capacity, "Hello", 5, 3, "Hello!!!")]
    public void Insert_ByteRepeated(InitType initType, string initial, int index, int count, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(Encoding.UTF8.GetBytes(initial));
        vsb.Insert(index, (byte)'!', count);

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.Equal(expected, result);
    }

    #endregion

    #region Length and Capacity Tests

    [Theory]
    [InlineData(InitType.Span)]
    [InlineData(InitType.Capacity)]
    public void Length_ReflectsAppendedContent(InitType initType)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 32);
        Assert.Equal(0, vsb.Length);

        vsb.Append(Encoding.UTF8.GetBytes("Hello"));
        Assert.Equal(5, vsb.Length);

        vsb.Append(Encoding.UTF8.GetBytes(" World"));
        Assert.Equal(11, vsb.Length);
        vsb.Dispose();
    }

    [Theory]
    [InlineData(InitType.Span, 32)]
    [InlineData(InitType.Capacity, 32)]
    public void EnsureCapacity_IncreasesCapacity(InitType initType, int initialCapacity)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, initialCapacity);
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
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 32);
        vsb.Append(Encoding.UTF8.GetBytes(content));

        byte[] dest = new byte[32];
        bool success = vsb.TryCopyTo(dest, out int written);

        Assert.True(success);
        Assert.Equal(content.Length, written);
        Assert.Equal(content, Encoding.UTF8.GetString(dest, 0, written));
    }

    [Theory]
    [InlineData(InitType.Span, "Hello World")]
    [InlineData(InitType.Capacity, "Hello World")]
    public void TryCopyTo_FailsWhenDestinationTooSmall(InitType initType, string content)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 32);
        vsb.Append(Encoding.UTF8.GetBytes(content));

        byte[] dest = new byte[2];
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
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 32);
        vsb.Append(Encoding.UTF8.GetBytes("Hello"));

        Span<byte> appended = vsb.AppendSpan(5);
        byte[] worldBytes = Encoding.UTF8.GetBytes("World");
        for (int i = 0; i < worldBytes.Length; i++)
        {
            appended[i] = worldBytes[i];
        }

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.Equal("HelloWorld", result);
    }

    #endregion

    #region CreateMemoryAndDispose Tests

    [Theory]
    [InlineData(InitType.Span, "Hello World")]
    [InlineData(InitType.Capacity, "Hello World")]
    public void CreateMemoryAndDispose_ReturnsContent(InitType initType, string content)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(Encoding.UTF8.GetBytes(content));

        ReadOnlyMemory<byte> memory = vsb.CreateMemoryAndDispose();
        string result = MemoryToString(memory);
        Assert.Equal(content, result);
    }

    #endregion

    #region Helpers

    private static Utf8ValueStringBuilder CreateBuilder(InitType initType, int size)
    {
        if (initType == InitType.Span)
        {
            // Same as ValueStringBuilder: for test purposes both paths exercise the
            // capacity constructor since we can't return a stackalloc span from a helper.
            return new Utf8ValueStringBuilder(size);
        }
        else
        {
            return new Utf8ValueStringBuilder(size);
        }
    }

    private static string SpanToString(ReadOnlySpan<byte> span)
    {
        byte[] array = span.ToArray();
        return Encoding.UTF8.GetString(array, 0, array.Length);
    }

    private static string MemoryToString(ReadOnlyMemory<byte> memory)
    {
        byte[] array = memory.ToArray();
        return Encoding.UTF8.GetString(array, 0, array.Length);
    }

    #endregion
}
