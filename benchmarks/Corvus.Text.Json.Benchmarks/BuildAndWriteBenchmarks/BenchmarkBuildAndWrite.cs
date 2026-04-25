// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Benchmark.CorvusTextJson;
using BenchmarkDotNet.Attributes;
using CommunityToolkit.HighPerformance.Buffers;
using Corvus.Text.Json;

#pragma warning disable IDE0130 // Namespace does not match folder structure

namespace BuildAndWriteBenchmarks;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Construct elements from a JSON element.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkBuildAndWrite
{
    [Benchmark(Baseline = true)]
    public bool BuildJsonObject()
    {
        var bufferWriter = new ArrayPoolBufferWriter<byte>();
        System.Text.Json.Utf8JsonWriter writer = new(bufferWriter);
        System.Text.Json.Nodes.JsonObject jsonObject =
        [
            new ("age", 51),
            new ("name",
            new System.Text.Json.Nodes.JsonObject([
                new ("firstName", "Michael"),
                new ("lastName", "Adams"),
                new ("otherNames", new System.Text.Json.Nodes.JsonArray("Francis", "James"))])),
            new ("competedInYears", new System.Text.Json.Nodes.JsonArray(2012, 2016, 2024)),
        ];

        jsonObject.WriteTo(writer);
        writer.Flush();
        writer.Dispose();
        bufferWriter.Dispose();
        return true;
    }

    [Benchmark]
    public bool BuildCorvusJsonSchema()
    {
        var bufferWriter = new ArrayPoolBufferWriter<byte>();
        System.Text.Json.Utf8JsonWriter writer = new(bufferWriter);
        var person = Benchmark.CorvusJsonSchema.Person.Create(
            age: 51,
            name: Benchmark.CorvusJsonSchema.PersonName.Create(
                firstName: "Michael",
                lastName: "Adams",
                otherNames: ["Francis", "James"]),
            competedInYears: [2012, 2016, 2024]);

        person.WriteTo(writer);
        writer.Flush();
        writer.Dispose();
        bufferWriter.Dispose();
        return true;
    }

    [Benchmark]
    public bool BuildCorvusTextJson()
    {
        using var workspace = JsonWorkspace.Create();

        using JsonDocumentBuilder<Person.Mutable> person = Person.CreateBuilder(
            workspace,
            (ref b) => b.Create(
            age: 51,
            name: PersonName.Build(static (ref personName) =>
            {
                personName.Create(
                    firstName: "Michael"u8,
                    lastName: "Adams"u8,
                    otherNames: OtherNames.Build(static (ref otherNames) =>
                    {
                        otherNames.AddItem("Francis"u8);
                        otherNames.AddItem("James"u8);
                    }));
            }),
            competedInYears: CompetedInYears.Build([2012, 2016, 2024])));

        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(defaultBufferSize: 1024, out IByteBufferWriter bufferWriter);
        person.WriteTo(writer);
        writer.Flush();
        workspace.ReturnWriterAndBuffer(writer, bufferWriter);
        return true;
    }

    [Benchmark]
    public bool BuildPocoAndSerialize()
    {
        var bufferWriter = new ArrayPoolBufferWriter<byte>();
        System.Text.Json.Utf8JsonWriter writer = new(bufferWriter);

        PersonPoco person = new()
        {
            Age = 51,
            Name = new PersonNamePoco
            {
                FirstName = "Michael",
                LastName = "Adams",
                OtherNames = ["Francis", "James"],
            },
            CompetedInYears = [2012, 2016, 2024],
        };

        System.Text.Json.JsonSerializer.Serialize(writer, person, PersonPocoContext.Default.PersonPoco);
        writer.Flush();
        writer.Dispose();
        bufferWriter.Dispose();
        return true;
    }
}

/// <summary>
/// Build-only benchmarks to isolate construction cost from serialization cost.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkBuildOnly
{
    [Benchmark(Baseline = true)]
    public System.Text.Json.Nodes.JsonObject BuildJsonObject()
    {
        return
        [
            new ("age", 51),
            new ("name",
            new System.Text.Json.Nodes.JsonObject([
                new ("firstName", "Michael"),
                new ("lastName", "Adams"),
                new ("otherNames", new System.Text.Json.Nodes.JsonArray("Francis", "James"))])),
            new ("competedInYears", new System.Text.Json.Nodes.JsonArray(2012, 2016, 2024)),
        ];
    }

    [Benchmark]
    public JsonDocumentBuilder<Person.Mutable> BuildCorvusTextJson()
    {
        using var workspace = JsonWorkspace.Create();

        return Person.CreateBuilder(
            workspace,
            (ref b) => b.Create(
            age: 51,
            name: PersonName.Build(static (ref personName) =>
            {
                personName.Create(
                    firstName: "Michael"u8,
                    lastName: "Adams"u8,
                    otherNames: OtherNames.Build(static (ref otherNames) =>
                    {
                        otherNames.AddItem("Francis"u8);
                        otherNames.AddItem("James"u8);
                    }));
            }),
            competedInYears: CompetedInYears.Build([2012, 2016, 2024])));
    }

    [Benchmark]
    public PersonPoco BuildPoco()
    {
        return new()
        {
            Age = 51,
            Name = new PersonNamePoco
            {
                FirstName = "Michael",
                LastName = "Adams",
                OtherNames = ["Francis", "James"],
            },
            CompetedInYears = [2012, 2016, 2024],
        };
    }
}

/// <summary>
/// Write-only benchmarks to isolate serialization cost from construction cost.
/// Pre-built documents are written in each iteration.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkWriteOnly
{
    private System.Text.Json.Nodes.JsonObject _jsonObject = default!;
    private JsonWorkspace _workspace;
    private JsonDocumentBuilder<Person.Mutable> _person = default!;
    private PersonPoco _poco = default!;

    [GlobalSetup]
    public void Setup()
    {
        _jsonObject =
        [
            new ("age", 51),
            new ("name",
            new System.Text.Json.Nodes.JsonObject([
                new ("firstName", "Michael"),
                new ("lastName", "Adams"),
                new ("otherNames", new System.Text.Json.Nodes.JsonArray("Francis", "James"))])),
            new ("competedInYears", new System.Text.Json.Nodes.JsonArray(2012, 2016, 2024)),
        ];

        _workspace = JsonWorkspace.Create();
        _person = Person.CreateBuilder(
            _workspace,
            (ref b) => b.Create(
            age: 51,
            name: PersonName.Build(static (ref personName) =>
            {
                personName.Create(
                    firstName: "Michael"u8,
                    lastName: "Adams"u8,
                    otherNames: OtherNames.Build(static (ref otherNames) =>
                    {
                        otherNames.AddItem("Francis"u8);
                        otherNames.AddItem("James"u8);
                    }));
            }),
            competedInYears: CompetedInYears.Build([2012, 2016, 2024])));

        _poco = new()
        {
            Age = 51,
            Name = new PersonNamePoco
            {
                FirstName = "Michael",
                LastName = "Adams",
                OtherNames = ["Francis", "James"],
            },
            CompetedInYears = [2012, 2016, 2024],
        };
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _person.Dispose();
        _workspace.Dispose();
    }

    [Benchmark(Baseline = true)]
    public bool WriteJsonObject()
    {
        var bufferWriter = new ArrayPoolBufferWriter<byte>();
        System.Text.Json.Utf8JsonWriter writer = new(bufferWriter);
        _jsonObject.WriteTo(writer);
        writer.Flush();
        writer.Dispose();
        bufferWriter.Dispose();
        return true;
    }

    [Benchmark]
    public bool WriteCorvusTextJson()
    {
        Utf8JsonWriter writer = _workspace.RentWriterAndBuffer(defaultBufferSize: 1024, out IByteBufferWriter bufferWriter);
        _person.WriteTo(writer);
        writer.Flush();
        _workspace.ReturnWriterAndBuffer(writer, bufferWriter);
        return true;
    }

    [Benchmark]
    public bool WritePocoAndSerialize()
    {
        var bufferWriter = new ArrayPoolBufferWriter<byte>();
        System.Text.Json.Utf8JsonWriter writer = new(bufferWriter);
        System.Text.Json.JsonSerializer.Serialize(writer, _poco, PersonPocoContext.Default.PersonPoco);
        writer.Flush();
        writer.Dispose();
        bufferWriter.Dispose();
        return true;
    }
}