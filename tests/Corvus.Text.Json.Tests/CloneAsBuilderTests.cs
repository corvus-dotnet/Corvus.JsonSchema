// <copyright file="CloneAsBuilderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public class CloneAsBuilderTests
{
    [TestMethod]
    public void Clone_survives_disposal_of_the_source_document()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();

        JsonElement clone;
        using (ParsedJsonDocument<JsonElement> source =
            ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"name":"Fido","age":3,"tags":["a","b"]}""")))
        {
            clone = source.RootElement.CloneAsBuilder(workspace).RootElement;

            // Valid while the source is alive.
            Assert.IsTrue(clone.TryGetProperty("name", out JsonElement liveName));
            Assert.AreEqual("Fido", liveName.GetString());
        }

        // The source document is now disposed; the clone is a standalone, workspace-owned copy and
        // must remain fully readable (no ObjectDisposedException).
        Assert.IsTrue(clone.TryGetProperty("name", out JsonElement name));
        Assert.AreEqual("Fido", name.GetString());
        Assert.IsTrue(clone.TryGetProperty("age", out JsonElement age));
        Assert.AreEqual(3, age.GetInt32());
        Assert.IsTrue(clone.TryGetProperty("tags", out JsonElement tags));
        Assert.AreEqual(2, tags.GetArrayLength());
    }

    [TestMethod]
    public void Clone_of_a_nested_element_is_independent_of_the_source()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();

        JsonElement clone;
        using (ParsedJsonDocument<JsonElement> source =
            ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"outer":{"inner":"value"}}""")))
        {
            Assert.IsTrue(source.RootElement.TryGetProperty("outer", out JsonElement outer));
            clone = outer.CloneAsBuilder(workspace).RootElement;
        }

        Assert.IsTrue(clone.TryGetProperty("inner", out JsonElement inner));
        Assert.AreEqual("value", inner.GetString());
    }
}