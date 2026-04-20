// <copyright file="YamlEventConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Yaml;
using Xunit;
using Xunit.Abstractions;

namespace Corvus.Text.Json.Yaml.Tests;

/// <summary>
/// Conformance tests for the YAML event parser, driven by the yaml-test-suite
/// <c>test.event</c> files. Each test case directory contains an <c>in.yaml</c>
/// and a <c>test.event</c> file describing the expected parse events.
/// </summary>
public class YamlEventConformanceTests
{
    private static readonly string TestSuitePath = FindTestSuitePath();

    private readonly ITestOutputHelper _output;

    public YamlEventConformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Gets the valid test case IDs (directories with test.event and NO error file).
    /// </summary>
    public static IEnumerable<object[]> ValidEventTestCases()
    {
        string suitePath = FindTestSuitePath();
        if (!Directory.Exists(suitePath))
        {
            yield break;
        }

        foreach (string dir in Directory.GetDirectories(suitePath).OrderBy(d => d))
        {
            string eventFile = Path.Combine(dir, "test.event");
            string yamlFile = Path.Combine(dir, "in.yaml");
            string errorFile = Path.Combine(dir, "error");

            if (File.Exists(eventFile) && File.Exists(yamlFile) && !File.Exists(errorFile))
            {
                yield return [Path.GetFileName(dir)];
            }
        }
    }

    /// <summary>
    /// Gets the error test case IDs (directories with error file).
    /// </summary>
    public static IEnumerable<object[]> ErrorEventTestCases()
    {
        string suitePath = FindTestSuitePath();
        if (!Directory.Exists(suitePath))
        {
            yield break;
        }

        foreach (string dir in Directory.GetDirectories(suitePath).OrderBy(d => d))
        {
            string yamlFile = Path.Combine(dir, "in.yaml");
            string errorFile = Path.Combine(dir, "error");

            if (File.Exists(yamlFile) && File.Exists(errorFile))
            {
                yield return [Path.GetFileName(dir)];
            }
        }
    }

    [Theory]
    [MemberData(nameof(ValidEventTestCases))]
    public void ValidEventTest(string testId)
    {
        string dir = Path.Combine(TestSuitePath, testId);
        byte[] yaml = File.ReadAllBytes(Path.Combine(dir, "in.yaml"));
        string expectedEvents = File.ReadAllText(Path.Combine(dir, "test.event")).TrimEnd();

        List<string> eventLines = [];

        YamlDocument.EnumerateEvents(
            yaml,
            (in YamlEvent evt) =>
            {
                eventLines.Add(FormatEvent(in evt));
                return true;
            });

        string actualEvents = string.Join("\n", eventLines);

        _output.WriteLine($"Test: {testId}");
        _output.WriteLine($"Expected:\n{expectedEvents}");
        _output.WriteLine($"Actual:\n{actualEvents}");

        Assert.Equal(
            NormalizeEventString(expectedEvents),
            NormalizeEventString(actualEvents));
    }

    [Theory]
    [MemberData(nameof(ErrorEventTestCases))]
    public void ErrorEventTest(string testId)
    {
        string dir = Path.Combine(TestSuitePath, testId);
        byte[] yaml = File.ReadAllBytes(Path.Combine(dir, "in.yaml"));

        _output.WriteLine($"Test: {testId}");

        // Error test cases should cause the parser to throw YamlException.
        // Some may parse partially before the error, but the parser should
        // eventually throw.
        Assert.Throws<YamlException>(() =>
        {
            YamlDocument.EnumerateEvents(
                yaml,
                static (in YamlEvent _) => true);
        });
    }

    /// <summary>
    /// Formats a <see cref="YamlEvent"/> into the yaml-test-suite event format.
    /// </summary>
    private static string FormatEvent(in YamlEvent evt)
    {
        return evt.Type switch
        {
            YamlEventType.StreamStart => "+STR",
            YamlEventType.StreamEnd => "-STR",
            YamlEventType.DocumentStart => evt.IsImplicit ? "+DOC" : "+DOC ---",
            YamlEventType.DocumentEnd => evt.IsImplicit ? "-DOC" : "-DOC ...",
            YamlEventType.MappingStart => FormatCollectionStart("+MAP", evt.IsFlowStyle ? "{}" : null, evt.Anchor, evt.Tag),
            YamlEventType.MappingEnd => "-MAP",
            YamlEventType.SequenceStart => FormatCollectionStart("+SEQ", evt.IsFlowStyle ? "[]" : null, evt.Anchor, evt.Tag),
            YamlEventType.SequenceEnd => "-SEQ",
            YamlEventType.Scalar => FormatScalar(in evt),
            YamlEventType.Alias => $"=ALI *{Encoding.UTF8.GetString(evt.Value)}",
            _ => $"??? {evt.Type}",
        };
    }

    private static string FormatCollectionStart(string prefix, string? flowIndicator, ReadOnlySpan<byte> anchor, ReadOnlySpan<byte> tag)
    {
        StringBuilder sb = new();
        sb.Append(prefix);

        if (flowIndicator is not null)
        {
            sb.Append(' ');
            sb.Append(flowIndicator);
        }

        if (!anchor.IsEmpty)
        {
            sb.Append(" &");
            sb.Append(Encoding.UTF8.GetString(anchor));
        }

        if (!tag.IsEmpty)
        {
            sb.Append(" <");
            sb.Append(Encoding.UTF8.GetString(tag));
            sb.Append('>');
        }

        return sb.ToString();
    }

    private static string FormatScalar(in YamlEvent evt)
    {
        StringBuilder sb = new();
        sb.Append("=VAL");

        if (!evt.Anchor.IsEmpty)
        {
            sb.Append(" &");
            sb.Append(Encoding.UTF8.GetString(evt.Anchor));
        }

        if (!evt.Tag.IsEmpty)
        {
            sb.Append(" <");
            sb.Append(Encoding.UTF8.GetString(evt.Tag));
            sb.Append('>');
        }

        char styleChar = evt.ScalarStyle switch
        {
            YamlScalarStyle.Plain => ':',
            YamlScalarStyle.SingleQuoted => '\'',
            YamlScalarStyle.DoubleQuoted => '"',
            YamlScalarStyle.Literal => '|',
            YamlScalarStyle.Folded => '>',
            _ => ':',
        };

        sb.Append(' ');
        sb.Append(styleChar);
        sb.Append(EscapeEventValue(evt.Value));

        return sb.ToString();
    }

    /// <summary>
    /// Escapes a scalar value for the yaml-test-suite event format.
    /// Newlines → \n, backslash → \\, tab → \t, etc.
    /// </summary>
    private static string EscapeEventValue(ReadOnlySpan<byte> utf8Value)
    {
        string s = Encoding.UTF8.GetString(utf8Value);
        StringBuilder sb = new(s.Length);

        foreach (char c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\a': sb.Append("\\a"); break;
                case '\0': sb.Append("\\0"); break;
                default: sb.Append(c); break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Normalizes event strings for comparison by trimming whitespace
    /// and normalizing line endings.
    /// </summary>
    private static string NormalizeEventString(string events)
    {
        return events
            .Replace("\r\n", "\n")
            .Trim();
    }

    private static string FindTestSuitePath()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            string candidate = Path.Combine(dir, "yaml-test-suite");
            if (Directory.Exists(candidate) && Directory.GetDirectories(candidate).Length > 100)
            {
                return candidate;
            }

            dir = Path.GetDirectoryName(dir);
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "yaml-test-suite"));
    }
}