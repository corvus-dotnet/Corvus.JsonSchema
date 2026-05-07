// <copyright file="JsonPropertiesTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using System.Text;
using Corvus.Json;
using Xunit;

namespace Corvus.Json.Specs.Tests.JsonModel.Properties;

public class JsonPropertiesTests
{
    [Fact]
    public void Remove_JsonElementBacked_JsonObject_String_FromObject()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "bar"}""");
        JsonObject result = sut.RemoveProperty("foo");
        Assert.False(result.HasProperty("foo"));
    }

    [Fact]
    public void Remove_JsonElementBacked_JsonObject_String_FromUndefined_ThrowsInvalidOperationException()
    {
        JsonObject sut = default(JsonObject);
        Assert.Throws<InvalidOperationException>(() => { sut.RemoveProperty("foo"); });
    }

    [Fact]
    public void Remove_JsonElementBacked_JsonObject_String_FromNumber_ThrowsInvalidOperationException()
    {
        JsonObject sut = JsonObject.Parse("1.2");
        Assert.Throws<InvalidOperationException>(() => { sut.RemoveProperty("foo"); });
    }

    [Fact]
    public void Remove_JsonElementBacked_JsonObject_SpanChar_FromObject()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "bar"}""");
        JsonObject result = sut.RemoveProperty("foo".AsSpan());
        Assert.False(result.HasProperty("foo".AsSpan()));
    }

    [Fact]
    public void Remove_JsonElementBacked_JsonObject_SpanChar_FromUndefined_ThrowsInvalidOperationException()
    {
        JsonObject sut = default(JsonObject);
        Assert.Throws<InvalidOperationException>(() => { sut.RemoveProperty("foo".AsSpan()); });
    }

    [Fact]
    public void Remove_JsonElementBacked_JsonObject_SpanChar_FromNumber_ThrowsInvalidOperationException()
    {
        JsonObject sut = JsonObject.Parse("1.2");
        Assert.Throws<InvalidOperationException>(() => { sut.RemoveProperty("foo".AsSpan()); });
    }

    [Fact]
    public void Remove_JsonElementBacked_JsonObject_SpanByte_FromObject()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "bar"}""");
        JsonObject result = sut.RemoveProperty(Encoding.UTF8.GetBytes("foo"));
        Assert.False(result.HasProperty(Encoding.UTF8.GetBytes("foo")));
    }

    [Fact]
    public void Remove_JsonElementBacked_JsonObject_SpanByte_FromUndefined_ThrowsInvalidOperationException()
    {
        JsonObject sut = default(JsonObject);
        Assert.Throws<InvalidOperationException>(() => { sut.RemoveProperty(Encoding.UTF8.GetBytes("foo")); });
    }

    [Fact]
    public void Remove_JsonElementBacked_JsonObject_SpanByte_FromNumber_ThrowsInvalidOperationException()
    {
        JsonObject sut = JsonObject.Parse("1.2");
        Assert.Throws<InvalidOperationException>(() => { sut.RemoveProperty(Encoding.UTF8.GetBytes("foo")); });
    }

    [Fact]
    public void Remove_JsonElementBacked_JsonAny_String_FromObject()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "bar"}""");
        JsonAny result = sut.AsObject.RemoveProperty("foo").AsAny;
        Assert.False(result.AsObject.HasProperty("foo"));
    }

    [Fact]
    public void Remove_JsonElementBacked_JsonAny_String_FromUndefined_ThrowsInvalidOperationException()
    {
        JsonAny sut = JsonAny.Undefined;
        Assert.Throws<InvalidOperationException>(() => { _ = sut.AsObject.RemoveProperty("foo").AsAny; });
    }

    [Fact]
    public void Remove_JsonElementBacked_JsonAny_String_FromNumber_ThrowsInvalidOperationException()
    {
        JsonAny sut = JsonAny.Parse("1.2");
        Assert.Throws<InvalidOperationException>(() => { _ = sut.AsObject.RemoveProperty("foo").AsAny; });
    }

    [Fact]
    public void Remove_JsonElementBacked_JsonAny_SpanChar_FromObject()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "bar"}""");
        JsonAny result = sut.AsObject.RemoveProperty("foo".AsSpan()).AsAny;
        Assert.False(result.AsObject.HasProperty("foo".AsSpan()));
    }

    [Fact]
    public void Remove_JsonElementBacked_JsonAny_SpanChar_FromUndefined_ThrowsInvalidOperationException()
    {
        JsonAny sut = JsonAny.Undefined;
        Assert.Throws<InvalidOperationException>(() => { _ = sut.AsObject.RemoveProperty("foo".AsSpan()).AsAny; });
    }

    [Fact]
    public void Remove_JsonElementBacked_JsonAny_SpanChar_FromNumber_ThrowsInvalidOperationException()
    {
        JsonAny sut = JsonAny.Parse("1.2");
        Assert.Throws<InvalidOperationException>(() => { _ = sut.AsObject.RemoveProperty("foo".AsSpan()).AsAny; });
    }

    [Fact]
    public void Remove_JsonElementBacked_JsonAny_SpanByte_FromObject()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "bar"}""");
        JsonAny result = sut.AsObject.RemoveProperty(Encoding.UTF8.GetBytes("foo")).AsAny;
        Assert.False(result.AsObject.HasProperty(Encoding.UTF8.GetBytes("foo")));
    }

    [Fact]
    public void Remove_JsonElementBacked_JsonAny_SpanByte_FromUndefined_ThrowsInvalidOperationException()
    {
        JsonAny sut = JsonAny.Undefined;
        Assert.Throws<InvalidOperationException>(() => { _ = sut.AsObject.RemoveProperty(Encoding.UTF8.GetBytes("foo")).AsAny; });
    }

    [Fact]
    public void Remove_JsonElementBacked_JsonAny_SpanByte_FromNumber_ThrowsInvalidOperationException()
    {
        JsonAny sut = JsonAny.Parse("1.2");
        Assert.Throws<InvalidOperationException>(() => { _ = sut.AsObject.RemoveProperty(Encoding.UTF8.GetBytes("foo")).AsAny; });
    }

    [Fact]
    public void Remove_JsonElementBacked_JsonNotAny_String_FromObject()
    {
        JsonNotAny sut = JsonNotAny.Parse("""{"foo": "bar"}""");
        JsonNotAny result = sut.AsObject.RemoveProperty("foo").As<JsonNotAny>();
        Assert.False(result.AsObject.HasProperty("foo"));
    }

    [Fact]
    public void Remove_JsonElementBacked_JsonNotAny_String_FromUndefined_ThrowsInvalidOperationException()
    {
        JsonNotAny sut = JsonNotAny.Undefined;
        Assert.Throws<InvalidOperationException>(() => { sut.AsObject.RemoveProperty("foo").As<JsonNotAny>(); });
    }

    [Fact]
    public void Remove_JsonElementBacked_JsonNotAny_String_FromNumber_ThrowsInvalidOperationException()
    {
        JsonNotAny sut = JsonNotAny.Parse("1.2");
        Assert.Throws<InvalidOperationException>(() => { sut.AsObject.RemoveProperty("foo").As<JsonNotAny>(); });
    }

    [Fact]
    public void Remove_JsonElementBacked_JsonNotAny_SpanChar_FromObject()
    {
        JsonNotAny sut = JsonNotAny.Parse("""{"foo": "bar"}""");
        JsonNotAny result = sut.AsObject.RemoveProperty("foo".AsSpan()).As<JsonNotAny>();
        Assert.False(result.AsObject.HasProperty("foo".AsSpan()));
    }

    [Fact]
    public void Remove_JsonElementBacked_JsonNotAny_SpanChar_FromUndefined_ThrowsInvalidOperationException()
    {
        JsonNotAny sut = JsonNotAny.Undefined;
        Assert.Throws<InvalidOperationException>(() => { sut.AsObject.RemoveProperty("foo".AsSpan()).As<JsonNotAny>(); });
    }

    [Fact]
    public void Remove_JsonElementBacked_JsonNotAny_SpanChar_FromNumber_ThrowsInvalidOperationException()
    {
        JsonNotAny sut = JsonNotAny.Parse("1.2");
        Assert.Throws<InvalidOperationException>(() => { sut.AsObject.RemoveProperty("foo".AsSpan()).As<JsonNotAny>(); });
    }

    [Fact]
    public void Remove_JsonElementBacked_JsonNotAny_SpanByte_FromObject()
    {
        JsonNotAny sut = JsonNotAny.Parse("""{"foo": "bar"}""");
        JsonNotAny result = sut.AsObject.RemoveProperty(Encoding.UTF8.GetBytes("foo")).As<JsonNotAny>();
        Assert.False(result.AsObject.HasProperty(Encoding.UTF8.GetBytes("foo")));
    }

    [Fact]
    public void Remove_JsonElementBacked_JsonNotAny_SpanByte_FromUndefined_ThrowsInvalidOperationException()
    {
        JsonNotAny sut = JsonNotAny.Undefined;
        Assert.Throws<InvalidOperationException>(() => { sut.AsObject.RemoveProperty(Encoding.UTF8.GetBytes("foo")).As<JsonNotAny>(); });
    }

    [Fact]
    public void Remove_JsonElementBacked_JsonNotAny_SpanByte_FromNumber_ThrowsInvalidOperationException()
    {
        JsonNotAny sut = JsonNotAny.Parse("1.2");
        Assert.Throws<InvalidOperationException>(() => { sut.AsObject.RemoveProperty(Encoding.UTF8.GetBytes("foo")).As<JsonNotAny>(); });
    }

    [Fact]
    public void Set_JsonElementBacked_JsonObject_String_OnExistingObject()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "baz"}""");
        JsonObject result = sut.SetProperty(new JsonPropertyName("foo"), JsonAny.Parse("\"bar\""));
        Assert.True(result.TryGetProperty("foo", out JsonAny val));
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_JsonElementBacked_JsonAny_String_OnExistingObject()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "baz"}""");
        JsonAny result = sut.AsObject.SetProperty(new JsonPropertyName("foo"), JsonAny.Parse("\"bar\"")).AsAny;
        Assert.True(result.AsObject.TryGetProperty("foo", out JsonAny val));
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_JsonElementBacked_JsonObject_String_OnEmptyObject()
    {
        JsonObject sut = JsonObject.Parse("{}");
        JsonObject result = sut.SetProperty(new JsonPropertyName("foo"), JsonAny.Parse("\"bar\""));
        Assert.True(result.TryGetProperty("foo", out JsonAny val));
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_JsonElementBacked_JsonAny_String_OnEmptyObject()
    {
        JsonAny sut = JsonAny.Parse("{}");
        JsonAny result = sut.AsObject.SetProperty(new JsonPropertyName("foo"), JsonAny.Parse("\"bar\"")).AsAny;
        Assert.True(result.AsObject.TryGetProperty("foo", out JsonAny val));
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_JsonElementBacked_JsonObject_String_OnUndefined_ThrowsInvalidOperationException()
    {
        JsonObject sut = default(JsonObject);
        Assert.Throws<InvalidOperationException>(() => { sut.SetProperty(new JsonPropertyName("foo"), JsonAny.Parse("\"bar\"")); });
    }

    [Fact]
    public void Set_JsonElementBacked_JsonAny_String_OnUndefined_ThrowsInvalidOperationException()
    {
        JsonAny sut = JsonAny.Undefined;
        Assert.Throws<InvalidOperationException>(() => { _ = sut.AsObject.SetProperty(new JsonPropertyName("foo"), JsonAny.Parse("\"bar\"")).AsAny; });
    }

    [Fact]
    public void Set_JsonElementBacked_JsonObject_String_OnNumber_ThrowsInvalidOperationException()
    {
        JsonObject sut = JsonObject.Parse("1.2");
        Assert.Throws<InvalidOperationException>(() => { sut.SetProperty(new JsonPropertyName("foo"), JsonAny.Parse("\"bar\"")); });
    }

    [Fact]
    public void Set_JsonElementBacked_JsonAny_String_OnNumber_ThrowsInvalidOperationException()
    {
        JsonAny sut = JsonAny.Parse("1.2");
        Assert.Throws<InvalidOperationException>(() => { _ = sut.AsObject.SetProperty(new JsonPropertyName("foo"), JsonAny.Parse("\"bar\"")).AsAny; });
    }

    [Fact]
    public void Set_JsonElementBacked_JsonObject_JsonElement_OnExistingObject()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "baz"}""");
        JsonObject result = sut.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonAny.Parse("\"bar\""));
        Assert.True(result.TryGetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), out JsonAny val));
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_JsonElementBacked_JsonAny_JsonElement_OnExistingObject()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "baz"}""");
        JsonAny result = sut.AsObject.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonAny.Parse("\"bar\"")).AsAny;
        Assert.True(result.AsObject.TryGetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), out JsonAny val));
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_JsonElementBacked_JsonObject_JsonElement_OnEmptyObject()
    {
        JsonObject sut = JsonObject.Parse("{}");
        JsonObject result = sut.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonAny.Parse("\"bar\""));
        Assert.True(result.TryGetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), out JsonAny val));
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_JsonElementBacked_JsonAny_JsonElement_OnEmptyObject()
    {
        JsonAny sut = JsonAny.Parse("{}");
        JsonAny result = sut.AsObject.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonAny.Parse("\"bar\"")).AsAny;
        Assert.True(result.AsObject.TryGetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), out JsonAny val));
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_JsonElementBacked_JsonObject_JsonElement_OnUndefined_ThrowsInvalidOperationException()
    {
        JsonObject sut = default(JsonObject);
        Assert.Throws<InvalidOperationException>(() => { sut.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonAny.Parse("\"bar\"")); });
    }

    [Fact]
    public void Set_JsonElementBacked_JsonAny_JsonElement_OnUndefined_ThrowsInvalidOperationException()
    {
        JsonAny sut = JsonAny.Undefined;
        Assert.Throws<InvalidOperationException>(() => { _ = sut.AsObject.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonAny.Parse("\"bar\"")).AsAny; });
    }

    [Fact]
    public void Set_JsonElementBacked_JsonObject_JsonElement_OnNumber_ThrowsInvalidOperationException()
    {
        JsonObject sut = JsonObject.Parse("1.2");
        Assert.Throws<InvalidOperationException>(() => { sut.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonAny.Parse("\"bar\"")); });
    }

    [Fact]
    public void Set_JsonElementBacked_JsonAny_JsonElement_OnNumber_ThrowsInvalidOperationException()
    {
        JsonAny sut = JsonAny.Parse("1.2");
        Assert.Throws<InvalidOperationException>(() => { _ = sut.AsObject.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonAny.Parse("\"bar\"")).AsAny; });
    }

    [Fact]
    public void Set_JsonElementBacked_JsonObject_JsonElement_OnExistingObject_2()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "baz"}""");
        JsonObject result = sut.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonAny.Parse("\"bar\""));
        Assert.True(result.TryGetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), out JsonAny val));
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_JsonElementBacked_JsonAny_JsonElement_OnExistingObject_2()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "baz"}""");
        JsonAny result = sut.AsObject.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonAny.Parse("\"bar\"")).AsAny;
        Assert.True(result.AsObject.TryGetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), out JsonAny val));
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_JsonElementBacked_JsonObject_JsonElement_OnEmptyObject_2()
    {
        JsonObject sut = JsonObject.Parse("{}");
        JsonObject result = sut.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonAny.Parse("\"bar\""));
        Assert.True(result.TryGetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), out JsonAny val));
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_JsonElementBacked_JsonAny_JsonElement_OnEmptyObject_2()
    {
        JsonAny sut = JsonAny.Parse("{}");
        JsonAny result = sut.AsObject.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonAny.Parse("\"bar\"")).AsAny;
        Assert.True(result.AsObject.TryGetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), out JsonAny val));
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_JsonElementBacked_JsonObject_JsonElement_OnUndefined_ThrowsInvalidOperationException_2()
    {
        JsonObject sut = default(JsonObject);
        Assert.Throws<InvalidOperationException>(() => { sut.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonAny.Parse("\"bar\"")); });
    }

    [Fact]
    public void Set_JsonElementBacked_JsonAny_JsonElement_OnUndefined_ThrowsInvalidOperationException_2()
    {
        JsonAny sut = JsonAny.Undefined;
        Assert.Throws<InvalidOperationException>(() => { _ = sut.AsObject.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonAny.Parse("\"bar\"")).AsAny; });
    }

    [Fact]
    public void Set_JsonElementBacked_JsonObject_JsonElement_OnNumber_ThrowsInvalidOperationException_2()
    {
        JsonObject sut = JsonObject.Parse("1.2");
        Assert.Throws<InvalidOperationException>(() => { sut.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonAny.Parse("\"bar\"")); });
    }

    [Fact]
    public void Set_JsonElementBacked_JsonAny_JsonElement_OnNumber_ThrowsInvalidOperationException_2()
    {
        JsonAny sut = JsonAny.Parse("1.2");
        Assert.Throws<InvalidOperationException>(() => { _ = sut.AsObject.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonAny.Parse("\"bar\"")).AsAny; });
    }

    [Fact]
    public void Remove_DotnetBacked_JsonObject_String_FromObject()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        JsonObject result = sut.RemoveProperty("foo");
        Assert.False(result.HasProperty("foo"));
    }

    [Fact]
    public void Remove_DotnetBacked_JsonObject_String_FromUndefined()
    {
        JsonObject sut = JsonObject.Undefined;
        try
        {
            var result = sut.RemoveProperty("foo");
            if (result.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                Assert.False(result.HasProperty("foo"));
            }
        }
        catch (InvalidOperationException)
        {
            // Expected for non-object/undefined values
        }
    }

    [Fact]
    public void Remove_DotnetBacked_JsonObject_String_FromNumber()
    {
        JsonObject sut = JsonObject.Parse("1.2").AsDotnetBackedValue();
        try
        {
            var result = sut.RemoveProperty("foo");
            if (result.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                Assert.False(result.HasProperty("foo"));
            }
        }
        catch (InvalidOperationException)
        {
            // Expected for non-object/undefined values
        }
    }

    [Fact]
    public void Remove_DotnetBacked_JsonObject_SpanChar_FromObject()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        JsonObject result = sut.RemoveProperty("foo".AsSpan());
        Assert.False(result.HasProperty("foo".AsSpan()));
    }

    [Fact]
    public void Remove_DotnetBacked_JsonObject_SpanChar_FromUndefined()
    {
        JsonObject sut = JsonObject.Undefined;
        try
        {
            var result = sut.RemoveProperty("foo".AsSpan());
            if (result.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                Assert.False(result.HasProperty("foo".AsSpan()));
            }
        }
        catch (InvalidOperationException)
        {
            // Expected for non-object/undefined values
        }
    }

    [Fact]
    public void Remove_DotnetBacked_JsonObject_SpanChar_FromNumber()
    {
        JsonObject sut = JsonObject.Parse("1.2").AsDotnetBackedValue();
        try
        {
            var result = sut.RemoveProperty("foo".AsSpan());
            if (result.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                Assert.False(result.HasProperty("foo".AsSpan()));
            }
        }
        catch (InvalidOperationException)
        {
            // Expected for non-object/undefined values
        }
    }

    [Fact]
    public void Remove_DotnetBacked_JsonObject_SpanByte_FromObject()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        JsonObject result = sut.RemoveProperty(Encoding.UTF8.GetBytes("foo"));
        Assert.False(result.HasProperty(Encoding.UTF8.GetBytes("foo")));
    }

    [Fact]
    public void Remove_DotnetBacked_JsonObject_SpanByte_FromUndefined()
    {
        JsonObject sut = JsonObject.Undefined;
        try
        {
            var result = sut.RemoveProperty(Encoding.UTF8.GetBytes("foo"));
            if (result.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                Assert.False(result.HasProperty(Encoding.UTF8.GetBytes("foo")));
            }
        }
        catch (InvalidOperationException)
        {
            // Expected for non-object/undefined values
        }
    }

    [Fact]
    public void Remove_DotnetBacked_JsonObject_SpanByte_FromNumber()
    {
        JsonObject sut = JsonObject.Parse("1.2").AsDotnetBackedValue();
        try
        {
            var result = sut.RemoveProperty(Encoding.UTF8.GetBytes("foo"));
            if (result.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                Assert.False(result.HasProperty(Encoding.UTF8.GetBytes("foo")));
            }
        }
        catch (InvalidOperationException)
        {
            // Expected for non-object/undefined values
        }
    }

    [Fact]
    public void Remove_DotnetBacked_JsonAny_String_FromObject()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        JsonAny result = sut.AsObject.RemoveProperty("foo").AsAny;
        Assert.False(result.AsObject.HasProperty("foo"));
    }

    [Fact]
    public void Remove_DotnetBacked_JsonAny_String_FromUndefined()
    {
        JsonAny sut = default(JsonAny);
        try
        {
            var result = sut.AsObject.RemoveProperty("foo").AsAny;
            if (result.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                Assert.False(result.AsObject.HasProperty("foo"));
            }
        }
        catch (InvalidOperationException)
        {
            // Expected for non-object/undefined values
        }
    }

    [Fact]
    public void Remove_DotnetBacked_JsonAny_String_FromNumber()
    {
        JsonAny sut = JsonAny.Parse("1.2").AsDotnetBackedValue();
        try
        {
            var result = sut.AsObject.RemoveProperty("foo").AsAny;
            if (result.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                Assert.False(result.AsObject.HasProperty("foo"));
            }
        }
        catch (InvalidOperationException)
        {
            // Expected for non-object/undefined values
        }
    }

    [Fact]
    public void Remove_DotnetBacked_JsonAny_SpanChar_FromObject()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        JsonAny result = sut.AsObject.RemoveProperty("foo".AsSpan()).AsAny;
        Assert.False(result.AsObject.HasProperty("foo".AsSpan()));
    }

    [Fact]
    public void Remove_DotnetBacked_JsonAny_SpanChar_FromUndefined()
    {
        JsonAny sut = default(JsonAny);
        try
        {
            var result = sut.AsObject.RemoveProperty("foo".AsSpan()).AsAny;
            if (result.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                Assert.False(result.AsObject.HasProperty("foo".AsSpan()));
            }
        }
        catch (InvalidOperationException)
        {
            // Expected for non-object/undefined values
        }
    }

    [Fact]
    public void Remove_DotnetBacked_JsonAny_SpanChar_FromNumber()
    {
        JsonAny sut = JsonAny.Parse("1.2").AsDotnetBackedValue();
        try
        {
            var result = sut.AsObject.RemoveProperty("foo".AsSpan()).AsAny;
            if (result.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                Assert.False(result.AsObject.HasProperty("foo".AsSpan()));
            }
        }
        catch (InvalidOperationException)
        {
            // Expected for non-object/undefined values
        }
    }

    [Fact]
    public void Remove_DotnetBacked_JsonAny_SpanByte_FromObject()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        JsonAny result = sut.AsObject.RemoveProperty(Encoding.UTF8.GetBytes("foo")).AsAny;
        Assert.False(result.AsObject.HasProperty(Encoding.UTF8.GetBytes("foo")));
    }

    [Fact]
    public void Remove_DotnetBacked_JsonAny_SpanByte_FromUndefined()
    {
        JsonAny sut = default(JsonAny);
        try
        {
            var result = sut.AsObject.RemoveProperty(Encoding.UTF8.GetBytes("foo")).AsAny;
            if (result.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                Assert.False(result.AsObject.HasProperty(Encoding.UTF8.GetBytes("foo")));
            }
        }
        catch (InvalidOperationException)
        {
            // Expected for non-object/undefined values
        }
    }

    [Fact]
    public void Remove_DotnetBacked_JsonAny_SpanByte_FromNumber()
    {
        JsonAny sut = JsonAny.Parse("1.2").AsDotnetBackedValue();
        try
        {
            var result = sut.AsObject.RemoveProperty(Encoding.UTF8.GetBytes("foo")).AsAny;
            if (result.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                Assert.False(result.AsObject.HasProperty(Encoding.UTF8.GetBytes("foo")));
            }
        }
        catch (InvalidOperationException)
        {
            // Expected for non-object/undefined values
        }
    }

    [Fact]
    public void Remove_DotnetBacked_JsonNotAny_String_FromObject()
    {
        JsonNotAny sut = JsonNotAny.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        JsonNotAny result = sut.AsObject.RemoveProperty("foo").As<JsonNotAny>();
        Assert.False(result.AsObject.HasProperty("foo"));
    }

    [Fact]
    public void Remove_DotnetBacked_JsonNotAny_String_FromUndefined()
    {
        JsonNotAny sut = JsonNotAny.Undefined;
        try
        {
            var result = sut.AsObject.RemoveProperty("foo").As<JsonNotAny>();
            if (result.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                Assert.False(result.AsObject.HasProperty("foo"));
            }
        }
        catch (InvalidOperationException)
        {
            // Expected for non-object/undefined values
        }
    }

    [Fact]
    public void Remove_DotnetBacked_JsonNotAny_String_FromNumber()
    {
        JsonNotAny sut = JsonNotAny.Parse("1.2").AsDotnetBackedValue();
        try
        {
            var result = sut.AsObject.RemoveProperty("foo").As<JsonNotAny>();
            if (result.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                Assert.False(result.AsObject.HasProperty("foo"));
            }
        }
        catch (InvalidOperationException)
        {
            // Expected for non-object/undefined values
        }
    }

    [Fact]
    public void Remove_DotnetBacked_JsonNotAny_SpanChar_FromObject()
    {
        JsonNotAny sut = JsonNotAny.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        JsonNotAny result = sut.AsObject.RemoveProperty("foo".AsSpan()).As<JsonNotAny>();
        Assert.False(result.AsObject.HasProperty("foo".AsSpan()));
    }

    [Fact]
    public void Remove_DotnetBacked_JsonNotAny_SpanChar_FromUndefined()
    {
        JsonNotAny sut = JsonNotAny.Undefined;
        try
        {
            var result = sut.AsObject.RemoveProperty("foo".AsSpan()).As<JsonNotAny>();
            if (result.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                Assert.False(result.AsObject.HasProperty("foo".AsSpan()));
            }
        }
        catch (InvalidOperationException)
        {
            // Expected for non-object/undefined values
        }
    }

    [Fact]
    public void Remove_DotnetBacked_JsonNotAny_SpanChar_FromNumber()
    {
        JsonNotAny sut = JsonNotAny.Parse("1.2").AsDotnetBackedValue();
        try
        {
            var result = sut.AsObject.RemoveProperty("foo".AsSpan()).As<JsonNotAny>();
            if (result.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                Assert.False(result.AsObject.HasProperty("foo".AsSpan()));
            }
        }
        catch (InvalidOperationException)
        {
            // Expected for non-object/undefined values
        }
    }

    [Fact]
    public void Remove_DotnetBacked_JsonNotAny_SpanByte_FromObject()
    {
        JsonNotAny sut = JsonNotAny.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        JsonNotAny result = sut.AsObject.RemoveProperty(Encoding.UTF8.GetBytes("foo")).As<JsonNotAny>();
        Assert.False(result.AsObject.HasProperty(Encoding.UTF8.GetBytes("foo")));
    }

    [Fact]
    public void Remove_DotnetBacked_JsonNotAny_SpanByte_FromUndefined()
    {
        JsonNotAny sut = JsonNotAny.Undefined;
        try
        {
            var result = sut.AsObject.RemoveProperty(Encoding.UTF8.GetBytes("foo")).As<JsonNotAny>();
            if (result.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                Assert.False(result.AsObject.HasProperty(Encoding.UTF8.GetBytes("foo")));
            }
        }
        catch (InvalidOperationException)
        {
            // Expected for non-object/undefined values
        }
    }

    [Fact]
    public void Remove_DotnetBacked_JsonNotAny_SpanByte_FromNumber()
    {
        JsonNotAny sut = JsonNotAny.Parse("1.2").AsDotnetBackedValue();
        try
        {
            var result = sut.AsObject.RemoveProperty(Encoding.UTF8.GetBytes("foo")).As<JsonNotAny>();
            if (result.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                Assert.False(result.AsObject.HasProperty(Encoding.UTF8.GetBytes("foo")));
            }
        }
        catch (InvalidOperationException)
        {
            // Expected for non-object/undefined values
        }
    }

    [Fact]
    public void Set_DotnetBacked_JsonObject_String_OnExistingObject()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "baz"}""").AsDotnetBackedValue();
        JsonObject result = sut.SetProperty(new JsonPropertyName("foo"), JsonAny.Parse("\"bar\""));
        Assert.True(result.TryGetProperty("foo", out JsonAny val));
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_DotnetBacked_JsonAny_String_OnExistingObject()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "baz"}""").AsDotnetBackedValue();
        JsonAny result = sut.AsObject.SetProperty(new JsonPropertyName("foo"), JsonAny.Parse("\"bar\"")).AsAny;
        Assert.True(result.AsObject.TryGetProperty("foo", out JsonAny val));
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_DotnetBacked_JsonNotAny_String_OnExistingObject()
    {
        JsonNotAny sut = JsonNotAny.Parse("""{"foo": "baz"}""").AsDotnetBackedValue();
        JsonNotAny result = sut.AsObject.SetProperty(new JsonPropertyName("foo"), JsonNotAny.Parse("\"bar\"")).As<JsonNotAny>();
        Assert.True(result.AsObject.TryGetProperty("foo", out JsonNotAny val));
        Assert.Equal(JsonNotAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_DotnetBacked_JsonObject_String_OnEmptyObject()
    {
        JsonObject sut = JsonObject.Parse("{}").AsDotnetBackedValue();
        JsonObject result = sut.SetProperty(new JsonPropertyName("foo"), JsonAny.Parse("\"bar\""));
        Assert.True(result.TryGetProperty("foo", out JsonAny val));
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_DotnetBacked_JsonAny_String_OnEmptyObject()
    {
        JsonAny sut = JsonAny.Parse("{}").AsDotnetBackedValue();
        JsonAny result = sut.AsObject.SetProperty(new JsonPropertyName("foo"), JsonAny.Parse("\"bar\"")).AsAny;
        Assert.True(result.AsObject.TryGetProperty("foo", out JsonAny val));
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_DotnetBacked_JsonNotAny_String_OnEmptyObject()
    {
        JsonNotAny sut = JsonNotAny.Parse("{}").AsDotnetBackedValue();
        JsonNotAny result = sut.AsObject.SetProperty(new JsonPropertyName("foo"), JsonNotAny.Parse("\"bar\"")).As<JsonNotAny>();
        Assert.True(result.AsObject.TryGetProperty("foo", out JsonNotAny val));
        Assert.Equal(JsonNotAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_DotnetBacked_JsonObject_String_OnUndefined_ThrowsInvalidOperationException()
    {
        JsonObject sut = JsonObject.Undefined;
        Assert.Throws<InvalidOperationException>(() => { sut.SetProperty(new JsonPropertyName("foo"), JsonAny.Parse("\"bar\"")); });
    }

    [Fact]
    public void Set_DotnetBacked_JsonAny_String_OnUndefined_ThrowsInvalidOperationException()
    {
        JsonAny sut = default(JsonAny);
        Assert.Throws<InvalidOperationException>(() => { _ = sut.AsObject.SetProperty(new JsonPropertyName("foo"), JsonAny.Parse("\"bar\"")).AsAny; });
    }

    [Fact]
    public void Set_DotnetBacked_JsonNotAny_String_OnUndefined_ThrowsInvalidOperationException()
    {
        JsonNotAny sut = JsonNotAny.Undefined;
        Assert.Throws<InvalidOperationException>(() => { sut.AsObject.SetProperty(new JsonPropertyName("foo"), JsonNotAny.Parse("\"bar\"")).As<JsonNotAny>(); });
    }

    [Fact]
    public void Set_DotnetBacked_JsonObject_JsonElement_OnExistingObject()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "baz"}""").AsDotnetBackedValue();
        JsonObject result = sut.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonAny.Parse("\"bar\""));
        Assert.True(result.TryGetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), out JsonAny val));
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_DotnetBacked_JsonAny_JsonElement_OnExistingObject()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "baz"}""").AsDotnetBackedValue();
        JsonAny result = sut.AsObject.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonAny.Parse("\"bar\"")).AsAny;
        Assert.True(result.AsObject.TryGetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), out JsonAny val));
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_DotnetBacked_JsonNotAny_JsonElement_OnExistingObject()
    {
        JsonNotAny sut = JsonNotAny.Parse("""{"foo": "baz"}""").AsDotnetBackedValue();
        JsonNotAny result = sut.AsObject.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonNotAny.Parse("\"bar\"")).As<JsonNotAny>();
        Assert.True(result.AsObject.TryGetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), out JsonNotAny val));
        Assert.Equal(JsonNotAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_DotnetBacked_JsonObject_JsonElement_OnEmptyObject()
    {
        JsonObject sut = JsonObject.Parse("{}").AsDotnetBackedValue();
        JsonObject result = sut.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonAny.Parse("\"bar\""));
        Assert.True(result.TryGetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), out JsonAny val));
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_DotnetBacked_JsonAny_JsonElement_OnEmptyObject()
    {
        JsonAny sut = JsonAny.Parse("{}").AsDotnetBackedValue();
        JsonAny result = sut.AsObject.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonAny.Parse("\"bar\"")).AsAny;
        Assert.True(result.AsObject.TryGetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), out JsonAny val));
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_DotnetBacked_JsonNotAny_JsonElement_OnEmptyObject()
    {
        JsonNotAny sut = JsonNotAny.Parse("{}").AsDotnetBackedValue();
        JsonNotAny result = sut.AsObject.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonNotAny.Parse("\"bar\"")).As<JsonNotAny>();
        Assert.True(result.AsObject.TryGetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), out JsonNotAny val));
        Assert.Equal(JsonNotAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_DotnetBacked_JsonObject_JsonElement_OnUndefined_ThrowsInvalidOperationException()
    {
        JsonObject sut = JsonObject.Undefined;
        Assert.Throws<InvalidOperationException>(() => { sut.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonAny.Parse("\"bar\"")); });
    }

    [Fact]
    public void Set_DotnetBacked_JsonAny_JsonElement_OnUndefined_ThrowsInvalidOperationException()
    {
        JsonAny sut = default(JsonAny);
        Assert.Throws<InvalidOperationException>(() => { _ = sut.AsObject.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonAny.Parse("\"bar\"")).AsAny; });
    }

    [Fact]
    public void Set_DotnetBacked_JsonNotAny_JsonElement_OnUndefined_ThrowsInvalidOperationException()
    {
        JsonNotAny sut = JsonNotAny.Undefined;
        Assert.Throws<InvalidOperationException>(() => { sut.AsObject.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonNotAny.Parse("\"bar\"")).As<JsonNotAny>(); });
    }

    [Fact]
    public void Set_DotnetBacked_JsonObject_JsonElement_OnExistingObject_2()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "baz"}""").AsDotnetBackedValue();
        JsonObject result = sut.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonAny.Parse("\"bar\""));
        Assert.True(result.TryGetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), out JsonAny val));
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_DotnetBacked_JsonAny_JsonElement_OnExistingObject_2()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "baz"}""").AsDotnetBackedValue();
        JsonAny result = sut.AsObject.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonAny.Parse("\"bar\"")).AsAny;
        Assert.True(result.AsObject.TryGetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), out JsonAny val));
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_DotnetBacked_JsonNotAny_JsonElement_OnExistingObject_2()
    {
        JsonNotAny sut = JsonNotAny.Parse("""{"foo": "baz"}""").AsDotnetBackedValue();
        JsonNotAny result = sut.AsObject.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonNotAny.Parse("\"bar\"")).As<JsonNotAny>();
        Assert.True(result.AsObject.TryGetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), out JsonNotAny val));
        Assert.Equal(JsonNotAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_DotnetBacked_JsonObject_JsonElement_OnEmptyObject_2()
    {
        JsonObject sut = JsonObject.Parse("{}").AsDotnetBackedValue();
        JsonObject result = sut.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonAny.Parse("\"bar\""));
        Assert.True(result.TryGetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), out JsonAny val));
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_DotnetBacked_JsonAny_JsonElement_OnEmptyObject_2()
    {
        JsonAny sut = JsonAny.Parse("{}").AsDotnetBackedValue();
        JsonAny result = sut.AsObject.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonAny.Parse("\"bar\"")).AsAny;
        Assert.True(result.AsObject.TryGetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), out JsonAny val));
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_DotnetBacked_JsonNotAny_JsonElement_OnEmptyObject_2()
    {
        JsonNotAny sut = JsonNotAny.Parse("{}").AsDotnetBackedValue();
        JsonNotAny result = sut.AsObject.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonNotAny.Parse("\"bar\"")).As<JsonNotAny>();
        Assert.True(result.AsObject.TryGetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), out JsonNotAny val));
        Assert.Equal(JsonNotAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void Set_DotnetBacked_JsonObject_JsonElement_OnUndefined_ThrowsInvalidOperationException_2()
    {
        JsonObject sut = JsonObject.Undefined;
        Assert.Throws<InvalidOperationException>(() => { sut.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonAny.Parse("\"bar\"")); });
    }

    [Fact]
    public void Set_DotnetBacked_JsonAny_JsonElement_OnUndefined_ThrowsInvalidOperationException_2()
    {
        JsonAny sut = default(JsonAny);
        Assert.Throws<InvalidOperationException>(() => { _ = sut.AsObject.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonAny.Parse("\"bar\"")).AsAny; });
    }

    [Fact]
    public void Set_DotnetBacked_JsonNotAny_JsonElement_OnUndefined_ThrowsInvalidOperationException_2()
    {
        JsonNotAny sut = JsonNotAny.Undefined;
        Assert.Throws<InvalidOperationException>(() => { sut.AsObject.SetProperty(JsonPropertyName.ParseValue("\"foo\"".AsSpan()), JsonNotAny.Parse("\"bar\"")).As<JsonNotAny>(); });
    }

    [Fact]
    public void TryGet_JsonElementBacked_JsonObject_String_Found()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "bar"}""");
        bool found = sut.TryGetProperty("foo", out JsonAny val);
        Assert.True(found);
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void TryGet_JsonElementBacked_JsonObject_SpanChar_Found()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "bar"}""");
        bool found = sut.TryGetProperty("foo".AsSpan(), out JsonAny val);
        Assert.True(found);
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void TryGet_JsonElementBacked_JsonObject_SpanByte_Found()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "bar"}""");
        bool found = sut.TryGetProperty(Encoding.UTF8.GetBytes("foo"), out JsonAny val);
        Assert.True(found);
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void TryGet_JsonElementBacked_JsonAny_String_Found()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "bar"}""");
        bool found = sut.AsObject.TryGetProperty("foo", out JsonAny val);
        Assert.True(found);
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void TryGet_JsonElementBacked_JsonAny_SpanChar_Found()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "bar"}""");
        bool found = sut.AsObject.TryGetProperty("foo".AsSpan(), out JsonAny val);
        Assert.True(found);
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void TryGet_JsonElementBacked_JsonAny_SpanByte_Found()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "bar"}""");
        bool found = sut.AsObject.TryGetProperty(Encoding.UTF8.GetBytes("foo"), out JsonAny val);
        Assert.True(found);
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void TryGet_JsonElementBacked_JsonNotAny_String_Found()
    {
        JsonNotAny sut = JsonNotAny.Parse("""{"foo": "bar"}""");
        bool found = sut.AsObject.TryGetProperty("foo", out JsonAny val);
        Assert.True(found);
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void TryGet_JsonElementBacked_JsonNotAny_SpanChar_Found()
    {
        JsonNotAny sut = JsonNotAny.Parse("""{"foo": "bar"}""");
        bool found = sut.AsObject.TryGetProperty("foo".AsSpan(), out JsonAny val);
        Assert.True(found);
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void TryGet_JsonElementBacked_JsonNotAny_SpanByte_Found()
    {
        JsonNotAny sut = JsonNotAny.Parse("""{"foo": "bar"}""");
        bool found = sut.AsObject.TryGetProperty(Encoding.UTF8.GetBytes("foo"), out JsonAny val);
        Assert.True(found);
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void TryGet_JsonElementBacked_JsonObject_String_NotFound()
    {
        JsonObject sut = JsonObject.Parse("{}");
        bool found = sut.TryGetProperty("foo", out JsonAny _);
        Assert.False(found);
    }

    [Fact]
    public void TryGet_JsonElementBacked_JsonObject_SpanChar_NotFound()
    {
        JsonObject sut = JsonObject.Parse("{}");
        bool found = sut.TryGetProperty("foo".AsSpan(), out JsonAny _);
        Assert.False(found);
    }

    [Fact]
    public void TryGet_JsonElementBacked_JsonObject_SpanByte_NotFound()
    {
        JsonObject sut = JsonObject.Parse("{}");
        bool found = sut.TryGetProperty(Encoding.UTF8.GetBytes("foo"), out JsonAny _);
        Assert.False(found);
    }

    [Fact]
    public void TryGet_JsonElementBacked_JsonAny_String_NotFound()
    {
        JsonAny sut = JsonAny.Parse("{}");
        bool found = sut.AsObject.TryGetProperty("foo", out JsonAny _);
        Assert.False(found);
    }

    [Fact]
    public void TryGet_JsonElementBacked_JsonAny_SpanChar_NotFound()
    {
        JsonAny sut = JsonAny.Parse("{}");
        bool found = sut.AsObject.TryGetProperty("foo".AsSpan(), out JsonAny _);
        Assert.False(found);
    }

    [Fact]
    public void TryGet_JsonElementBacked_JsonAny_SpanByte_NotFound()
    {
        JsonAny sut = JsonAny.Parse("{}");
        bool found = sut.AsObject.TryGetProperty(Encoding.UTF8.GetBytes("foo"), out JsonAny _);
        Assert.False(found);
    }

    [Fact]
    public void TryGet_JsonElementBacked_JsonNotAny_String_NotFound()
    {
        JsonNotAny sut = JsonNotAny.Parse("{}");
        bool found = sut.AsObject.TryGetProperty("foo", out JsonAny _);
        Assert.False(found);
    }

    [Fact]
    public void TryGet_JsonElementBacked_JsonNotAny_SpanChar_NotFound()
    {
        JsonNotAny sut = JsonNotAny.Parse("{}");
        bool found = sut.AsObject.TryGetProperty("foo".AsSpan(), out JsonAny _);
        Assert.False(found);
    }

    [Fact]
    public void TryGet_JsonElementBacked_JsonNotAny_SpanByte_NotFound()
    {
        JsonNotAny sut = JsonNotAny.Parse("{}");
        bool found = sut.AsObject.TryGetProperty(Encoding.UTF8.GetBytes("foo"), out JsonAny _);
        Assert.False(found);
    }

    [Fact]
    public void TryGet_DotnetBacked_JsonObject_String_Found()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        bool found = sut.TryGetProperty("foo", out JsonAny val);
        Assert.True(found);
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void TryGet_DotnetBacked_JsonObject_SpanChar_Found()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        bool found = sut.TryGetProperty("foo".AsSpan(), out JsonAny val);
        Assert.True(found);
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void TryGet_DotnetBacked_JsonObject_SpanByte_Found()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        bool found = sut.TryGetProperty(Encoding.UTF8.GetBytes("foo"), out JsonAny val);
        Assert.True(found);
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void TryGet_DotnetBacked_JsonAny_String_Found()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        bool found = sut.AsObject.TryGetProperty("foo", out JsonAny val);
        Assert.True(found);
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void TryGet_DotnetBacked_JsonAny_SpanChar_Found()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        bool found = sut.AsObject.TryGetProperty("foo".AsSpan(), out JsonAny val);
        Assert.True(found);
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void TryGet_DotnetBacked_JsonAny_SpanByte_Found()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        bool found = sut.AsObject.TryGetProperty(Encoding.UTF8.GetBytes("foo"), out JsonAny val);
        Assert.True(found);
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void TryGet_DotnetBacked_JsonNotAny_String_Found()
    {
        JsonNotAny sut = JsonNotAny.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        bool found = sut.AsObject.TryGetProperty("foo", out JsonAny val);
        Assert.True(found);
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void TryGet_DotnetBacked_JsonNotAny_SpanChar_Found()
    {
        JsonNotAny sut = JsonNotAny.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        bool found = sut.AsObject.TryGetProperty("foo".AsSpan(), out JsonAny val);
        Assert.True(found);
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void TryGet_DotnetBacked_JsonNotAny_SpanByte_Found()
    {
        JsonNotAny sut = JsonNotAny.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        bool found = sut.AsObject.TryGetProperty(Encoding.UTF8.GetBytes("foo"), out JsonAny val);
        Assert.True(found);
        Assert.Equal(JsonAny.Parse("\"bar\""), val);
    }

    [Fact]
    public void TryGet_DotnetBacked_JsonObject_String_NotFound()
    {
        JsonObject sut = JsonObject.Parse("{}").AsDotnetBackedValue();
        bool found = sut.TryGetProperty("foo", out JsonAny _);
        Assert.False(found);
    }

    [Fact]
    public void TryGet_DotnetBacked_JsonObject_SpanChar_NotFound()
    {
        JsonObject sut = JsonObject.Parse("{}").AsDotnetBackedValue();
        bool found = sut.TryGetProperty("foo".AsSpan(), out JsonAny _);
        Assert.False(found);
    }

    [Fact]
    public void TryGet_DotnetBacked_JsonObject_SpanByte_NotFound()
    {
        JsonObject sut = JsonObject.Parse("{}").AsDotnetBackedValue();
        bool found = sut.TryGetProperty(Encoding.UTF8.GetBytes("foo"), out JsonAny _);
        Assert.False(found);
    }

    [Fact]
    public void TryGet_DotnetBacked_JsonAny_String_NotFound()
    {
        JsonAny sut = JsonAny.Parse("{}").AsDotnetBackedValue();
        bool found = sut.AsObject.TryGetProperty("foo", out JsonAny _);
        Assert.False(found);
    }

    [Fact]
    public void TryGet_DotnetBacked_JsonAny_SpanChar_NotFound()
    {
        JsonAny sut = JsonAny.Parse("{}").AsDotnetBackedValue();
        bool found = sut.AsObject.TryGetProperty("foo".AsSpan(), out JsonAny _);
        Assert.False(found);
    }

    [Fact]
    public void TryGet_DotnetBacked_JsonAny_SpanByte_NotFound()
    {
        JsonAny sut = JsonAny.Parse("{}").AsDotnetBackedValue();
        bool found = sut.AsObject.TryGetProperty(Encoding.UTF8.GetBytes("foo"), out JsonAny _);
        Assert.False(found);
    }

    [Fact]
    public void TryGet_DotnetBacked_JsonNotAny_String_NotFound()
    {
        JsonNotAny sut = JsonNotAny.Parse("{}").AsDotnetBackedValue();
        bool found = sut.AsObject.TryGetProperty("foo", out JsonAny _);
        Assert.False(found);
    }

    [Fact]
    public void TryGet_DotnetBacked_JsonNotAny_SpanChar_NotFound()
    {
        JsonNotAny sut = JsonNotAny.Parse("{}").AsDotnetBackedValue();
        bool found = sut.AsObject.TryGetProperty("foo".AsSpan(), out JsonAny _);
        Assert.False(found);
    }

    [Fact]
    public void TryGet_DotnetBacked_JsonNotAny_SpanByte_NotFound()
    {
        JsonNotAny sut = JsonNotAny.Parse("{}").AsDotnetBackedValue();
        bool found = sut.AsObject.TryGetProperty(Encoding.UTF8.GetBytes("foo"), out JsonAny _);
        Assert.False(found);
    }

    [Fact]
    public void HasProperty_JsonElementBacked_JsonObject_String_Found()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "bar"}""");
        bool result = sut.HasProperty("foo");
        Assert.True(result);
    }

    [Fact]
    public void HasProperty_JsonElementBacked_JsonObject_SpanChar_Found()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "bar"}""");
        bool result = sut.HasProperty("foo".AsSpan());
        Assert.True(result);
    }

    [Fact]
    public void HasProperty_JsonElementBacked_JsonObject_SpanByte_Found()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "bar"}""");
        bool result = sut.HasProperty(Encoding.UTF8.GetBytes("foo"));
        Assert.True(result);
    }

    [Fact]
    public void HasProperty_JsonElementBacked_JsonAny_String_Found()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "bar"}""");
        bool result = sut.AsObject.HasProperty("foo");
        Assert.True(result);
    }

    [Fact]
    public void HasProperty_JsonElementBacked_JsonAny_SpanChar_Found()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "bar"}""");
        bool result = sut.AsObject.HasProperty("foo".AsSpan());
        Assert.True(result);
    }

    [Fact]
    public void HasProperty_JsonElementBacked_JsonAny_SpanByte_Found()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "bar"}""");
        bool result = sut.AsObject.HasProperty(Encoding.UTF8.GetBytes("foo"));
        Assert.True(result);
    }

    [Fact]
    public void HasProperty_JsonElementBacked_JsonNotAny_String_Found()
    {
        JsonNotAny sut = JsonNotAny.Parse("""{"foo": "bar"}""");
        bool result = sut.AsObject.HasProperty("foo");
        Assert.True(result);
    }

    [Fact]
    public void HasProperty_JsonElementBacked_JsonNotAny_SpanChar_Found()
    {
        JsonNotAny sut = JsonNotAny.Parse("""{"foo": "bar"}""");
        bool result = sut.AsObject.HasProperty("foo".AsSpan());
        Assert.True(result);
    }

    [Fact]
    public void HasProperty_JsonElementBacked_JsonNotAny_SpanByte_Found()
    {
        JsonNotAny sut = JsonNotAny.Parse("""{"foo": "bar"}""");
        bool result = sut.AsObject.HasProperty(Encoding.UTF8.GetBytes("foo"));
        Assert.True(result);
    }

    [Fact]
    public void HasProperty_JsonElementBacked_JsonObject_String_NotFound_WrongName()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "bar"}""");
        bool result = sut.HasProperty("bar");
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_JsonElementBacked_JsonObject_SpanChar_NotFound_WrongName()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "bar"}""");
        bool result = sut.HasProperty("bar".AsSpan());
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_JsonElementBacked_JsonObject_SpanByte_NotFound_WrongName()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "bar"}""");
        bool result = sut.HasProperty(Encoding.UTF8.GetBytes("bar"));
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_JsonElementBacked_JsonAny_String_NotFound_WrongName()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "bar"}""");
        bool result = sut.AsObject.HasProperty("bar");
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_JsonElementBacked_JsonAny_SpanChar_NotFound_WrongName()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "bar"}""");
        bool result = sut.AsObject.HasProperty("bar".AsSpan());
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_JsonElementBacked_JsonAny_SpanByte_NotFound_WrongName()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "bar"}""");
        bool result = sut.AsObject.HasProperty(Encoding.UTF8.GetBytes("bar"));
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_JsonElementBacked_JsonNotAny_String_NotFound_WrongName()
    {
        JsonNotAny sut = JsonNotAny.Parse("""{"foo": "bar"}""");
        bool result = sut.AsObject.HasProperty("bar");
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_JsonElementBacked_JsonNotAny_SpanChar_NotFound_WrongName()
    {
        JsonNotAny sut = JsonNotAny.Parse("""{"foo": "bar"}""");
        bool result = sut.AsObject.HasProperty("bar".AsSpan());
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_JsonElementBacked_JsonNotAny_SpanByte_NotFound_WrongName()
    {
        JsonNotAny sut = JsonNotAny.Parse("""{"foo": "bar"}""");
        bool result = sut.AsObject.HasProperty(Encoding.UTF8.GetBytes("bar"));
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_JsonElementBacked_JsonObject_String_NotFound_EmptyObject()
    {
        JsonObject sut = JsonObject.Parse("{}");
        bool result = sut.HasProperty("foo");
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_JsonElementBacked_JsonObject_SpanChar_NotFound_EmptyObject()
    {
        JsonObject sut = JsonObject.Parse("{}");
        bool result = sut.HasProperty("foo".AsSpan());
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_JsonElementBacked_JsonObject_SpanByte_NotFound_EmptyObject()
    {
        JsonObject sut = JsonObject.Parse("{}");
        bool result = sut.HasProperty(Encoding.UTF8.GetBytes("foo"));
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_JsonElementBacked_JsonAny_String_NotFound_EmptyObject()
    {
        JsonAny sut = JsonAny.Parse("{}");
        bool result = sut.AsObject.HasProperty("foo");
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_JsonElementBacked_JsonAny_SpanChar_NotFound_EmptyObject()
    {
        JsonAny sut = JsonAny.Parse("{}");
        bool result = sut.AsObject.HasProperty("foo".AsSpan());
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_JsonElementBacked_JsonAny_SpanByte_NotFound_EmptyObject()
    {
        JsonAny sut = JsonAny.Parse("{}");
        bool result = sut.AsObject.HasProperty(Encoding.UTF8.GetBytes("foo"));
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_JsonElementBacked_JsonNotAny_String_NotFound_EmptyObject()
    {
        JsonNotAny sut = JsonNotAny.Parse("{}");
        bool result = sut.AsObject.HasProperty("foo");
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_JsonElementBacked_JsonNotAny_SpanChar_NotFound_EmptyObject()
    {
        JsonNotAny sut = JsonNotAny.Parse("{}");
        bool result = sut.AsObject.HasProperty("foo".AsSpan());
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_JsonElementBacked_JsonNotAny_SpanByte_NotFound_EmptyObject()
    {
        JsonNotAny sut = JsonNotAny.Parse("{}");
        bool result = sut.AsObject.HasProperty(Encoding.UTF8.GetBytes("foo"));
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_DotnetBacked_JsonObject_String_Found()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        bool result = sut.HasProperty("foo");
        Assert.True(result);
    }

    [Fact]
    public void HasProperty_DotnetBacked_JsonObject_SpanChar_Found()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        bool result = sut.HasProperty("foo".AsSpan());
        Assert.True(result);
    }

    [Fact]
    public void HasProperty_DotnetBacked_JsonObject_SpanByte_Found()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        bool result = sut.HasProperty(Encoding.UTF8.GetBytes("foo"));
        Assert.True(result);
    }

    [Fact]
    public void HasProperty_DotnetBacked_JsonAny_String_Found()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        bool result = sut.AsObject.HasProperty("foo");
        Assert.True(result);
    }

    [Fact]
    public void HasProperty_DotnetBacked_JsonAny_SpanChar_Found()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        bool result = sut.AsObject.HasProperty("foo".AsSpan());
        Assert.True(result);
    }

    [Fact]
    public void HasProperty_DotnetBacked_JsonAny_SpanByte_Found()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        bool result = sut.AsObject.HasProperty(Encoding.UTF8.GetBytes("foo"));
        Assert.True(result);
    }

    [Fact]
    public void HasProperty_DotnetBacked_JsonNotAny_String_Found()
    {
        JsonNotAny sut = JsonNotAny.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        bool result = sut.AsObject.HasProperty("foo");
        Assert.True(result);
    }

    [Fact]
    public void HasProperty_DotnetBacked_JsonNotAny_SpanChar_Found()
    {
        JsonNotAny sut = JsonNotAny.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        bool result = sut.AsObject.HasProperty("foo".AsSpan());
        Assert.True(result);
    }

    [Fact]
    public void HasProperty_DotnetBacked_JsonNotAny_SpanByte_Found()
    {
        JsonNotAny sut = JsonNotAny.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        bool result = sut.AsObject.HasProperty(Encoding.UTF8.GetBytes("foo"));
        Assert.True(result);
    }

    [Fact]
    public void HasProperty_DotnetBacked_JsonObject_String_NotFound_WrongName()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        bool result = sut.HasProperty("bar");
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_DotnetBacked_JsonObject_SpanChar_NotFound_WrongName()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        bool result = sut.HasProperty("bar".AsSpan());
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_DotnetBacked_JsonObject_SpanByte_NotFound_WrongName()
    {
        JsonObject sut = JsonObject.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        bool result = sut.HasProperty(Encoding.UTF8.GetBytes("bar"));
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_DotnetBacked_JsonAny_String_NotFound_WrongName()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        bool result = sut.AsObject.HasProperty("bar");
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_DotnetBacked_JsonAny_SpanChar_NotFound_WrongName()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        bool result = sut.AsObject.HasProperty("bar".AsSpan());
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_DotnetBacked_JsonAny_SpanByte_NotFound_WrongName()
    {
        JsonAny sut = JsonAny.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        bool result = sut.AsObject.HasProperty(Encoding.UTF8.GetBytes("bar"));
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_DotnetBacked_JsonNotAny_String_NotFound_WrongName()
    {
        JsonNotAny sut = JsonNotAny.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        bool result = sut.AsObject.HasProperty("bar");
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_DotnetBacked_JsonNotAny_SpanChar_NotFound_WrongName()
    {
        JsonNotAny sut = JsonNotAny.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        bool result = sut.AsObject.HasProperty("bar".AsSpan());
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_DotnetBacked_JsonNotAny_SpanByte_NotFound_WrongName()
    {
        JsonNotAny sut = JsonNotAny.Parse("""{"foo": "bar"}""").AsDotnetBackedValue();
        bool result = sut.AsObject.HasProperty(Encoding.UTF8.GetBytes("bar"));
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_DotnetBacked_JsonObject_String_NotFound_EmptyObject()
    {
        JsonObject sut = JsonObject.Parse("{}").AsDotnetBackedValue();
        bool result = sut.HasProperty("foo");
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_DotnetBacked_JsonObject_SpanChar_NotFound_EmptyObject()
    {
        JsonObject sut = JsonObject.Parse("{}").AsDotnetBackedValue();
        bool result = sut.HasProperty("foo".AsSpan());
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_DotnetBacked_JsonObject_SpanByte_NotFound_EmptyObject()
    {
        JsonObject sut = JsonObject.Parse("{}").AsDotnetBackedValue();
        bool result = sut.HasProperty(Encoding.UTF8.GetBytes("foo"));
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_DotnetBacked_JsonAny_String_NotFound_EmptyObject()
    {
        JsonAny sut = JsonAny.Parse("{}").AsDotnetBackedValue();
        bool result = sut.AsObject.HasProperty("foo");
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_DotnetBacked_JsonAny_SpanChar_NotFound_EmptyObject()
    {
        JsonAny sut = JsonAny.Parse("{}").AsDotnetBackedValue();
        bool result = sut.AsObject.HasProperty("foo".AsSpan());
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_DotnetBacked_JsonAny_SpanByte_NotFound_EmptyObject()
    {
        JsonAny sut = JsonAny.Parse("{}").AsDotnetBackedValue();
        bool result = sut.AsObject.HasProperty(Encoding.UTF8.GetBytes("foo"));
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_DotnetBacked_JsonNotAny_String_NotFound_EmptyObject()
    {
        JsonNotAny sut = JsonNotAny.Parse("{}").AsDotnetBackedValue();
        bool result = sut.AsObject.HasProperty("foo");
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_DotnetBacked_JsonNotAny_SpanChar_NotFound_EmptyObject()
    {
        JsonNotAny sut = JsonNotAny.Parse("{}").AsDotnetBackedValue();
        bool result = sut.AsObject.HasProperty("foo".AsSpan());
        Assert.False(result);
    }

    [Fact]
    public void HasProperty_DotnetBacked_JsonNotAny_SpanByte_NotFound_EmptyObject()
    {
        JsonNotAny sut = JsonNotAny.Parse("{}").AsDotnetBackedValue();
        bool result = sut.AsObject.HasProperty(Encoding.UTF8.GetBytes("foo"));
        Assert.False(result);
    }
}