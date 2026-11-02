// <copyright file="GeneratedCloneTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Internal;
using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// A generated type's <c>Clone()</c> copies only when the instance's document can be disposed: the clone of a clone
/// is the same instance, as <see cref="JsonElement.Clone()"/> does
/// (https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/927).
/// </summary>
[TestClass]
public class GeneratedCloneTests
{
    private const string Json = """{"address":{"street":"1 High Street","city":"Bath"},"notes":"n"}""";

    [TestMethod]
    public void CloningAParsedInstance_CopiesItIntoADocumentThatOutlivesTheOriginal()
    {
        NestedObject clone;
        IJsonDocument original;
        using (var doc = ParsedJsonDocument<NestedObject>.Parse(Json))
        {
            original = ((IJsonElement)doc.RootElement).ParentDocument;
            clone = doc.RootElement.Clone();
        }

        Assert.AreNotSame(original, ((IJsonElement)clone).ParentDocument);
        Assert.AreEqual("1 High Street", (string)clone.Address.Street);
    }

    [TestMethod]
    public void CloningAClone_ReturnsTheSameInstance()
    {
        using var doc = ParsedJsonDocument<NestedObject>.Parse(Json);
        NestedObject clone = doc.RootElement.Clone();
        NestedObject cloneOfClone = clone.Clone();

        Assert.AreSame(((IJsonElement)clone).ParentDocument, ((IJsonElement)cloneOfClone).ParentDocument);
        Assert.AreEqual(((IJsonElement)clone).ParentDocumentIndex, ((IJsonElement)cloneOfClone).ParentDocumentIndex);
    }

    [TestMethod]
    public void CloningAValueInsideAClone_ReturnsTheSameInstance()
    {
        using var doc = ParsedJsonDocument<NestedObject>.Parse(Json);
        NestedObject clone = doc.RootElement.Clone();
        var address = clone.Address;
        var addressClone = address.Clone();

        Assert.AreSame(((IJsonElement)clone).ParentDocument, ((IJsonElement)addressClone).ParentDocument);
        Assert.AreEqual(((IJsonElement)address).ParentDocumentIndex, ((IJsonElement)addressClone).ParentDocumentIndex);
    }
}