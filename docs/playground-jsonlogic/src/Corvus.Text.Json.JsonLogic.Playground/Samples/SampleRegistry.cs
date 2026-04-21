namespace Corvus.Text.Json.JsonLogic.Playground.Samples;

/// <summary>
/// A built-in sample with rule and data.
/// </summary>
public sealed class Sample
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string Description { get; init; }

    public required string Rule { get; init; }

    public required string Data { get; init; }
}

/// <summary>
/// Registry of built-in sample JsonLogic rules.
/// </summary>
public static class SampleRegistry
{
    public static IReadOnlyList<Sample> All { get; } =
    [
        new Sample
        {
            Id = "simple-comparison",
            DisplayName = "Simple Comparison",
            Description = "Check if age is greater than 18 using the > operator and var to read data.",
            Rule = """
            {">":[{"var":"age"}, 18]}
            """,
            Data = """
            {"age": 25}
            """,
        },

        new Sample
        {
            Id = "if-then-else",
            DisplayName = "If / Then / Else",
            Description = "Assign a letter grade based on score using chained if conditions.",
            Rule = """
            {"if":[
              {">=":[{"var":"score"}, 90]}, "A",
              {">=":[{"var":"score"}, 80]}, "B",
              {">=":[{"var":"score"}, 70]}, "C",
              {">=":[{"var":"score"}, 60]}, "D",
              "F"
            ]}
            """,
            Data = """
            {"score": 85}
            """,
        },

        new Sample
        {
            Id = "arithmetic",
            DisplayName = "Arithmetic",
            Description = "Calculate the total price from unit price, quantity, and discount.",
            Rule = """
            {"-":[
              {"*":[{"var":"price"}, {"var":"quantity"}]},
              {"var":"discount"}
            ]}
            """,
            Data = """
            {"price": 25.50, "quantity": 4, "discount": 10}
            """,
        },

        new Sample
        {
            Id = "logical-and",
            DisplayName = "Logical AND / OR",
            Description = "Check if a person is a working-age adult (over 18 AND under 65).",
            Rule = """
            {"and":[
              {">":[{"var":"age"}, 18]},
              {"<":[{"var":"age"}, 65]}
            ]}
            """,
            Data = """
            {"age": 35}
            """,
        },

        new Sample
        {
            Id = "equality",
            DisplayName = "Equality Check",
            Description = "Check if the user's role equals \"admin\" using strict equality.",
            Rule = """
            {"===":[{"var":"role"}, "admin"]}
            """,
            Data = """
            {"role": "admin", "name": "Alice"}
            """,
        },

        new Sample
        {
            Id = "between",
            DisplayName = "Between (3-arg comparison)",
            Description = "Check if temperature is between 18 and 25 using a 3-argument < operator.",
            Rule = """
            {"<=":[18, {"var":"temp"}, 25]}
            """,
            Data = """
            {"temp": 22}
            """,
        },

        new Sample
        {
            Id = "missing-fields",
            DisplayName = "Missing Fields",
            Description = "Check which required fields are missing from the data.",
            Rule = """
            {"missing":["first_name", "last_name", "email"]}
            """,
            Data = """
            {"first_name": "Alice", "email": "alice@example.com"}
            """,
        },

        new Sample
        {
            Id = "array-filter",
            DisplayName = "Array Filter",
            Description = "Filter products to find those priced above $20.",
            Rule = """
            {"filter":[
              {"var":"products"},
              {">":[{"var":"price"}, 20]}
            ]}
            """,
            Data = """
            {
              "products": [
                {"name": "Widget", "price": 9.99},
                {"name": "Gadget", "price": 29.99},
                {"name": "Gizmo", "price": 49.99},
                {"name": "Thing", "price": 4.99}
              ]
            }
            """,
        },

        new Sample
        {
            Id = "array-map",
            DisplayName = "Array Map",
            Description = "Transform an array of names into greeting strings using cat.",
            Rule = """
            {"map":[
              {"var":"names"},
              {"cat":["Hello, ", {"var":""}]}
            ]}
            """,
            Data = """
            {"names": ["Alice", "Bob", "Charlie"]}
            """,
        },

        new Sample
        {
            Id = "array-reduce",
            DisplayName = "Array Reduce",
            Description = "Sum all numbers in an array using reduce with an accumulator.",
            Rule = """
            {"reduce":[
              {"var":"numbers"},
              {"+":[{"var":"current"}, {"var":"accumulator"}]},
              0
            ]}
            """,
            Data = """
            {"numbers": [1, 2, 3, 4, 5]}
            """,
        },

        new Sample
        {
            Id = "string-contains",
            DisplayName = "String Contains",
            Description = "Check if an email address contains the domain \"example.com\".",
            Rule = """
            {"in":["example.com", {"var":"email"}]}
            """,
            Data = """
            {"email": "alice@example.com"}
            """,
        },

        new Sample
        {
            Id = "complex-business-rule",
            DisplayName = "Business Rule",
            Description = "Determine shipping tier: free for orders over $100 or premium members, otherwise standard or express based on total.",
            Rule = """
            {"if":[
              {"or":[
                {">=":[{"var":"order.total"}, 100]},
                {"===":[{"var":"customer.tier"}, "premium"]}
              ]},
              "free",
              {">=":[{"var":"order.total"}, 50]},
              "standard",
              "express"
            ]}
            """,
            Data = """
            {
              "customer": {"name": "Alice", "tier": "standard"},
              "order": {"total": 75.00, "items": 3}
            }
            """,
        },
    ];
}
