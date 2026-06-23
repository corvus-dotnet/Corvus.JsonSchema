// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Diagnostics;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Postgres;
using Npgsql;
using Testcontainers.PostgreSql;

// A container-backed *latency* harness for the control-plane throughput campaign (see
// docs/control-plane/throughput-campaign.md) — NOT a MemoryDiagnoser / allocation benchmark. It spins up a real
// backend in a container (via the pre-tunneled podman socket: DOCKER_HOST=unix:///tmp/podman-arazzo.sock,
// TESTCONTAINERS_RYUK_DISABLED=true) and times a high-frequency read-modify-write store op end-to-end, reporting
// round-trips, payload bytes, and mean/p50/p99 latency so a native-partial-update change can be measured
// before/after. Run directly: `dotnet run -c Release --project <this> [scenario]`.

int warmup = 500;
int measure = 5000;
string scenario = args.Length > 0 ? args[0] : "postgres-heartbeat";

switch (scenario)
{
    case "postgres-heartbeat":
        await PostgresHeartbeatAsync(warmup, measure);
        break;
    default:
        await Console.Error.WriteLineAsync($"Unknown scenario '{scenario}'. Known: postgres-heartbeat.");
        Environment.ExitCode = 1;
        break;
}

static async Task PostgresHeartbeatAsync(int warmup, int measure)
{
    Console.WriteLine("== Postgres runner heartbeat (RMW) ==");
    await using PostgreSqlContainer container = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    await container.StartAsync();
    await using NpgsqlDataSource dataSource = NpgsqlDataSource.Create(container.GetConnectionString());
    await PostgresRunnerRegistry.PrepareAsync(dataSource);
    await using PostgresRunnerRegistry registry = await PostgresRunnerRegistry.ConnectAsync(dataSource);

    RunnerRegistration reg = BuildRegistration("runner-1");
    await registry.RegisterAsync(reg, default);

    int docBytes = SerializedBytes(reg);
    DateTimeOffset baseAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    for (int i = 0; i < warmup; i++)
    {
        await registry.HeartbeatAsync("runner-1", baseAt.AddSeconds(i), default);
    }

    double[] samples = new double[measure];
    for (int i = 0; i < measure; i++)
    {
        long t0 = Stopwatch.GetTimestamp();
        await registry.HeartbeatAsync("runner-1", baseAt.AddSeconds(warmup + i), default);
        samples[i] = Stopwatch.GetElapsedTime(t0).TotalMicroseconds;
    }

    Report(docBytes, samples);
}

static void Report(int docBytes, double[] samples)
{
    Array.Sort(samples);
    double mean = samples.Average();
    Console.WriteLine($"registration doc size: {docBytes} bytes (RMW form re-sends the whole doc per heartbeat; native partial update sends only the parameters)");
    Console.WriteLine($"samples: {samples.Length}");
    Console.WriteLine(
        $"latency (us)  mean={mean,8:F1}  p50={Pct(samples, 50),8:F1}  p90={Pct(samples, 90),8:F1}  p99={Pct(samples, 99),8:F1}  max={samples[^1],8:F1}");
}

static double Pct(double[] sorted, int p)
{
    int idx = (int)Math.Ceiling(p / 100.0 * sorted.Length) - 1;
    return sorted[Math.Clamp(idx, 0, sorted.Length - 1)];
}

static int SerializedBytes(in RunnerRegistration reg)
{
    ArrayBufferWriter<byte> buffer = new();
    using (Utf8JsonWriter writer = new(buffer))
    {
        reg.WriteTo(writer);
    }

    return buffer.WrittenCount;
}

// Builds a representative runner registration: an address, two transports, and three loaded hosted versions —
// the kind of document a live runner re-serializes on every heartbeat today.
static RunnerRegistration BuildRegistration(string runnerId)
{
    ArrayBufferWriter<byte> buffer = new();
    using (Utf8JsonWriter writer = new(buffer))
    {
        writer.WriteStartObject();
        writer.WriteString("runnerId", runnerId);
        writer.WriteString("address", "https://runner-1.internal.example.com:8443");
        writer.WriteString("startedAt", "2026-01-01T00:00:00.0000000+00:00");
        writer.WriteString("lastSeenAt", "2026-01-01T00:00:00.0000000+00:00");
        writer.WriteNumber("maxConcurrency", 16);

        writer.WriteStartArray("transports");
        writer.WriteStringValue("http");
        writer.WriteStringValue("amqp");
        writer.WriteEndArray();

        writer.WriteStartArray("hostedVersions");
        foreach ((string baseId, int version) in new[] { ("shipping", 3), ("billing", 7), ("inventory", 2) })
        {
            writer.WriteStartObject();
            writer.WriteString("baseWorkflowId", baseId);
            writer.WriteNumber("versionNumber", version);
            writer.WriteString("hash", "sha256-0123456789abcdef0123456789abcdef");
            writer.WriteBoolean("loaded", true);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    using ParsedJsonDocument<RunnerRegistration> doc = ParsedJsonDocument<RunnerRegistration>.Parse(buffer.WrittenMemory);
    return doc.RootElement.Clone();
}
