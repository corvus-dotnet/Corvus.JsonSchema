namespace Corvus.Text.Json.JMESPath.Playground.Samples;

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
            Id = "basic-expressions",
            DisplayName = "Basic Expressions",
            Data = """
            {"a": "foo", "b": "bar", "c": "baz"}
            """,
            Expression = "a",
        },

        new Sample
        {
            Id = "sub-expressions",
            DisplayName = "Sub-expressions",
            Data = """
            {"a": {"b": {"c": {"d": "value"}}}}
            """,
            Expression = "a.b.c.d",
        },

        new Sample
        {
            Id = "index-expressions",
            DisplayName = "Index Expressions",
            Data = """
            ["a", "b", "c", "d", "e", "f"]
            """,
            Expression = "[2]",
        },

        new Sample
        {
            Id = "slicing",
            DisplayName = "Slicing",
            Data = """
            [0, 1, 2, 3, 4, 5, 6, 7, 8, 9]
            """,
            Expression = "[0:5]",
        },

        new Sample
        {
            Id = "list-projections",
            DisplayName = "List Projections",
            Data = """
            {
              "people": [
                {"first": "James", "last": "d"},
                {"first": "Jacob", "last": "e"},
                {"first": "Jayden", "last": "f"},
                {"missing": "different"}
              ],
              "foo": {"bar": "baz"}
            }
            """,
            Expression = "people[*].first",
        },

        new Sample
        {
            Id = "object-projections",
            DisplayName = "Object Projections",
            Data = """
            {
              "ops": {
                "functionA": {"numArgs": 2},
                "functionB": {"numArgs": 3},
                "functionC": {"variadic": true}
              }
            }
            """,
            Expression = "ops.*.numArgs",
        },

        new Sample
        {
            Id = "flatten-projections",
            DisplayName = "Flatten Projections",
            Data = """
            {
              "reservations": [
                {
                  "instances": [
                    {"state": "running", "type": "small"},
                    {"state": "stopped", "type": "large"}
                  ]
                },
                {
                  "instances": [
                    {"state": "terminated", "type": "medium"},
                    {"state": "running", "type": "xlarge"}
                  ]
                }
              ]
            }
            """,
            Expression = "reservations[].instances[].state",
        },

        new Sample
        {
            Id = "filter-expressions",
            DisplayName = "Filter Expressions",
            Data = """
            {
              "machines": [
                {"name": "a", "state": "running"},
                {"name": "b", "state": "stopped"},
                {"name": "c", "state": "running"}
              ]
            }
            """,
            Expression = "machines[?state == 'running'].name",
        },

        new Sample
        {
            Id = "pipe-expressions",
            DisplayName = "Pipe Expressions",
            Data = """
            {
              "people": [
                {"first": "James", "last": "d"},
                {"first": "Jacob", "last": "e"},
                {"first": "Jayden", "last": "f"}
              ]
            }
            """,
            Expression = "people[*].first | [0]",
        },

        new Sample
        {
            Id = "multiselect-list",
            DisplayName = "Multiselect List",
            Data = """
            {
              "people": [
                {"name": "a", "state": {"name": "WA"}},
                {"name": "b", "state": {"name": "NY"}},
                {"name": "c", "state": {"name": "WA"}}
              ]
            }
            """,
            Expression = "people[].[name, state.name]",
        },

        new Sample
        {
            Id = "multiselect-hash",
            DisplayName = "Multiselect Hash",
            Data = """
            {
              "people": [
                {"name": "a", "state": {"name": "WA"}},
                {"name": "b", "state": {"name": "NY"}}
              ]
            }
            """,
            Expression = "people[*].{Name: name, State: state.name}",
        },

        new Sample
        {
            Id = "functions",
            DisplayName = "Functions",
            Data = """
            {
              "people": [
                {"name": "b", "age": 30},
                {"name": "a", "age": 50},
                {"name": "c", "age": 40}
              ]
            }
            """,
            Expression = "max_by(people, &age).name",
        },

        new Sample
        {
            Id = "working-with-nested-data",
            DisplayName = "Nested Data",
            Data = """
            {
              "reservations": [
                {
                  "instances": [
                    {"type": "small", "state": {"name": "running"}, "tags": [{"Key": "Name", "Values": ["Web"]}]},
                    {"type": "large", "state": {"name": "stopped"}, "tags": [{"Key": "Name", "Values": ["DB"]}]}
                  ]
                },
                {
                  "instances": [
                    {"type": "medium", "state": {"name": "terminated"}, "tags": [{"Key": "Name", "Values": ["Mon"]}]},
                    {"type": "xlarge", "state": {"name": "running"}, "tags": [{"Key": "Name", "Values": ["Mail"]}]}
                  ]
                }
              ]
            }
            """,
            Expression = "reservations[].instances[].{type: type, state: state.name, tags: tags[?Key == 'Name'].Values[] | [0]}",
        },

        new Sample
        {
            Id = "filtering-and-selecting",
            DisplayName = "Filtering and Selecting",
            Data = """
            {
              "locations": [
                {"name": "Seattle", "state": "WA"},
                {"name": "New York", "state": "NY"},
                {"name": "Bellevue", "state": "WA"},
                {"name": "Olympia", "state": "WA"}
              ]
            }
            """,
            Expression = "locations[?state == 'WA'].name | sort(@) | {WashingtonCities: join(', ', @)}",
        },
    ];
}
