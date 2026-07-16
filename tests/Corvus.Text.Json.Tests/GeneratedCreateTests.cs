// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for the generated Create() factory methods, which mirror the CreateBuilder() overloads but
/// produce a self-contained <see cref="ParsedJsonDocument{T}"/> without a serialize-and-reparse
/// round trip.
/// </summary>
[TestClass]
public class GeneratedCreateTests
{
    #region Create from per-property Source parameters

    [TestMethod]
    public void Create_WithRequiredPropertyOnly_CreatesExpectedJson()
    {
        using ParsedJsonDocument<AllOfObjectWithProperties> doc =
            AllOfObjectWithProperties.Create("Alice");

        Assert.AreEqual("""{"name":"Alice"}""", doc.RootElement.ToString());
    }

    [TestMethod]
    public void Create_WithRequiredAndOptionalProperties_CreatesExpectedJson()
    {
        using ParsedJsonDocument<AllOfObjectWithProperties> doc =
            AllOfObjectWithProperties.Create("Alice", 30, "alice@example.com");

        AllOfObjectWithProperties root = doc.RootElement;
        Assert.IsTrue(root.Name.ValueEquals("Alice"));
        Assert.AreEqual("30", root.Age.ToString());
        Assert.IsTrue(root.Email.ValueEquals("alice@example.com"));
    }

    [TestMethod]
    public void Create_WithRequiredAndPartialOptionalProperties_CreatesExpectedJson()
    {
        using ParsedJsonDocument<AllOfObjectWithProperties> doc =
            AllOfObjectWithProperties.Create("Bob", 25);

        AllOfObjectWithProperties root = doc.RootElement;
        Assert.IsTrue(root.Name.ValueEquals("Bob"));
        Assert.AreEqual("25", root.Age.ToString());
        Assert.IsTrue(root.Email.IsUndefined());
    }

    [TestMethod]
    public void Create_WithMixedProperties_CreatesExpectedJson()
    {
        using ParsedJsonDocument<ObjectWithMixedProperties> doc =
            ObjectWithMixedProperties.Create(40, "Charlie", "charlie@example.com", true);

        ObjectWithMixedProperties root = doc.RootElement;
        Assert.IsTrue(root.Name.ValueEquals("Charlie"));
        Assert.AreEqual("40", root.Age.ToString());
        Assert.IsTrue(root.Email.ValueEquals("charlie@example.com"));
        Assert.IsTrue((bool)root.IsActive);
    }

    [TestMethod]
    public void Create_WithDefaultOptionalProperties_OmitsThem()
    {
        using ParsedJsonDocument<ObjectWithMixedProperties> doc =
            ObjectWithMixedProperties.Create(30, "Dave");

        ObjectWithMixedProperties root = doc.RootElement;
        Assert.IsTrue(root.Name.ValueEquals("Dave"));
        Assert.AreEqual("30", root.Age.ToString());
        Assert.IsTrue(root.Email.IsUndefined());
        Assert.IsTrue(root.IsActive.IsUndefined());
    }

    [TestMethod]
    public void Create_ProducesValidRoundTrippableJson()
    {
        using ParsedJsonDocument<AllOfObjectWithProperties> doc =
            AllOfObjectWithProperties.Create("Alice", 30, "alice@example.com");

        string json = doc.RootElement.ToString();

        using var parsed =
            ParsedJsonDocument<AllOfObjectWithProperties>.Parse(json);

        Assert.IsTrue(parsed.RootElement.Name.ValueEquals("Alice"));
        Assert.AreEqual("30", parsed.RootElement.Age.ToString());
        Assert.IsTrue(parsed.RootElement.Email.ValueEquals("alice@example.com"));
    }

    #endregion

    #region Create from Source / Build delegates

    [TestMethod]
    public void Create_NestedObject_FromBuild_ProducesExpectedJson()
    {
        using ParsedJsonDocument<NestedObject> doc = NestedObject.Create(
            NestedObject.Build(
                address: NestedObject.RequiredStreet.Build(street: "1 First Ave", city: "Metropolis", zip: "00001"),
                notes: "hello"));

        Assert.AreEqual(
            """
            {"address":{"street":"1 First Ave","city":"Metropolis","zip":"00001"},"notes":"hello"}
            """,
            doc.RootElement.ToString());
    }

    [TestMethod]
    public void Create_NestedObject_FromProperties_ProducesExpectedJson()
    {
        using ParsedJsonDocument<NestedObject> doc = NestedObject.Create(
            address: NestedObject.RequiredStreet.Build(street: "2 Second St", city: "Gotham", zip: "00002"),
            notes: "note");

        NestedObject root = doc.RootElement;
        Assert.IsTrue(root.Address.Street.ValueEquals("2 Second St"));
        Assert.IsTrue(root.Address.City.ValueEquals("Gotham"));
        Assert.IsTrue(root.Notes.ValueEquals("note"));
    }

    [TestMethod]
    public void Create_ArrayOfItems_FromBuild_MaterializesRoundTrip()
    {
        ArrayOfItems.Source source = ArrayOfItems.Build(
            static (ref builder) =>
            {
                builder.AddItem(ArrayOfItems.RequiredId.Build(
                    static (ref b) => { b.Create(id: 1, label: "first"); }));
                builder.AddItem(ArrayOfItems.RequiredId.Build(
                    static (ref b) => { b.Create(id: 2, label: "second"); }));
            });

        using ParsedJsonDocument<ArrayOfItems> doc = ArrayOfItems.Create(source);

        Assert.AreEqual(2, doc.RootElement.GetArrayLength());
        Assert.AreEqual("1", doc.RootElement[0].Id.ToString());
        Assert.AreEqual("first", doc.RootElement[0].Label.ToString());
        Assert.AreEqual("2", doc.RootElement[1].Id.ToString());
        Assert.AreEqual("second", doc.RootElement[1].Label.ToString());
    }

    [TestMethod]
    public void Create_ContextFlowing_EqualsNonContext()
    {
        (string Street, string City, string Zip, string Notes) context =
            ("9 Ninth St", "Gotham", "99999", "ctx notes");

        using ParsedJsonDocument<NestedObject> contextDoc = NestedObject.Create(
            context,
            address: NestedObject.RequiredStreet.Build(street: context.Street, city: context.City, zip: context.Zip),
            notes: context.Notes);
        string contextJson = contextDoc.RootElement.ToString();

        using ParsedJsonDocument<NestedObject> plainDoc = NestedObject.Create(
            NestedObject.Build(
                address: NestedObject.RequiredStreet.Build(street: "9 Ninth St", city: "Gotham", zip: "99999"),
                notes: "ctx notes"));
        string plainJson = plainDoc.RootElement.ToString();

        Assert.AreEqual(plainJson, contextJson);
    }

    #endregion

    #region Composition

    [TestMethod]
    public void Create_AllOfObject_FromBuild_AllProperties()
    {
        CompositionAllOf.Source source = CompositionAllOf.Build(
            static (ref builder) => { builder.Create(firstName: "Alice", lastName: "Smith"); });

        using ParsedJsonDocument<CompositionAllOf> doc = CompositionAllOf.Create(source);

        CompositionAllOf root = doc.RootElement;
        Assert.IsTrue(root.FirstName.ValueEquals("Alice"));
        Assert.IsTrue(root.LastName.ValueEquals("Smith"));
    }

    [TestMethod]
    public void Create_AllOfObject_FromProperties()
    {
        using ParsedJsonDocument<CompositionAllOf> doc = CompositionAllOf.Create(firstName: "Ada", lastName: "Lovelace");

        Assert.AreEqual("""{"firstName":"Ada","lastName":"Lovelace"}""", doc.RootElement.ToString());
    }

    #endregion

    #region Tuples

    [TestMethod]
    public void Create_PureTuple_FromSources()
    {
        using ParsedJsonDocument<PureTuple> doc = PureTuple.Create("world", 99, false);

        PureTuple root = doc.RootElement;
        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual("""["world",99,false]""", root.ToString());
    }

    [TestMethod]
    public void Create_PureTuple_FromBuild()
    {
        using ParsedJsonDocument<PureTuple> doc = PureTuple.Create(PureTuple.Build("hello", 1, true));

        Assert.AreEqual("""["hello",1,true]""", doc.RootElement.ToString());
    }

    #endregion

    #region Numeric arrays

    [TestMethod]
    public void Create_Rank1Int32Vector_FromSpan()
    {
        ReadOnlySpan<int> values = [3, 1, 4, 1];
        using ParsedJsonDocument<Rank1Int32Vector> doc = Rank1Int32Vector.Create(values);

        Assert.AreEqual("[3,1,4,1]", doc.RootElement.ToString());
    }

    [TestMethod]
    public void Create_Rank1DoubleVector_FromSpan()
    {
        ReadOnlySpan<double> values = [1.5, -2.25, 3];
        using ParsedJsonDocument<Rank1DoubleVector> doc = Rank1DoubleVector.Create(values);

        Assert.AreEqual("[1.5,-2.25,3]", doc.RootElement.ToString());
    }

    [TestMethod]
    public void Create_Rank2DoubleMatrix_MatchesCreateBuilderOutput()
    {
        ReadOnlySpan<double> values = [1, 2, 3, 4, 5, 6];

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Rank2DoubleMatrix.Mutable> builder =
            Rank2DoubleMatrix.CreateBuilder(workspace, values);

        using ParsedJsonDocument<Rank2DoubleMatrix> doc = Rank2DoubleMatrix.Create(values);

        Assert.AreEqual(builder.RootElement.ToString(), doc.RootElement.ToString());
    }

    #endregion

    #region Empty documents

    [TestMethod]
    public void Create_EmptyArray_ProducesEmptyArrayDocument()
    {
        using ParsedJsonDocument<ArrayOfItems> doc = ArrayOfItems.Create();

        Assert.AreEqual("[]", doc.RootElement.ToString());
        Assert.AreEqual(0, doc.RootElement.GetArrayLength());
    }

    [TestMethod]
    public void Create_SelfReferencingObject_Empty_ProducesEmptyObjectDocument()
    {
        using ParsedJsonDocument<SelfReferencingObject> doc = SelfReferencingObject.Create();

        Assert.AreEqual("{}", doc.RootElement.ToString());
    }

    [TestMethod]
    public void CreateArray_OnDualKindType_ProducesEmptyArrayDocument()
    {
        using ParsedJsonDocument<CompositionWithAny> doc = CompositionWithAny.CreateArray();

        Assert.AreEqual("[]", doc.RootElement.ToString());
        Assert.AreEqual(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [TestMethod]
    public void CreateObject_OnDualKindType_ProducesEmptyObjectDocument()
    {
        using ParsedJsonDocument<CompositionWithAny> doc = CompositionWithAny.CreateObject();

        Assert.AreEqual("{}", doc.RootElement.ToString());
        Assert.AreEqual(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    #endregion

    #region Self-referencing recursion

    [TestMethod]
    public void Create_SelfReferencingObject_Nested_ProducesExpectedJson()
    {
        using ParsedJsonDocument<SelfReferencingObject> doc = SelfReferencingObject.Create(
            static (ref objectBuilder) =>
            {
                objectBuilder.Create(
                    parent: SelfReferencingObject.Build(
                        static (ref innerBuilder) =>
                        {
                            innerBuilder.Create(
                                parent: SelfReferencingObject.Build(
                                    static (ref leaf) =>
                                    {
                                        leaf.Create();
                                    }));
                        }));
            });

        Assert.AreEqual("""{"parent":{"parent":{}}}""", doc.RootElement.ToString());
    }

    #endregion

    #region Embedding existing elements

    [TestMethod]
    public void Create_EmbeddingParsedElement_ResultIsSelfContained()
    {
        var external = ParsedJsonDocument<NestedObject.RequiredStreet>.Parse(
            """{"street":"5 Fifth Ave","city":"Star City","zip":"00005"}""");

        using ParsedJsonDocument<NestedObject> doc = NestedObject.Create(
            NestedObject.Build(address: external.RootElement, notes: "embedded"));

        // Dispose the source before reading: the created document must own all of its content.
        external.Dispose();

        NestedObject root = doc.RootElement;
        Assert.IsTrue(root.Address.Street.ValueEquals("5 Fifth Ave"));
        Assert.IsTrue(root.Address.City.ValueEquals("Star City"));
        Assert.IsTrue(root.Notes.ValueEquals("embedded"));
    }

    #endregion

    #region Equivalence with CreateBuilder

    [TestMethod]
    public void Create_AllOfObjectWithProperties_MatchesCreateBuilderOutput()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<AllOfObjectWithProperties.Mutable> builder =
            AllOfObjectWithProperties.CreateBuilder(workspace, "Alice", 30, "alice@example.com");

        using ParsedJsonDocument<AllOfObjectWithProperties> doc =
            AllOfObjectWithProperties.Create("Alice", 30, "alice@example.com");

        Assert.AreEqual(builder.RootElement.ToString(), doc.RootElement.ToString());
    }

    [TestMethod]
    public void Create_NestedObject_MatchesCreateBuilderOutput()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<NestedObject.Mutable> builder = NestedObject.CreateBuilder(
            workspace,
            NestedObject.Build(
                address: NestedObject.RequiredStreet.Build(street: "1 First Ave", city: "Metropolis", zip: "00001"),
                notes: "hello"));

        using ParsedJsonDocument<NestedObject> doc = NestedObject.Create(
            NestedObject.Build(
                address: NestedObject.RequiredStreet.Build(street: "1 First Ave", city: "Metropolis", zip: "00001"),
                notes: "hello"));

        Assert.AreEqual(builder.RootElement.ToString(), doc.RootElement.ToString());
    }

    [TestMethod]
    public void Create_PureTuple_MatchesCreateBuilderOutput()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<PureTuple.Mutable> builder =
            PureTuple.CreateBuilder(workspace, "world", 99, false);

        using ParsedJsonDocument<PureTuple> doc = PureTuple.Create("world", 99, false);

        Assert.AreEqual(builder.RootElement.ToString(), doc.RootElement.ToString());
    }

    [TestMethod]
    public void Create_Rank1Int32Vector_MatchesCreateBuilderOutput()
    {
        ReadOnlySpan<int> values = [3, 1, 4, 1];

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Rank1Int32Vector.Mutable> builder =
            Rank1Int32Vector.CreateBuilder(workspace, values);

        using ParsedJsonDocument<Rank1Int32Vector> doc = Rank1Int32Vector.Create(values);

        Assert.AreEqual(builder.RootElement.ToString(), doc.RootElement.ToString());
    }

    #endregion

    #region Scalar types

    [TestMethod]
    public void Create_ScalarString_ProducesExpectedDocument()
    {
        using ParsedJsonDocument<JsonString> doc = JsonString.Create("hello world");

        Assert.AreEqual(JsonValueKind.String, doc.RootElement.ValueKind);
        Assert.IsTrue(doc.RootElement.ValueEquals("hello world"));
    }

    [TestMethod]
    public void Create_ScalarInt32_ProducesExpectedDocument()
    {
        using ParsedJsonDocument<JsonInt32> doc = JsonInt32.Create(42);

        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.ValueKind);
        Assert.AreEqual(42, (int)doc.RootElement);
    }

    [TestMethod]
    public void Create_ScalarBoolean_ProducesExpectedDocument()
    {
        using ParsedJsonDocument<JsonBoolean> doc = JsonBoolean.Create(true);

        Assert.AreEqual(JsonValueKind.True, doc.RootElement.ValueKind);
        Assert.IsTrue((bool)doc.RootElement);
    }

    #endregion
}