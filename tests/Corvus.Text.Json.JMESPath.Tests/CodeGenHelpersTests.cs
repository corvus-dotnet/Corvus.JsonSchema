// <copyright file="CodeGenHelpersTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.JMESPath;
using Xunit;

namespace Corvus.Text.Json.JMESPath.Tests;

/// <summary>
/// Direct unit tests for <see cref="JMESPathCodeGenHelpers"/> public methods.
/// </summary>
public class CodeGenHelpersTests
{
    // ----- DoubleToElement / SmallIntegers cache -----

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(255)]
    [InlineData(511)]
    public void DoubleToElement_SmallInteger_ReturnsCachedElement(int value)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.DoubleToElement(value, workspace);
        Assert.Equal(value, result.GetDouble());
    }

    [Fact]
    public void DoubleToElement_SmallInteger_ReturnsSameInstance()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement a = JMESPathCodeGenHelpers.DoubleToElement(42, workspace);
        JsonElement b = JMESPathCodeGenHelpers.DoubleToElement(42, workspace);

        // Both should be the same cached element — verify by raw text identity
        Assert.Equal(a.GetRawText(), b.GetRawText());
    }

    [Theory]
    [InlineData(512)]
    [InlineData(1000)]
    [InlineData(1_000_000)]
    public void DoubleToElement_AboveCacheRange_CreatesNewElement(double value)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.DoubleToElement(value, workspace);
        Assert.Equal(value, result.GetDouble());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(-0.5)]
    public void DoubleToElement_Negative_CreatesNewElement(double value)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.DoubleToElement(value, workspace);
        Assert.Equal(value, result.GetDouble());
    }

    [Theory]
    [InlineData(1.5)]
    [InlineData(0.1)]
    [InlineData(99.99)]
    public void DoubleToElement_Fractional_CreatesNewElement(double value)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.DoubleToElement(value, workspace);
        Assert.Equal(value, result.GetDouble());
    }

    // ----- StringToElement -----

    [Fact]
    public void StringToElement_PlainString_ReturnsCorrectElement()
    {
        JsonElement result = JMESPathCodeGenHelpers.StringToElement("hello");
        Assert.Equal("hello", result.GetString());
    }

    [Fact]
    public void StringToElement_EmptyString_ReturnsEmptyStringElement()
    {
        JsonElement result = JMESPathCodeGenHelpers.StringToElement(string.Empty);
        Assert.Equal(string.Empty, result.GetString());
    }

    [Theory]
    [InlineData("hello \"world\"")]   // embedded quotes
    [InlineData("back\\slash")]       // embedded backslash
    [InlineData("tab\there")]         // tab
    [InlineData("line\nbreak")]       // newline
    [InlineData("return\rhere")]      // carriage return
    public void StringToElement_EscapeCharacters_RoundTripsCorrectly(string value)
    {
        JsonElement result = JMESPathCodeGenHelpers.StringToElement(value);
        Assert.Equal(value, result.GetString());
    }

    [Fact]
    public void StringToElement_ControlCharacter_EscapedAsUnicode()
    {
        string value = "\u0001";
        JsonElement result = JMESPathCodeGenHelpers.StringToElement(value);
        Assert.Equal(value, result.GetString());
    }

    [Fact]
    public void StringToElement_Unicode_PreservesCharacters()
    {
        string value = "café ☕ 日本語";
        JsonElement result = JMESPathCodeGenHelpers.StringToElement(value);
        Assert.Equal(value, result.GetString());
    }

    // ----- NormalizeSliceIndex -----

    [Theory]
    [InlineData(0, 5, true, true, 0)]    // start=0 in 5-element array, positive step
    [InlineData(3, 5, true, true, 3)]    // start=3, within bounds
    [InlineData(10, 5, true, true, 5)]   // start=10, clamped to length
    [InlineData(-1, 5, true, true, 4)]   // start=-1, wraps to 4
    [InlineData(-5, 5, true, true, 0)]   // start=-5, wraps to 0
    [InlineData(-10, 5, true, true, 0)]  // start=-10, clamped to 0
    public void NormalizeSliceIndex_PositiveStep_Start(int index, int length, bool isStart, bool positiveStep, int expected)
    {
        int result = JMESPathCodeGenHelpers.NormalizeSliceIndex(index, length, isStart, positiveStep);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, 5, false, true, 0)]   // stop=0
    [InlineData(3, 5, false, true, 3)]   // stop=3
    [InlineData(10, 5, false, true, 5)]  // stop=10, clamped to length
    [InlineData(-1, 5, false, true, 4)]  // stop=-1, wraps to 4
    [InlineData(-10, 5, false, true, 0)] // stop=-10, clamped to 0
    public void NormalizeSliceIndex_PositiveStep_Stop(int index, int length, bool isStart, bool positiveStep, int expected)
    {
        int result = JMESPathCodeGenHelpers.NormalizeSliceIndex(index, length, isStart, positiveStep);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0, 5, true, false, 0)]   // start=0, negative step
    [InlineData(4, 5, true, false, 4)]   // start=4, within bounds
    [InlineData(10, 5, true, false, 4)]  // start=10, clamped to length-1
    [InlineData(-1, 5, true, false, 4)]  // start=-1, wraps to 4
    [InlineData(-10, 5, true, false, -1)] // start=-10, clamped to -1
    public void NormalizeSliceIndex_NegativeStep_Start(int index, int length, bool isStart, bool positiveStep, int expected)
    {
        int result = JMESPathCodeGenHelpers.NormalizeSliceIndex(index, length, isStart, positiveStep);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizeSliceIndex_EmptyArray_ClampsCorrectly()
    {
        Assert.Equal(0, JMESPathCodeGenHelpers.NormalizeSliceIndex(0, 0, true, true));
        Assert.Equal(0, JMESPathCodeGenHelpers.NormalizeSliceIndex(5, 0, true, true));
        Assert.Equal(0, JMESPathCodeGenHelpers.NormalizeSliceIndex(-1, 0, true, true));
    }

    // ----- Merge -----

    [Fact]
    public void Merge_TwoObjects_CombinesProperties()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement obj1 = JsonElement.ParseValue("""{"a":1}"""u8);
        JsonElement obj2 = JsonElement.ParseValue("""{"b":2}"""u8);

        JsonElement result = JMESPathCodeGenHelpers.Merge(obj1, obj2, workspace);

        Assert.Equal(1, result.GetProperty("a"u8).GetDouble());
        Assert.Equal(2, result.GetProperty("b"u8).GetDouble());
    }

    [Fact]
    public void Merge_OverlappingKeys_SecondWins()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement obj1 = JsonElement.ParseValue("""{"a":1,"b":2}"""u8);
        JsonElement obj2 = JsonElement.ParseValue("""{"b":99,"c":3}"""u8);

        JsonElement result = JMESPathCodeGenHelpers.Merge(obj1, obj2, workspace);

        Assert.Equal(1, result.GetProperty("a"u8).GetDouble());
        Assert.Equal(99, result.GetProperty("b"u8).GetDouble());
        Assert.Equal(3, result.GetProperty("c"u8).GetDouble());
    }

    [Fact]
    public void Merge_ArrayOverload_CombinesMultipleObjects()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement[] objects =
        [
            JsonElement.ParseValue("""{"a":1}"""u8),
            JsonElement.ParseValue("""{"b":2}"""u8),
            JsonElement.ParseValue("""{"c":3}"""u8),
        ];

        JsonElement result = JMESPathCodeGenHelpers.Merge(objects, workspace);

        Assert.Equal(1, result.GetProperty("a"u8).GetDouble());
        Assert.Equal(2, result.GetProperty("b"u8).GetDouble());
        Assert.Equal(3, result.GetProperty("c"u8).GetDouble());
    }

    // ----- IsTruthy -----

    [Theory]
    [InlineData("""true""", true)]
    [InlineData("""false""", false)]
    [InlineData("""null""", false)]
    [InlineData("""0""", true)]         // Numbers are truthy (even 0)
    [InlineData("""1""", true)]
    [InlineData("\"\"", false)]           // Empty string is falsy
    [InlineData("\"hello\"", true)]       // Non-empty string is truthy
    [InlineData("""[]""", false)]       // Empty array is falsy
    [InlineData("""[1]""", true)]       // Non-empty array is truthy
    [InlineData("""{}""", false)]       // Empty object is falsy
    [InlineData("""{"a":1}""", true)]   // Non-empty object is truthy
    public void IsTruthy_FollowsJMESPathSemantics(string json, bool expected)
    {
        JsonElement element = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        Assert.Equal(expected, JMESPathCodeGenHelpers.IsTruthy(element));
    }

    // ----- DeepEquals -----

    [Theory]
    [InlineData("""1""", """1""", true)]
    [InlineData("""1""", """2""", false)]
    [InlineData("\"a\"", "\"a\"", true)]
    [InlineData("\"a\"", "\"b\"", false)]
    [InlineData("""true""", """true""", true)]
    [InlineData("""true""", """false""", false)]
    [InlineData("""null""", """null""", true)]
    [InlineData("""[1,2]""", """[1,2]""", true)]
    [InlineData("""[1,2]""", """[2,1]""", false)]
    [InlineData("""{"a":1}""", """{"a":1}""", true)]
    [InlineData("""{"a":1}""", """{"a":2}""", false)]
    public void DeepEquals_ComparesCorrectly(string leftJson, string rightJson, bool expected)
    {
        JsonElement left = JsonElement.ParseValue(Encoding.UTF8.GetBytes(leftJson));
        JsonElement right = JsonElement.ParseValue(Encoding.UTF8.GetBytes(rightJson));
        Assert.Equal(expected, JMESPathCodeGenHelpers.DeepEquals(left, right));
    }
}
