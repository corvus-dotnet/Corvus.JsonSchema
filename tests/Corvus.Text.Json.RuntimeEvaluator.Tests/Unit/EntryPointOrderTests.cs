// <copyright file="EntryPointOrderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.RuntimeEvaluator;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// The order in which entry points are registered decides the numbering of the nodes and resources they add, and
/// nothing else: a generator that emits its entry points in another order produces the same program.
/// </summary>
[TestClass]
public class EntryPointOrderTests
{
    private const string Root = "https://t.example/root";

    private static readonly string[] EntryPoints =
    [
        "https://t.example/a#",
        "https://t.example/b#/$defs/x",
        "https://t.example/b#/$defs/y",
        "https://t.example/c#",
        "https://t.example/d#",
    ];

    [TestMethod]
    public void EntryPointsRegisteredInAnotherOrderGiveTheSameProgramUpToNumbering()
    {
        Dictionary<string, byte[]> documents = Documents(minimum: 0);
        byte[] forward = Compile(documents, EntryPoints);
        byte[] reversed = Compile(documents, [.. Enumerable.Reverse(EntryPoints)]);

        CollectionAssert.AreNotEqual(forward, reversed, "the nodes an entry point adds are numbered when it is registered, so the images differ");
        ProgramIsomorphism.AssertIsomorphic(forward, reversed, Options(documents));
    }

    [TestMethod]
    public void ProgramsThatDifferInAKeywordAreNotTheSameUpToNumbering()
    {
        Dictionary<string, byte[]> documents = Documents(minimum: 0);
        Dictionary<string, byte[]> changed = Documents(minimum: 1);
        byte[] first = Compile(documents, EntryPoints);
        byte[] second = Compile(changed, EntryPoints);

        Assert.ThrowsExactly<AssertFailedException>(() => ProgramIsomorphism.AssertIsomorphic(first, second, Options(documents)));
    }

    [TestMethod]
    public void ProgramsWithDifferentEntryPointsAreNotTheSameUpToNumbering()
    {
        Dictionary<string, byte[]> documents = Documents(minimum: 0);
        byte[] all = Compile(documents, EntryPoints);
        byte[] fewer = Compile(documents, [.. EntryPoints.Take(4)]);

        Assert.ThrowsExactly<AssertFailedException>(() => ProgramIsomorphism.AssertIsomorphic(all, fewer, Options(documents)));
    }

    private static Dictionary<string, byte[]> Documents(int minimum)
    {
        return new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [Root] = Encoding.UTF8.GetBytes("""
                {
                  "$schema": "https://json-schema.org/draft/2020-12/schema",
                  "$id": "https://t.example/root",
                  "type": "object",
                  "properties": {"name": {"type": "string", "minLength": 1}},
                  "required": ["name"]
                }
                """),
            ["https://t.example/a"] = Encoding.UTF8.GetBytes($$"""
                {
                  "$schema": "https://json-schema.org/draft/2020-12/schema",
                  "$id": "https://t.example/a",
                  "type": "integer",
                  "minimum": {{minimum}},
                  "description": "a count"
                }
                """),
            ["https://t.example/b"] = Encoding.UTF8.GetBytes("""
                {
                  "$schema": "https://json-schema.org/draft/2020-12/schema",
                  "$id": "https://t.example/b",
                  "$defs": {
                    "x": {"type": "string", "pattern": "^[a-z]+$", "enum": ["ab", "cd"]},
                    "y": {"type": "array", "items": {"$ref": "#/$defs/x"}, "minItems": 1},
                    "z": {"oneOf": [{"$ref": "#/$defs/x"}, {"type": "object", "properties": {"kind": {"const": "z"}}, "required": ["kind"]}]}
                  }
                }
                """),
            ["https://t.example/c"] = Encoding.UTF8.GetBytes("""
                {
                  "$schema": "https://json-schema.org/draft/2020-12/schema",
                  "$id": "https://t.example/c",
                  "$dynamicAnchor": "node",
                  "type": "object",
                  "properties": {"data": true, "children": {"type": "array", "items": {"$dynamicRef": "#node"}}}
                }
                """),
            ["https://t.example/d"] = Encoding.UTF8.GetBytes("""
                {
                  "$schema": "https://json-schema.org/draft/2020-12/schema",
                  "$id": "https://t.example/d",
                  "$dynamicAnchor": "node",
                  "$ref": "c",
                  "unevaluatedProperties": false
                }
                """),
        };
    }

    private static JsonSchemaEvaluatorOptions Options(Dictionary<string, byte[]> documents)
    {
        return new JsonSchemaEvaluatorOptions
        {
            DefaultDialect = JsonSchemaDialect.Draft202012,
            AssertFormat = true,
            DocumentResolver = (string uri, out ReadOnlyMemory<byte> utf8Json) =>
            {
                if (documents.TryGetValue(uri, out byte[]? bytes))
                {
                    utf8Json = bytes;
                    return true;
                }

                utf8Json = default;
                return false;
            },
        };
    }

    private static byte[] Compile(Dictionary<string, byte[]> documents, IReadOnlyList<string> entryPoints)
    {
        using JsonSchemaEvaluator root = JsonSchemaEvaluator.CompileFromUri(Root, Options(documents));
        root.RegisterEntryPoints(entryPoints);
        return root.ToProgramImage();
    }
}