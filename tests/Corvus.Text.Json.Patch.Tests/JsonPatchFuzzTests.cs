// <copyright file="JsonPatchFuzzTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json.Nodes;
using Corvus.Text.Json.Patch;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Patch.Tests;

/// <summary>
/// Randomized RFC 6902 patch sequences over the mutable builder, verified after every
/// operation against an independent reference implementation and a full serialization
/// round-trip. Written while investigating
/// https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/954.
/// </summary>
[TestClass]
public class JsonPatchFuzzTests
{
    private static readonly string[] PropertyNamePool =
    [
        "storage", "storages", "generator", "generators", "type", "title", "t",
        "port", "connection", "id", "active", "a", "ab", "timeout_ms",
    ];

    [TestMethod]
    public void FuzzPatchOperationSequences()
    {
        int failures = 0;
        var sb = new StringBuilder();
        string? traceFile = Environment.GetEnvironmentVariable("PATCH_FUZZ_TRACE");
        using StreamWriter? trace = traceFile is null ? null : new StreamWriter(traceFile, append: true) { AutoFlush = true };
        int startIteration = int.TryParse(Environment.GetEnvironmentVariable("PATCH_FUZZ_START"), out int s) ? s : 0;

        for (int iteration = startIteration; iteration < 1500; iteration++)
        {
            var rnd = new Random(982_451_653 + iteration);
            string docJson = RandomValue(rnd, depth: 0, forceContainer: true).ToJsonString();

            JsonNode? reference = JsonNode.Parse(docJson);

            using JsonWorkspace workspace = JsonWorkspace.Create();
            using var builder = JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, docJson);
            JsonElement.Mutable root = builder.RootElement;

            var history = new List<string>();

            for (int opIndex = 0; opIndex < 8; opIndex++)
            {
                string? opJson = TryGenerateOperation(rnd, ref reference);
                if (opJson is null)
                {
                    continue;
                }

                history.Add(opJson);
                trace?.WriteLine($"i={iteration} op={opIndex} doc={docJson} ops={string.Join(",", history)}");

                JsonPatchDocument patch = JsonPatchDocument.ParseValue($"[{opJson}]");
                bool applied;
                try
                {
                    applied = JsonPatchExtensions.TryApplyPatch(ref root, in patch);
                }
                catch (Exception ex)
                {
                    failures++;
                    sb.AppendLine($"[apply-throw i={iteration}] doc={docJson} ops={string.Join(",", history)} -> {ex.GetType().Name}: {ex.Message}");
                    trace?.WriteLine($"FAIL [apply-throw i={iteration}] doc={docJson} ops={string.Join(",", history)} -> {ex.GetType().Name}: {ex.Message}");
                    break;
                }

                if (!applied)
                {
                    failures++;
                    sb.AppendLine($"[apply-false i={iteration}] doc={docJson} ops={string.Join(",", history)}");
                    trace?.WriteLine($"FAIL [apply-false i={iteration}] doc={docJson} ops={string.Join(",", history)}");
                    break;
                }

                string actual;
                try
                {
                    actual = builder.RootElement.ToString();
                }
                catch (Exception ex)
                {
                    failures++;
                    sb.AppendLine($"[read-throw i={iteration}] doc={docJson} ops={string.Join(",", history)} -> {ex.GetType().Name}: {ex.Message}");
                    trace?.WriteLine($"FAIL [read-throw i={iteration}] doc={docJson} ops={string.Join(",", history)} -> {ex.GetType().Name}: {ex.Message}");
                    break;
                }

                string expected = reference?.ToJsonString() ?? "null";
                string actualCanonical;
                try
                {
                    actualCanonical = JsonNode.Parse(actual)?.ToJsonString() ?? "null";
                }
                catch (Exception ex)
                {
                    failures++;
                    sb.AppendLine($"[reparse-throw i={iteration}] doc={docJson} ops={string.Join(",", history)} actual={actual} -> {ex.GetType().Name}: {ex.Message}");
                    trace?.WriteLine($"FAIL [reparse-throw i={iteration}] doc={docJson} ops={string.Join(",", history)} actual={actual} -> {ex.GetType().Name}: {ex.Message}");
                    break;
                }

                if (actualCanonical != expected)
                {
                    failures++;
                    sb.AppendLine($"[diverged i={iteration}] doc={docJson} ops={string.Join(",", history)} expected={expected} actual={actualCanonical}");
                    trace?.WriteLine($"FAIL [diverged i={iteration}] doc={docJson} ops={string.Join(",", history)} expected={expected} actual={actualCanonical}");
                    break;
                }
            }

            if (failures >= 20)
            {
                break;
            }
        }

        Assert.AreEqual(0, failures, sb.ToString());
    }

    /// <summary>
    /// Generates one random operation that is valid against the reference document, applies
    /// it to the reference, and returns its JSON text; returns null if no valid operation
    /// could be produced for the current document shape.
    /// </summary>
    private static string? TryGenerateOperation(Random rnd, ref JsonNode? reference)
    {
        if (reference is null)
        {
            return null;
        }

        List<string> paths = CollectPointers(reference);

        for (int attempt = 0; attempt < 10; attempt++)
        {
            int kind = rnd.Next(10);
            string? op = kind switch
            {
                // Heavily weighted toward moves: the operation under suspicion.
                <= 4 => TryBuildMove(rnd, reference, paths),
                5 => TryBuildCopy(rnd, reference, paths),
                6 => TryBuildRemove(rnd, paths),
                7 or 8 => TryBuildAdd(rnd, reference, paths),
                _ => TryBuildReplace(rnd, paths),
            };

            if (op is null)
            {
                continue;
            }

            JsonNode? updated = JsonNode.Parse(reference.ToJsonString());
            if (TryApplyReferenceOperation(ref updated, op))
            {
                reference = updated;
                return op;
            }
        }

        return null;
    }

    private static string? TryBuildMove(Random rnd, JsonNode reference, List<string> paths)
    {
        string? from = PickNonRootPointer(rnd, paths);
        if (from is null)
        {
            return null;
        }

        string? destination = PickDestination(rnd, reference, paths, excludeSubtreeOf: from);
        if (destination is null)
        {
            return null;
        }

        return $$"""{"op":"move","from":"{{from}}","path":"{{destination}}"}""";
    }

    private static string? TryBuildCopy(Random rnd, JsonNode reference, List<string> paths)
    {
        string? from = PickNonRootPointer(rnd, paths);
        if (from is null)
        {
            return null;
        }

        string? destination = PickDestination(rnd, reference, paths, excludeSubtreeOf: null);
        if (destination is null)
        {
            return null;
        }

        return $$"""{"op":"copy","from":"{{from}}","path":"{{destination}}"}""";
    }

    private static string? TryBuildRemove(Random rnd, List<string> paths)
    {
        string? path = PickNonRootPointer(rnd, paths);
        return path is null ? null : $$"""{"op":"remove","path":"{{path}}"}""";
    }

    private static string? TryBuildAdd(Random rnd, JsonNode reference, List<string> paths)
    {
        string? destination = PickDestination(rnd, reference, paths, excludeSubtreeOf: null);
        if (destination is null)
        {
            return null;
        }

        JsonNode value = RandomValue(rnd, depth: 2, forceContainer: false);
        return $$"""{"op":"add","path":"{{destination}}","value":{{value.ToJsonString()}}}""";
    }

    private static string? TryBuildReplace(Random rnd, List<string> paths)
    {
        string? path = PickNonRootPointer(rnd, paths);
        if (path is null)
        {
            return null;
        }

        JsonNode value = RandomValue(rnd, depth: 2, forceContainer: false);
        return $$"""{"op":"replace","path":"{{path}}","value":{{value.ToJsonString()}}}""";
    }

    private static string? PickNonRootPointer(Random rnd, List<string> paths)
    {
        List<string> nonRoot = paths.FindAll(p => p.Length > 0);
        return nonRoot.Count == 0 ? null : nonRoot[rnd.Next(nonRoot.Count)];
    }

    /// <summary>
    /// Picks a destination pointer whose parent container exists: either an existing member
    /// (object replace), a fresh property name on an existing object, or an index/append
    /// position in an existing array. Never inside the excluded subtree (a move source).
    /// </summary>
    private static string? PickDestination(Random rnd, JsonNode reference, List<string> paths, string? excludeSubtreeOf)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            string containerPath = paths[rnd.Next(paths.Count)];
            if (excludeSubtreeOf is not null &&
                (containerPath.Length == 0 || containerPath.StartsWith(excludeSubtreeOf, StringComparison.Ordinal)))
            {
                if (containerPath.Length != 0)
                {
                    continue;
                }
            }

            JsonNode? container = ResolvePointer(reference, containerPath);
            switch (container)
            {
                case JsonObject:
                    string name = PropertyNamePool[rnd.Next(PropertyNamePool.Length)];
                    string candidate = $"{containerPath}/{name}";
                    if (excludeSubtreeOf is not null && candidate.StartsWith(excludeSubtreeOf, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    return candidate;
                case JsonArray array:
                    string segment = rnd.Next(4) == 0 ? "-" : rnd.Next(array.Count + 1).ToString();
                    string arrayCandidate = $"{containerPath}/{segment}";
                    if (excludeSubtreeOf is not null && arrayCandidate.StartsWith(excludeSubtreeOf, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    return arrayCandidate;
            }
        }

        return null;
    }

    private static List<string> CollectPointers(JsonNode? node)
    {
        var result = new List<string>();
        Collect(node, string.Empty, result);
        return result;

        static void Collect(JsonNode? node, string path, List<string> result)
        {
            result.Add(path);
            switch (node)
            {
                case JsonObject obj:
                    foreach (KeyValuePair<string, JsonNode?> property in obj)
                    {
                        Collect(property.Value, $"{path}/{Escape(property.Key)}", result);
                    }

                    break;
                case JsonArray array:
                    for (int i = 0; i < array.Count; i++)
                    {
                        Collect(array[i], $"{path}/{i}", result);
                    }

                    break;
            }
        }
    }

    private static string Escape(string segment)
    {
        return segment.Replace("~", "~0").Replace("/", "~1");
    }

    // =====================================================================
    // Reference RFC 6902 implementation over System.Text.Json.Nodes.
    // =====================================================================
    internal static bool TryApplyReferenceOperation(ref JsonNode? doc, string opJson)
    {
        JsonNode op = JsonNode.Parse(opJson)!;
        string kind = op["op"]!.GetValue<string>();
        string path = op["path"]!.GetValue<string>();

        switch (kind)
        {
            case "add":
                return TryAdd(ref doc, path, JsonNode.Parse(op["value"]!.ToJsonString()));
            case "remove":
                return TryRemove(ref doc, path, out _);
            case "replace":
                return TryReplace(ref doc, path, JsonNode.Parse(op["value"]!.ToJsonString()));
            case "move":
            {
                string from = op["from"]!.GetValue<string>();
                if (!TryRemove(ref doc, from, out JsonNode? removed))
                {
                    return false;
                }

                return TryAdd(ref doc, path, removed);
            }

            case "copy":
            {
                string from = op["from"]!.GetValue<string>();
                JsonNode? source = ResolvePointer(doc, from);
                if (source is null && !PointerExists(doc, from))
                {
                    return false;
                }

                return TryAdd(ref doc, path, source is null ? null : JsonNode.Parse(source.ToJsonString()));
            }

            default:
                return false;
        }
    }

    private static bool TryReplace(ref JsonNode? doc, string pointer, JsonNode? value)
    {
        if (pointer.Length == 0)
        {
            doc = value;
            return true;
        }

        (string parentPath, string lastSegment) = SplitPointer(pointer);
        JsonNode? parent = ResolvePointer(doc, parentPath);
        switch (parent)
        {
            case JsonObject obj:
                string name = Unescape(lastSegment);
                if (!obj.ContainsKey(name))
                {
                    return false;
                }

                // In-place assignment preserves the property's position, matching the builder.
                obj[name] = value;
                return true;
            case JsonArray array:
                if (!int.TryParse(lastSegment, out int index) || index < 0 || index >= array.Count)
                {
                    return false;
                }

                array[index] = value;
                return true;
            default:
                return false;
        }
    }

    private static bool TryAdd(ref JsonNode? doc, string pointer, JsonNode? value)
    {
        if (pointer.Length == 0)
        {
            doc = value;
            return true;
        }

        (string parentPath, string lastSegment) = SplitPointer(pointer);
        JsonNode? parent = ResolvePointer(doc, parentPath);
        switch (parent)
        {
            case JsonObject obj:
                obj[Unescape(lastSegment)] = value;
                return true;
            case JsonArray array:
                if (lastSegment == "-")
                {
                    array.Add(value);
                    return true;
                }

                if (!int.TryParse(lastSegment, out int index) || index < 0 || index > array.Count)
                {
                    return false;
                }

                array.Insert(index, value);
                return true;
            default:
                return false;
        }
    }

    private static bool TryRemove(ref JsonNode? doc, string pointer, out JsonNode? removed)
    {
        removed = null;
        if (pointer.Length == 0)
        {
            return false;
        }

        (string parentPath, string lastSegment) = SplitPointer(pointer);
        JsonNode? parent = ResolvePointer(doc, parentPath);
        switch (parent)
        {
            case JsonObject obj:
                string name = Unescape(lastSegment);
                if (!obj.ContainsKey(name))
                {
                    return false;
                }

                removed = obj[name];
                obj.Remove(name);
                removed = removed is null ? null : JsonNode.Parse(removed.ToJsonString());
                return true;
            case JsonArray array:
                if (!int.TryParse(lastSegment, out int index) || index < 0 || index >= array.Count)
                {
                    return false;
                }

                removed = array[index];
                array.RemoveAt(index);
                removed = removed is null ? null : JsonNode.Parse(removed.ToJsonString());
                return true;
            default:
                return false;
        }
    }

    private static bool PointerExists(JsonNode? doc, string pointer)
    {
        if (pointer.Length == 0)
        {
            return true;
        }

        (string parentPath, string lastSegment) = SplitPointer(pointer);
        JsonNode? parent = ResolvePointer(doc, parentPath);
        return parent switch
        {
            JsonObject obj => obj.ContainsKey(Unescape(lastSegment)),
            JsonArray array => int.TryParse(lastSegment, out int index) && index >= 0 && index < array.Count,
            _ => false,
        };
    }

    private static (string ParentPath, string LastSegment) SplitPointer(string pointer)
    {
        int lastSlash = pointer.LastIndexOf('/');
        return (pointer.Substring(0, lastSlash), pointer.Substring(lastSlash + 1));
    }

    private static JsonNode? ResolvePointer(JsonNode? node, string pointer)
    {
        if (pointer.Length == 0)
        {
            return node;
        }

        JsonNode? current = node;
        string[] segments = pointer.Split('/');
        for (int i = 1; i < segments.Length; i++)
        {
            string unescaped = Unescape(segments[i]);
            switch (current)
            {
                case JsonObject obj:
                    if (!obj.ContainsKey(unescaped))
                    {
                        return null;
                    }

                    current = obj[unescaped];
                    break;
                case JsonArray array:
                    if (!int.TryParse(unescaped, out int index) || index < 0 || index >= array.Count)
                    {
                        return null;
                    }

                    current = array[index];
                    break;
                default:
                    return null;
            }
        }

        return current;
    }

    private static string Unescape(string segment)
    {
        return segment.Replace("~1", "/").Replace("~0", "~");
    }

    // =====================================================================
    // Random document generation.
    // =====================================================================
    private static JsonNode RandomValue(Random rnd, int depth, bool forceContainer)
    {
        int kind = forceContainer ? rnd.Next(2) : (depth >= 3 ? rnd.Next(2, 7) : rnd.Next(7));
        return kind switch
        {
            0 => RandomObject(rnd, depth),
            1 => RandomArray(rnd, depth),
            2 => JsonValue.Create(rnd.Next(0, 100000)),
            3 => JsonValue.Create(rnd.NextDouble() < 0.5 ? 97.7 : -0.4),
            4 => JsonValue.Create(true),
            5 => JsonValue.Create("t" + rnd.Next(100)),
            _ => JsonValue.Create("x"),
        };
    }

    private static JsonObject RandomObject(Random rnd, int depth)
    {
        var result = new JsonObject();
        int count = rnd.Next(1, 6);
        for (int i = 0; i < count; i++)
        {
            string name = PropertyNamePool[rnd.Next(PropertyNamePool.Length)];
            result[name] = RandomValue(rnd, depth + 1, forceContainer: false);
        }

        return result;
    }

    private static JsonArray RandomArray(Random rnd, int depth)
    {
        var result = new JsonArray();
        int count = rnd.Next(1, 5);
        for (int i = 0; i < count; i++)
        {
            result.Add(RandomValue(rnd, depth + 1, forceContainer: false));
        }

        return result;
    }
}
