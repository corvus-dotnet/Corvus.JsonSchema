// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;

namespace JsonParsingBenchmarks;

/// <summary>
/// Compares two paths for creating a mutable JsonDocumentBuilder from JSON:
///   1. ParsedJsonDocument → CreateBuilder (two-pass)
///   2. JsonDocumentBuilder.Parse (single-pass, direct)
/// Tests multiple JSON shapes and sizes to show where the single-pass wins.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkParseToBuilder
{
    private byte[]? smallObjectBytes;
    private byte[]? mediumObjectBytes;
    private byte[]? largeArrayBytes;
    private byte[]? deeplyNestedBytes;
    private byte[]? apiResponseBytes;
    private byte[]? stringHeavyBytes;

    [GlobalSetup]
    public void Setup()
    {
        smallObjectBytes = Encoding.UTF8.GetBytes(GenerateSmallObject());
        mediumObjectBytes = Encoding.UTF8.GetBytes(GenerateMediumObject());
        largeArrayBytes = Encoding.UTF8.GetBytes(GenerateLargeArray());
        deeplyNestedBytes = Encoding.UTF8.GetBytes(GenerateDeeplyNested());
        apiResponseBytes = Encoding.UTF8.GetBytes(GenerateApiResponse());
        stringHeavyBytes = Encoding.UTF8.GetBytes(GenerateStringHeavy());
    }

    #region Small Object (~100 bytes)

    [Benchmark(Baseline = true)]
    public JsonElement.Mutable SmallObject_ParseThenBuild()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc =
            ParsedJsonDocument<JsonElement>.Parse(smallObjectBytes);
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            doc.RootElement.CreateBuilder(workspace);
        return builder.RootElement;
    }

    [Benchmark]
    public JsonElement.Mutable SmallObject_DirectParse()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, smallObjectBytes);
        return builder.RootElement;
    }

    #endregion

    #region Medium Object (~2 KB, 50 properties)

    [Benchmark]
    public JsonElement.Mutable MediumObject_ParseThenBuild()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc =
            ParsedJsonDocument<JsonElement>.Parse(mediumObjectBytes);
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            doc.RootElement.CreateBuilder(workspace);
        return builder.RootElement;
    }

    [Benchmark]
    public JsonElement.Mutable MediumObject_DirectParse()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, mediumObjectBytes);
        return builder.RootElement;
    }

    #endregion

    #region Large Array (~50 KB, 1000 elements)

    [Benchmark]
    public JsonElement.Mutable LargeArray_ParseThenBuild()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc =
            ParsedJsonDocument<JsonElement>.Parse(largeArrayBytes);
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            doc.RootElement.CreateBuilder(workspace);
        return builder.RootElement;
    }

    [Benchmark]
    public JsonElement.Mutable LargeArray_DirectParse()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, largeArrayBytes);
        return builder.RootElement;
    }

    #endregion

    #region Deeply Nested (~1 KB, 32 levels)

    [Benchmark]
    public JsonElement.Mutable DeeplyNested_ParseThenBuild()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc =
            ParsedJsonDocument<JsonElement>.Parse(deeplyNestedBytes);
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            doc.RootElement.CreateBuilder(workspace);
        return builder.RootElement;
    }

    [Benchmark]
    public JsonElement.Mutable DeeplyNested_DirectParse()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, deeplyNestedBytes);
        return builder.RootElement;
    }

    #endregion

    #region API Response (~10 KB, mixed structure)

    [Benchmark]
    public JsonElement.Mutable ApiResponse_ParseThenBuild()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc =
            ParsedJsonDocument<JsonElement>.Parse(apiResponseBytes);
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            doc.RootElement.CreateBuilder(workspace);
        return builder.RootElement;
    }

    [Benchmark]
    public JsonElement.Mutable ApiResponse_DirectParse()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, apiResponseBytes);
        return builder.RootElement;
    }

    #endregion

    #region String-heavy (~5 KB, escape sequences)

    [Benchmark]
    public JsonElement.Mutable StringHeavy_ParseThenBuild()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> doc =
            ParsedJsonDocument<JsonElement>.Parse(stringHeavyBytes);
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            doc.RootElement.CreateBuilder(workspace);
        return builder.RootElement;
    }

    [Benchmark]
    public JsonElement.Mutable StringHeavy_DirectParse()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, stringHeavyBytes);
        return builder.RootElement;
    }

    #endregion

    #region Data generators

    private static string GenerateSmallObject()
    {
        return """{"name":"Alice","age":30,"active":true,"score":98.6,"email":"alice@example.com"}""";
    }

    private static string GenerateMediumObject()
    {
        StringBuilder sb = new();
        sb.Append('{');

        for (int i = 0; i < 50; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            switch (i % 5)
            {
                case 0: sb.Append($"\"str_{i}\":\"value_{i}\""); break;
                case 1: sb.Append($"\"num_{i}\":{i * 17}"); break;
                case 2: sb.Append($"\"bool_{i}\":{(i % 2 == 0 ? "true" : "false")}"); break;
                case 3: sb.Append($"\"float_{i}\":{i * 3.14159}"); break;
                case 4: sb.Append($"\"null_{i}\":null"); break;
            }
        }

        sb.Append('}');
        return sb.ToString();
    }

    private static string GenerateLargeArray()
    {
        StringBuilder sb = new();
        sb.Append('[');

        for (int i = 0; i < 1000; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append($"{{\"id\":{i},\"name\":\"item_{i}\",\"value\":{i * 1.5},\"active\":{(i % 3 == 0 ? "true" : "false")}}}");
        }

        sb.Append(']');
        return sb.ToString();
    }

    private static string GenerateDeeplyNested()
    {
        StringBuilder sb = new();

        for (int i = 0; i < 32; i++)
        {
            sb.Append("{\"level\":");
            sb.Append(i);
            sb.Append(",\"child\":");
        }

        sb.Append("\"leaf\"");

        for (int i = 0; i < 32; i++)
        {
            sb.Append('}');
        }

        return sb.ToString();
    }

    private static string GenerateApiResponse()
    {
        StringBuilder sb = new();
        sb.Append("""{"status":"ok","pagination":{"page":1,"perPage":50,"total":2847},"data":[""");

        for (int i = 0; i < 50; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append($"{{\"id\":{i},\"username\":\"user_{i}\",\"email\":\"user_{i}@example.com\",\"profile\":{{\"displayName\":\"User {i}\",\"bio\":\"A short biography for user {i}.\",\"avatar\":\"https://cdn.example.com/avatars/{i}.png\"}},\"settings\":{{\"theme\":\"dark\",\"notifications\":true,\"language\":\"en\"}},\"stats\":{{\"posts\":{i * 12},\"followers\":{i * 47},\"following\":{i * 23}}}}}");
        }

        sb.Append("""]}""");
        return sb.ToString();
    }

    private static string GenerateStringHeavy()
    {
        StringBuilder sb = new();
        sb.Append('{');

        for (int i = 0; i < 30; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            switch (i % 6)
            {
                case 0: sb.Append($"\"path_{i}\":\"C:\\\\Users\\\\test\\\\Documents\\\\file_{i}.txt\""); break;
                case 1: sb.Append($"\"url_{i}\":\"https://api.example.com/v2/resources/{i}?filter=active&sort=name\""); break;
                case 2: sb.Append($"\"desc_{i}\":\"This is a longer description with some \\\"quoted\\\" text and a tab\\there.\""); break;
                case 3: sb.Append($"\"unicode_{i}\":\"caf\\u00E9 na\\u00EFve r\\u00E9sum\\u00E9\""); break;
                case 4: sb.Append($"\"multiline_{i}\":\"line 1\\nline 2\\nline 3\\nline 4\""); break;
                case 5: sb.Append($"\"mixed_{i}\":\"Hello \\\"World\\\" from C:\\\\path\\\\to\\\\file\\n\\tindented\""); break;
            }
        }

        sb.Append('}');
        return sb.ToString();
    }

    #endregion
}
