namespace Corvus.Text.Json.JsonPath.Playground.Samples;

/// <summary>
/// A built-in sample expression with associated input data.
/// </summary>
public sealed class Sample
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string Data { get; init; }

    public required string Expression { get; init; }

    public string? FunctionsCode { get; init; }
}

/// <summary>
/// Registry of built-in sample expressions.
/// </summary>
public static class SampleRegistry
{
    private const string BookstoreData = """
        {
          "store": {
            "book": [
              {"category": "reference", "author": "Sandi Toksvig", "title": "Between the Stops", "price": 8.95},
              {"category": "fiction", "author": "Evelyn Waugh", "title": "Sword of Honour", "price": 12.99},
              {"category": "fiction", "author": "Jane Austen", "title": "Pride and Prejudice", "isbn": "0-553-21311-3", "price": 8.99},
              {"category": "fiction", "author": "J. R. R. Tolkien", "title": "The Lord of the Rings", "isbn": "0-395-19395-8", "price": 22.99}
            ],
            "bicycle": {"color": "red", "price": 399.99}
          }
        }
        """;

    public static IReadOnlyList<Sample> All { get; } =
    [
        new Sample
        {
            Id = "property-access",
            DisplayName = "Property Access",
            Data = BookstoreData,
            Expression = "$.store.bicycle.color",
        },

        new Sample
        {
            Id = "wildcard",
            DisplayName = "Wildcard",
            Data = BookstoreData,
            Expression = "$.store.book[*].author",
        },

        new Sample
        {
            Id = "recursive-descent",
            DisplayName = "Recursive Descent",
            Data = BookstoreData,
            Expression = "$..author",
        },

        new Sample
        {
            Id = "index-access",
            DisplayName = "Index Access",
            Data = BookstoreData,
            Expression = "$.store.book[0].title",
        },

        new Sample
        {
            Id = "negative-index",
            DisplayName = "Negative Index",
            Data = BookstoreData,
            Expression = "$.store.book[-1].title",
        },

        new Sample
        {
            Id = "array-slice",
            DisplayName = "Array Slice",
            Data = BookstoreData,
            Expression = "$.store.book[0:2].title",
        },

        new Sample
        {
            Id = "filter-expression",
            DisplayName = "Filter Expression",
            Data = BookstoreData,
            Expression = "$.store.book[?@.price < 10].title",
        },

        new Sample
        {
            Id = "filter-logical",
            DisplayName = "Filter (Logical)",
            Data = BookstoreData,
            Expression = "$.store.book[?@.price < 10 && @.category == 'fiction'].title",
        },

        new Sample
        {
            Id = "filter-function",
            DisplayName = "Filter Function",
            Data = BookstoreData,
            Expression = "$.store.book[?length(@.title) > 15].title",
        },

        new Sample
        {
            Id = "all-prices",
            DisplayName = "All Prices",
            Data = BookstoreData,
            Expression = "$..price",
        },

        new Sample
        {
            Id = "all-members",
            DisplayName = "All Store Members",
            Data = BookstoreData,
            Expression = "$.store.*",
        },

        new Sample
        {
            Id = "union",
            DisplayName = "Union Selector",
            Data = BookstoreData,
            Expression = "$.store.book[0,2].title",
        },

        new Sample
        {
            Id = "custom-functions",
            DisplayName = "Custom Functions",
            Data = BookstoreData,
            Expression = "$.store.book[?ceil(@.price) == 9].title",
            FunctionsCode = """
                {
                    // ceil: rounds a number up to the nearest integer
                    ["ceil"] = ValueFunction(v =>
                        Value((int)Math.Ceiling(v.GetDouble()))),

                    // is_fiction: checks if a category is "fiction"
                    ["is_fiction"] = LogicalFunction(v =>
                        v.ValueKind == JsonValueKind.String && v.ValueEquals("fiction"u8)),
                }
                """,
        },
    ];
}
