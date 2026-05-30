namespace Corvus.Text.Json.Toon.Playground.Samples;

/// <summary>
/// A built-in sample with associated description.
/// </summary>
public sealed class Sample
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string Content { get; init; }

    public required string Description { get; init; }
}

/// <summary>
/// Registry of built-in samples for both TOON→JSON and JSON→TOON directions.
/// </summary>
public static class SampleRegistry
{
    public static IReadOnlyList<Sample> ToonSamples { get; } =
    [
        new Sample
        {
            Id = "basic-object",
            DisplayName = "Basic Object",
            Description = "Simple key-value pairs showing TOON object syntax.",
            Content = """
            name: Alice
            age: 30
            active: true
            """,
        },

        new Sample
        {
            Id = "nested-object",
            DisplayName = "Nested Object",
            Description = "Indentation represents nested objects without JSON braces.",
            Content = """
            user:
              id: 123
              profile:
                name: Alice
                email: alice@example.com
              tags[3]: admin,beta,reader
            """,
        },

        new Sample
        {
            Id = "tabular-array",
            DisplayName = "Tabular Array",
            Description = "Uniform arrays of objects are represented as compact table rows.",
            Content = """
            users[3]{id,name,active}:
              1,Alice,true
              2,Bob,false
              3,Charlie,true
            """,
        },

        new Sample
        {
            Id = "path-expansion",
            DisplayName = "Path Expansion",
            Description = "Enable safe path expansion to turn dotted root keys into nested JSON objects.",
            Content = """
            user.name: Alice
            user.email: alice@example.com
            user.roles[2]: admin,reader
            """,
        },

        new Sample
        {
            Id = "pipe-delimited",
            DisplayName = "Pipe Delimited",
            Description = "TOON supports comma, pipe, and tab delimiters for inline arrays and tables.",
            Content = """
            users[2|]{id|name|note}:
              1|Alice|likes commas, safely
              2|Bob|uses pipes
            """,
        },

        new Sample
        {
            Id = "expanded-list",
            DisplayName = "Expanded List Items",
            Description = "Expanded list items are useful for arrays containing nested objects.",
            Content = """
            orders[2]:
              - id: A001
                customer: Alice
                total: 123.45
              - id: B002
                customer: Bob
                total: 67.89
            """,
        },
    ];

    public static IReadOnlyList<Sample> JsonSamples { get; } =
    [
        new Sample
        {
            Id = "basic-json",
            DisplayName = "Basic Object",
            Description = "A small JSON object converted to TOON key-value pairs.",
            Content = """
            {
              "name": "Alice",
              "age": 30,
              "active": true
            }
            """,
        },

        new Sample
        {
            Id = "uniform-array",
            DisplayName = "Uniform Object Array",
            Description = "Uniform arrays of objects become compact TOON tables.",
            Content = """
            {
              "users": [
                { "id": 1, "name": "Alice", "active": true },
                { "id": 2, "name": "Bob", "active": false },
                { "id": 3, "name": "Charlie", "active": true }
              ]
            }
            """,
        },

        new Sample
        {
            Id = "nested-json",
            DisplayName = "Nested JSON",
            Description = "Nested objects and arrays converted to indentation and inline arrays.",
            Content = """
            {
              "order": {
                "id": "A001",
                "customer": {
                  "name": "Alice",
                  "region": "EMEA"
                },
                "items": [
                  { "sku": "PEN", "quantity": 12 },
                  { "sku": "PAD", "quantity": 3 }
                ]
              }
            }
            """,
        },

        new Sample
        {
            Id = "key-folding",
            DisplayName = "Key Folding",
            Description = "Enable safe key folding to collapse single-child object paths into dotted keys.",
            Content = """
            {
              "user": {
                "profile": {
                  "name": "Alice"
                }
              },
              "active": true
            }
            """,
        },
    ];
}