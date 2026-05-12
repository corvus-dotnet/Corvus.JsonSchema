// <copyright file="Utf8YamlWriterTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
#if STJ
using Corvus.Yaml;
#else
using Corvus.Text.Json.Yaml;
#endif
using Microsoft.VisualStudio.TestTools.UnitTesting;

#if STJ
namespace Corvus.Yaml.SystemTextJson.Tests;
#else
namespace Corvus.Text.Json.Yaml.Tests;
#endif

/// <summary>
/// Tests for <see cref="Utf8YamlWriter"/> state machine validation and API coverage.
/// </summary>
[TestClass]
public class Utf8YamlWriterTests
{
    // ===================================================================
    // Category 1: State machine — invalid transitions
    // ===================================================================

    [TestMethod]
    public void WriteEndMapping_InSequence_Throws()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartSequence();

            bool threw = false;
            try
            {
                writer.WriteEndMapping();
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "Expected InvalidOperationException for WriteEndMapping in sequence context");
        }
        finally
        {
            writer.Dispose();
        }
    }

    [TestMethod]
    public void WriteEndSequence_InMapping_Throws()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartMapping();

            bool threw = false;
            try
            {
                writer.WriteEndSequence();
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "Expected InvalidOperationException for WriteEndSequence in mapping context");
        }
        finally
        {
            writer.Dispose();
        }
    }

    [TestMethod]
    public void WritePropertyName_InSequence_Throws()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartSequence();

            bool threw = false;
            try
            {
                writer.WritePropertyName("key"u8);
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "Expected InvalidOperationException for WritePropertyName in sequence context");
        }
        finally
        {
            writer.Dispose();
        }
    }

    [TestMethod]
    public void WriteValue_InMappingKeyState_Throws()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartMapping();

            bool threw = false;
            try
            {
                writer.WriteStringValue("value"u8);
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "Expected InvalidOperationException for WriteStringValue in mapping-key state");
        }
        finally
        {
            writer.Dispose();
        }
    }

    [TestMethod]
    public void WritePropertyName_WhenValueExpected_Throws()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartMapping();
            writer.WritePropertyName("key1"u8);

            bool threw = false;
            try
            {
                writer.WritePropertyName("key2"u8);
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "Expected InvalidOperationException for double WritePropertyName");
        }
        finally
        {
            writer.Dispose();
        }
    }

    [TestMethod]
    public void MultipleRootValues_Throws()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStringValue("first"u8);

            bool threw = false;
            try
            {
                writer.WriteStringValue("second"u8);
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "Expected InvalidOperationException for multiple root values");
        }
        finally
        {
            writer.Dispose();
        }
    }

    [TestMethod]
    public void WritePropertyName_AtRoot_Throws()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            bool threw = false;
            try
            {
                writer.WritePropertyName("key"u8);
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "Expected InvalidOperationException for WritePropertyName at root");
        }
        finally
        {
            writer.Dispose();
        }
    }

    [TestMethod]
    public void WriteStartMapping_AsMappingKey_Throws()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartMapping();

            bool threw = false;
            try
            {
                writer.WriteStartMapping();
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "Expected InvalidOperationException for WriteStartMapping in mapping-key state");
        }
        finally
        {
            writer.Dispose();
        }
    }

    [TestMethod]
    public void WriteStartSequence_AsMappingKey_Throws()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartMapping();

            bool threw = false;
            try
            {
                writer.WriteStartSequence();
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "Expected InvalidOperationException for WriteStartSequence in mapping-key state");
        }
        finally
        {
            writer.Dispose();
        }
    }

    [TestMethod]
    public void WriteEndMapping_NoOpenContainer_Throws()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            bool threw = false;
            try
            {
                writer.WriteEndMapping();
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "Expected InvalidOperationException for WriteEndMapping with no open container");
        }
        finally
        {
            writer.Dispose();
        }
    }

    [TestMethod]
    public void WriteEndSequence_NoOpenContainer_Throws()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            bool threw = false;
            try
            {
                writer.WriteEndSequence();
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "Expected InvalidOperationException for WriteEndSequence with no open container");
        }
        finally
        {
            writer.Dispose();
        }
    }

    [TestMethod]
    public void WriteEndMapping_WhenValueExpected_Throws()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartMapping();
            writer.WritePropertyName("key"u8);

            bool threw = false;
            try
            {
                writer.WriteEndMapping();
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "Expected InvalidOperationException for WriteEndMapping when value expected");
        }
        finally
        {
            writer.Dispose();
        }
    }

    [TestMethod]
    public void MultipleRootContainers_Throws()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartMapping();
            writer.WriteEndMapping();

            bool threw = false;
            try
            {
                writer.WriteStartMapping();
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "Expected InvalidOperationException for second root container");
        }
        finally
        {
            writer.Dispose();
        }
    }

    [TestMethod]
    public void WriteNumberValue_InMappingKeyState_Throws()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartMapping();

            bool threw = false;
            try
            {
                writer.WriteNumberValue("42"u8);
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "Expected InvalidOperationException for WriteNumberValue in mapping-key state");
        }
        finally
        {
            writer.Dispose();
        }
    }

    [TestMethod]
    public void WriteBooleanValue_InMappingKeyState_Throws()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartMapping();

            bool threw = false;
            try
            {
                writer.WriteBooleanValue(true);
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "Expected InvalidOperationException for WriteBooleanValue in mapping-key state");
        }
        finally
        {
            writer.Dispose();
        }
    }

    [TestMethod]
    public void WriteNullValue_InMappingKeyState_Throws()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartMapping();

            bool threw = false;
            try
            {
                writer.WriteNullValue();
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "Expected InvalidOperationException for WriteNullValue in mapping-key state");
        }
        finally
        {
            writer.Dispose();
        }
    }

    [TestMethod]
    public void WriteEmptyMapping_InMappingKeyState_Throws()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartMapping();

            bool threw = false;
            try
            {
                writer.WriteEmptyMapping();
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "Expected InvalidOperationException for WriteEmptyMapping in mapping-key state");
        }
        finally
        {
            writer.Dispose();
        }
    }

    [TestMethod]
    public void WriteEmptySequence_InMappingKeyState_Throws()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartMapping();

            bool threw = false;
            try
            {
                writer.WriteEmptySequence();
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "Expected InvalidOperationException for WriteEmptySequence in mapping-key state");
        }
        finally
        {
            writer.Dispose();
        }
    }

    // ===================================================================
    // Category 2: SkipValidation — invalid transitions are allowed
    // ===================================================================

    [TestMethod]
    public void SkipValidation_ValueInMappingKeyState_DoesNotThrow()
    {
        ArrayBufferWriter<byte> buffer = new();
        YamlWriterOptions options = new() { SkipValidation = true };
        Utf8YamlWriter writer = new(buffer, options);

        try
        {
            writer.WriteStartMapping();
            writer.WriteStringValue("value"u8);
            writer.WriteEndMapping();
        }
        finally
        {
            writer.Dispose();
        }
    }

    [TestMethod]
    public void SkipValidation_ContainerAsMappingKey_DoesNotThrow()
    {
        ArrayBufferWriter<byte> buffer = new();
        YamlWriterOptions options = new() { SkipValidation = true };
        Utf8YamlWriter writer = new(buffer, options);

        try
        {
            writer.WriteStartMapping();
            writer.WriteStartSequence();
            writer.WriteEndSequence();
            writer.WriteEndMapping();
        }
        finally
        {
            writer.Dispose();
        }
    }

    [TestMethod]
    public void SkipValidation_PropertyNameInSequence_DoesNotThrow()
    {
        ArrayBufferWriter<byte> buffer = new();
        YamlWriterOptions options = new() { SkipValidation = true };
        Utf8YamlWriter writer = new(buffer, options);

        try
        {
            writer.WriteStartSequence();
            writer.WritePropertyName("key"u8);
            writer.WriteStringValue("val"u8);
            writer.WriteEndSequence();
        }
        finally
        {
            writer.Dispose();
        }
    }

    [TestMethod]
    public void SkipValidation_DoublePropertyName_DoesNotThrow()
    {
        ArrayBufferWriter<byte> buffer = new();
        YamlWriterOptions options = new() { SkipValidation = true };
        Utf8YamlWriter writer = new(buffer, options);

        try
        {
            writer.WriteStartMapping();
            writer.WritePropertyName("key1"u8);
            writer.WritePropertyName("key2"u8);
            writer.WriteStringValue("val"u8);
            writer.WriteEndMapping();
        }
        finally
        {
            writer.Dispose();
        }
    }

    [TestMethod]
    public void SkipValidation_MultipleRootValues_DoesNotThrow()
    {
        ArrayBufferWriter<byte> buffer = new();
        YamlWriterOptions options = new() { SkipValidation = true };
        Utf8YamlWriter writer = new(buffer, options);

        try
        {
            writer.WriteStringValue("first"u8);
            writer.WriteStringValue("second"u8);
        }
        finally
        {
            writer.Dispose();
        }
    }

    [TestMethod]
    public void SkipValidation_MultipleRootContainers_DoesNotThrow()
    {
        ArrayBufferWriter<byte> buffer = new();
        YamlWriterOptions options = new() { SkipValidation = true };
        Utf8YamlWriter writer = new(buffer, options);

        try
        {
            writer.WriteStartMapping();
            writer.WriteEndMapping();
            writer.WriteStartSequence();
            writer.WriteEndSequence();
        }
        finally
        {
            writer.Dispose();
        }
    }

    [TestMethod]
    public void SkipValidation_EndMappingWhenValueExpected_DoesNotThrow()
    {
        ArrayBufferWriter<byte> buffer = new();
        YamlWriterOptions options = new() { SkipValidation = true };
        Utf8YamlWriter writer = new(buffer, options);

        try
        {
            writer.WriteStartMapping();
            writer.WritePropertyName("key"u8);
            writer.WriteEndMapping();
        }
        finally
        {
            writer.Dispose();
        }
    }

    [TestMethod]
    public void SkipValidation_EndMappingInSequence_DoesNotThrow()
    {
        ArrayBufferWriter<byte> buffer = new();
        YamlWriterOptions options = new() { SkipValidation = true };
        Utf8YamlWriter writer = new(buffer, options);

        try
        {
            writer.WriteStartSequence();
            writer.WriteStartMapping();
            writer.WriteEndMapping();
            // End the outer sequence with EndMapping — structurally wrong, but validation is off
            // Note: the context stack pop will still work because there's still an open context
            writer.WriteEndSequence();
        }
        finally
        {
            writer.Dispose();
        }
    }

    // ===================================================================
    // Category 3: Valid write sequences
    // ===================================================================

    [TestMethod]
    public void WriteSimpleMapping()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartMapping();
            writer.WritePropertyName("name"u8);
            writer.WriteStringValue("Alice"u8);
            writer.WritePropertyName("age"u8);
            writer.WriteNumberValue("30"u8);
            writer.WriteEndMapping();
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

#if NET
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
#endif
        Assert.AreEqual("name: Alice\nage: 30", yaml);
    }

    [TestMethod]
    public void WriteSimpleSequence()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartSequence();
            writer.WriteNumberValue("1"u8);
            writer.WriteNumberValue("2"u8);
            writer.WriteNumberValue("3"u8);
            writer.WriteEndSequence();
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

#if NET
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
#endif
        Assert.AreEqual("- 1\n- 2\n- 3", yaml);
    }

    [TestMethod]
    public void WriteBooleanValues()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartMapping();
            writer.WritePropertyName("yes"u8);
            writer.WriteBooleanValue(true);
            writer.WritePropertyName("no"u8);
            writer.WriteBooleanValue(false);
            writer.WriteEndMapping();
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

#if NET
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
#endif
        // "yes" and "no" are reserved YAML 1.1 words, so they get quoted as keys
        Assert.AreEqual("\"yes\": true\n\"no\": false", yaml);
    }

    [TestMethod]
    public void WriteNullValue()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartMapping();
            writer.WritePropertyName("key"u8);
            writer.WriteNullValue();
            writer.WriteEndMapping();
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

#if NET
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
#endif
        Assert.AreEqual("key: null", yaml);
    }

    [TestMethod]
    public void WriteEmptyMapping()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteEmptyMapping();
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

#if NET
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
#endif
        Assert.AreEqual("{}", yaml);
    }

    [TestMethod]
    public void WriteEmptySequence()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteEmptySequence();
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

#if NET
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
#endif
        Assert.AreEqual("[]", yaml);
    }

    // ===================================================================
    // Category 4: Flow style collections
    // ===================================================================

    [TestMethod]
    public void WriteFlowMapping()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartMapping(YamlCollectionStyle.Flow);
            writer.WritePropertyName("a"u8);
            writer.WriteNumberValue("1"u8);
            writer.WritePropertyName("b"u8);
            writer.WriteNumberValue("2"u8);
            writer.WriteEndMapping();
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

#if NET
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
#endif
        Assert.AreEqual("{a: 1, b: 2}", yaml);
    }

    [TestMethod]
    public void WriteFlowSequence()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartSequence(YamlCollectionStyle.Flow);
            writer.WriteNumberValue("1"u8);
            writer.WriteNumberValue("2"u8);
            writer.WriteNumberValue("3"u8);
            writer.WriteEndSequence();
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

#if NET
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
#endif
        Assert.AreEqual("[1, 2, 3]", yaml);
    }

    // ===================================================================
    // Category 5: Depth tracking
    // ===================================================================

    [TestMethod]
    public void CurrentDepth_TracksNesting()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            Assert.AreEqual(0, writer.CurrentDepth);

            writer.WriteStartMapping();
            Assert.AreEqual(1, writer.CurrentDepth);

            writer.WritePropertyName("nested"u8);
            writer.WriteStartMapping();
            Assert.AreEqual(2, writer.CurrentDepth);

            writer.WritePropertyName("deep"u8);
            writer.WriteStartSequence();
            Assert.AreEqual(3, writer.CurrentDepth);

            writer.WriteEndSequence();
            Assert.AreEqual(2, writer.CurrentDepth);

            writer.WriteEndMapping();
            Assert.AreEqual(1, writer.CurrentDepth);

            writer.WriteEndMapping();
            Assert.AreEqual(0, writer.CurrentDepth);
        }
        finally
        {
            writer.Dispose();
        }
    }

    // ===================================================================
    // Category 6: Stream output
    // ===================================================================

    [TestMethod]
    public void WriteToStream()
    {
        using MemoryStream stream = new();
        Utf8YamlWriter writer = new(stream);

        try
        {
            writer.WriteStartMapping();
            writer.WritePropertyName("key"u8);
            writer.WriteStringValue("value"u8);
            writer.WriteEndMapping();
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

        stream.Position = 0;
        string yaml = new StreamReader(stream, Encoding.UTF8).ReadToEnd();
        Assert.AreEqual("key: value", yaml);
    }

    [TestMethod]
    public void NullBufferWriter_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
        {
            Utf8YamlWriter writer = new((IBufferWriter<byte>)null!);
        });
    }

    [TestMethod]
    public void NullStream_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
        {
            Utf8YamlWriter writer = new((System.IO.Stream)null!);
        });
    }

    [TestMethod]
    public void ReadOnlyStream_Throws()
    {
        using MemoryStream stream = new(Array.Empty<byte>(), writable: false);
        Assert.ThrowsExactly<ArgumentException>(() =>
        {
            Utf8YamlWriter writer = new(stream);
        });
    }

    // ===================================================================
    // Category 7: Nested structures (block)
    // ===================================================================

    [TestMethod]
    public void WriteMappingInsideMapping()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartMapping();
            writer.WritePropertyName("outer"u8);
            writer.WriteStartMapping();
            writer.WritePropertyName("inner"u8);
            writer.WriteStringValue("value"u8);
            writer.WriteEndMapping();
            writer.WriteEndMapping();
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

#if NET
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
#endif
        Assert.AreEqual("outer:\n  inner: value", yaml);
    }

    [TestMethod]
    public void WriteSequenceInsideMapping()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartMapping();
            writer.WritePropertyName("items"u8);
            writer.WriteStartSequence();
            writer.WriteStringValue("a"u8);
            writer.WriteStringValue("b"u8);
            writer.WriteEndSequence();
            writer.WriteEndMapping();
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

#if NET
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
#endif
        Assert.AreEqual("items:\n  - a\n  - b", yaml);
    }

    [TestMethod]
    public void WriteMappingInsideSequence()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartSequence();
            writer.WriteStartMapping();
            writer.WritePropertyName("key"u8);
            writer.WriteStringValue("val"u8);
            writer.WriteEndMapping();
            writer.WriteEndSequence();
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

#if NET
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
#endif
        Assert.AreEqual("- \n  key: val", yaml);
    }

    [TestMethod]
    public void WriteSequenceInsideSequence()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartSequence();
            writer.WriteStartSequence();
            writer.WriteNumberValue("1"u8);
            writer.WriteNumberValue("2"u8);
            writer.WriteEndSequence();
            writer.WriteEndSequence();
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

#if NET
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
#endif
        Assert.AreEqual("- \n  - 1\n  - 2", yaml);
    }

    [TestMethod]
    public void WriteDeeplyNestedStructure()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartMapping();
            writer.WritePropertyName("a"u8);
            writer.WriteStartMapping();
            writer.WritePropertyName("b"u8);
            writer.WriteStartMapping();
            writer.WritePropertyName("c"u8);
            writer.WriteStartSequence();
            writer.WriteNumberValue("1"u8);
            writer.WriteEndSequence();
            writer.WriteEndMapping();
            writer.WriteEndMapping();
            writer.WriteEndMapping();
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

#if NET
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
#endif
        Assert.AreEqual("a:\n  b:\n    c:\n      - 1", yaml);
    }

    [TestMethod]
    public void WriteFlowInsideBlock()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartMapping();
            writer.WritePropertyName("tags"u8);
            writer.WriteStartSequence(YamlCollectionStyle.Flow);
            writer.WriteStringValue("a"u8);
            writer.WriteStringValue("b"u8);
            writer.WriteEndSequence();
            writer.WriteEndMapping();
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

#if NET
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
#endif
        Assert.AreEqual("tags: [a, b]", yaml);
    }

    [TestMethod]
    public void WriteFlowMappingInsideBlockMapping()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartMapping();
            writer.WritePropertyName("config"u8);
            writer.WriteStartMapping(YamlCollectionStyle.Flow);
            writer.WritePropertyName("x"u8);
            writer.WriteNumberValue("1"u8);
            writer.WritePropertyName("y"u8);
            writer.WriteNumberValue("2"u8);
            writer.WriteEndMapping();
            writer.WriteEndMapping();
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

#if NET
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
#endif
        Assert.AreEqual("config: {x: 1, \"y\": 2}", yaml);
    }

    [TestMethod]
    public void WriteEmptyMappingAsValue()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartMapping();
            writer.WritePropertyName("empty"u8);
            writer.WriteEmptyMapping();
            writer.WriteEndMapping();
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

#if NET
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
#endif
        Assert.AreEqual("empty: {}", yaml);
    }

    [TestMethod]
    public void WriteEmptySequenceAsValue()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartMapping();
            writer.WritePropertyName("empty"u8);
            writer.WriteEmptySequence();
            writer.WriteEndMapping();
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

#if NET
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
#endif
        Assert.AreEqual("empty: []", yaml);
    }

    [TestMethod]
    public void WriteEmptyMappingAsSequenceItem()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartSequence();
            writer.WriteEmptyMapping();
            writer.WriteEmptyMapping();
            writer.WriteEndSequence();
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

#if NET
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
#endif
        Assert.AreEqual("- {}\n- {}", yaml);
    }

    [TestMethod]
    public void WriteAllValueTypesInSequence()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartSequence();
            writer.WriteStringValue("text"u8);
            writer.WriteNumberValue("42"u8);
            writer.WriteBooleanValue(true);
            writer.WriteBooleanValue(false);
            writer.WriteNullValue();
            writer.WriteEmptyMapping();
            writer.WriteEmptySequence();
            writer.WriteEndSequence();
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

#if NET
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
#endif
        Assert.AreEqual("- text\n- 42\n- true\n- false\n- null\n- {}\n- []", yaml);
    }

    [TestMethod]
    public void WriteNestingExceedsInitialStackCapacity()
    {
        // MaxStackDepth is 16 — exceed it to exercise the ValueListBuilder growth path
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            const int depth = 20;
            for (int i = 0; i < depth; i++)
            {
                writer.WriteStartSequence();
            }

            writer.WriteStringValue("leaf"u8);

            for (int i = 0; i < depth; i++)
            {
                writer.WriteEndSequence();
            }

            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

        // Verify it completed without error and depth returned to 0
#if NET
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
#endif
        StringAssert.Contains(yaml, "leaf");
    }

    // ===================================================================
    // Category 8: Incomplete document at dispose (no validation on dispose)
    // ===================================================================

    [TestMethod]
    public void IncompleteMapping_DisposeDoesNotThrow()
    {
        // The writer does not validate completeness on Flush/Dispose
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartMapping();
            writer.WritePropertyName("key"u8);
            // No value written — document is structurally incomplete
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

        // Incomplete output — the writer doesn't error on incomplete documents
#if NET
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
#endif
        StringAssert.Contains(yaml, "key:");
    }

    [TestMethod]
    public void UnclosedContainer_DisposeDoesNotThrow()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartMapping();
            writer.WritePropertyName("a"u8);
            writer.WriteStringValue("b"u8);
            // No WriteEndMapping — container left open
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

#if NET
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
#endif
        Assert.AreEqual("a: b", yaml);
    }

    // ===================================================================
    // Category 9: Scalar quoting
    // ===================================================================

    [TestMethod]
    [DataRow("hello", "hello")]
    [DataRow("plain text", "plain text")]
    [DataRow("no-quotes-needed", "no-quotes-needed")]
    public void PlainScalar_NotQuoted(string input, string expected)
    {
        string yaml = WriteRootStringValue(input);
        Assert.AreEqual(expected, yaml);
    }

    [TestMethod]
    [DataRow("null", "\"null\"")]
    [DataRow("Null", "\"Null\"")]
    [DataRow("NULL", "\"NULL\"")]
    [DataRow("~", "\"~\"")]
    [DataRow("true", "\"true\"")]
    [DataRow("True", "\"True\"")]
    [DataRow("TRUE", "\"TRUE\"")]
    [DataRow("false", "\"false\"")]
    [DataRow("False", "\"False\"")]
    [DataRow("FALSE", "\"FALSE\"")]
    [DataRow("yes", "\"yes\"")]
    [DataRow("Yes", "\"Yes\"")]
    [DataRow("YES", "\"YES\"")]
    [DataRow("no", "\"no\"")]
    [DataRow("No", "\"No\"")]
    [DataRow("NO", "\"NO\"")]
    [DataRow("on", "\"on\"")]
    [DataRow("On", "\"On\"")]
    [DataRow("ON", "\"ON\"")]
    [DataRow("off", "\"off\"")]
    [DataRow("Off", "\"Off\"")]
    [DataRow("OFF", "\"OFF\"")]
    [DataRow("y", "\"y\"")]
    [DataRow("Y", "\"Y\"")]
    [DataRow("n", "\"n\"")]
    [DataRow("N", "\"N\"")]
    public void ReservedWord_IsQuoted(string input, string expected)
    {
        string yaml = WriteRootStringValue(input);
        Assert.AreEqual(expected, yaml);
    }

    [TestMethod]
    [DataRow("42", "\"42\"")]
    [DataRow("3.14", "\"3.14\"")]
    [DataRow("-1", "\"-1\"")]
    [DataRow("+1", "\"+1\"")]
    [DataRow(".5", "\".5\"")]
    [DataRow("0x1A", "\"0x1A\"")]
    [DataRow("0o77", "\"0o77\"")]
    public void NumericLooking_IsQuoted(string input, string expected)
    {
        string yaml = WriteRootStringValue(input);
        Assert.AreEqual(expected, yaml);
    }

    [TestMethod]
    [DataRow("", "\"\"")]
    [DataRow(" leading", "\" leading\"")]
    [DataRow("trailing ", "\"trailing \"")]
    public void LeadingTrailingWhitespace_IsQuoted(string input, string expected)
    {
        string yaml = WriteRootStringValue(input);
        Assert.AreEqual(expected, yaml);
    }

    [TestMethod]
    [DataRow("key: value", "\"key: value\"")]
    [DataRow("a #comment", "\"a #comment\"")]
    public void InlineIndicators_AreQuoted(string input, string expected)
    {
        string yaml = WriteRootStringValue(input);
        Assert.AreEqual(expected, yaml);
    }

    [TestMethod]
    [DataRow("&anchor", "\"&anchor\"")]
    [DataRow("*alias", "\"*alias\"")]
    [DataRow("!tag", "\"!tag\"")]
    [DataRow("|block", "\"|block\"")]
    [DataRow(">fold", "\">fold\"")]
    [DataRow("'single", "\"'single\"")]
    [DataRow("%directive", "\"%directive\"")]
    [DataRow("@at", "\"@at\"")]
    [DataRow("{open", "\"{open\"")]
    [DataRow("}close", "\"}close\"")]
    [DataRow("[open", "\"[open\"")]
    [DataRow("]close", "\"]close\"")]
    [DataRow(",comma", "\",comma\"")]
    [DataRow("#hash", "\"#hash\"")]
    public void LeadingIndicator_IsQuoted(string input, string expected)
    {
        string yaml = WriteRootStringValue(input);
        Assert.AreEqual(expected, yaml);
    }

    [TestMethod]
    [DataRow("- item", "\"- item\"")]
    [DataRow("? query", "\"? query\"")]
    [DataRow(": value", "\": value\"")]
    [DataRow("-", "\"-\"")]
    [DataRow("?", "\"?\"")]
    [DataRow(":", "\":\"")]
    public void DashQuestionColon_IsQuoted(string input, string expected)
    {
        string yaml = WriteRootStringValue(input);
        Assert.AreEqual(expected, yaml);
    }

    [TestMethod]
    public void EscapeSequences_InQuotedValue()
    {
        string yaml = WriteRootStringValue("line1\nline2");
        Assert.AreEqual("\"line1\\nline2\"", yaml);
    }

    [TestMethod]
    public void Backslash_NotQuoted_WhenNotIndicator()
    {
        // Backslash is not a YAML indicator, so plain scalars with backslash are not quoted
        string yaml = WriteRootStringValue("a\\b");
        Assert.AreEqual("a\\b", yaml);
    }

    [TestMethod]
    public void DoubleQuote_AtStart_IsQuoted()
    {
        // Leading double-quote IS a YAML indicator
        string yaml = WriteRootStringValue("\"hi\"");
        Assert.AreEqual("\"\\\"hi\\\"\"", yaml);
    }

    [TestMethod]
    public void ReservedWordAsKey_IsQuoted()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartMapping();
            writer.WritePropertyName("null"u8);
            writer.WriteStringValue("value"u8);
            writer.WriteEndMapping();
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

#if NET
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
#endif
        Assert.AreEqual("\"null\": value", yaml);
    }

    [TestMethod]
    public void NumericLookingKey_IsQuoted()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartMapping();
            writer.WritePropertyName("42"u8);
            writer.WriteStringValue("val"u8);
            writer.WriteEndMapping();
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

#if NET
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
#endif
        Assert.AreEqual("\"42\": val", yaml);
    }

    [TestMethod]
    public void InlineIndicatorKey_IsQuoted()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            writer.WriteStartMapping();
            writer.WritePropertyName("a: b"u8);
            writer.WriteStringValue("val"u8);
            writer.WriteEndMapping();
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

#if NET
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
#endif
        Assert.AreEqual("\"a: b\": val", yaml);
    }

    // ===================================================================
    // Category 10: IndentSize customization
    // ===================================================================

    [TestMethod]
    public void IndentSize_4_ProducesCorrectIndentation()
    {
        ArrayBufferWriter<byte> buffer = new();
        YamlWriterOptions options = new() { IndentSize = 4 };
        Utf8YamlWriter writer = new(buffer, options);

        try
        {
            writer.WriteStartMapping();
            writer.WritePropertyName("outer"u8);
            writer.WriteStartMapping();
            writer.WritePropertyName("inner"u8);
            writer.WriteStringValue("val"u8);
            writer.WriteEndMapping();
            writer.WriteEndMapping();
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

#if NET
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
#endif
        Assert.AreEqual("outer:\n    inner: val", yaml);
    }

    [TestMethod]
    public void IndentSize_ZeroOrNegative_FallsBackToDefault()
    {
        ArrayBufferWriter<byte> buffer = new();
        YamlWriterOptions options = new() { IndentSize = 0 };
        Utf8YamlWriter writer = new(buffer, options);

        try
        {
            writer.WriteStartMapping();
            writer.WritePropertyName("outer"u8);
            writer.WriteStartMapping();
            writer.WritePropertyName("inner"u8);
            writer.WriteStringValue("val"u8);
            writer.WriteEndMapping();
            writer.WriteEndMapping();
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

#if NET
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
#endif
        // Default indent is 2
        Assert.AreEqual("outer:\n  inner: val", yaml);
    }

    // ===================================================================
    // Helper methods
    // ===================================================================

    private static string WriteRootStringValue(string value)
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(value);
            writer.WriteStringValue(utf8);
            writer.Flush();
        }
        finally
        {
            writer.Dispose();
        }

#if NET
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
#else
        return Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());
#endif
    }
}
