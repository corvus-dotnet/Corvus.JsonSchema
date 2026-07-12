// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Benchmark.CorvusTextJson;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;

#pragma warning disable IDE0130 // Namespace does not match folder structure

namespace BuildAndWriteBenchmarks;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Compares the two routes to a self-contained <see cref="ParsedJsonDocument{T}"/> built from
/// .NET values: the pre-Create() round trip (build a <see cref="JsonDocumentBuilder{T}"/>,
/// serialize it via a rented workspace writer and buffer, and parse the bytes back) against the
/// generated <c>Create()</c> factory, which writes the document text and metadata once, directly.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkCreateParsedDocument
{
    [Benchmark(Baseline = true)]
    public bool ViaBuilderSerializeAndParse()
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
        try
        {
            person.WriteTo(writer);
            writer.Flush();

            using ParsedJsonDocument<Person> doc = ParsedJsonDocument<Person>.Parse(bufferWriter.WrittenMemory);
            return true;
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, bufferWriter);
        }
    }

    [Benchmark]
    public bool ViaCreate()
    {
        using ParsedJsonDocument<Person> doc = Person.Create(
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

        return true;
    }
}