// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for the property-parameter <c>Build(...)</c> overloads that capture the
/// <c>Create(...)</c> arguments directly into a <c>Source</c> (issue #789).
/// Exercises non-context and context-flowing forms and verifies equivalence with
/// the delegate-based <c>Build</c> form.
/// </summary>
[TestClass]
public class GeneratedParamsBuildTests
{
    [TestMethod]
    public void ParamsBuild_NestedObject_EqualsDelegateBuild()
    {
        // Property-parameter form: Build(address: Address.Build(street:, city:, zip:), notes:).
        using JsonWorkspace wsParams = JsonWorkspace.Create();
        using JsonDocumentBuilder<NestedObject.Mutable> paramsDoc = NestedObject.CreateBuilder(
            wsParams,
            NestedObject.Build(
                address: NestedObject.RequiredStreet.Build(street: "123 Main St", city: "Springfield", zip: "62704"),
                notes: "Test notes"));
        string paramsJson = paramsDoc.RootElement.ToString();

        // Delegate form: Build((ref b) => b.Create(...)).
        using JsonWorkspace wsDelegate = JsonWorkspace.Create();
        using JsonDocumentBuilder<NestedObject.Mutable> delegateDoc = NestedObject.CreateBuilder(
            wsDelegate,
            NestedObject.Build((ref NestedObject.Builder b) => b.Create(
                address: NestedObject.RequiredStreet.Build((ref NestedObject.RequiredStreet.Builder ab) =>
                    ab.Create(street: "123 Main St", city: "Springfield", zip: "62704")),
                notes: "Test notes")));
        string delegateJson = delegateDoc.RootElement.ToString();

        Assert.AreEqual(delegateJson, paramsJson);
    }

    [TestMethod]
    public void ParamsBuild_NestedObject_ProducesExpectedJson()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<NestedObject.Mutable> doc = NestedObject.CreateBuilder(
            workspace,
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
    public void ParamsBuild_OptionalPropertyOmitted_IsAbsentFromOutput()
    {
        // 'notes' is optional and defaulted; omitting it must leave it out of the output.
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<NestedObject.Mutable> doc = NestedObject.CreateBuilder(
            workspace,
            NestedObject.Build(
                address: NestedObject.RequiredStreet.Build(street: "1 First Ave", city: "Metropolis", zip: "00001")));

        Assert.AreEqual(
            """
            {"address":{"street":"1 First Ave","city":"Metropolis","zip":"00001"}}
            """,
            doc.RootElement.ToString());
    }

    [TestMethod]
    public void ParamsBuild_ContextFlowing_EqualsNonContext()
    {
        // The context-flowing overload exists because 'address' is an object property.
        // Capture the source data in a ref-struct-friendly context tuple.
        (string street, string city, string zip, string notes) context =
            ("9 Ninth St", "Gotham", "99999", "ctx notes");

        using JsonWorkspace wsContext = JsonWorkspace.Create();
        using JsonDocumentBuilder<NestedObject.Mutable> contextDoc = NestedObject.CreateBuilder(
            wsContext,
            context,
            address: NestedObject.RequiredStreet.Build(street: context.street, city: context.city, zip: context.zip),
            notes: context.notes);
        string contextJson = contextDoc.RootElement.ToString();

        using JsonWorkspace wsPlain = JsonWorkspace.Create();
        using JsonDocumentBuilder<NestedObject.Mutable> plainDoc = NestedObject.CreateBuilder(
            wsPlain,
            NestedObject.Build(
                address: NestedObject.RequiredStreet.Build(street: "9 Ninth St", city: "Gotham", zip: "99999"),
                notes: "ctx notes"));
        string plainJson = plainDoc.RootElement.ToString();

        Assert.AreEqual(plainJson, contextJson);
    }

    [TestMethod]
    public void ParamsBuild_PropertyNamedContext_DoesNotCollideWithContextParameter()
    {
        // Regression guard for issue #789: a property whose parameter name is "context" must not
        // collide with the synthetic TContext parameter of the context-flowing Build<TContext>
        // overload. (The object property 'nested' is what makes the type emit the context-flowing
        // overloads; the generated ContextNamedProperty model failing to compile would itself catch
        // a regression of the duplicate-parameter bug.)
        using JsonWorkspace wsParams = JsonWorkspace.Create();
        using JsonDocumentBuilder<ContextNamedProperty.Mutable> paramsDoc = ContextNamedProperty.CreateBuilder(
            wsParams,
            ContextNamedProperty.Build(
                nested: ContextNamedProperty.RequiredValue.Build(value: "v"),
                context: "ctxval"));
        string paramsJson = paramsDoc.RootElement.ToString();

        using JsonWorkspace wsDelegate = JsonWorkspace.Create();
        using JsonDocumentBuilder<ContextNamedProperty.Mutable> delegateDoc = ContextNamedProperty.CreateBuilder(
            wsDelegate,
            ContextNamedProperty.Build((ref ContextNamedProperty.Builder b) => b.Create(
                nested: ContextNamedProperty.RequiredValue.Build((ref ContextNamedProperty.RequiredValue.Builder nb) =>
                    nb.Create(value: "v")),
                context: "ctxval")));
        string delegateJson = delegateDoc.RootElement.ToString();

        Assert.AreEqual(delegateJson, paramsJson);
    }

    [TestMethod]
    public void ParamsBuild_AsArrayElement_InsideArrayBuilder()
    {
        // Regression guard: the field-set Build(...) factory must be usable as an array
        // element inside an array builder lambda — builder.AddItem(Item.Build(field: ...)).
        // This requires `scoped in` on the Build factory parameters, the capturing Source
        // constructor, and Array.AddItem/InsertItem; without it the C# ref-safety analysis
        // rejects the call with CS8347/CS8350 (the Build result's Source would otherwise be
        // assumed to escape into the wider-scoped array builder). The delegate-form Build —
        // tested elsewhere — works without the fix because it wraps a heap closure, not refs.
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<ArrayOfItems.Mutable> doc = ArrayOfItems.CreateBuilder(
            workspace,
            ArrayOfItems.Build(static (ref ArrayOfItems.Builder b) =>
            {
                b.AddItem(ArrayOfItems.RequiredId.Build(id: 1, label: "first"));
                b.AddItem(ArrayOfItems.RequiredId.Build(id: 2));
            }));

        Assert.AreEqual(
            """
            [{"id":1,"label":"first"},{"id":2}]
            """,
            doc.RootElement.ToString());
    }
}
