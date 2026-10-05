// <copyright file="GetStringRun.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;

namespace Corvus.Text.Json.RuntimeEvaluator.Benchmarks.SourceMeta;

/// <summary>
/// Warm <c>GetString</c> over every string value of a corpus's instances: the time of the last pass by the protocol's
/// warm-up rule, as nanoseconds a string, with the strings' count and mean length in bytes.
/// </summary>
internal static class GetStringRun
{
    public static int Run(string[] args)
    {
        string root = Environment.GetEnvironmentVariable("COLD_ROOT") ?? Path.Combine(AppContext.BaseDirectory, "sourcemeta");
        ParsedJsonDocument<JsonElement>[] documents = [.. File.ReadAllLines(Path.Combine(root, args[0] + "-instances.jsonl")).Where(l => l.Length > 0).Select(l => ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes(l)))];
        List<JsonElement> strings = [];
        foreach (ParsedJsonDocument<JsonElement> document in documents)
        {
            Collect(document.RootElement, strings);
        }

        JsonElement[] values = [.. strings];
        if (values.Length == 0)
        {
            return 1;
        }

        long warmUpEnd = Stopwatch.GetTimestamp() + (2 * Stopwatch.Frequency);
        long warm = 0;
        long characters = 0;
        for (int i = 0; i < 100 || Stopwatch.GetTimestamp() < warmUpEnd; i++)
        {
            long start = Stopwatch.GetTimestamp();
            characters = 0;
            foreach (JsonElement value in values)
            {
                characters += value.GetString()!.Length;
            }

            warm = Stopwatch.GetTimestamp() - start;
        }

        Console.WriteLine($"{args[0]},{warm * 1_000_000_000.0 / Stopwatch.Frequency / values.Length:F2},{values.Length},{(double)characters / values.Length:F1}");
        return 0;
    }

    /// <summary>
    /// Warm validation of an array of 1,000 ASCII strings against <c>minLength</c> and <c>maxLength</c> bounds the byte
    /// length does not decide (the string is longer than the minimum but shorter than four times it): nanoseconds a
    /// string, for several string lengths.
    /// </summary>
    public static int RunLengths()
    {
        foreach (int length in (int[])[8, 20, 40, 100, 400, 2000])
        {
            byte[] schema = System.Text.Encoding.UTF8.GetBytes($"{{\"type\":\"array\",\"items\":{{\"type\":\"string\",\"minLength\":{length / 2},\"maxLength\":{length * 2}}}}}");
            using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema, new JsonSchemaEvaluatorOptions { DefaultDialect = JsonSchemaDialect.Draft202012 });
            string item = "\"" + new string('a', length) + "\"";
            using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes("[" + string.Join(",", Enumerable.Repeat(item, 1000)) + "]"));
            long warmUpEnd = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
            int valid = 0;
            while (Stopwatch.GetTimestamp() < warmUpEnd)
            {
                valid += evaluator.Evaluate(document.RootElement) ? 1 : 0;
            }

            double best = double.MaxValue;
            for (int batch = 0; batch < 41; batch++)
            {
                long start = Stopwatch.GetTimestamp();
                for (int i = 0; i < 200; i++)
                {
                    valid += evaluator.Evaluate(document.RootElement) ? 1 : 0;
                }

                best = Math.Min(best, (Stopwatch.GetTimestamp() - start) * 1_000_000_000.0 / Stopwatch.Frequency / 200 / 1000);
            }

            Console.WriteLine($"{length},{best:F2},{(valid > 0 ? "valid" : "INVALID")}");
        }

        return 0;
    }

    private static void Collect(JsonElement element, List<JsonElement> strings)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                strings.Add(element);
                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    Collect(item, strings);
                }

                break;
            case JsonValueKind.Object:
                foreach (JsonProperty<JsonElement> property in element.EnumerateObject())
                {
                    Collect(property.Value, strings);
                }

                break;
        }
    }
}