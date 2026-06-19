// <copyright file="KyamlTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
#if STJ
using System.Text.Json;
using Corvus.Yaml;
#else
using Corvus.Text.Json;
using Corvus.Text.Json.Yaml;
#endif
using Microsoft.VisualStudio.TestTools.UnitTesting;

#if STJ
namespace Corvus.Yaml.SystemTextJson.Tests;
#else
namespace Corvus.Text.Json.Yaml.Tests;
#endif

/// <summary>
/// Tests for KYAML output (<see cref="YamlWriterFormat.Kyaml"/>): a strict subset of
/// YAML 1.2 with explicit delimiters, quoted string values, and trailing commas.
/// </summary>
[TestClass]
public class KyamlTests
{
    // ===================================================================
    // Output shape
    // ===================================================================

    [TestMethod]
    public void NestedObject_ProducesExpectedLayout()
    {
        string json =
            """{"name":"hostnames","port":80,"enabled":true,"meta":{"a":"b"},"tags":["x","y"],"empty":{},"none":null}""";

        string expected =
            """
            {
              name: "hostnames",
              port: 80,
              enabled: true,
              meta: {
                a: "b",
              },
              tags: [
                "x",
                "y",
              ],
              empty: {},
              none: null,
            }
            """;

        string kyaml = YamlDocument.ConvertToYamlString(json, YamlWriterOptions.Kyaml);
        Assert.AreEqual(Lf(expected), kyaml);
    }

    [TestMethod]
    public void ArrayOfObjects_ProducesExpectedLayout()
    {
        string json = """[{"a":1},{"b":2}]""";

        string expected =
            """
            [
              {
                a: 1,
              },
              {
                b: 2,
              },
            ]
            """;

        string kyaml = YamlDocument.ConvertToYamlString(json, YamlWriterOptions.Kyaml);
        Assert.AreEqual(Lf(expected), kyaml);
    }

    // ===================================================================
    // Scalar quoting — strings always quoted; numbers/bools/null bare
    // ===================================================================

    [TestMethod]
    public void StringValuesAreAlwaysQuoted_IncludingTypeAmbiguousWords()
    {
        // The "Norway problem": NO must remain a quoted string, and a string
        // that looks like a number ("123") or a bool ("true") must be quoted too.
        string json = """{"country":"NO","str":"123","flag":"true"}""";

        string expected =
            """
            {
              country: "NO",
              str: "123",
              flag: "true",
            }
            """;

        string kyaml = YamlDocument.ConvertToYamlString(json, YamlWriterOptions.Kyaml);
        Assert.AreEqual(Lf(expected), kyaml);
    }

    [TestMethod]
    public void NumbersBooleansAndNullAreBare()
    {
        string json = """{"num":42,"real":3.14,"flagOn":true,"flagOff":false,"nothing":null}""";

        string expected =
            """
            {
              num: 42,
              real: 3.14,
              flagOn: true,
              flagOff: false,
              nothing: null,
            }
            """;

        string kyaml = YamlDocument.ConvertToYamlString(json, YamlWriterOptions.Kyaml);
        Assert.AreEqual(Lf(expected), kyaml);
    }

    [TestMethod]
    public void AmbiguousKeysAreQuoted_SafeKeysAreNot()
    {
        // Keys that would resolve to a non-string scalar (true, 123) or are a
        // YAML 1.1 type-ambiguous word (no) are quoted; ordinary identifier keys
        // are left bare.
        string json = """{"safeKey":"a","true":"b","123":"c","no":"d"}""";

        string expected =
            """
            {
              safeKey: "a",
              "true": "b",
              "123": "c",
              "no": "d",
            }
            """;

        string kyaml = YamlDocument.ConvertToYamlString(json, YamlWriterOptions.Kyaml);
        Assert.AreEqual(Lf(expected), kyaml);
    }

    // ===================================================================
    // Empty and root values
    // ===================================================================

    [TestMethod]
    [DataRow("{}", "{}")]
    [DataRow("[]", "[]")]
    [DataRow("\"hello\"", "\"hello\"")]
    [DataRow("42", "42")]
    [DataRow("true", "true")]
    [DataRow("null", "null")]
    public void RootScalarsAndEmptyContainers(string json, string expectedKyaml)
    {
        string kyaml = YamlDocument.ConvertToYamlString(json, YamlWriterOptions.Kyaml);
        Assert.AreEqual(expectedKyaml, kyaml);
    }

    // ===================================================================
    // Enabling KYAML via Format vs the preset are equivalent
    // ===================================================================

    [TestMethod]
    public void FormatPropertyAndPresetAreEquivalent()
    {
        string json = """{"a":"b","c":[1,2]}""";

        string viaPreset = YamlDocument.ConvertToYamlString(json, YamlWriterOptions.Kyaml);
        string viaFormat = YamlDocument.ConvertToYamlString(
            json,
            new YamlWriterOptions { Format = YamlWriterFormat.Kyaml });

        Assert.AreEqual(viaPreset, viaFormat);
    }

    [TestMethod]
    public void DefaultFormatIsStillBlockYaml()
    {
        // Guard against regressing the default (canonical block) output.
        string json = """{"name":"Alice","age":30}""";

        string expected =
            """
            name: Alice
            age: 30
            """;

        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.AreEqual(Lf(expected), yaml);
    }

    // ===================================================================
    // Round-trip — KYAML is valid YAML and parses back to the same data
    // ===================================================================

    [TestMethod]
    public void RoundTripsThroughTheParser()
    {
        string json =
            """{"country":"NO","port":80,"enabled":true,"nested":{"items":["a","b"]},"none":null}""";

        string kyaml = YamlDocument.ConvertToYamlString(json, YamlWriterOptions.Kyaml);

        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(kyaml));
        JsonElement root = doc.RootElement;

        Assert.AreEqual("NO", root.GetProperty("country"u8).GetString());
        Assert.AreEqual(80, root.GetProperty("port"u8).GetInt32());
        Assert.IsTrue(root.GetProperty("enabled"u8).GetBoolean());
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty("none"u8).ValueKind);

        JsonElement items = root.GetProperty("nested"u8).GetProperty("items"u8);
        Assert.AreEqual(2, items.GetArrayLength());
        Assert.AreEqual("a", items[0].GetString());
        Assert.AreEqual("b", items[1].GetString());
    }

    // ===================================================================
    // Low-level Utf8YamlWriter honours the KYAML format
    // ===================================================================

    [TestMethod]
    public void Utf8YamlWriter_KyamlOptions_EmitsKyaml()
    {
        ArrayBufferWriter<byte> buffer = new();
        Utf8YamlWriter writer = new(buffer, YamlWriterOptions.Kyaml);

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

        string kyaml = Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray());

        string expected =
            """
            {
              key: "value",
            }
            """;

        Assert.AreEqual(Lf(expected), kyaml);
    }

    // ===================================================================
    // The exact example published in docs/Yaml.md (guards against drift)
    // ===================================================================

    [TestMethod]
    public void KubernetesServiceExample_MatchesDocumentation()
    {
        string json =
            """{"apiVersion":"v1","kind":"Service","metadata":{"name":"hostnames"},"spec":{"ports":[{"port":80,"protocol":"TCP"}]}}""";

        string expected =
            """
            {
              apiVersion: "v1",
              kind: "Service",
              metadata: {
                name: "hostnames",
              },
              spec: {
                ports: [
                  {
                    port: 80,
                    protocol: "TCP",
                  },
                ],
              },
            }
            """;

        string kyaml = YamlDocument.ConvertToYamlString(json, YamlWriterOptions.Kyaml);
        Assert.AreEqual(Lf(expected), kyaml);
    }

    /// <summary>
    /// Normalizes CRLF to LF so expected values match the writer's LF output
    /// regardless of how this source file is checked out.
    /// </summary>
    private static string Lf(string value) => value.Replace("\r\n", "\n");
}