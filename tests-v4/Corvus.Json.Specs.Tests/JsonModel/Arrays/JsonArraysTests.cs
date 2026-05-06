// <copyright file="JsonArraysTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using Corvus.Json;
using Xunit;

namespace Corvus.Json.Specs.Tests.JsonModel.Arrays;

public class JsonArraysTests
{
    [Fact]
    public void Remove_items_from_a_JsonElement_backed_JsonArray()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonArray result = sut.RemoveAt(0);
        Assert.Equal(JsonArray.ParseValue("[\"bar\", 3]"u8), result);
    }

    [Fact]
    public void Remove_items_from_a_JsonElement_backed_JsonArray_2()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonAny result = sut.AsArray.RemoveAt(0).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"bar\", 3]"u8), result);
    }

    [Fact]
    public void Remove_items_from_a_JsonElement_backed_JsonArray_3()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonNotAny result = sut.AsArray.RemoveAt(0).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"bar\", 3]"u8), result);
    }

    [Fact]
    public void Remove_items_from_a_JsonElement_backed_JsonArray_4()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonArray result = sut.RemoveAt(1);
        Assert.Equal(JsonArray.ParseValue("[\"foo\", 3]"u8), result);
    }

    [Fact]
    public void Remove_items_from_a_JsonElement_backed_JsonArray_5()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonAny result = sut.AsArray.RemoveAt(1).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", 3]"u8), result);
    }

    [Fact]
    public void Remove_items_from_a_JsonElement_backed_JsonArray_6()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonNotAny result = sut.AsArray.RemoveAt(1).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"foo\", 3]"u8), result);
    }

    [Fact]
    public void Remove_items_from_a_JsonElement_backed_JsonArray_7()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonArray result = sut.RemoveAt(2);
        Assert.Equal(JsonArray.ParseValue("[\"foo\", \"bar\"]"u8), result);
    }

    [Fact]
    public void Remove_items_from_a_JsonElement_backed_JsonArray_8()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonAny result = sut.AsArray.RemoveAt(2).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", \"bar\"]"u8), result);
    }

    [Fact]
    public void Remove_items_from_a_JsonElement_backed_JsonArray_9()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonNotAny result = sut.AsArray.RemoveAt(2).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"foo\", \"bar\"]"u8), result);
    }

    [Fact]
    public void Add_items_to_a_JsonElement_backed_JsonArray()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonArray result = sut.Add(JsonAny.ParseValue("\"baz\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"foo\", \"bar\", 3, \"baz\"]"u8), result);
    }

    [Fact]
    public void Add_items_to_a_JsonElement_backed_JsonArray_2()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonAny result = sut.AsArray.Add(JsonAny.ParseValue("\"baz\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", \"bar\", 3, \"baz\"]"u8), result);
    }

    [Fact]
    public void Add_items_to_a_JsonElement_backed_JsonArray_3()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonNotAny result = sut.AsArray.Add(JsonNotAny.ParseValue("\"baz\""u8)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"foo\", \"bar\", 3, \"baz\"]"u8), result);
    }

    [Fact]
    public void Add_two_items_to_a_JsonElement_backed_JsonArray()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonArray result = sut.Add(JsonAny.ParseValue("\"baz\""u8), JsonAny.ParseValue("\"fip\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"foo\", \"bar\", 3, \"baz\", \"fip\"]"u8), result);
    }

    [Fact]
    public void Add_two_items_to_a_JsonElement_backed_JsonArray_2()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonAny result = sut.AsArray.Add(JsonAny.ParseValue("\"baz\""u8), JsonAny.ParseValue("\"fip\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", \"bar\", 3, \"baz\", \"fip\"]"u8), result);
    }

    [Fact]
    public void Add_two_items_to_a_JsonElement_backed_JsonArray_3()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonNotAny result = sut.AsArray.Add(JsonNotAny.ParseValue("\"baz\""u8), JsonNotAny.ParseValue("\"fip\""u8)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"foo\", \"bar\", 3, \"baz\", \"fip\"]"u8), result);
    }

    [Fact]
    public void Add_three_items_to_a_JsonElement_backed_JsonArray()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonArray result = sut.Add(JsonAny.ParseValue("\"baz\""u8), JsonAny.ParseValue("\"fip\""u8), JsonAny.ParseValue("\"zip\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"foo\", \"bar\", 3, \"baz\", \"fip\", \"zip\"]"u8), result);
    }

    [Fact]
    public void Add_three_items_to_a_JsonElement_backed_JsonArray_2()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonAny result = sut.AsArray.Add(JsonAny.ParseValue("\"baz\""u8), JsonAny.ParseValue("\"fip\""u8), JsonAny.ParseValue("\"zip\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", \"bar\", 3, \"baz\", \"fip\", \"zip\"]"u8), result);
    }

    [Fact]
    public void Add_three_items_to_a_JsonElement_backed_JsonArray_3()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonNotAny result = sut.AsArray.Add(JsonNotAny.ParseValue("\"baz\""u8), JsonNotAny.ParseValue("\"fip\""u8), JsonNotAny.ParseValue("\"zip\""u8)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"foo\", \"bar\", 3, \"baz\", \"fip\", \"zip\"]"u8), result);
    }

    [Fact]
    public void Add_four_items_to_a_JsonElement_backed_JsonArray()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonArray result = sut.Add(JsonAny.ParseValue("\"baz\""u8), JsonAny.ParseValue("\"fip\""u8), JsonAny.ParseValue("\"zip\""u8), JsonAny.ParseValue("\"bing\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"foo\", \"bar\", 3, \"baz\", \"fip\", \"zip\", \"bing\"]"u8), result);
    }

    [Fact]
    public void Add_four_items_to_a_JsonElement_backed_JsonArray_2()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonAny result = sut.AsArray.Add(JsonAny.ParseValue("\"baz\""u8), JsonAny.ParseValue("\"fip\""u8), JsonAny.ParseValue("\"zip\""u8), JsonAny.ParseValue("\"bing\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", \"bar\", 3, \"baz\", \"fip\", \"zip\", \"bing\"]"u8), result);
    }

    [Fact]
    public void Add_four_items_to_a_JsonElement_backed_JsonArray_3()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonNotAny result = sut.AsArray.Add(JsonNotAny.ParseValue("\"baz\""u8), JsonNotAny.ParseValue("\"fip\""u8), JsonNotAny.ParseValue("\"zip\""u8), JsonNotAny.ParseValue("\"bing\""u8)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"foo\", \"bar\", 3, \"baz\", \"fip\", \"zip\", \"bing\"]"u8), result);
    }

    [Fact]
    public void Add_many_items_to_a_JsonElement_backed_JsonArray()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonArray result = sut.Add(JsonAny.ParseValue("\"baz\""u8), JsonAny.ParseValue("\"fip\""u8), JsonAny.ParseValue("\"zip\""u8), JsonAny.ParseValue("\"bing\""u8), JsonAny.ParseValue("\"wobble\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"foo\", \"bar\", 3, \"baz\", \"fip\", \"zip\", \"bing\", \"wobble\"]"u8), result);
    }

    [Fact]
    public void Add_many_items_to_a_JsonElement_backed_JsonArray_2()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonAny result = sut.AsArray.Add(JsonAny.ParseValue("\"baz\""u8), JsonAny.ParseValue("\"fip\""u8), JsonAny.ParseValue("\"zip\""u8), JsonAny.ParseValue("\"bing\""u8), JsonAny.ParseValue("\"wobble\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", \"bar\", 3, \"baz\", \"fip\", \"zip\", \"bing\", \"wobble\"]"u8), result);
    }

    [Fact]
    public void Add_many_items_to_a_JsonElement_backed_JsonArray_3()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonNotAny result = sut.AsArray.Add(JsonNotAny.ParseValue("\"baz\""u8), JsonNotAny.ParseValue("\"fip\""u8), JsonNotAny.ParseValue("\"zip\""u8), JsonNotAny.ParseValue("\"bing\""u8), JsonNotAny.ParseValue("\"wobble\""u8)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"foo\", \"bar\", 3, \"baz\", \"fip\", \"zip\", \"bing\", \"wobble\"]"u8), result);
    }

    [Fact]
    public void Add_a_range_of_items_to_a_JsonElement_backed_JsonArray()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonArray result = sut.AddRange(JsonArray.ParseValue("[\"baz\", \"fip\", \"zip\", \"bing\", \"wobble\"]"u8).EnumerateArray().Select(x => x.AsAny));
        Assert.Equal(JsonArray.ParseValue("[\"foo\", \"bar\", 3, \"baz\", \"fip\", \"zip\", \"bing\", \"wobble\"]"u8), result);
    }

    [Fact]
    public void Add_a_range_of_items_to_a_JsonElement_backed_JsonArray_2()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonAny result = sut.AsArray.AddRange(JsonArray.ParseValue("[\"baz\", \"fip\", \"zip\", \"bing\", \"wobble\"]"u8).EnumerateArray().Select(x => x.AsAny)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", \"bar\", 3, \"baz\", \"fip\", \"zip\", \"bing\", \"wobble\"]"u8), result);
    }

    [Fact]
    public void Add_a_range_of_items_to_a_JsonElement_backed_JsonArray_3()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonNotAny result = sut.AsArray.AddRange(JsonArray.ParseValue("[\"baz\", \"fip\", \"zip\", \"bing\", \"wobble\"]"u8).EnumerateArray().Select(x => x.AsAny)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"foo\", \"bar\", 3, \"baz\", \"fip\", \"zip\", \"bing\", \"wobble\"]"u8), result);
    }

    [Fact]
    public void Set_items_to_JsonElement_backed_JsonArray()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonArray result = sut.SetItem(0, JsonAny.ParseValue("\"baz\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"baz\", \"bar\", 3]"u8), result);
    }

    [Fact]
    public void Set_items_to_JsonElement_backed_JsonArray_2()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonAny result = sut.AsArray.SetItem(0, JsonAny.ParseValue("\"baz\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"baz\", \"bar\", 3]"u8), result);
    }

    [Fact]
    public void Set_items_to_JsonElement_backed_JsonArray_3()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonNotAny result = sut.AsArray.SetItem(0, JsonAny.ParseValue("\"baz\""u8)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"baz\", \"bar\", 3]"u8), result);
    }

    [Fact]
    public void Set_items_to_JsonElement_backed_JsonArray_4()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonArray result = sut.SetItem(1, JsonAny.ParseValue("\"baz\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"foo\", \"baz\", 3]"u8), result);
    }

    [Fact]
    public void Set_items_to_JsonElement_backed_JsonArray_5()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonAny result = sut.AsArray.SetItem(1, JsonAny.ParseValue("\"baz\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", \"baz\", 3]"u8), result);
    }

    [Fact]
    public void Set_items_to_JsonElement_backed_JsonArray_6()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonNotAny result = sut.AsArray.SetItem(1, JsonAny.ParseValue("\"baz\""u8)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"foo\", \"baz\", 3]"u8), result);
    }

    [Fact]
    public void Set_items_to_JsonElement_backed_JsonArray_7()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonArray result = sut.SetItem(2, JsonAny.ParseValue("\"baz\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"foo\", \"bar\", \"baz\"]"u8), result);
    }

    [Fact]
    public void Set_items_to_JsonElement_backed_JsonArray_8()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonAny result = sut.AsArray.SetItem(2, JsonAny.ParseValue("\"baz\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", \"bar\", \"baz\"]"u8), result);
    }

    [Fact]
    public void Set_items_to_JsonElement_backed_JsonArray_9()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonNotAny result = sut.AsArray.SetItem(2, JsonAny.ParseValue("\"baz\""u8)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"foo\", \"bar\", \"baz\"]"u8), result);
    }

    [Fact]
    public void Remove_items_from_a_JsonElement_backed_JsonArray_where_the_index_is_out_of_range()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8);
        Assert.Throws<IndexOutOfRangeException>(() => { _ = sut.RemoveAt(3); });
    }

    [Fact]
    public void Remove_items_from_a_JsonElement_backed_JsonArray_where_the_index_is_out_of_range_2()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        Assert.Throws<IndexOutOfRangeException>(() => { _ = sut.AsArray.RemoveAt(3).AsAny; });
    }

    [Fact]
    public void Remove_items_from_a_JsonElement_backed_JsonArray_where_the_index_is_out_of_range_3()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        Assert.Throws<IndexOutOfRangeException>(() => { _ = sut.AsArray.RemoveAt(3).As<JsonNotAny>(); });
    }

    [Fact]
    public void Set_items_to_JsonElement_backed_JsonArray_where_the_index_is_out_of_range()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8);
        Assert.Throws<IndexOutOfRangeException>(() => { _ = sut.SetItem(3, JsonAny.ParseValue("\"baz\""u8)); });
    }

    [Fact]
    public void Set_items_to_JsonElement_backed_JsonArray_where_the_index_is_out_of_range_2()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        Assert.Throws<IndexOutOfRangeException>(() => { _ = sut.AsArray.SetItem(3, JsonAny.ParseValue("\"baz\""u8)).AsAny; });
    }

    [Fact]
    public void Set_items_to_JsonElement_backed_JsonArray_where_the_index_is_out_of_range_3()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        Assert.Throws<IndexOutOfRangeException>(() => { _ = sut.AsArray.SetItem(3, JsonAny.ParseValue("\"baz\""u8)).As<JsonNotAny>(); });
    }

    [Fact]
    public void Get_items_from_a_JsonElement_backed_JsonArray()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonString item = sut[0].AsString;
        Assert.Equal(JsonString.ParseValue("\"foo\""u8), item);
    }

    [Fact]
    public void Get_items_from_a_JsonElement_backed_JsonArray_2()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonString item = sut.AsArray[0].AsString;
        Assert.Equal(JsonString.ParseValue("\"foo\""u8), item);
    }

    [Fact]
    public void Get_items_from_a_JsonElement_backed_JsonArray_3()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonString item = sut.AsArray[0].AsString;
        Assert.Equal(JsonString.ParseValue("\"foo\""u8), item);
    }

    [Fact]
    public void Get_items_from_a_JsonElement_backed_JsonArray_4()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonString item = sut[1].AsString;
        Assert.Equal(JsonString.ParseValue("\"bar\""u8), item);
    }

    [Fact]
    public void Get_items_from_a_JsonElement_backed_JsonArray_5()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonString item = sut.AsArray[1].AsString;
        Assert.Equal(JsonString.ParseValue("\"bar\""u8), item);
    }

    [Fact]
    public void Get_items_from_a_JsonElement_backed_JsonArray_6()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonString item = sut.AsArray[1].AsString;
        Assert.Equal(JsonString.ParseValue("\"bar\""u8), item);
    }

    [Fact]
    public void Get_items_from_a_JsonElement_backed_JsonArray_7()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonNumber item = sut[2].AsNumber;
        Assert.Equal(JsonNumber.ParseValue("3"u8), item);
    }

    [Fact]
    public void Get_items_from_a_JsonElement_backed_JsonArray_8()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonNumber item = sut.AsArray[2].AsNumber;
        Assert.Equal(JsonNumber.ParseValue("3"u8), item);
    }

    [Fact]
    public void Get_items_from_a_JsonElement_backed_JsonArray_9()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonNumber item = sut.AsArray[2].AsNumber;
        Assert.Equal(JsonNumber.ParseValue("3"u8), item);
    }

    [Fact]
    public void Get_items_from_a_JsonElement_backed_JsonArray_where_the_index_is_out_of_range()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8);
        Assert.Throws<IndexOutOfRangeException>(() => { _ = sut[3].AsString; });
    }

    [Fact]
    public void Get_items_from_a_JsonElement_backed_JsonArray_where_the_index_is_out_of_range_2()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        Assert.Throws<IndexOutOfRangeException>(() => { _ = sut.AsArray[3].AsString; });
    }

    [Fact]
    public void Get_items_from_a_JsonElement_backed_JsonArray_where_the_index_is_out_of_range_3()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        Assert.Throws<IndexOutOfRangeException>(() => { _ = sut.AsArray[3].AsString; });
    }

    [Fact]
    public void Insert_items_into_JsonElement_backed_JsonArray()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonArray result = sut.Insert(0, JsonAny.ParseValue("\"baz\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"baz\", \"foo\", \"bar\", 3]"u8), result);
    }

    [Fact]
    public void Insert_items_into_JsonElement_backed_JsonArray_2()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonAny result = sut.AsArray.Insert(0, JsonAny.ParseValue("\"baz\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"baz\", \"foo\", \"bar\", 3]"u8), result);
    }

    [Fact]
    public void Insert_items_into_JsonElement_backed_JsonArray_3()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonNotAny result = sut.AsArray.Insert(0, JsonAny.ParseValue("\"baz\""u8)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"baz\", \"foo\", \"bar\", 3]"u8), result);
    }

    [Fact]
    public void Insert_items_into_JsonElement_backed_JsonArray_4()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonArray result = sut.Insert(1, JsonAny.ParseValue("\"baz\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"foo\", \"baz\", \"bar\", 3]"u8), result);
    }

    [Fact]
    public void Insert_items_into_JsonElement_backed_JsonArray_5()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonAny result = sut.AsArray.Insert(1, JsonAny.ParseValue("\"baz\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", \"baz\", \"bar\", 3]"u8), result);
    }

    [Fact]
    public void Insert_items_into_JsonElement_backed_JsonArray_6()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonNotAny result = sut.AsArray.Insert(1, JsonAny.ParseValue("\"baz\""u8)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"foo\", \"baz\", \"bar\", 3]"u8), result);
    }

    [Fact]
    public void Insert_items_into_JsonElement_backed_JsonArray_7()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonArray result = sut.Insert(2, JsonAny.ParseValue("\"baz\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"foo\", \"bar\", \"baz\", 3]"u8), result);
    }

    [Fact]
    public void Insert_items_into_JsonElement_backed_JsonArray_8()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonAny result = sut.AsArray.Insert(2, JsonAny.ParseValue("\"baz\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", \"bar\", \"baz\", 3]"u8), result);
    }

    [Fact]
    public void Insert_items_into_JsonElement_backed_JsonArray_9()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonNotAny result = sut.AsArray.Insert(2, JsonAny.ParseValue("\"baz\""u8)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"foo\", \"bar\", \"baz\", 3]"u8), result);
    }

    [Fact]
    public void Insert_items_into_JsonElement_backed_JsonArray_10()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonArray result = sut.Insert(3, JsonAny.ParseValue("\"baz\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"foo\", \"bar\", 3, \"baz\"]"u8), result);
    }

    [Fact]
    public void Insert_items_into_JsonElement_backed_JsonArray_11()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonAny result = sut.AsArray.Insert(3, JsonAny.ParseValue("\"baz\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", \"bar\", 3, \"baz\"]"u8), result);
    }

    [Fact]
    public void Insert_items_into_JsonElement_backed_JsonArray_12()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonNotAny result = sut.AsArray.Insert(3, JsonAny.ParseValue("\"baz\""u8)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"foo\", \"bar\", 3, \"baz\"]"u8), result);
    }

    [Fact]
    public void Replace_items_in_a_JsonElement_backed_JsonArray()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonArray result = sut.Replace(JsonAny.ParseValue("\"foo\""u8), JsonAny.ParseValue("\"baz\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"baz\", \"bar\", 3]"u8), result);
    }

    [Fact]
    public void Replace_items_in_a_JsonElement_backed_JsonArray_2()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonAny result = sut.AsArray.Replace(JsonAny.ParseValue("\"foo\""u8), JsonAny.ParseValue("\"baz\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"baz\", \"bar\", 3]"u8), result);
    }

    [Fact]
    public void Replace_items_in_a_JsonElement_backed_JsonArray_3()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonNotAny result = sut.AsArray.Replace(JsonAny.ParseValue("\"foo\""u8), JsonAny.ParseValue("\"baz\""u8)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"baz\", \"bar\", 3]"u8), result);
    }

    [Fact]
    public void Replace_items_in_a_JsonElement_backed_JsonArray_4()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonArray result = sut.Replace(JsonAny.ParseValue("\"bar\""u8), JsonAny.ParseValue("\"baz\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"foo\", \"baz\", 3]"u8), result);
    }

    [Fact]
    public void Replace_items_in_a_JsonElement_backed_JsonArray_5()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonAny result = sut.AsArray.Replace(JsonAny.ParseValue("\"bar\""u8), JsonAny.ParseValue("\"baz\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", \"baz\", 3]"u8), result);
    }

    [Fact]
    public void Replace_items_in_a_JsonElement_backed_JsonArray_6()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonNotAny result = sut.AsArray.Replace(JsonAny.ParseValue("\"bar\""u8), JsonAny.ParseValue("\"baz\""u8)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"foo\", \"baz\", 3]"u8), result);
    }

    [Fact]
    public void Replace_items_in_a_JsonElement_backed_JsonArray_7()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonArray result = sut.Replace(JsonAny.ParseValue("3"u8), JsonAny.ParseValue("\"baz\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"foo\", \"bar\", \"baz\"]"u8), result);
    }

    [Fact]
    public void Replace_items_in_a_JsonElement_backed_JsonArray_8()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonAny result = sut.AsArray.Replace(JsonAny.ParseValue("3"u8), JsonAny.ParseValue("\"baz\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", \"bar\", \"baz\"]"u8), result);
    }

    [Fact]
    public void Replace_items_in_a_JsonElement_backed_JsonArray_9()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8);
        JsonNotAny result = sut.AsArray.Replace(JsonAny.ParseValue("3"u8), JsonAny.ParseValue("\"baz\""u8)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"foo\", \"bar\", \"baz\"]"u8), result);
    }

    [Fact]
    public void Remove_items_from_a_dotnet_backed_JsonArray()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonArray result = sut.RemoveAt(0);
        Assert.Equal(JsonArray.ParseValue("[\"bar\", 3]"u8), result);
    }

    [Fact]
    public void Remove_items_from_a_dotnet_backed_JsonArray_2()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonAny result = sut.AsArray.RemoveAt(0).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"bar\", 3]"u8), result);
    }

    [Fact]
    public void Remove_items_from_a_dotnet_backed_JsonArray_3()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonNotAny result = sut.AsArray.RemoveAt(0).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"bar\", 3]"u8), result);
    }

    [Fact]
    public void Remove_items_from_a_dotnet_backed_JsonArray_4()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonArray result = sut.RemoveAt(1);
        Assert.Equal(JsonArray.ParseValue("[\"foo\", 3]"u8), result);
    }

    [Fact]
    public void Remove_items_from_a_dotnet_backed_JsonArray_5()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonAny result = sut.AsArray.RemoveAt(1).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", 3]"u8), result);
    }

    [Fact]
    public void Remove_items_from_a_dotnet_backed_JsonArray_6()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonNotAny result = sut.AsArray.RemoveAt(1).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"foo\", 3]"u8), result);
    }

    [Fact]
    public void Remove_items_from_a_dotnet_backed_JsonArray_7()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonArray result = sut.RemoveAt(2);
        Assert.Equal(JsonArray.ParseValue("[\"foo\", \"bar\"]"u8), result);
    }

    [Fact]
    public void Remove_items_from_a_dotnet_backed_JsonArray_8()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonAny result = sut.AsArray.RemoveAt(2).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", \"bar\"]"u8), result);
    }

    [Fact]
    public void Remove_items_from_a_dotnet_backed_JsonArray_9()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonNotAny result = sut.AsArray.RemoveAt(2).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"foo\", \"bar\"]"u8), result);
    }

    [Fact]
    public void Add_items_to_a_dotnet_backed_JsonArray()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonArray result = sut.Add(JsonAny.ParseValue("\"baz\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"foo\", \"bar\", 3, \"baz\"]"u8), result);
    }

    [Fact]
    public void Add_items_to_a_dotnet_backed_JsonArray_2()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonAny result = sut.AsArray.Add(JsonAny.ParseValue("\"baz\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", \"bar\", 3, \"baz\"]"u8), result);
    }

    [Fact]
    public void Add_items_to_a_dotnet_backed_JsonArray_3()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonNotAny result = sut.AsArray.Add(JsonNotAny.ParseValue("\"baz\""u8)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"foo\", \"bar\", 3, \"baz\"]"u8), result);
    }

    [Fact]
    public void Add_two_items_to_a_dotnet_backed_JsonArray()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonArray result = sut.Add(JsonAny.ParseValue("\"baz\""u8), JsonAny.ParseValue("\"fip\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"foo\", \"bar\", 3, \"baz\", \"fip\"]"u8), result);
    }

    [Fact]
    public void Add_two_items_to_a_dotnet_backed_JsonArray_2()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonAny result = sut.AsArray.Add(JsonAny.ParseValue("\"baz\""u8), JsonAny.ParseValue("\"fip\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", \"bar\", 3, \"baz\", \"fip\"]"u8), result);
    }

    [Fact]
    public void Add_two_items_to_a_dotnet_backed_JsonArray_3()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonNotAny result = sut.AsArray.Add(JsonNotAny.ParseValue("\"baz\""u8), JsonNotAny.ParseValue("\"fip\""u8)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"foo\", \"bar\", 3, \"baz\", \"fip\"]"u8), result);
    }

    [Fact]
    public void Add_three_items_to_a_dotnet_backed_JsonArray()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonArray result = sut.Add(JsonAny.ParseValue("\"baz\""u8), JsonAny.ParseValue("\"fip\""u8), JsonAny.ParseValue("\"zip\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"foo\", \"bar\", 3, \"baz\", \"fip\", \"zip\"]"u8), result);
    }

    [Fact]
    public void Add_three_items_to_a_dotnet_backed_JsonArray_2()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonAny result = sut.AsArray.Add(JsonAny.ParseValue("\"baz\""u8), JsonAny.ParseValue("\"fip\""u8), JsonAny.ParseValue("\"zip\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", \"bar\", 3, \"baz\", \"fip\", \"zip\"]"u8), result);
    }

    [Fact]
    public void Add_four_items_to_a_dotnet_backed_JsonArray()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonArray result = sut.Add(JsonAny.ParseValue("\"baz\""u8), JsonAny.ParseValue("\"fip\""u8), JsonAny.ParseValue("\"zip\""u8), JsonAny.ParseValue("\"bing\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"foo\", \"bar\", 3, \"baz\", \"fip\", \"zip\", \"bing\"]"u8), result);
    }

    [Fact]
    public void Add_four_items_to_a_dotnet_backed_JsonArray_2()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonAny result = sut.AsArray.Add(JsonAny.ParseValue("\"baz\""u8), JsonAny.ParseValue("\"fip\""u8), JsonAny.ParseValue("\"zip\""u8), JsonAny.ParseValue("\"bing\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", \"bar\", 3, \"baz\", \"fip\", \"zip\", \"bing\"]"u8), result);
    }

    [Fact]
    public void Add_four_items_to_a_dotnet_backed_JsonArray_3()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonNotAny result = sut.AsArray.Add(JsonNotAny.ParseValue("\"baz\""u8), JsonNotAny.ParseValue("\"fip\""u8), JsonNotAny.ParseValue("\"zip\""u8), JsonNotAny.ParseValue("\"bing\""u8)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"foo\", \"bar\", 3, \"baz\", \"fip\", \"zip\", \"bing\"]"u8), result);
    }

    [Fact]
    public void Add_many_items_to_a_dotnet_backed_JsonArray()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonArray result = sut.Add(JsonAny.ParseValue("\"baz\""u8), JsonAny.ParseValue("\"fip\""u8), JsonAny.ParseValue("\"zip\""u8), JsonAny.ParseValue("\"bing\""u8), JsonAny.ParseValue("\"wobble\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"foo\", \"bar\", 3, \"baz\", \"fip\", \"zip\", \"bing\", \"wobble\"]"u8), result);
    }

    [Fact]
    public void Add_many_items_to_a_dotnet_backed_JsonArray_2()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonAny result = sut.AsArray.Add(JsonAny.ParseValue("\"baz\""u8), JsonAny.ParseValue("\"fip\""u8), JsonAny.ParseValue("\"zip\""u8), JsonAny.ParseValue("\"bing\""u8), JsonAny.ParseValue("\"wobble\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", \"bar\", 3, \"baz\", \"fip\", \"zip\", \"bing\", \"wobble\"]"u8), result);
    }

    [Fact]
    public void Add_many_items_to_a_dotnet_backed_JsonArray_3()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonNotAny result = sut.AsArray.Add(JsonNotAny.ParseValue("\"baz\""u8), JsonNotAny.ParseValue("\"fip\""u8), JsonNotAny.ParseValue("\"zip\""u8), JsonNotAny.ParseValue("\"bing\""u8), JsonNotAny.ParseValue("\"wobble\""u8)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"foo\", \"bar\", 3, \"baz\", \"fip\", \"zip\", \"bing\", \"wobble\"]"u8), result);
    }

    [Fact]
    public void Add_a_range_of_items_to_a_dotnet_backed_JsonArray()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonArray result = sut.AddRange(JsonArray.ParseValue("[\"baz\", \"fip\", \"zip\", \"bing\", \"wobble\"]"u8).EnumerateArray().Select(x => x.AsAny));
        Assert.Equal(JsonArray.ParseValue("[\"foo\", \"bar\", 3, \"baz\", \"fip\", \"zip\", \"bing\", \"wobble\"]"u8), result);
    }

    [Fact]
    public void Add_a_range_of_items_to_a_dotnet_backed_JsonArray_2()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonAny result = sut.AsArray.AddRange(JsonArray.ParseValue("[\"baz\", \"fip\", \"zip\", \"bing\", \"wobble\"]"u8).EnumerateArray().Select(x => x.AsAny)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", \"bar\", 3, \"baz\", \"fip\", \"zip\", \"bing\", \"wobble\"]"u8), result);
    }

    [Fact]
    public void Add_a_range_of_items_to_a_dotnet_backed_JsonArray_3()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonNotAny result = sut.AsArray.AddRange(JsonArray.ParseValue("[\"baz\", \"fip\", \"zip\", \"bing\", \"wobble\"]"u8).EnumerateArray().Select(x => x.AsAny)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"foo\", \"bar\", 3, \"baz\", \"fip\", \"zip\", \"bing\", \"wobble\"]"u8), result);
    }

    [Fact]
    public void Set_items_to_dotnet_backed_JsonArray()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonArray result = sut.SetItem(0, JsonAny.ParseValue("\"baz\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"baz\", \"bar\", 3]"u8), result);
    }

    [Fact]
    public void Set_items_to_dotnet_backed_JsonArray_2()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonAny result = sut.AsArray.SetItem(0, JsonAny.ParseValue("\"baz\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"baz\", \"bar\", 3]"u8), result);
    }

    [Fact]
    public void Set_items_to_dotnet_backed_JsonArray_3()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonNotAny result = sut.AsArray.SetItem(0, JsonAny.ParseValue("\"baz\""u8)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"baz\", \"bar\", 3]"u8), result);
    }

    [Fact]
    public void Set_items_to_dotnet_backed_JsonArray_4()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonArray result = sut.SetItem(1, JsonAny.ParseValue("\"baz\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"foo\", \"baz\", 3]"u8), result);
    }

    [Fact]
    public void Set_items_to_dotnet_backed_JsonArray_5()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonAny result = sut.AsArray.SetItem(1, JsonAny.ParseValue("\"baz\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", \"baz\", 3]"u8), result);
    }

    [Fact]
    public void Set_items_to_dotnet_backed_JsonArray_6()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonNotAny result = sut.AsArray.SetItem(1, JsonAny.ParseValue("\"baz\""u8)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"foo\", \"baz\", 3]"u8), result);
    }

    [Fact]
    public void Set_items_to_dotnet_backed_JsonArray_7()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonArray result = sut.SetItem(2, JsonAny.ParseValue("\"baz\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"foo\", \"bar\", \"baz\"]"u8), result);
    }

    [Fact]
    public void Set_items_to_dotnet_backed_JsonArray_8()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonAny result = sut.AsArray.SetItem(2, JsonAny.ParseValue("\"baz\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", \"bar\", \"baz\"]"u8), result);
    }

    [Fact]
    public void Set_items_to_dotnet_backed_JsonArray_9()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonNotAny result = sut.AsArray.SetItem(2, JsonAny.ParseValue("\"baz\""u8)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"foo\", \"bar\", \"baz\"]"u8), result);
    }

    [Fact]
    public void Remove_items_from_a_dotnet_backed_JsonArray_where_the_index_is_out_of_range()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        Assert.Throws<IndexOutOfRangeException>(() => { _ = sut.RemoveAt(3); });
    }

    [Fact]
    public void Remove_items_from_a_dotnet_backed_JsonArray_where_the_index_is_out_of_range_2()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        Assert.Throws<IndexOutOfRangeException>(() => { _ = sut.AsArray.RemoveAt(3).AsAny; });
    }

    [Fact]
    public void Remove_items_from_a_dotnet_backed_JsonArray_where_the_index_is_out_of_range_3()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        Assert.Throws<IndexOutOfRangeException>(() => { _ = sut.AsArray.RemoveAt(3).As<JsonNotAny>(); });
    }

    [Fact]
    public void Set_items_to_dotnet_backed_JsonArray_where_the_index_is_out_of_range()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        Assert.Throws<IndexOutOfRangeException>(() => { _ = sut.SetItem(3, JsonAny.ParseValue("\"baz\""u8)); });
    }

    [Fact]
    public void Set_items_to_dotnet_backed_JsonArray_where_the_index_is_out_of_range_2()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        Assert.Throws<IndexOutOfRangeException>(() => { _ = sut.AsArray.SetItem(3, JsonAny.ParseValue("\"baz\""u8)).AsAny; });
    }

    [Fact]
    public void Set_items_to_dotnet_backed_JsonArray_where_the_index_is_out_of_range_3()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        Assert.Throws<IndexOutOfRangeException>(() => { _ = sut.AsArray.SetItem(3, JsonAny.ParseValue("\"baz\""u8)).As<JsonNotAny>(); });
    }

    [Fact]
    public void Get_items_from_a_dotnet_backed_JsonArray()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonString item = sut[0].AsString;
        Assert.Equal(JsonString.ParseValue("\"foo\""u8), item);
    }

    [Fact]
    public void Get_items_from_a_dotnet_backed_JsonArray_2()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonString item = sut.AsArray[0].AsString;
        Assert.Equal(JsonString.ParseValue("\"foo\""u8), item);
    }

    [Fact]
    public void Get_items_from_a_dotnet_backed_JsonArray_3()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonString item = sut.AsArray[0].AsString;
        Assert.Equal(JsonString.ParseValue("\"foo\""u8), item);
    }

    [Fact]
    public void Get_items_from_a_dotnet_backed_JsonArray_4()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonString item = sut[1].AsString;
        Assert.Equal(JsonString.ParseValue("\"bar\""u8), item);
    }

    [Fact]
    public void Get_items_from_a_dotnet_backed_JsonArray_5()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonString item = sut.AsArray[1].AsString;
        Assert.Equal(JsonString.ParseValue("\"bar\""u8), item);
    }

    [Fact]
    public void Get_items_from_a_dotnet_backed_JsonArray_6()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonString item = sut.AsArray[1].AsString;
        Assert.Equal(JsonString.ParseValue("\"bar\""u8), item);
    }

    [Fact]
    public void Get_items_from_a_dotnet_backed_JsonArray_7()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonNumber item = sut[2].AsNumber;
        Assert.Equal(JsonNumber.ParseValue("3"u8), item);
    }

    [Fact]
    public void Get_items_from_a_dotnet_backed_JsonArray_8()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonNumber item = sut.AsArray[2].AsNumber;
        Assert.Equal(JsonNumber.ParseValue("3"u8), item);
    }

    [Fact]
    public void Get_items_from_a_dotnet_backed_JsonArray_9()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonNumber item = sut.AsArray[2].AsNumber;
        Assert.Equal(JsonNumber.ParseValue("3"u8), item);
    }

    [Fact]
    public void Get_items_from_a_dotnet_backed_JsonArray_where_the_index_is_out_of_range()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        Assert.Throws<IndexOutOfRangeException>(() => { _ = sut[3].AsString; });
    }

    [Fact]
    public void Get_items_from_a_dotnet_backed_JsonArray_where_the_index_is_out_of_range_2()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        Assert.Throws<IndexOutOfRangeException>(() => { _ = sut.AsArray[3].AsString; });
    }

    [Fact]
    public void Get_items_from_a_dotnet_backed_JsonArray_where_the_index_is_out_of_range_3()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        Assert.Throws<IndexOutOfRangeException>(() => { _ = sut.AsArray[3].AsString; });
    }

    [Fact]
    public void Insert_items_into_dotnet_backed_JsonArray()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonArray result = sut.Insert(0, JsonAny.ParseValue("\"baz\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"baz\", \"foo\", \"bar\", 3]"u8), result);
    }

    [Fact]
    public void Insert_items_into_dotnet_backed_JsonArray_2()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonAny result = sut.AsArray.Insert(0, JsonAny.ParseValue("\"baz\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"baz\", \"foo\", \"bar\", 3]"u8), result);
    }

    [Fact]
    public void Insert_items_into_dotnet_backed_JsonArray_3()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonNotAny result = sut.AsArray.Insert(0, JsonAny.ParseValue("\"baz\""u8)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"baz\", \"foo\", \"bar\", 3]"u8), result);
    }

    [Fact]
    public void Insert_items_into_dotnet_backed_JsonArray_4()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonArray result = sut.Insert(1, JsonAny.ParseValue("\"baz\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"foo\", \"baz\", \"bar\", 3]"u8), result);
    }

    [Fact]
    public void Insert_items_into_dotnet_backed_JsonArray_5()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonAny result = sut.AsArray.Insert(1, JsonAny.ParseValue("\"baz\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", \"baz\", \"bar\", 3]"u8), result);
    }

    [Fact]
    public void Insert_items_into_dotnet_backed_JsonArray_6()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonNotAny result = sut.AsArray.Insert(1, JsonAny.ParseValue("\"baz\""u8)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"foo\", \"baz\", \"bar\", 3]"u8), result);
    }

    [Fact]
    public void Insert_items_into_dotnet_backed_JsonArray_7()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonArray result = sut.Insert(2, JsonAny.ParseValue("\"baz\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"foo\", \"bar\", \"baz\", 3]"u8), result);
    }

    [Fact]
    public void Insert_items_into_dotnet_backed_JsonArray_8()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonAny result = sut.AsArray.Insert(2, JsonAny.ParseValue("\"baz\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", \"bar\", \"baz\", 3]"u8), result);
    }

    [Fact]
    public void Insert_items_into_dotnet_backed_JsonArray_9()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonNotAny result = sut.AsArray.Insert(2, JsonAny.ParseValue("\"baz\""u8)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"foo\", \"bar\", \"baz\", 3]"u8), result);
    }

    [Fact]
    public void Insert_items_into_dotnet_backed_JsonArray_10()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonArray result = sut.Insert(3, JsonAny.ParseValue("\"baz\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"foo\", \"bar\", 3, \"baz\"]"u8), result);
    }

    [Fact]
    public void Insert_items_into_dotnet_backed_JsonArray_11()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonAny result = sut.AsArray.Insert(3, JsonAny.ParseValue("\"baz\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", \"bar\", 3, \"baz\"]"u8), result);
    }

    [Fact]
    public void Insert_items_into_dotnet_backed_JsonArray_12()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonNotAny result = sut.AsArray.Insert(3, JsonAny.ParseValue("\"baz\""u8)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"foo\", \"bar\", 3, \"baz\"]"u8), result);
    }

    [Fact]
    public void Replace_items_in_a_dotnet_backed_JsonArray()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonArray result = sut.Replace(JsonAny.ParseValue("\"foo\""u8), JsonAny.ParseValue("\"baz\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"baz\", \"bar\", 3]"u8), result);
    }

    [Fact]
    public void Replace_items_in_a_dotnet_backed_JsonArray_2()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonAny result = sut.AsArray.Replace(JsonAny.ParseValue("\"foo\""u8), JsonAny.ParseValue("\"baz\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"baz\", \"bar\", 3]"u8), result);
    }

    [Fact]
    public void Replace_items_in_a_dotnet_backed_JsonArray_3()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonNotAny result = sut.AsArray.Replace(JsonAny.ParseValue("\"foo\""u8), JsonAny.ParseValue("\"baz\""u8)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"baz\", \"bar\", 3]"u8), result);
    }

    [Fact]
    public void Replace_items_in_a_dotnet_backed_JsonArray_4()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonArray result = sut.Replace(JsonAny.ParseValue("\"bar\""u8), JsonAny.ParseValue("\"baz\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"foo\", \"baz\", 3]"u8), result);
    }

    [Fact]
    public void Replace_items_in_a_dotnet_backed_JsonArray_5()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonAny result = sut.AsArray.Replace(JsonAny.ParseValue("\"bar\""u8), JsonAny.ParseValue("\"baz\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", \"baz\", 3]"u8), result);
    }

    [Fact]
    public void Replace_items_in_a_dotnet_backed_JsonArray_6()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonNotAny result = sut.AsArray.Replace(JsonAny.ParseValue("\"bar\""u8), JsonAny.ParseValue("\"baz\""u8)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"foo\", \"baz\", 3]"u8), result);
    }

    [Fact]
    public void Replace_items_in_a_dotnet_backed_JsonArray_7()
    {
        JsonArray sut = JsonArray.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonArray result = sut.Replace(JsonAny.ParseValue("3"u8), JsonAny.ParseValue("\"baz\""u8));
        Assert.Equal(JsonArray.ParseValue("[\"foo\", \"bar\", \"baz\"]"u8), result);
    }

    [Fact]
    public void Replace_items_in_a_dotnet_backed_JsonArray_8()
    {
        JsonAny sut = JsonAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonAny result = sut.AsArray.Replace(JsonAny.ParseValue("3"u8), JsonAny.ParseValue("\"baz\""u8)).AsAny;
        Assert.Equal(JsonAny.ParseValue("[\"foo\", \"bar\", \"baz\"]"u8), result);
    }

    [Fact]
    public void Replace_items_in_a_dotnet_backed_JsonArray_9()
    {
        JsonNotAny sut = JsonNotAny.ParseValue("[\"foo\", \"bar\", 3]"u8).AsDotnetBackedValue();
        JsonNotAny result = sut.AsArray.Replace(JsonAny.ParseValue("3"u8), JsonAny.ParseValue("\"baz\""u8)).As<JsonNotAny>();
        Assert.Equal(JsonNotAny.ParseValue("[\"foo\", \"bar\", \"baz\"]"u8), result);
    }
}