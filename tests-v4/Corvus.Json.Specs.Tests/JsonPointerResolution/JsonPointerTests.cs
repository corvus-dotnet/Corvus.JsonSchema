// <copyright file="JsonPointerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Corvus.Json;
using Xunit;

namespace Corvus.Json.Specs.Tests.JsonPointerResolution
{
    public class JsonPointerTests
    {
        [Fact]
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

            Assert.True(result);
            Assert.Equal(6, line);
            Assert.Equal(17, charOffset);
        }

        [Fact]
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

            Assert.True(result);
            Assert.Equal(1, line);
            Assert.Equal(4, charOffset);
        }

        [Fact]
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

            Assert.True(result);
            Assert.Equal(6, line);
            Assert.Equal(4, charOffset);
        }

        [Fact]
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

            Assert.False(result);
        }

        [Fact]
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

            Assert.False(result);
        }

        [Fact]
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

            Assert.False(result);
        }

        [Fact]
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

            Assert.False(result);
        }

        [Fact]
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

            Assert.True(result);
            Assert.Equal(2, line);
            Assert.Equal(19, charOffset);
        }

        [Fact]
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

            Assert.True(result);
            Assert.Equal(3, line);
            Assert.Equal(19, charOffset);
        }

        [Fact]
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

            Assert.True(result);
            Assert.Equal(7, line);
            Assert.Equal(19, charOffset);
        }

        [Fact]
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

            Assert.True(result);
            Assert.Equal(8, line);
            Assert.Equal(19, charOffset);
        }

        [Fact]
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

            Assert.True(result);
            Assert.Equal(12, line);
            Assert.Equal(8, charOffset);
        }

        [Fact]
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

            Assert.True(result);
            Assert.Equal(12, line);
            Assert.Equal(23, charOffset);
        }

        [Fact]
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

            Assert.True(result);
            Assert.Equal(12, line);
            Assert.Equal(35, charOffset);
        }

        [Fact]
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

            Assert.True(result);
            Assert.Equal(12, line);
            Assert.Equal(44, charOffset);
        }

        [Fact]
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

            Assert.True(result);
            Assert.Equal(1, line);
            Assert.Equal(4, charOffset);
        }

        [Fact]
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

            Assert.True(result);
            Assert.Equal(6, line);
            Assert.Equal(4, charOffset);
        }

        [Fact]
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

            Assert.True(result);
            Assert.Equal(8, line);
            Assert.Equal(19, charOffset);
        }

        [Fact]
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

            Assert.False(result);
        }

        [Fact]
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

            Assert.False(result);
        }

        [Fact]
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

            Assert.False(result);
        }
    }
}