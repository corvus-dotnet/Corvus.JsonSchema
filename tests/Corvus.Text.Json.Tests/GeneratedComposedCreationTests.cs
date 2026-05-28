// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for Build/Create patterns on composed object and array types.
/// Covers inline allOf, $ref-based allOf, and existing non-composed types
/// to verify that builder creation works correctly for all composition patterns.
/// </summary>
[TestClass]
public class GeneratedComposedCreationTests
{
    #region CompositionAllOf — inline allOf object (existing) — Build + Create

    [TestMethod]
    public void AllOfObject_Build_Create_AllProperties()
    {
        using var workspace = JsonWorkspace.Create();

        CompositionAllOf.Source source = CompositionAllOf.Build(
            static (ref builder) =>
            {
                builder.Create(firstName: "Alice", lastName: "Smith");
            });

        using JsonDocumentBuilder<CompositionAllOf.Mutable> doc =
            CompositionAllOf.CreateBuilder(workspace, source);
        CompositionAllOf.Mutable root = doc.RootElement;

        Assert.AreEqual("Alice", root.FirstName.ToString());
        Assert.AreEqual("Smith", root.LastName.ToString());
    }

    [TestMethod]
    public void AllOfObject_Build_Create_RequiredOnly()
    {
        using var workspace = JsonWorkspace.Create();

        // Both firstName and lastName are optional in CompositionAllOf
        CompositionAllOf.Source source = CompositionAllOf.Build(
            static (ref builder) =>
            {
                builder.Create(firstName: "Alice");
            });

        using JsonDocumentBuilder<CompositionAllOf.Mutable> doc =
            CompositionAllOf.CreateBuilder(workspace, source);
        CompositionAllOf.Mutable root = doc.RootElement;

        Assert.AreEqual("Alice", root.FirstName.ToString());
    }

    [TestMethod]
    public void AllOfObject_Build_Create_MaterializesRoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        CompositionAllOf.Source source = CompositionAllOf.Build(
            static (ref b) =>
            {
                b.Create(firstName: "Alice", lastName: "Smith");
            });

        using JsonDocumentBuilder<CompositionAllOf.Mutable> doc =
            CompositionAllOf.CreateBuilder(workspace, source);

        string json = doc.RootElement.ToString();

        using var reparsed =
            ParsedJsonDocument<CompositionAllOf>.Parse(json);
        Assert.AreEqual("Alice", reparsed.RootElement.FirstName.ToString());
        Assert.AreEqual("Smith", reparsed.RootElement.LastName.ToString());
    }

    #endregion

    #region CompositionWithAny — allOf with built-in any type

    [TestMethod]
    public void AllOfWithAny_MutableTryGetAsJsonElement_ReturnsMutable()
    {
        using var doc =
            ParsedJsonDocument<CompositionWithAny>.Parse("""{"kind":"anything"}""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<CompositionWithAny.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        CompositionWithAny.Mutable root = builderDoc.RootElement;
        Assert.IsTrue(root.TryGetAsJsonElement(out JsonElement.Mutable jsonElement));

        jsonElement.SetProperty("kind"u8, "updated");

        CompositionWithAny.Mutable updatedRoot = builderDoc.RootElement;
        Assert.AreEqual("updated", updatedRoot["kind"u8].ToString());
    }

    #endregion

    #region AllOfObjectWithProperties — inline allOf object with local properties — Build + Create

    [TestMethod]
    public void InlineAllOfObject_Build_Create_AllProperties()
    {
        using var workspace = JsonWorkspace.Create();

        AllOfObjectWithProperties.Source source = AllOfObjectWithProperties.Build(
            static (ref builder) =>
            {
                builder.Create(name: "Bob", age: 30, email: "bob@test.com");
            });

        using JsonDocumentBuilder<AllOfObjectWithProperties.Mutable> doc =
            AllOfObjectWithProperties.CreateBuilder(workspace, source);
        AllOfObjectWithProperties.Mutable root = doc.RootElement;

        Assert.AreEqual("Bob", root.Name.ToString());
        Assert.AreEqual("30", root.Age.ToString());
        Assert.AreEqual("bob@test.com", root.Email.ToString());
    }

    [TestMethod]
    public void InlineAllOfObject_Build_Create_RequiredOnly()
    {
        using var workspace = JsonWorkspace.Create();

        AllOfObjectWithProperties.Source source = AllOfObjectWithProperties.Build(
            static (ref builder) =>
            {
                builder.Create(name: "Bob");
            });

        using JsonDocumentBuilder<AllOfObjectWithProperties.Mutable> doc =
            AllOfObjectWithProperties.CreateBuilder(workspace, source);
        AllOfObjectWithProperties.Mutable root = doc.RootElement;

        Assert.AreEqual("Bob", root.Name.ToString());
    }

    [TestMethod]
    public void InlineAllOfObject_Build_Create_MaterializesRoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        AllOfObjectWithProperties.Source source = AllOfObjectWithProperties.Build(
            static (ref b) =>
            {
                b.Create(name: "Bob", age: 30, email: "bob@test.com");
            });

        using JsonDocumentBuilder<AllOfObjectWithProperties.Mutable> doc =
            AllOfObjectWithProperties.CreateBuilder(workspace, source);

        string json = doc.RootElement.ToString();

        using var reparsed =
            ParsedJsonDocument<AllOfObjectWithProperties>.Parse(json);
        Assert.AreEqual("Bob", reparsed.RootElement.Name.ToString());
        Assert.AreEqual("30", reparsed.RootElement.Age.ToString());
        Assert.AreEqual("bob@test.com", reparsed.RootElement.Email.ToString());
    }

    [TestMethod]
    public void InlineAllOfObject_Mutable_SetComposedAndLocalProperties()
    {
        using var doc =
            ParsedJsonDocument<AllOfObjectWithProperties>.Parse("""{"name":"Bob"}""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<AllOfObjectWithProperties.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        AllOfObjectWithProperties.Mutable root = builderDoc.RootElement;
        root.SetAge(25);
        root.SetEmail("bob@test.com");

        Assert.AreEqual("Bob", root.Name.ToString());
        Assert.AreEqual("25", root.Age.ToString());
        Assert.AreEqual("bob@test.com", root.Email.ToString());
    }

    #endregion

    #region RefObjectWithProperties — $ref-based allOf object with local properties — Build + Create

    [TestMethod]
    public void RefAllOfObject_Build_Create_AllProperties()
    {
        using var workspace = JsonWorkspace.Create();

        RefObjectWithProperties.Source source = RefObjectWithProperties.Build(
            static (ref builder) =>
            {
                builder.Create(name: "Carol", age: 45, email: "carol@test.com");
            });

        using JsonDocumentBuilder<RefObjectWithProperties.Mutable> doc =
            RefObjectWithProperties.CreateBuilder(workspace, source);
        RefObjectWithProperties.Mutable root = doc.RootElement;

        Assert.AreEqual("Carol", root.Name.ToString());
        Assert.AreEqual("45", root.Age.ToString());
        Assert.AreEqual("carol@test.com", root.Email.ToString());
    }

    [TestMethod]
    public void RefAllOfObject_Build_Create_RequiredOnly()
    {
        using var workspace = JsonWorkspace.Create();

        RefObjectWithProperties.Source source = RefObjectWithProperties.Build(
            static (ref builder) =>
            {
                builder.Create(name: "Carol");
            });

        using JsonDocumentBuilder<RefObjectWithProperties.Mutable> doc =
            RefObjectWithProperties.CreateBuilder(workspace, source);
        RefObjectWithProperties.Mutable root = doc.RootElement;

        Assert.AreEqual("Carol", root.Name.ToString());
    }

    [TestMethod]
    public void RefAllOfObject_Build_Create_MaterializesRoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        RefObjectWithProperties.Source source = RefObjectWithProperties.Build(
            static (ref b) =>
            {
                b.Create(name: "Carol", age: 45, email: "carol@test.com");
            });

        using JsonDocumentBuilder<RefObjectWithProperties.Mutable> doc =
            RefObjectWithProperties.CreateBuilder(workspace, source);

        string json = doc.RootElement.ToString();

        using var reparsed =
            ParsedJsonDocument<RefObjectWithProperties>.Parse(json);
        Assert.AreEqual("Carol", reparsed.RootElement.Name.ToString());
        Assert.AreEqual("45", reparsed.RootElement.Age.ToString());
        Assert.AreEqual("carol@test.com", reparsed.RootElement.Email.ToString());
    }

    [TestMethod]
    public void RefAllOfObject_TryGetAsBasePerson_AccessesComposedProperties()
    {
        using var doc =
            ParsedJsonDocument<RefObjectWithProperties>.Parse("""{"name":"Carol","age":45,"email":"carol@test.com"}""");

        Assert.IsTrue(doc.RootElement.TryGetAsBasePerson(out RefObjectWithProperties.BasePerson basePerson));
        Assert.AreEqual("Carol", basePerson.Name.ToString());
        Assert.AreEqual("45", basePerson.Age.ToString());
    }

    [TestMethod]
    public void RefAllOfObject_MutableTryGetAsBasePerson_ReturnsMutable()
    {
        using var doc =
            ParsedJsonDocument<RefObjectWithProperties>.Parse("""{"name":"Carol","age":45,"email":"carol@test.com"}""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<RefObjectWithProperties.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        RefObjectWithProperties.Mutable root = builderDoc.RootElement;
        Assert.IsTrue(root.TryGetAsBasePerson(out RefObjectWithProperties.BasePerson.Mutable basePerson));

        basePerson.SetName("Caroline");
        basePerson.SetAge(50);

        RefObjectWithProperties.Mutable updatedRoot = builderDoc.RootElement;
        Assert.AreEqual("Caroline", updatedRoot.Name.ToString());
        Assert.AreEqual("50", updatedRoot.Age.ToString());
        Assert.AreEqual("carol@test.com", updatedRoot.Email.ToString());
    }

    [TestMethod]
    public void RefAllOfObject_MutableTryGetAsBasePerson_ReturnsFalseForInvalidValue()
    {
        using var doc =
            ParsedJsonDocument<RefObjectWithProperties>.Parse("""{"email":"carol@test.com"}""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<RefObjectWithProperties.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        RefObjectWithProperties.Mutable root = builderDoc.RootElement;

        Assert.IsFalse(root.TryGetAsBasePerson(out RefObjectWithProperties.BasePerson.Mutable basePerson));
        Assert.AreEqual(default, basePerson);
    }

    [TestMethod]
    public void RefAllOfObject_Mutable_SetComposedAndLocalProperties()
    {
        using var doc =
            ParsedJsonDocument<RefObjectWithProperties>.Parse("""{"name":"Carol"}""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<RefObjectWithProperties.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        RefObjectWithProperties.Mutable root = builderDoc.RootElement;
        root.SetAge(50);
        root.SetEmail("carol@new.com");

        Assert.AreEqual("Carol", root.Name.ToString());
        Assert.AreEqual("50", root.Age.ToString());
        Assert.AreEqual("carol@new.com", root.Email.ToString());
    }

    [TestMethod]
    public void RefAllOfObject_Mutable_RemoveComposedOptionalProperty()
    {
        using var doc =
            ParsedJsonDocument<RefObjectWithProperties>.Parse("""{"name":"Carol","age":45,"email":"carol@test.com"}""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<RefObjectWithProperties.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        RefObjectWithProperties.Mutable root = builderDoc.RootElement;
        Assert.IsTrue(root.RemoveAge());

        string json = root.ToString();
        StringAssert.Contains(json, "Carol");
        Assert.DoesNotContain("age", json);
        StringAssert.Contains(json, "email");
    }

    [TestMethod]
    public void RefAllOfObject_Mutable_RemoveLocalOptionalProperty()
    {
        using var doc =
            ParsedJsonDocument<RefObjectWithProperties>.Parse("""{"name":"Carol","age":45,"email":"carol@test.com"}""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<RefObjectWithProperties.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        RefObjectWithProperties.Mutable root = builderDoc.RootElement;
        Assert.IsTrue(root.RemoveEmail());

        string json = root.ToString();
        StringAssert.Contains(json, "Carol");
        StringAssert.Contains(json, "age");
        Assert.DoesNotContain("email", json);
    }

    [TestMethod]
    public void RefAllOfObject_Mutable_ChainedRemoves()
    {
        using var doc =
            ParsedJsonDocument<RefObjectWithProperties>.Parse("""{"name":"Carol","age":45,"email":"carol@test.com"}""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<RefObjectWithProperties.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        RefObjectWithProperties.Mutable root = builderDoc.RootElement;
        Assert.IsTrue(root.RemoveAge());
        Assert.IsTrue(root.RemoveEmail());

        string json = root.ToString();
        StringAssert.Contains(json, "Carol");
        Assert.DoesNotContain("age", json);
        Assert.DoesNotContain("email", json);
    }

    #endregion

    #region ArrayOfItems — non-composed array (existing) — Build + Add

    [TestMethod]
    public void Array_Build_Add_CreatesItems()
    {
        using var workspace = JsonWorkspace.Create();

        ArrayOfItems.Source source = ArrayOfItems.Build(
            static (ref builder) =>
            {
                builder.AddItem(ArrayOfItems.RequiredId.Build(
                    static (ref b) =>
                    {
                        b.Create(id: 1, label: "first");
                    }));
                builder.AddItem(ArrayOfItems.RequiredId.Build(
                    static (ref b) =>
                    {
                        b.Create(id: 2, label: "second");
                    }));
            });

        using JsonDocumentBuilder<ArrayOfItems.Mutable> doc =
            ArrayOfItems.CreateBuilder(workspace, source);
        ArrayOfItems.Mutable root = doc.RootElement;

        Assert.AreEqual(2, root.GetArrayLength());
    }

    [TestMethod]
    public void Array_Build_Add_MaterializesRoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        ArrayOfItems.Source source = ArrayOfItems.Build(
            static (ref builder) =>
            {
                builder.AddItem(ArrayOfItems.RequiredId.Build(
                    static (ref b) =>
                    {
                        b.Create(id: 1, label: "first");
                    }));
            });

        using JsonDocumentBuilder<ArrayOfItems.Mutable> doc =
            ArrayOfItems.CreateBuilder(workspace, source);

        string json = doc.RootElement.ToString();

        using var reparsed =
            ParsedJsonDocument<ArrayOfItems>.Parse(json);
        Assert.AreEqual(1, reparsed.RootElement.GetArrayLength());
        Assert.AreEqual("1", reparsed.RootElement[0].Id.ToString());
        Assert.AreEqual("first", reparsed.RootElement[0].Label.ToString());
    }

    #endregion

    #region AllOfArrayWithItems — inline allOf array — Build + Add

    [TestMethod]
    public void InlineAllOfArray_Build_Add_CreatesItems()
    {
        using var workspace = JsonWorkspace.Create();

        AllOfArrayWithItems.Source source = AllOfArrayWithItems.Build(
            static (ref builder) =>
            {
                builder.AddItem("hello");
                builder.AddItem("world");
            });

        using JsonDocumentBuilder<AllOfArrayWithItems.Mutable> doc =
            AllOfArrayWithItems.CreateBuilder(workspace, source);
        AllOfArrayWithItems.Mutable root = doc.RootElement;

        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual("hello", root[0].ToString());
        Assert.AreEqual("world", root[1].ToString());
    }

    [TestMethod]
    public void InlineAllOfArray_Build_Add_MaterializesRoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        AllOfArrayWithItems.Source source = AllOfArrayWithItems.Build(
            static (ref builder) =>
            {
                builder.AddItem("hello");
                builder.AddItem("world");
            });

        using JsonDocumentBuilder<AllOfArrayWithItems.Mutable> doc =
            AllOfArrayWithItems.CreateBuilder(workspace, source);

        string json = doc.RootElement.ToString();

        using var reparsed =
            ParsedJsonDocument<AllOfArrayWithItems>.Parse(json);
        Assert.AreEqual(2, reparsed.RootElement.GetArrayLength());
        Assert.AreEqual("hello", reparsed.RootElement[0].ToString());
        Assert.AreEqual("world", reparsed.RootElement[1].ToString());
    }

    [TestMethod]
    public void InlineAllOfArray_Mutable_SetItem()
    {
        using var doc =
            ParsedJsonDocument<AllOfArrayWithItems>.Parse("""["hello","world"]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<AllOfArrayWithItems.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        AllOfArrayWithItems.Mutable root = builderDoc.RootElement;
        root.SetItem(0, "replaced");

        Assert.AreEqual("replaced", root[0].ToString());
        Assert.AreEqual("world", root[1].ToString());
    }

    [TestMethod]
    public void InlineAllOfArray_Mutable_InsertItem()
    {
        using var doc =
            ParsedJsonDocument<AllOfArrayWithItems>.Parse("""["hello","world"]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<AllOfArrayWithItems.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        AllOfArrayWithItems.Mutable root = builderDoc.RootElement;
        root.InsertItem(1, "middle");

        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual("hello", root[0].ToString());
        Assert.AreEqual("middle", root[1].ToString());
        Assert.AreEqual("world", root[2].ToString());
    }

    [TestMethod]
    public void InlineAllOfArray_Mutable_Remove()
    {
        using var doc =
            ParsedJsonDocument<AllOfArrayWithItems>.Parse("""["hello","world","extra"]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<AllOfArrayWithItems.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        AllOfArrayWithItems.Mutable root = builderDoc.RootElement;
        root.RemoveAt(1);

        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual("hello", root[0].ToString());
        Assert.AreEqual("extra", root[1].ToString());
    }

    #endregion

    #region RefArrayWithItems — $ref-based allOf array — Build + Add

    [TestMethod]
    public void RefAllOfArray_Build_Add_CreatesItems()
    {
        using var workspace = JsonWorkspace.Create();

        RefArrayWithItems.Source source = RefArrayWithItems.Build(
            static (ref builder) =>
            {
                builder.AddItem("hello");
                builder.AddItem("world");
            });

        using JsonDocumentBuilder<RefArrayWithItems.Mutable> doc =
            RefArrayWithItems.CreateBuilder(workspace, source);
        RefArrayWithItems.Mutable root = doc.RootElement;

        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual("hello", root[0].ToString());
        Assert.AreEqual("world", root[1].ToString());
    }

    [TestMethod]
    public void RefAllOfArray_Build_Add_MaterializesRoundTrip()
    {
        using var workspace = JsonWorkspace.Create();

        RefArrayWithItems.Source source = RefArrayWithItems.Build(
            static (ref builder) =>
            {
                builder.AddItem("hello");
                builder.AddItem("world");
            });

        using JsonDocumentBuilder<RefArrayWithItems.Mutable> doc =
            RefArrayWithItems.CreateBuilder(workspace, source);

        string json = doc.RootElement.ToString();

        using var reparsed =
            ParsedJsonDocument<RefArrayWithItems>.Parse(json);
        Assert.AreEqual(2, reparsed.RootElement.GetArrayLength());
        Assert.AreEqual("hello", reparsed.RootElement[0].ToString());
        Assert.AreEqual("world", reparsed.RootElement[1].ToString());
    }

    [TestMethod]
    public void RefAllOfArray_TryGetAsBaseArray_AccessesItems()
    {
        using var doc =
            ParsedJsonDocument<RefArrayWithItems>.Parse("""["hello","world"]""");

        Assert.IsTrue(doc.RootElement.TryGetAsBaseArray(out RefArrayWithItems.BaseArray baseArray));
        Assert.AreEqual(2, baseArray.GetArrayLength());
        Assert.AreEqual("hello", baseArray[0].ToString());
    }

    [TestMethod]
    public void RefAllOfArray_MutableTryGetAsBaseArray_ReturnsMutable()
    {
        using var doc =
            ParsedJsonDocument<RefArrayWithItems>.Parse("""["hello","world"]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<RefArrayWithItems.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        RefArrayWithItems.Mutable root = builderDoc.RootElement;
        Assert.IsTrue(root.TryGetAsBaseArray(out RefArrayWithItems.BaseArray.Mutable baseArray));

        baseArray.SetItem(0, "replaced");

        RefArrayWithItems.Mutable updatedRoot = builderDoc.RootElement;
        Assert.AreEqual(2, updatedRoot.GetArrayLength());
        Assert.AreEqual("replaced", updatedRoot[0].ToString());
        Assert.AreEqual("world", updatedRoot[1].ToString());
    }

    [TestMethod]
    public void RefAllOfArray_Mutable_SetItem()
    {
        using var doc =
            ParsedJsonDocument<RefArrayWithItems>.Parse("""["hello","world"]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<RefArrayWithItems.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        RefArrayWithItems.Mutable root = builderDoc.RootElement;
        root.SetItem(0, "replaced");

        Assert.AreEqual("replaced", root[0].ToString());
        Assert.AreEqual("world", root[1].ToString());
    }

    [TestMethod]
    public void RefAllOfArray_Mutable_InsertItem()
    {
        using var doc =
            ParsedJsonDocument<RefArrayWithItems>.Parse("""["hello","world"]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<RefArrayWithItems.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        RefArrayWithItems.Mutable root = builderDoc.RootElement;
        root.InsertItem(1, "middle");

        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual("hello", root[0].ToString());
        Assert.AreEqual("middle", root[1].ToString());
        Assert.AreEqual("world", root[2].ToString());
    }

    [TestMethod]
    public void RefAllOfArray_Mutable_Remove()
    {
        using var doc =
            ParsedJsonDocument<RefArrayWithItems>.Parse("""["hello","world","extra"]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<RefArrayWithItems.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        RefArrayWithItems.Mutable root = builderDoc.RootElement;
        root.RemoveAt(1);

        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual("hello", root[0].ToString());
        Assert.AreEqual("extra", root[1].ToString());
    }

    [TestMethod]
    public void RefAllOfArray_Mutable_RemoveWhere()
    {
        using var doc =
            ParsedJsonDocument<RefArrayWithItems>.Parse("""["hello","world","extra"]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<RefArrayWithItems.Mutable> builderDoc =
            doc.RootElement.CreateBuilder(workspace);

        RefArrayWithItems.Mutable root = builderDoc.RootElement;
        root.RemoveWhere(
            static (in item) =>
                item.ToString() == "world");

        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual("hello", root[0].ToString());
        Assert.AreEqual("extra", root[1].ToString());
    }

    #endregion

    #region ObjectWithArrayProperty — Build + Create with nested array sources

    [TestMethod]
    public void ObjectWithArray_Build_Create_WithArrayProperty()
    {
        using var workspace = JsonWorkspace.Create();

        ObjectWithArrayProperty.Source source = ObjectWithArrayProperty.Build(
            static (ref builder) =>
            {
                builder.Create(
                    tags: ObjectWithArrayProperty.JsonStringArray.Build(
                        static (ref b) =>
                        {
                            b.AddItem("tag1");
                            b.AddItem("tag2");
                        }));
            });

        using JsonDocumentBuilder<ObjectWithArrayProperty.Mutable> doc =
            ObjectWithArrayProperty.CreateBuilder(workspace, source);

        string json = doc.RootElement.ToString();
        StringAssert.Contains(json, "tag1");
        StringAssert.Contains(json, "tag2");
    }

    #endregion
}