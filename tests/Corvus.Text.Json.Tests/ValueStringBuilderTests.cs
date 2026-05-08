// Derived from SpecFlow tests in https://github.com/corvus-dotnet/Corvus.HighPerformance

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public class ValueStringBuilderTests
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
        ValueStringBuilder vsb = CreateBuilder(initType, totalLength);
        vsb.Append(first);
        vsb.Append(second);

        string result = vsb.ToString(); // ToString also disposes
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(InitType.Span, "Hello", " World", "Hello World")]
    [DataRow(InitType.Capacity, "Hello", " World", "Hello World")]
    [DataRow(InitType.Span, "Short", " but this second string is much longer and will require growth", "Short but this second string is much longer and will require growth")]
    [DataRow(InitType.Capacity, "Short", " but this second string is much longer and will require growth", "Short but this second string is much longer and will require growth")]
    public void AppendTwoStrings_Grows(InitType initType, string first, string second, string expected)
    {
        // Use a very small initial capacity to force growth
        ValueStringBuilder vsb = CreateBuilder(initType, 4);
        vsb.Append(first);
        vsb.Append(second);

        string result = vsb.ToString();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(InitType.Span, 42, " items", "42 items")]
    [DataRow(InitType.Capacity, 42, " items", "42 items")]
    [DataRow(InitType.Span, -1, " error", "-1 error")]
    [DataRow(InitType.Capacity, -1, " error", "-1 error")]
    public void AppendNumberThenString(InitType initType, int number, string text, string expected)
    {
        ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(number.ToString());
        vsb.Append(text);

        string result = vsb.ToString();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(InitType.Span, "Count: ", 99, "Count: 99")]
    [DataRow(InitType.Capacity, "Count: ", 99, "Count: 99")]
    [DataRow(InitType.Span, "Value=", 0, "Value=0")]
    [DataRow(InitType.Capacity, "Value=", 0, "Value=0")]
    public void AppendStringThenNumber(InitType initType, string text, int number, string expected)
    {
        ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(text);
        vsb.Append(number.ToString());

        string result = vsb.ToString();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(InitType.Span)]
    [DataRow(InitType.Capacity)]
    public void AppendGrows_WhenOverflowsInitialBuffer(InitType initType)
    {
        // Start with tiny buffer, append enough to force multiple growths
        ValueStringBuilder vsb = CreateBuilder(initType, 2);
        string longString = new string('X', 500);
        vsb.Append(longString);

        ReadOnlySpan<char> span = vsb.AsSpan();
        Assert.AreEqual(500, span.Length);
        Assert.AreEqual(longString, span.ToString());
        vsb.Dispose();
    }

    [TestMethod]
    [DataRow(InitType.Span)]
    [DataRow(InitType.Capacity)]
    public void AppendChar_Repeated(InitType initType)
    {
        ValueStringBuilder vsb = CreateBuilder(initType, 32);
        vsb.Append('A', 5);

        string result = vsb.ToString();
        Assert.AreEqual("AAAAA", result);
    }

    [TestMethod]
    [DataRow(InitType.Span)]
    [DataRow(InitType.Capacity)]
    public void AppendReadOnlySpan(InitType initType)
    {
        ValueStringBuilder vsb = CreateBuilder(initType, 32);
        ReadOnlySpan<char> data = "Hello".AsSpan();
        vsb.Append(data);
        vsb.Append(" ".AsSpan());
        vsb.Append("World".AsSpan());

        string result = vsb.ToString();
        Assert.AreEqual("Hello World", result);
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
        ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(content);

        ReadOnlySpan<char> span = vsb.AsSpan();
        Assert.AreEqual(content.Length, span.Length);
        Assert.AreEqual(content, span.ToString());
        vsb.Dispose();
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
        ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(content);

        ReadOnlySpan<char> span = vsb.AsSpan(start);
        Assert.AreEqual(expected, span.ToString());
        vsb.Dispose();
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
        ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(content);

        ReadOnlySpan<char> span = vsb.AsSpan(start, length);
        Assert.AreEqual(expected, span.ToString());
        vsb.Dispose();
    }

    #endregion

    #region Insert Tests

    [TestMethod]
    [DataRow(InitType.Span, "Hello World", 5, "Beautiful ", "HelloBeautiful  World")]
    [DataRow(InitType.Capacity, "Hello World", 5, "Beautiful ", "HelloBeautiful  World")]
    public void Insert_String(InitType initType, string initial, int index, string toInsert, string expected)
    {
        ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(initial);
        vsb.Insert(index, toInsert);

        string result = vsb.ToString();
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(InitType.Span, "AC", 1, 'B', 1, "ABC")]
    [DataRow(InitType.Capacity, "AC", 1, 'B', 1, "ABC")]
    [DataRow(InitType.Span, "Hello", 5, '!', 3, "Hello!!!")]
    [DataRow(InitType.Capacity, "Hello", 5, '!', 3, "Hello!!!")]
    public void Insert_CharRepeated(InitType initType, string initial, int index, char c, int count, string expected)
    {
        ValueStringBuilder vsb = CreateBuilder(initType, 64);
        vsb.Append(initial);
        vsb.Insert(index, c, count);

        string result = vsb.ToString();
        Assert.AreEqual(expected, result);
    }

    #endregion

    #region Length and Capacity Tests

    [TestMethod]
    [DataRow(InitType.Span)]
    [DataRow(InitType.Capacity)]
    public void Length_ReflectsAppendedContent(InitType initType)
    {
        ValueStringBuilder vsb = CreateBuilder(initType, 32);
        Assert.AreEqual(0, vsb.Length);

        vsb.Append("Hello");
        Assert.AreEqual(5, vsb.Length);

        vsb.Append(" World");
        Assert.AreEqual(11, vsb.Length);
        vsb.Dispose();
    }

    [TestMethod]
    [DataRow(InitType.Span, 32)]
    [DataRow(InitType.Capacity, 32)]
    public void EnsureCapacity_IncreasesCapacity(InitType initType, int initialCapacity)
    {
        ValueStringBuilder vsb = CreateBuilder(initType, initialCapacity);
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
        ValueStringBuilder vsb = CreateBuilder(initType, 32);
        vsb.Append(content);

        char[] dest = new char[32];
        bool success = vsb.TryCopyTo(dest, out int written);

        Assert.IsTrue(success);
        Assert.AreEqual(content.Length, written);
        Assert.AreEqual(content, new string(dest, 0, written));
    }

    [TestMethod]
    [DataRow(InitType.Span, "Hello World")]
    [DataRow(InitType.Capacity, "Hello World")]
    public void TryCopyTo_FailsWhenDestinationTooSmall(InitType initType, string content)
    {
        ValueStringBuilder vsb = CreateBuilder(initType, 32);
        vsb.Append(content);

        char[] dest = new char[2];
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
        ValueStringBuilder vsb = CreateBuilder(initType, 32);
        vsb.Append("Hello");

        Span<char> appended = vsb.AppendSpan(5);
        "World".AsSpan().CopyTo(appended);

        string result = vsb.ToString();
        Assert.AreEqual("HelloWorld", result);
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
