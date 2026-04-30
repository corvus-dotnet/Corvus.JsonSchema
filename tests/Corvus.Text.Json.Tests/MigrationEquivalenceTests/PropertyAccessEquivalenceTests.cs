// Copyright (c) William Adams. All rights reserved.
// Licensed under the MIT License.

namespace Corvus.Text.Json.Tests.MigrationEquivalenceTests;

using Corvus.Json;
using Xunit;

using V4 = MigrationModels.V4;
using V5 = MigrationModels.V5;

/// <summary>
/// Verifies that V4 and V5 property access produces equivalent results.
/// </summary>
/// <remarks>
/// <para>V4: <c>person.Name</c> returns <c>NameEntity</c>; <c>person.Count</c>; <c>person.TryGetProperty(name, out var)</c></para>
/// <para>V5: <c>person.Name</c> returns <c>NameEntity</c>; <c>person.GetPropertyCount()</c>; <c>person["name"]</c> indexer</para>
/// </remarks>
public class PropertyAccessEquivalenceTests
{
    private const string PersonJson = """{"name":"Jo","age":30,"email":"jo@example.com","isActive":true,"dateOfBirth":"1990-01-15"}""";
    private const string MinimalJson = """{"name":"Jo","age":30}""";

    [Fact]
    public void V4_ReadRequiredStringProperty()
    {
        var v4 = V4.MigrationPerson.Parse(PersonJson);
        string name = (string)v4.Name;
        Assert.Equal("Jo", name);
    }

    [Fact]
    public void V4_ReadRequiredStringProperty_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        V4.MigrationPerson v4 = parsedV4.Instance;
        string name = (string)v4.Name;
        Assert.Equal("Jo", name);
    }

    [Fact]
    public void V5_ReadRequiredStringProperty()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;
        string name = (string)v5.Name;
        Assert.Equal("Jo", name);
    }

    [Fact]
    public void V4_ReadRequiredIntProperty()
    {
        var v4 = V4.MigrationPerson.Parse(PersonJson);
        int age = (int)v4.Age;
        Assert.Equal(30, age);
    }

    [Fact]
    public void V4_ReadRequiredIntProperty_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        V4.MigrationPerson v4 = parsedV4.Instance;
        int age = (int)v4.Age;
        Assert.Equal(30, age);
    }

    [Fact]
    public void V5_ReadRequiredIntProperty()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;
        Assert.Equal(30, (int)v5.Age);
    }

    [Fact]
    public void V4_ReadOptionalBoolProperty()
    {
        var v4 = V4.MigrationPerson.Parse(PersonJson);
        bool isActive = (bool)v4.IsActive;
        Assert.True(isActive);
    }

    [Fact]
    public void V4_ReadOptionalBoolProperty_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        V4.MigrationPerson v4 = parsedV4.Instance;
        bool isActive = (bool)v4.IsActive;
        Assert.True(isActive);
    }

    [Fact]
    public void V5_ReadOptionalBoolProperty()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;
        bool isActive = (bool)v5.IsActive;
        Assert.True(isActive);
    }

    [Fact]
    public void V4_CheckUndefinedOptionalProperty()
    {
        var v4 = V4.MigrationPerson.Parse(MinimalJson);
        Assert.Equal(System.Text.Json.JsonValueKind.Undefined, v4.Email.ValueKind);
    }

    [Fact]
    public void V4_CheckUndefinedOptionalProperty_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(MinimalJson);
        V4.MigrationPerson v4 = parsedV4.Instance;
        Assert.Equal(System.Text.Json.JsonValueKind.Undefined, v4.Email.ValueKind);
    }

    [Fact]
    public void V5_CheckUndefinedOptionalProperty()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(MinimalJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;
        Assert.True(v5.Email.IsUndefined());
    }

    [Fact]
    public void V4_CountProperties()
    {
        var v4 = V4.MigrationPerson.Parse(PersonJson);
        // V4 uses Count property from IJsonObject<T>
        Assert.Equal(5, v4.Count);
    }

    [Fact]
    public void V4_CountProperties_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        V4.MigrationPerson v4 = parsedV4.Instance;
        // V4 uses Count property from IJsonObject<T>
        Assert.Equal(5, v4.Count);
    }

    [Fact]
    public void V5_CountProperties()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;
        // V5 uses GetPropertyCount() method
        Assert.Equal(5, v5.GetPropertyCount());
    }

    [Fact]
    public void V4_TryGetPropertyByName()
    {
        var v4 = V4.MigrationPerson.Parse(PersonJson);
        // V4: TryGetProperty returns bool, out is JsonAny
        Assert.True(v4.TryGetProperty("name", out JsonAny value));
        Assert.Equal(System.Text.Json.JsonValueKind.String, value.ValueKind);
    }

    [Fact]
    public void V4_TryGetPropertyByName_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        V4.MigrationPerson v4 = parsedV4.Instance;
        // V4: TryGetProperty returns bool, out is JsonAny
        Assert.True(v4.TryGetProperty("name", out JsonAny value));
        Assert.Equal(System.Text.Json.JsonValueKind.String, value.ValueKind);
    }

    [Fact]
    public void V5_GetPropertyByName()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;
        // V5: string indexer returns JsonElement (undefined if missing)
        Corvus.Text.Json.JsonElement nameEl = v5["name"];
        Assert.True(nameEl.IsNotUndefined());
        Assert.Equal(Corvus.Text.Json.JsonValueKind.String, nameEl.ValueKind);
    }

    [Fact]
    public void V5_TryGetPropertyByName()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;
        // V5: TryGetProperty returns bool, out is JsonElement — direct parity with V4
        Assert.True(v5.TryGetProperty("name", out Corvus.Text.Json.JsonElement value));
        Assert.Equal(Corvus.Text.Json.JsonValueKind.String, value.ValueKind);
    }

    [Fact]
    public void V4_TryGetPropertyByName_NotFound()
    {
        var v4 = V4.MigrationPerson.Parse(MinimalJson);
        Assert.False(v4.TryGetProperty("email", out _));
    }

    [Fact]
    public void V4_TryGetPropertyByName_NotFound_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(MinimalJson);
        V4.MigrationPerson v4 = parsedV4.Instance;
        Assert.False(v4.TryGetProperty("email", out _));
    }

    [Fact]
    public void V5_GetPropertyByName_NotFound()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(MinimalJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;
        // V5: indexer returns undefined JsonElement for missing properties
        Corvus.Text.Json.JsonElement emailEl = v5["email"];
        Assert.True(emailEl.IsUndefined());
    }

    [Fact]
    public void V5_TryGetPropertyByName_NotFound()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(MinimalJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;
        // V5: TryGetProperty returns false for missing properties — direct parity with V4
        Assert.False(v5.TryGetProperty("email", out _));
    }

    [Fact]
    public void V4_EnumerateProperties()
    {
        var v4 = V4.MigrationPerson.Parse(PersonJson);
        int count = 0;
        foreach (JsonObjectProperty prop in v4.EnumerateObject())
        {
            count++;
        }

        Assert.Equal(5, count);
    }

    [Fact]
    public void V4_EnumerateProperties_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        V4.MigrationPerson v4 = parsedV4.Instance;
        int count = 0;
        foreach (JsonObjectProperty prop in v4.EnumerateObject())
        {
            count++;
        }

        Assert.Equal(5, count);
    }

    [Fact]
    public void V5_EnumerateProperties()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;
        int count = 0;
        foreach (JsonProperty<JsonElement> prop in v5.EnumerateObject())
        {
            count++;
        }

        Assert.Equal(5, count);
    }

    [Fact]
    public void V4_ReadValueKind()
    {
        var v4 = V4.MigrationPerson.Parse(PersonJson);
        Assert.Equal(System.Text.Json.JsonValueKind.Object, v4.ValueKind);

        var v4Array = V4.MigrationItemArray.Parse("[]");
        Assert.Equal(System.Text.Json.JsonValueKind.Array, v4Array.ValueKind);
    }

    [Fact]
    public void V4_ReadValueKind_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        V4.MigrationPerson v4 = parsedV4.Instance;
        Assert.Equal(System.Text.Json.JsonValueKind.Object, v4.ValueKind);

        using var parsedV4Array = Corvus.Json.ParsedValue<V4.MigrationItemArray>.Parse("[]");
        V4.MigrationItemArray v4Array = parsedV4Array.Instance;
        Assert.Equal(System.Text.Json.JsonValueKind.Array, v4Array.ValueKind);
    }

    [Fact]
    public void V5_ReadValueKind()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;
        Assert.Equal(Corvus.Text.Json.JsonValueKind.Object, v5.ValueKind);

        using var parsedV5Array = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationItemArray>.Parse("[]");
        V5.MigrationItemArray v5Array = parsedV5Array.RootElement;
        Assert.Equal(Corvus.Text.Json.JsonValueKind.Array, v5Array.ValueKind);
    }

    [Fact]
    public void V5_TryGetProperty_ExistingProperty()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;
        Assert.True(v5.TryGetProperty("name", out Corvus.Text.Json.JsonElement value));
        Assert.Equal(Corvus.Text.Json.JsonValueKind.String, value.ValueKind);
        Assert.Equal("Jo", value.GetString());
    }

    [Fact]
    public void V5_TryGetProperty_MissingProperty()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(MinimalJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;
        Assert.False(v5.TryGetProperty("nonexistent", out _));
    }

    [Fact]
    public void BothEngines_TryGetProperty_SameResult()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        V4.MigrationPerson v4 = parsedV4.Instance;
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;

        Assert.True(v4.TryGetProperty("name", out JsonAny v4Value));
        Assert.True(v5.TryGetProperty("name", out Corvus.Text.Json.JsonElement v5Value));

        Assert.Equal((string)v4Value.AsString, v5Value.GetString());
    }

    [Fact]
    public void BothEngines_PropertyCount_SameResult()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        V4.MigrationPerson v4 = parsedV4.Instance;
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;

        // V4 uses .Count, V5 uses .GetPropertyCount()
        Assert.Equal(v4.Count, v5.GetPropertyCount());
    }

    [Fact]
    public void V4_HasProperty_Exists()
    {
        var v4 = V4.MigrationPerson.Parse(PersonJson);
        Assert.True(v4.HasProperty("name"));
        Assert.True(v4.HasProperty("age"));
    }

    [Fact]
    public void V4_HasProperty_Exists_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        V4.MigrationPerson v4 = parsedV4.Instance;
        Assert.True(v4.HasProperty("name"));
        Assert.True(v4.HasProperty("age"));
    }

    [Fact]
    public void V5_HasProperty_ViaTryGetProperty()
    {
        // V5 does not have HasProperty() — use TryGetProperty() instead.
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;
        Assert.True(v5.TryGetProperty("name", out _));
        Assert.True(v5.TryGetProperty("age", out _));
    }

    [Fact]
    public void V4_HasProperty_Missing()
    {
        var v4 = V4.MigrationPerson.Parse(MinimalJson);
        Assert.False(v4.HasProperty("email"));
        Assert.False(v4.HasProperty("nonexistent"));
    }

    [Fact]
    public void V4_HasProperty_Missing_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(MinimalJson);
        V4.MigrationPerson v4 = parsedV4.Instance;
        Assert.False(v4.HasProperty("email"));
        Assert.False(v4.HasProperty("nonexistent"));
    }

    [Fact]
    public void V5_HasProperty_Missing_ViaTryGetProperty()
    {
        // V5: TryGetProperty returns false for missing properties — equivalent to V4 HasProperty().
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(MinimalJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;
        Assert.False(v5.TryGetProperty("email", out _));
        Assert.False(v5.TryGetProperty("nonexistent", out _));
    }

    [Fact]
    public void V4_HasProperties()
    {
        var v4 = V4.MigrationPerson.Parse(PersonJson);
        Assert.True(v4.HasProperties());
    }

    [Fact]
    public void V4_HasProperties_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        V4.MigrationPerson v4 = parsedV4.Instance;
        Assert.True(v4.HasProperties());
    }

    [Fact]
    public void V5_HasProperties_ViaPropertyCount()
    {
        // V5 does not have HasProperties() — use GetPropertyCount() > 0 instead.
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;
        Assert.True(v5.GetPropertyCount() > 0);
    }

    [Fact]
    public void V4_AsAny()
    {
        // V4: AsAny returns a JsonAny (the V4 "any" type).
        var v4 = V4.MigrationPerson.Parse(PersonJson);
        JsonAny any = v4.AsAny;
        Assert.Equal(System.Text.Json.JsonValueKind.Object, any.ValueKind);
    }

    [Fact]
    public void V4_AsAny_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        V4.MigrationPerson v4 = parsedV4.Instance;
        JsonAny any = v4.AsAny;
        Assert.Equal(System.Text.Json.JsonValueKind.Object, any.ValueKind);
    }

    [Fact]
    public void V5_AsJsonElement()
    {
        // V5: implicit operator JsonElement (V5's JsonElement = V4's JsonAny).
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;
        Corvus.Text.Json.JsonElement element = v5;
        Assert.Equal(Corvus.Text.Json.JsonValueKind.Object, element.ValueKind);
    }

#if NET
    [Fact]
    public void V5_TryGetProperty_Utf8()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;
        Assert.True(v5.TryGetProperty("name"u8, out Corvus.Text.Json.JsonElement value));
        Assert.Equal("Jo", value.GetString());
    }

    [Fact]
    public void V5_PropertyIndexerByString()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;
        Corvus.Text.Json.JsonElement nameEl = v5["name"];
        Assert.Equal(Corvus.Text.Json.JsonValueKind.String, nameEl.ValueKind);
    }

    [Fact]
    public void V5_PropertyIndexerByUtf8()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;
        Corvus.Text.Json.JsonElement nameEl = v5["name"u8];
        Assert.Equal(Corvus.Text.Json.JsonValueKind.String, nameEl.ValueKind);
    }
#endif

    [Fact]
    public void V4_GetString_OnStringProperty()
    {
        // V4: GetString() returns the string value from a string-typed property.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        V4.MigrationPerson v4 = parsedV4.Instance;
        string? name = v4.Name.GetString();
        Assert.Equal("Jo", name);
    }

    [Fact]
    public void V5_GetString_OnStringProperty()
    {
        // V5: GetString() is also available directly on the string-typed property.
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;
        string? name = v5.Name.GetString();
        Assert.Equal("Jo", name);
    }

    [Fact]
    public void V5_GetUtf8String_OnStringProperty()
    {
        // V5: GetUtf8String() returns the unescaped UTF-8 bytes without allocating a string.
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;
        using Corvus.Text.Json.UnescapedUtf8JsonString utf8 = v5.Name.GetUtf8String();
        Assert.True(utf8.Span.SequenceEqual("Jo"u8));
    }

    [Fact]
    public void V5_GetUtf8String_MatchesGetString()
    {
        // V5: GetUtf8String().Span transcoded to string matches GetString().
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        V5.MigrationPerson v5 = parsedV5.RootElement;
        string? fromGetString = v5.Name.GetString();
        using Corvus.Text.Json.UnescapedUtf8JsonString utf8 = v5.Name.GetUtf8String();
        string fromUtf8 = JsonReaderHelper.TranscodeHelper(utf8.Span);
        Assert.Equal(fromGetString, fromUtf8);
    }

    [Fact]
    public void BothEngines_GetString_SameResult()
    {
        // Both V4 and V5 GetString() on a string property produce the same value.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        Assert.Equal(parsedV4.Instance.Name.GetString(), parsedV5.RootElement.Name.GetString());
    }

    [Fact]
    public void V5_GetUtf8String_WithEscapedCharacters()
    {
        // Verifies that GetUtf8String() correctly unescapes JSON string escapes.
        string json = """{"name":"Jo\u0026Co","age":30}""";
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(json);
        V5.MigrationPerson v5 = parsedV5.RootElement;
        using Corvus.Text.Json.UnescapedUtf8JsonString utf8 = v5.Name.GetUtf8String();
        Assert.True(utf8.Span.SequenceEqual("Jo&Co"u8));
    }
}