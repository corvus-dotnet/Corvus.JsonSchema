// <copyright file="YamlEventSpanTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Yaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Yaml.Tests;

/// <summary>
/// Tests for the <see cref="YamlEvent.Line"/>, <see cref="YamlEvent.Column"/>,
/// <see cref="YamlEvent.EndLine"/>, and <see cref="YamlEvent.EndColumn"/> span fields.
/// Each test collects every event's span and extracted text and asserts on the full list.
/// </summary>
[TestClass]
public class YamlEventSpanTests
{
    private static string Extract(string yaml, int startLine, int startCol, int endLine, int endCol)
    {
        int[] lineStarts = BuildLineStarts();
        int start = lineStarts[startLine - 1] + startCol - 1;
        int end = lineStarts[endLine - 1] + endCol - 1;
        return yaml.Substring(start, end - start);

        int[] BuildLineStarts()
        {
            var starts = new List<int> { 0 };
            for (int i = 0; i < yaml.Length; i++)
                if (yaml[i] == '\n')
                    starts.Add(i + 1);
            return [.. starts];
        }
    }

    private static List<(YamlEventType Type, int Line, int Column, int EndLine, int EndColumn, string Text)> AllEventSpans(string yaml)
    {
        var result = new List<(YamlEventType, int, int, int, int, string)>();
        YamlDocument.EnumerateEvents(yaml, (in YamlEvent evt) =>
        {
            result.Add((evt.Type, evt.Line, evt.Column, evt.EndLine, evt.EndColumn, Extract(yaml, evt.Line, evt.Column, evt.EndLine, evt.EndColumn)));
            return true;
        });
        return result;
    }

    [TestMethod]
    public void PlainScalars_AllEventSpans()
    {
        const string yaml = "key: value\n";
        CollectionAssert.AreEqual(new[]
        {
            (YamlEventType.StreamStart,    1, 1, 1, 1, ""),
            (YamlEventType.DocumentStart,  1, 1, 1, 1, ""),
            (YamlEventType.MappingStart,   1, 1, 1, 1, ""),
            (YamlEventType.Scalar,         1, 1, 1, 4, "key"),
            (YamlEventType.Scalar,         1, 6, 1, 11, "value"),
            (YamlEventType.MappingEnd,     2, 1, 2, 1, ""),
            (YamlEventType.DocumentEnd,    2, 1, 2, 1, ""),
            (YamlEventType.StreamEnd,      2, 1, 2, 1, ""),
        }, AllEventSpans(yaml));
    }

    [TestMethod]
    public void DoubleQuotedScalar_AllEventSpans()
    {
        const string yaml = "k: \"hello\"\n";
        CollectionAssert.AreEqual(new[]
        {
            (YamlEventType.StreamStart,    1, 1, 1,  1, ""),
            (YamlEventType.DocumentStart,  1, 1, 1,  1, ""),
            (YamlEventType.MappingStart,   1, 1, 1,  1, ""),
            (YamlEventType.Scalar,         1, 1, 1,  2, "k"),
            (YamlEventType.Scalar,         1, 4, 1, 11, "\"hello\""),
            (YamlEventType.MappingEnd,     2, 1, 2,  1, ""),
            (YamlEventType.DocumentEnd,    2, 1, 2,  1, ""),
            (YamlEventType.StreamEnd,      2, 1, 2,  1, ""),
        }, AllEventSpans(yaml));
    }

    [TestMethod]
    public void LiteralBlockScalar_AllEventSpans()
    {
        const string yaml = "x: |-\n  line1\n  line2\n\n\ny: z\n";
        CollectionAssert.AreEqual(new[]
        {
            (YamlEventType.StreamStart,    1, 1, 1, 1, ""),
            (YamlEventType.DocumentStart,  1, 1, 1, 1, ""),
            (YamlEventType.MappingStart,   1, 1, 1, 1, ""),
            (YamlEventType.Scalar,         1, 1, 1, 2, "x"),
            (YamlEventType.Scalar,         1, 4, 3, 8, "|-\n  line1\n  line2"),
            (YamlEventType.Scalar,         6, 1, 6, 2, "y"),
            (YamlEventType.Scalar,         6, 4, 6, 5, "z"),
            (YamlEventType.MappingEnd,     7, 1, 7, 1, ""),
            (YamlEventType.DocumentEnd,    7, 1, 7, 1, ""),
            (YamlEventType.StreamEnd,      7, 1, 7, 1, ""),
        }, AllEventSpans(yaml));
    }

    [TestMethod]
    public void EmptyNode_AllEventSpans()
    {
        const string yaml = "a: 1\nb:\n";
        CollectionAssert.AreEqual(new[]
        {
            (YamlEventType.StreamStart,    1, 1, 1, 1, ""),
            (YamlEventType.DocumentStart,  1, 1, 1, 1, ""),
            (YamlEventType.MappingStart,   1, 1, 1, 1, ""),
            (YamlEventType.Scalar,         1, 1, 1, 2, "a"),
            (YamlEventType.Scalar,         1, 4, 1, 5, "1"),
            (YamlEventType.Scalar,         2, 1, 2, 2, "b"),
            (YamlEventType.Scalar,         3, 1, 3, 1, ""),
            (YamlEventType.MappingEnd,     3, 1, 3, 1, ""),
            (YamlEventType.DocumentEnd,    3, 1, 3, 1, ""),
            (YamlEventType.StreamEnd,      3, 1, 3, 1, ""),
        }, AllEventSpans(yaml));
    }

    [TestMethod]
    public void FlowSequence_AllEventSpans()
    {
        const string yaml = "name: foo\ntags: [x, y, z]\n";
        CollectionAssert.AreEqual(new[]
        {
            (YamlEventType.StreamStart,    1,  1, 1,  1, ""),
            (YamlEventType.DocumentStart,  1,  1, 1,  1, ""),
            (YamlEventType.MappingStart,   1,  1, 1,  1, ""),
            (YamlEventType.Scalar,         1,  1, 1,  5, "name"),
            (YamlEventType.Scalar,         1,  7, 1, 10, "foo"),
            (YamlEventType.Scalar,         2,  1, 2,  5, "tags"),
            (YamlEventType.SequenceStart,  2,  7, 2,  7, ""),
            (YamlEventType.Scalar,         2,  8, 2,  9, "x"),
            (YamlEventType.Scalar,         2, 11, 2, 12, "y"),
            (YamlEventType.Scalar,         2, 14, 2, 15, "z"),
            (YamlEventType.SequenceEnd,    2, 16, 2, 16, ""),
            (YamlEventType.MappingEnd,     3,  1, 3,  1, ""),
            (YamlEventType.DocumentEnd,    3,  1, 3,  1, ""),
            (YamlEventType.StreamEnd,      3,  1, 3,  1, ""),
        }, AllEventSpans(yaml));
    }

    [TestMethod]
    public void FlowMapping_AllEventSpans()
    {
        const string yaml = "config: {a: 1, b: 2}\nother: z\n";
        CollectionAssert.AreEqual(new[]
        {
            (YamlEventType.StreamStart,    1,  1, 1,  1, ""),
            (YamlEventType.DocumentStart,  1,  1, 1,  1, ""),
            (YamlEventType.MappingStart,   1,  1, 1,  1, ""),
            (YamlEventType.Scalar,         1,  1, 1,  7, "config"),
            (YamlEventType.MappingStart,   1,  9, 1,  9, ""),
            (YamlEventType.Scalar,         1, 10, 1, 11, "a"),
            (YamlEventType.Scalar,         1, 13, 1, 14, "1"),
            (YamlEventType.Scalar,         1, 16, 1, 17, "b"),
            (YamlEventType.Scalar,         1, 19, 1, 20, "2"),
            (YamlEventType.MappingEnd,     1, 21, 1, 21, ""),
            (YamlEventType.Scalar,         2,  1, 2,  6, "other"),
            (YamlEventType.Scalar,         2,  8, 2,  9, "z"),
            (YamlEventType.MappingEnd,     3,  1, 3,  1, ""),
            (YamlEventType.DocumentEnd,    3,  1, 3,  1, ""),
            (YamlEventType.StreamEnd,      3,  1, 3,  1, ""),
        }, AllEventSpans(yaml));
    }

    [TestMethod]
    public void NestedBlockMapping_AllEventSpans()
    {
        const string yaml = "outer: x\nmap:\n  a: 1\n  b: 2\n";
        CollectionAssert.AreEqual(new[]
        {
            (YamlEventType.StreamStart,    1, 1, 1, 1, ""),
            (YamlEventType.DocumentStart,  1, 1, 1, 1, ""),
            (YamlEventType.MappingStart,   1, 1, 1, 1, ""),
            (YamlEventType.Scalar,         1, 1, 1, 6, "outer"),
            (YamlEventType.Scalar,         1, 8, 1, 9, "x"),
            (YamlEventType.Scalar,         2, 1, 2, 4, "map"),
            (YamlEventType.MappingStart,   3, 3, 3, 3, ""),
            (YamlEventType.Scalar,         3, 3, 3, 4, "a"),
            (YamlEventType.Scalar,         3, 6, 3, 7, "1"),
            (YamlEventType.Scalar,         4, 3, 4, 4, "b"),
            (YamlEventType.Scalar,         4, 6, 4, 7, "2"),
            (YamlEventType.MappingEnd,     5, 1, 5, 1, ""),
            (YamlEventType.MappingEnd,     5, 1, 5, 1, ""),
            (YamlEventType.DocumentEnd,    5, 1, 5, 1, ""),
            (YamlEventType.StreamEnd,      5, 1, 5, 1, ""),
        }, AllEventSpans(yaml));
    }
}
