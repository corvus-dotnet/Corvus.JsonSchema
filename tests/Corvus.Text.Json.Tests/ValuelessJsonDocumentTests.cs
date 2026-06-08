// <copyright file="ValuelessJsonDocumentTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public class ValuelessJsonDocumentTests
{
    [TestMethod]
    public void BooleanTrue_singleton_is_a_true_boolean()
    {
        JsonElement element = ValuelessJsonDocument<JsonElement>.BooleanTrue.RootElement;

        Assert.AreEqual(JsonValueKind.True, element.ValueKind);
        Assert.IsTrue(element.GetBoolean());
        Assert.AreEqual("true", element.ToString());
    }

    [TestMethod]
    public void BooleanFalse_singleton_is_a_false_boolean()
    {
        JsonElement element = ValuelessJsonDocument<JsonElement>.BooleanFalse.RootElement;

        Assert.AreEqual(JsonValueKind.False, element.ValueKind);
        Assert.IsFalse(element.GetBoolean());
        Assert.AreEqual("false", element.ToString());
    }

    [TestMethod]
    public void Null_singleton_is_a_null()
    {
        JsonElement element = ValuelessJsonDocument<JsonElement>.Null.RootElement;

        Assert.AreEqual(JsonValueKind.Null, element.ValueKind);
    }

    [TestMethod]
    public void Singletons_are_shared_instances()
    {
        Assert.AreSame(ValuelessJsonDocument<JsonElement>.BooleanTrue, ValuelessJsonDocument<JsonElement>.BooleanTrue);
        Assert.AreSame(ValuelessJsonDocument<JsonElement>.Null, ValuelessJsonDocument<JsonElement>.Null);
    }
}