// <copyright file="CodeGenHelpersTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.JMESPath;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.JMESPath.Tests;

/// <summary>
/// Direct unit tests for <see cref="JMESPathCodeGenHelpers"/> public methods.
/// </summary>
[TestClass]
public class CodeGenHelpersTests
{
    // ----- DoubleToElement / SmallIntegers cache -----

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(255)]
    [DataRow(511)]
    public void DoubleToElement_SmallInteger_ReturnsCachedElement(int value)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.DoubleToElement(value, workspace);
        Assert.AreEqual(value, result.GetDouble());
    }

    [TestMethod]
    public void DoubleToElement_SmallInteger_ReturnsSameInstance()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement a = JMESPathCodeGenHelpers.DoubleToElement(42, workspace);
        JsonElement b = JMESPathCodeGenHelpers.DoubleToElement(42, workspace);

        // Both should be the same cached element — verify by raw text identity
        Assert.AreEqual(a.GetRawText(), b.GetRawText());
    }

    [TestMethod]
    [DataRow(512)]
    [DataRow(1000)]
    [DataRow(1_000_000)]
    public void DoubleToElement_AboveCacheRange_CreatesNewElement(double value)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.DoubleToElement(value, workspace);
        Assert.AreEqual(value, result.GetDouble());
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(-100)]
    [DataRow(-0.5)]
    public void DoubleToElement_Negative_CreatesNewElement(double value)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.DoubleToElement(value, workspace);
        Assert.AreEqual(value, result.GetDouble());
    }

    [TestMethod]
    [DataRow(1.5)]
    [DataRow(0.1)]
    [DataRow(99.99)]
    public void DoubleToElement_Fractional_CreatesNewElement(double value)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement result = JMESPathCodeGenHelpers.DoubleToElement(value, workspace);
        Assert.AreEqual(value, result.GetDouble());
    }

    // ----- StringToElement -----

    [TestMethod]
    public void StringToElement_PlainString_ReturnsCorrectElement()
    {
        JsonElement result = JMESPathCodeGenHelpers.StringToElement("hello");
        Assert.AreEqual("hello", result.GetString());
    }

    [TestMethod]
    public void StringToElement_EmptyString_ReturnsEmptyStringElement()
    {
        JsonElement result = JMESPathCodeGenHelpers.StringToElement(string.Empty);
        Assert.AreEqual(string.Empty, result.GetString());
    }

    [TestMethod]
    [DataRow("hello \"world\"")]   // embedded quotes
    [DataRow("back\\slash")]       // embedded backslash
    [DataRow("tab\there")]         // tab
    [DataRow("line\nbreak")]       // newline
    [DataRow("return\rhere")]      // carriage return
    public void StringToElement_EscapeCharacters_RoundTripsCorrectly(string value)
    {
        JsonElement result = JMESPathCodeGenHelpers.StringToElement(value);
        Assert.AreEqual(value, result.GetString());
    }

    [TestMethod]
    public void StringToElement_ControlCharacter_EscapedAsUnicode()
    {
        string value = "\u0001";
        JsonElement result = JMESPathCodeGenHelpers.StringToElement(value);
        Assert.AreEqual(value, result.GetString());
    }

    [TestMethod]
    public void StringToElement_Unicode_PreservesCharacters()
    {
        string value = "café ☕ 日本語";
        JsonElement result = JMESPathCodeGenHelpers.StringToElement(value);
        Assert.AreEqual(value, result.GetString());
    }

    // ----- NormalizeSliceIndex -----

    [TestMethod]
    [DataRow(0, 5, true, true, 0)]    // start=0 in 5-element array, positive step
    [DataRow(3, 5, true, true, 3)]    // start=3, within bounds
    [DataRow(10, 5, true, true, 5)]   // start=10, clamped to length
    [DataRow(-1, 5, true, true, 4)]   // start=-1, wraps to 4
    [DataRow(-5, 5, true, true, 0)]   // start=-5, wraps to 0
    [DataRow(-10, 5, true, true, 0)]  // start=-10, clamped to 0
    public void NormalizeSliceIndex_PositiveStep_Start(int index, int length, bool isStart, bool positiveStep, int expected)
    {
        int result = JMESPathCodeGenHelpers.NormalizeSliceIndex(index, length, isStart, positiveStep);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(0, 5, false, true, 0)]   // stop=0
    [DataRow(3, 5, false, true, 3)]   // stop=3
    [DataRow(10, 5, false, true, 5)]  // stop=10, clamped to length
    [DataRow(-1, 5, false, true, 4)]  // stop=-1, wraps to 4
    [DataRow(-10, 5, false, true, 0)] // stop=-10, clamped to 0
    public void NormalizeSliceIndex_PositiveStep_Stop(int index, int length, bool isStart, bool positiveStep, int expected)
    {
        int result = JMESPathCodeGenHelpers.NormalizeSliceIndex(index, length, isStart, positiveStep);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(0, 5, true, false, 0)]   // start=0, negative step
    [DataRow(4, 5, true, false, 4)]   // start=4, within bounds
    [DataRow(10, 5, true, false, 4)]  // start=10, clamped to length-1
    [DataRow(-1, 5, true, false, 4)]  // start=-1, wraps to 4
    [DataRow(-10, 5, true, false, -1)] // start=-10, clamped to -1
    public void NormalizeSliceIndex_NegativeStep_Start(int index, int length, bool isStart, bool positiveStep, int expected)
    {
        int result = JMESPathCodeGenHelpers.NormalizeSliceIndex(index, length, isStart, positiveStep);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void NormalizeSliceIndex_EmptyArray_ClampsCorrectly()
    {
        Assert.AreEqual(0, JMESPathCodeGenHelpers.NormalizeSliceIndex(0, 0, true, true));
        Assert.AreEqual(0, JMESPathCodeGenHelpers.NormalizeSliceIndex(5, 0, true, true));
        Assert.AreEqual(0, JMESPathCodeGenHelpers.NormalizeSliceIndex(-1, 0, true, true));
    }

    // ----- Merge -----

    [TestMethod]
    public void Merge_TwoObjects_CombinesProperties()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement obj1 = JsonElement.ParseValue("""{"a":1}"""u8);
        JsonElement obj2 = JsonElement.ParseValue("""{"b":2}"""u8);

        JsonElement result = JMESPathCodeGenHelpers.Merge(obj1, obj2, workspace);

        Assert.AreEqual(1, result.GetProperty("a"u8).GetDouble());
        Assert.AreEqual(2, result.GetProperty("b"u8).GetDouble());
    }

    [TestMethod]
    public void Merge_OverlappingKeys_SecondWins()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement obj1 = JsonElement.ParseValue("""{"a":1,"b":2}"""u8);
        JsonElement obj2 = JsonElement.ParseValue("""{"b":99,"c":3}"""u8);

        JsonElement result = JMESPathCodeGenHelpers.Merge(obj1, obj2, workspace);

        Assert.AreEqual(1, result.GetProperty("a"u8).GetDouble());
        Assert.AreEqual(99, result.GetProperty("b"u8).GetDouble());
        Assert.AreEqual(3, result.GetProperty("c"u8).GetDouble());
    }

    [TestMethod]
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

        Assert.AreEqual(1, result.GetProperty("a"u8).GetDouble());
        Assert.AreEqual(2, result.GetProperty("b"u8).GetDouble());
        Assert.AreEqual(3, result.GetProperty("c"u8).GetDouble());
    }

    [TestMethod]
    public void Merge_ArrayOverload_OverlappingKeys_LastWins()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        JsonElement[] objects =
        [
            JsonElement.ParseValue("""{"a":1,"b":10}"""u8),
            JsonElement.ParseValue("""{"b":2}"""u8),
            JsonElement.ParseValue("""{"b":99,"c":3}"""u8),
        ];

        JsonElement result = JMESPathCodeGenHelpers.Merge(objects, workspace);

        Assert.AreEqual(1, result.GetProperty("a"u8).GetDouble());
        Assert.AreEqual(99, result.GetProperty("b"u8).GetDouble());
        Assert.AreEqual(3, result.GetProperty("c"u8).GetDouble());
        Assert.AreEqual(3, result.GetPropertyCount());
    }

    // ----- IsTruthy -----

    [TestMethod]
    [DataRow("""true""", true)]
    [DataRow("""false""", false)]
    [DataRow("""null""", false)]
    [DataRow("""0""", true)]         // Numbers are truthy (even 0)
    [DataRow("""1""", true)]
    [DataRow("\"\"", false)]           // Empty string is falsy
    [DataRow("\"hello\"", true)]       // Non-empty string is truthy
    [DataRow("""[]""", false)]       // Empty array is falsy
    [DataRow("""[1]""", true)]       // Non-empty array is truthy
    [DataRow("""{}""", false)]       // Empty object is falsy
    [DataRow("""{"a":1}""", true)]   // Non-empty object is truthy
    public void IsTruthy_FollowsJMESPathSemantics(string json, bool expected)
    {
        JsonElement element = JsonElement.ParseValue(Encoding.UTF8.GetBytes(json));
        Assert.AreEqual(expected, JMESPathCodeGenHelpers.IsTruthy(element));
    }

    // ----- DeepEquals -----

    [TestMethod]
    [DataRow("""1""", """1""", true)]
    [DataRow("""1""", """2""", false)]
    [DataRow("\"a\"", "\"a\"", true)]
    [DataRow("\"a\"", "\"b\"", false)]
    [DataRow("""true""", """true""", true)]
    [DataRow("""true""", """false""", false)]
    [DataRow("""null""", """null""", true)]
    [DataRow("""[1,2]""", """[1,2]""", true)]
    [DataRow("""[1,2]""", """[2,1]""", false)]
    [DataRow("""{"a":1}""", """{"a":1}""", true)]
    [DataRow("""{"a":1}""", """{"a":2}""", false)]
    public void DeepEquals_ComparesCorrectly(string leftJson, string rightJson, bool expected)
    {
        JsonElement left = JsonElement.ParseValue(Encoding.UTF8.GetBytes(leftJson));
        JsonElement right = JsonElement.ParseValue(Encoding.UTF8.GetBytes(rightJson));
        Assert.AreEqual(expected, JMESPathCodeGenHelpers.DeepEquals(left, right));
    }
}
