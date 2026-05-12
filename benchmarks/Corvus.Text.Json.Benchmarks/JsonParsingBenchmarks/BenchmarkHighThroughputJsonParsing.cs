// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;

namespace JsonParsingBenchmarks;

/// <summary>
/// High-throughput JSON parsing benchmarks focused on server/batch processing scenarios.
/// Tests steady-state performance with realistic data volumes.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkHighThroughputJsonParsing
{
    private string? batchApiResponseJson;
    private string? logEntriesJson;
    private string? metricsDataJson;
    private string? dataExportJson;
    private byte[]? batchApiResponseBytes;
    private byte[]? logEntriesBytes;
    private byte[]? metricsDataBytes;

    [GlobalSetup]
    public void Setup()
    {
        // Batch API response - typical server-side processing
        batchApiResponseJson = GenerateBatchApiResponseJson();

        // Log entries - typical log processing scenario
        logEntriesJson = GenerateLogEntriesJson();

        // Metrics data - typical monitoring/analytics scenario
        metricsDataJson = GenerateMetricsDataJson();

        // Data export - typical ETL/reporting scenario
        dataExportJson = GenerateDataExportJson();

        // Pre-encode to UTF8 for byte-based benchmarks
        batchApiResponseBytes = Encoding.UTF8.GetBytes(batchApiResponseJson);
        logEntriesBytes = Encoding.UTF8.GetBytes(logEntriesJson);
        metricsDataBytes = Encoding.UTF8.GetBytes(metricsDataJson);
    }

    #region Batch API Response Processing

    [Benchmark]
    public int ProcessBatchApiResponseCorvus()
    {
        using var document = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(batchApiResponseJson!);
        Corvus.Text.Json.JsonElement root = document.RootElement;

        int totalProcessed = 0;

        if (root.TryGetProperty("data"u8, out Corvus.Text.Json.JsonElement dataElement) &&
            dataElement.TryGetProperty("users"u8, out Corvus.Text.Json.JsonElement usersElement) &&
            usersElement.ValueKind == Corvus.Text.Json.JsonValueKind.Array)
        {
            foreach (Corvus.Text.Json.JsonElement user in usersElement.EnumerateArray())
            {
                // Simulate processing each user
                if (user.TryGetProperty("active"u8, out Corvus.Text.Json.JsonElement activeElement) && activeElement.GetBoolean())
                {
                    totalProcessed++;
                }
            }
        }

        return totalProcessed;
    }

    [Benchmark(Baseline = true)]
    public int ProcessBatchApiResponseSystemTextJson()
    {
        using var document = System.Text.Json.JsonDocument.Parse(batchApiResponseJson!);
        System.Text.Json.JsonElement root = document.RootElement;

        int totalProcessed = 0;

        if (root.TryGetProperty("data"u8, out System.Text.Json.JsonElement dataElement) &&
            dataElement.TryGetProperty("users"u8, out System.Text.Json.JsonElement usersElement) &&
            usersElement.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (System.Text.Json.JsonElement user in usersElement.EnumerateArray())
            {
                // Simulate processing each user
                if (user.TryGetProperty("active"u8, out System.Text.Json.JsonElement activeElement) && activeElement.GetBoolean())
                {
                    totalProcessed++;
                }
            }
        }

        return totalProcessed;
    }

    #endregion

    #region UTF8 Byte Processing Benchmarks

    [Benchmark]
    public int ProcessBatchApiResponseFromBytesCorvus()
    {
        using var document = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(batchApiResponseBytes!);
        Corvus.Text.Json.JsonElement root = document.RootElement;

        int totalProcessed = 0;

        if (root.TryGetProperty("data", out Corvus.Text.Json.JsonElement dataElement) &&
            dataElement.TryGetProperty("users", out Corvus.Text.Json.JsonElement usersElement) &&
            usersElement.ValueKind == Corvus.Text.Json.JsonValueKind.Array)
        {
            foreach (Corvus.Text.Json.JsonElement user in usersElement.EnumerateArray())
            {
                if (user.TryGetProperty("active", out Corvus.Text.Json.JsonElement activeElement) && activeElement.GetBoolean())
                {
                    totalProcessed++;
                }
            }
        }

        return totalProcessed;
    }

    [Benchmark]
    public int ProcessBatchApiResponseFromBytesSystemTextJson()
    {
        using var document = System.Text.Json.JsonDocument.Parse(batchApiResponseBytes!);
        System.Text.Json.JsonElement root = document.RootElement;

        int totalProcessed = 0;

        if (root.TryGetProperty("data", out System.Text.Json.JsonElement dataElement) &&
            dataElement.TryGetProperty("users", out System.Text.Json.JsonElement usersElement) &&
            usersElement.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (System.Text.Json.JsonElement user in usersElement.EnumerateArray())
            {
                if (user.TryGetProperty("active", out System.Text.Json.JsonElement activeElement) && activeElement.GetBoolean())
                {
                    totalProcessed++;
                }
            }
        }

        return totalProcessed;
    }

    #endregion

    #region Log Processing Benchmarks

    [Benchmark]
    public (int, int, int) ProcessLogEntriesCorvus()
    {
        using var document = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(logEntriesJson!);
        Corvus.Text.Json.JsonElement root = document.RootElement;

        int errorCount = 0;
        int warningCount = 0;
        int infoCount = 0;

        if (root.TryGetProperty("logs"u8, out Corvus.Text.Json.JsonElement logsElement) &&
            logsElement.ValueKind == Corvus.Text.Json.JsonValueKind.Array)
        {
            foreach (Corvus.Text.Json.JsonElement logEntry in logsElement.EnumerateArray())
            {
                if (logEntry.TryGetProperty("level"u8, out Corvus.Text.Json.JsonElement levelElement))
                {
                    using UnescapedUtf8JsonString level = levelElement.GetUtf8String();
                    ReadOnlySpan<byte> levelText = level.Span;
                    if (levelText.SequenceEqual("error"u8))
                    {
                        errorCount++;
                    }
                    else if (levelText.SequenceEqual("warning"u8))
                    {
                        warningCount++;
                    }
                    else if (levelText.SequenceEqual("info"u8))
                    {
                        infoCount++;
                    }
                }
            }
        }

        return (errorCount, warningCount, infoCount);
    }

    [Benchmark]
    public (int, int, int) ProcessLogEntriesSystemTextJson()
    {
        using var document = System.Text.Json.JsonDocument.Parse(logEntriesJson!);
        System.Text.Json.JsonElement root = document.RootElement;

        int errorCount = 0;
        int warningCount = 0;
        int infoCount = 0;

        if (root.TryGetProperty("logs", out System.Text.Json.JsonElement logsElement) &&
            logsElement.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (System.Text.Json.JsonElement logEntry in logsElement.EnumerateArray())
            {
                if (logEntry.TryGetProperty("level", out System.Text.Json.JsonElement levelElement))
                {
                    string? level = levelElement.GetString();
                    switch (level)
                    {
                        case "error":
                            errorCount++;
                            break;
                        case "warning":
                            warningCount++;
                            break;
                        case "info":
                            infoCount++;
                            break;
                    }
                }
            }
        }

        return (errorCount, warningCount, infoCount);
    }

    #endregion

    #region Metrics Processing Benchmarks

    [Benchmark]
    public (double, double, int) ProcessMetricsDataCorvus()
    {
        using var document = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(metricsDataJson!);
        Corvus.Text.Json.JsonElement root = document.RootElement;

        double totalValue = 0.0;
        double maxValue = double.MinValue;
        int dataPointCount = 0;

        if (root.TryGetProperty("metrics", out Corvus.Text.Json.JsonElement metricsElement) &&
            metricsElement.ValueKind == Corvus.Text.Json.JsonValueKind.Array)
        {
            foreach (Corvus.Text.Json.JsonElement metric in metricsElement.EnumerateArray())
            {
                if (metric.TryGetProperty("dataPoints"u8, out Corvus.Text.Json.JsonElement dataPointsElement) &&
                    dataPointsElement.ValueKind == Corvus.Text.Json.JsonValueKind.Array)
                {
                    foreach (Corvus.Text.Json.JsonElement dataPoint in dataPointsElement.EnumerateArray())
                    {
                        if (dataPoint.TryGetProperty("value"u8, out Corvus.Text.Json.JsonElement valueElement) &&
                            valueElement.ValueKind == Corvus.Text.Json.JsonValueKind.Number)
                        {
                            double value = valueElement.GetDouble();
                            totalValue += value;
                            maxValue = Math.Max(maxValue, value);
                            dataPointCount++;
                        }
                    }
                }
            }
        }

        return (totalValue, maxValue, dataPointCount);
    }

    [Benchmark]
    public (double, double, int) ProcessMetricsDataSystemTextJson()
    {
        using var document = System.Text.Json.JsonDocument.Parse(metricsDataJson!);
        System.Text.Json.JsonElement root = document.RootElement;

        double totalValue = 0.0;
        double maxValue = double.MinValue;
        int dataPointCount = 0;

        if (root.TryGetProperty("metrics"u8, out System.Text.Json.JsonElement metricsElement) &&
            metricsElement.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (System.Text.Json.JsonElement metric in metricsElement.EnumerateArray())
            {
                if (metric.TryGetProperty("dataPoints"u8, out System.Text.Json.JsonElement dataPointsElement) &&
                    dataPointsElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (System.Text.Json.JsonElement dataPoint in dataPointsElement.EnumerateArray())
                    {
                        if (dataPoint.TryGetProperty("value"u8, out System.Text.Json.JsonElement valueElement) &&
                            valueElement.ValueKind == System.Text.Json.JsonValueKind.Number)
                        {
                            double value = valueElement.GetDouble();
                            totalValue += value;
                            maxValue = Math.Max(maxValue, value);
                            dataPointCount++;
                        }
                    }
                }
            }
        }

        return (totalValue, maxValue, dataPointCount);
    }

    #endregion

    #region Data Export Processing Benchmarks

    [Benchmark]
    public (int, decimal, int) ProcessDataExportCorvus()
    {
        using var document = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(dataExportJson!);
        Corvus.Text.Json.JsonElement root = document.RootElement;

        int recordCount = 0;
        decimal totalAmount = 0m;
        UnescapedUtf8JsonString lastCustomer = default;

        if (root.TryGetProperty("records"u8, out Corvus.Text.Json.JsonElement recordsElement) &&
            recordsElement.ValueKind == Corvus.Text.Json.JsonValueKind.Array)
        {
            foreach (Corvus.Text.Json.JsonElement record in recordsElement.EnumerateArray())
            {
                recordCount++;

                if (record.TryGetProperty("amountu8", out Corvus.Text.Json.JsonElement amountElement) &&
                    amountElement.ValueKind == Corvus.Text.Json.JsonValueKind.Number)
                {
                    totalAmount += (decimal)amountElement.GetDouble();
                }

                if (record.TryGetProperty("customerName"u8, out Corvus.Text.Json.JsonElement customerElement))
                {
                    lastCustomer = customerElement.GetUtf8String();
                }
            }
        }

        return (recordCount, totalAmount, lastCustomer.Span.Length);
    }

    [Benchmark]
    public (int, decimal, int) ProcessDataExportSystemTextJson()
    {
        using var document = System.Text.Json.JsonDocument.Parse(dataExportJson!);
        System.Text.Json.JsonElement root = document.RootElement;

        int recordCount = 0;
        decimal totalAmount = 0m;
        string? lastCustomer = null;

        if (root.TryGetProperty("records"u8, out System.Text.Json.JsonElement recordsElement) &&
            recordsElement.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (System.Text.Json.JsonElement record in recordsElement.EnumerateArray())
            {
                recordCount++;

                if (record.TryGetProperty("amount"u8, out System.Text.Json.JsonElement amountElement) &&
                    amountElement.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    totalAmount += (decimal)amountElement.GetDouble();
                }

                if (record.TryGetProperty("customerName"u8, out System.Text.Json.JsonElement customerElement))
                {
                    lastCustomer = customerElement.GetString();
                }
            }
        }

        return (recordCount, totalAmount, lastCustomer?.Length ?? 0);
    }

    #endregion

    #region JSON Generation Methods

    private static string GenerateBatchApiResponseJson()
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"success\": true,");
        sb.AppendLine("  \"data\": {");
        sb.AppendLine("    \"users\": [");

        for (int i = 0; i < 200; i++)
        {
            if (i > 0) sb.Append(",");
            sb.AppendLine();
            sb.AppendLine("      {");
            sb.AppendLine($"        \"id\": {i + 1},");
            sb.AppendLine($"        \"username\": \"user{i + 1}\",");
            sb.AppendLine($"        \"email\": \"user{i + 1}@example.com\",");
            sb.AppendLine($"        \"active\": {(i % 3 != 0).ToString().ToLower()},");
            sb.AppendLine($"        \"score\": {(i * 12.34).ToString("F2")},");
            sb.AppendLine($"        \"lastLogin\": \"2024-01-{(i % 28) + 1:D2}T{i % 24:D2}:00:00Z\",");
            sb.AppendLine($"        \"department\": \"Department {(i % 5) + 1}\"");
            sb.Append("      }");
        }

        sb.AppendLine();
        sb.AppendLine("    ],");
        sb.AppendLine("    \"pagination\": {");
        sb.AppendLine("      \"total\": 200,");
        sb.AppendLine("      \"page\": 1,");
        sb.AppendLine("      \"pageSize\": 200");
        sb.AppendLine("    }");
        sb.AppendLine("  },");
        sb.AppendLine("  \"metadata\": {");
        sb.AppendLine("    \"requestId\": \"batch_123456\",");
        sb.AppendLine("    \"timestamp\": \"2024-01-15T15:30:00Z\",");
        sb.AppendLine("    \"processingTimeMs\": 234");
        sb.AppendLine("  }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateLogEntriesJson()
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"logs\": [");

        string[] levels = new[] { "info", "warning", "error", "debug" };
        string[] services = new[] { "api", "database", "cache", "auth", "payment" };

        for (int i = 0; i < 300; i++)
        {
            if (i > 0) sb.Append(",");
            sb.AppendLine();
            sb.AppendLine("    {");
            sb.AppendLine($"      \"timestamp\": \"2024-01-15T{i % 24:D2}:{i % 60:D2}:{i % 60:D2}Z\",");
            sb.AppendLine($"      \"level\": \"{levels[i % levels.Length]}\",");
            sb.AppendLine($"      \"service\": \"{services[i % services.Length]}\",");
            sb.AppendLine($"      \"message\": \"Log message {i + 1} with some details\",");
            sb.AppendLine($"      \"requestId\": \"req_{i + 1000}\",");
            sb.AppendLine($"      \"userId\": {(i % 100) + 1},");
            sb.AppendLine($"      \"duration\": {(i * 12) % 1000}");
            sb.Append("    }");
        }

        sb.AppendLine();
        sb.AppendLine("  ],");
        sb.AppendLine("  \"metadata\": {");
        sb.AppendLine("    \"totalLogs\": 300,");
        sb.AppendLine("    \"timeRange\": \"2024-01-15T00:00:00Z/2024-01-15T23:59:59Z\"");
        sb.AppendLine("  }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateMetricsDataJson()
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"metrics\": [");

        string[] metricNames = new[] { "cpu_usage", "memory_usage", "disk_io", "network_io", "response_time" };

        for (int i = 0; i < metricNames.Length; i++)
        {
            if (i > 0) sb.Append(",");
            sb.AppendLine();
            sb.AppendLine("    {");
            sb.AppendLine($"      \"name\": \"{metricNames[i]}\",");
            sb.AppendLine($"      \"unit\": \"percent\",");
            sb.AppendLine("      \"dataPoints\": [");

            for (int j = 0; j < 100; j++)
            {
                if (j > 0) sb.Append(",");
                sb.AppendLine();
                sb.AppendLine("        {");
                sb.AppendLine($"          \"timestamp\": \"2024-01-15T15:{j % 60:D2}:00Z\",");
                sb.AppendLine($"          \"value\": {(Math.Sin(j * 0.1) * 50 + 50).ToString("F3")}");
                sb.Append("        }");
            }

            sb.AppendLine();
            sb.AppendLine("      ]");
            sb.Append("    }");
        }

        sb.AppendLine();
        sb.AppendLine("  ],");
        sb.AppendLine("  \"metadata\": {");
        sb.AppendLine("    \"interval\": \"1m\",");
        sb.AppendLine("    \"source\": \"monitoring-system\"");
        sb.AppendLine("  }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateDataExportJson()
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"exportInfo\": {");
        sb.AppendLine("    \"format\": \"json\",");
        sb.AppendLine("    \"timestamp\": \"2024-01-15T15:30:00Z\",");
        sb.AppendLine("    \"totalRecords\": 150");
        sb.AppendLine("  },");
        sb.AppendLine("  \"records\": [");

        string[] customerNames = new[] { "Acme Corp", "Tech Solutions", "Global Industries", "Data Systems", "Innovation Labs" };
        string[] categories = new[] { "Software", "Hardware", "Services", "Consulting", "Support" };

        for (int i = 0; i < 150; i++)
        {
            if (i > 0) sb.Append(",");
            sb.AppendLine();
            sb.AppendLine("    {");
            sb.AppendLine($"      \"id\": {i + 1},");
            sb.AppendLine($"      \"customerName\": \"{customerNames[i % customerNames.Length]}\",");
            sb.AppendLine($"      \"category\": \"{categories[i % categories.Length]}\",");
            sb.AppendLine($"      \"amount\": {(i * 123.45 + 1000).ToString("F2")},");
            sb.AppendLine($"      \"quantity\": {(i % 20) + 1},");
            sb.AppendLine($"      \"date\": \"2024-01-{(i % 28) + 1:D2}\",");
            sb.AppendLine($"      \"status\": \"{(i % 3 == 0 ? "completed" : i % 3 == 1 ? "pending" : "cancelled")}\",");
            sb.AppendLine($"      \"region\": \"Region {(i % 4) + 1}\"");
            sb.Append("    }");
        }

        sb.AppendLine();
        sb.AppendLine("  ]");
        sb.AppendLine("}");

        return sb.ToString();
    }

    #endregion
}