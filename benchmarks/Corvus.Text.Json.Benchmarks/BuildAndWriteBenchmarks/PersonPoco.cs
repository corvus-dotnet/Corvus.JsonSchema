// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure

namespace BuildAndWriteBenchmarks;

#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// A hand-crafted POCO equivalent to the Person JSON schema,
/// for benchmarking System.Text.Json serialization.
/// </summary>
public sealed class PersonPoco
{
    public int Age { get; set; }

    public PersonNamePoco? Name { get; set; }

    public int[]? CompetedInYears { get; set; }
}

/// <summary>
/// A hand-crafted POCO equivalent to the PersonName JSON schema.
/// </summary>
public sealed class PersonNamePoco
{
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string[]? OtherNames { get; set; }
}

/// <summary>
/// Source-generated JSON serializer context for PersonPoco.
/// </summary>
[JsonSerializable(typeof(PersonPoco))]
internal partial class PersonPocoContext : JsonSerializerContext
{
}