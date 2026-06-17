// <copyright file="Issue820Fuzz.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json.Nodes;
using Corvus.Text.Json;
using Corvus.Text.Json.Patch;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Patch.Tests;

[TestClass]
public class JsonMergePatchFuzzTests
{
    [TestMethod]
    public void FuzzFrozenMutablePatchReads()
    {
        var rnd = new Random(12345);
        int failures = 0;
        var sb = new StringBuilder();

        for (int i = 0; i < 2000; i++)
        {
            string targetJson = RandomObject(rnd, 0);
            string patchJson = RandomObject(rnd, 0);

            using JsonWorkspace workspace = JsonWorkspace.Create();
            using var targetBuilder = JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, targetJson);
            JsonElement.Mutable target = targetBuilder.RootElement;

            using var patchBuilder = JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, patchJson);
            JsonElement frozenPatch = patchBuilder.RootElement.Freeze();

            string before = frozenPatch.ToString();

            try
            {
                JsonMergePatchExtensions.ApplyMergePatch(ref target, in frozenPatch);
            }
            catch (Exception ex)
            {
                failures++;
                sb.AppendLine($"[apply] target={targetJson} patch={patchJson} -> {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            // The frozen patch must remain fully readable and unchanged after the merge.
            try
            {
                string after = frozenPatch.ToString();
                if (after != before)
                {
                    failures++;
                    sb.AppendLine($"[mutated] target={targetJson} patch={patchJson} before={before} after={after}");
                }
            }
            catch (Exception ex)
            {
                failures++;
                sb.AppendLine($"[read] target={targetJson} patch={patchJson} -> {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            // The merge result must match an independent RFC 7396 reference implementation.
            try
            {
                string actual = Canonicalize(target.ToString());
                string expected = Canonicalize(ReferenceMerge(targetJson, patchJson));
                if (actual != expected)
                {
                    failures++;
                    sb.AppendLine($"[result] target={targetJson} patch={patchJson} expected={expected} actual={actual}");
                }
            }
            catch (Exception ex)
            {
                failures++;
                sb.AppendLine($"[result-read] target={targetJson} patch={patchJson} -> {ex.GetType().Name}: {ex.Message}");
            }
        }

        Assert.AreEqual(0, failures, sb.ToString());
    }

    // Independent RFC 7396 merge over System.Text.Json.Nodes, returning the merged JSON text.
    private static string ReferenceMerge(string targetJson, string patchJson)
    {
        JsonNode? merged = MergeNode(JsonNode.Parse(targetJson), JsonNode.Parse(patchJson));
        return merged?.ToJsonString() ?? "null";
    }

    private static JsonNode? MergeNode(JsonNode? target, JsonNode? patch)
    {
        if (patch is not JsonObject patchObj)
        {
            // Non-object patch replaces the target wholesale (deep clone to detach).
            return patch?.DeepClone();
        }

        JsonObject targetObj = target as JsonObject ?? new JsonObject();

        // Detach into a fresh object we own.
        var result = new JsonObject();
        foreach (KeyValuePair<string, JsonNode?> kvp in targetObj)
        {
            result[kvp.Key] = kvp.Value?.DeepClone();
        }

        foreach (KeyValuePair<string, JsonNode?> kvp in patchObj)
        {
            if (kvp.Value is null || kvp.Value.GetValueKind() == System.Text.Json.JsonValueKind.Null)
            {
                result.Remove(kvp.Key);
            }
            else
            {
                result[kvp.Key] = MergeNode(result.TryGetPropertyValue(kvp.Key, out JsonNode? existing) ? existing : null, kvp.Value);
            }
        }

        return result;
    }

    // Normalize via reparse with recursively sorted object keys, so values compare structurally
    // regardless of formatting or property order (RFC 7396 does not mandate property order).
    private static string Canonicalize(string json)
    {
        return SortNode(JsonNode.Parse(json))?.ToJsonString() ?? "null";
    }

    private static JsonNode? SortNode(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                var sorted = new JsonObject();
                foreach (KeyValuePair<string, JsonNode?> kvp in obj.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    sorted[kvp.Key] = SortNode(kvp.Value?.DeepClone());
                }

                return sorted;
            case JsonArray arr:
                var arrCopy = new JsonArray();
                foreach (JsonNode? item in arr)
                {
                    arrCopy.Add(SortNode(item?.DeepClone()));
                }

                return arrCopy;
            default:
                return node?.DeepClone();
        }
    }

    private static string RandomObject(Random rnd, int depth)
    {
        int props = rnd.Next(0, 4);
        var sb = new StringBuilder("{");

        // Unique keys per object (valid JSON object).
        var keys = new List<char>();
        for (char c = 'a'; c <= 'h'; c++)
        {
            keys.Add(c);
        }

        for (int i = 0; i < props && keys.Count > 0; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            int k = rnd.Next(0, keys.Count);
            char key = keys[k];
            keys.RemoveAt(k);

            sb.Append('"').Append(key).Append("\":");
            sb.Append(RandomValue(rnd, depth));
        }

        sb.Append('}');
        return sb.ToString();
    }

    private static string RandomValue(Random rnd, int depth)
    {
        int choice = depth >= 3 ? rnd.Next(0, 4) : rnd.Next(0, 6);
        return choice switch
        {
            0 => "\"s" + rnd.Next(0, 9) + "\"",
            1 => rnd.Next(0, 100).ToString(),
            2 => rnd.Next(0, 2) == 0 ? "true" : "false",
            3 => "null",
            4 => RandomArray(rnd, depth + 1),
            _ => RandomObject(rnd, depth + 1),
        };
    }

    private static string RandomArray(Random rnd, int depth)
    {
        int items = rnd.Next(0, 3);
        var sb = new StringBuilder("[");
        for (int i = 0; i < items; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append(RandomValue(rnd, depth));
        }

        sb.Append(']');
        return sb.ToString();
    }
}
