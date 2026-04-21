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
using Xunit;

#if STJ
namespace Corvus.Yaml.SystemTextJson.Tests;
#else
namespace Corvus.Text.Json.Yaml.Tests;
#endif

/// <summary>
/// Tests for <see cref="Utf8YamlWriter"/> state machine validation and API coverage.
/// </summary>
public class Utf8YamlWriterTests
{
    // ===================================================================
    // Category 1: State machine — invalid transitions
    // ===================================================================

    [Fact]
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

            Assert.True(threw, "Expected InvalidOperationException for WriteEndMapping in sequence context");
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Fact]
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

            Assert.True(threw, "Expected InvalidOperationException for WriteEndSequence in mapping context");
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Fact]
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

            Assert.True(threw, "Expected InvalidOperationException for WritePropertyName in sequence context");
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Fact]
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

            Assert.True(threw, "Expected InvalidOperationException for WriteStringValue in mapping-key state");
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Fact]
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

            Assert.True(threw, "Expected InvalidOperationException for double WritePropertyName");
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Fact]
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

            Assert.True(threw, "Expected InvalidOperationException for multiple root values");
        }
        finally
        {
            writer.Dispose();
        }
    }

    [Fact]
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

            Assert.True(threw, "Expected InvalidOperationException for WritePropertyName at root");
        }
        finally
        {
            writer.Dispose();
        }
    }

    // ===================================================================
    // Category 2: SkipValidation — invalid transitions are allowed
    // ===================================================================

    [Fact]
    public void SkipValidation_DoesNotThrow()
    {
        ArrayBufferWriter<byte> buffer = new();
        YamlWriterOptions options = new() { SkipValidation = true };
        Utf8YamlWriter writer = new(buffer, options);

        try
        {
            writer.WriteStartSequence();
            // This would throw without SkipValidation
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

    [Fact]
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
        Assert.Equal("name: Alice\nage: 30", yaml);
    }

    [Fact]
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
        Assert.Equal("- 1\n- 2\n- 3", yaml);
    }

    [Fact]
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
        Assert.Equal("\"yes\": true\n\"no\": false", yaml);
    }

    [Fact]
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
        Assert.Equal("key: null", yaml);
    }

    [Fact]
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
        Assert.Equal("{}", yaml);
    }

    [Fact]
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
        Assert.Equal("[]", yaml);
    }

    // ===================================================================
    // Category 4: Flow style collections
    // ===================================================================

    [Fact]
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
        Assert.Equal("{a: 1, b: 2}", yaml);
    }

    [Fact]
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
        Assert.Equal("[1, 2, 3]", yaml);
    }

    // ===================================================================
    // Category 5: Depth tracking
    // ===================================================================

    [Fact]
    public void CurrentDepth_TracksNesting()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer);

        try
        {
            Assert.Equal(0, writer.CurrentDepth);

            writer.WriteStartMapping();
            Assert.Equal(1, writer.CurrentDepth);

            writer.WritePropertyName("nested"u8);
            writer.WriteStartMapping();
            Assert.Equal(2, writer.CurrentDepth);

            writer.WritePropertyName("deep"u8);
            writer.WriteStartSequence();
            Assert.Equal(3, writer.CurrentDepth);

            writer.WriteEndSequence();
            Assert.Equal(2, writer.CurrentDepth);

            writer.WriteEndMapping();
            Assert.Equal(1, writer.CurrentDepth);

            writer.WriteEndMapping();
            Assert.Equal(0, writer.CurrentDepth);
        }
        finally
        {
            writer.Dispose();
        }
    }

    // ===================================================================
    // Category 6: Stream output
    // ===================================================================

    [Fact]
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
        Assert.Equal("key: value", yaml);
    }

    [Fact]
    public void NullBufferWriter_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            Utf8YamlWriter writer = new((IBufferWriter<byte>)null!);
        });
    }

    [Fact]
    public void NullStream_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            Utf8YamlWriter writer = new((System.IO.Stream)null!);
        });
    }

    [Fact]
    public void ReadOnlyStream_Throws()
    {
        using MemoryStream stream = new(Array.Empty<byte>(), writable: false);
        Assert.Throws<ArgumentException>(() =>
        {
            Utf8YamlWriter writer = new(stream);
        });
    }
}
