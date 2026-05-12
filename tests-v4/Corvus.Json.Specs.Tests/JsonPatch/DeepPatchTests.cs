// <copyright file="DeepPatchTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using Corvus.Json.Patch;
using Corvus.Json.Patch.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.JsonPatch
{
    /// <summary>
    /// Tests for deep patching and setting deep properties on JSON documents.
    /// </summary>
    [TestClass]
    public class DeepPatchTests
    {
        [TestMethod]
        public void DeepPatch_Row1()
        {
            string documentJson = "{}";
            string serializedValue = """{"foo": 3 }""";
            string path = "";
            string expected = """{"foo": 3 }""";

            var value = JsonAny.ParseValue(serializedValue.AsSpan());
            JsonAny document = JsonAny.Parse(documentJson);
            PatchBuilder builder = document.BeginPatch().DeepAddOrReplaceObjectProperties(value, path.AsSpan());

            Assert.AreEqual(JsonAny.Parse(expected), builder.Value);
        }

        [TestMethod]
        public void DeepPatch_Row2()
        {
            string documentJson = """{"foo": 2}""";
            string serializedValue = "3";
            string path = "/foo";
            string expected = """{"foo": 3 }""";

            var value = JsonAny.ParseValue(serializedValue.AsSpan());
            JsonAny document = JsonAny.Parse(documentJson);
            PatchBuilder builder = document.BeginPatch().DeepAddOrReplaceObjectProperties(value, path.AsSpan());

            Assert.AreEqual(JsonAny.Parse(expected), builder.Value);
        }

        [TestMethod]
        public void DeepPatch_Row3()
        {
            string documentJson = """{"foo": 2}""";
            string serializedValue = "3";
            string path = "/bar";
            string expected = """{ "foo": 2, "bar": 3 }""";

            var value = JsonAny.ParseValue(serializedValue.AsSpan());
            JsonAny document = JsonAny.Parse(documentJson);
            PatchBuilder builder = document.BeginPatch().DeepAddOrReplaceObjectProperties(value, path.AsSpan());

            Assert.AreEqual(JsonAny.Parse(expected), builder.Value);
        }

        [TestMethod]
        public void DeepPatch_Row4()
        {
            string documentJson = """{"foo": 2}""";
            string serializedValue = "3";
            string path = "/bar/baz";
            string expected = """{ "foo": 2, "bar": { "baz": 3} }""";

            var value = JsonAny.ParseValue(serializedValue.AsSpan());
            JsonAny document = JsonAny.Parse(documentJson);
            PatchBuilder builder = document.BeginPatch().DeepAddOrReplaceObjectProperties(value, path.AsSpan());

            Assert.AreEqual(JsonAny.Parse(expected), builder.Value);
        }

        [TestMethod]
        public void DeepPatch_Row5()
        {
            string documentJson = """{"foo": 2}""";
            string serializedValue = "3";
            string path = "/bar/bash/bank";
            string expected = """{ "foo": 2, "bar": { "bash": {"bank": 3}} }""";

            var value = JsonAny.ParseValue(serializedValue.AsSpan());
            JsonAny document = JsonAny.Parse(documentJson);
            PatchBuilder builder = document.BeginPatch().DeepAddOrReplaceObjectProperties(value, path.AsSpan());

            Assert.AreEqual(JsonAny.Parse(expected), builder.Value);
        }

        [TestMethod]
        public void DeepPatch_Row6()
        {
            string documentJson = """{"foo": [{}] }""";
            string serializedValue = "3";
            string path = "/foo/0/bar";
            string expected = """{ "foo": [ {"bar": 3} ] }""";

            var value = JsonAny.ParseValue(serializedValue.AsSpan());
            JsonAny document = JsonAny.Parse(documentJson);
            PatchBuilder builder = document.BeginPatch().DeepAddOrReplaceObjectProperties(value, path.AsSpan());

            Assert.AreEqual(JsonAny.Parse(expected), builder.Value);
        }

        [TestMethod]
        public void DeepPatch_Row7()
        {
            string documentJson = """{"foo": [] }""";
            string serializedValue = "3";
            string path = "/foo/0/bar";
            string expected = """{ "foo": [ {"bar": 3} ] }""";

            var value = JsonAny.ParseValue(serializedValue.AsSpan());
            JsonAny document = JsonAny.Parse(documentJson);
            PatchBuilder builder = document.BeginPatch().DeepAddOrReplaceObjectProperties(value, path.AsSpan());

            Assert.AreEqual(JsonAny.Parse(expected), builder.Value);
        }

        [TestMethod]
        public void SetDeepProperty_Row1()
        {
            string documentJson = "{}";
            string serializedValue = """{"foo": 3 }""";
            string path = "";
            string expected = """{"foo": 3 }""";

            var value = JsonAny.ParseValue(serializedValue.AsSpan());
            JsonObject document = JsonAny.Parse(documentJson).AsObject;
            document.TrySetDeepProperty(path.AsSpan(), value, out JsonObject result);

            Assert.AreEqual(JsonAny.Parse(expected), result.AsAny);
        }

        [TestMethod]
        public void SetDeepProperty_Row2()
        {
            string documentJson = """{"foo": 2}""";
            string serializedValue = "3";
            string path = "/foo";
            string expected = """{"foo": 3 }""";

            var value = JsonAny.ParseValue(serializedValue.AsSpan());
            JsonObject document = JsonAny.Parse(documentJson).AsObject;
            document.TrySetDeepProperty(path.AsSpan(), value, out JsonObject result);

            Assert.AreEqual(JsonAny.Parse(expected), result.AsAny);
        }

        [TestMethod]
        public void SetDeepProperty_Row3()
        {
            string documentJson = """{"foo": 2}""";
            string serializedValue = "3";
            string path = "/bar";
            string expected = """{ "foo": 2, "bar": 3 }""";

            var value = JsonAny.ParseValue(serializedValue.AsSpan());
            JsonObject document = JsonAny.Parse(documentJson).AsObject;
            document.TrySetDeepProperty(path.AsSpan(), value, out JsonObject result);

            Assert.AreEqual(JsonAny.Parse(expected), result.AsAny);
        }

        [TestMethod]
        public void SetDeepProperty_Row4()
        {
            string documentJson = """{"foo": 2}""";
            string serializedValue = "3";
            string path = "/bar/baz";
            string expected = """{ "foo": 2, "bar": { "baz": 3} }""";

            var value = JsonAny.ParseValue(serializedValue.AsSpan());
            JsonObject document = JsonAny.Parse(documentJson).AsObject;
            document.TrySetDeepProperty(path.AsSpan(), value, out JsonObject result);

            Assert.AreEqual(JsonAny.Parse(expected), result.AsAny);
        }

        [TestMethod]
        public void SetDeepProperty_Row5()
        {
            string documentJson = """{"foo": 2}""";
            string serializedValue = "3";
            string path = "/bar/bash/bank";
            string expected = """{ "foo": 2, "bar": { "bash": {"bank": 3}} }""";

            var value = JsonAny.ParseValue(serializedValue.AsSpan());
            JsonObject document = JsonAny.Parse(documentJson).AsObject;
            document.TrySetDeepProperty(path.AsSpan(), value, out JsonObject result);

            Assert.AreEqual(JsonAny.Parse(expected), result.AsAny);
        }

        [TestMethod]
        public void SetDeepProperty_Row6()
        {
            string documentJson = """{"foo": [{}] }""";
            string serializedValue = "3";
            string path = "/foo/0/bar";
            string expected = """{ "foo": [ {"bar": 3} ] }""";

            var value = JsonAny.ParseValue(serializedValue.AsSpan());
            JsonObject document = JsonAny.Parse(documentJson).AsObject;
            document.TrySetDeepProperty(path.AsSpan(), value, out JsonObject result);

            Assert.AreEqual(JsonAny.Parse(expected), result.AsAny);
        }

        [TestMethod]
        public void SetDeepProperty_Row7()
        {
            string documentJson = """{"foo": [] }""";
            string serializedValue = "3";
            string path = "/foo/0/bar";
            string expected = """{ "foo": [ {"bar": 3} ] }""";

            var value = JsonAny.ParseValue(serializedValue.AsSpan());
            JsonObject document = JsonAny.Parse(documentJson).AsObject;
            document.TrySetDeepProperty(path.AsSpan(), value, out JsonObject result);

            Assert.AreEqual(JsonAny.Parse(expected), result.AsAny);
        }
    }
}