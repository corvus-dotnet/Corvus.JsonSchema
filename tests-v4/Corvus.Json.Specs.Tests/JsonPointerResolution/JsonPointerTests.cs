// <copyright file="JsonPointerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Corvus.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.JsonPointerResolution
{
    [TestClass]
    public class JsonPointerTests
    {
        [TestMethod]
        public void ResolveTopLevelPropertyPointer()
        {
            byte[] jsonFile = Encoding.UTF8.GetBytes(
                """
                {
                    "property1": {
                        "nested1": {
                            "foo": "bar"
                        }
                    },
                    "property2": {
                        "nested1": {
                            "foo": "bar"
                        }
                    }
                }
                """);

            bool result = JsonPointerUtilities.TryGetLineAndOffsetForPointer(
                jsonFile,
                "#/property2".AsSpan(),
                out int line,
                out int charOffset,
                out long lineOffset);

            Assert.IsTrue(result);
            Assert.AreEqual(6, line);
            Assert.AreEqual(17, charOffset);
        }

        [TestMethod]
        public void ResolveTopLevelArray_Pointer0()
        {
            byte[] jsonFile = Encoding.UTF8.GetBytes(
                """
                [
                    {
                        "nested1": {
                            "foo": "bar"
                        }
                    },
                    {
                        "nested1": {
                            "foo": "bar"
                        }
                    }
                }
                """);

            bool result = JsonPointerUtilities.TryGetLineAndOffsetForPointer(
                jsonFile,
                "#/0".AsSpan(),
                out int line,
                out int charOffset,
                out long lineOffset);

            Assert.IsTrue(result);
            Assert.AreEqual(1, line);
            Assert.AreEqual(4, charOffset);
        }

        [TestMethod]
        public void ResolveTopLevelArray_Pointer1()
        {
            byte[] jsonFile = Encoding.UTF8.GetBytes(
                """
                [
                    {
                        "nested1": {
                            "foo": "bar"
                        }
                    },
                    {
                        "nested1": {
                            "foo": "bar"
                        }
                    }
                }
                """);

            bool result = JsonPointerUtilities.TryGetLineAndOffsetForPointer(
                jsonFile,
                "#/1".AsSpan(),
                out int line,
                out int charOffset,
                out long lineOffset);

            Assert.IsTrue(result);
            Assert.AreEqual(6, line);
            Assert.AreEqual(4, charOffset);
        }

        [TestMethod]
        public void NonExistentTopLevelPointer_Property3()
        {
            byte[] jsonFile = Encoding.UTF8.GetBytes(
                """
                {
                    "property1": {
                        "nested1": {
                            "foo": "bar"
                        }
                    },
                    "property2": {
                        "nested1": {
                            "foo": "bar"
                        }
                    }
                }
                """);

            bool result = JsonPointerUtilities.TryGetLineAndOffsetForPointer(
                jsonFile,
                "#/property3".AsSpan(),
                out int line,
                out int charOffset,
                out long lineOffset);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void NonExistentTopLevelPointer_Nested1()
        {
            byte[] jsonFile = Encoding.UTF8.GetBytes(
                """
                {
                    "property1": {
                        "nested1": {
                            "foo": "bar"
                        }
                    },
                    "property2": {
                        "nested1": {
                            "foo": "bar"
                        }
                    }
                }
                """);

            bool result = JsonPointerUtilities.TryGetLineAndOffsetForPointer(
                jsonFile,
                "#/nested1".AsSpan(),
                out int line,
                out int charOffset,
                out long lineOffset);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void NonExistentTopLevelPointer_Foo()
        {
            byte[] jsonFile = Encoding.UTF8.GetBytes(
                """
                {
                    "property1": {
                        "nested1": {
                            "foo": "bar"
                        }
                    },
                    "property2": {
                        "nested1": {
                            "foo": "bar"
                        }
                    }
                }
                """);

            bool result = JsonPointerUtilities.TryGetLineAndOffsetForPointer(
                jsonFile,
                "#/foo".AsSpan(),
                out int line,
                out int charOffset,
                out long lineOffset);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void NonExistentTopLevelPointer_ArrayIndex0()
        {
            byte[] jsonFile = Encoding.UTF8.GetBytes(
                """
                {
                    "property1": {
                        "nested1": {
                            "foo": "bar"
                        }
                    },
                    "property2": {
                        "nested1": {
                            "foo": "bar"
                        }
                    }
                }
                """);

            bool result = JsonPointerUtilities.TryGetLineAndOffsetForPointer(
                jsonFile,
                "#/0".AsSpan(),
                out int line,
                out int charOffset,
                out long lineOffset);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ResolveNestedPointer_Property1Nested1()
        {
            byte[] jsonFile = Encoding.UTF8.GetBytes(
                """
                {
                    "property1": {
                        "nested1": {
                            "foo": "bar"
                        }
                    },
                    "property2": {
                        "nested1": {
                            "foo": "bar"
                        }
                    },
                    "array": [
                        { "property3": { "nested": { "foo": "bar" } } },
                        { "property3": { "nested": { "foo": "bar" } } },
                    ]
                }
                """);

            bool result = JsonPointerUtilities.TryGetLineAndOffsetForPointer(
                jsonFile,
                "#/property1/nested1".AsSpan(),
                out int line,
                out int charOffset,
                out long lineOffset);

            Assert.IsTrue(result);
            Assert.AreEqual(2, line);
            Assert.AreEqual(19, charOffset);
        }

        [TestMethod]
        public void ResolveNestedPointer_Property1Nested1Foo()
        {
            byte[] jsonFile = Encoding.UTF8.GetBytes(
                """
                {
                    "property1": {
                        "nested1": {
                            "foo": "bar"
                        }
                    },
                    "property2": {
                        "nested1": {
                            "foo": "bar"
                        }
                    },
                    "array": [
                        { "property3": { "nested": { "foo": "bar" } } },
                        { "property3": { "nested": { "foo": "bar" } } },
                    ]
                }
                """);

            bool result = JsonPointerUtilities.TryGetLineAndOffsetForPointer(
                jsonFile,
                "#/property1/nested1/foo".AsSpan(),
                out int line,
                out int charOffset,
                out long lineOffset);

            Assert.IsTrue(result);
            Assert.AreEqual(3, line);
            Assert.AreEqual(19, charOffset);
        }

        [TestMethod]
        public void ResolveNestedPointer_Property2Nested1()
        {
            byte[] jsonFile = Encoding.UTF8.GetBytes(
                """
                {
                    "property1": {
                        "nested1": {
                            "foo": "bar"
                        }
                    },
                    "property2": {
                        "nested1": {
                            "foo": "bar"
                        }
                    },
                    "array": [
                        { "property3": { "nested": { "foo": "bar" } } },
                        { "property3": { "nested": { "foo": "bar" } } },
                    ]
                }
                """);

            bool result = JsonPointerUtilities.TryGetLineAndOffsetForPointer(
                jsonFile,
                "#/property2/nested1".AsSpan(),
                out int line,
                out int charOffset,
                out long lineOffset);

            Assert.IsTrue(result);
            Assert.AreEqual(7, line);
            Assert.AreEqual(19, charOffset);
        }

        [TestMethod]
        public void ResolveNestedPointer_Property2Nested1Foo()
        {
            byte[] jsonFile = Encoding.UTF8.GetBytes(
                """
                {
                    "property1": {
                        "nested1": {
                            "foo": "bar"
                        }
                    },
                    "property2": {
                        "nested1": {
                            "foo": "bar"
                        }
                    },
                    "array": [
                        { "property3": { "nested": { "foo": "bar" } } },
                        { "property3": { "nested": { "foo": "bar" } } },
                    ]
                }
                """);

            bool result = JsonPointerUtilities.TryGetLineAndOffsetForPointer(
                jsonFile,
                "#/property2/nested1/foo".AsSpan(),
                out int line,
                out int charOffset,
                out long lineOffset);

            Assert.IsTrue(result);
            Assert.AreEqual(8, line);
            Assert.AreEqual(19, charOffset);
        }

        [TestMethod]
        public void ResolveNestedPointer_Array0()
        {
            byte[] jsonFile = Encoding.UTF8.GetBytes(
                """
                {
                    "property1": {
                        "nested1": {
                            "foo": "bar"
                        }
                    },
                    "property2": {
                        "nested1": {
                            "foo": "bar"
                        }
                    },
                    "array": [
                        { "property3": { "nested": { "foo": "bar" } } },
                        { "property3": { "nested": { "foo": "bar" } } },
                    ]
                }
                """);

            bool result = JsonPointerUtilities.TryGetLineAndOffsetForPointer(
                jsonFile,
                "#/array/0".AsSpan(),
                out int line,
                out int charOffset,
                out long lineOffset);

            Assert.IsTrue(result);
            Assert.AreEqual(12, line);
            Assert.AreEqual(8, charOffset);
        }

        [TestMethod]
        public void ResolveNestedPointer_Array0Property3()
        {
            byte[] jsonFile = Encoding.UTF8.GetBytes(
                """
                {
                    "property1": {
                        "nested1": {
                            "foo": "bar"
                        }
                    },
                    "property2": {
                        "nested1": {
                            "foo": "bar"
                        }
                    },
                    "array": [
                        { "property3": { "nested": { "foo": "bar" } } },
                        { "property3": { "nested": { "foo": "bar" } } },
                    ]
                }
                """);

            bool result = JsonPointerUtilities.TryGetLineAndOffsetForPointer(
                jsonFile,
                "#/array/0/property3".AsSpan(),
                out int line,
                out int charOffset,
                out long lineOffset);

            Assert.IsTrue(result);
            Assert.AreEqual(12, line);
            Assert.AreEqual(23, charOffset);
        }

        [TestMethod]
        public void ResolveNestedPointer_Array0Property3Nested()
        {
            byte[] jsonFile = Encoding.UTF8.GetBytes(
                """
                {
                    "property1": {
                        "nested1": {
                            "foo": "bar"
                        }
                    },
                    "property2": {
                        "nested1": {
                            "foo": "bar"
                        }
                    },
                    "array": [
                        { "property3": { "nested": { "foo": "bar" } } },
                        { "property3": { "nested": { "foo": "bar" } } },
                    ]
                }
                """);

            bool result = JsonPointerUtilities.TryGetLineAndOffsetForPointer(
                jsonFile,
                "#/array/0/property3/nested".AsSpan(),
                out int line,
                out int charOffset,
                out long lineOffset);

            Assert.IsTrue(result);
            Assert.AreEqual(12, line);
            Assert.AreEqual(35, charOffset);
        }

        [TestMethod]
        public void ResolveNestedPointer_Array0Property3NestedFoo()
        {
            byte[] jsonFile = Encoding.UTF8.GetBytes(
                """
                {
                    "property1": {
                        "nested1": {
                            "foo": "bar"
                        }
                    },
                    "property2": {
                        "nested1": {
                            "foo": "bar"
                        }
                    },
                    "array": [
                        { "property3": { "nested": { "foo": "bar" } } },
                        { "property3": { "nested": { "foo": "bar" } } },
                    ]
                }
                """);

            bool result = JsonPointerUtilities.TryGetLineAndOffsetForPointer(
                jsonFile,
                "#/array/0/property3/nested/foo".AsSpan(),
                out int line,
                out int charOffset,
                out long lineOffset);

            Assert.IsTrue(result);
            Assert.AreEqual(12, line);
            Assert.AreEqual(44, charOffset);
        }

        [TestMethod]
        public void ResolveNestedPropertyInTopLevelArray_Pointer0()
        {
            byte[] jsonFile = Encoding.UTF8.GetBytes(
                """
                [
                    {
                        "nested1": {
                            "foo": "bar"
                        }
                    },
                    {
                        "nested1": {
                            "foo": "bar"
                        }
                    }
                }
                """);

            bool result = JsonPointerUtilities.TryGetLineAndOffsetForPointer(
                jsonFile,
                "#/0".AsSpan(),
                out int line,
                out int charOffset,
                out long lineOffset);

            Assert.IsTrue(result);
            Assert.AreEqual(1, line);
            Assert.AreEqual(4, charOffset);
        }

        [TestMethod]
        public void ResolveNestedPropertyInTopLevelArray_Pointer1()
        {
            byte[] jsonFile = Encoding.UTF8.GetBytes(
                """
                [
                    {
                        "nested1": {
                            "foo": "bar"
                        }
                    },
                    {
                        "nested1": {
                            "foo": "bar"
                        }
                    }
                }
                """);

            bool result = JsonPointerUtilities.TryGetLineAndOffsetForPointer(
                jsonFile,
                "#/1".AsSpan(),
                out int line,
                out int charOffset,
                out long lineOffset);

            Assert.IsTrue(result);
            Assert.AreEqual(6, line);
            Assert.AreEqual(4, charOffset);
        }

        [TestMethod]
        public void ResolveNestedPointerWithComments()
        {
            byte[] jsonFile = Encoding.UTF8.GetBytes(
                """
                {
                    "property1": {
                        "nested1": {
                            "foo": "bar"
                        }
                    },
                    // This is a comment
                    "property2": {
                        "nested1": {
                            "foo": "bar"
                        }
                    }
                }
                """);

            JsonReaderOptions options = new()
            {
                CommentHandling = JsonCommentHandling.Skip,
            };

            bool result = JsonPointerUtilities.TryGetLineAndOffsetForPointer(
                jsonFile,
                "#/property2/nested1".AsSpan(),
                options,
                out int line,
                out int charOffset,
                out long lineOffset);

            Assert.IsTrue(result);
            Assert.AreEqual(8, line);
            Assert.AreEqual(19, charOffset);
        }

        [TestMethod]
        public void NonExistentNestedPointer_Property1Foo()
        {
            byte[] jsonFile = Encoding.UTF8.GetBytes(
                """
                {
                    "property1": {
                        "nested1": {
                            "foo": "bar"
                        }
                    },
                    "property2": {
                        "nested1": {
                            "foo": "bar"
                        }
                    }
                }
                """);

            bool result = JsonPointerUtilities.TryGetLineAndOffsetForPointer(
                jsonFile,
                "#/property1/foo".AsSpan(),
                out int line,
                out int charOffset,
                out long lineOffset);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void NonExistentNestedPointer_Property2Foo()
        {
            byte[] jsonFile = Encoding.UTF8.GetBytes(
                """
                {
                    "property1": {
                        "nested1": {
                            "foo": "bar"
                        }
                    },
                    "property2": {
                        "nested1": {
                            "foo": "bar"
                        }
                    }
                }
                """);

            bool result = JsonPointerUtilities.TryGetLineAndOffsetForPointer(
                jsonFile,
                "#/property2/foo".AsSpan(),
                out int line,
                out int charOffset,
                out long lineOffset);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void NonExistentNestedPointer_Nested1Foo()
        {
            byte[] jsonFile = Encoding.UTF8.GetBytes(
                """
                {
                    "property1": {
                        "nested1": {
                            "foo": "bar"
                        }
                    },
                    "property2": {
                        "nested1": {
                            "foo": "bar"
                        }
                    }
                }
                """);

            bool result = JsonPointerUtilities.TryGetLineAndOffsetForPointer(
                jsonFile,
                "#/nested1/foo".AsSpan(),
                out int line,
                out int charOffset,
                out long lineOffset);

            Assert.IsFalse(result);
        }
    }
}