namespace Corvus.Text.Json.Jsonata.Playground.Samples;

/// <summary>
/// A built-in sample expression with associated input data.
/// </summary>
public sealed class Sample
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public required string Data { get; init; }

    public required string Expression { get; init; }
}

/// <summary>
/// Registry of built-in sample expressions.
/// </summary>
public static class SampleRegistry
{
    public static IReadOnlyList<Sample> All { get; } =
    [
        new Sample
        {
            Id = "invoice-total",
            DisplayName = "Invoice Total",
            Data = """
            {
              "Account": "ACC-001",
              "Order": [
                { "Product": "Widget A", "Quantity": 4, "Price": 9.99 },
                { "Product": "Widget B", "Quantity": 2, "Price": 24.50 },
                { "Product": "Gadget C", "Quantity": 1, "Price": 199.00 }
              ]
            }
            """,
            Expression = """$sum(Order.(Price * Quantity))""",
        },

        new Sample
        {
            Id = "transform-object",
            DisplayName = "Object Transform",
            Data = """
            {
              "FirstName": "Alice",
              "LastName": "Smith",
              "Age": 30,
              "Email": "alice@example.com"
            }
            """,
            Expression = """
            {
              "name": FirstName & " " & LastName,
              "email": Email,
              "isAdult": Age >= 18
            }
            """,
        },

        new Sample
        {
            Id = "filter-array",
            DisplayName = "Array Filter",
            Data = """
            {
              "Products": [
                { "Name": "Laptop", "Price": 999, "InStock": true },
                { "Name": "Phone", "Price": 699, "InStock": false },
                { "Name": "Tablet", "Price": 449, "InStock": true },
                { "Name": "Watch", "Price": 299, "InStock": true }
              ]
            }
            """,
            Expression = """Products[InStock and Price < 500].Name""",
        },

        new Sample
        {
            Id = "string-ops",
            DisplayName = "String Operations",
            Data = """
            {
              "words": ["hello", "world", "from", "jsonata"]
            }
            """,
            Expression = """$join(words ~> $map(function($w) { $uppercase($substring($w, 0, 1)) & $substring($w, 1) }), " ")""",
        },

        new Sample
        {
            Id = "map-reduce",
            DisplayName = "Map & Reduce",
            Data = """
            {
              "Numbers": [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]
            }
            """,
            Expression = """
            {
              "evens": Numbers[$  % 2 = 0],
              "odds": Numbers[$ % 2 = 1],
              "sumOfSquares": $sum(Numbers ~> $map(function($n) { $n * $n })),
              "factorial5": $reduce([1,2,3,4,5], function($prev, $curr) { $prev * $curr })
            }
            """,
        },

        new Sample
        {
            Id = "nested-nav",
            DisplayName = "Nested Navigation",
            Data = """
            {
              "Company": {
                "Name": "Acme Corp",
                "Departments": [
                  {
                    "Name": "Engineering",
                    "Employees": [
                      { "Name": "Alice", "Role": "Lead" },
                      { "Name": "Bob", "Role": "Developer" }
                    ]
                  },
                  {
                    "Name": "Marketing",
                    "Employees": [
                      { "Name": "Charlie", "Role": "Manager" },
                      { "Name": "Diana", "Role": "Designer" }
                    ]
                  }
                ]
              }
            }
            """,
            Expression = """Company.Departments.{"dept": Name, "people": Employees.Name}""",
        },

        new Sample
        {
            Id = "date-time",
            DisplayName = "Date & Time",
            Data = """
            {
              "Events": [
                { "Name": "Launch", "Timestamp": 1672531200000 },
                { "Name": "Update", "Timestamp": 1675209600000 },
                { "Name": "Review", "Timestamp": 1677628800000 }
              ]
            }
            """,
            Expression = """Events.{ "event": Name, "date": $fromMillis(Timestamp, "[Y]-[M01]-[D01]") }""",
        },

        new Sample
        {
            Id = "conditional",
            DisplayName = "Conditional Logic",
            Data = """
            {
              "Temperature": 22,
              "Unit": "C"
            }
            """,
            Expression = """
            (
              $temp := Unit = "C" ? Temperature * 9/5 + 32 : Temperature;
              {
                "fahrenheit": $temp,
                "description": $temp < 32 ? "Freezing" :
                               $temp < 60 ? "Cold" :
                               $temp < 80 ? "Comfortable" :
                               "Hot"
              }
            )
            """,
        },
    ];
}