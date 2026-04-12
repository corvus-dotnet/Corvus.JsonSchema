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

    /// <summary>
    /// Gets C# source code for the Bindings panel. When non-null, this is compiled
    /// via Roslyn into a <c>Dictionary&lt;string, JsonataBinding&gt;</c> at runtime.
    /// Must be a dictionary initializer body (the <c>{ ... }</c> part).
    /// </summary>
    public string? BindingsCode { get; init; }
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
              "Account": {
                "Account Name": "Firefly",
                "Order": [
                  {
                    "OrderID": "order103",
                    "Product": [
                      {
                        "Product Name": "Bowler Hat",
                        "ProductID": 858383,
                        "SKU": "0406654608",
                        "Description": {
                          "Colour": "Purple",
                          "Width": 300,
                          "Height": 200,
                          "Depth": 210,
                          "Weight": 0.75
                        },
                        "Price": 34.45,
                        "Quantity": 2
                      },
                      {
                        "Product Name": "Trilby hat",
                        "ProductID": 858236,
                        "SKU": "0406634348",
                        "Description": {
                          "Colour": "Orange",
                          "Width": 300,
                          "Height": 200,
                          "Depth": 210,
                          "Weight": 0.6
                        },
                        "Price": 21.67,
                        "Quantity": 1
                      }
                    ]
                  },
                  {
                    "OrderID": "order104",
                    "Product": [
                      {
                        "Product Name": "Bowler Hat",
                        "ProductID": 858383,
                        "SKU": "040657863",
                        "Description": {
                          "Colour": "Purple",
                          "Width": 300,
                          "Height": 200,
                          "Depth": 210,
                          "Weight": 0.75
                        },
                        "Price": 34.45,
                        "Quantity": 4
                      },
                      {
                        "ProductID": 345664,
                        "SKU": "0406654603",
                        "Product Name": "Cloak",
                        "Description": {
                          "Colour": "Black",
                          "Width": 30,
                          "Height": 20,
                          "Depth": 210,
                          "Weight": 2
                        },
                        "Price": 107.99,
                        "Quantity": 1
                      }
                    ]
                  }
                ]
              }
            }
            """,
            Expression = """$sum(Account.Order.Product.(Price * Quantity))""",
        },

        new Sample
        {
            Id = "address-transform",
            DisplayName = "Address Transform",
            Data = """
            {
              "FirstName": "Fred",
              "Surname": "Smith",
              "Age": 28,
              "Address": {
                "Street": "Hursley Park",
                "City": "Winchester",
                "Postcode": "SO21 2JN"
              },
              "Phone": [
                { "type": "home", "number": "0203 544 1234" },
                { "type": "office", "number": "01962 001234" },
                { "type": "office", "number": "01962 001235" },
                { "type": "mobile", "number": "077 7700 1234" }
              ],
              "Email": [
                {
                  "type": "office",
                  "address": ["fred.smith@my-work.com", "fsmith@my-work.com"]
                },
                {
                  "type": "home",
                  "address": ["freddy@my-social.com", "frederic.smith@very-serious.com"]
                }
              ],
              "Other": {
                "Over 18 ?": true,
                "Misc": null,
                "Alternative.Address": {
                  "Street": "Brick Lane",
                  "City": "London",
                  "Postcode": "E1 6RF"
                }
              }
            }
            """,
            Expression = """
            {
              "name": FirstName & " " & Surname,
              "mobile": Phone[type = "mobile"].number
            }
            """,
        },

        new Sample
        {
            Id = "library-join",
            DisplayName = "Library Join",
            Data = """
            {
              "library": {
                "books": [
                  {
                    "title": "Structure and Interpretation of Computer Programs",
                    "authors": ["Abelson", "Sussman"],
                    "isbn": "9780262510875",
                    "price": 38.90,
                    "copies": 2
                  },
                  {
                    "title": "The C Programming Language",
                    "authors": ["Kernighan", "Richie"],
                    "isbn": "9780131103627",
                    "price": 33.59,
                    "copies": 3
                  },
                  {
                    "title": "The AWK Programming Language",
                    "authors": ["Aho", "Kernighan", "Weinberger"],
                    "isbn": "9780201079814",
                    "copies": 1
                  },
                  {
                    "title": "Compilers: Principles, Techniques, and Tools",
                    "authors": ["Aho", "Lam", "Sethi", "Ullman"],
                    "isbn": "9780201100884",
                    "price": 23.38,
                    "copies": 1
                  }
                ],
                "loans": [
                  { "customer": "10001", "isbn": "9780262510875", "return": "2016-12-05" },
                  { "customer": "10003", "isbn": "9780201100884", "return": "2016-10-22" },
                  { "customer": "10003", "isbn": "9780262510875", "return": "2016-12-22" }
                ],
                "customers": [
                  {
                    "id": "10001",
                    "name": "Joe Doe",
                    "address": { "street": "2 Long Road", "city": "Winchester", "postcode": "SO22 5PU" }
                  },
                  {
                    "id": "10002",
                    "name": "Fred Bloggs",
                    "address": { "street": "56 Letsby Avenue", "city": "Winchester", "postcode": "SO22 4WD" }
                  },
                  {
                    "id": "10003",
                    "name": "Jason Arthur",
                    "address": { "street": "1 Preddy Gate", "city": "Southampton", "postcode": "SO14 0MG" }
                  }
                ]
              }
            }
            """,
            Expression = """
            library.loans@$L.books@$B[$L.isbn=$B.isbn].customers[$L.customer=id].{
              "customer": name,
              "book": $B.title,
              "due": $L.return
            }
            """,
        },

        new Sample
        {
            Id = "schema-keys",
            DisplayName = "Schema Keys",
            Data = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "required": ["Account"],
              "type": "object",
              "properties": {
                "Account": {
                  "required": ["Order"],
                  "type": "object",
                  "properties": {
                    "Account Name": { "type": "string" },
                    "Order": {
                      "type": "array",
                      "items": {
                        "required": ["OrderID", "Product"],
                        "type": "object",
                        "properties": {
                          "OrderID": { "type": "string" },
                          "Product": {
                            "type": "array",
                            "items": {
                              "required": ["ProductID", "Product Name", "Price", "Quantity"],
                              "type": "object",
                              "properties": {
                                "ProductID": { "type": "integer" },
                                "Product Name": { "type": "string" },
                                "SKU": { "type": "string" },
                                "Price": { "type": "number" },
                                "Quantity": { "type": "integer" }
                              }
                            }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """,
            Expression = """**.properties ~> $keys()""",
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

        new Sample
        {
            Id = "bindings",
            DisplayName = "Bindings",
            Data = """
            {
              "angle": 60
            }
            """,
            Expression = """
            $cosine(angle * $pi / 180)

            /*
            JSONata can be extended by binding variables to external
            functions and values.

            The Bindings panel contains C# code that defines a dictionary
            of JsonataBinding entries. Value bindings (like $pi) make a
            constant available; function bindings (like $cosine) let you
            call .NET methods from your expression.

            Helper functions available in bindings code:
              Value(double|string|bool)  — create a value binding
              Function(func, paramCount) — create a function binding
              ToElement(double|string|bool) — convert .NET value to JsonElement
            */
            """,
            BindingsCode = """
            {
                ["pi"] = Value(3.1415926535898),
                ["cosine"] = Function(
                    (args, ws) => ToElement(Math.Cos(args[0].GetDouble())), 1),
            }
            """,
        },
    ];
}