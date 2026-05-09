// <copyright file="ImmutableListHelpersTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600

using System.Collections.Immutable;
using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Internal;

namespace Corvus.Json.Specs.Tests.CoverageGap;

[TestClass]
public class ImmutableListHelpersTests
{
    private static JsonElement ParseArray(string json)
    {
        return JsonDocument.Parse(json).RootElement;
    }

    [TestMethod]
    public void GetImmutableListWithout_RemovesFirst()
    {
        JsonElement arr = ParseArray("[1,2,3]");
        JsonAny item = JsonAny.Parse("2");
        ImmutableList<JsonAny> result = JsonValueHelpers.GetImmutableListFromJsonElementWithout(arr, item);
        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void GetImmutableListWithout_RemovesFirstOnly()
    {
        JsonElement arr = ParseArray("[1,2,2,3]");
        JsonAny item = JsonAny.Parse("2");
        ImmutableList<JsonAny> result = JsonValueHelpers.GetImmutableListFromJsonElementWithout(arr, item);

        // Should remove only the first 2
        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public void GetImmutableListWithout_ItemNotFound_KeepsAll()
    {
        JsonElement arr = ParseArray("[1,2,3]");
        JsonAny item = JsonAny.Parse("99");
        ImmutableList<JsonAny> result = JsonValueHelpers.GetImmutableListFromJsonElementWithout(arr, item);
        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public void GetImmutableListWithout_RemovesLast()
    {
        JsonElement arr = ParseArray("[1,2,3]");
        JsonAny item = JsonAny.Parse("3");
        ImmutableList<JsonAny> result = JsonValueHelpers.GetImmutableListFromJsonElementWithout(arr, item);
        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void GetImmutableListReplacing_ReplacesItem()
    {
        JsonElement arr = ParseArray("[1,2,3]");
        ImmutableList<JsonAny> result = JsonValueHelpers.GetImmutableListFromJsonElementReplacing(arr, JsonAny.Parse("2"), JsonAny.Parse("99"));
        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public void GetImmutableListReplacing_ItemNotFound_Appends()
    {
        JsonElement arr = ParseArray("[1,2,3]");
        ImmutableList<JsonAny> result = JsonValueHelpers.GetImmutableListFromJsonElementReplacing(arr, JsonAny.Parse("42"), JsonAny.Parse("99"));

        // Old item not found, new item appended
        Assert.AreEqual(4, result.Count);
    }

    [TestMethod]
    public void GetImmutableListWithoutRange_RemoveFromStart()
    {
        JsonElement arr = ParseArray("[1,2,3,4]");
        ImmutableList<JsonAny> result = JsonValueHelpers.GetImmutableListFromJsonElementWithoutRange(arr, 0, 2);
        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void GetImmutableListWithoutRange_RemoveFromMiddle()
    {
        JsonElement arr = ParseArray("[1,2,3,4]");
        ImmutableList<JsonAny> result = JsonValueHelpers.GetImmutableListFromJsonElementWithoutRange(arr, 1, 2);
        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void GetImmutableListWithoutRange_RemoveFromEnd()
    {
        JsonElement arr = ParseArray("[1,2,3,4]");
        ImmutableList<JsonAny> result = JsonValueHelpers.GetImmutableListFromJsonElementWithoutRange(arr, 3, 1);
        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public void GetImmutableListWithoutRange_NegativeIndex_Throws()
    {
        JsonElement arr = ParseArray("[1,2,3]");
        Assert.ThrowsExactly<IndexOutOfRangeException>(
            () => JsonValueHelpers.GetImmutableListFromJsonElementWithoutRange(arr, -1, 1));
    }

    [TestMethod]
    public void GetImmutableListWithoutRange_ZeroCount_Throws()
    {
        JsonElement arr = ParseArray("[1,2,3]");
        Assert.ThrowsExactly<IndexOutOfRangeException>(
            () => JsonValueHelpers.GetImmutableListFromJsonElementWithoutRange(arr, 0, 0));
    }

    [TestMethod]
    public void GetImmutableListWithoutRange_PastEnd_Throws()
    {
        JsonElement arr = ParseArray("[1,2,3]");
        Assert.ThrowsExactly<IndexOutOfRangeException>(
            () => JsonValueHelpers.GetImmutableListFromJsonElementWithoutRange(arr, 2, 2));
    }

    [TestMethod]
    public void GetImmutableListSetting_SetFirst()
    {
        JsonElement arr = ParseArray("[1,2,3]");
        ImmutableList<JsonAny> result = JsonValueHelpers.GetImmutableListFromJsonElementSetting(arr, 0, JsonAny.Parse("99"));
        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public void GetImmutableListSetting_SetMiddle()
    {
        JsonElement arr = ParseArray("[1,2,3]");
        ImmutableList<JsonAny> result = JsonValueHelpers.GetImmutableListFromJsonElementSetting(arr, 1, JsonAny.Parse("99"));
        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public void GetImmutableListSetting_SetLast()
    {
        JsonElement arr = ParseArray("[1,2,3]");
        ImmutableList<JsonAny> result = JsonValueHelpers.GetImmutableListFromJsonElementSetting(arr, 2, JsonAny.Parse("99"));
        Assert.AreEqual(3, result.Count);
    }

    [TestMethod]
    public void GetImmutableListSetting_NegativeIndex_Throws()
    {
        JsonElement arr = ParseArray("[1,2,3]");
        Assert.ThrowsExactly<IndexOutOfRangeException>(
            () => JsonValueHelpers.GetImmutableListFromJsonElementSetting(arr, -1, JsonAny.Parse("99")));
    }

    [TestMethod]
    public void GetImmutableListSetting_PastEnd_Throws()
    {
        JsonElement arr = ParseArray("[1,2,3]");
        Assert.ThrowsExactly<IndexOutOfRangeException>(
            () => JsonValueHelpers.GetImmutableListFromJsonElementSetting(arr, 3, JsonAny.Parse("99")));
    }

    [TestMethod]
    public void GetImmutableListWith_InsertAtStart()
    {
        JsonElement arr = ParseArray("[1,2,3]");
        ImmutableList<JsonAny> result = JsonValueHelpers.GetImmutableListFromJsonElementWith(arr, 0, JsonAny.Parse("0"));
        Assert.AreEqual(4, result.Count);
    }

    [TestMethod]
    public void GetImmutableListWith_InsertAtMiddle()
    {
        JsonElement arr = ParseArray("[1,2,3]");
        ImmutableList<JsonAny> result = JsonValueHelpers.GetImmutableListFromJsonElementWith(arr, 1, JsonAny.Parse("99"));
        Assert.AreEqual(4, result.Count);
    }

    [TestMethod]
    public void GetImmutableListWith_InsertAtEnd()
    {
        JsonElement arr = ParseArray("[1,2,3]");
        ImmutableList<JsonAny> result = JsonValueHelpers.GetImmutableListFromJsonElementWith(arr, 3, JsonAny.Parse("4"));
        Assert.AreEqual(4, result.Count);
    }

    [TestMethod]
    public void GetImmutableListWith_NegativeIndex_Throws()
    {
        JsonElement arr = ParseArray("[1,2,3]");
        Assert.ThrowsExactly<IndexOutOfRangeException>(
            () => JsonValueHelpers.GetImmutableListFromJsonElementWith(arr, -1, JsonAny.Parse("99")));
    }

    [TestMethod]
    public void GetImmutableListWith_PastEnd_Throws()
    {
        JsonElement arr = ParseArray("[1,2,3]");
        Assert.ThrowsExactly<IndexOutOfRangeException>(
            () => JsonValueHelpers.GetImmutableListFromJsonElementWith(arr, 4, JsonAny.Parse("99")));
    }

    [TestMethod]
    public void GetImmutableListWithRange_InsertAtStart()
    {
        JsonElement arr = ParseArray("[1,2,3]");
        JsonAny[] values = [JsonAny.Parse("10"), JsonAny.Parse("20")];
        ImmutableList<JsonAny> result = JsonValueHelpers.GetImmutableListFromJsonElementWith(arr, 0, values);
        Assert.AreEqual(5, result.Count);
    }

    [TestMethod]
    public void GetImmutableListWithRange_InsertAtMiddle()
    {
        JsonElement arr = ParseArray("[1,2,3]");
        JsonAny[] values = [JsonAny.Parse("10"), JsonAny.Parse("20")];
        ImmutableList<JsonAny> result = JsonValueHelpers.GetImmutableListFromJsonElementWith(arr, 1, values);
        Assert.AreEqual(5, result.Count);
    }

    [TestMethod]
    public void GetImmutableListWithRange_InsertAtEnd()
    {
        JsonElement arr = ParseArray("[1,2,3]");
        JsonAny[] values = [JsonAny.Parse("10"), JsonAny.Parse("20")];
        ImmutableList<JsonAny> result = JsonValueHelpers.GetImmutableListFromJsonElementWith(arr, 3, values);
        Assert.AreEqual(5, result.Count);
    }

    [TestMethod]
    public void GetImmutableListWithRange_NegativeIndex_Throws()
    {
        JsonElement arr = ParseArray("[1,2,3]");
        JsonAny[] values = [JsonAny.Parse("10")];
        Assert.ThrowsExactly<IndexOutOfRangeException>(
            () => JsonValueHelpers.GetImmutableListFromJsonElementWith(arr, -1, values));
    }

    [TestMethod]
    public void GetImmutableListWithRange_PastEnd_Throws()
    {
        JsonElement arr = ParseArray("[1,2,3]");
        JsonAny[] values = [JsonAny.Parse("10")];
        Assert.ThrowsExactly<IndexOutOfRangeException>(
            () => JsonValueHelpers.GetImmutableListFromJsonElementWith(arr, 4, values));
    }
}