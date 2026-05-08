// Derived from SpecFlow tests in https://github.com/corvus-dotnet/Corvus.HighPerformance

using System.Buffers.Text;
using System.Text;
using Corvus.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public class Utf8ValueStringBuilderTests
{
    public enum InitType
    {
        Span,
        Capacity,
    }

    #region Append Tests

    [TestMethod]
    [DataRow(InitType.Span, "Hello", " World", "Hello World")]
    [DataRow(InitType.Capacity, "Hello", " World", "Hello World")]
    [DataRow(InitType.Span, "A", "B", "AB")]
    [DataRow(InitType.Capacity, "A", "B", "AB")]
    [DataRow(InitType.Span, "", "Something", "Something")]
    [DataRow(InitType.Capacity, "", "Something", "Something")]
    public void AppendTwoStrings_FitsInSpace(InitType initType, string first, string second, string expected)
    {
        int totalLength = first.Length + second.Length + 10;
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, totalLength);
        vsb.Append(Encoding.UTF8.GetBytes(first));
        vsb.Append(Encoding.UTF8.GetBytes(second));

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(InitType.Span, "Hello", " World", "Hello World")]
    [DataRow(InitType.Capacity, "Hello", " World", "Hello World")]
    [DataRow(InitType.Span, "Short", " but this second string is much longer and will require growth", "Short but this second string is much longer and will require growth")]
    [DataRow(InitType.Capacity, "Short", " but this second string is much longer and will require growth", "Short but this second string is much longer and will require growth")]
    public void AppendTwoStrings_Grows(InitType initType, string first, string second, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 4);
        vsb.Append(Encoding.UTF8.GetBytes(first));
        vsb.Append(Encoding.UTF8.GetBytes(second));

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(InitType.Span, 42, " items", "42 items")]
    [DataRow(InitType.Capacity, 42, " items", "42 items")]
    [DataRow(InitType.Span, -1, " error", "-1 error")]
    [DataRow(InitType.Capacity, -1, " error", "-1 error")]
    public void AppendNumberThenString(InitType initType, int number, string text, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(number);
        vsb.Append(Encoding.UTF8.GetBytes(text));

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(InitType.Span, "Count: ", 99, "Count: 99")]
    [DataRow(InitType.Capacity, "Count: ", 99, "Count: 99")]
    [DataRow(InitType.Span, "Value=", 0, "Value=0")]
    [DataRow(InitType.Capacity, "Value=", 0, "Value=0")]
    public void AppendStringThenNumber(InitType initType, string text, int number, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(Encoding.UTF8.GetBytes(text));
        vsb.Append(number);

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(InitType.Span, 123456789L, "123456789")]
    [DataRow(InitType.Capacity, 123456789L, "123456789")]
    [DataRow(InitType.Span, -99L, "-99")]
    [DataRow(InitType.Capacity, -99L, "-99")]
    public void AppendLong(InitType initType, long number, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(number);

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(InitType.Span)]
    [DataRow(InitType.Capacity)]
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
        Assert.AreEqual(500, span.Length);
        for (int i = 0; i < span.Length; i++)
        {
            Assert.AreEqual((byte)'X', span[i]);
        }

        vsb.Dispose();
    }

    [TestMethod]
    [DataRow(InitType.Span)]
    [DataRow(InitType.Capacity)]
    public void AppendByte_Repeated(InitType initType)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 32);
        vsb.Append((byte)'A', 5);

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.AreEqual("AAAAA", result);
    }

    [TestMethod]
    [DataRow(InitType.Span, "Hello")]
    [DataRow(InitType.Capacity, "Hello")]
    [DataRow(InitType.Span, "ASCII only")]
    [DataRow(InitType.Capacity, "ASCII only")]
    public void AppendAsciiString(InitType initType, string text)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.AppendAsciiString(text);

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.AreEqual(text, result);
    }

    [TestMethod]
    [DataRow(InitType.Span, 'A', "A")]
    [DataRow(InitType.Capacity, 'A', "A")]
    [DataRow(InitType.Span, '!', "!")]
    [DataRow(InitType.Capacity, '!', "!")]
    public void AppendChar(InitType initType, char c, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 32);
        vsb.AppendChar(c);

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.AreEqual(expected, result);
    }

    #endregion

    #region AsSpan Tests

    [TestMethod]
    [DataRow(InitType.Span, "Hello World")]
    [DataRow(InitType.Capacity, "Hello World")]
    [DataRow(InitType.Span, "")]
    [DataRow(InitType.Capacity, "")]
    public void AsSpan_ReturnsFullContent(InitType initType, string content)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(Encoding.UTF8.GetBytes(content));

        ReadOnlySpan<byte> span = vsb.AsSpan();
        string result = SpanToString(span);
        vsb.Dispose();
        Assert.AreEqual(content, result);
    }

    [TestMethod]
    [DataRow(InitType.Span, "Hello World", 6, "World")]
    [DataRow(InitType.Capacity, "Hello World", 6, "World")]
    [DataRow(InitType.Span, "ABCDEF", 0, "ABCDEF")]
    [DataRow(InitType.Capacity, "ABCDEF", 0, "ABCDEF")]
    [DataRow(InitType.Span, "ABCDEF", 5, "F")]
    [DataRow(InitType.Capacity, "ABCDEF", 5, "F")]
    public void AsSpan_WithStart(InitType initType, string content, int start, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(Encoding.UTF8.GetBytes(content));

        ReadOnlySpan<byte> span = vsb.AsSpan(start);
        string result = SpanToString(span);
        vsb.Dispose();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(InitType.Span, "Hello World", 6, 5, "World")]
    [DataRow(InitType.Capacity, "Hello World", 6, 5, "World")]
    [DataRow(InitType.Span, "ABCDEF", 2, 3, "CDE")]
    [DataRow(InitType.Capacity, "ABCDEF", 2, 3, "CDE")]
    [DataRow(InitType.Span, "Hello World", 0, 5, "Hello")]
    [DataRow(InitType.Capacity, "Hello World", 0, 5, "Hello")]
    public void AsSpan_WithStartAndLength(InitType initType, string content, int start, int length, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(Encoding.UTF8.GetBytes(content));

        ReadOnlySpan<byte> span = vsb.AsSpan(start, length);
        string result = SpanToString(span);
        vsb.Dispose();
        Assert.AreEqual(expected, result);
    }

    #endregion

    #region AsMemory Tests

    [TestMethod]
    [DataRow(InitType.Span, "Hello World")]
    [DataRow(InitType.Capacity, "Hello World")]
    public void AsMemory_ReturnsFullContent(InitType initType, string content)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(Encoding.UTF8.GetBytes(content));

        ReadOnlyMemory<byte> memory = vsb.AsMemory();
        string result = MemoryToString(memory);
        vsb.Dispose();
        Assert.AreEqual(content, result);
    }

    [TestMethod]
    [DataRow(InitType.Span, "Hello World", 6, "World")]
    [DataRow(InitType.Capacity, "Hello World", 6, "World")]
    public void AsMemory_WithStart(InitType initType, string content, int start, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(Encoding.UTF8.GetBytes(content));

        ReadOnlyMemory<byte> memory = vsb.AsMemory(start);
        string result = MemoryToString(memory);
        vsb.Dispose();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(InitType.Span, "Hello World", 6, 5, "World")]
    [DataRow(InitType.Capacity, "Hello World", 6, 5, "World")]
    [DataRow(InitType.Span, "ABCDEF", 2, 3, "CDE")]
    [DataRow(InitType.Capacity, "ABCDEF", 2, 3, "CDE")]
    public void AsMemory_WithStartAndLength(InitType initType, string content, int start, int length, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(Encoding.UTF8.GetBytes(content));

        ReadOnlyMemory<byte> memory = vsb.AsMemory(start, length);
        string result = MemoryToString(memory);
        vsb.Dispose();
        Assert.AreEqual(expected, result);
    }

    #endregion

    #region ApplySlice Tests

    [TestMethod]
    [DataRow(InitType.Span, "Hello World", 6, "World")]
    [DataRow(InitType.Capacity, "Hello World", 6, "World")]
    [DataRow(InitType.Span, "ABCDEF", 3, "DEF")]
    [DataRow(InitType.Capacity, "ABCDEF", 3, "DEF")]
    public void ApplySlice_WithStart(InitType initType, string content, int start, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(Encoding.UTF8.GetBytes(content));
        vsb.ApplySlice(start);

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(InitType.Span, "Hello World", 6, 5, "World")]
    [DataRow(InitType.Capacity, "Hello World", 6, 5, "World")]
    [DataRow(InitType.Span, "ABCDEF", 1, 4, "BCDE")]
    [DataRow(InitType.Capacity, "ABCDEF", 1, 4, "BCDE")]
    public void ApplySlice_WithStartAndLength(InitType initType, string content, int start, int length, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(Encoding.UTF8.GetBytes(content));
        vsb.ApplySlice(start, length);

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(InitType.Span)]
    [DataRow(InitType.Capacity)]
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

    [TestMethod]
    [DataRow(InitType.Span)]
    [DataRow(InitType.Capacity)]
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

    [TestMethod]
    [DataRow(InitType.Span, "Hello World", "xyz", "abc", "Hello World")]
    [DataRow(InitType.Capacity, "Hello World", "xyz", "abc", "Hello World")]
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
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(InitType.Span, "Hello World", "World", "All", "Hello All")]
    [DataRow(InitType.Capacity, "Hello World", "World", "All", "Hello All")]
    [DataRow(InitType.Span, "aabbcc", "bb", "x", "aaxcc")]
    [DataRow(InitType.Capacity, "aabbcc", "bb", "x", "aaxcc")]
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
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(InitType.Span, "Hello World", "World", "Earth", "Hello Earth")]
    [DataRow(InitType.Capacity, "Hello World", "World", "Earth", "Hello Earth")]
    [DataRow(InitType.Span, "aabaa", "a", "x", "xxbxx")]
    [DataRow(InitType.Capacity, "aabaa", "a", "x", "xxbxx")]
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
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(InitType.Span, "Hello World", "World", "Universe", "Hello Universe")]
    [DataRow(InitType.Capacity, "Hello World", "World", "Universe", "Hello Universe")]
    [DataRow(InitType.Span, "ab", "a", "xyz", "xyzb")]
    [DataRow(InitType.Capacity, "ab", "a", "xyz", "xyzb")]
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
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(InitType.Span, "Hello World!!", "World", "XXXXXXXXXXXXXXXXXXXX", "Hello XXXXXXXXXXXXXXXXXXXX!!")]
    [DataRow(InitType.Capacity, "Hello World!!", "World", "XXXXXXXXXXXXXXXXXXXX", "Hello XXXXXXXXXXXXXXXXXXXX!!")]
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
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(InitType.Span, "Hello World", "World", "X", 0, 5, "Hello World")]
    [DataRow(InitType.Capacity, "Hello World", "World", "X", 0, 5, "Hello World")]
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
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(InitType.Span)]
    [DataRow(InitType.Capacity)]
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

    [TestMethod]
    [DataRow(InitType.Span)]
    [DataRow(InitType.Capacity)]
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

    [TestMethod]
    [DataRow(InitType.Span, "aXaXa", "X", "YY", "aYYaYYa")]
    [DataRow(InitType.Capacity, "aXaXa", "X", "YY", "aYYaYYa")]
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
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(InitType.Span, "aXXa", "XX", "Y", "aYa")]
    [DataRow(InitType.Capacity, "aXXa", "XX", "Y", "aYa")]
    [DataRow(InitType.Span, "XXhelloXXworldXX", "XX", "Y", "YhelloYworldY")]
    [DataRow(InitType.Capacity, "XXhelloXXworldXX", "XX", "Y", "YhelloYworldY")]
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
        Assert.AreEqual(expected, result);
    }

    #endregion

    #region Insert Tests

    [TestMethod]
    [DataRow(InitType.Span, "AC", 1, "ABC")]
    [DataRow(InitType.Capacity, "AC", 1, "ABC")]
    public void Insert_ByteSpan(InitType initType, string initial, int index, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(Encoding.UTF8.GetBytes(initial));
        vsb.Insert(index, Encoding.UTF8.GetBytes("B"));

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(InitType.Span, "Hello", 5, 3, "Hello!!!")]
    [DataRow(InitType.Capacity, "Hello", 5, 3, "Hello!!!")]
    public void Insert_ByteRepeated(InitType initType, string initial, int index, int count, string expected)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(Encoding.UTF8.GetBytes(initial));
        vsb.Insert(index, (byte)'!', count);

        string result = SpanToString(vsb.AsSpan());
        vsb.Dispose();
        Assert.AreEqual(expected, result);
    }

    #endregion

    #region Length and Capacity Tests

    [TestMethod]
    [DataRow(InitType.Span)]
    [DataRow(InitType.Capacity)]
    public void Length_ReflectsAppendedContent(InitType initType)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 32);
        Assert.AreEqual(0, vsb.Length);

        vsb.Append(Encoding.UTF8.GetBytes("Hello"));
        Assert.AreEqual(5, vsb.Length);

        vsb.Append(Encoding.UTF8.GetBytes(" World"));
        Assert.AreEqual(11, vsb.Length);
        vsb.Dispose();
    }

    [TestMethod]
    [DataRow(InitType.Span, 32)]
    [DataRow(InitType.Capacity, 32)]
    public void EnsureCapacity_IncreasesCapacity(InitType initType, int initialCapacity)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, initialCapacity);
        vsb.EnsureCapacity(100);

        Assert.IsTrue(vsb.Capacity >= 100);
        vsb.Dispose();
    }

    #endregion

    #region TryCopyTo Tests

    [TestMethod]
    [DataRow(InitType.Span, "Hello")]
    [DataRow(InitType.Capacity, "Hello")]
    public void TryCopyTo_SucceedsWhenDestinationLargeEnough(InitType initType, string content)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 32);
        vsb.Append(Encoding.UTF8.GetBytes(content));

        byte[] dest = new byte[32];
        bool success = vsb.TryCopyTo(dest, out int written);

        Assert.IsTrue(success);
        Assert.AreEqual(content.Length, written);
        Assert.AreEqual(content, Encoding.UTF8.GetString(dest, 0, written));
    }

    [TestMethod]
    [DataRow(InitType.Span, "Hello World")]
    [DataRow(InitType.Capacity, "Hello World")]
    public void TryCopyTo_FailsWhenDestinationTooSmall(InitType initType, string content)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 32);
        vsb.Append(Encoding.UTF8.GetBytes(content));

        byte[] dest = new byte[2];
        bool success = vsb.TryCopyTo(dest, out int written);

        Assert.IsFalse(success);
        Assert.AreEqual(0, written);
    }

    #endregion

    #region AppendSpan Tests

    [TestMethod]
    [DataRow(InitType.Span)]
    [DataRow(InitType.Capacity)]
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
        Assert.AreEqual("HelloWorld", result);
    }

    #endregion

    #region CreateMemoryAndDispose Tests

    [TestMethod]
    [DataRow(InitType.Span, "Hello World")]
    [DataRow(InitType.Capacity, "Hello World")]
    public void CreateMemoryAndDispose_ReturnsContent(InitType initType, string content)
    {
        Utf8ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(Encoding.UTF8.GetBytes(content));

        ReadOnlyMemory<byte> memory = vsb.CreateMemoryAndDispose();
        string result = MemoryToString(memory);
        Assert.AreEqual(content, result);
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
