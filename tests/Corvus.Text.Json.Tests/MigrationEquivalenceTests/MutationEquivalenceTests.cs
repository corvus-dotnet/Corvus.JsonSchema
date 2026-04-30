// Copyright (c) William Adams. All rights reserved.
// Licensed under the MIT License.

namespace Corvus.Text.Json.Tests.MigrationEquivalenceTests;

using Corvus.Text.Json.Tests.MigrationModels.V5;
using Xunit;

using V4 = MigrationModels.V4;
using V5 = MigrationModels.V5;

/// <summary>
/// Verifies that V4 functional mutation and V5 imperative mutation produce equivalent JSON results.
/// </summary>
/// <remarks>
/// <para>V4: <c>MyType.Create(prop1: val1, ...)</c> — functional, returns new instance.</para>
/// <para>V5 (imperative): <c>mutable.SetProp(source)</c> — in-place via <see cref="JsonWorkspace"/>.</para>
/// <para>V5 (builder): <c>CreateBuilder(workspace, (ref Builder b) =&gt; b.Create(...))</c> — creates from scratch.</para>
/// </remarks>
public class MutationEquivalenceTests
{
    private const string PersonJson = """{"name":"Jo","age":30,"email":"jo@example.com","isActive":true}""";
    private const string NestedJson = """{"name":"Jo","address":{"city":"London","street":"123 Main St","zipCode":"SW1A 1AA"}}""";

    [Fact]
    public void V4_SetPropertyByMutable()
    {
        var v4 = V4.MigrationPerson.Parse(PersonJson);
        V4.MigrationPerson updated = v4.WithName("Bob");
        Assert.Equal("Bob", (string)updated.Name);
        Assert.Equal(30, (int)updated.Age);
    }

    [Fact]
    public void V4_SetPropertyByMutable_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        V4.MigrationPerson v4 = parsedV4.Instance;
        V4.MigrationPerson updated = v4.WithName("Bob");
        Assert.Equal("Bob", (string)updated.Name);
        Assert.Equal(30, (int)updated.Age);
    }

    [Fact]
    public void V5_SetPropertyByMutable()
    {
        // V5 imperative approach: parse, build mutable, then SetXxx
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using var doc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        using JsonDocumentBuilder<V5.MigrationPerson.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        V5.MigrationPerson.Mutable root = builder.RootElement;

        root.SetName("Bob");
        Assert.Equal("Bob", (string)root.Name);
        Assert.Equal(30, (int)root.Age);
    }

    [Fact]
    public void BothEngines_SetProperty_SameResult()
    {
        // V4: functional WithName() returns a new instance
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        V4.MigrationPerson v4 = parsedV4.Instance;
        V4.MigrationPerson v4Updated = v4.WithName("Bob");

        // V5: imperative SetName() mutates in place
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using var doc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        using JsonDocumentBuilder<V5.MigrationPerson.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        V5.MigrationPerson.Mutable root = builder.RootElement;
        root.SetName("Bob");

        Assert.Equal((string)v4Updated.Name, (string)root.Name);
        Assert.Equal((int)v4Updated.Age, (int)root.Age);
        Assert.Equal((string)v4Updated.Email, (string)root.Email);
    }

    [Fact]
    public void V4_CreateFromScratch()
    {
        // V4: Create from scratch using implicit conversions from primitives.
        var v4 = V4.MigrationPerson.Create(
            name: "Alice",
            age: 30,
            email: "alice@test.com");

        Assert.Equal("Alice", (string)v4.Name);
        Assert.Equal(30, (int)v4.Age);
        Assert.Equal("alice@test.com", (string)v4.Email);
    }

    [Fact]
    public void V5_CreateFromScratch()
    {
        // V5: Create from scratch using builder with implicit conversions from primitives.
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using JsonDocumentBuilder<V5.MigrationPerson.Mutable> builder =
            V5.MigrationPerson.CreateBuilder(
                workspace,
                (ref b) => b.Create(
                    name: "Alice",
                    age: 30,
                    email: "alice@test.com"));
        V5.MigrationPerson.Mutable root = builder.RootElement;

        Assert.Equal("Alice", (string)root.Name);
        Assert.Equal(30, (int)root.Age);
        Assert.Equal("alice@test.com", (string)root.Email);
    }

    [Fact]
    public void BothEngines_CreateFromScratch_SameResult()
    {
        // V4: Create from scratch with implicit conversions from primitives
        var v4 = V4.MigrationPerson.Create(
            name: "Alice",
            age: 30,
            email: "alice@test.com");

        // V5: Create from scratch using builder with implicit conversions
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using JsonDocumentBuilder<V5.MigrationPerson.Mutable> builder =
            V5.MigrationPerson.CreateBuilder(
                workspace,
                (ref b) => b.Create(
                    name: "Alice",
                    age: 30,
                    email: "alice@test.com"));
        V5.MigrationPerson.Mutable root = builder.RootElement;

        Assert.Equal((string)v4.Name, (string)root.Name);
        Assert.Equal((int)v4.Age, (int)root.Age);
        Assert.Equal((string)v4.Email, (string)root.Email);
    }

    [Fact]
    public void V4_RemoveOptionalProperty()
    {
        var v4 = V4.MigrationPerson.Parse(PersonJson);
        // V4: WithEmail(default) removes the optional property — equivalent to V5 RemoveEmail().
        V4.MigrationPerson updated = v4.WithEmail(default);

        Assert.Equal(System.Text.Json.JsonValueKind.Undefined, updated.Email.ValueKind);
        Assert.Equal("Jo", (string)updated.Name);
    }

    [Fact]
    public void V4_RemoveOptionalProperty_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        V4.MigrationPerson v4 = parsedV4.Instance;
        // V4: WithEmail(default) removes the optional property — equivalent to V5 RemoveEmail().
        V4.MigrationPerson updated = v4.WithEmail(default);

        Assert.Equal(System.Text.Json.JsonValueKind.Undefined, updated.Email.ValueKind);
        Assert.Equal("Jo", (string)updated.Name);
    }

    [Fact]
    public void V5_RemoveOptionalProperty()
    {
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using var doc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        using JsonDocumentBuilder<V5.MigrationPerson.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        V5.MigrationPerson.Mutable root = builder.RootElement;

        bool removed = root.RemoveEmail();
        Assert.True(removed);
        Assert.True(root.Email.IsUndefined());
    }

    [Fact]
    public void V5_RemoveNonExistentProperty_ReturnsFalse()
    {
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using var doc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse("""{"name":"Jo","age":30}""");
        using JsonDocumentBuilder<V5.MigrationPerson.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        V5.MigrationPerson.Mutable root = builder.RootElement;

        bool removed = root.RemoveEmail();
        Assert.False(removed);
    }

    [Fact]
    public void V4_CreateWithOptionalOmitted()
    {
        // V4: omit optional parameters from Create.
        var v4 = V4.MigrationPerson.Create(
            name: "Jo",
            age: 30);

        Assert.Equal(System.Text.Json.JsonValueKind.Undefined, v4.Email.ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Undefined, v4.IsActive.ValueKind);
    }

    [Fact]
    public void V5_CreateWithOptionalOmitted()
    {
        // V5: omit optional parameters from Create.
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using JsonDocumentBuilder<V5.MigrationPerson.Mutable> builder =
            V5.MigrationPerson.CreateBuilder(
                workspace,
                static (ref b) => b.Create(
                    name: "Jo",
                    age: 30));

        V5.MigrationPerson result = builder.RootElement;
        Assert.True(result.Email.IsUndefined());
        Assert.True(result.IsActive.IsUndefined());
    }

    [Fact]
    public void V4_SetOptionalProperty()
    {
        var v4 = V4.MigrationPerson.Parse("""{"name":"Jo","age":30}""");
        // V4: WithEmail() sets the optional property — equivalent to V5 SetEmail().
        V4.MigrationPerson updated = v4.WithEmail("test@test.com");

        Assert.Equal(System.Text.Json.JsonValueKind.String, updated.Email.ValueKind);
    }

    [Fact]
    public void V4_SetOptionalProperty_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse("""{"name":"Jo","age":30}""");
        V4.MigrationPerson v4 = parsedV4.Instance;
        // V4: WithEmail() sets the optional property — equivalent to V5 SetEmail().
        V4.MigrationPerson updated = v4.WithEmail("test@test.com");

        Assert.Equal(System.Text.Json.JsonValueKind.String, updated.Email.ValueKind);
    }

    [Fact]
    public void V5_SetOptionalProperty()
    {
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using var doc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse("""{"name":"Jo","age":30}""");
        using JsonDocumentBuilder<V5.MigrationPerson.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        V5.MigrationPerson.Mutable root = builder.RootElement;

        root.SetEmail("test@test.com");
        Assert.False(root.Email.IsUndefined());
    }

    [Fact]
    public void V4_MutationRoundTrip()
    {
        var v4 = V4.MigrationPerson.Parse(PersonJson);
        var updated = V4.MigrationPerson.Create(
            name: "Alice",
            age: v4.Age);

        string json = updated.ToString();
        var reparsed = V4.MigrationPerson.Parse(json);
        Assert.Equal("Alice", (string)reparsed.Name);
        Assert.Equal(30, (int)reparsed.Age);
    }

    [Fact]
    public void V4_MutationRoundTrip_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        V4.MigrationPerson v4 = parsedV4.Instance;
        var updated = V4.MigrationPerson.Create(
            name: "Alice",
            age: v4.Age);

        string json = updated.ToString();
        using var reparsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(json);
        V4.MigrationPerson reparsed = reparsedV4.Instance;
        Assert.Equal("Alice", (string)reparsed.Name);
        Assert.Equal(30, (int)reparsed.Age);
    }

    [Fact]
    public void V5_MutationRoundTrip_Imperative()
    {
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using var doc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        using JsonDocumentBuilder<V5.MigrationPerson.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        V5.MigrationPerson.Mutable root = builder.RootElement;
        root.SetName("Alice");
        root.RemoveEmail();
        root.RemoveIsActive();

        string json = root.ToString();
        using var parsedV5Reparsed = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(json);
        V5.MigrationPerson reparsed = parsedV5Reparsed.RootElement;
        Assert.Equal("Alice", (string)reparsed.Name);
        Assert.Equal(30, (int)reparsed.Age);
    }

    [Fact]
    public void V5_MutationRoundTrip_BuilderCreate()
    {
        // V5 builder approach: equivalent to V4 Create round-trip
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using JsonDocumentBuilder<V5.MigrationPerson.Mutable> builder =
            V5.MigrationPerson.CreateBuilder(
                workspace,
                static (ref b) => b.Create(
                    name: "Alice",
                    age: 30));

        string json = builder.RootElement.ToString();
        using var parsedV5Reparsed = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(json);
        V5.MigrationPerson reparsed = parsedV5Reparsed.RootElement;
        Assert.Equal("Alice", (string)reparsed.Name);
        Assert.Equal(30, (int)reparsed.Age);
    }

    [Fact]
    public void V4_SetPropertyByRecreation()
    {
        var v4 = V4.MigrationPerson.Parse(PersonJson);
        var updated = V4.MigrationPerson.Create(
            name: V4.MigrationPerson.NameEntity.Parse("\"Bob\""),
            age: v4.Age,
            email: v4.Email,
            isActive: v4.IsActive);
        Assert.Equal("Bob", (string)updated.Name);
        Assert.Equal(30, (int)updated.Age);
    }

    [Fact]
    public void V4_SetPropertyByRecreation_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        V4.MigrationPerson v4 = parsedV4.Instance;
        var updated = V4.MigrationPerson.Create(
            name: V4.MigrationPerson.NameEntity.Parse("\"Bob\""),
            age: v4.Age,
            email: v4.Email,
            isActive: v4.IsActive);
        Assert.Equal("Bob", (string)updated.Name);
        Assert.Equal(30, (int)updated.Age);
    }

    [Fact]
    public void V4_SetNestedProperty()
    {
        // V4: changing a nested property requires rebuilding the entire path from root.
        var v4 = V4.MigrationNested.Parse(NestedJson);
        V4.MigrationNested updated = v4.WithAddress(v4.Address.WithCity("NYC"));
        Assert.Equal("NYC", (string)updated.Address.City);
        Assert.Equal("123 Main St", (string)updated.Address.Street);
        Assert.Equal("Jo", (string)updated.Name);
    }

    [Fact]
    public void V4_SetNestedProperty_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationNested>.Parse(NestedJson);
        V4.MigrationNested v4 = parsedV4.Instance;
        V4.MigrationNested updated = v4.WithAddress(v4.Address.WithCity("NYC"));
        Assert.Equal("NYC", (string)updated.Address.City);
        Assert.Equal("123 Main St", (string)updated.Address.Street);
        Assert.Equal("Jo", (string)updated.Name);
    }

    [Fact]
    public void V5_SetNestedProperty()
    {
        // V5: direct mutation of nested elements — no need to rebuild the path from root.
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using var doc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationNested>.Parse(NestedJson);
        using JsonDocumentBuilder<V5.MigrationNested.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        V5.MigrationNested.Mutable root = builder.RootElement;

        root.Address.SetCity("NYC");
        Assert.Equal("NYC", (string)root.Address.City);
        Assert.Equal("123 Main St", (string)root.Address.Street);
        Assert.Equal("Jo", (string)root.Name);
    }

    [Fact]
    public void BothEngines_SetNestedProperty_SameResult()
    {
        // V4: rebuilding the path from root
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationNested>.Parse(NestedJson);
        V4.MigrationNested v4 = parsedV4.Instance;
        V4.MigrationNested v4Updated = v4.WithAddress(v4.Address.WithCity("NYC"));

        // V5: direct nested mutation
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using var doc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationNested>.Parse(NestedJson);
        using JsonDocumentBuilder<V5.MigrationNested.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        V5.MigrationNested.Mutable root = builder.RootElement;
        root.Address.SetCity("NYC");

        Assert.Equal((string)v4Updated.Name, (string)root.Name);
        Assert.Equal((string)v4Updated.Address.City, (string)root.Address.City);
        Assert.Equal((string)v4Updated.Address.Street, (string)root.Address.Street);
    }

    [Fact]
    public void V4_RemovePropertyByName()
    {
        // V4: functional RemoveProperty(string) returns new object without that property.
        var v4 = V4.MigrationPerson.Parse(PersonJson);
        V4.MigrationPerson updated = v4.RemoveProperty("email");
        Assert.Equal(System.Text.Json.JsonValueKind.Undefined, updated.Email.ValueKind);
        Assert.Equal("Jo", (string)updated.Name);
    }

    [Fact]
    public void V4_RemovePropertyByName_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        V4.MigrationPerson v4 = parsedV4.Instance;
        V4.MigrationPerson updated = v4.RemoveProperty("email");
        Assert.Equal(System.Text.Json.JsonValueKind.Undefined, updated.Email.ValueKind);
        Assert.Equal("Jo", (string)updated.Name);
    }

    [Fact]
    public void V5_RemovePropertyByName()
    {
        // V5: imperative RemoveProperty(string) on the mutable type.
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using var doc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        using JsonDocumentBuilder<V5.MigrationPerson.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        V5.MigrationPerson.Mutable root = builder.RootElement;

        bool removed = root.RemoveProperty("email");
        Assert.True(removed);
        Assert.True(root.Email.IsUndefined());
        Assert.Equal("Jo", (string)root.Name);
    }

    [Fact]
    public void V4_SetPropertyByName()
    {
        // V4: functional SetProperty<TValue>(name, value) — sets or adds a property by name.
        var v4 = V4.MigrationPerson.Parse(PersonJson);
        V4.MigrationPerson updated = v4.SetProperty("email", Corvus.Json.JsonAny.Parse("\"new@test.com\""));
        Assert.Equal("new@test.com", (string)updated.Email);
    }

    [Fact]
    public void V4_SetPropertyByName_ParsedValue()
    {
        // Preferred V4 pattern: ParsedValue<T> manages the underlying JsonDocument lifetime.
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPerson>.Parse(PersonJson);
        V4.MigrationPerson v4 = parsedV4.Instance;
        V4.MigrationPerson updated = v4.SetProperty("email", Corvus.Json.JsonAny.Parse("\"new@test.com\""));
        Assert.Equal("new@test.com", (string)updated.Email);
    }

    [Fact]
    public void V5_SetPropertyByName()
    {
        // V5: imperative SetProperty(string, value) on the mutable type.
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using var doc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPerson>.Parse(PersonJson);
        using JsonDocumentBuilder<V5.MigrationPerson.Mutable> builder = doc.RootElement.CreateBuilder(workspace);
        V5.MigrationPerson.Mutable root = builder.RootElement;

        root.SetProperty("email", "new@test.com");
        Assert.Equal("new@test.com", (string)root.Email);
    }

    [Fact]
    public void V4_BuildNestedObject()
    {
        // V4: compose nested values with Create()
        var v4 = V4.MigrationNested.Create(
            address: V4.MigrationNested.RequiredCityAndStreet.Create(
                city: "London",
                street: "221B Baker Street",
                zipCode: "12345"),
            name: "Sherlock");

        Assert.Equal("Sherlock", (string)v4.Name);
        Assert.Equal("London", (string)v4.Address.City);
        Assert.Equal("221B Baker Street", (string)v4.Address.Street);
        Assert.Equal("12345", (string)v4.Address.ZipCode);
    }

    [Fact]
    public void V5_BuildNestedObject()
    {
        // V5: compose nested values with Build() and CreateBuilder()
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using JsonDocumentBuilder<V5.MigrationNested.Mutable> builder =
            V5.MigrationNested.CreateBuilder(
                workspace,
                (ref b) => b.Create(
                    address: V5.MigrationNested.RequiredCityAndStreet.Build(
                        (ref ab) => ab.Create(
                            city: "London",
                            street: "221B Baker Street",
                            zipCode: "12345")),
                    name: "Sherlock"));

        V5.MigrationNested.Mutable root = builder.RootElement;
        Assert.Equal("Sherlock", (string)root.Name);
        Assert.Equal("London", (string)root.Address.City);
        Assert.Equal("221B Baker Street", (string)root.Address.Street);
        Assert.Equal("12345", (string)root.Address.ZipCode);
    }

    [Fact]
    public void BothEngines_BuildNestedObject_SameResult()
    {
        // V4
        var v4 = V4.MigrationNested.Create(
            address: V4.MigrationNested.RequiredCityAndStreet.Create(
                city: "London",
                street: "221B Baker Street",
                zipCode: "12345"),
            name: "Sherlock");

        // V5
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using JsonDocumentBuilder<V5.MigrationNested.Mutable> builder =
            V5.MigrationNested.CreateBuilder(
                workspace,
                (ref b) => b.Create(
                    address: V5.MigrationNested.RequiredCityAndStreet.Build(
                        (ref ab) => ab.Create(
                            city: "London",
                            street: "221B Baker Street",
                            zipCode: "12345")),
                    name: "Sherlock"));
        V5.MigrationNested.Mutable root = builder.RootElement;

        Assert.Equal((string)v4.Name, (string)root.Name);
        Assert.Equal((string)v4.Address.City, (string)root.Address.City);
        Assert.Equal((string)v4.Address.Street, (string)root.Address.Street);
        Assert.Equal((string)v4.Address.ZipCode, (string)root.Address.ZipCode);
    }

    [Fact]
    public void V4_BuildArrayOfObjects()
    {
        // V4: build array with FromItems()
        var v4 = V4.MigrationItemArray.FromItems(
            V4.MigrationItemArray.RequiredId.Create(id: 1, label: "First"),
            V4.MigrationItemArray.RequiredId.Create(id: 2, label: "Second"),
            V4.MigrationItemArray.RequiredId.Create(id: 3));

        Assert.Equal(3, v4.GetArrayLength());
        Assert.Equal(1, (int)v4[0].Id);
        Assert.Equal("First", (string)v4[0].Label);
        Assert.Equal(2, (int)v4[1].Id);
        Assert.Equal("Second", (string)v4[1].Label);
        Assert.Equal(3, (int)v4[2].Id);
    }

    [Fact]
    public void V5_BuildArrayOfObjects()
    {
        // V5: build array with AddItem() inside Build() callback, then CreateBuilder()
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using JsonDocumentBuilder<V5.MigrationItemArray.Mutable> builder =
            V5.MigrationItemArray.CreateBuilder(
                workspace,
                V5.MigrationItemArray.Build(
                    (ref b) =>
                    {
                        b.AddItem(V5.MigrationItemArray.RequiredId.Build(
                            (ref ib) => ib.Create(id: 1, label: "First")));
                        b.AddItem(V5.MigrationItemArray.RequiredId.Build(
                            (ref ib) => ib.Create(id: 2, label: "Second")));
                        b.AddItem(V5.MigrationItemArray.RequiredId.Build(
                            (ref ib) => ib.Create(id: 3)));
                    }));

        V5.MigrationItemArray.Mutable root = builder.RootElement;
        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(1, (int)root[0].Id);
        Assert.Equal("First", (string)root[0].Label);
        Assert.Equal(2, (int)root[1].Id);
        Assert.Equal("Second", (string)root[1].Label);
        Assert.Equal(3, (int)root[2].Id);
    }

    [Fact]
    public void BothEngines_BuildArrayOfObjects_SameResult()
    {
        // V4
        var v4 = V4.MigrationItemArray.FromItems(
            V4.MigrationItemArray.RequiredId.Create(id: 1, label: "First"),
            V4.MigrationItemArray.RequiredId.Create(id: 2, label: "Second"),
            V4.MigrationItemArray.RequiredId.Create(id: 3));

        // V5
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using JsonDocumentBuilder<V5.MigrationItemArray.Mutable> builder =
            V5.MigrationItemArray.CreateBuilder(
                workspace,
                V5.MigrationItemArray.Build(
                    (ref b) =>
                    {
                        b.AddItem(V5.MigrationItemArray.RequiredId.Build(
                            (ref ib) => ib.Create(id: 1, label: "First")));
                        b.AddItem(V5.MigrationItemArray.RequiredId.Build(
                            (ref ib) => ib.Create(id: 2, label: "Second")));
                        b.AddItem(V5.MigrationItemArray.RequiredId.Build(
                            (ref ib) => ib.Create(id: 3)));
                    }));
        V5.MigrationItemArray.Mutable root = builder.RootElement;

        Assert.Equal(v4.GetArrayLength(), root.GetArrayLength());
        for (int i = 0; i < v4.GetArrayLength(); i++)
        {
            Assert.Equal((int)v4[i].Id, (int)root[i].Id);
        }

        Assert.Equal((string)v4[0].Label, (string)root[0].Label);
        Assert.Equal((string)v4[1].Label, (string)root[1].Label);
    }

    /// <summary>
    /// Verifies that a cached V5 root element remains valid across mutations to different
    /// child properties. The root element is always live (index 0, never relocated).
    /// </summary>
    [Fact]
    public void V5_CachedRootElement_MultiplePropertyMutations()
    {
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();
        using var doc = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationNested>.Parse(NestedJson);
        using JsonDocumentBuilder<MigrationNested.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        V5.MigrationNested.Mutable root = builder.RootElement;

        // Navigate from root to the nested Address child and mutate.
        root.Address.SetCity("NYC");

        // Root is always live — we can still navigate from it.
        root.SetName("Bob");

        // Navigate from root to Address again and mutate another property.
        root.Address.SetStreet("456 Broadway");

        // Verify all changes.
        Assert.Equal("Bob", (string)root.Name);
        Assert.Equal("NYC", (string)root.Address.City);
        Assert.Equal("456 Broadway", (string)root.Address.Street);
    }
}