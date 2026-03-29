// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Benchmark.CorvusJsonSchema;
using BenchmarkDotNet.Attributes;

namespace ValidationBenchmarks;

/// <summary>
/// Construct elements from a JSON element.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkBuild
{
    [Benchmark]
    public Person BuildCorvusJsonSchema()
    {
        var person = Person.Create(
            age: 51,
            name: PersonName.Create(
                firstName: "Michael",
                lastName: "Adams",
                otherNames: ["Francis", "James"]),
            competedInYears: [2012, 2016, 2024]);

        return person;
    }

    [Benchmark]
    public Benchmark.CorvusTextJson.Person.Mutable BuildCorvusTextJson()
    {
        using var workspace = Corvus.Text.Json.JsonWorkspace.Create();

        using Corvus.Text.Json.JsonDocumentBuilder<Benchmark.CorvusTextJson.Person.Mutable> person = Benchmark.CorvusTextJson.Person.CreateBuilder(
            workspace,
            (ref b) => b.Create(
                age: 51,
                name: Benchmark.CorvusTextJson.PersonName.Build(static (ref personName) =>
                {
                    personName.Create(
                        firstName: "Michael"u8,
                        lastName: "Adams"u8,
                        otherNames: Benchmark.CorvusTextJson.OtherNames.Build(static (ref otherNames) =>
                        {
                            otherNames.AddItem("Francis"u8);
                            otherNames.AddItem("James"u8);
                        }));
                }),
                competedInYears: Benchmark.CorvusTextJson.CompetedInYears.Build([2012, 2106, 2024])));

        return person.RootElement;
    }

    [Benchmark(Baseline = true)]
    public System.Text.Json.Nodes.JsonObject BuildJsonObject()
    {
        System.Text.Json.Nodes.JsonObject jsonObject =
        [
            new ("age", 51),
            new ("name",
            new System.Text.Json.Nodes.JsonObject([
                new ("firstName", "Michael"),
                new ("lastName", "Adams"),
                new ("otherNames", new System.Text.Json.Nodes.JsonArray("Francis", "James")),
            new ("competedInYears", new System.Text.Json.Nodes.JsonArray(2012, 2016, 2024))])),
        ];

        return jsonObject;
    }
}